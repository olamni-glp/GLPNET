---

description: "Task list for D2NET.Init storage swap to PGLite WASM via bridge-direct.mjs"
---

# Tasks: D2NET.Init — PGLite Bridge Upgrade

**Input**: Design documents from `/specs/005-d2net-pglite-bridge/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Tests are mandatory for this feature. The shipped 002 D2Net.Init.Tests suite must continue to compile and pass — new behaviour requires new tests, and existing tests need migration off SQLite.

**Organization**: Tasks are grouped by user story (US1 / US2 / US3 from spec.md) so each story can be implemented and validated independently. Several tasks have hard cross-story dependencies (the bridge subprocess is foundational to all three) — those land in Phase 2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps task to a spec user story (US1, US2, US3) — `Foundational` for prerequisites, `Polish` for cross-cutting follow-ups.
- All file paths are repo-relative.

## Path Conventions

- **Production source**: `tools/d2net/src/D2Net.Init/`
- **Tests**: `tools/d2net/tests/D2Net.Init.Tests/`
- **Vendored bridge bundle**: `tools/d2net/src/D2Net.Init/pgbridge/`
- **Schema contract** (mirror of authoritative DDL): `specs/005-d2net-pglite-bridge/contracts/db-schema.sql`
- **Build-time scripts**: `scripts/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Vendor the bridge bundle, swap the production csproj's database driver, and wire up the build-time `pg-gateway` ban.

- [ ] **T001** [P] [Foundational] Vendor `bridge-direct.mjs` into `tools/d2net/src/D2Net.Init/pgbridge/bridge-direct.mjs`. Start from the verbatim copy of `docs/research/pgbridge-reference/bridge-direct.mjs`, then **DELETE the smoke-seed block** (the two `pglite.exec("CREATE TABLE IF NOT EXISTS t (x INT);")` and `pglite.exec("DELETE FROM t; INSERT INTO t VALUES (1), (2), (3);")` calls plus the surrounding `console.error('[pglite] ready, seeding test schema')` line). Replace those lines with a single `console.error('[pglite] ready')` so startup logging stays informative. Rationale: the smoke seed mutates the data tree on every bridge spawn, including inspection invocations — that violates the shipped 002 SC-009 ("inspection modifies zero bytes") which 005 FR-013 preserves. Add a header comment naming the source path, the RCA document, and the explicit divergence ("smoke seed removed; see specs/005-d2net-pglite-bridge analysis finding C1").
- [ ] **T002** [P] [Foundational] Create `tools/d2net/src/D2Net.Init/pgbridge/package.json` pinning `@electric-sql/pglite@0.2.17` as the only dependency (no `pg-gateway`, no devDependencies). Add `"private": true` and `"type": "module"`.
- [ ] **T003** [Foundational] Run `npm install` once locally to generate `package-lock.json`; commit only `package.json` + `package-lock.json` (NOT `node_modules/`). Add a `.gitignore` at `tools/d2net/src/D2Net.Init/pgbridge/.gitignore` containing `node_modules/`. **Decision reversal**: the original plan committed `node_modules/`; empirical measurement showed 256 MB install footprint (PGLite 0.2.17 bundles all Postgres contrib extensions), too large for git. See `research.md` R2 (revised). (One-time setup; depends on T002.)
- [ ] **T004** [P] [Foundational] Update `tools/d2net/src/D2Net.Init/D2Net.Init.csproj`:
   - Remove `<PackageReference Include="Microsoft.Data.Sqlite" />`.
   - Add `<PackageReference Include="Npgsql" Version="8.0.3" />`.
   - Add `<None Include="pgbridge\**" Exclude="pgbridge\node_modules\.bin\**;pgbridge\node_modules\.cache\**" CopyToOutputDirectory="PreserveNewest" />` so the populated `node_modules/` (after `npm ci`) ships with the build output.
   - Add an MSBuild target that runs `npm ci` inside `pgbridge/` BEFORE `Build`, gated on a sentinel file (e.g., `pgbridge/node_modules/.npm-ci-stamp`) being older than `pgbridge/package-lock.json`:
     ```xml
     <Target Name="EnsurePgBridgeNodeModules" BeforeTargets="VerifyPgBridgeDeps;Build"
             Inputs="pgbridge\package-lock.json"
             Outputs="pgbridge\node_modules\.npm-ci-stamp">
       <Exec Command="npm ci" WorkingDirectory="pgbridge" ConsoleToMSBuild="true" />
       <Touch Files="pgbridge\node_modules\.npm-ci-stamp" AlwaysCreate="true" />
     </Target>
     ```
   - Bump `<Version>` and `<InformationalVersion>` to `0.2.0`.
