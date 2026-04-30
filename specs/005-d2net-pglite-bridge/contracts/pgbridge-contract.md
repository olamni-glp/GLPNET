# PGLite Bridge Subprocess Contract

**Feature**: `005-d2net-pglite-bridge` — see [spec.md](../spec.md), [plan.md](../plan.md), [research.md](../research.md)

The vendored Node.js bridge (`tools/d2net/src/D2Net.Init/pgbridge/bridge-direct.mjs`) is a verbatim copy of the reference implementation at `docs/research/pgbridge-reference/bridge-direct.mjs`. This contract specifies the externally observable behaviour the .NET caller (`PgBridgeProcess.cs`) depends on. Any future deviation MUST be justified against the failure-analysis document at `docs/research/pglite-pg-gateway-odbc-failure-analysis.md` — anything that risks reintroducing the historical Npgsql / psqlODBC failures is forbidden by FR-008.

## Invocation

```bash
node bridge-direct.mjs --pgdir <abs path to .D2NET/pgdb> --port <int> [--bind 127.0.0.1]
```

| Flag       | Required | Default     | Notes                                                                                              |
|------------|----------|-------------|----------------------------------------------------------------------------------------------------|
| `--pgdir`  | yes      | (none)      | Absolute path to the PGLite data directory. Created if missing. Must be writable.                   |
| `--port`   | yes      | (none)      | TCP port to listen on. The .NET caller passes either `--bridge-port` value or the spec default 54400. |
| `--bind`   | no       | `127.0.0.1` | Bind address. The .NET caller MUST NOT pass anything other than `127.0.0.1` in v1 (no remote bind). |

## Stdout protocol (machine-readable)

The bridge MUST emit exactly one of the following lines on stdout, terminated by `\n`, before any other stdout output:

| Line                              | Meaning                                                                                |
|-----------------------------------|----------------------------------------------------------------------------------------|
| `BRIDGE_READY port=<port> pid=<pid>` | Listener is bound; PGLite is initialised; ready to accept Postgres-wire connections.   |
| `BRIDGE_ERROR <message>`          | Fatal startup failure. The `<message>` is one of the documented failure modes below.   |

After `BRIDGE_READY` the bridge MUST emit nothing further on stdout for the lifetime of the process.

After `BRIDGE_ERROR` the bridge MUST exit with a non-zero exit code.

## Stderr protocol (human-readable)

The bridge MAY emit any number of human-readable diagnostic lines on stderr at any time. The .NET caller captures stderr but does not parse it. On failure (BRIDGE_ERROR or unexpected exit) the .NET caller surfaces the last few stderr lines as debugging context — never as the primary user-facing message.

## Documented BRIDGE_ERROR `<message>` values

| Message                                  | Meaning                                                                                          | .NET-side mapping                                  |
|------------------------------------------|--------------------------------------------------------------------------------------------------|----------------------------------------------------|
| `pglite_init_failed <PGLite error>`      | PGLite could not open the data directory. Likely corrupt or permission-denied.                  | Exit code 7 (`DbOpenFailed`) + recovery hint pointing at `--FORCE --DELETE-EXISTING` (Q4) |
| `listen <Node net error>`                | TCP `listen()` failed. Most commonly `EADDRINUSE`.                                              | Exit code 17 (`BridgePortInUse`)                  |
| `missing --pgdir`                        | Required arg missing.                                                                           | Exit code 7 (`DbOpenFailed`)                       |
| `missing --port`                         | Required arg missing.                                                                           | Exit code 7 (`DbOpenFailed`)                       |

The set is closed by this contract. Future failure modes added to `bridge-direct.mjs` MUST extend this table.

## Wire protocol

After `BRIDGE_READY`, the bridge accepts Postgres frontend/backend protocol v3 connections. Specifically:

- **Startup**: handles `StartupMessage` (any non-zero protocol-version magic) by replying `R AuthenticationOk`, six `S ParameterStatus` frames (`server_version=16.0`, `server_encoding=UTF8`, `client_encoding=UTF8`, `DateStyle=ISO, MDY`, `integer_datetimes=on`, `standard_conforming_strings=on`), `K BackendKeyData(1, 1)`, and `Z ReadyForQuery('I')`.
- **SSLRequest**: replies `N` (no SSL). Clients MUST disable TLS.
- **Steady state**: buffers tagged client frames until a flush-worthy tag arrives (`Q` simple Query, `S` Sync, `X` Terminate, `H` Flush, `c` CopyDone, `f` CopyFail) and forwards the entire batch as one `pglite.execProtocolRaw` call. The PGLite response is shipped straight back to the socket.

## Lifecycle

