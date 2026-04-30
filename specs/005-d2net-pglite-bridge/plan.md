# Implementation Plan: D2NET.Init — Storage Swap to PGLite WASM via Direct Postgres-Wire Bridge

**Branch**: `005-d2net-pglite-bridge` | **Date**: 2026-04-30 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/005-d2net-pglite-bridge/spec.md`

## Summary

This feature replaces `D2NET.Init`'s shipped storage backend (embedded SQLite via `Microsoft.Data.Sqlite`) with **PGLite WASM** accessed through a verified hand-rolled Postgres-wire bridge (`bridge-direct.mjs` from `docs/research/pgbridge-reference/`) and **Npgsql 8.0.3** on the .NET side. The five-table schema, all CLI flags, the temp-staging + atomic-rename safety pattern, and the prompt/exclusion flow are preserved unchanged from the shipped 002 spec; only the storage engine and the persisted connection contract change.

The Node.js bridge subprocess is spawned per D2NET invocation (init or inspection), listens on `127.0.0.1:<port>` (default `54400`, overridable via `--bridge-port`), and is torn down before the .NET command exits. The vendored bundle (`bridge-direct.mjs` + `package.json` + `node_modules/` containing the pinned `@electric-sql/pglite@0.2.17`) ships inside the `D2Net.Init` artifact so installation requires no developer-side `npm install`. `pg-gateway` is banned from the dependency tree by build-time check.

External Postgres clients can connect to the bridge port using the persisted Npgsql or ODBC connection strings (both formats persisted per Q5 clarification). Hard release-blocking guarantees on **Windows v1**: Npgsql 8.x extended protocol + `PostgreSQL ODBC Driver(UNICODE)` basic SELECT against the five workspace tables. Anything outside that surface is best-effort. macOS / Linux are listed as "expected to work but unverified" per the Q2 clarification.

## Technical Context

**Language/Version**: C# 12 on .NET 8 (LTS), Node.js 20+ LTS (24.14.0 verified by RCA)
**Primary Dependencies**:
- **.NET runtime side**: `Npgsql` 8.0.3 (already in test csproj; new dependency for the production csproj). Replaces `Microsoft.Data.Sqlite` 8.0.10. `System.Text.Json` retained.
- **Bridge side (vendored Node.js bundle)**: `@electric-sql/pglite` 0.2.17 — the only runtime npm dependency, RCA-pinned. `pg-gateway` is **forbidden** (FR-008).
- A hand-written `ArgParser` covers every CLI flag — preserved verbatim from the shipped 002 implementation; no `System.CommandLine` pre-release dependency.

**Storage**: PGLite WASM data tree under `.D2NET/pgdb/` (multi-file Postgres data layout); a JSON mirror at `.D2NET/D2NET-Settings.json`. **No SQLite file** anywhere under the workspace.
**Testing**: `xUnit` for unit and integration tests. The replacement test fixture `PgBridgeHarness` spawns its own `bridge-direct.mjs` subprocess against the test's `pgdb/` directory on a free port (using `TcpListener(IPAddress.Loopback, 0)`) and connects via Npgsql. The production `PgBridgeProcess.cs` is reused by the harness so the test exercises the same lifecycle code as production.
**Target Platform**: Windows 11+ as the **release-blocking** host (Q2 clarification). macOS and Linux are best-effort: the bridge is cross-platform Node.js and Npgsql is cross-platform .NET, but the RCA was Windows-only and the `STATUS_STACK_BUFFER_OVERRUN` regression that SC-005 guards against is Windows-specific.
**Project Type**: CLI tool (single console app, sibling to `D2Net.Scaffold` inside `tools/d2net/`). The vendored Node bundle is a build-output asset, not a separate project.
**Performance Goals**:
- Fresh init against ≤500 `.dart` files / ≤100 non-excluded directories: under **15 s** (SC-001 — adds 5 s budget over shipped 10 s SQLite SC-001 to absorb bridge spawn / `BRIDGE_READY` / clean teardown).
- Inspection options (`--list`, `--Exclusions`, `--current-phase`): under 5 s each (bridge spawn + single SELECT + bridge teardown). No SC-* mandates a hard inspection budget; this is a plan-time target.

**Constraints**:
- Atomicity on abort (R7) — preserved from 002 via the temp-staging + atomic-rename pattern.
- Bridge subprocess never outlives the .NET command (FR-007); a SIGINT/SIGTERM handler in `Program.cs` ensures bridge teardown on Ctrl-C.
- Forward-slash paths in `dart_files.full_path` on every OS (FR-014, inherited from shipped 002).
- CWD is the repo root; no walk-up to find `.git` (FR-002, inherited).
- `pg-gateway` MUST NOT appear in any vendored `node_modules` (FR-008 + SC-010); a build-time PowerShell check (`scripts/verify-pgbridge-deps.ps1`) walks the bundle and fails the build if it appears.

**Scale/Scope**: One `.D2NET` workspace per repo. `dart_files` row count scales linearly with non-excluded `.dart` files (low thousands at most). Single-user; concurrent D2NET invocations are denied at the OS layer by `EADDRINUSE` on the bridge port (FR-Edge-case). No multi-repo, multi-user, or networked scenarios.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

The repository's `.specify/memory/constitution.md` contains only unfilled `[PRINCIPLE_*_NAME]` template placeholders — no project-specific principles ratified. There are therefore no constitution gates to evaluate. **Gate status: pass (vacuously)** — same disposition as the shipped 002 plan.

When the constitution is populated, this section MUST be re-evaluated against any populated principles before re-running `/speckit-plan`.

**Post-Phase-1 re-check**: still vacuous-pass. The Phase 1 design adds a new subprocess (the Node.js bridge) and a new C# class (`PgBridgeProcess`); neither violates any principle because none are ratified.

## Project Structure

### Documentation (this feature)

```text
specs/005-d2net-pglite-bridge/
├── plan.md                       # This file (/speckit-plan command output)
├── spec.md                       # Feature specification (already exists)
├── research.md                   # Phase 0 output
├── data-model.md                 # Phase 1 output
├── quickstart.md                 # Phase 1 output
├── contracts/
│   ├── cli-contract.md           # CLI invocation contract — delta vs 002
│   ├── db-schema.sql             # Authoritative PGLite/PostgreSQL DDL for the five tables
│   ├── settings-schema.json      # JSON Schema for D2NET-Settings.json (engine="pglite", connection block)
│   └── pgbridge-contract.md      # Stdout/stderr/exit-code contract of the Node bridge
├── checklists/
│   └── requirements.md           # Spec quality checklist (already exists)
└── tasks.md                      # Phase 2 output (/speckit-tasks command - NOT created here)
```

### Source Code (repository root)

```text
tools/
└── d2net/
    ├── D2Net.sln                                    # Existing
    ├── src/
    │   ├── D2Net.Scaffold/                          # Existing — UNTOUCHED by this feature
    │   └── D2Net.Init/                              # MODIFIED
    │       ├── D2Net.Init.csproj                    # MODIFIED: Microsoft.Data.Sqlite removed; Npgsql 8.0.3 added; pgbridge/** copy item added
    │       ├── Program.cs                           # MODIFIED: ArgParser default port stays in [1,65535]; signal-handler tear-down added
    │       ├── InitOptions.cs                       # UNCHANGED
    │       ├── InspectOptions.cs                    # UNCHANGED
    │       ├── InitRunner.cs                        # MODIFIED: spawns PgBridgeProcess; switches SqliteConnection→NpgsqlConnection; passes through corrupt-data hint on PGLite open failure
    │       ├── InspectionRunner.cs                  # MODIFIED: spawns PgBridgeProcess at command start; reads persisted connection.port from settings (Q3); passes through corrupt-data hint
    │       ├── InteractivePrompter.cs               # UNCHANGED
    │       ├── ExclusionDetector.cs                 # UNCHANGED
    │       ├── DartFileScanner.cs                   # UNCHANGED
    │       ├── WorkspaceLayout.cs                   # MODIFIED: replaces DbFile/DbFileName with PgDataDir; adds LooksLikeSqliteEra() for FR-014 detection
    │       ├── PgBridgeProcess.cs                   # NEW: IDisposable lifecycle wrapper; spawn / BRIDGE_READY parse / staged shutdown
    │       ├── BridgeOptions.cs                     # NEW: resolved bridge config record (host, port, db, user, pwd, dataDir)
    │       ├── OdbcConnectionStringBuilder.cs       # MODIFIED: renamed type to DbConnectionStringBuilder (already done in 002); engine flips to "pglite"; emits both Npgsql and ODBC connection-string forms
    │       ├── SchemaInitializer.cs                 # MODIFIED: uses NpgsqlConnection; runs the new PostgreSQL DDL
    │       ├── SettingsWriter.cs                    # MODIFIED: emits new connection block (engine, host, port, database, user, password, data_dir, connection_string, connection_string_odbc); writes db_* setting rows accordingly
    │       ├── DartFilesWriter.cs                   # MODIFIED: SqliteCommand → NpgsqlCommand; parameter syntax @k → @k (Npgsql convention)
    │       ├── ExclusionsWriter.cs                  # MODIFIED: same parameter-syntax migration
    │       ├── Inspectors/
    │       │   ├── ListInspector.cs                 # MODIFIED: SqliteCommand → NpgsqlCommand; SQL unchanged
    │       │   ├── ExclusionsInspector.cs           # MODIFIED: same
    │       │   └── CurrentPhaseInspector.cs         # MODIFIED: last_updated rendered via to_char(... AT TIME ZONE 'UTC', ...) to preserve ISO-8601 wire format
    │       ├── OutputFormat.cs                      # UNCHANGED
    │       ├── ExitCodes.cs                         # MODIFIED: adds BridgeStartFailed (15) and NodeMissing (16) exit codes; preserves WrongCwd (1), WorkspaceAlreadyExists (3), etc.
    │       ├── RunSummary.cs                        # MODIFIED: prints PgDataDir + bridge port (instead of DbFile)
    │       ├── Schema/
    │       │   └── db-schema.sql                    # MODIFIED: BIGSERIAL, TIMESTAMPTZ, etc. (PostgreSQL DDL)
    │       └── pgbridge/                            # NEW: vendored Node bundle
    │           ├── bridge-direct.mjs                # Verbatim copy of docs/research/pgbridge-reference/bridge-direct.mjs
    │           ├── package.json                     # @electric-sql/pglite 0.2.17 (only dep)
    │           ├── package-lock.json                # Generated by `npm install`, committed
    │           └── node_modules/                    # Committed; ~5 MB; excluded from .gitignore via explicit !pattern
    └── tests/
        ├── D2Net.Scaffold.Tests/                    # Existing — UNTOUCHED
        └── D2Net.Init.Tests/                        # MODIFIED
            ├── D2Net.Init.Tests.csproj              # UNCHANGED (already has Npgsql 8.0.3)
            ├── Fixtures/
            │   ├── TempRepoBuilder.cs               # UNCHANGED
            │   ├── DbVerifier.cs                    # MODIFIED → PgBridgeHarness: spawns own bridge on a free port; exposes the same query helpers via Npgsql
            │   └── PortPicker.cs                    # NEW: TcpListener(IPAddress.Loopback, 0) free-port selector
            ├── ArgParserTests.cs                    # UNCHANGED
            ├── DartFileScannerTests.cs              # UNCHANGED
            ├── ExclusionHeuristicTests.cs           # UNCHANGED
            ├── FreshInitTests.cs                    # MODIFIED: SQLite-isms → PGLite via PgBridgeHarness; ConnectionEngine assertion flipped to "pglite"; settings.json connection block reshape; sqlite db file absence check (SC-003)
            ├── InspectorIntegrationTests.cs         # MODIFIED: same migration; CurrentPhase last_updated assertion still ISO-8601-Z
            ├── InteractivePromptTests.cs            # UNCHANGED
            ├── OdbcConnectionStringBuilderTests.cs  # MODIFIED: engine="pglite", new fields, both Npgsql and ODBC string assertions
            ├── WorkspaceLayoutTests.cs              # MODIFIED: removes DbFile assertions; adds PgDataDir + LooksLikeSqliteEra coverage
            ├── WorkspaceMissingForInspectionTests.cs # UNCHANGED
            ├── PgBridgeProcessTests.cs              # NEW: contract tests for spawn / BRIDGE_READY / BRIDGE_ERROR / staged shutdown
            ├── BridgeStartupTests.cs                # NEW: port-in-use, missing-Node, missing-bundle edge cases (SC-006, SC-007, SC-008)
            ├── ExternalClientTests.cs               # NEW: Npgsql + psqlODBC connectivity smoke tests against a live bridge (SC-004, SC-005)
            └── CorruptDataRecoveryTests.cs          # NEW: corrupted pgdb/ tree → BRIDGE_ERROR pglite_init_failed → recovery hint emitted (Q4)

scripts/
└── verify-pgbridge-deps.ps1                         # NEW: walks pgbridge/node_modules; fails the build if pg-gateway is anywhere in the tree (SC-010)
```

**Structure Decision**: Single-project tweak inside the existing `tools/d2net/` toolkit family. No new csproj, no new tests project — the work is concentrated in `D2Net.Init` source + `D2Net.Init.Tests` test fixtures, with the new vendored `pgbridge/` bundle living under the existing `D2Net.Init` source tree as a build-time asset. The build-time dependency-verification script lives in the repo's existing `scripts/` directory (created on demand if it does not already exist).

## Complexity Tracking

> No constitution violations to justify; no extra projects introduced. The added Node.js subprocess is the entire point of the feature and is mandated by the spec, not a complexity choice.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| (none)    | (none)     | (none)                               |