- [ ] **T005** [P] [Foundational] Create `scripts/verify-pgbridge-deps.ps1`: walks `tools/d2net/src/D2Net.Init/pgbridge/node_modules`, fails with non-zero exit code if any directory named `pg-gateway` is found, prints the resolved bundle root and the `@electric-sql/pglite` version on success.
- [ ] **T006** [Foundational] In `tools/d2net/src/D2Net.Init/D2Net.Init.csproj`, add an MSBuild `<Target Name="VerifyPgBridgeDeps" BeforeTargets="Build">` that invokes `pwsh scripts/verify-pgbridge-deps.ps1` and propagates a non-zero exit as a build error. (Depends on T004 + T005.)

**Checkpoint**: `dotnet build tools/d2net/D2Net.sln` should now run the verify-pgbridge-deps check before compiling. The build will fail until the production code is migrated off `Microsoft.Data.Sqlite` (subsequent phases).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Build the new lifecycle, connection-string, and schema primitives that every user story depends on. **No US1/US2/US3 task may begin until this phase passes.**

- [ ] **T007** [Foundational] Create `tools/d2net/src/D2Net.Init/BridgeOptions.cs` — record `(string Host, int Port, string Database, string User, string Password, string DataDir)`. Centralises bridge config used by spawn and connection-string composition.
- [ ] **T008** [Foundational] Create `tools/d2net/src/D2Net.Init/PgBridgeProcess.cs` — IDisposable lifecycle wrapper per `contracts/pgbridge-contract.md`. Responsibilities: locate `node` on PATH (fail with `ExitCodes.NodeMissing`); locate the vendored `bridge-direct.mjs` and its `node_modules/` (fail with `ExitCodes.BridgeBundleMissing`); spawn with stdin/stdout/stderr piped; read stdout on a background `Task`; expose `Task<bool> WaitForReadyAsync(TimeSpan timeout)` that completes on `BRIDGE_READY` (true) / `BRIDGE_ERROR` / timeout (false, with the verbatim error message exposed); `Dispose()` runs the staged shutdown (close stdin → wait 5s → SIGTERM → wait 2s → kill); raises a `BridgeReadyResult` event payload carrying the parsed port/pid + last `BRIDGE_ERROR` message if any.
- [ ] **T009** [P] [Foundational] Replace `tools/d2net/src/D2Net.Init/OdbcConnectionStringBuilder.cs` content (file name preserved) with the new `DbConnectionStringBuilder`: `EngineName = "pglite"`; `BuildNpgsql(BridgeOptions o)` returns `Host=<o.Host>;Port=<o.Port>;Database=<o.Database>;Username=<o.User>;Password=<o.Password>;SSL Mode=Disable`; `BuildOdbc(BridgeOptions o)` returns the verbatim psqlODBC string from research R10. Keep both methods static.
- [ ] **T010** [P] [Foundational] Update `tools/d2net/src/D2Net.Init/Schema/db-schema.sql` to the PostgreSQL DDL from `specs/005-d2net-pglite-bridge/contracts/db-schema.sql`. Confirm the SQL contains no SQLite-isms (`AUTOINCREMENT`, `strftime`, `sqlite_master`).
- [ ] **T011** [Foundational] Update `tools/d2net/src/D2Net.Init/SchemaInitializer.cs` — switch parameter type from `SqliteConnection` to `NpgsqlConnection`; transaction wrapping unchanged. Keep `LoadSchemaSql()` and the embedded resource pipeline intact.
- [ ] **T012** [Foundational] Update `tools/d2net/src/D2Net.Init/WorkspaceLayout.cs`: rename `DbFile` → `PgDataDir` (already `PgDir` exists; reuse it but add a clarifying comment); remove `DbFileName = "workspace.sqlite"`; add new method `static bool LooksLikeSqliteEra(string repoRoot)` that returns true if `<repoRoot>/.D2NET/pgdb/workspace.sqlite` exists OR if `<repoRoot>/.D2NET/D2NET-Settings.json` parses with a non-`pglite` engine; update `AsTemp` to drop the `DbFile` field.
- [ ] **T013** [Foundational] Update `tools/d2net/src/D2Net.Init/ExitCodes.cs` to add the new exit codes from `contracts/cli-contract.md`: `BridgeStartFailed = 15`, `NodeMissing = 16`, `BridgePortInUse = 17`, `BridgeBundleMissing = 18`. Preserve all existing constants.
- [ ] **T014** [P] [Foundational] Update `tools/d2net/src/D2Net.Init/Program.cs`: register `Console.CancelKeyPress` and `AppDomain.CurrentDomain.ProcessExit` handlers that tear down a globally-tracked `PgBridgeProcess` if any (per pgbridge-contract obligations 7 & 8); leave the `ArgParser` alone — `--bridge-port` integer parsing is already correct, but the default value MUST be **54400** (FR-012). Update the default `bridgePort = 54329` line accordingly. Update the help text to mention Node.js requirement and the new bridge-port semantics.
- [ ] **T015** [P] [Foundational] Create `tools/d2net/tests/D2Net.Init.Tests/Fixtures/PortPicker.cs` — `static int NextFreePort()` opens a `TcpListener(IPAddress.Loopback, 0)`, calls `Start()`, captures the assigned port, calls `Stop()`, returns the port. Used by tests to avoid port collisions across parallel runs.
- [ ] **T016** [Foundational] Replace `tools/d2net/tests/D2Net.Init.Tests/Fixtures/DbVerifier.cs` with `PgBridgeHarness.cs`: takes a `pgdb` data-directory path; spawns its own `PgBridgeProcess` on a free port (T015); exposes the same query-helper API as the old DbVerifier (`GetTableNames`, `CountRows`, `GetSetting`, `GetExclusions`, `GetDartFiles`) but backed by `NpgsqlConnection`; SQL: `GetTableNames` queries `pg_tables` (`SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename`); `Dispose` shuts the bridge cleanly.

