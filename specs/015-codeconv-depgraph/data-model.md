# Data Model: 015-codeconv-depgraph

## TL;DR — three new tables + five new tombstone YAML keys

This feature introduces **two normative new tables** in the existing `codeconv` schema, plus **one optional traceability table** (decided in research § R5), plus **five new YAML keys** appended to the existing tombstone frontmatter field order. No change to any existing column, row shape, or constraint. The data model from `specs/012-codeconv-runner/data-model.md` and the (no-change) delta from `specs/014-package-self-import-resolution/data-model.md` are preserved verbatim.

## 1. New tables (all under `codeconv` schema)

### 1.1 `codeconv.dart_depgraph` — ordering + readiness per file

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `path` | text | PRIMARY KEY, FK → `codeconv.dart_files(path)` ON DELETE CASCADE | Subtree-relative POSIX path; same shape as `dart_files.path`. |
| `topo_level` | integer | NOT NULL CHECK (topo_level >= 0) | 0 = leaves (no in-subtree dependencies, or whose dependency-SCCs are all at level < this row's SCC). Higher = depends on more layers. |
| `cycle_group_id` | integer | NOT NULL | Tarjan SCC id. Singletons get unique values; multi-file SCCs share a value. **NOT NULL** per spec FR-005 (the spec's Key Entities line listing `int NULL` is corrected to `NOT NULL` to align with FR-005's authoritative text; see analyze remediations). |
| `ready` | boolean | NOT NULL | Convenience boolean equal to `status = 'ready'`. Indexed for the common-case query. |
| `status` | text | NOT NULL CHECK (status IN ('pending','ready','in_progress','converted')) | See § 3 lifecycle. |
| `dependency_count` | integer | NOT NULL CHECK (dependency_count >= 0) | Count of in-subtree dependencies (rows in `dart_imports` where `from_path = this.path`). Renamed from spec Key Entities' `in_degree` for clarity (see analyze remediations). |
| `caller_count` | integer | NOT NULL CHECK (caller_count >= 0) | Count of in-subtree callers (rows in `dart_imports` where `to_path = this.path`). Renamed from `out_degree`. |
| `computed_at` | timestamptz | NOT NULL DEFAULT NOW() | When this row was written. |
| `depgraph_run_id` | uuid | NULL, FK → `codeconv.depgraph_runs(id)` | The compute run that produced this row (provenance). |
| `discover_run_id` | uuid | NULL, FK → `codeconv.discover_runs(id)` | The inventory state this row was computed against. |

**Index**: `CREATE INDEX dart_depgraph_ready_idx ON codeconv.dart_depgraph (ready) WHERE ready;` — speeds the SC-005 "show me what's ready" query.

**Atomic-per-run write contract** (FR-008): every `compute` invocation runs `DELETE FROM codeconv.dart_depgraph;` followed by a bulk `INSERT ... ON CONFLICT (path) DO UPDATE` of all 128 rows inside one transaction. A crash mid-write leaves either the previous run's data intact (transaction rolled back) or the new run's data intact (committed). No partial state is observable.

### 1.2 `codeconv.dart_conversions` — two-phase conversion state per file

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `path` | text | PRIMARY KEY, FK → `codeconv.dart_files(path)` ON DELETE CASCADE | Subtree-relative POSIX path. |
| `started_at` | timestamptz | NOT NULL | Set by `mark-started`. |
| `completed_at` | timestamptz | NULL | Set by `mark-completed`. NULL ⇒ still `in_progress`. |
| `sha256_of_dart_at_start` | text | NOT NULL | Snapshot of `dart_files.sha256` at `mark-started` time. Lets a future query detect post-completion source drift. |
| `target_path` | text | NULL | Relative path to the produced C# / .NET artefact, written at `mark-completed`. Spec FR-006a allows the writer to leave it NULL if not yet known. |
| `marked_started_run_id` | uuid | NULL, FK → `codeconv.depgraph_runs(id)` | Provenance: which `mark-started` invocation wrote this row. |
| `marked_completed_run_id` | uuid | NULL, FK → `codeconv.depgraph_runs(id)` | Provenance: which `mark-completed` invocation flipped `completed_at` non-NULL. |

**Lifecycle** (FR-006a / spec § Key Entities):

- Row absent ⇒ `status='pending'` in `dart_depgraph`.
- Row present with `completed_at IS NULL` ⇒ `status='in_progress'`.
- Row present with `completed_at IS NOT NULL` ⇒ `status='converted'`.
- (No DELETE workflow in v1 — once a file is marked, the row stays.)

**Idempotence**: `mark-started` on an already-started row is a no-op + warning. `mark-completed` on an already-completed row is a no-op + warning. `mark-completed` on a never-started row is an error (does NOT auto-create the row — the user must call `mark-started` first).

### 1.3 `codeconv.depgraph_runs` — per-invocation traceability (mirrors `discover_runs`)

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `id` | uuid | PRIMARY KEY | One per CLI invocation. |
| `started_at` | timestamptz | NOT NULL | |
| `completed_at` | timestamptz | NULL | Set on successful exit; NULL on crash. |
| `mode` | text | NOT NULL CHECK (mode IN ('compute','mark-started','mark-completed','stamp-tombstones','rebuild-conversions-from-tombstones')) | Which subcommand. |
| `files_total` | integer | NULL | For `compute`/`stamp-tombstones`; NULL for the others. |
| `ready_count` | integer | NULL | For `compute`. |
| `in_progress_count` | integer | NULL | For `compute`. |
| `converted_count` | integer | NULL | For `compute`. |
| `cycle_count` | integer | NULL | For `compute`. Count of multi-file SCCs (singletons not counted; matches FR-005 metric). |
| `warnings` | jsonb | NOT NULL DEFAULT '[]'::jsonb | Same shape as `discover_runs.warnings`. |

## 2. Tombstone YAML frontmatter — five new keys (appended)

The existing field order tuple in `codeconv/src/codeconv/tools/discover/tombstone.py::_FIELD_ORDER` is:

```python
_FIELD_ORDER = ("path", "name", "purpose", "key_idea", "dependencies", "callers", "mtime", "sha256")
```

After this feature, the tuple becomes:

```python
_FIELD_ORDER = (
    "path", "name", "purpose", "key_idea", "dependencies", "callers", "mtime", "sha256",
    # --- feature 015 (codeconv-depgraph) appended fields ---
    "topo_level",
    "cycle_group_id",
    "status",
    "conversion_started_at",
    "conversion_completed_at",
)
```

| Key | Source table | Type in YAML | Null/missing semantics |
|---|---|---|---|
| `topo_level` | `dart_depgraph.topo_level` | integer | Missing key ⇒ depgraph never computed for this file. Reader-side default: treat as "unknown" (NOT zero). |
| `cycle_group_id` | `dart_depgraph.cycle_group_id` | integer | Same null/missing semantics as `topo_level`. |
| `status` | `dart_depgraph.status` | string (one of pending/ready/in_progress/converted) | Missing ⇒ depgraph never computed. |
| `conversion_started_at` | `dart_conversions.started_at` | ISO8601 string with 'Z' suffix (UTC) | Missing ⇒ never started. Emitted as YAML `null` when the row exists but the value is somehow NULL (defensive — should not occur given the NOT NULL constraint). |
| `conversion_completed_at` | `dart_conversions.completed_at` | ISO8601 string with 'Z' suffix (UTC) | Missing or YAML `null` ⇒ not yet completed. Distinguishes pre-feature tombstones (missing key) from post-feature-but-not-yet-completed (key present, value null). |

**Null vs missing convention**: a key is OMITTED from the frontmatter when there is no row to source the value from (i.e. depgraph has never been computed; conversion has never been started). A key is PRESENT with value `null` when the row exists but the specific field is NULL (i.e. `conversion_completed_at` for an in-progress conversion). This lets the `rebuild-conversions-from-tombstones` subcommand distinguish "no record" from "record exists but incomplete".

**Idempotence proof** (SC-002 / SC-007 carry-forward): YAML emitter settings are unchanged from feature 012 (`tombstone.py::_YAML_DUMP_KWARGS`: `default_flow_style=False, sort_keys=False, allow_unicode=True, width=10000`). The field order is enforced by `_canonicalise(fields)`. Appended keys appear in the same position every run. Lists stay sorted lexicographically. A re-stamp on unchanged data produces byte-identical files.

## 3. Status state machine

```text
              mark-started                  mark-completed
   pending ───────────────►  in_progress ───────────────►  converted
       │                          │
       │                          └── (no transition back to pending in v1)
       │
       └── (transition to 'ready' is computed, not marked — see § 4)
```

The `ready` value is derived (FR-006). A file is `ready` iff it is `pending` AND every SCC-external in-subtree dependency is `converted`. SCC members are ready as a batch when all their SCC-external dependencies are converted.

## 4. Diff against feature 012's data model

| Section of `specs/012-codeconv-runner/data-model.md` | This feature changes anything? | Note |
|---|---|---|
| 1.1 `codeconv.dart_files` | NO | Read-only consumer. |
| 1.2 `codeconv.dart_imports` | NO | Read-only consumer. |
| 1.3 `codeconv.dart_callers` | NO | Not directly consumed (the depgraph computes from `dart_imports`); read only if a future report needs caller fan-in. |
| 1.4 `codeconv.dart_files_orphaned` | NO | Explicitly excluded from depgraph membership (spec edge case § "Orphaned files"). |
| 1.5 `codeconv.discover_runs` | NO | Referenced via `dart_depgraph.discover_run_id` FK. |
| 2 `.pgdb/bridge.json` | NO | Bridge lifecycle untouched. |
| 3 `.pgdb/.migration-record.json` | NO | Migration tool untouched. |
| 4.1 Tombstone YAML frontmatter | **APPENDS five keys** — see § 2 above. Field set extended; pre-existing keys/order unchanged. |
| 4.4 `.codeconv/tombstones/.orphaned/...` | NO | Orphan tree untouched (orphans don't appear in depgraph; their tombstones are not stamped). |
| 4.5 `.pgdb.bridge.lock/` | NO | Bridge lock untouched. |
| 5 State transitions | **EXTENDS** — adds the conversion state machine in § 3 above. Discover's transitions unchanged. |
| 7 D2NET schemas (`public.*`) | NO | D2NET schemas untouched (FR-007 / SC-007 carry-forward). |

## 5. New on-disk artefacts

| Path | Status | Lifetime | Reason |
|---|---|---|---|
| `.codeconv/depgraph.json` | NEW (gitignored, R10) | Per-`compute`-invocation | Developer-local "what should I convert next?" answer; recomputable. |
| `.codeconv/tombstones/<rel>.dart.md` | MODIFIED (checked in) | Persistent | Carries five new keys; durable round-trip source for depgraph + conversion state. |

## 6. Verification

After this feature lands and `/codeconv-depgraph` is run on `glp_runtime_net/`:

- `\d codeconv.dart_depgraph`, `\d codeconv.dart_conversions`, `\d codeconv.depgraph_runs` against the live PGLite cluster must produce the column-set + constraints listed in § 1.1, 1.2, 1.3.
- `\d codeconv.dart_files`, `\d codeconv.dart_imports`, `\d codeconv.dart_callers`, `\d codeconv.dart_files_orphaned`, `\d codeconv.discover_runs` against the live PGLite cluster must produce byte-identical output to the pre-feature snapshot (zero change to feature-012/-014 surfaces).
- A diff of `.codeconv/tombstones/<file>.dart.md` frontmatter keys against the pre-feature snapshot shows exactly the five appended keys, in the order specified in § 2, in every tombstone.
- `\dn codeconv` shows exactly one schema (codeconv); `\dn public` and `\dn dbos` are untouched (SC-007).
