## Issue 7: DBOS-on-PGLite launch — four required hooks

**Status**: Mitigated (workarounds applied; revisit if DBOS upstream changes)
**Discovered**: 2026-05-10 during 012-codeconv-runner Phase 6
**Affects**: `codeconv migrate` (DBOS `dbos.launch()`) against the unified `.pgdb/` bridge

### Summary

A fresh DBOS launch against the PGLite bridge requires four non-default configuration choices. Removing any one of them produces a different failure mode (separate-DB connect failure, deadlock on advisory lock, SQL syntax error at line 5, or hang on LISTEN/NOTIFY). See `codeconv/src/codeconv/db/engine.py::setup_dbos` and `codeconv/src/codeconv/_vendor/dbos_pglite_patch.py`.

### The four hooks

1. **DB-name override**: `DBOSConfig(application_database_url=url, system_database_url=url, dbos_system_schema="dbos")`. DBOS defaults to a sibling `<dbname>_dbos_sys` database that PGLite cannot create; point both roles at `postgres` and isolate via the `dbos` schema (FR-015).
2. **Pool sizing**: `db_engine_kwargs["pool_size"]=5, max_overflow=5` AND `sys_db_pool_size=5`. The vendored `pglite_engine_kwargs` defaults to `pool_size=1`, which deadlocks DBOS's `run_migrations` (holds one connection across an inner `ensure_dbos_schema` call). PGLite serialisation is preserved client-side because the bridge's `globalWorkChain` queues queries.
3. **uuid-ossp rewrite preserves semicolon**: the SQLAlchemy `before_cursor_execute` filter substitutes `CREATE EXTENSION "uuid-ossp"` → `SELECT 1;` (with terminating `;`). Without the semicolon, the next CREATE TABLE concatenates in DBOS's multi-statement migration text and PGLite errors at line 5.
4. **Disable LISTEN/NOTIFY**: `use_listen_notify=False` in `DBOSConfig`. PGLite does not implement `NOTIFY` end-to-end; leaving DBOS to poll triggers mystery hangs.

### PGLite specifics worth knowing

- `SELECT current_database()` returns `'template1'` regardless of the requested `dbname` in the connection URL. PGLite routes everything to template1. Functionally fine — do not "fix" by changing the URL.
- `pg_try_advisory_lock(...)` works and returns BOOLEAN. The earlier "cannot unpack non-iterable NoneType object" trace was a bug in the SQLAlchemy `before_cursor_execute` filter (must always return a tuple under `retval=True`), not a PGLite limitation.

### Where this is enforced

- `codeconv/src/codeconv/db/engine.py::setup_dbos` — hooks 1, 2, 4.
- `codeconv/src/codeconv/_vendor/dbos_pglite_patch.py::_install_sqlalchemy_uuid_ossp_filter` — hook 3.
- `codeconv/tests/test_engine.py::test_apply_to_engine_installed` — end-to-end smoke (codeconv side).

### Why this isn't fixed upstream

PGLite is feature 011's pre-req pattern; we treat it as a black-box dependency. The five workarounds above live in `codeconv/_vendor/dbos_pglite_patch.py` and `codeconv/db/engine.py` so a future DBOS or PGLite upgrade that lifts these limitations can drop them without touching consumer code.

---

## Issue 8: PGLite cluster files cannot live on exFAT

**Status**: ⚠️ ENVIRONMENT PREMISE VOID as of 2026-05-17 — D: was re-verified **NTFS** (`Get-Volume D` → `FileSystem: NTFS`, label `GAVRI_VOL_D`; the prior `Lexar`/exFAT drive was physically replaced). On *this* machine the exFAT crash no longer applies and `<repo>/.pgdb/` passes the guard. The `--data-dir` mechanism below remains valid and is retained as the canonical-cluster convention (see CLAUDE.md) and for any genuinely-exFAT checkout. (Originally: Mitigated via `--data-dir` override, release `v2026.05.11-2`.)
**Discovered**: 2026-05-11 when the freshly-merged `codeconv` was first invoked against the then-exFAT live repo
**Affects**: Any `codeconv` (or other unified-bridge consumer) invocation where the repo genuinely lives on an exFAT volume (no longer the case for D: on this machine)