**Checkpoint**: `dotnet build` should now compile (since the test fixture and production code are aligned on Npgsql) — though many tests will still need migration in subsequent phases. The new bridge primitives are exercisable.

---

## Phase 3: User Story 1 — Fresh PGLite-backed init (Priority: P1) 🎯 MVP

**Goal**: A clean repo + `d2net-init [args]` produces `.D2NET/pgdb/` as a PGLite data tree, populates the five tables via Npgsql over the bridge, persists Postgres-flavoured connection details, and tears down the bridge before exit.

**Independent Test**: After running the upgraded init non-interactively against a TempRepoBuilder with N seeded `.dart` files, `.D2NET/pgdb/workspace.sqlite` does NOT exist; `D2NET-Settings.json` has `connection.engine = "pglite"`; PgBridgeHarness can connect and verify each of the five tables; the spawned `node` subprocess is no longer running after the command exits.

### Tests for User Story 1

> **Write these tests FIRST. They MUST fail before implementation; pass after.**

- [ ] **T017** [P] [US1] Migrate `tools/d2net/tests/D2Net.Init.Tests/FreshInitTests.cs` to PGLite assertions: replace `SqliteConnection` / `DbVerifier` usage with `PgBridgeHarness`; `connection.engine` = `"pglite"`; `connection.port` = 54400 (or supplied); `connection.host`, `database`, `user`, `password`, `data_dir`, `connection_string`, `connection_string_odbc` all present and matched against `connection_string` regex from `contracts/settings-schema.json`; assert that `Path.Combine(workspace, "pgdb", "workspace.sqlite")` does NOT exist (SC-003); assert `Directory.EnumerateFiles(pgdb).Count() > 1` (PGLite data tree).
- [ ] **T018** [P] [US1] Migrate `tools/d2net/tests/D2Net.Init.Tests/InspectorIntegrationTests.cs` similarly — switch the SqliteConnection in `CurrentPhase_ReturnsLowestSequenceNonCompleted` to `PgBridgeHarness`-spawned NpgsqlConnection; assert `last_updated` is rendered as ISO-8601 UTC with trailing `Z` in both plain-text and JSON outputs.
- [ ] **T019** [P] [US1] Migrate `tools/d2net/tests/D2Net.Init.Tests/OdbcConnectionStringBuilderTests.cs` — assertions for new `EngineName = "pglite"`, `BuildNpgsql` output, and `BuildOdbc` output containing `Driver={PostgreSQL ODBC Driver(UNICODE)}` and `SSLmode=disable`.
- [ ] **T020** [P] [US1] Migrate `tools/d2net/tests/D2Net.Init.Tests/WorkspaceLayoutTests.cs`: remove `DbFile` assertions; assert `PgDataDir` resolves to `<repo>/.D2NET/pgdb`; new tests for `LooksLikeSqliteEra` covering (a) workspace dir absent → false, (b) workspace dir present but settings JSON has `engine = "pglite"` → false, (c) workspace dir present + `pgdb/workspace.sqlite` exists → true, (d) workspace dir present + JSON has `engine = "sqlite"` → true.
- [ ] **T021** [US1] Create `tools/d2net/tests/D2Net.Init.Tests/PgBridgeProcessTests.cs` — direct contract tests for `PgBridgeProcess`:
   - **(a) happy-path**: `BRIDGE_READY` arrives, port matches, dispose closes process within ~1 s.
   - **(b) pglite_init_failed**: `BRIDGE_ERROR pglite_init_failed …` on a permission-denied pgdir surfaces verbatim and disposes.
   - **(c) ready timeout**: timeout when the bridge is artificially blocked (use `Fixtures/Bridges/blocked-bridge.mjs`).
   - **(d) port-in-use**: `EADDRINUSE` when the port is already bound (open a `TcpListener` on the chosen port before spawn).
   - **(e) [M1 remediation] blocked-shutdown escalation**: spawn `Fixtures/Bridges/blocked-bridge.mjs` (stdin-ignoring), let it print `BRIDGE_READY`, then call `Dispose()`. Assert that (1) total dispose time falls within `[5s, 9s]` window (5 s wait → SIGTERM → 2 s wait → kill), (2) the process is terminated, (3) a non-fatal warning containing the bridge PID is written to the harness's stderr, (4) the test process's exit code is unchanged from the success case (pre-Dispose).
   - **(f) [SC-009 mtime guarantee]** spawn a bridge against an already-initialised `pgdb/`, capture all `pgdb/` file mtimes pre-bridge, await ready, immediately dispose without running any SQL, capture mtimes post-bridge, assert no mtime changed by more than 1 s (allowing for OS-level granularity but failing on the smoke-seed regression).

