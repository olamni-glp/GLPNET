# Contract — `dart_equivalence` schema + migration `0008` (FR-018, 012 schema isolation)

## DDL (`codeconv` schema only)
See `data-model.md` for the full column list. Created by `db/migrations/versions/0008_equivalence.py`:
- `CREATE TABLE IF NOT EXISTS codeconv.dart_equivalence (...)` — idempotent (012).
- Unique `(tombstone_key, source_path)`; indexes on `(subsystem, tier)`, `(verdict)`, partial `WHERE verdict='stale'`.
- **No** `public`/`dbos` objects authored (012 schema isolation; DBOS owns its `dbos` tables).

## Migration linearization (single head)
- `revision = '0008'`, `down_revision = '0007'`.
- Chain: `0001→0002→0003_d2net_into_codeconv→0003_dart_plans→0005→0006→0007→0008`. The historical dual-`0003` was already linearized downstream; `0007` is the single current head.
- **Single-head proof (verified in tasks)**: the migration runner reports exactly one head (`0008`) after add. A test (`test_migration_single_head`) asserts no branching.
- Replay/idempotency: `IF NOT EXISTS` + additive columns only; safe to re-run on the canonical cluster.

## Two-phase write discipline (mirrors `dart_codegen`, 019)
1. `capture` writes `phase=captured` + trace hashes (agent/CLI layer).
2. `compare` (durable step) writes `phase=compared` + `verdict` + `divergence` deterministically from recorded traces.
No row reaches `verdict=equivalent` without `builds=true` and the relation passing.

## Read-only upstream
`0008` adds only; it does not alter `dart_codegen`/`dart_plans`/`dart_convspecs`/`dart_depgraph` (FR-018 — extend, never recompute).
