# Contract: `codeconv` schema DDL — three new tables (feature 015 additions)

This document specifies the exact DDL added by Alembic revision `0002_dart_depgraph.py`. The Alembic file follows it character-for-character (idiomatic minor variations like whitespace are fine; constraint names, column types, and CHECK predicates are normative).

## Source of truth references

- Spec FRs covered: FR-008 (atomic-per-run write), FR-011 (write surface restriction), and the column shapes in spec Key Entities § `dart_depgraph_row`, § `depgraph_run`, § `dart_conversion`
- Research notes: R5 (depgraph_runs included), R7 (status CHECK constraint)
- Sibling DDL precedent: `codeconv/src/codeconv/db/migrations/versions/0001_codeconv_schema.py`

## File layout

```text
codeconv/src/codeconv/db/migrations/versions/
├── 0001_codeconv_schema.py     # existing — codeconv schema + dart_files/imports/callers/orphaned/runs
└── 0002_dart_depgraph.py       # NEW — three new tables for feature 015
```

The Alembic env config in `codeconv/src/codeconv/db/migrations/env.py` (existing) discovers `versions/` automatically; no edit needed there.

## Revision metadata

```python
revision: str = "0002"
down_revision: Union[str, None] = "0001"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None
```

## `upgrade()` SQL

```sql
-- § 1 codeconv.depgraph_runs (R5 — traceability table, mirrors discover_runs shape)
CREATE TABLE IF NOT EXISTS codeconv.depgraph_runs (
    id                       uuid PRIMARY KEY,
    started_at               timestamptz NOT NULL,
    completed_at             timestamptz,
    mode                     text NOT NULL,
    files_total              integer,
    ready_count              integer,
    in_progress_count        integer,
    converted_count          integer,
    cycle_count              integer,
    warnings                 jsonb NOT NULL DEFAULT '[]'::jsonb,
    CONSTRAINT depgraph_runs_mode_check CHECK (
        mode IN (
            'compute',
            'mark-started',
            'mark-completed',
            'stamp-tombstones',
            'rebuild-conversions-from-tombstones'
        )
    )
);

-- § 2 codeconv.dart_conversions (FR-006a — two-phase conversion state)
CREATE TABLE IF NOT EXISTS codeconv.dart_conversions (
    path                     text PRIMARY KEY,
    started_at               timestamptz NOT NULL,
    completed_at             timestamptz,
    sha256_of_dart_at_start  text NOT NULL,
    target_path              text,
    marked_started_run_id    uuid,
    marked_completed_run_id  uuid,
    CONSTRAINT dart_conversions_path_fk
        FOREIGN KEY (path) REFERENCES codeconv.dart_files(path) ON DELETE CASCADE,
    CONSTRAINT dart_conversions_started_run_fk
        FOREIGN KEY (marked_started_run_id) REFERENCES codeconv.depgraph_runs(id) ON DELETE SET NULL,
    CONSTRAINT dart_conversions_completed_run_fk
        FOREIGN KEY (marked_completed_run_id) REFERENCES codeconv.depgraph_runs(id) ON DELETE SET NULL,
    CONSTRAINT dart_conversions_completed_after_started CHECK (
        completed_at IS NULL OR completed_at >= started_at
    )
);

-- § 3 codeconv.dart_depgraph (FR-008 — per-file ordering + readiness)
CREATE TABLE IF NOT EXISTS codeconv.dart_depgraph (
    path                     text PRIMARY KEY,
    topo_level               integer NOT NULL,
    cycle_group_id           integer NOT NULL,
    ready                    boolean NOT NULL,
    status                   text NOT NULL,
    dependency_count         integer NOT NULL,
    caller_count             integer NOT NULL,
    computed_at              timestamptz NOT NULL DEFAULT NOW(),
    depgraph_run_id          uuid,
    discover_run_id          uuid,
    CONSTRAINT dart_depgraph_path_fk
        FOREIGN KEY (path) REFERENCES codeconv.dart_files(path) ON DELETE CASCADE,
    CONSTRAINT dart_depgraph_depgraph_run_fk
        FOREIGN KEY (depgraph_run_id) REFERENCES codeconv.depgraph_runs(id) ON DELETE SET NULL,
    CONSTRAINT dart_depgraph_discover_run_fk
        FOREIGN KEY (discover_run_id) REFERENCES codeconv.discover_runs(id) ON DELETE SET NULL,
    CONSTRAINT dart_depgraph_topo_level_nonneg CHECK (topo_level >= 0),
    CONSTRAINT dart_depgraph_dep_count_nonneg CHECK (dependency_count >= 0),
    CONSTRAINT dart_depgraph_caller_count_nonneg CHECK (caller_count >= 0),
    CONSTRAINT dart_depgraph_status_check CHECK (
        status IN ('pending', 'ready', 'in_progress', 'converted')
    ),
    CONSTRAINT dart_depgraph_ready_status_consistent CHECK (
        (ready = TRUE AND status = 'ready')
        OR (ready = FALSE AND status <> 'ready')
    )
);

-- Index speeds SC-005 "what's ready?" lookup
CREATE INDEX IF NOT EXISTS dart_depgraph_ready_idx
    ON codeconv.dart_depgraph (ready) WHERE ready;

-- Index speeds SC-003 "for every edge, check topo invariant" verification join
CREATE INDEX IF NOT EXISTS dart_depgraph_path_topo_idx
    ON codeconv.dart_depgraph (path, topo_level, cycle_group_id);
```