### Implementation for User Story 1

- [ ] **T022** [US1] Update `tools/d2net/src/D2Net.Init/SettingsWriter.cs`: refactor `DbConnectionSettings` from `(Engine, DbFile, ConnectionString)` to `(Engine, Host, int Port, Database, User, Password, DataDir, ConnectionString, ConnectionStringOdbc)`. Update `ForFile` → `ForBridge(BridgeOptions)`. JSON serializer types `SettingsJsonConnection` updated with new fields per `contracts/settings-schema.json`. Update `WriteSettingRows` to write all 11 connection rows from data-model.md; switch SqliteConnection → NpgsqlConnection; parameter syntax `$k`/`$v` → `@k`/`@v`.
- [ ] **T023** [P] [US1] Update `tools/d2net/src/D2Net.Init/DartFilesWriter.cs`: switch SqliteConnection → NpgsqlConnection; parameter syntax `$filename`/`$full_path` → `@filename`/`@full_path`; remove any explicit ID assignment (`BIGSERIAL` auto-generates); INSERT preserves order of the scanner output.
- [ ] **T024** [P] [US1] Update `tools/d2net/src/D2Net.Init/ExclusionsWriter.cs`: switch SqliteConnection → NpgsqlConnection; parameter syntax migration; INSERTs into `excluded_directories` unchanged in shape.
- [ ] **T025** [P] [US1] Update `tools/d2net/src/D2Net.Init/Inspectors/ListInspector.cs`, `ExclusionsInspector.cs`: switch `SqliteConnection` → `NpgsqlConnection`. SQL bodies unchanged (`SELECT id, filename, full_path FROM dart_files ORDER BY full_path ASC`, etc.).
- [ ] **T026** [US1] Update `tools/d2net/src/D2Net.Init/Inspectors/CurrentPhaseInspector.cs`: render `last_updated` via `to_char(last_updated AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"')` so the wire output remains ISO-8601 UTC `Z`-suffixed (FR-019 of 002, preserved by FR-013 of 005). Switch SqliteConnection → NpgsqlConnection.
- [ ] **T027** [US1] Update `tools/d2net/src/D2Net.Init/InitRunner.cs` — the workhorse change:
   - Replace the `SqliteConnection` open with: spawn `PgBridgeProcess` against the temp `pgdir`; await `BRIDGE_READY` (15s); on failure, surface verbatim error with recovery hint when `pglite_init_failed` (FR-005); on success, open `NpgsqlConnection` against the bridge port; run schema apply + writes inside a transaction; close connection; dispose bridge.
   - Replace the `SqliteConnection.ClearAllPools()` call with `NpgsqlConnection.ClearAllPools()` (Npgsql provides the same API).
   - Update the `BridgeOptions` passed to `SettingsWriter.WriteSettingsFile` and `WriteSettingRows` to use the **post-rename** absolute `data_dir` (mirroring the shipped 002 trick of post-move db_file path).
   - Catch `InvalidOperationException`/`NpgsqlException` distinctly and map to `ExitCodes.DbOpenFailed` (preserving the shipped behaviour). Bridge-spawn failures map to `BridgeStartFailed` / `NodeMissing` / `BridgePortInUse` / `BridgeBundleMissing` per the contract.
