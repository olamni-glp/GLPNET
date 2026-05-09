# Applicability — pglite

This pattern targets any standard Postgres wire-protocol consumer. The bridge (see [sources.md](./sources.md)) surfaces PGLite as a TCP Postgres endpoint, so any client that speaks Postgres wire — Python via `psycopg`, .NET via `Npgsql` / `psqlODBC`, etc. — should connect; the only adaptation is the engine / pool / driver config that enforces the single-session invariant. Sub-sections below are ordered from highest-level abstraction (DBOS, which composes the others) down to direct `psycopg` and the .NET stack.

`COPY ... FROM STDIN` is unsupported by PGLite under any circumstance — do not issue it from any consumer. Use `INSERT` (multi-row, prepared) or load via JS-side helpers instead.

### DBOS

Pass `db_engine_kwargs=pglite_engine_kwargs(application_name='dbos')` to `DBOSConfig`. This wires `pool_size=1`, `max_overflow=0`, `prepare_threshold=None`, `pool_pre_ping=False` through to DBOS's underlying SQLAlchemy engine.

DBOS additionally requires a one-line monkey-patch to its `migration_one` because that migration calls `CREATE EXTENSION uuid-ossp` and PGLite ships without `uuid-ossp`. Apply the upstream `_apply_pglite_compat_patch()` helper (cited in [sources.md](./sources.md), action `Read`, in the `pglite-bridge-queueing-prompt.md` upstream document) before DBOS runs its first migration. Without this patch, DBOS startup fails on a fresh PGLite data directory.

### SQLAlchemy

Construct `Engine` with `engine_kwargs = pglite_engine_kwargs(application_name='your-app')` from the upstream `ulpani_lms_pglite_compat.py` (cited in [sources.md](./sources.md), action `Copy`). The default `QueuePool(size=1, max_overflow=0)` it returns is the right shape for write-heavy or transactionally-mixed workloads — every checkout that finds the slot busy waits its turn, which is exactly the serialisation PGLite's single shared session needs.

For **Flask-SQLAlchemy** specifically, switch to the `NullPool` + `AUTOCOMMIT` variant (the LMS reference in `patch_entry.py` upstream summarises the shape; the queueing-prompt doc cited in [sources.md](./sources.md) explains why). The LMS workload is read-mostly, and `NullPool` opens a fresh connection per checkout — which is fine here because PGLite's session is shared regardless of how many TCP connections land on it, so per-checkout connections do not multiply PGLite-side load.

### Alembic

Do **NOT** rely on Alembic's default engine config — it will deadlock on a PGLite session. Build the engine yourself, using the shape from `ulpani_lms_apply_revision.py`'s `_build_engine()`:

```python
create_engine(
    url,
    poolclass=NullPool,
    isolation_level='AUTOCOMMIT',
    connect_args={'prepare_threshold': None, 'application_name': 'alembic'},
)
```

