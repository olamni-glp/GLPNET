# Pattern directory

Index of curated prerequisite implementations available in glpnet. Each entry below points at a sub-directory under `prereq-patterns/` containing the pattern's `description.md`, `applicability.md`, and `sources.md`. See [howto.md](./howto.md) for authoring rules and the line format used here.

## Patterns

- **pglite** — Local Postgres-compatible DB via PGLite + Node TCP bridge with single-session pool, for SQLAlchemy / Alembic / DBOS / psycopg / Npgsql / psqlODBC consumers.
- **dbos** — Durable agent-action workflows over pglite via DBOS, modelled on hatzinor ulpani. (draft)
- **flask-sqlalchemy-alembic-api** — Canonical Flask + SQLAlchemy + Alembic API on pglite via the bridge + single-session pool. (draft)
- **pglite-backup-restore** — Backup and restore for the local pglite database, with snapshot-before-development discipline. (draft)
- **blazor-spa-bg-api** — Browser-side Blazor SPA served by a background Flask + websocket API, researched and experimentally validated. (draft)
- **background-task-manager** — Registry of background tasks with prereq/dependent edges, lifecycle, bootstrapped on pglite. (draft)
- **local-secrets-store** — Home-directory secrets store behind a swappable interface, drawn from hatzinor / sipdem / ospark. (draft)
- **secure-signatures** — Sign / verify / rotate interface for data and code artefacts, modelled on NHS data-pipeline practice. (draft)
