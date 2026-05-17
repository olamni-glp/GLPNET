# Data Model: 017-conversion-plan-agents

## TL;DR — one new table (+ one optional) + four new tombstone YAML keys

This feature introduces **one normative new table** in the existing `codeconv` schema (`codeconv.dart_plans`), **one optional traceability table** (`codeconv.planagents_runs`, decided in research R8 — parity with feature-015 `depgraph_runs`), and **four new YAML keys** appended to the existing tombstone frontmatter field order, AFTER feature-015's six. No change to any existing column, row shape, or constraint. The data models from `specs/012-codeconv-runner/data-model.md` and `specs/015-codeconv-depgraph/data-model.md` are preserved verbatim.

## 1. New tables (all under `codeconv` schema)

### 1.1 `codeconv.dart_plans` — two-phase conversion-plan state per file (FR-012)

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `path` | text | PRIMARY KEY, FK → `codeconv.dart_files(path)` ON DELETE CASCADE | Subtree-relative POSIX path; same shape as `dart_files.path` / `dart_conversions.path`. |
| `plan_started_at` | timestamptz | NOT NULL | Set when the orchestrator records `plan-started` (a planning sub-agent was dispatched for this tombstone). |
| `plan_completed_at` | timestamptz | NULL | Set by `plan-completed`. NULL ⇒ `plan_in_progress`. |
| `sha256_of_dart_at_plan_start` | text | NOT NULL | Snapshot of `dart_files.sha256` at `plan-started`. Drives source-drift / stale detection (FR-015). |
| `plan_path` | text | NULL | Relative path to the produced conversion-plan artefact (default `.codeconv/conversion-plans/<rel>.dart.md`). NULL until the artefact path is recorded. |
| `open_escalation_count` | integer | NOT NULL DEFAULT 0, CHECK (open_escalation_count >= 0) | Number of unresolved escalations in this file's artefact. `> 0` ⇒ conversion-blocked (FR-017), planning-frontier still advances (FR-004/FR-017). |
| `plan_run_id` | uuid | NULL, FK → `codeconv.planagents_runs(id)` | Provenance: the orchestrator run that last wrote this row. NULL-able so the table is usable even if `planagents_runs` is not created. |

**Lifecycle** (FR-012 / spec § Key Entities):

- Row **absent** ⇒ `plan_pending` (no plan attempt yet).
- Row present, `plan_completed_at IS NULL` ⇒ `plan_in_progress` (agent dispatched, not finished — does NOT unblock downstream, FR-004).
- Row present, `plan_completed_at IS NOT NULL` ⇒ `planned` (counts for the planning frontier even if `open_escalation_count > 0`, FR-017).
- No DELETE workflow in v1. `--replan` UPDATEs an existing row in place (new `plan_started_at`, new SHA, `plan_completed_at` reset to NULL) and the artefact records a "superseded prior escalations" note (R9) — the row is never deleted.

**Idempotence** (FR-014): `plan-started` on an existing not-completed row is a no-op + warning (idempotent recovery of a crashed agent — the file stays `plan_in_progress`, resumable). `plan-completed` on an already-completed row is a no-op + warning. `plan-completed` on a never-started row is an error (the orchestrator must record `plan-started` first — no auto-create).

