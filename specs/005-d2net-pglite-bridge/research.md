# Phase 0 Research — D2NET.Init Storage Swap to PGLite WASM

**Feature**: `005-d2net-pglite-bridge` — see [spec.md](spec.md) and [plan.md](plan.md)

The user's request is bounded: re-point `D2NET.Init` away from embedded SQLite and back to PGLite WASM via the verified hand-rolled bridge documented in `docs/research/pgbridge-reference/`. All five clarifications are resolved (psqlODBC support level, Windows-only v1, `--bridge-port` lifecycle, corrupt-data recovery, ODBC connection-string persistence). No `NEEDS CLARIFICATION` markers remain. This document records the technical decisions the spec leaves to plan time.

---

## R1 — Storage engine and bridge architecture

**Decision**: PGLite WASM (`@electric-sql/pglite`) under `.D2NET/pgdb/`, exposed via a per-invocation Node.js subprocess running a verbatim port of `docs/research/pgbridge-reference/bridge-direct.mjs`. The .NET side talks to the bridge over the Postgres wire protocol on `127.0.0.1:<port>` using **Npgsql 8.0.3**.

**Rationale**:
- The RCA at `docs/research/pglite-pg-gateway-odbc-failure-analysis.md` is unambiguous: `bridge-direct.mjs` works with both Npgsql 8.0.3 (extended protocol) and `PostgreSQL ODBC Driver(UNICODE)`. `pg-gateway` does not.
- Npgsql is preferred for the .NET-side client (per RCA recommendation): same wire compatibility as ODBC, no native driver to install, fewer crash surfaces. psqlODBC stays as a verified external-client surface (Q1 hard guarantee).
- The bridge code is ~150 lines of Node.js using only the standard library plus `@electric-sql/pglite`. Vendoring it verbatim avoids dependency drift.

**Alternatives considered (rejected)**:
- **`pg-gateway`-based bridge**: caused `STATUS_STACK_BUFFER_OVERRUN` in psqlODBC and `Message code not yet implemented` in Npgsql. Banned by FR-008.
- **In-process PGLite via `Jering.Javascript.NodeJS`**: runs Node embedded inside the .NET process. Heavier dependency surface, less debuggable, no clear win over the subprocess model.
- **Embedded Postgres binary (`EmbeddedPostgres` NuGet)**: spawns a real `postgres` server. Heavier disk footprint, slower startup, wasteful for a single-developer tool.
- **Stay on SQLite (the shipped 002 implementation)**: rejected by the user's request — the upgrade exists specifically to swap storage engines.

---

## R2 — Bridge bundle distribution

**Decision**: Commit `bridge-direct.mjs`, `package.json`, and `package-lock.json` under `tools/d2net/src/D2Net.Init/pgbridge/`. **Do NOT commit `node_modules/`** — gitignore it. An MSBuild target runs `npm ci` (idempotent, lockfile-driven) before compilation when `node_modules/` is missing or stale; the resulting tree is then bundled into the build output via `<None Include="pgbridge/**" CopyToOutputDirectory="PreserveNewest" />` (with `node_modules/.bin/**` excluded for portability).

**Rationale (reversed from initial estimate)**:
- The initial plan-time estimate (~5 MB) was based on the RCA's wire-protocol description. **The actual installed footprint is ~256 MB** because `@electric-sql/pglite@0.2.17` bundles the full Postgres `contrib/` tree (vector, pg_trgm, fuzzystrmatch, uuid-ossp, etc. — ~126 MB) plus the `dist/fs/` runtime tree (~18 MB) plus the WASM blob (~8 MB). Confirmed by `du -sh node_modules/@electric-sql/pglite/dist/contrib`.
- 256 MB committed to git is unacceptable: bloats history forever, slows clones, breaks shallow-clone CI strategies.
- `npm ci` is the lockfile-driven, deterministic install. Given a committed `package-lock.json`, every install produces byte-identical `node_modules/`. This is reproducible.
- Node.js + npm are required at **build time** (developer machine or CI). The pre-built `d2net-init` artifact ships the populated `node_modules/` via `CopyToOutputDirectory="PreserveNewest"`, so end users with only a pre-built binary do not need npm — they need Node.js (for the bridge subprocess at run time) but NOT npm. FR-015 is satisfied: "the command Just Works after installation" where "installation" = dropping the build output on disk.
- `pg-gateway` is not in `package.json`. The build-time check (`scripts/verify-pgbridge-deps.ps1`) runs after `npm ci` and walks the resulting `node_modules/`, failing the build if `pg-gateway` is anywhere in the transitive tree.

