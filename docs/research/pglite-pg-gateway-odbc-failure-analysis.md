# Root-cause analysis: PGLite + pg-gateway + ODBC stack failures

**Date**: 2026-04-30
**Context**: During implementation of `D2NET.Init` (specs/002-d2net-init), the planned storage stack — PGLite (WASM Postgres) accessed through a Node.js bridge using `pg-gateway` 0.3.0-beta.4, reachable from .NET via psqlODBC and Npgsql — failed end-to-end in two distinct ways. The feature was shipped on an embedded SQLite database instead (clarification Q6 in `specs/002-d2net-init/spec.md`). This document records the deep-dive investigation that followed and a robust fix path back to PGLite if it is ever wanted.

## TL;DR

Two visible symptoms had a single underlying causal chain:

1. **PGLite issue**: `pglite.execProtocolRaw(buf)` returns the standard Postgres response **plus an implicit trailing `Z` (ReadyForQuery)** on every call, regardless of whether the input batch ended with a `S` (Sync). When pg-gateway forwards individual extended-protocol frames (Parse, Bind, Describe, Execute, Sync) to `execProtocolRaw` one at a time, the response stream gains *extra* `Z` frames between every legitimate response message. Standard Postgres clients (Npgsql in particular) state-machine on the wire and reject the unexpected `Z` mid-batch — hence "Received backend message ReadyForQuery while expecting BindCompleteMessage."

2. **pg-gateway issue**: even after batching client frames so PGLite sees one complete `P/B/D/E/S` group per call (which gives a clean canonical response), pg-gateway 0.3.0-beta.4 corrupts the wire stream further. The exact corruption was not pinpointed (more on that below) but its visible effects are: Npgsql raises `PostgresException: 123: Message code not yet implemented`, and psqlODBC's native parser triggers `STATUS_STACK_BUFFER_OVERRUN` (`__fastfail`, exit code `0xC0000409`) and kills the host process.

3. **Conclusion**: bypassing `pg-gateway` entirely with a hand-rolled minimal Postgres-wire bridge resolves *both* symptoms. The replacement is small (~150 lines of Node.js) and uses only the standard library plus `@electric-sql/pglite`.

## Reproduction environment

- Windows 11 Pro 10.0.26200, x64
- Node.js v24.14.0, npm 11.9.0
- .NET SDK 8.0.420
- `@electric-sql/pglite@0.2.17`
- `pg-gateway@0.3.0-beta.4` (also tested against `0.2.4` — different API, same class of failures)
- `Npgsql@8.0.3` (.NET extended-protocol Postgres client)
- psqlODBC `PostgreSQL ODBC Driver(UNICODE)` (modern installer; legacy alias `PostgreSQL Unicode(x64)` also installed)

## Investigation playground

