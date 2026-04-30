# Implementation Plan: D2NET.Init — Workspace and Metadata DB Initializer

**Branch**: `002-d2net-init` | **Date**: 2026-04-30 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-d2net-init/spec.md`

## Summary

`D2NET.Init` is the bootstrap step of the `D2NET` (Dart-to-.NET) toolkit and a sibling to the existing `D2NET.Scaffold` console app. It collects the source/extension/target directory names and the developer-approved exclusion list (from CLI flags or interactive prompts), creates a hidden `.D2NET` workspace folder at the repository root, writes a `D2NET-Settings.json` mirror of the configuration, stands up a single-user PGLite (WASM-based) Postgres database under `.D2NET/pgdb/`, and populates five tables: `setting` (flat key/value), `excluded_directories`, `dart_files`, `phase_sequence` (empty), and `phase_status` (empty). It additionally supports inspection options `--list`, `--Exclusions`, `--current-phase` (each with a `--json` variant), and a destructive re-init via `--FORCE --DELETE-EXISTING`.

The toolkit is implemented in **C# on .NET 8**, mirroring the conventions established by `D2NET.Scaffold`. The original PGLite + pg-gateway + ODBC stack proved fundamentally fragile during implementation (psqlODBC crashed and Npgsql hit protocol mismatches against pg-gateway's PGLite handshake). The Q6 clarification therefore pivots the storage engine to **embedded single-user SQLite** via **Microsoft.Data.Sqlite**. The `pgdb/` folder name is preserved for backward compatibility; it now holds a single SQLite database file (`workspace.sqlite`). No bridge process, no TCP listener, no ODBC driver dependency. Every D2NET command opens the SQLite file in-process for the duration of the invocation and closes it before exit. The five-table schema is unchanged in shape — only the PostgreSQL-specific types (`BIGSERIAL`, `TIMESTAMPTZ`) are translated to SQLite equivalents (`INTEGER PRIMARY KEY AUTOINCREMENT`, ISO-8601 text in `last_updated`).

## Technical Context

**Language/Version**: C# 12 on .NET 8 (LTS)
**Primary Dependencies**:
- `Microsoft.Data.Sqlite` 8.0.x — embedded SQLite client (single-user, file-backed, in-process)
- `System.Text.Json` — `D2NET-Settings.json` writer/reader
- A hand-written `ArgParser` covers every flag in `contracts/cli-contract.md` so we avoid the `System.CommandLine` pre-release dependency
- No external runtime prerequisites — no Node.js, no ODBC driver, no bridge process

**Storage**: Embedded single-user SQLite database file (`.D2NET/pgdb/workspace.sqlite`); a JSON mirror at `.D2NET/D2NET-Settings.json`.
**Testing**: `xUnit` for unit and integration tests, mirroring the convention of `D2Net.Scaffold.Tests`. Tests open the same SQLite database directly (no separate verification protocol) via the same `Microsoft.Data.Sqlite` client used by production code. Test fixtures build disposable `.D2NET` workspaces in `Path.GetTempPath()`.
**Target Platform**: Cross-platform .NET 8. Primary host is Windows 11 (this repo); Linux/macOS supported as long as Node.js and psqlODBC are installed.
**Project Type**: CLI tool (single console app, sibling to `D2Net.Scaffold` inside the existing `tools/d2net/` toolkit family).
**Performance Goals**: A fresh init against a source tree of up to 500 `.dart` files / 100 non-excluded directories completes in under 10 s on a typical workstation (per SC-001). Inspection options (`--list`, `--Exclusions`, `--current-phase`) complete in under 2 s each (bridge spin-up + single SQL query + bridge teardown).
**Constraints**:
- Strict atomicity on abort (FR-022): on any failure during a fresh init, no `.D2NET` directory is left on disk; on any failure during `--FORCE --DELETE-EXISTING`, the previous workspace is either fully present (deletion not yet started) or fully replaced (deletion done, fresh write succeeded). Implemented via the temp-staging + atomic-rename pattern (R7).
- All connections released and pools cleared (`SqliteConnection.ClearAllPools()`) before the directory move so file handles do not block the rename on Windows.
- Forward-slash paths in `dart_files.full_path` on every OS (FR-014).
- CWD is the repo root; no walk-up to find `.git` (FR-002).
**Scale/Scope**: One `.D2NET` workspace per repo. Rows in `dart_files` scale linearly with non-excluded `.dart` files in the source (low thousands at most for `glp_runtime`). No multi-repo, multi-user, or networked scenarios.

## Constitution Check

The repository's `.specify/memory/constitution.md` contains only the unfilled `[PRINCIPLE_*_NAME]` template placeholders — no project-specific gates have been ratified. There are therefore no constitution gates to evaluate. **Gate status: pass (vacuously)**.

This matches the disposition recorded in `specs/001-d2net-scaffold/plan.md`. When the constitution is populated, this section must be re-evaluated against any populated principles before re-running `/speckit-plan`.

## Project Structure

### Documentation (this feature)

```text
specs/002-d2net-init/
├── plan.md                  # This file (/speckit-plan command output)
├── spec.md                  # Feature specification (already exists)
├── research.md              # Phase 0 output (/speckit-plan command)
├── data-model.md            # Phase 1 output (/speckit-plan command)
├── quickstart.md            # Phase 1 output (/speckit-plan command)
├── contracts/
│   ├── cli-contract.md      # CLI invocation contract for d2net-init
│   ├── db-schema.sql        # Authoritative DDL for the five tables
│   ├── settings-schema.json # JSON Schema for D2NET-Settings.json
│   └── pgbridge-contract.md # Stdout/stderr/exit-code contract of the Node bridge
├── checklists/
│   └── requirements.md      # Spec quality checklist (already exists)
└── tasks.md                 # Phase 2 output (/speckit-tasks command - NOT created here)
```

### Source Code (repository root)

```text
tools/
└── d2net/
    ├── D2Net.sln                              # Existing solution; D2Net.Init + D2Net.Init.Tests added
    ├── src/
    │   ├── D2Net.Scaffold/                    # Existing — untouched by this feature
    │   └── D2Net.Init/                        # NEW
    │       ├── D2Net.Init.csproj              # net8.0, single executable
    │       ├── Program.cs                     # Entry point + System.CommandLine wiring
    │       ├── InitOptions.cs                 # Parsed CLI options record (init mode)
    │       ├── InspectOptions.cs              # Parsed CLI options record (--list/--Exclusions/--current-phase)
    │       ├── InitRunner.cs                  # Orchestrator: collect inputs → walk → bridge → write
    │       ├── InteractivePrompter.cs         # Prompts for missing inputs + exclusion approval (FR-005..FR-008)
    │       ├── ExclusionDetector.cs           # Archive/backup/old heuristic + well-known-tool-dir scan (FR-006, FR-007)
    │       ├── DartFileScanner.cs             # Walks source tree, returns the .dart inventory (FR-014)
    │       ├── WorkspaceLayout.cs             # Resolves repo-root, .D2NET, pgdb, settings paths (FR-002)
    │       ├── PgBridgeProcess.cs             # IDisposable wrapper that spawns/waits/kills the Node bridge
    │       ├── OdbcConnectionStringBuilder.cs # Builds psqlODBC connection string from setting fields
    │       ├── SchemaInitializer.cs           # Runs db-schema.sql on a freshly-created PGLite DB
    │       ├── SettingsWriter.cs              # Writes D2NET-Settings.json + populates `setting` table
    │       ├── DartFilesWriter.cs             # Inserts rows into `dart_files`
    │       ├── ExclusionsWriter.cs            # Inserts rows into `excluded_directories`
    │       ├── Inspectors/
    │       │   ├── ListInspector.cs           # --list (FR-017)
    │       │   ├── ExclusionsInspector.cs     # --Exclusions (FR-018)
    │       │   └── CurrentPhaseInspector.cs   # --current-phase (FR-019)
    │       ├── OutputFormat.cs                # Plain-text vs --json (FR-019a, SC-009)
    │       ├── ExitCodes.cs                   # Centralised exit-code constants
    │       ├── RunSummary.cs                  # Stdout summary at end of fresh init (FR-021)
    │       └── pgbridge/                      # Vendored Node bridge
    │           ├── package.json               # @electric-sql/pglite + pg-gateway
    │           ├── package-lock.json
    │           └── server.mjs                 # The bridge script (see contracts/pgbridge-contract.md)
    └── tests/
        ├── D2Net.Scaffold.Tests/              # Existing — untouched
        └── D2Net.Init.Tests/                  # NEW
            ├── D2Net.Init.Tests.csproj        # net8.0, xunit, references Npgsql for DB verification only
            ├── Fixtures/
            │   ├── TempRepoBuilder.cs         # Builds disposable temp repo trees with synthetic .dart files
            │   └── DbVerifier.cs              # Opens an Npgsql connection to a running bridge and asserts table state
            ├── FreshInitTests.cs              # End-to-end happy path (US1, SC-001..SC-006)
            ├── InteractivePromptTests.cs      # Prompt flow, redisplay, exclusion approval (US1 acceptance #2, #3)
            ├── ExclusionHeuristicTests.cs     # FR-007 marker matching, SC-010
            ├── ForceDeleteExistingTests.cs    # US3, FR-003, SC-007/SC-008
            ├── ListInspectorTests.cs          # FR-017, --json shape, US2 acceptance #1
            ├── ExclusionsInspectorTests.cs    # FR-018, --json shape, US2 acceptance #2
            ├── CurrentPhaseInspectorTests.cs  # FR-019, --json shape, US2 acceptance #3 & #4
            ├── PortInUseTests.cs              # FR-011b
            ├── WrongCwdTests.cs               # FR-002 wrong-CWD edge case
            └── ExitCodeTests.cs               # All non-zero paths
```

`.D2NET/` itself is NOT created or committed by this work — it is the *output* of running `d2net-init`. Tests build their own throwaway workspaces in `Path.GetTempPath()`.

**Structure Decision**: A new sibling project `D2Net.Init` is added to the existing `tools/d2net/` toolkit alongside `D2Net.Scaffold`, sharing the same `D2Net.sln`. The Node bridge is vendored under `D2Net.Init/pgbridge/` and copied to the publish output at build time so a published `d2net-init` binary plus its `pgbridge/` sibling folder is fully self-contained on a machine with `node` and the psqlODBC driver installed. No code is shared with `D2Net.Scaffold` in this iteration; if utility classes overlap later (e.g. CWD resolution), they can be promoted to a `D2Net.Common` library.

## Complexity Tracking

No constitution violations to justify (constitution is unpopulated). One non-obvious-but-deliberate complexity is the Node-hosted bridge for PGLite — its inclusion is forced by the user's explicit choice of "PGLite WASM-based single-user Postgres" over a native-.NET embedded database; the bridge is the cheapest reasonable way to satisfy that constraint while still exposing standard Postgres-wire / ODBC access (see `research.md` R1 for alternatives considered).

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | (n/a) | (n/a) |
