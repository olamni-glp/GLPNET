# Sources — flask-sqlalchemy-alembic-api

The AIGRID prereq-patterns catalog is glpnet's consolidating upstream for this pattern. Its `prereq-patterns/flask-sqlalchemy-alembic-api/` index reaches into the hatzinor ulpani-LMS Flask application factory plus four trusted-web canonical references (Flask app-factory, blueprints, SQLAlchemy contextual sessions, Alembic with branched histories, Flask testing). Glpnet has no own implementation today.

## Index

| Path | Upstream | Action | Summary |
|---|---|---|---|
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/flask-sqlalchemy-alembic-api/description.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's pattern description — what a Flask + SQLAlchemy + Alembic API on pglite produces and why it matters. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/flask-sqlalchemy-alembic-api/applicability.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's consumer-class notes for fresh services, SQLite-migration, Postgres-migration, and DBOS coexistence. |
| `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/flask-sqlalchemy-alembic-api/sources.md` | `olamni-breen/aigrid-aws-infra@004a-opskit-sidecar-autospawn` | Read | AIGRID's upstream-source citations into hatzinor (the LMS application factory, model module, Alembic env, alembic.ini) plus five trusted-web Flask / SQLAlchemy / Alembic canonical references. |

## Per-source notes

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/flask-sqlalchemy-alembic-api/description.md`

- Three load-bearing constraints (driver dialect; bridge connection-string shape; pool concurrency model) are what make the difference between a working Flask + pglite stack and a deadlocking one. A glpnet adopter MUST honour them; the AIGRID file's "Why it matters" section is the rationale.
- DBOS coexistence rule: `version_table_schema=<your-schema-name>` in the Alembic env, plus distinct `application_name` values across consumers so `pg_stat_activity` is debuggable through the single shared session.

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/flask-sqlalchemy-alembic-api/applicability.md`

- Per-consumer H3s cover four cases: a fresh Flask service (use the application-factory `create_app(app_name, testing, config_overrides)` shape; pass `engine_options=pglite_engine_kwargs(...)`); migrating off SQLite (URL change, paramstyle change, isolation defaults differ); migrating off hosted Postgres (URL points at sidecar TCP port, `prepare_threshold=None` in `connect_args`, `pool_size=1`); DBOS coexistence on the same store.

### `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/flask-sqlalchemy-alembic-api/sources.md`

- Cites the hatzinor ulpani-LMS application factory (`now_lms/__init__.py`, `create_app()` at line 454), the model module (`now_lms/db/__init__.py`), the Alembic env (`alembic/env.py` — `target_metadata = NAMESPACED_METADATA`, `engine_from_config(..., poolclass=pool.NullPool, future=True)`), and `alembic.ini` (the env-set-URL contract, `sqlalchemy.url=` deliberately empty).
- Cites five trusted-web canonical references: Flask application-factory pattern, Flask blueprints, SQLAlchemy contextual sessions, Alembic with branched histories, Flask transactional test fixtures. Each strengthens (never weakens) the pglite-specific knowledge.
- Re-cites `ulpani_lms_pglite_compat.py` (the `pglite_engine_kwargs` helper from the substrate) for self-containment of the Flask consumer's adoption path.