- [ ] **T028** [US1] Update `tools/d2net/src/D2Net.Init/InspectionRunner.cs`:
   - Change the `--bridge-port` parameter resolution: if user supplied via CLI flag, use the override (live invocation only — do NOT rewrite settings, per Q3 / FR-012); otherwise, **read `connection.port` from `D2NET-Settings.json`** and use that. If settings is missing/corrupt, fail with `WorkspaceMissingForInspection` / `DbOpenFailed`.
   - Spawn `PgBridgeProcess` against `<workspace>/pgdb` on the resolved port. On `pglite_init_failed`, emit recovery hint + `DbOpenFailed`.
   - Open `NpgsqlConnection` to the bridge; run inspection; dispose.
- [ ] **T029** [US1] Update `tools/d2net/src/D2Net.Init/RunSummary.cs` — replace `DbFile` field with `PgDataDir` and add `BridgePort`. Print line `"PGLite data tree    <pgdir> (engine=pglite, port=<n>)"`.

**Checkpoint**: `dotnet test --filter Category=US1` passes (or simply: the migrated FreshInitTests, InspectorIntegrationTests, OdbcConnectionStringBuilderTests, WorkspaceLayoutTests, PgBridgeProcessTests all pass). User Story 1 is independently demoable.

---

## Phase 4: User Story 2 — External Postgres clients connect via the bridge (Priority: P2)

**Goal**: While any D2NET command is running, an external Npgsql client AND an external psqlODBC client (Windows v1) can open a session on the persisted port and run the documented `SELECT` operations against the five workspace tables, without `STATUS_STACK_BUFFER_OVERRUN`.

**Independent Test**: With User Story 1 already shipped, run `d2net-init --list` in one process and connect a separate Npgsql / psqlODBC client to the recorded port — both succeed. Killing the D2NET command closes the port (FR-007).

### Tests for User Story 2

- [ ] **T030** [P] [US2] Create `tools/d2net/tests/D2Net.Init.Tests/ExternalClientTests.cs`:
   - **[M4 remediation]** First add `<PackageReference Include="System.Data.Odbc" Version="8.0.1" />` to `D2Net.Init.Tests.csproj` (xunit 2.5.3 + Npgsql 8.0.3 are already present).
   - **Npgsql smoke (always runs)**: while a bridge is alive (use `PgBridgeHarness` directly), open an Npgsql connection using the persisted `connection_string`; run `SELECT 1`, `SELECT version()` (assert string contains `PostgreSQL`), `SELECT count(*) FROM dart_files` against a seeded workspace, `SELECT * FROM phase_status`. Assert `version()` does NOT contain `SQLite`. Wrapped as a plain `[Fact]`.
   - **psqlODBC smoke**: same workspace. **[H1 remediation]** Use plain `[Fact]` with a runtime precondition: at the top of the test, attempt to enumerate ODBC drivers via `System.Data.Odbc.OdbcConnection`'s exception surface (try opening `Driver={PostgreSQL ODBC Driver(UNICODE)};...` with a deliberately bad host on a closed port and inspect the exception type — `OdbcException` means the driver loaded; `ArgumentException`/`DllNotFoundException` means it is not installed). If the driver is not installed, write `Skipping psqlODBC smoke: PostgreSQL ODBC Driver(UNICODE) not installed` to the test output and `return` early — this is documented and not a test failure. If the driver is installed, open the real connection using the persisted `connection_string_odbc`; run `SELECT count(*) FROM dart_files`; assert returns the expected integer; assert the test process is still alive (the historical `STATUS_STACK_BUFFER_OVERRUN` regression would have killed it).
   - **[Q2 cross-platform note]** All tests in this file additionally check `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` at the top. On non-Windows the psqlODBC test always early-returns. The Npgsql test runs on every OS.