`NullPool` + `AUTOCOMMIT` is the right combination for one-shot DDL: each migration step runs in its own connection that closes when the step ends, with no implicit transaction wrapping (Alembic's default DDL-in-a-transaction behaviour interacts badly with PGLite when a step issues a `CREATE INDEX CONCURRENTLY`-style statement that PGLite cannot roll back).

### psycopg

Direct `psycopg` consumers — anyone connecting with `psycopg.connect(...)` rather than going through SQLAlchemy — MUST do three things by hand:

1. Pass `prepare_threshold=None` to `psycopg.connect()` to disable the prepared-statement cache.
2. Serialise their own access to a single connection. Either keep one `Connection` object for the lifetime of the process and never share it across threads/tasks without a lock, or wrap your connection acquisition in a `threading.Lock` / `asyncio.Lock` so only one caller runs a batch at a time.
3. Call `register_pglite_compat_loaders(conn.adapters)` (from the vendored `pglite_compat_loaders.py` cited in [sources.md](./sources.md), action `Copy`) on every new connection — see the next subsection for the rationale.

Without (1), you will see `DuplicatePreparedStatement` after a handful of repeated queries. Without (2), you will see `lost synchronization with server: got message type …` once concurrent code paths overlap. Without (3), `SELECT`s of `timestamptz` (and possibly other date/time columns) crash psycopg natively — no Python exception, the process dies with a Windows access-violation or POSIX SIGSEGV.

### psycopg type-loader patch (substrate fix, non-optional)

**PGLite WASM emits valid Postgres text output that crashes psycopg 3's built-in parsers for some types.** This is not a configuration mistake; it is a parser bug that does not surface against real Postgres servers and so was not caught by the upstream test suite. SQLAlchemy on top of psycopg inherits the crash unchanged.

The mitigation is to replace the offending psycopg loaders with safe ones on every connection. The vendored module `pglite_compat_loaders.py` (cited in [sources.md](./sources.md)) ships:

- A safe text loader for OID `1184` (`timestamptz`, `TIMESTAMP WITH TIME ZONE`).
- A safe text loader for OID `1114` (`timestamp`, no tz) — defensive, same parser code path.
- `register_pglite_compat_loaders(adapters)` for raw psycopg consumers.
- `apply_to_engine(engine)` that wires a SQLAlchemy `connect` event listener so SQLAlchemy / Alembic engines patch every newly-checked-out psycopg connection automatically.

**Wire `apply_to_engine` into every PGLite engine you create.** Both the application engine factory AND the Alembic env.py engine MUST install the patch. The reference shape:

```python
engine = create_engine(url, **pglite_engine_kwargs(application_name='myapp'))
apply_to_engine(engine)  # MUST follow create_engine
```

Confirmed crash signature when omitted (Windows + psycopg 3): `Windows fatal exception: access violation` originating in `psycopg/_cursor_base.py:_select_current_result`, observed during a `SELECT` of any rowset that includes a `timestamptz` column.

The patch is benign against real Postgres (the safe loaders return the same `datetime` shape as psycopg's built-ins), so engines that may run against either PGLite or real PG can install it unconditionally.

### Npgsql

`Npgsql` is the .NET PostgreSQL driver. Confirmed working against the merged hand-rolled wire-protocol bridge during glpnet's investigation. The required adaptations:

- **`Pooling=false` in the connection string.** Npgsql's pool keeps connections open across logical "uses" and would multiplex requests through them; PGLite's single shared session means every multiplexed request still serialises bridge-side, so pooling buys nothing and risks confusing the application's mental model. Setting `Pooling=false` makes each `NpgsqlConnection.Open()` mint a fresh TCP connection; combined with the bridge's global serialisation chain, this is the cleanest correctness/simplicity trade.
- **No prepared-statement-cache equivalent.** Npgsql does not cache prepared statements unless you explicitly call `NpgsqlCommand.Prepare()`. Don't call `Prepare()` against PGLite — there is no client-side cache to disable, but explicit prepare round-trips are uneconomic against a single-session backend and may interact badly with the synthetic `ROLLBACK` issued by the bridge on each new TCP connection.
- **Application-side serialisation.** Same rule as `psycopg`: keep one logical "connection" worth of activity at a time. With `Pooling=false` this is naturally enforced by the application's own `using` blocks, but if you fan out concurrent work, wrap the work in a single `SemaphoreSlim(1, 1)` so only one batch is in flight against the bridge at a time.

A minimal connection string: `Host=127.0.0.1;Port=<port>;Database=postgres;Username=postgres;Password=postgres;Pooling=false;`. The bridge accepts any username / password (it issues `AuthenticationOk` unconditionally) and reports `server_version=16.0`.

### psqlODBC

`psqlODBC` is the official PostgreSQL ODBC driver, used by .NET via `System.Data.Odbc.OdbcConnection` / `System.Data.Odbc.OdbcDataAdapter` and by other Windows tooling that prefers ODBC over native Npgsql. Confirmed working against the merged hand-rolled wire-protocol bridge during glpnet's investigation (one of the two ODBC clients that drove the no-pg-gateway choice in the first place — pg-gateway 0.3.0-beta.4's response-stream corruption made `psqlODBC` immediately unusable, which is why the bridge skips pg-gateway entirely).

Required adaptations:

- **`Pooling=false`** in the ODBC connection string (DSN-less form: `Driver={PostgreSQL Unicode};Server=127.0.0.1;Port=<port>;Database=postgres;Uid=postgres;Pwd=postgres;Pooling=false;`).
- **`UseDeclareFetch=0`**. `psqlODBC`'s default cursor-based fetch interacts poorly with PGLite's single-session semantics under concurrency; disabling it forces the simpler "read all rows on Execute" path.
- **`Protocol=7.4`** is fine — the bridge announces `server_version=16.0` but speaks the same v3 wire protocol Postgres has used since 7.4, and `psqlODBC` negotiates correctly.
- **Same application-side serialisation rule** as Npgsql: do not fan out concurrent ODBC commands across overlapping `OdbcConnection` objects pointed at the same bridge port.

### Other consumers

Untested but expected to work with the same `prepare_threshold`-equivalent + serialise-access discipline: `asyncpg` (set `statement_cache_size=0`), `psycopg2` (set `prepare_threshold=0`), and any ORM that wraps SQLAlchemy (e.g. Tortoise's SQLAlchemy backend, Pony's). For consumers that do not let you disable prepared-statement caching, the bridge currently has no workaround and the consumer is incompatible.

For wire-protocol-direct consumers (e.g. a hand-written `pq` client or a connection from `psql` itself): the bridge speaks v3 wire correctly, so basic `SELECT 1` works. Do not issue `COPY ... FROM STDIN` (unsupported under any circumstance — see [description.md](./description.md)).
