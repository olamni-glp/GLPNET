# Phase 1 Data Model — codeconv-builder (018)

**Delta-only** against features 012/015/016/017. No existing column, row,
constraint, or table is altered (D2 principle). DBOS owns its own
`dbos`-schema tables (created by `dbos.launch()`, not by Alembic). All new
objects live under the **`codeconv`** schema; `CREATE TABLE IF NOT EXISTS`;
downgrade `DROP … CASCADE`.

## 1. Migration chain (FR-015 — was broken)

| Revision | down_revision | File | Status |
|---|---|---|---|
| 0001 | (none) | `0001_codeconv_schema.py` | unchanged |
| 0002 | 0001 | `0002_dart_depgraph.py` | unchanged |
| 0003 | 0002 | `0003_d2net_into_codeconv.py` | unchanged (keeps id `0003`) |
| **0004** | **0003** | `0003_dart_plans.py` | **MODIFIED**: `revision "0003"→"0004"`, `down_revision "0002"→"0003"` (file name unchanged; only the in-file ids change) |
| **0005** | **0004** | `0005_codeconv_builder.py` | **NEW** (this feature) |

Result: one linear chain, one head (`0005`), zero duplicate/multiple-head
errors → `alembic upgrade head` (called by `codeconv migrate`) succeeds. See
[contracts/migration_linearization.md](./contracts/migration_linearization.md).

## 2. New tables (`0005_codeconv_builder.py`, `codeconv` schema)

### 2.1 `codeconv.dart_convspecs` — per-file convspec two-phase state (parallel to `dart_plans`)

| Column | Type | Notes |
|---|---|---|
| `path` | text PK | FK → `codeconv.dart_files(path)` |
| `convspec_started_at` | timestamptz NULL | set when convspec stage begins |
| `convspec_completed_at` | timestamptz NULL | set only by the terminal step action (FR-003 write-ordering) |
| `spec_path` | text NULL | `.codeconv/conversion-specs/<rel>.dart.md` |
| `sha256_of_dart_at_spec_start` | text NULL | drift detection (FR-019) / `--respec` |
| `open_escalation_count` | integer NOT NULL DEFAULT 0 | gates *conversion*, not specing |
| `convspec_run_id` | bigint NULL | FK → `builder_runs(id)` (nullable; survives runs table drop) |

### 2.2 `codeconv.conversion_idioms` — persistent codebase-scoped idiom KB (FR-012/SC-007)

| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `construct_key` | text NOT NULL UNIQUE | normalised construct signature (lookup key) |
| `source_form` | text NOT NULL | Dart-side shape |
| `target_form` | text NOT NULL | C#/.NET-side decision |
| `rationale` | text NOT NULL | human-readable why |
| `research_finding_id` | bigint NULL | FK → `research_findings(id)` |
| `first_seen_path` | text NOT NULL | originating file |
| `status` | text NOT NULL DEFAULT `'active'` | `active｜conflicted｜escalated` (FR-014) |
| `created_at` | timestamptz NOT NULL DEFAULT now() | |

### 2.3 `codeconv.research_findings` — official-docs-authoritative provenance cache (FR-024)

| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `construct_key` | text NOT NULL | join key with idioms |
| `query` | text NOT NULL | verbatim research request |
| `authoritative_source` | text NOT NULL | official Dart/.NET doc citation/URL |
| `corroborating_sources` | jsonb NOT NULL DEFAULT `'[]'` | non-authoritative, optional |
| `conclusion` | text NOT NULL | what was decided |
| `retrieved_at` | timestamptz NOT NULL DEFAULT now() | |
| `is_authoritative` | boolean NOT NULL | false ⇒ must escalate (FR-024) |

Cache rule: a row keyed by `construct_key` makes that construct **never
re-researched** (FR-012/FR-024) → offline-reproducible after first research.

### 2.4 `codeconv.builder_runs` — run traceability + DBOS-trace join key (R9/R11)

| Column | Type | Notes |
|---|---|---|
| `id` | bigserial PK | |
| `workspace_id` | text NOT NULL | |
| `outer_workflow_id` | text NOT NULL UNIQUE | deterministic id (R9) → joins `dbos.workflow_status` |
| `started_at` | timestamptz NOT NULL DEFAULT now() | |
| `finished_at` | timestamptz NULL | |
| `code_version` | text NULL | git HEAD at launch (R13 mid-run-change visibility) |
| `files_total` / `files_done` / `files_escalated` | integer NOT NULL DEFAULT 0 | reconciled counts |
| `outcome` | text NULL | `completed｜resumed｜nothing_to_convert｜escalated` (FR-020) |

Helper index: `dart_convspecs_open_escalations_idx` partial on
`open_escalation_count` `WHERE open_escalation_count > 0` (FR-017 query).

## 3. Tombstone `_FIELD_ORDER` extension (append-only, after feature-017's keys)

Appended to `tools/discover/tombstone.py::_FIELD_ORDER` **after** 017's four
plan keys (canonical YAML, sorted lists, pinned order preserved):

- `convspec_started_at`, `convspec_completed_at`, `spec_path`,
  `convspec_open_escalation_count`
- `builder_outer_workflow_id`, `builder_file_state`
  (one of the R11 state-vocabulary values)

Null-vs-missing: a key absent ⇒ stage never reached; present-but-null ⇒
reached, not completed. Append-only ⇒ the 012/014/015/017 idempotence proof
(stamp→rebuild→stamp is a fixed point) carries unchanged. Artifact *content*
is NOT mirrored into YAML (it is durable in the checked-in
`.codeconv/conversion-specs/` artifacts + the idiom KB export).

## 4. Read dependencies (unchanged, read-only)

`codeconv.dart_depgraph` (015 — order/SCC/status; MUST NOT recompute),
`codeconv.dart_files`/`dart_files_orphaned` (node set + `sha256`),
`codeconv.dart_plans` (017), `codeconv.dart_conversions` (015),
`codeconv.workspace_settings`/`excluded_directories`/`phase_*` (016, via the
new `workspace.py` facade). `dbos.workflow_status` / `dbos.operation_outputs`
(DBOS-owned — read-only, for `builder trace`).

## 5. State machine (unified per-file state — R11)

`not_started → blocked_on_deps → analysed → specced → scaffolded → converted →
complete`, with `escalated` reachable from any non-terminal state and resolved
back to its prior state. Terminal: `complete`. The state is a **projection**
over the durable two-phase columns + escalation counts + depgraph readiness —
not a separately-mutated field (so it cannot diverge from durable truth;
FR-017/FR-019).
