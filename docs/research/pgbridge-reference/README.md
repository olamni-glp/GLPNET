# pgbridge reference scripts

Reference bridges for the PGLite + Postgres-wire investigation
documented in [../pglite-pg-gateway-odbc-failure-analysis.md](../pglite-pg-gateway-odbc-failure-analysis.md).

These are **reference artifacts** — they are not wired into any `D2Net.*`
project and are not referenced by any build script. They exist solely so the
investigation is reproducible and so a future implementer who wants to revive
PGLite as a workspace storage engine has a known-good starting point.

## Files

| File | What it does |
|------|--------------|
| `bridge-traced.mjs` | pg-gateway-based bridge that logs every Postgres-wire frame in both directions. Used to *diagnose* the implicit-Sync issue. |
| `bridge-batched.mjs` | pg-gateway-based bridge that buffers extended-protocol client frames until a flush-worthy tag arrives, then forwards the entire batch as one `pglite.execProtocolRaw` call. Removes the implicit-Sync issue but still fails Npgsql / psqlODBC because of pg-gateway's own response corruption. |
| `bridge-direct.mjs` | **Working bridge.** Hand-rolled minimal Postgres-wire server that handles startup by hand, skips pg-gateway entirely, and applies the same batching. Both Npgsql and psqlODBC interact with it correctly. |
| `package.json` | Pins the npm versions used during the investigation (`@electric-sql/pglite` 0.2.17, `pg-gateway` 0.3.0-beta.4). |

## Usage

```bash
cd docs/research/pgbridge-reference
npm install
mkdir -p ./pgdir
node bridge-direct.mjs --pgdir ./pgdir --port 54400
```

Wait for `BRIDGE_READY port=54400 pid=...` on stdout, then connect any
standard Postgres client to `127.0.0.1:54400` (database `d2net`, user
`d2net`, password `d2net`). The bridge seeds a small `t (x INT)` table for
smoke-testing.

Close stdin (or send SIGTERM) to shut the bridge down cleanly.
