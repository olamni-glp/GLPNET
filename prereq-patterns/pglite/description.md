# pglite

Status: active

## What this produces

A working local-machine Postgres-compatible database that any standard Postgres wire-protocol client can talk to over TCP — Python (`psycopg`, `SQLAlchemy`, `Alembic`, `DBOS`) and .NET (`Npgsql`, `psqlODBC`) clients are all confirmed compatible. The pattern is the *combined* system, not PGLite in isolation:

1. **PGLite** — a Postgres build compiled to WASM, running embedded in a Node.js process as a single in-memory/on-disk session. Pinned to `@electric-sql/pglite@0.4.5` (PostgreSQL 17). The earlier `0.2.17` pin (PostgreSQL 16) was raised because `0.2.17`'s `execProtocolRaw` mishandled an extended-protocol `ROLLBACK` issued while the session was in the aborted transaction state (returned a malformed fragment with no `ReadyForQuery`, hanging any extended-protocol client; an extended `SAVEPOINT` in that state hard-crashed the WASM session). `0.4.5` fixes both. See [sources.md](./sources.md) and `specs/011-prereq-patterns-catalog/pglite-merge-analysis.md` for the full version-pin rationale.
2. **A Node.js TCP bridge** — a hand-rolled minimal Postgres-wire server (no `pg-gateway`) that handles the startup handshake by hand, buffers each client's frames until a flush boundary, and forwards each batch into PGLite's `execProtocolRaw()` through a global serialisation chain. The bridge file `pglite_bridge.mjs` and its `package.json` ship in this directory and are designed to be copied verbatim into a downstream feature's working tree.
3. **A queue-of-one client config** — depending on consumer language: SQLAlchemy / DBOS use `pool_size=1`, `max_overflow=0`, `prepare_threshold=None`, `pool_pre_ping=False`; raw `psycopg` uses `prepare_threshold=None` plus an explicit lock; .NET (`Npgsql`, `psqlODBC`) sets `Pooling=false`. This is what makes the bridge *safe* to use from concurrent code paths.

The deliverable for a downstream feature is: copy this directory's bridge files (`pglite_bridge.mjs`, `package.json`) into your feature working tree, run `npm install`, spawn `node pglite_bridge.mjs --data-dir <path> --port <p>` at process start, and point your engine / driver at `127.0.0.1:<port>` (use the `BRIDGE_READY port=...` stdout token to synchronise on listen-readiness, or wrap the spawn in a TCP-probe sidecar à la the Python sidecar reference cited in [sources.md](./sources.md)). After that, your code addresses the database the same way it would address any Postgres server.

`COPY ... FROM STDIN` is **not supported** under any circumstance — PGLite WASM does not implement COPY-IN over the wire. Use `INSERT` (multi-row, prepared) or out-of-band data loaders instead.

## Why it matters

PGLite is a **single shared session**. There is exactly one Postgres backend behind the bridge, regardless of how many TCP clients connect. Two consequences follow, and both are non-obvious:

- **Concurrent client batches interleave on the wire.** If two clients fire `Parse → Bind → Describe → Execute → Sync` pipelines at the same time, the bridge can write half of one client's response into the other client's socket. `psycopg` then reports `lost synchronization with server: got message type 'p'…` and the connection's state machine corrupts; `Npgsql` reports a similar protocol-out-of-step error and disposes the connection. The bridge defends against this with a *global* serialisation chain across all connections — that is the bridge-side half of the invariant; the matched half lives client-side as a queue-of-one connection and disabled prepared-statement caching.
- **Prepared-statement caches desync.** `psycopg` reports `DuplicatePreparedStatement` after a few repeated statements unless the cache is disabled outright (`prepare_threshold=None`). `Npgsql` and `psqlODBC` behave under the same rule with `Pooling=false` and no client-side caching.
- **Aborted-transaction recovery must round-trip cleanly, and the bridge owns this.** When a statement errors inside a transaction the session enters Postgres's *aborted* state; the client (psycopg / Npgsql / SQLAlchemy `engine.begin()`) then issues `ROLLBACK` over the **extended** query protocol. PGLite's `execProtocolRaw` deliberately *bypasses* PGLite's own transaction/error wrappers (upstream docs: "Only use if you need to bypass these wrappers… `execProtocol` is a safer alternative"), so a wire bridge built on `execProtocolRaw` must guarantee this round-trip itself. Two facets the bridge handles: (1) the synthetic `ROLLBACK` on each new connection's startup handshake clears a prior client's aborted session; (2) PGLite `0.4.x` emits a *doubled* trailing `ReadyForQuery` on the error path (`… E Z Z`) — the bridge coalesces any run of consecutive trailing `ReadyForQuery` frames to the single one real Postgres sends, so `Npgsql`/`psqlODBC` (which historically desync on a doubled `Z`) stay in protocol step. The `0.2.17 → 0.4.5` upgrade was driven by this failure class — see the version-pin note above.

