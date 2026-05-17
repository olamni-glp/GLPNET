# Contract — Builder Schema DDL (`0005_codeconv_builder.py`)

All under `codeconv` schema; `CREATE TABLE IF NOT EXISTS` (idempotent,
isolation-safe — matches 0001/0002 style). Order: `builder_runs` →
`research_findings` → `conversion_idioms` → `dart_convspecs` (FK-satisfiable
order). Columns/PK/FK per [../data-model.md](../data-model.md) §2.

## Lifecycle write protocol (append-then-UPDATE — proven 015/017 shape)

- `dart_convspecs`: `INSERT … ON CONFLICT (path) DO UPDATE` — set
  `convspec_started_at` first; `convspec_completed_at` + `spec_path` set
  **only** by the convspec step's terminal action (FR-003 ordering →
  half-step is observably incomplete).
- `conversion_idioms`: insert on first resolution of a `construct_key`;
  `status='conflicted'`/`'escalated'` on FR-014 conflict (never silent
  overwrite of `target_form`).
- `research_findings`: `construct_key` **UNIQUE**; insert-once via
  `ON CONFLICT (construct_key) DO NOTHING`; never updated (cache;
  FR-012/FR-024 — a construct is never re-researched, no duplicate/ambiguous
  rows under parallel agents). `is_authoritative=false` ⇒ caller MUST escalate.
- `builder_runs`: insert at outer-workflow start; UPDATE counts/outcome;
  `outer_workflow_id` UNIQUE so resume reuses the row.

## FK / null discipline

`dart_convspecs.path` FK→`dart_files(path)`; `convspec_run_id` &
`conversion_idioms.research_finding_id` are **nullable** so the optional
parent surviving a CASCADE drop never orphans a usable row.

## Schema isolation assertion

`test_schema_isolation.py`: after `0005`, every new relation's
`table_schema = 'codeconv'`; **zero** new objects in `public` or `dbos`
attributable to Alembic (DBOS authors its own tables at launch — out of
Alembic scope).

## Downgrade

`DROP TABLE IF EXISTS codeconv.{builder_runs,research_findings,
conversion_idioms,dart_convspecs} CASCADE;` (reverse creation order).