- [ ] **T031** [P] [US2] Create `tools/d2net/tests/D2Net.Init.Tests/BridgeStartupTests.cs`:
   - **Port-in-use**: pre-bind a `TcpListener` on a fixed port; invoke `Program.Run` with `--bridge-port <fixed>`; assert `ExitCodes.BridgePortInUse` (or `BridgeStartFailed` if the bridge surfaces a generic `BRIDGE_ERROR listen`); assert no `.D2NET` was created.
   - **Node missing**: temporarily prepend an empty directory to `PATH` for the test process so `node` cannot be resolved; invoke init; assert `ExitCodes.NodeMissing`.
   - **Bundle missing**: rename the test-output `pgbridge/bridge-direct.mjs` aside; invoke init; assert `ExitCodes.BridgeBundleMissing`; restore.
   - **Bridge readiness timeout**: `Program.Run` with an env-var override pointing at the test fixture `Fixtures/Bridges/blocked-bridge.mjs`; assert `BridgeStartFailed` and timeout window respected (~15s).
   - **[M2 remediation] SIGINT teardown**: spawn `Program.Run --list` against an already-seeded workspace in a background `Task`. After the bridge prints `BRIDGE_READY` (detect by polling the chosen port), simulate Ctrl-C by raising `Console.CancelKeyPress`. Assert (1) the foreground task completes within 5 s, (2) the bridge port is no longer bound (TcpListener can re-bind), (3) no orphan `node.exe` remains alive owned by the test process. (Skip this case on platforms where `Console.CancelKeyPress` is not raisable from test code; mark with a documented runtime precondition.)