| Stage    | Trigger                                | Bridge action                                                                                                                                                                                                                                                                       |
|----------|----------------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Startup  | process spawn                          | Initialise PGLite against `--pgdir`. On success: `BRIDGE_READY`. On failure: `BRIDGE_ERROR pglite_init_failed`.                                                                                                                                                                     |
| Bind     | (after PGLite init)                    | `server.listen(port, bind)`. On failure: `BRIDGE_ERROR listen`.                                                                                                                                                                                                                     |
| Serving  | client connections                     | Per-connection: handle Startup → forward batched frames to `execProtocolRaw` → write response.                                                                                                                                                                                      |
| Shutdown | stdin close (EOF) **or** SIGTERM **or** SIGINT | Exit cleanly via `process.exit(0)`. The bridge does NOT drain in-flight transactions — clients connecting at the moment of shutdown may see a closed socket. The .NET caller is responsible for closing its own SQL connection before signalling shutdown. |

## .NET-side contract obligations (`PgBridgeProcess.cs`)

The .NET caller MUST:

1. Spawn the bridge with stdin/stdout/stderr piped (not inherited).
2. Read stdout line-by-line on a background task; capture the first line as the handshake.
3. Wait at most **15 seconds** for the handshake (FR-005). On timeout: kill the process; abort with exit code 15 (`BridgeStartFailed`).
4. On `BRIDGE_READY`: open `NpgsqlConnection`. The connection MUST use the connection string from `D2NET-Settings.json`'s `connection.connection_string` (same machine, same instance) — the Npgsql one.
5. On any `BRIDGE_ERROR <message>`: surface the verbatim message to stderr; map to the appropriate exit code (table above); for `pglite_init_failed`, ALSO emit the recovery hint per FR-005 of the spec.
6. On `Dispose()`: close NpgsqlConnection → close bridge stdin → wait 5 s → SIGTERM → wait 2 s → hard-kill. The kill paths emit a non-fatal warning to stderr but do not change the exit code.
7. Register a `Console.CancelKeyPress` handler so Ctrl-C tears down the bridge before the .NET process exits.
8. Register `AppDomain.CurrentDomain.ProcessExit` so unexpected .NET termination still tears down the bridge.

## What the bridge does NOT do

- **No authentication enforcement**: any user/password is accepted. The bridge is bound to `127.0.0.1`; this is sufficient for v1's threat model. Documented in spec Assumptions ("placeholder credentials").
- **No TLS termination**: `SSLRequest` is rejected with `N`. Clients MUST connect with `SSL Mode=Disable` / `SSLmode=disable`.
- **No remote bind**: only `127.0.0.1`. The .NET caller MUST NOT pass `--bind 0.0.0.0` in v1.
- **No multi-database / multi-tenant**: PGLite is single-database. The persisted `database` field (`d2net`) is informational; the bridge ignores it during the StartupMessage handshake.
- **No COPY**: `COPY FROM STDIN` and `COPY TO STDOUT` are not exercised by D2NET.Init's SQL surface; the bridge inherits whatever PGLite supports, but COPY is not part of the supported surface (spec FR-011).

## Smoke-test seed data: REMOVED in production vendored copy

The reference bridge at `docs/research/pgbridge-reference/bridge-direct.mjs` seeds a small `t (x INT)` table with rows `(1), (2), (3)` on every startup as a debug aid. **The production vendored copy at `tools/d2net/src/D2Net.Init/pgbridge/bridge-direct.mjs` MUST NOT include this seed.** The seed `DELETE FROM t; INSERT INTO t VALUES …` runs on every bridge spawn, including inspection invocations — that mutates the data tree and violates the shipped 002 SC-009 ("inspection options modify zero bytes under .D2NET") which 005 FR-013 explicitly preserves.

Production divergence from the reference (T001):
- DELETE the two `pglite.exec("CREATE TABLE IF NOT EXISTS t (x INT);")` and `pglite.exec("DELETE FROM t; INSERT INTO t VALUES (1), (2), (3);")` calls.
- DELETE the `console.error('[pglite] ready, seeding test schema')` line.
- Add a single `console.error('[pglite] ready')` line in their place so startup logging stays informative.

This is the only sanctioned divergence from the reference. It is verified by test T021 sub-case (f), which asserts that file mtimes under `pgdb/` do not change across a bridge spawn-and-dispose cycle without SQL.

## Restrictions on bridge code modification

- The bridge implementation MUST remain a verbatim or near-verbatim copy of `docs/research/pgbridge-reference/bridge-direct.mjs`. Functional equivalence to the RCA-verified script is mandatory.
- Bumping `@electric-sql/pglite` past 0.2.17 is allowed (FR-016) but MUST be accompanied by a re-run of the RCA's verification steps against both Npgsql and psqlODBC — recorded in the upgrade PR description.
- `pg-gateway` MUST NEVER be added to `package.json` or appear in `node_modules` — ban enforced by `scripts/verify-pgbridge-deps.ps1` at build time (SC-010).
