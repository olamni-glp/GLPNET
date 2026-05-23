# Contract — `dart_codegen` schema + migration 0007

## DDL (migration `0007_codegen.py`, `down_revision="0006"`)
```sql
CREATE TABLE IF NOT EXISTS codeconv.dart_codegen (
    path                            text PRIMARY KEY,
    codegen_started_at              timestamptz,
    codegen_completed_at            timestamptz,
    sha256_of_dart_at_codegen_start text,
    target_cs_path                  text,
    build_status                    text CHECK (build_status IN ('pass','fail','not_built')),
    test_pass_rate                  real,
    human_review_score              smallint CHECK (human_review_score BETWEEN 1 AND 5),
    open_escalation_count           integer NOT NULL DEFAULT 0,
    batch_id                        text,
    promoted                        boolean NOT NULL DEFAULT false,
    codegen_run_id                  text
);
```
Downgrade: `DROP TABLE IF EXISTS codeconv.dart_codegen CASCADE;`

## Invariants
- **Schema isolation**: Alembic authors objects ONLY in `codeconv`; no `public`/`dbos` object (DBOS owns its own tables). `test_schema_isolation`-analog asserts it.
- **Single head**: `alembic upgrade head` reaches exactly one head (`0007`); no dup/multi-head (`test_migration_0007_single_head.py`).
- **No data migration**: fresh additive table (CREATE TABLE IF NOT EXISTS), no backfill.
- **Lifecycle**: phase-1 INSERT-ON-CONFLICT (started) → UPDATEs (build/test/review/escalation/promote) → phase-2 completed. Never bulk DELETE (R9 carry-forward).

## Tombstone keys (append-only)
Appended AFTER the plan keys in `_FIELD_ORDER`: `codegen_started_at, codegen_completed_at, target_cs_path, build_status, codegen_open_escalation_count`. Canonical YAML; stamp↔rebuild idempotent.