**Alternatives considered (rejected)**:
- **Commit `node_modules/`**: 256 MB is too large for git. Initial plan-time decision; reversed after empirical measurement.
- **Strip `dist/contrib/` and `dist/fs/` from a committed tree**: fragile — bumping `@electric-sql/pglite` could re-add files we strip; PGLite may load contrib lazily on certain SQL we cannot fully audit.
- **Bundle into a single-file `.mjs` with esbuild/ncc**: PGLite ships `postgres.wasm` and dynamically loads `*.tar.gz` files from `dist/contrib/`; a single-file bundle is not technically achievable.
- **Download at first run**: hostile to offline developer + CI workflow.

**Build-time + run-time prerequisites table** (revised):

| Tool       | Build time | Run time |
|------------|-----------|----------|
| .NET 8 SDK | required  | not required (self-contained binary if published with `--self-contained`) |
| Node.js 20+| not required at build (only `npm` is needed during `npm ci`) **AND required at run time** (the bridge subprocess) | required |
| npm        | required at build time | not required |

---

## R3 — Schema DDL translation (SQLite → PostgreSQL/PGLite)

**Decision**: Translate the shipped `db-schema.sql` to PostgreSQL syntax with externally equivalent semantics:

| Shipped (SQLite) | Upgrade (PGLite/PostgreSQL) | Notes |
|------------------|------------------------------|-------|
| `INTEGER PRIMARY KEY AUTOINCREMENT` (on `dart_files.id`) | `BIGSERIAL PRIMARY KEY` | Monotonic, server-assigned, accessible via `RETURNING id` if needed. |
| `TEXT PRIMARY KEY` | `TEXT PRIMARY KEY` | unchanged |
| `TEXT NOT NULL` | `TEXT NOT NULL` | unchanged |
| `CHECK (kind IN ('tool','pattern','manual'))` | `CHECK (kind IN ('tool','pattern','manual'))` | unchanged |
| `TEXT NOT NULL UNIQUE` (on `dart_files.full_path`) | `TEXT NOT NULL UNIQUE` | unchanged |
| `TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ','now'))` (on `phase_status.last_updated`) | `TIMESTAMPTZ NOT NULL DEFAULT now()` | Native Postgres timestamp; reads back ISO-8601 UTC via `to_char(last_updated AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"')` in `CurrentPhaseInspector` to preserve FR-019's wire format. |