**Write protocol** (carry-forward feature-012 FR-026/-027): per-operation, never a bulk wipe — no `DELETE FROM dart_plans` (unlike feature-015's `dart_depgraph` atomic-per-run; `dart_plans` is an accumulating lifecycle table like `dart_conversions`, not a recomputed projection). `plan-started` ⇒ `INSERT … ON CONFLICT (path) DO NOTHING` (then warn if the row pre-existed — idempotent crashed-agent recovery, FR-014; matches `contracts/planagents_schema.md` write protocol, T015, and §Idempotence above). `plan-completed` ⇒ `UPDATE … WHERE plan_completed_at IS NULL`. Only `--replan` and `rebuild-plans-from-tombstones` ⇒ `INSERT … ON CONFLICT (path) DO UPDATE` (R9 in-place supersede / DB-wipe recovery).

### 1.2 `codeconv.planagents_runs` — per-invocation traceability (optional; mirrors `depgraph_runs`)

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `id` | uuid | PRIMARY KEY | One per `codeconv planagents` orchestrator-affecting invocation. |
| `started_at` | timestamptz | NOT NULL | |
| `completed_at` | timestamptz | NULL | Set on successful exit; NULL on crash. |
| `mode` | text | NOT NULL CHECK (mode IN ('status','next','plan-started','plan-completed','aggregate-escalations','stamp-tombstones','rebuild-plans-from-tombstones')) | Which subcommand. |
| `files_total` | integer | NULL | For `status`/`stamp-tombstones`. |
| `plan_ready_count` | integer | NULL | For `status`/`next`. |
| `plan_in_progress_count` | integer | NULL | For `status`. |
| `planned_count` | integer | NULL | For `status`. |
| `open_escalations_total` | integer | NULL | For `status`/`aggregate-escalations`. |
| `warnings` | jsonb | NOT NULL DEFAULT '[]'::jsonb | Same shape as `discover_runs.warnings` / `depgraph_runs.warnings`. |

## 2. Tombstone YAML frontmatter — four new keys (appended after feature-015's six)

The field-order tuple in `codeconv/src/codeconv/tools/discover/tombstone.py::_FIELD_ORDER` is, post-feature-015:

```python
_FIELD_ORDER = (
    "path", "name", "purpose", "key_idea", "dependencies", "callers", "mtime", "sha256",
    # --- feature 015 (codeconv-depgraph) appended fields ---
    "topo_level", "cycle_group_id", "status",
    "conversion_started_at", "conversion_completed_at", "target_path",
)
```

After this feature it becomes:

```python
_FIELD_ORDER = (
    "path", "name", "purpose", "key_idea", "dependencies", "callers", "mtime", "sha256",
    # --- feature 015 (codeconv-depgraph) appended fields ---
    "topo_level", "cycle_group_id", "status",
    "conversion_started_at", "conversion_completed_at", "target_path",
    # --- feature 017 (codeconv-planagents) appended fields ---
    "plan_started_at",
    "plan_completed_at",
    "plan_path",
    "open_escalation_count",
)
```

| Key | Source column | YAML type | Null/missing semantics |
|---|---|---|---|
| `plan_started_at` | `dart_plans.plan_started_at` | ISO8601 string, 'Z' UTC suffix | **Missing** ⇒ no `dart_plans` row (never planned). |
| `plan_completed_at` | `dart_plans.plan_completed_at` | ISO8601 string, 'Z' UTC suffix | **Missing** ⇒ no row. **`null`** ⇒ row exists, plan in progress (distinguishes never-planned from planned-not-finished). |
| `plan_path` | `dart_plans.plan_path` | string OR YAML-null | **Missing** ⇒ no row. **`null`** ⇒ row exists, artefact path not yet recorded. Else the artefact's relative path. |
| `open_escalation_count` | `dart_plans.open_escalation_count` | integer | **Missing** ⇒ no row. Present integer (≥0) when the row exists; `0` ⇒ no open escalations. |

**Null vs missing convention** (identical to feature 015): a key is OMITTED when there is no `dart_plans` row to source it. A key is PRESENT with `null` when the row exists but the specific field is NULL (`plan_completed_at`, `plan_path` for an in-progress plan). This lets `rebuild-plans-from-tombstones` distinguish "no plan record" from "plan record exists but incomplete".

**Idempotence proof** (SC-003 / carry-forward feature-012 SC-008 / feature-015 idempotence): YAML emitter settings are unchanged (`tombstone.py::_YAML_DUMP_KWARGS`). Field order is enforced by `_canonicalise(fields)`. The four keys are append-only at the END of `_FIELD_ORDER`, so the position of all 14 pre-existing keys is unchanged; lists stay sorted lexicographically; a re-stamp on unchanged data produces byte-identical files.

## 3. Plan-state machine

```text
            plan-started                     plan-completed
 plan_pending ──────────────► plan_in_progress ──────────────► planned
   (no row)                      (row, completed_at NULL)         (row, completed_at NOT NULL)
                                       │                                  │
                                       │ crash/resume: plan-started        │ open_escalation_count > 0
                                       │ is idempotent no-op (stays        │ ⇒ counts as `planned` for the
                                       │ in_progress, resumable)           │ PLANNING frontier (FR-004/FR-017)
                                       │                                   │ but flagged CONVERSION-blocking
                                       └─ --replan ─► (row UPDATEd: new     │ (queryable: open_escalation_count > 0)
                                          started_at + SHA, completed_at    │
                                          reset NULL, prior escalations     │
                                          superseded-with-note)             ▼
```

`plan_ready` is **derived, not stored**: a `plan_pending` file is `plan_ready` iff every SCC-external in-subtree dependency is `planned` (`plan_completed_at IS NOT NULL`), or its SCC has no external dependencies. SCC members become `plan_ready` as a batch. (See `contracts/plan_readiness_algorithm.md`.)

## 4. Diff against feature-012 / feature-015 data models

| Section | This feature changes anything? | Note |
|---|---|---|
| 012 §1.1 `codeconv.dart_files` | NO | Read-only (node set + `sha256` for drift). New `dart_plans` FK → `dart_files` ON DELETE CASCADE (same pattern as feature-015's `dart_conversions`). |
| 012 §1.2 `codeconv.dart_imports` | NO | Not read directly — cross-SCC dependency edges are taken from feature-015's `dart_depgraph`/derived view; this feature MUST NOT read `.dart` to derive deps (FR-003). |
| 012 §1.3 `codeconv.dart_callers` | NO | Untouched. |
| 012 §1.4 `codeconv.dart_files_orphaned` | NO | Read-only — orphaned files are excluded from planning (FR-020, edge case "orphaned files"). |
| 012 §1.5 `codeconv.discover_runs` | NO | Untouched. |
| 015 `codeconv.dart_depgraph` | NO (read-only) | Canonical ordering/SCC/status source (FR-003). MUST NOT recompute or write. |
| 015 `codeconv.dart_conversions` | NO (read-only, if at all) | This feature's frontier is keyed on `dart_plans`, NOT `dart_conversions` (Clarification Q1). Not modified (FR-020). |
| 015 `codeconv.depgraph_runs` | NO | Untouched; `planagents_runs` is the parallel new traceability table. |
| 012 §4.1 / 015 §2 Tombstone YAML | **APPENDS four keys** — see §2. Pre-existing 14 keys/order unchanged. |
| 012 §7 D2NET schemas (`public.*`) | NO | Untouched (FR-007/SC-007 carry-forward). |
| Bridge lifecycle / lock | NO | Just another consumer (spec Out of Scope). |

## 5. New on-disk artefacts

| Path | Status | Lifetime | Reason |
|---|---|---|---|
| `.codeconv/conversion-plans/<rel>.dart.md` | NEW (**checked in**, FR-010) | Persistent | One conversion-plan artefact per tombstone; durable, diffable, PR-reviewable, DB-wipe-survivable record of plan + escalation history. |
| `.codeconv/conversion-plans/_escalations-report.md` | NEW (**checked in**, FR-016) | Persistent (regenerated) | Aggregated engineer-facing open escalations across all artefacts. Path overridable. |
| `.codeconv/tombstones/<rel>.dart.md` | MODIFIED (checked in) | Persistent | Carries four new plan-state keys; durable round-trip source for `dart_plans` state. |

## 6. Verification

After this feature lands and `/codeconv-planagents` has run on `glp_runtime_net/`:

- `\d codeconv.dart_plans` (and `\d codeconv.planagents_runs` if created) against the live PGLite cluster must produce the column-set + constraints in §1.1 / §1.2.
- `\d codeconv.dart_files`, `\d codeconv.dart_imports`, `\d codeconv.dart_callers`, `\d codeconv.dart_files_orphaned`, `\d codeconv.discover_runs`, `\d codeconv.dart_depgraph`, `\d codeconv.dart_conversions`, `\d codeconv.depgraph_runs` must be byte-identical to the pre-feature snapshot (zero change to feature-012/-014/-015 surfaces).
- A diff of `.codeconv/tombstones/<file>.dart.md` frontmatter keys against the pre-feature snapshot shows exactly the four appended keys, in the §2 order, in every stamped tombstone — and the 14 pre-existing keys byte-identical.
- `\dn codeconv` shows exactly one schema; `\dn public` / `\dn dbos` untouched (SC-007).
- `SELECT count(*) FROM codeconv.dart_plans` after `--dry-run` is unchanged from before (SC-008).
