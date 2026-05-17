# Contract — Migration Linearization (FR-015 / FR-021 / SC-004)

## Defect (confirmed real)

`0003_d2net_into_codeconv.py` and `0003_dart_plans.py` **both** declare
`revision = "0003"`, `down_revision = "0002"`. Alembic raises on duplicate
revision id / multiple heads, so `command.upgrade(cfg, "head")` in
`cli.py::_run_alembic_upgrade` (the `codeconv migrate` path) is **currently
broken** against a fresh PG17 cluster.

## Fix (minimal, no data migration — FR-021)

Edit only the four module-level identifiers in `0003_dart_plans.py`
(filename unchanged to preserve git lineage):

```
revision:      "0003" → "0004"
down_revision: "0002" → "0003"
branch_labels: None    (unchanged)
depends_on:    None    (unchanged)
```

New `0005_codeconv_builder.py`:

```
revision:      "0005"
down_revision: "0004"
```

`0003_d2net_into_codeconv.py` is **untouched** (keeps `0003`/`0002`).

## Invariant + test

- `alembic heads` returns exactly **one** revision (`0005`).
- `alembic history` is the linear chain `0001→0002→0003→0004→0005`.
- `test_migration_single_head.py`: fresh cluster → `alembic upgrade head`
  exits 0; `len(script.get_heads()) == 1`; re-run is a no-op (idempotent —
  all DDL is `IF NOT EXISTS`).

## Downgrade

`0005.downgrade()` = `DROP TABLE IF EXISTS codeconv.{builder_runs,
research_findings,conversion_idioms,dart_convspecs} CASCADE;`
`0004.downgrade()` unchanged from the original `0003_dart_plans` body.

## No data migration

Per FR-021 + the 2026-05-17 fresh-PG17 decision: all conversion data is
recreatable; no legacy rows are read or copied. Schema isolation: every
object stays in `codeconv`; Alembic authors **no** `public`/`dbos` object
(DBOS creates its own at `dbos.launch()`).
