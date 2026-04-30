# Quickstart — D2NET.Init (PGLite-backed)

**Feature**: `005-d2net-pglite-bridge` — see [spec.md](spec.md), [plan.md](plan.md)

After this upgrade ships, the developer flow is unchanged for the four CLI inputs and the prompt cycle, but the workspace database is now a PGLite WASM instance reachable through a per-invocation bridge on `127.0.0.1:54400` (default; overridable via `--bridge-port`).

## Prerequisites

- **.NET 8 SDK** (`8.0.420` or compatible — pinned in `tools/d2net/global.json`).
- **Node.js 20 LTS or later** on PATH. Verify with `node --version`. (R8.)
- A repository root: a directory containing `.git/`, an existing `.D2NET/`, or a subdirectory matching the supplied source name (FR-002 of 002).

No `npm install` is required — the bridge bundle (`bridge-direct.mjs` + pinned `@electric-sql/pglite@0.2.17`) is shipped inside the `D2Net.Init` artifact (R2 / FR-015).

## 1. Build and run a fresh init

From the repo root:

```text
dotnet build tools/d2net/D2Net.sln
tools\d2net\src\D2Net.Init\bin\Debug\net8.0\d2net-init.exe ^
    --source glp_runtime ^
    --target-extension _net ^
    --target glp_runtime_net ^
    --accept-suggested-exclusions ^
    --non-interactive
```

Expected output (paths abbreviated):

```text
[bridge] BRIDGE_READY port=54400 pid=12345
workspace ready:
  workspace dir       D:\repo\.D2NET
  settings file       D:\repo\.D2NET\D2NET-Settings.json
  PGLite data tree    D:\repo\.D2NET\pgdb (engine=pglite, port=54400)
  source              glp_runtime
  target              glp_runtime_net (extension _net)
  approved exclusions 4
  dart files indexed  237
  created at          2026-04-30T13:42:11Z
```

**Verify the storage swap**: `.D2NET/pgdb/` is now a directory of files (`PG_VERSION`, `base/`, `global/`, `pg_xact/`, `pg_wal/`, ...) — **not** a single `workspace.sqlite` file. Per SC-003.

## 2. Inspect via CLI (default port)

```text
d2net-init --list                # plain text, sorted by full_path
d2net-init --list --json         # JSON
d2net-init --Exclusions          # plain text
d2net-init --current-phase       # reports "no active phase" until downstream commands populate phase_status
```

Each invocation spins up its own bridge subprocess on the persisted `connection.port`, runs a single SELECT, and tears the bridge down. No daemon is ever left running.

## 3. Inspect via external Postgres client (during a D2NET command)

In one shell:

```text
d2net-init --list   # keeps a bridge alive for its lifetime
```

In a second shell, with `psql` installed:

```text
$ psql "Host=127.0.0.1 Port=54400 Database=d2net Username=d2net Password=d2net SslMode=Disable"
psql (16.1, server 16.0)
d2net=> SELECT count(*) FROM dart_files;
 count
-------
   237
(1 row)

d2net=> SELECT path, kind FROM excluded_directories ORDER BY path;
     path     |  kind
--------------+---------
 .git         | tool
 archive_2024 | pattern
 build        | tool
 legacy_lib   | pattern
(4 rows)
```

Or with **DBeaver** / **JetBrains DataGrip**: open a new Postgres connection using the values in `.D2NET/D2NET-Settings.json`'s `connection` block. The connection `connection_string` field is directly pasteable into Npgsql/.NET tools; the `connection_string_odbc` field is pasteable into ODBC tools.

When the D2NET command exits, the bridge port closes and external clients receive "connection refused" until the next D2NET command is invoked. This is the per-invocation lifecycle (FR-007).

## 4. Re-init over an existing PGLite workspace

```text
d2net-init [...]
# stderr: workspace already exists at D:\repo\.D2NET; use --FORCE --DELETE-EXISTING to recreate it
# exit code 3
```

```text
d2net-init [...] --FORCE --DELETE-EXISTING
# .D2NET is fully removed and rebuilt; new connection block, new created_at
# exit code 0
```

## 5. Re-init over a SQLite-era (002) workspace

If the working tree has a workspace from before this upgrade (a `.D2NET/pgdb/workspace.sqlite` file plus a settings JSON with `connection.engine: sqlite`):

```text
d2net-init [...]
# stderr: workspace already exists at D:\repo\.D2NET; use --FORCE --DELETE-EXISTING to recreate it
# exit code 3
```

```text
d2net-init [...] --FORCE --DELETE-EXISTING
# the SQLite file and the entire .D2NET tree are deleted, a fresh PGLite-backed workspace is created
# exit code 0
```

No automatic data migration is performed — the source tree is rewalked and the inventory rebuilt. (Per spec Out-of-Scope and FR-014.)

## 6. Recover from a corrupt PGLite data tree

If a previous run was hard-killed or the PGLite data tree was otherwise damaged, the next D2NET invocation will surface the bridge's verbatim error followed by a recovery hint:

```text
d2net-init --list
# stderr:
#   PGLite bridge failed to open the workspace database:
#   BRIDGE_ERROR pglite_init_failed cannot read PG_VERSION: ENOENT
#   The workspace database appears to be unreadable. To rebuild from the source tree, re-run with:
#     d2net-init --FORCE --DELETE-EXISTING [other flags...]
# exit code 7
```

## 7. Bridge port collision

```text
d2net-init --list   # one shell, holds 54400
d2net-init --list   # another shell
# stderr: PGLite bridge port 54400 is already in use. Either stop the conflicting process, or supply --bridge-port <n>.
# exit code 17
```

```text
d2net-init --list --bridge-port 54401   # works
```

Note: inspection commands invoked with `--bridge-port` do not modify the persisted `connection.port`. The value persisted in settings stays whatever init wrote (Q3 clarification).

## 8. Run the test suite

```text
dotnet test tools/d2net/D2Net.sln
```

Tests spawn their own bridge subprocesses on free ports (the `PgBridgeHarness` fixture). On Windows, the modern `PostgreSQL ODBC Driver(UNICODE)` is required for the small subset of tests under `ExternalClientTests.cs` that exercises the SC-005 psqlODBC contract — those tests are skipped if the driver is not installed. Npgsql tests always run.

## 9. Verify build-time invariants

```text
pwsh scripts/verify-pgbridge-deps.ps1
# OK: pgbridge bundle has @electric-sql/pglite@0.2.17 and zero pg-gateway transitive deps.
```

This script is wired into `dotnet build` via an MSBuild `BeforeTargets="Build"` hook so a regression on FR-008 / SC-010 fails CI.