Skip either half (bridge serialisation OR client-side serialisation/caching discipline) and you will eventually see one of the two failure modes above — not on every run, but enough that tests flake and migrations occasionally corrupt mid-run. **This is the same constraint SQLite enforces internally for its single-writer model**, surfaced here because PGLite does not enforce it for you.

The pattern is the consolidation of these halves into one re-usable block. Implementing only the bridge or only the client config is not implementing the pattern.

## How a feature uses this pattern

1. Copy `pglite_bridge.mjs` and `package.json` from this directory into your feature's working tree (e.g. `<your-feature>/pgbridge/`).
2. Run `npm install` in that directory to install `@electric-sql/pglite@0.2.17`.
3. Spawn the bridge at process start: `node pglite_bridge.mjs --data-dir ./pgdir --port 54400` (or use a sidecar-style daemon — see the Python sidecar cited in [sources.md](./sources.md), action `Read`, for the lifecycle / discovery / readiness-probe blueprint).
4. Wait for `BRIDGE_READY port=<port> pid=<pid>` on stdout, OR poll `127.0.0.1:<port>` for TCP readiness. Then build your engine / connection URL: `postgresql+psycopg://postgres:postgres@127.0.0.1:<port>/postgres` for Python, `Host=127.0.0.1;Port=<port>;Database=postgres;Username=postgres;Pooling=false;` for Npgsql/psqlODBC.
5. Apply the consumer-specific client config from [applicability.md](./applicability.md) — there is a section per consumer with the exact knobs.

The `prereq-patterns/` directory is documentation + index + a copyable bridge implementation — it is NOT a runtime library. Adapting the pattern means **copying** the cited files into your feature, not importing them from the catalog.

## Repo-wide deployment (this repo: glpnet) — feature 012 update

Beginning with feature `012-codeconv-runner` (FR-012), the canonical bridge files in this directory ARE ALSO the **live deployment** for repo-wide PGLite use. There is one bridge per repo, listening against the unified data directory at `<repo-root>/.pgdb/`, started on demand via cross-process file lock + auto-spawn (see `specs/012-codeconv-runner/contracts/bridge_lifecycle.md`). All in-repo consumers (the Python `codeconv` runner, the .NET `D2Net.Init` / `D2Net.Scaffold` / `D2Net.PgdbMigrate` tools, plus any future D2NET or codeconv tool) connect to that single bridge.

The "copy `pglite_bridge.mjs` and `package.json` into your feature working tree" guidance from feature 011 (above) still applies — but only for features that genuinely need a **feature-private PGLite deployment** (separate data dir, separate bridge process, isolated from the repo-wide one). For glpnet's repo-wide use, `node prereq-patterns/pglite/pglite_bridge.mjs --data-dir .pgdb --port 0 --daemon` is invoked directly; no copy is made.

Two things follow from this dual role:

- **Edits to `pglite_bridge.mjs` are now load-bearing for the running system.** Treat it like any other in-tree source file under change control — no "this is reference material, edit freely" assumption.
- **The OS-level lock (`proper-lockfile` against `<data-dir>/.bridge.lock`), the sidecar discovery file (`<data-dir>/bridge.json`), the `BRIDGE_READY` token shape, and the rotated stderr log (`<data-dir>/bridge.log` + `.log.{1,2,3}`) are part of the contract** clients in this repo depend on. Any feature copying the bridge into a private working tree inherits these contracts — see `specs/012-codeconv-runner/contracts/bridge_lifecycle.md` and `bridge_cli.md` for the canonical wording.