### Summary

PGLite's WASM data files rely on POSIX-style file operations (atomic rename, advisory locks, certain mmap operations) that exFAT does not implement. When `.pgdb/` is created on exFAT, the bridge process crashes mid-DBOS-migration (typically around migration 4) — the client sees `psycopg.OperationalError: consuming input failed: server closed the connection unexpectedly` on a downstream query, and the bridge log is empty because the bridge died before flushing.

This is environment-dependent and does NOT show up in `pytest` runs, because pytest's `tmp_path` lives under `%TEMP%` on `C:` (NTFS). It surfaced only while the live repo at `D:\BSTDEV\research\GLP\GLPNET\` sat on a `Lexar`-labelled exFAT drive. **That drive was replaced 2026-05-17; D: is now `GAVRI_VOL_D` / NTFS, so this no longer reproduces on this machine** — the text below is retained for the general exFAT case and the canonical-cluster convention.

### Fix

Added a global `--data-dir <path>` flag to `codeconv` (cli.py / bridge_client.acquire_or_discover / workflow.run_discover). The flag decouples the PGLite cluster location from `--repo-root`. Point it at an NTFS directory:

```powershell
codeconv --data-dir $env:LOCALAPPDATA/codeconv-pgdb migrate
codeconv --data-dir $env:LOCALAPPDATA/codeconv-pgdb discover run
```

Sidecar (`<data-dir>/bridge.json`), OS lock (`<data-dir>.bridge.lock/`), consumer registrations (`<data-dir>.consumers/`), and force-shutdown marker (`<data-dir>/.shutdown`) all follow the override automatically.

### Detection

```powershell
Get-Volume <drive-letter> | Format-List FileSystem
```

If `FileSystem` is `exFAT`, `FAT32`, or anything other than `NTFS` / `ReFS`, you MUST use `--data-dir` to point the cluster at an NTFS path. The repo source files themselves are fine on exFAT.

### Why this isn't a code fix

PGLite is a WASM build of upstream PostgreSQL. It assumes a POSIX-class filesystem under the data directory. We cannot fix this in `codeconv` itself — only route around it. The flag and this doc-note are the mitigation.

---

## Issue 9: live shared cluster is PG16; 0.4.5 bridge is PG17 (not forward-compatible)

**Status**: Worked around for feature 015 via a separate side cluster (option c); the shared-cluster migration is gated/pending
**Discovered**: 2026-05-16 completing feature 015's live-cluster tasks (T039/T040/T043)
**Affects**: Any live-cluster `codeconv` work on a branch whose bridge is PGLite ≥0.3.0

### Summary

The canonical shared cluster `C:/pglite/research/glpnet/` (`PG_VERSION`=16) was created by old PGLite 0.2.x. The PGLite-0.4.5 upgrade (aborted-txn fix) embeds PostgreSQL 17. PGLite data dirs are **not** forward-compatible (0.2.x=PG16, 0.3.0+=PG17). That cluster also holds D2NET's `public` schema, so an in-place dump/restore is a gated cross-tool op (runbook: `docs/pglite-0.4-cluster-migration-runbook.md`, requires Gabi's go + D2NET sign-off, not yet executed).

### Workaround (used for feature 015)

Stand up a separate fresh PG17 cluster at a side path (e.g. `C:/pglite/research/glpnet-015/`), `codeconv --data-dir <side> migrate` then `discover run`, and run all live tasks there. D2NET's shared cluster is left untouched until D2NET drives its own migration.

### Doc staleness noticed (not yet fixed in spec/quickstart)

`specs/015-codeconv-depgraph/quickstart.md` Flow H still uses `--data-dir .pgdb` and reads `.pgdb\bridge.json`; the bridge sidecar is codeconv-managed and ephemeral (no fixed `bridge.json` for arbitrary data-dirs — discover via `bridge_client.acquire_or_discover`). The cycle-fixture check references `$json.cycle_count` but the key is `$json.metadata.cycle_count`. These are doc bugs, not product bugs — the bridge/CLI are correct.

---
