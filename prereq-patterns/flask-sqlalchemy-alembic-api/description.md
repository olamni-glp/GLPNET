# flask-sqlalchemy-alembic-api

Status: draft

## What this produces

A working Flask service that exposes a SQLAlchemy/Alembic-managed schema served from the local pglite database via the bridge + single-session pool from the [`pglite`](../pglite/description.md) pattern. The deliverable is a curated, indexed reference grounded in the hatzinor ulpani-LMS implementation, enriched with trusted-web canonical patterns (application-factory, blueprints, scoped sessions, Alembic with multiple binds, transactional test fixtures), with the pglite-driver-specific knowledge preserved through every layer.

## Why it matters

Standing up a Flask + SQLAlchemy + Alembic service against pglite naively does not work. The default SQLAlchemy engine assumes a multi-connection server; pglite is a single shared session, and the default config will deadlock on the second concurrent request. The trusted-web canonical patterns (e.g. `flask-sqlalchemy`'s default scoped session, the application-factory pattern) are correct in the abstract but incomplete here — they MUST be composed with the substrate's `pglite_engine_kwargs(application_name=...)` helper or they will deadlock or desync prepared statements.

Three load-bearing constraints carry through from the substrate:

- **Driver dialect**: a plain `postgresql://` URL with psycopg internally — pglite emulates Postgres wire protocol and the standard psycopg path works, but the URL builder MUST go through the substrate's helper rather than be hand-built (the helper handles `postgresql+psycopg://` / `postgresql://` rewriting and the prepared-statement-cache disable).
- **Bridge connection-string shape**: the connection points at the pglite sidecar's TCP port (discovered from `sidecar.json`), not at a unix socket or a remote host. The Flask service inherits the substrate's startup-ordering rule — the sidecar MUST be up before the Flask app starts.
- **Pool concurrency model**: `QueuePool(pool_size=1, max_overflow=0)` for the long-lived Flask SQLAlchemy engine; `NullPool` for the short-lived Alembic engine. Both go through the substrate's helper.

Composition with sibling patterns: this pattern coexists with [`dbos`](../dbos/description.md) on the same pglite store. There is no contention because every consumer queues through the substrate's single-session pool; DBOS workflow state lives in the `dbos` schema, the API's domain data lives in its own schema, and the Alembic env-variant in this pattern uses a `version_table_schema=<your-schema-name>` to keep its `alembic_version` table out of the public schema.

## How a feature uses this pattern

This pattern is `Status: draft` — no glpnet feature has yet adopted it. The full implementation surface (the application-factory, the SQLAlchemy model module, the Alembic env, the alembic.ini with the env-set-URL contract) is consolidated in the curated upstream catalog; see [sources.md](./sources.md) for citations into the hatzinor ulpani-LMS reference. When the first glpnet feature adopts this pattern, that feature's PR is responsible for promoting `Status:` to `active`, fleshing out [applicability.md](./applicability.md) with substantive consumer-class content (e.g. fresh-Flask, migration-off-SQLite, migration-off-Postgres), and updating [../directory.md](../directory.md)'s suffix.

## Cross-cutting policies

This pattern is on the [Policy 1](../policies.md#policy-1--no-cleartext-auth-tokens-fr-cc-1) `Applies to` list because the API may surface authenticated endpoints whose handler code touches secret material. Concrete realisation lives in [`local-secrets-store/description.md`](../local-secrets-store/description.md) (chosen v1 hash algorithm and parameters) and [`background-task-manager/description.md`](../background-task-manager/description.md) (data-plane realisation of the forbidden-cleartext rule); this pattern only cross-links.

This pattern is on the [Policy 2](../policies.md#policy-2--non-config-history-off-repo-to-glpnet-datalake-fr-cc-2) `Applies to` list because the API emits non-config history (request logs, audit events, error traces). Per the inclusion list, request logs and audit events are NOT pglite content; they route to the off-repo glpnet datalake destination. The runtime config-key resolving the destination, the per-pattern destination filename, and the unreachable-destination fallback are named in [`background-task-manager/description.md`](../background-task-manager/description.md).
