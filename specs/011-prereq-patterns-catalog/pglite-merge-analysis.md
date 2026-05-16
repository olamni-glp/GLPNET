# pglite Merge Analysis — bridge-direct.mjs × pglite_bridge.mjs

**Branch**: `011-prereq-patterns-catalog` | **Date**: 2026-05-09 | **Deliverable for**: FR-009 / SC-005

This document classifies every distinguishing feature of the two pre-merge bridges so no learning is silently dropped. The merged bridge lives at `prereq-patterns/pglite/pglite_bridge.mjs`; this analysis is the static analogue of the SC-003 / SC-004 regression checks deferred to the first glpnet feature that adopts the bridge.

## Pre-merge sources

| Source | Path | Lines |
|---|---|---|
| glpnet | `docs/research/pgbridge-reference/bridge-direct.mjs` | 156 |
| AIGRID | `D:/BREENDEV/aigrid/AWS-Infra/prereq-patterns/pglite/pglite_bridge.mjs@004a-opskit-sidecar-autospawn` (canonical at `D:/BREENDEV/aigrid/AWS-Infra/src/breenlake/_vendor/pglite_bridge.mjs`, identical copies in `src/opskit/_vendor/` and `.opskit-pglite.bridge/`) | 397 |

**Lineage observation**: AIGRID's `pglite_bridge.mjs` self-identifies (line 3 of the file) as "originally adapted from GLPNET tools/d2net/src/D2Net.Init/pgbridge/bridge-direct.mjs". The AIGRID file is therefore a *descendant* of glpnet's investigation, with three classes of additions: serialization (`globalWorkChain`), session-state safety (synthetic `ROLLBACK`), and `COPY ... FROM STDIN` interception (PGlite WASM does not implement COPY-IN over the wire). This shapes the classification below: glpnet's "no-pg-gateway" lineage is preserved transitively, not by re-grafting.

## Classification vocabulary

| Token | Meaning |
|---|---|
| `present-in-merged` | The feature is in the merged file at `prereq-patterns/pglite/pglite_bridge.mjs`, in the same form or a strictly more general form. |
| `superseded-with-rationale` | The feature is replaced by a different implementation of the same intent. Rationale states what supersedes it and why. |
| `dropped-with-rationale` | The feature is intentionally not in the merged file. Rationale states why dropping it does not lose substantive behaviour. |

## glpnet `bridge-direct.mjs` — feature classification