**Rationale**:
- Native Postgres types are idiomatic and let downstream tools use Postgres date arithmetic on `last_updated`.
- The wire format on stdout (FR-019: ISO-8601 UTC with `Z` suffix) is preserved by formatting at read time, not at write time.
- `BIGSERIAL` keeps `id` as a 64-bit integer (matching the shipped `INTEGER PRIMARY KEY AUTOINCREMENT`'s practical effect).

**Alternatives considered (rejected)**:
- **`GENERATED ALWAYS AS IDENTITY`**: more modern Postgres syntax, behaves identically. Equivalent; chosen `BIGSERIAL` for terseness.
- **Store `last_updated` as `TEXT`** to mirror the shipped wire format byte-for-byte: rejected — it loses Postgres date arithmetic and forces ISO-8601-aware writers in downstream tools. Format conversion at read time is one line of SQL.

---

## R4 — Per-invocation bridge lifecycle

**Decision**: A new `PgBridgeProcess.cs` (IDisposable, owns one `System.Diagnostics.Process`) replaces the implicit lifecycle of `Microsoft.Data.Sqlite`. Sequence:

1. **Startup**: `Process.Start("node", ["bridge-direct.mjs", "--pgdir", <abs>, "--port", <int>])` with stdin/stdout/stderr piped. Resolve `node` from PATH; fail fast with the FR-007/edge-case error message if missing.
2. **Ready handshake**: read stdout line-by-line until `BRIDGE_READY port=<port> pid=<pid>` arrives or 15 s elapses (FR-005). On `BRIDGE_ERROR <message>`, surface the message verbatim and abort. The stdout reader runs on a background `Task`.
3. **SQL connection**: open `NpgsqlConnection(connectionString)`. The bridge has the data tree open; the connection just forwards to it.
4. **Workspace mutations**: schema apply, INSERTs into `setting`, `excluded_directories`, `dart_files`. All inside one transaction.
5. **Teardown** (FR-006, in `Dispose`): close the SQL connection → close bridge stdin → wait 5 s → SIGTERM (`Process.Kill(entireProcessTree: false)`) → wait 2 s → hard kill. The graceful path is the common case; the kill paths emit a non-fatal warning to stderr but do not change the exit code.

**Rationale**:
- The shipped `bridge-direct.mjs` exits cleanly on stdin EOF (`process.stdin.on('end', () => process.exit(0))`). Closing stdin is the documented signal.
- A staged shutdown matches FR-006 and tolerates the rare case where Node hangs (e.g. PGLite's WASM module is mid-flush).
- `Process.Kill(entireProcessTree: false)` is the correct .NET 8 API for SIGTERM-equivalent on Windows (it sends `WM_CLOSE` to the process).

**Alternatives considered (rejected)**:
- **Daemon bridge** (one bridge process shared across multiple D2NET commands): rejected by spec Out-of-Scope ("Long-running daemon bridge").
- **No staged shutdown — just kill on Dispose**: misses the `bridge-direct.mjs` clean-exit contract; could leave PGLite wasm state mid-flush.
- **Shell out to `npm exec` or `npx`**: introduces a second process, slower startup, no benefit over invoking `node` directly.

---

## R5 — Pre-existing SQLite workspace detection (Q5 from clarifications + FR-014)

**Decision**: At the `.D2NET` existence check, additionally probe `.D2NET/pgdb/workspace.sqlite`. If that file exists OR `.D2NET/D2NET-Settings.json` parses with `connection.engine != "pglite"`, treat it identically to "workspace already exists": refuse, exit non-zero, point the user at `--FORCE --DELETE-EXISTING`. No automatic data migration — re-init rebuilds from the source tree per User Story 3.

**Rationale**:
- Two independent signals (the `.sqlite` file's presence, and the JSON's `engine` field) catch both well-formed SQLite-era workspaces and workspaces where the JSON has been tampered with.
- The shipped FR-003 already gates re-init on `--FORCE --DELETE-EXISTING`; this extension just makes the gate engine-aware.

**Alternatives considered (rejected)**:
- **Automatic migration** (read SQLite tables, write into PGLite): documented as Out of Scope. The source tree rewalk costs <10 s and is the spec's intended recovery path.
- **Auto-detect SQLite-era and silently treat as a fresh-init target without flags**: dangerous — destroys the user's prior workspace without asking.

---

## R6 — Test harness for verification post-init

**Decision**: Replace the `DbVerifier` fixture (currently a `SqliteConnection` wrapper) with a `PgBridgeHarness` fixture that spawns its own bridge subprocess pointing at the test's `pgdb/` data directory on a free port (`PortPicker.NextFreePort()` using `TcpListener(IPAddress.Loopback, 0)` + immediate close). The harness exposes the same query-helper surface (`GetTableNames`, `CountRows`, `GetSetting`, `GetExclusions`, `GetDartFiles`) but backed by Npgsql. Tests use it after the init command has exited.

**Rationale**:
- Init's bridge is gone after init exits (per FR-007); the test's verifier needs its own bridge to read the data tree.
- A free-port picker keeps tests parallel-safe; the production default 54400 is reserved for production runs.
- Reusing the production `PgBridgeProcess.cs` (or a thin subclass with a different shutdown policy if needed) keeps the test harness honest — it exercises the same lifecycle code.

**Alternatives considered (rejected)**:
- **Tests connect during the init's bridge lifetime**: would require restructuring init to expose a "pause for verification" hook. Bad coupling.
- **Tests parse `--list` / `--Exclusions` / `--current-phase` stdout instead of querying the DB directly**: covers some assertions but cannot verify schema-level invariants (column types, constraints, presence of empty tables).

---

## R7 — Atomic-rename pattern preservation

**Decision**: Keep the shipped temp-staging + atomic-rename pattern (build into `.D2NET.tmp.<guid>`, rename old `.D2NET` aside, rename temp into place, delete renamed-aside on success). On failure, restore the renamed-aside copy. The change: the temp staging now spawns a bridge against `.D2NET.tmp.<guid>/pgdb/` during the build phase, tears it down before the rename, then **does not** spawn a second bridge after the rename — the caller exits.

**Rationale**:
- Preserves FR-022 of the shipped 002 spec (no partial workspace on failure).
- The PGLite data tree is fully self-contained inside `pgdb/` — there are no absolute-path references that would break under rename, as long as the bridge passes a relative-or-absolute `--pgdir` and PGLite stores no host paths in its data files. (Verified empirically by `bridge-direct.mjs`'s smoke test which uses an arbitrary `--pgdir`.)
- The persisted `connection.data_dir` field uses the **post-rename** absolute path (mirroring the shipped 002's "post-move db_file" trick).

**Alternatives considered (rejected)**:
- **In-place build**: rejected by FR-022 (atomicity on abort).
- **Build into a tempdir outside the repo and copy**: extra IO with no atomicity advantage.

---

## R8 — Node.js version floor

**Decision**: Minimum supported Node.js: **20.x LTS** (the active LTS at v1 release). The CLI runs `node --version` at bridge-spawn time, parses the major version, and fails fast with the FR-Edge-case error message if it is below 20. Recommended (RCA verification version): **24.14.0**.

**Rationale**:
- Node 20 LTS is widely available on Windows developer machines (the v1 supported host) and is the minimum that ships modern V8 + WASI used by PGLite.
- Pinning to 20 LTS — not the RCA's 24.14.0 — gives users headroom: anything 20+ runs the bridge per RCA empirical results.
- A simple major-version gate (`-ge 20`) is the smallest reliable check and avoids brittle string parsing.

**Alternatives considered (rejected)**:
- **Pin to 24.x exactly** (the RCA version): unnecessarily strict; pins users to a non-LTS line.
- **No version check, hope for the best**: defers the failure to PGLite's own initialisation, where the error message is opaque.
- **Bundle Node.js with the install**: cross-platform single-file Node.js bundles exist but bloat the install by ~50 MB. Not justified for v1.

---

## R9 — Free-port selection on bridge-port collision

**Decision**: When `--bridge-port` is omitted and the spec-fixed default 54400 is already bound (e.g., another D2NET command is running concurrently), the command MUST fail fast per FR-Edge-case. It MUST NOT auto-fall-back to a free ephemeral port. Users supply `--bridge-port <other>` to override.

**Rationale**:
- Auto-falling-back would change the persisted `connection.port` silently — making it impossible for external tools to know which port to connect to without re-reading settings each time.
- The single-user model means port collision is rare in practice; explicit override on collision is the predictable behaviour.

**Alternatives considered (rejected)**:
- **Auto-pick a free port**: breaks the "external tool reads `connection.port` from settings" contract.
- **Try default + N adjacent ports**: same problem, just delayed.

---

## R10 — Connection-string formats persisted to settings

**Decision** (per spec FR-009/FR-010 and Q5 clarification):

```text
connection.connection_string      = "Host=127.0.0.1;Port=<port>;Database=d2net;Username=d2net;Password=d2net;SSL Mode=Disable"
connection.connection_string_odbc = "Driver={PostgreSQL ODBC Driver(UNICODE)};Server=127.0.0.1;Port=<port>;Database=d2net;Uid=d2net;Pwd=d2net;SSLmode=disable;"
```

**Rationale**:
- Npgsql syntax: `SSL Mode=Disable` (case-sensitive in Npgsql 8.x), `Username`/`Password` keys.
- ODBC syntax: `SSLmode=disable` (lowercase per psqlODBC convention), `Uid`/`Pwd` keys, `Driver={PostgreSQL ODBC Driver(UNICODE)}` (the modern installer's exact name, verified in the RCA).
- Both strings end with `disable`-flavoured TLS off; the bridge replies `N` to `SSLRequest` (FR-Edge-case), so attempting TLS would fail.

**Alternatives considered (rejected)**:
- **Persist Postgres URL form** (`postgresql://d2net:d2net@127.0.0.1:<port>/d2net?sslmode=disable`): supported by Npgsql but not by psqlODBC; less universally pasteable.
- **Persist DSN form** (relies on a system-wide ODBC DSN being created): loses portability and forces a UI step.

---

## Implementation history (kept for posterity)

The shipped `D2NET.Init` (v2026.04.30-2) ships on embedded SQLite via `Microsoft.Data.Sqlite` after the original PGLite + `pg-gateway` + ODBC stack failed end-to-end during 002 implementation. The follow-up RCA (PR #3, branch `003-pglite-bridge-rca`, tag `v2026.04.30-3`) ships `bridge-direct.mjs` as a working reference — but does NOT integrate it into D2Net.Init. **This feature is the integration step.** The RCA's pinned versions (`@electric-sql/pglite@0.2.17`, no `pg-gateway`, Npgsql 8.0.3, `PostgreSQL ODBC Driver(UNICODE)`) are inherited by this plan with no modification.
