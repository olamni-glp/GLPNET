# Contract: `codeconv.dart_plans` (+ optional `codeconv.planagents_runs`) DDL

Implements spec FR-012, FR-020; SC-007. Delivered as Alembic revision `codeconv/src/codeconv/db/migrations/versions/0003_dart_plans.py` (down-revision = `0002_dart_depgraph`).

## Source of truth references

- Spec FR-012 (exact column set — normative), FR-020 (write-surface restriction), SC-007 (schema isolation).
- Feature 015 `specs/015-codeconv-depgraph/contracts/depgraph_schema.md` — `dart_conversions` / `depgraph_runs` shape this mirrors.
- Research R8.

## `codeconv.dart_plans`

```sql
CREATE TABLE IF NOT EXISTS codeconv.dart_plans (
    path                          text        PRIMARY KEY
                                  REFERENCES codeconv.dart_files (path) ON DELETE CASCADE,
    plan_started_at               timestamptz NOT NULL,
    plan_completed_at             timestamptz NULL,
    sha256_of_dart_at_plan_start  text        NOT NULL,
    plan_path                     text        NULL,
    open_escalation_count         integer     NOT NULL DEFAULT 0
                                  CHECK (open_escalation_count >= 0),
    plan_run_id                   uuid        NULL
);
-- FK plan_run_id → codeconv.planagents_runs(id) added only if planagents_runs exists
-- (added in the same migration after the runs table; NULL-able so dart_plans is
--  usable even if the optional runs table is later dropped).

CREATE INDEX IF NOT EXISTS dart_plans_open_escalations_idx
    ON codeconv.dart_plans (open_escalation_count)
    WHERE open_escalation_count > 0;   -- speeds the FR-017 conversion-blocked query
```

- `path` PK/FK shape is identical to `codeconv.dart_conversions.path` (feature 015).
- `plan_completed_at NULL` ⇔ `plan_in_progress`; `NOT NULL` ⇔ `planned` (data-model §3).
- No `status` text column — the four states are derived (`readiness.py`), not stored, to avoid a denormalised field drifting from `(plan_started_at, plan_completed_at)` (consistent with feature-015's choice to derive `ready`).
- No DELETE workflow in v1. `--replan` is an `UPDATE` (data-model §1.1).

## `codeconv.planagents_runs` (optional — research R8; mirrors `codeconv.depgraph_runs`)

```sql
CREATE TABLE IF NOT EXISTS codeconv.planagents_runs (
    id                       uuid        PRIMARY KEY,
    started_at               timestamptz NOT NULL,
    completed_at             timestamptz NULL,
    mode                     text        NOT NULL
        CHECK (mode IN ('status','next','plan-started','plan-completed',
                        'aggregate-escalations','stamp-tombstones',
                        'rebuild-plans-from-tombstones')),
    files_total              integer     NULL,
    plan_ready_count         integer     NULL,
    plan_in_progress_count   integer     NULL,
    planned_count            integer     NULL,
    open_escalations_total   integer     NULL,
    warnings                 jsonb       NOT NULL DEFAULT '[]'::jsonb
);
```

## Migration shape (carry-forward feature-012 FR-026/-027; feature-015 migration discipline)

- Upgrade: `CREATE TABLE IF NOT EXISTS` for both tables (+ index + conditional FK). Idempotent — safe to re-run.
- Downgrade: single `DROP TABLE IF EXISTS codeconv.planagents_runs, codeconv.dart_plans CASCADE;` (runs table first, then plans — reverse FK order).
- No `public` / `dbos` objects created (SC-007). No data migration of feature-012/-014/-015 tables (data-model §4; spec Assumptions).
- No `COPY … FROM STDIN`; no client-side prepared-statement caching (feature-012 FR-026/-027 carry-forward).

## Write protocol (FR-012 lifecycle / FR-020 surface restriction)

- `plan-started`: `INSERT INTO codeconv.dart_plans (path, plan_started_at, sha256_of_dart_at_plan_start, plan_run_id) VALUES (…) ON CONFLICT (path) DO NOTHING` then warn if the row already existed (idempotent — FR-014).
- `plan-completed`: `UPDATE codeconv.dart_plans SET plan_completed_at = NOW(), plan_path = :p, open_escalation_count = :n, plan_run_id = :r WHERE path = :path AND plan_completed_at IS NULL`; 0 rows affected ⇒ either never-started (error: exit 2) or already-completed (warn: exit 0) — disambiguated by a follow-up `SELECT`.
- `--replan`: `UPDATE … SET plan_started_at = NOW(), sha256_of_dart_at_plan_start = :sha, plan_completed_at = NULL, plan_run_id = :r WHERE path = :path` (row preserved; artefact records superseded-prior-escalations — R9).
- **No** `DELETE FROM codeconv.dart_plans` bulk wipe (unlike feature-015's `dart_depgraph` atomic-per-run — `dart_plans` is an accumulating lifecycle table).
- This feature MUST NOT issue any write against `codeconv.dart_files`, `dart_imports`, `dart_callers`, `dart_files_orphaned`, `discover_runs`, `dart_depgraph`, or `dart_conversions` (FR-020 — enforced by review of the migration + workflow SQL; test `test_planagents_schema_isolation.py`).

## Verification (SC-007)

- `\d codeconv.dart_plans` matches the column/constraint set above.
- `\dn` shows `codeconv` only changed; `public` / `dbos` unchanged.
- `test_planagents_schema_isolation.py` asserts no non-`codeconv` object was created and the feature-012/-014/-015 tables are byte-identical to a pre-feature `\d` snapshot.