## `downgrade()` SQL

```sql
DROP TABLE IF EXISTS codeconv.dart_depgraph CASCADE;
DROP TABLE IF EXISTS codeconv.dart_conversions CASCADE;
DROP TABLE IF EXISTS codeconv.depgraph_runs CASCADE;
```

(Order is reverse of creation to respect FKs. CASCADE handles the indices automatically.)

## Atomic-per-run write protocol (FR-008)

The `compute` subcommand runs the following SQL block inside ONE transaction:

```sql
BEGIN;

INSERT INTO codeconv.depgraph_runs
    (id, started_at, mode)
    VALUES (:run_id, NOW(), 'compute');

DELETE FROM codeconv.dart_depgraph;

-- Bulk INSERT N rows (where N = count of files in dart_files); one round-trip
-- using psycopg's executemany over a single INSERT statement. NOT COPY FROM
-- STDIN (FR-026 carry-forward).
INSERT INTO codeconv.dart_depgraph
    (path, topo_level, cycle_group_id, ready, status,
     dependency_count, caller_count, depgraph_run_id, discover_run_id)
    VALUES (...)
    ON CONFLICT (path) DO UPDATE SET
        topo_level       = EXCLUDED.topo_level,
        cycle_group_id   = EXCLUDED.cycle_group_id,
        ready            = EXCLUDED.ready,
        status           = EXCLUDED.status,
        dependency_count = EXCLUDED.dependency_count,
        caller_count     = EXCLUDED.caller_count,
        computed_at      = NOW(),
        depgraph_run_id  = EXCLUDED.depgraph_run_id,
        discover_run_id  = EXCLUDED.discover_run_id;

UPDATE codeconv.depgraph_runs SET
    completed_at      = NOW(),
    files_total       = :n_files,
    ready_count       = :n_ready,
    in_progress_count = :n_in_progress,
    converted_count   = :n_converted,
    cycle_count       = :n_cycles
    WHERE id = :run_id;

COMMIT;
```

A crash between BEGIN and COMMIT leaves the previous run's rows intact (transaction rolled back); a crash after COMMIT leaves the new run intact. No partial-state window observable from another reader.

The `DELETE FROM ... ; INSERT ... ON CONFLICT DO UPDATE` shape is belt-and-braces: the DELETE clears any stale rows (e.g. for paths that no longer exist in `dart_files` — defensive against drift); the ON CONFLICT clause handles the case where DELETE didn't fire for some reason. Net effect on a normal run: every existing row deleted + replaced; insert path always taken.

## Constraint rationale

| Constraint | Rationale |
|---|---|
| `dart_conversions.completed_at >= started_at` | Defensive: invalid two-phase sequence would be a Python bug; DB catches it. |
| `dart_depgraph.topo_level >= 0` | Spec FR-004: levels are non-negative. |
| `dart_depgraph.dependency_count >= 0` | Counts are non-negative. |
| `dart_depgraph.caller_count >= 0` | Counts are non-negative. |
| `dart_depgraph.status IN (...)` | R7 — defense in depth against typos. |
| `dart_depgraph_ready_status_consistent` | Spec FR-006: `ready = TRUE ⇔ status = 'ready'`. Without this constraint, a code bug could write `ready=true, status='pending'` and break SC-005. |
| `dart_depgraph_path_fk ... ON DELETE CASCADE` | If a file is later removed from `dart_files` (e.g. via a future cleanup tool), the corresponding `dart_depgraph` row vanishes automatically. Same convention as feature 012's view of `dart_imports` / `dart_callers`. |
| `dart_conversions_path_fk ... ON DELETE CASCADE` | Same rationale as above for conversion state. |
| `..._run_fk ... ON DELETE SET NULL` | If a `depgraph_runs` row is later GC'd, the FK column becomes NULL — the per-file data survives. |

## PGLite compatibility note

PGLite (≥0.2.17 per `package.json`) implements the PostgreSQL DDL surface needed here: CHECK constraints, FK constraints, partial-index WHERE clauses, jsonb DEFAULT casts. All forms above are exercised by feature 012's existing `0001_codeconv_schema.py` (FK + jsonb + partial index — verify in code). The new constraints add only:

- `CHECK (status IN (...))` (string literal IN-list) — pure SQL, no PG-only operator.
- `CHECK (completed_at IS NULL OR completed_at >= started_at)` — null-aware comparison, standard SQL.
- `CHECK (ready = TRUE AND status = 'ready' OR ready = FALSE AND status <> 'ready')` — boolean algebra, standard SQL.

If the test suite reveals a PGLite-specific incompatibility with any of these constructs, STOP and escalate per the spec's Assumptions section before relaxing the constraint.

## Test obligations

The implementation MUST pass tests for:

1. `test_depgraph_schema_isolation.py`: after running migration, `\dn` shows `codeconv` only; `\dn public` and `\dn dbos` are untouched (SC-007).
2. `test_depgraph_schema_isolation.py`: after running migration, exactly three new tables exist under `codeconv`: `dart_depgraph`, `dart_conversions`, `depgraph_runs`.
3. CHECK constraint violations are caught: INSERT-ing `status='completed'` (typo) raises a CHECK violation; INSERT-ing `ready=true, status='pending'` raises the consistency CHECK violation.
4. FK CASCADE works: DELETE-ing a row from `dart_files` cascades to `dart_depgraph` and `dart_conversions`.
5. Downgrade-then-upgrade is idempotent: `alembic downgrade -1; alembic upgrade head` restores the same schema state.
