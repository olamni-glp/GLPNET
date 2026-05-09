# pglite-backup-restore

Status: draft

## What this produces

A vetted backup-and-restore procedure for the local pglite database. The deliverable is an indexed pattern grounded in hatzinor's `ulpani-dbbackup` implementation: `backup_db()` / `backup_all()` produce timestamped per-database snapshots; `restore()` consumes them with an atomic-swap apply. Each backup unit is a directory containing `dump.sql`, `manifest.json`, `fingerprint.json`, `README.md`; the restore is invariant against a corrupted target because it takes a pre-restore snapshot of the live database before applying any changes.

## Why it matters

Backup and restore are the load-bearing safety net under every other pattern in this catalog. Without it, every operational change (schema migration, agent-driven mutation, `dbos` workflow that mishandles a step) is unrecoverable. Two non-obvious correctness rules carry through:

- **Snapshot-before-development discipline**: BEFORE building or testing anything that touches the backup-restore code path itself, take a manual snapshot of the live pglite database. Every restore the tool performs auto-snapshots first, but the operator's own iteration on the tool MUST also snapshot before the first test run. The lesson learned upstream: a bad iteration on the restore tool wiped the live database before the test suite caught the regression. The discipline is to never trust a freshly-changed restore path against an irreplaceable database.
- **Atomic-swap restore semantics**: the restore is not "drop and recreate"; it applies the dump into a transaction, validates schema and row counts against the manifest's fingerprint, then atomically swaps the new schema in. A failed apply leaves the live database untouched.

Recovery-point granularity: a backup is one timestamped directory per invocation, with one sub-directory per database (`postgres/`, etc.). Per-table or per-row recovery is not supported in v1; the granularity is the whole database snapshot. The fingerprint file (`fingerprint.json`) is the integrity check — if its SHA-256 of the dump's canonical form does not match, the restore refuses.

## How a feature uses this pattern

This pattern is `Status: draft` — no glpnet feature has yet adopted it. The full backup-restore tool plus its integration-test pair (round-trip and atomic-swap-under-failure) is consolidated upstream in AIGRID's catalog; see [sources.md](./sources.md) for citations into the hatzinor `ulpani-dbbackup` reference and the auto-snapshot operational evidence. When the first glpnet feature adopts this pattern, that feature's PR is responsible for promoting `Status:` to `active`, fleshing out [applicability.md](./applicability.md) with substantive content for the relevant DB topology cases (idle / under-DBOS-load / serving-Flask-API / inter-version), and updating [../directory.md](../directory.md)'s suffix.

Pre-flight for any future adopter: the pglite sidecar from this catalog's [`pglite`](../pglite/description.md) pattern MUST be running before invoking either direction (the tool talks via `psycopg` to the bridge's TCP port). Coexists with [`dbos`](../dbos/description.md) and [`flask-sqlalchemy-alembic-api`](../flask-sqlalchemy-alembic-api/description.md) on the same database — but per AIGRID's CO #37 note, the `dbos` schema's user-defined functions are skipped from v1 dumps (`--skip-schema dbos`); DBOS recreates them on next launch. Understand and accept this trade-off before relying on the pattern for DBOS-state recovery.

## Cross-cutting policies

This pattern is NOT on either policy's `Applies to` list in v1. It neither persists secrets in its own data plane (it operates *on* a database that may contain them) nor emits non-config history beyond the on-disk backup files (operational artefacts, not telemetry). If a future revision wires backup-completion logs into the glpnet datalake destination, that PR adds this pattern to [Policy 2](../policies.md#policy-2--non-config-history-off-repo-to-glpnet-datalake-fr-cc-2)'s `Applies to` list and adds the cross-link from this `description.md` then.
