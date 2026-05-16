# Runbook (GATED): live PGLite cluster PG16 → PG17 migration for the 0.4.5 upgrade

**Status: PREPARE-ONLY. Do NOT execute without Gabi's explicit go AND D2NET-side coordination.**

## Why this is needed

`@electric-sql/pglite` was raised `0.2.17 → 0.4.5` to fix the verified
extended-protocol-`ROLLBACK`-in-aborted-txn hang (and the savepoint WASM
crash). PGLite on-disk data directories are **NOT forward-compatible**:

- `0.2.17` embeds **PostgreSQL 16**; `0.4.5` embeds **PostgreSQL 17**.
- Official upgrade guide (https://pglite.dev/docs/upgrade): upgrade =
  `pg_dump` with the OLD version → fresh instance with the NEW version →
  restore. An existing `0.2.x` cluster will not be opened in place by
  `0.4.x` in a supported way (a casual read may appear to work — it is the
  documented false-positive; do not rely on it).
- `0.3.0` breaking change: default database is `postgres` (not
  `template1`). The bridge/clients already use `postgres`, so no action —
  but verify after restore.

## Blast radius — cross-tool

The unified live cluster `C:/pglite/research/glpnet/` (canonical for this
repo; D: is exFAT so the cluster lives on NTFS — see CLAUDE.md) holds
**three schemas in one PG cluster**:

| Schema | Owner | Notes |
|---|---|---|
| `public` | **D2NET** (`.NET` tools) | D2NET applies its schema unqualified — all its tables are here. **Cross-team: requires D2NET coordination/sign-off.** |
| `dbos` | DBOS runtime | Recreated by `dbos.launch()` migrations (idempotent) — see decision note below. |
| `codeconv` | codeconv | `dart_files/imports/callers/orphaned/discover_runs` (+ feature-015 `depgraph_runs/dart_conversions/dart_depgraph` once 015 lands). |

A dump/restore touches D2NET's data. Coordinate before executing.

## Preconditions

- No bridge running against `C:/pglite/research/glpnet/` (no consumers).
  Stop via the non-destructive marker: `printf requested >
  C:/pglite/research/glpnet.shutdown`, confirm the daemon exits.
- The fix branch `fix/pglite-aborted-txn-upgrade-0.4.5` is merged to
  `main` (so `prereq-patterns/pglite/` is on 0.4.5 + the coalesce).
- A throwaway 0.2.17 install is available to run the dump (the OLD
  engine must read the OLD cluster). Keep one (e.g. the probe harness
  `C:/pglite/_pgprobe/v0217/`).
- Full filesystem backup of `C:/pglite/research/glpnet/` taken first.

## Procedure (execute only when gated open)

1. **Backup**: copy `C:/pglite/research/glpnet/` →
   `C:/pglite/research/glpnet.pg16.bak/` (cold, no bridge running).
2. **Dump (OLD engine, 0.2.17)**: start a 0.2.17 bridge against the
   existing cluster on a scratch port; `pg_dump
   --format=custom --no-owner --no-privileges` (or per-schema dumps for
   `public`, `dbos`, `codeconv`) via that bridge into
   `C:/pglite/research/glpnet.dump`. (DBOS data MAY be excluded and
   rebuilt by `dbos.launch()` instead — decide with D2NET/Gabi; the
   `dbos` schema migrations are idempotent and recreate structure, but
   in-flight workflow state would be lost. Default recommendation: dump
   and restore `dbos` too, to preserve workflow state.)
3. **Fresh cluster (NEW engine, 0.4.5)**: move the old dir aside; let a
   0.4.5 bridge cold-init a fresh `C:/pglite/research/glpnet/`
   (PostgreSQL 17). Confirm `BRIDGE_READY`.
4. **Restore**: `pg_restore`/`psql` the dump into the fresh cluster
   through the 0.4.5 bridge. Order: roles/extensions are auto-handled
   (PGLite has none of the uuid-ossp issue post-`_apply_pglite_compat_patch`);
   restore `public` (D2NET) + `codeconv` (+ `dbos` if dumped).
5. **DBOS reconcile**: run `codeconv migrate` (Alembic head + DBOS
   launch) once — Alembic is head-current (no-op), DBOS migrations
   idempotent; this re-validates the `dbos` schema on PG17.
6. **Verify**:
   - `\dn` shows exactly `public`, `dbos`, `codeconv` (SC-007 parity).
   - D2NET smoke (coordinate with D2NET): a `D2Net.*` read/write cycle.
   - codeconv: `codeconv doctor`; row counts for `codeconv.dart_files`
     etc. match the pre-migration counts (compare against the snapshot
     in `specs/015-codeconv-depgraph/baseline.json` / a fresh `\dt+`).
   - Default DB is `postgres` and reachable.
7. **Cutover / rollback**:
   - Success → delete `glpnet.pg16.bak/` after a soak period.
   - Failure → stop 0.4.5 bridge, restore `glpnet.pg16.bak/` in place,
     revert `prereq-patterns/pglite/package.json` to `0.2.17` on a
     hotfix branch. (The hang returns, but data is safe.)

## Open decisions to settle at gate-open

1. Dump+restore `dbos` (preserve workflow state) **vs** drop `dbos` and
   let `dbos.launch()` rebuild it (simpler; loses in-flight workflows).
   Recommendation: dump+restore unless D2NET/codeconv confirm no
   in-flight workflows matter.
2. Maintenance window + which side (D2NET or codeconv) drives execution.
3. Whether to run SC-003 (`Npgsql`/`psqlODBC` 100-cycle) + SC-004
   (concurrent psycopg) against the restored 0.4.5 cluster as part of
   acceptance (recommended — SC-003 is the historically-fragile
   doubled-`Z` path the coalesce targets).

## Verification already completed (so this runbook is low-risk)

- 0.4.5 fixes the hang + savepoint crash (byte-level probes; node
  regression `tests/aborted_txn_rollback.test.mjs`).
- Full bridge node suite 8/8 green on 0.4.5 (lock/sidecar/concurrent/
  log-rotation/post-kill contracts intact).
- `codeconv migrate` (Alembic `0001`+`0002` + DBOS launch, 35 system
  migrations) green on a fresh PG17/0.4.5 cluster.
- psycopg3 end-to-end (CHECK-violation → rollback → reuse → timestamptz
  with compat loaders) green; psycopg3 tolerates the doubled `Z`; the
  bridge coalesce makes the wire correct for `Npgsql`/`psqlODBC` too.
