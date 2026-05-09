# pglite

Status: active

## What this produces

A working local-machine Postgres-compatible database that any standard Postgres wire-protocol client can talk to over TCP — Python (`psycopg`, `SQLAlchemy`, `Alembic`, `DBOS`) and .NET (`Npgsql`, `psqlODBC`) clients are all confirmed compatible. The pattern is the *combined* system, not PGLite in isolation:

1. **PGLite** — a Postgres build compiled to WASM, running embedded in a Node.js process as a single in-memory/on-disk session. Pinned to `@electric-sql/pglite@0.2.17`.
2. **A Node.js TCP bridge** — a hand-rolled minimal Postgres-wire server (no `pg-gateway`) that handles the startup handshake by hand, buffers each client's frames until a flush boundary, and forwards each batch into PGLite's `execProtocolRaw()` through a global serialisation chain. The bridge file `pglite_bridge.mjs` and its `package.json` ship in this directory and are designed to be copied verbatim into a downstream feature's working tree.
3. **A queue-of-one client config** — depending on consumer language: SQLAlchemy / DBOS use `pool_size=1`, `max_overflow=0`, `prepare_threshold=None`, `pool_pre_ping=False`; raw `psycopg` uses `prepare_threshold=None` plus an explicit lock; .NET (`Npgsql`, `psqlODBC`) sets `Pooling=false`. This is what makes the bridge *safe* to use from concurrent code paths.

The deliverable for a downstream feature is: copy this directory's bridge files (`pglite_bridge.mjs`, `package.json`) into your feature working tree, run `npm install`, spawn `node pglite_bridge.mjs --data-dir <path> --port <p>` at process start, and point your engine / driver at `127.0.0.1:<port>` (use the `BRIDGE_READY port=...` stdout token to synchronise on listen-readiness, or wrap the spawn in a TCP-probe sidecar à la the Python sidecar reference cited in [sources.md](./sources.md)). After that, your code addresses the database the same way it would address any Postgres server.

`COPY ... FROM STDIN` is **not supported** under any circumstance — PGLite WASM does not implement COPY-IN over the wire. Use `INSERT` (multi-row, prepared) or out-of-band data loaders instead.

## Why it matters

PGLite is a **single shared session**. There is exactly one Postgres backend behind the bridge, regardless of how many TCP clients connect. Two consequences follow, and both are non-obvious:

- **Concurrent client batches interleave on the wire.** If two clients fire `Parse → Bind → Describe → Execute → Sync` pipelines at the same time, the bridge can write half of one client's response into the other client's socket. `psycopg` then reports `lost synchronization with server: got message type 'p'…` and the connection's state machine corrupts; `Npgsql` reports a similar protocol-out-of-step error and disposes the connection. The bridge defends against this with a *global* serialisation chain across all connections — that is the bridge-side half of the invariant; the matched half lives client-side as a queue-of-one connection and disabled prepared-statement caching.
- **Prepared-statement caches desync.** `psycopg` reports `DuplicatePreparedStatement` after a few repeated statements unless the cache is disabled outright (`prepare_threshold=None`). `Npgsql` and `psqlODBC` behave under the same rule with `Pooling=false` and no client-side caching.

Skip either half (bridge serialisation OR client-side serialisation/caching discipline) and you will eventually see one of the two failure modes above — not on every run, but enough that tests flake and migrations occasionally corrupt mid-run. **This is the same constraint SQLite enforces internally for its single-writer model**, surfaced here because PGLite does not enforce it for you.

The pattern is the consolidation of these halves into one re-usable block. Implementing only the bridge or only the client config is not implementing the pattern.

## How a feature uses this pattern

1. Copy `pglite_bridge.mjs` and `package.json` from this directory into your feature's working tree (e.g. `<your-feature>/pgbridge/`).
2. Run `npm install` in that directory to install `@electric-sql/pglite@0.2.17`.
3. Spawn the bridge at process start: `node pglite_bridge.mjs --data-dir ./pgdir --port 54400` (or use a sidecar-style daemon — see the Python sidecar cited in [sources.md](./sources.md), action `Read`, for the lifecycle / discovery / readiness-probe blueprint).
4. Wait for `BRIDGE_READY port=<port> pid=<pid>` on stdout, OR poll `127.0.0.1:<port>` for TCP readiness. Then build your engine / connection URL: `postgresql+psycopg://postgres:postgres@127.0.0.1:<port>/postgres` for Python, `Host=127.0.0.1;Port=<port>;Database=postgres;Username=postgres;Pooling=false;` for Npgsql/psqlODBC.
5. Apply the consumer-specific client config from [applicability.md](./applicability.md) — there is a section per consumer with the exact knobs.

The `prereq-patterns/` directory is documentation + index + a copyable bridge implementation — it is NOT a runtime library. Adapting the pattern means **copying** the cited files into your feature, not importing them from the catalog.