- [ ] **T031b** [P] [US2] **[H2 remediation]** Create `tools/d2net/tests/D2Net.Init.Tests/InspectionPortLifecycleTests.cs`:
   - **Reads persisted port**: build temp repo; run `--source ... --target ... --bridge-port 55001` (non-default; non-interactive); assert init persisted `connection.port = 55001` to settings; in a separate `Program.Run` call invoke `--list` with NO `--bridge-port` flag; pre-bind a `TcpListener` on `55001` BEFORE the inspection runs; assert the inspection fails with `BridgePortInUse` (proving it tried to bind 55001, i.e. read the value from settings, not the hardcoded 54400 default).
   - **Override does not rewrite**: build temp repo; run init with default port; capture the file mtime + content of `D2NET-Settings.json`; run `--list --bridge-port 55002`; capture settings mtime + content again; assert content is byte-identical (per Q3 contract: inspection's `--bridge-port` does NOT modify settings).

### Implementation for User Story 2

- [ ] **T032** [US2] No production-code changes required for US2 — the bridge spawned by US1's `InitRunner` and `InspectionRunner` already exposes the wire surface. **This task is a documentation / quickstart audit only**: re-read `quickstart.md` Sections 3 and 7, manually verify the Npgsql-style and ODBC-style connection strings persisted by the FreshInitTests workspace are byte-identical to what `quickstart.md` instructs the user to copy/paste.
- [ ] **T033** [US2] [Polish-light] Add `tools/d2net/src/D2Net.Init/Fixtures/Bridges/blocked-bridge.mjs` (test fixture, NOT shipped) — a minimal Node script that swallows stdin and prints nothing on stdout for >20 s. Used by T021 and T031 timeout tests. Mark with a clear "test fixture only" comment.

**Checkpoint**: `dotnet test --filter Category=US2` passes. SC-004 (Npgsql), SC-005 (psqlODBC, Windows-skippable elsewhere), SC-006 (port released), SC-007 (port-in-use), SC-008 (Node missing) all green.

---

## Phase 5: User Story 3 — Detect and refuse a pre-upgrade SQLite workspace (Priority: P3)

**Goal**: A workspace created by the shipped 002 implementation (single `workspace.sqlite` under `pgdb/`, settings JSON with `engine: sqlite`) is detected at init time. The upgraded command refuses to touch it without `--FORCE --DELETE-EXISTING`; with both flags it does a clean wholesale rebuild.

**Independent Test**: Build a temp repo with a hand-crafted `.D2NET/` mimicking the shipped SQLite layout. Run upgraded `d2net-init` without override flags → `WorkspaceAlreadyExists`. Run with `--FORCE --DELETE-EXISTING` → success; the post-init `.D2NET/pgdb/` is a PGLite data tree, no `.sqlite` file remains.

### Tests for User Story 3

- [ ] **T034** [P] [US3] Create `tools/d2net/tests/D2Net.Init.Tests/SqliteEraDetectionTests.cs`:
   - **Detect by file**: build temp repo; manually create `.D2NET/pgdb/workspace.sqlite` (zero-byte placeholder is fine — the detection is by filename); run init without flags → `WorkspaceAlreadyExists` + correct stderr.
   - **Detect by JSON**: build temp repo; create `.D2NET/D2NET-Settings.json` with `connection.engine = "sqlite"`; run init without flags → `WorkspaceAlreadyExists`.
   - **`--FORCE --DELETE-EXISTING` rebuild**: same SQLite-era setup; run with both flags → success; assert `.sqlite` file is gone; PGLite data tree exists; new `connection.engine = "pglite"`.
   - **Fresh tree happy-path passes through unchanged** (no SQLite-era artifacts → no detection → fresh init).

### Implementation for User Story 3

- [ ] **T035** [US3] Update `tools/d2net/src/D2Net.Init/InitRunner.cs` step 2 ("Decide create / force-delete / refuse"): replace `Directory.Exists(layout.WorkspaceDir)` check with `Directory.Exists(layout.WorkspaceDir) || WorkspaceLayout.LooksLikeSqliteEra(opts.RepoRoot)`. Reuse the existing "workspace already exists at .D2NET; use --FORCE --DELETE-EXISTING to recreate it" message; on confirmed rebuild, the existing temp-staging + atomic-rename path handles the deletion regardless of which engine the prior workspace used.
- [ ] **T036** [US3] Update `tools/d2net/src/D2Net.Init/Program.cs` help text: mention that an old SQLite-format workspace is detected and rebuilt by `--FORCE --DELETE-EXISTING` — no automatic data migration.

**Checkpoint**: `dotnet test --filter Category=US3` passes. SC-009 green.

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: Final tightening, doc parity, end-to-end validation against `quickstart.md`.

- [ ] **T036b** [Polish] **[M3 remediation]** Add `tools/d2net/tests/D2Net.Init.Tests/PerformanceTests.cs` — a single `[Fact, Trait("Category","Performance")]` test that builds a TempRepoBuilder with 500 synthetic `.dart` files, runs init non-interactively, asserts elapsed wall-clock time < 15 s on a hot path. Default `dotnet test` skips this category; CI / release scripts opt in via `--filter "Category=Performance"`. Documents SC-001 in code.
- [ ] **T037** [P] [Polish] Update `tools/d2net/README.md` (if present) — note storage engine swap, Node.js requirement, default port 54400. If no README exists, create a brief one that describes how to build and run d2net-init.
- [ ] **T038** [P] [Polish] Update the root `CHANGELOG.md` — add a `## v2026.04.30-N` entry describing the storage swap, the bridge upgrade, the new exit codes (15–18), and the `--bridge-port` repurposing.
- [ ] **T039** [Polish] Run the full `quickstart.md` walkthrough against a real local build: `dotnet build`, fresh init, inspection commands, external Npgsql client (Section 3), `--FORCE --DELETE-EXISTING`, SQLite-era detection (Section 5), corrupt-data recovery (Section 6), bridge-port collision (Section 7). Record any discrepancies and fix them by extending the affected task or filing a follow-up task.
- [ ] **T040** [Polish] Run `pwsh scripts/verify-pgbridge-deps.ps1` standalone. Confirm `pg-gateway` is absent. (SC-010 verification.)
- [ ] **T041** [P] [Polish] If `tools/d2net/src/D2Net.Init/pgbridge/.gitignore` is needed (e.g. to exclude `node_modules/.bin/` symlinks that don't survive cross-platform commits), add it. Also add an entry to the repo `.gitignore` to NOT ignore `tools/d2net/src/D2Net.Init/pgbridge/node_modules/` (the standard `node_modules` rule will otherwise hide it). Confirm the bundle is still tracked after a fresh clone.
- [ ] **T042** [Polish] Tag-readiness check: the merge into main produces a CalVer tag (per `docs/VERSIONING.md`). The next CalVer slot is whatever the local landing-report logic picks — typically `v2026.04.30-4` if today is 2026-04-30. Verify that the post-merge `git log --oneline` and tag plan are consistent before opening the PR.

**Checkpoint**: All tests green. `quickstart.md` end-to-end walks. `pg-gateway` absent. CHANGELOG written. Ready to ship.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: T001, T002, T004, T005 are independent and parallelizable. T003 depends on T002. T006 depends on T004 + T005.
- **Phase 2 (Foundational)**: All of Phase 1 must be complete. Within Phase 2: T009 / T010 / T015 are parallelizable; T012 depends on data-model decisions only; T011 depends on T009; T014 depends on T013; T016 depends on T008 + T015.
- **Phase 3 (US1)**: depends on Phase 2 complete. Within US1: tests T017–T020 parallelizable; T021 depends on T008; production tasks T022–T026 mostly parallelizable; T027 depends on T008+T011+T012+T013+T022; T028 depends on T012+T027 (for shared port-resolution helpers); T029 depends on T012.
- **Phase 4 (US2)**: depends on Phase 3 complete (the bridge that US2 verifies is built by US1). Within US2: T030 and T031 parallelizable; T032 depends on T027+T028; T033 is independent.
- **Phase 5 (US3)**: depends on Phase 2 (specifically T012 for `LooksLikeSqliteEra`) but **not** on Phase 3 or 4. In a parallel-team scenario, US3 can land before or alongside US1 once Foundational is in.
- **Phase 6 (Polish)**: depends on US1 + US2 + US3 all complete.

### Within Each User Story

- Tests come first (TDD discipline — write failing test, then implementation).
- Models / contracts (data-model, settings JSON) before writers.
- Writers before runners.
- Runners before inspection passes.

### Parallel Opportunities

- **Phase 1**: T001 + T002 + T004 + T005 in parallel (different files).
- **Phase 2**: T009 + T010 + T015 in parallel.
- **Phase 3 tests**: T017 + T018 + T019 + T020 in parallel; T021 standalone.
- **Phase 3 implementation**: T023 + T024 + T025 in parallel after T022 lands.
- **Phase 4**: T030 + T031 in parallel.
- **Phase 5**: standalone phase, T034 first then T035.
- **Phase 6**: T037 + T038 + T041 in parallel; T039 + T040 + T042 sequential.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 → Phase 2 → Phase 3.
2. **STOP and validate**: run `dotnet test`, run a fresh init manually against this very repo's `glp_runtime/` source.
3. If green: ship (this is enough for the PGLite-backed workspace to work end-to-end with the .NET internal client).

### Incremental delivery

1. Phase 1 + Phase 2 = foundation.
2. Phase 3 (US1) = MVP, merge & tag.
3. Phase 4 (US2) = external-client guarantee, merge.
4. Phase 5 (US3) = SQLite-era handover, merge.
5. Phase 6 = polish, final tag.

### Parallel team (single dev in this case)

This entire feature lives in one project; team-parallelism is not the constraint. Sequential execution Phase 1 → 6 is the expected path.

---

## Notes

- `[P]` = different files, no dependencies. Tasks without `[P]` either share files with another task in the same phase or have explicit ordering constraints documented above.
- `[Story]` = US1 / US2 / US3 from spec.md. Foundational and Polish are cross-story.
- Tests must fail before implementation (TDD); commit after each task or logical group.
- After each story checkpoint: run the targeted test filter and verify nothing in the OTHER stories regressed.
- Avoid: editing `D2Net.Scaffold` or `D2Net.Scaffold.Tests` — they are out of scope for this feature.
- Bridge bundle node_modules: ~5 MB committed. Periodic re-pin requires running T003 again with the new version, then re-running the SC-005 verification on Windows.
