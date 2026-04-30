# Data Model — D2NET.Init Workspace (PGLite-backed)

**Feature**: `005-d2net-pglite-bridge` — see [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md)

The five workspace tables are unchanged in **shape** from the shipped 002 spec. Column names, primary keys, NOT NULL constraints, the `kind` CHECK constraint, the one-row-per-key contract on `setting`, and the read-time wire format of `phase_status.last_updated` are all preserved. What changes is (a) the underlying engine (PGLite/PostgreSQL ⇨ PG-native types), (b) the `setting` table's connection-block rows (engine, host, port, database, user, password, data_dir, connection_string, **connection_string_odbc**), (c) `D2NET-Settings.json`'s `connection` block.

## Tables

### `setting` — flat key/value workspace configuration

```sql
CREATE TABLE setting (
    key   TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
```

| Key                          | Example value                                                                                                                        | Source FR    |
|------------------------------|--------------------------------------------------------------------------------------------------------------------------------------|--------------|
| `source_dir`                 | `glp_runtime`                                                                                                                        | FR-005, FR-012 (002) |
| `target_extension`           | `_net`                                                                                                                               | FR-005, FR-012 (002) |
| `target_dir`                 | `glp_runtime_net`                                                                                                                    | FR-005, FR-012 (002) |
| `db_engine`                  | `pglite`                                                                                                                             | FR-009, FR-010 |
| `db_host`                    | `127.0.0.1`                                                                                                                          | FR-009, FR-010 |
| `db_port`                    | `54400` (or supplied `--bridge-port` value)                                                                                          | FR-009, FR-010, FR-012 |
| `db_database`                | `d2net`                                                                                                                              | FR-009, FR-010 |
| `db_user`                    | `d2net`                                                                                                                              | FR-009, FR-010 |
| `db_password`                | `d2net`                                                                                                                              | FR-009, FR-010 |
| `db_data_dir`                | `D:\repo\.D2NET\pgdb` (absolute, post-rename)                                                                                        | FR-009, FR-010 |
| `db_connection_string`       | `Host=127.0.0.1;Port=54400;Database=d2net;Username=d2net;Password=d2net;SSL Mode=Disable`                                            | FR-009, FR-010, R10 |
| `db_connection_string_odbc`  | `Driver={PostgreSQL ODBC Driver(UNICODE)};Server=127.0.0.1;Port=54400;Database=d2net;Uid=d2net;Pwd=d2net;SSLmode=disable;`           | FR-009, FR-010, R10 |

**Contracts**:
- `setting.value` for every connection-block key MUST byte-match the corresponding field in `D2NET-Settings.json`'s `connection` block after JSON-string unescaping.
- The `db_file` row from the shipped SQLite-era schema MUST NOT appear; `db_data_dir` replaces it.
- Future D2NET commands MAY add additional rows under new keys without altering the schema.

### `excluded_directories` — approved exclusions

```sql
CREATE TABLE excluded_directories (
    path TEXT PRIMARY KEY,
    kind TEXT NOT NULL CHECK (kind IN ('tool', 'pattern', 'manual'))
);
```

Unchanged from shipped 002 spec (FR-013 of 002). `path` is forward-slash relative path under `<source_dir>` (e.g. `archive_2024`, `legacy_lib/.git`). `kind` records why the exclusion exists (`tool` for well-known tool subdirs, `pattern` for archive-marker matches, `manual` for explicit `--exclude` flags).

### `dart_files` — Dart source inventory

```sql
CREATE TABLE dart_files (
    id        BIGSERIAL PRIMARY KEY,
    filename  TEXT NOT NULL,
    full_path TEXT NOT NULL UNIQUE
);
```

Unchanged from shipped 002 spec (FR-014 of 002) except for the PG-native `BIGSERIAL` in place of `INTEGER PRIMARY KEY AUTOINCREMENT`. Behavior is identical: monotonic auto-generated id, bare filename and full path stored separately, `full_path` always uses forward slashes regardless of host OS.

### `phase_sequence` — phase ordering

```sql
CREATE TABLE phase_sequence (
    phase    TEXT PRIMARY KEY,
    sequence INTEGER NOT NULL
);
```

Unchanged from shipped 002 spec (FR-015 of 002). Created empty by `D2NET.Init`; populated by downstream commands.

### `phase_status` — per-phase status

```sql
CREATE TABLE phase_status (
    phase        TEXT PRIMARY KEY,
    status       TEXT NOT NULL,
    last_updated TIMESTAMPTZ NOT NULL DEFAULT now()
);
```

Shape preserved from shipped 002 spec (FR-016 of 002) but with the **PG-native `TIMESTAMPTZ` type** (vs. SQLite TEXT-encoded ISO-8601). Read-side rendering at `--current-phase` MUST format `last_updated` as `to_char(last_updated AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"')` so the wire output stays ISO-8601 UTC with a trailing `Z` (FR-019 of 002, preserved by FR-013 of 005).

## Settings file shape (`D2NET-Settings.json`)