All artifacts live under `C:\Users\gavri\AppData\Local\Temp\pgwire-investigation\`:

- `package.json` — pins the npm versions above.
- `bridge-traced.mjs` — pg-gateway-based bridge that logs every wire frame in both directions.
- `bridge-batched.mjs` — pg-gateway-based bridge that buffers extended-protocol client frames until a flush-worthy tag (`S`, `Q`, `X`, `H`, `c`, `f`) arrives, then forwards the entire batch as one `execProtocolRaw` call. Removes symptom 1; symptom 2 still present.
- `bridge-direct.mjs` — minimal hand-rolled Postgres-wire bridge that handles `StartupMessage` (and `SSLRequest`) by hand, skips pg-gateway, applies the same batching, and forwards to PGLite. **Removes both symptoms.**

Test harnesses live alongside under `C:\Users\gavri\AppData\Local\Temp\NpgsqlExtTest\` and `\OdbcExtTest\`.

## Symptom 1: extra ReadyForQuery between extended-protocol frames

Wire trace (truncated) from `bridge-traced.mjs` while Npgsql ran `SELECT 1`:

```
[onMessage #4] CLIENT->BRIDGE  P  Parse           len=16
  PGLite returned 103 bytes
  BRIDGE->CLIENT  N  NoticeResponse   len=91
  BRIDGE->CLIENT  1  ParseComplete    len=4
  BRIDGE->CLIENT  Z  ReadyForQuery    len=5     <-- ⚠️ extra Z
[onMessage #5] CLIENT->BRIDGE  B  Bind            len=14
  BRIDGE->CLIENT  N  NoticeResponse   len=92
  BRIDGE->CLIENT  2  BindComplete     len=4
  BRIDGE->CLIENT  Z  ReadyForQuery    len=5     <-- ⚠️ extra Z
[onMessage #6] CLIENT->BRIDGE  D  Describe        len=6
  BRIDGE->CLIENT  T  RowDescription   len=33
  BRIDGE->CLIENT  Z  ReadyForQuery    len=5     <-- ⚠️ extra Z
[onMessage #7] CLIENT->BRIDGE  E  Execute         len=9
  BRIDGE->CLIENT  D  DataRow          len=14
  BRIDGE->CLIENT  C  CommandComplete  len=13
  BRIDGE->CLIENT  Z  ReadyForQuery    len=5     <-- ⚠️ extra Z
[onMessage #8] CLIENT->BRIDGE  S  Sync            len=4
  BRIDGE->CLIENT  Z  ReadyForQuery    len=5     <-- the legitimate Z
```

Npgsql expects the extended-protocol response sequence `1 2 T D C Z`. It receives `1 Z 2 Z T Z D C Z Z`. After consuming `1 ParseComplete`, the next byte is `Z` instead of `2 BindComplete` — error.

**Fix**: collect client frames until a flush-worthy tag appears (`S` Sync, `Q` simple Query, `X` Terminate, `H` Flush, `c` CopyDone, `f` CopyFail), then call `pglite.execProtocolRaw(batch)` with the entire batch in one shot. PGLite's implicit final `Z` then aligns with the client's explicit `S` Sync — the response is canonical.

This is implemented identically in both `bridge-batched.mjs` and `bridge-direct.mjs`:

```js
const FLUSH_TAGS = new Set([0x51, 0x53, 0x58, 0x48, 0x63, 0x66]);

while (buffered.length >= 5) {
  const len = buffered.readUInt32BE(1);
  if (buffered.length < 1 + len) return;        // wait for more bytes
  const frame = buffered.subarray(0, 1 + len);
  buffered = buffered.subarray(1 + len);
  pending.push(frame);
  if (FLUSH_TAGS.has(frame[0])) {
    const batch = Buffer.concat(pending);
    pending.length = 0;
    socket.write(Buffer.from(await pglite.execProtocolRaw(batch)));
  }
}
```

## Symptom 2: pg-gateway corrupts the response even with batching

After applying the batching fix, `bridge-batched.mjs` produces well-formed responses on the PGLite side (verified by hex-dump: `N 1 N 2 T D C Z` — the canonical sequence). Yet:

- **Npgsql** raises `PostgresException: 123: Message code not yet implemented`. The "code 123" is non-standard (real Postgres SQL states are five-character strings); this is what Npgsql 8.x emits when its inner backend-message dispatcher hits a tag byte it doesn't recognise — i.e. the wire stream Npgsql actually receives is *not* the same byte sequence the bridge handed to pg-gateway.
- **psqlODBC** crashes with `STATUS_STACK_BUFFER_OVERRUN` (`__fastfail(FAST_FAIL_FATAL_APP_EXIT)`, NTSTATUS `0xC0000409`) the moment it begins parsing pg-gateway's response. This is consistent with a malformed length-prefixed string overflowing a fixed-size stack buffer in psqlODBC's native message parser.

The exact byte-level corruption introduced by pg-gateway between the bridge handler returning `Uint8Array` and the bytes hitting the client socket was *not* fully characterised in this investigation. (Cheap next step: insert a transparent TCP proxy between Npgsql and the bridge to capture exactly what arrives on the client side, then byte-diff against what `pglite.execProtocolRaw` produced.)

What is empirically clear: replacing pg-gateway with a hand-rolled startup handler **and** keeping the same batching logic eliminates both client-side symptoms.

## Robust fix: `bridge-direct.mjs`

Replace `pg-gateway` with a minimal Postgres-wire server that handles only the protocol surface D2NET actually uses:

1. **Startup**: read 4-byte length + 4-byte protocol-or-magic from the socket. If magic is `0x04D2162F` (`SSLRequest`), reply with `N` (no SSL). Otherwise treat as `StartupMessage`, send `R AuthenticationOk` + a handful of `S ParameterStatus` (`server_version`, `server_encoding`, `client_encoding`, `DateStyle`, `integer_datetimes`, `standard_conforming_strings`) + `K BackendKeyData(1, 1)` + `Z ReadyForQuery('I')`.
2. **Steady state**: read tagged client frames, buffer until a flush-worthy tag arrives, hand the batch to `pglite.execProtocolRaw`, write the response straight back.
3. **Shutdown**: stdin EOF / SIGTERM / SIGINT exit cleanly.

That's it. Concrete file is at `C:\Users\gavri\AppData\Local\Temp\pgwire-investigation\bridge-direct.mjs`; verbatim verification:

```text
=== bridge-direct.mjs + Npgsql (extended protocol) ===
Connected. Server: 16.0
SELECT 1                  → row [1]                  total 1
SELECT 1, 'a'             → row [1, a]               total 1
SELECT * FROM t ORDER BY x → rows [1] [2] [3]        total 3

=== bridge-direct.mjs + psqlODBC ===
Connected. Server: 16.0.0
SELECT 1                  → row [1]                  total 1
SELECT 1, 'a'             → row [1, a]               total 1
SELECT * FROM t ORDER BY x → rows [1] [2] [3]        total 3
```

No crashes, no protocol mismatches, no extra ReadyForQuery messages. Both clients work.

## Implications for D2NET

The currently-shipped (PR #2, tag `v2026.04.30-2`) D2NET workspace uses **embedded SQLite** via `Microsoft.Data.Sqlite`. That choice removes the entire dependency surface (no Node.js, no ODBC driver, no bridge process) and remains the recommended default.

If the original PGLite plan is ever re-pursued — e.g. because the user wants Postgres-flavoured features (JSONB operators, arrays, full-text search, the `pg_*` system catalogs) — the path is:

1. Vendor `bridge-direct.mjs` (or its equivalent) under `tools/d2net/src/D2Net.<X>/pgbridge/server.mjs`.
2. Replace the SQLite-specific code (`SqliteConnection`, `Microsoft.Data.Sqlite`) with `OdbcConnection` (psqlODBC) or `Npgsql`. `Npgsql` is recommended over ODBC: same wire compatibility, no native driver to install, fewer crash surfaces.
3. Use the per-invocation bridge lifecycle from the original plan (start at command begin, kill at command end). The TCP-port-in-use logic (FR-011b) becomes meaningful again.
4. Keep `pg-gateway` *off* the dependency list.

## Open question: contributing fixes upstream

Two upstream packages would benefit from PRs:

- **PGLite**: document or guard against the implicit-Sync behavior of `execProtocolRaw`. Either add a parameter that suppresses the trailing `Z`, or document the requirement that callers must pass the entire `P/B/D/E/S` batch.
- **pg-gateway**: the response-stream corruption that breaks Npgsql + psqlODBC after batching deserves a proper diff against real Postgres' wire output. A small reproducer (the same `bridge-batched.mjs` from this investigation, plus the Npgsql harness) would be a one-paragraph issue.

These are good-citizen TODOs but explicitly out of scope for the GLPNET workspace itself.