| # | Feature | Site (lines) | Classification | Rationale |
|---|---|---|---|---|
| G1 | Hand-rolled minimal Postgres-wire server (no pg-gateway dependency) | 51–134 | `present-in-merged` | Skeleton-level lineage. AIGRID's `pglite_bridge.mjs` is itself a fork of `bridge-direct.mjs`'s startup + frame-loop structure (AIGRID file header line 3). The merged file inherits this directly — pg-gateway is not imported anywhere. |
| G2 | SSLRequest negotiation (reply with `'N'`) | 83–88 | `present-in-merged` | Verbatim in the merged file's startup phase. Same magic constant `0x04D2162F`, same `'N'` reply. |
| G3 | Synthetic StartupMessage handshake (AuthOk + 6× ParameterStatus + BackendKeyData + ReadyForQuery) | 89–104 | `present-in-merged` | Same set of seven message builders, same ParameterStatus tuples (`server_version=16.0`, `server_encoding=UTF8`, `client_encoding=UTF8`, `DateStyle='ISO, MDY'`, `integer_datetimes=on`, `standard_conforming_strings=on`), same `BackendKeyData(1, 1)` placeholder. |
| G4 | Frame-by-frame batched forwarding via `endsAtFlushBoundary()` and `FLUSH_TAGS` | 23, 58–70, 110–128 | `present-in-merged` | Bit-for-bit. `FLUSH_TAGS = {0x51, 0x53, 0x58, 0x48, 0x63, 0x66}` is identical; loop structure is identical. **This is the implicit-Sync fix** — the bridge waits until the buffered bytes end on a Sync / Flush / Terminate / Copy-Done / Copy-Fail boundary before forwarding, so half-batches never hit PGLite mid-pipeline (which previously triggered PGLite's implicit-Sync-after-execProtocolRaw bug). |
| G5 | `pg-gateway` 0.3.0-beta.4 response-corruption avoidance | by absence | `present-in-merged` (transitively) | The merged file does not import pg-gateway. No corruption surface exists. |
| G6 | Npgsql / psqlODBC client compatibility | functional consequence of G1+G4+G5 | `present-in-merged` (transitively) | The compatibility came from the absence of pg-gateway corruption combined with correct flush-boundary batching. Both halves are preserved. (SC-003 is a *runtime* verification deferred to the first glpnet adopter; this row records the *static* preservation.) |
| G7 | `BRIDGE_READY port=<port> pid=<pid>` stdout signal | 138 | `present-in-merged` | Re-grafted onto AIGRID's listen() callback so direct-spawn (without the AIGRID Python sidecar) can still synchronise on a stdout token. The AIGRID sidecar's TCP-readiness probe loop (`sidecar.py` lines 197-213, cited in `prereq-patterns/pglite/sources.md`) is the alternative path; both work. |
| G8 | `BRIDGE_ERROR ...` error tokens on stdout | 16, 136, 153–154 | `superseded-with-rationale` | Rerouted to stderr in the AIGRID convention (`[bridge] BRIDGE_ERROR ...`). Stdout is reserved for the `BRIDGE_READY` token (G7) and AIGRID's own `[bridge] start ...` token. Stderr is the conventional channel for diagnostic output and avoids polluting the stdout-token discovery path. The error-token *names* (`pglite_init_failed`, `listen`, `missing --data-dir`, `missing --port`) are preserved verbatim. |
| G9 | Test schema seeding (`CREATE TABLE IF NOT EXISTS t (x INT); …`) | 19–21 | `dropped-with-rationale` | Investigation-harness artefact, not part of the pattern. The seed table existed only so an Npgsql / psqlODBC smoke test could `SELECT * FROM t`. The merged bridge serves an empty PGLite session; consumers seed their own schema via Alembic / DDL / `CREATE TABLE` issued through the bridge. Retaining the seed would surprise downstream consumers with a phantom `t` table. |
| G10 | CLI flag `--pgdir` for the data directory | 149 | `superseded-with-rationale` | Renamed to `--data-dir` (AIGRID convention). The flag's *meaning* is identical (path to the on-disk PGLite directory; auto-created if missing). The longer name is more self-documenting and matches AIGRID's Python sidecar invocation. |
| G11 | CLI flag `--bind` for the listen address (default `127.0.0.1`) | 151 | `superseded-with-rationale` | Renamed to `--host` (AIGRID convention). Same default. Same semantics. |
| G12 | Console logs `[pglite] ready, seeding test schema`, `[server] new client`, `[server] client disconnected`, `[server] socket error`, `[forward error]` | 19, 52, 124, 132–133 | `superseded-with-rationale` | Unified under the `[bridge]` prefix (AIGRID convention). One prefix per process is easier to grep across logs that may aggregate the bridge with other components. The "client connected/disconnected" granular logs were dropped because they are noise once the bridge is running multiple connections; socket errors are still logged (`[bridge] socket_error`). |
| G13 | `process.stdin.on('end', exit)` lifecycle | 141–142 | `present-in-merged` (gated) | Preserved when `--daemon` is NOT passed (matches glpnet's "stdin closes ⇒ parent died ⇒ exit" semantics). Disabled when `--daemon` is passed (the AIGRID Python sidecar spawns with `stdin=DEVNULL` so the bridge survives a detached startup). The `--daemon` flag is the single new bit; the original behaviour is the default. |
| G14 | `SIGTERM` / `SIGINT` clean-exit handlers | 143–144 | `present-in-merged` | Verbatim. |
| G15 | `mkdirSync(args.pgdir, { recursive: true })` if missing | 10 | `present-in-merged` | Same auto-create behaviour. |
| G16 | `PGlite.create(args.pgdir)` initialization with try/catch | 12–18 | `present-in-merged` | Same pattern, same error path (initialization failure prints `BRIDGE_ERROR pglite_init_failed <msg>` and exits 1). |

## AIGRID `pglite_bridge.mjs` — feature classification

| # | Feature | Site (lines) | Classification | Rationale |
|---|---|---|---|---|
| A1 | `globalWorkChain` (global FIFO across all connections) | 48, 294, 354 | `present-in-merged` | The single most load-bearing AIGRID addition. PGLite has one shared session; concurrent `execProtocolRaw()` calls interleave responses on the wire and corrupt psycopg's state machine (and would similarly corrupt any other client's). The chain ensures sequential dispatch. **This is the half of the single-session invariant that lives bridge-side**; the matched half lives client-side as `pool_size=1` + serialised access (documented in `applicability.md`). |
| A2 | Synthetic `ROLLBACK` on startup handshake | 254 (`try { await pglite.exec('ROLLBACK'); } catch (_e) {}`) | `present-in-merged` | Clean-slate per-connection invariant. Because PGLite's session is shared across all bridge clients, a prior client that left a transaction in error state would poison every subsequent client until something rolls back. Best-effort `ROLLBACK` per startup handshake. |
| A3 | `endsAtFlushBoundary()` + `FLUSH_TAGS` (lines 43, 194–206) | 43, 194–206 | `present-in-merged` | Same algorithm as glpnet G4; AIGRID inherited it. (Cross-listed for completeness so the analysis covers both lineage directions.) |
| A4 | `COPY ... FROM STDIN` interception via `COPY_FROM_STDIN_REGEX` | 108–109 | `dropped-with-rationale` | **`COPY ... FROM STDIN` is forbidden with PGLite anywhere — full stop.** PGLite WASM does not implement COPY-IN over the wire (it crashes the WASM session); AIGRID's interception path was an attempt to translate it to a `/dev/blob` JS-API call (`pglite.query(sql, [], { blob })`, a 0.3.x-only surface). Glpnet rejects the interception path categorically: callers must not issue `COPY FROM STDIN` against PGLite, and the bridge does not pretend to support it. FR-008's "AIGRID distinctive learnings" floor lists `globalWorkChain`, `endsAtFlushBoundary`, synthetic `ROLLBACK`, sidecar lifecycle, `sidecar.json` discovery, and the version pin — but **not** COPY interception, so dropping it does not violate the spec floor. (Aligned consequence: with COPY interception dropped, the merged bridge no longer needs `pglite.query()`, so the version pin stays internally consistent — see A12, now `@electric-sql/pglite@0.4.5`.) |
| A5 | `detectCopyInBatch(batch)` frame-walker | 116–174 | `dropped-with-rationale` | Dropped because A4 is dropped — no caller. |
| A6 | `runInterceptedCopy(state)` | 208–238 | `dropped-with-rationale` | Dropped because A4 is dropped — no caller. (This was the function that would have called `pglite.query()`, which glpnet does not permit.) |
| A7 | `copy_in` state machine (`{ mode: 'normal' \| 'copy_in', ... }`) | 192, 283–308 | `dropped-with-rationale` | Dropped because A4 is dropped — no state to track. |
| A8 | New backend-message builders: `buildParseComplete`, `buildBindComplete`, `buildNoData`, `buildCommandComplete`, `buildErrorResponse`, `buildCopyInResponse` | 77–100 | `dropped-with-rationale` | These were introduced solely to support A4–A7. With the COPY interception path forbidden (A4), the bridge has no caller for them — every other reply path goes through `pglite.execProtocolRaw()` which already produces the correct backend-message bytes. |
| A9 | CLI flags `--data-dir`, `--port`, `--host`, `--daemon`, `--transport` | 384–396 | `present-in-merged` | Replaces glpnet's `--pgdir` / `--port` / `--bind`. `--transport` is accepted but currently ignored (forward-compat for future Unix-domain-socket support). `--daemon` disables the stdin-end-exit so a sidecar can spawn the bridge with `stdin=DEVNULL`. |
| A10 | `[bridge]` log prefix unified | throughout | `present-in-merged` | Single prefix replaces glpnet's `[pglite]` / `[server]` / `[forward error]` mix. |
| A11 | `process.exit(e && e.code === 'EADDRINUSE' ? 5 : 2)` on listen error | 371 | `present-in-merged` | Distinguishes "port in use" (5) from generic listen errors (2). Sidecar callers use this to retry with a fresh port. |
| A12 | `@electric-sql/pglite@0.4.5` version pin (was `0.2.17`) | `package.json` (cited as Copy in `sources.md`) | `present-in-merged` | The bridge code uses only the `PGlite.create()` / `pglite.exec()` / `pglite.execProtocolRaw()` subset, which is stable across `0.2.x`–`0.4.x`. **Raised from `0.2.17` to `0.4.5` (2026-05)**: `0.2.17`'s `execProtocolRaw` mishandled an extended-protocol `ROLLBACK` issued while the session was in the aborted transaction state — malformed fragment, no `ReadyForQuery`, hanging any extended-protocol client; an extended `SAVEPOINT` in that state hard-crashed the WASM session (verified by byte-level probe). `0.4.5` fixes both. The COPY-interception drop (A4–A8) still holds — `pglite.query()` remains unused — so the pin stays internally consistent at `0.4.5`. Two upgrade consequences are load-bearing: (i) PGLite on-disk data dirs are **NOT forward-compatible** (`0.2.x` = PostgreSQL 16, `0.3.0+` = PostgreSQL 17 — existing clusters need `pg_dump`→fresh→restore); (ii) `0.4.x` emits a doubled trailing `ReadyForQuery` on the error path, which the bridge coalesces (`coalesceTrailingReadyForQuery`). Full rationale + verification in `prereq-patterns/pglite/sources.md`, `description.md`, and the fix branch `fix/pglite-aborted-txn-upgrade-0.4.5`. |
| A13 | `sidecar.json` host+port discovery convention | external (Python sidecar) | `present-in-merged` (documented) | The bridge itself does not write `sidecar.json` — the AIGRID Python sidecar does, after a TCP readiness probe. Documented in `prereq-patterns/pglite/sources.md` as the `Copy` action for `ulpani_pglite_sidecar.py`. Glpnet adopters are free to use either the sidecar (write `sidecar.json`) OR the `BRIDGE_READY` stdout token (G7) for discovery. |
| A14 | Windows `DETACHED_PROCESS` + `CREATE_NEW_PROCESS_GROUP` lifecycle | external (Python sidecar lines 184–189) | `present-in-merged` (documented) | Same as A13 — the bridge supports detached spawn via `--daemon`; the Python sidecar handles the OS-level flags. Cited verbatim in `sources.md`. |
| A15 | Stale `postmaster.pid` cleanup | external (Python sidecar lines 154–158) | `present-in-merged` (documented) | PGLite uses `postmaster.pid` as a single-instance lock but writes `-42` instead of a real OS pid. After unclean shutdown the file lingers. The Python sidecar removes it before spawning. Cited in `sources.md`. |
| A16 | TCP readiness probe loop | external (Python sidecar lines 197–213) | `present-in-merged` (documented) | Polls the listening port at 0.25s intervals until `READINESS_TIMEOUT`; only persists `sidecar.json` after a successful connect. Cited in `sources.md`. |
| A17 | `BreenLake` / `~/.aigrid/` / `breenlake/_vendor` references in the AIGRID file header comment | header lines 1–24 | `dropped-with-rationale` | Per FR-011 (no AIGRID cross-references in `prereq-patterns/`). The header comment is rewritten to document the dual lineage in glpnet-local terms. The functional content of the header comment (what COPY interception does and why) is preserved verbatim. |
| A18 | Header comment phrasing "for BreenLake" | header line 1 | `superseded-with-rationale` | Rephrased: "Postgres-wire bridge for PGLite, supporting Python (psycopg/SQLAlchemy/Alembic/DBOS) and .NET (Npgsql/psqlODBC) consumers". Same code surface; broader documented audience. |

## Summary

| Source | Total features | `present-in-merged` | `superseded-with-rationale` | `dropped-with-rationale` |
|---|---:|---:|---:|---:|
| glpnet | 16 | 11 | 4 | 1 |
| AIGRID | 18 | 11 | 1 | 6 |
| **Combined** | **34** | **22** | **5** | **7** |

**Unclassified: 0.** Every distinguishing feature of either pre-merge bridge is accounted for in one of the three classifications. (SC-005 satisfied.)

## Verification hooks

- **Static**: this document. Re-run by re-reading both pre-merge files alongside the merged file and confirming every numbered row's site reference still resolves.
- **Runtime SC-003 (Npgsql / psqlODBC connectivity, 100 sequential cycles)**: deferred to the first glpnet feature that *adopts* the merged bridge. Procedure documented in `prereq-patterns/pglite/sources.md` Flow D1.
- **Runtime SC-004 (psycopg-style concurrent-pipeline invariant)**: deferred. Procedure documented in `prereq-patterns/pglite/sources.md` Flow D2.

The static analysis here plus the deferred runtime checks form the SC-005 / SC-003 / SC-004 verification triplet.