```json
{
  "schema_version": 1,
  "source_dir": "glp_runtime",
  "target_extension": "_net",
  "target_dir": "glp_runtime_net",
  "excluded_directories": ["archive_2024", "legacy_lib"],
  "connection": {
    "engine": "pglite",
    "host": "127.0.0.1",
    "port": 54400,
    "database": "d2net",
    "user": "d2net",
    "password": "d2net",
    "data_dir": "D:\\repo\\.D2NET\\pgdb",
    "connection_string": "Host=127.0.0.1;Port=54400;Database=d2net;Username=d2net;Password=d2net;SSL Mode=Disable",
    "connection_string_odbc": "Driver={PostgreSQL ODBC Driver(UNICODE)};Server=127.0.0.1;Port=54400;Database=d2net;Uid=d2net;Pwd=d2net;SSLmode=disable;"
  },
  "created_at": "2026-04-30T13:42:11Z"
}
```

**Diff vs shipped 002 settings JSON**:
- `connection.engine`: `"sqlite"` → `"pglite"`
- `connection.db_file`: removed
- `connection.host`, `connection.port`, `connection.database`, `connection.user`, `connection.password`, `connection.data_dir`: added
- `connection.connection_string`: was a SQLite ADO.NET string; now an Npgsql string
- `connection.connection_string_odbc`: NEW
- All other top-level fields (`schema_version`, `source_dir`, `target_extension`, `target_dir`, `excluded_directories`, `created_at`): unchanged
- `excluded_directories` is sorted ascending lexicographically; preserved from 002.

## Bridge handshake state machine

External — not a database table, but enumerated here for completeness. Owned by `PgBridgeProcess.cs`.

```
                   ┌──────────────┐
                   │   Spawned    │  Process.Start("node", ["bridge-direct.mjs", ...])
                   └──────┬───────┘
                          │ stdout reader task started
                          ▼
                   ┌──────────────┐
            ┌──────│  WaitReady   │  reading stdout, ≤15 s timer
            │      └──────┬───────┘
"BRIDGE_READY ..."        │ "BRIDGE_ERROR ..." or timeout
            ▼              ▼
     ┌──────────────┐  ┌──────────────┐
     │    Ready     │  │    Aborted   │  caller aborts with non-zero exit
     └──────┬───────┘  └──────────────┘
            │ caller opens NpgsqlConnection
            ▼
     ┌──────────────┐
     │   Serving    │  one or more SQL transactions
     └──────┬───────┘
            │ caller invokes Dispose()
            ▼
     ┌──────────────┐
     │ ShuttingDown │  close stdin → wait 5s → SIGTERM → wait 2s → kill
     └──────┬───────┘
            ▼
     ┌──────────────┐
     │   Reaped     │  process.WaitForExit() returned
     └──────────────┘
```

**Invariants**:
- The bridge is in `Ready` for at most one .NET command's lifetime.
- The bridge holds the only open file handle on `pgdb/` for its lifetime; any second concurrent bridge attempt against the same data tree fails with PGLite's lock error.
- Transitioning from `Aborted` to anywhere is illegal; the process is already gone.
- `ShuttingDown` is non-fatal even if it ends in hard-kill — the workspace mutation, if it completed before `Dispose`, is still durable on disk.

## File-system layout invariants

```text
<repo-root>/
└── .D2NET/                          (mode 0755)
    ├── D2NET-Settings.json          (mode 0644)
    └── pgdb/                        (mode 0755)
        ├── PG_VERSION
        ├── postgresql.conf          (PGLite-managed)
        ├── pg_hba.conf              (PGLite-managed; the bridge does not consult it)
        ├── base/
        ├── global/
        ├── pg_xact/
        ├── pg_wal/
        └── ...                      (other PGLite/Postgres data directories)
```

The exact list of files under `pgdb/` is determined by PGLite/Postgres at first init; the contract is just "a multi-file Postgres data tree, not a single `.sqlite` file" (SC-003). The implementation MUST NOT depend on the names of files inside `pgdb/`.

## Migration / detection rules (Q5 + FR-014)

The "workspace already exists" check at command start probes:

1. `Directory.Exists(<repo-root>/.D2NET/)` — same as shipped 002.
2. **NEW**: `File.Exists(<repo-root>/.D2NET/pgdb/workspace.sqlite)` — the SQLite-era marker.
3. **NEW**: `File.Exists(<repo-root>/.D2NET/D2NET-Settings.json)` AND parsing yields `connection.engine != "pglite"`.

Any of (1), (2), (3) being true triggers the same "workspace already exists" refusal unless `--FORCE --DELETE-EXISTING` is supplied. No automatic migration is performed; the source tree is rewalked on re-init.

## Concurrency

A single repo's `.D2NET` workspace is single-user by design. Concurrency is enforced at the bridge port: a second D2NET command in the same repo with the same `--bridge-port` (or default 54400) fails with `EADDRINUSE` per FR-Edge-case "Bridge port already in use". The PGLite data tree's own lock mechanism would also reject the second open, but the port collision is the user-facing failure.
