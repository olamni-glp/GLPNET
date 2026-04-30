# Tasks: D2NET.Init — Workspace and Metadata DB Initializer

**Input**: Design documents from `/specs/002-d2net-init/` — [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/)
**Prerequisites**: plan.md (✓), spec.md (✓), research.md (✓), data-model.md (✓), contracts/ (✓)

**Tests**: Included. The spec defines testable user-story acceptance scenarios and 10 measurable success criteria; xUnit integration tests are written alongside (or before) implementation per User Story.

**Organization**: Tasks are grouped by user story to enable independent implementation, testing, and delivery of each story. Phase 1 (Setup) and Phase 2 (Foundational) are shared infrastructure that must complete before any user story can start.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on prior tasks in this phase)
- **[Story]**: User story this task belongs to (US1, US2, US3) or unmarked for shared work
- File paths are absolute under the repo at `D:\BSTDEV\RESEARCH\glp\glpnet`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Wire `D2Net.Init` and `D2Net.Init.Tests` into the existing `tools/d2net/D2Net.sln`; vendor the Node bridge.

- [ ] **T001** Create `tools/d2net/src/D2Net.Init/D2Net.Init.csproj` (`net8.0` exe, `LangVersion=12`, `Nullable=enable`, `ImplicitUsings=enable`). Reference `System.CommandLine` (pre-release pinned), `System.Data.Odbc`. Configure `<Content Include="pgbridge\**\*" CopyToOutputDirectory="PreserveNewest" />` so the bridge ships next to the executable. Add the project to `tools/d2net/D2Net.sln`.
- [ ] **T002** Create `tools/d2net/tests/D2Net.Init.Tests/D2Net.Init.Tests.csproj` (`net8.0`, xunit, `Microsoft.NET.Test.Sdk`, `xunit.runner.visualstudio`, `Npgsql` for **verification only**, project reference to `D2Net.Init`). Add to `D2Net.sln`.
- [ ] **T003** [P] Vendor the bridge under `tools/d2net/src/D2Net.Init/pgbridge/`: write `package.json` pinning `@electric-sql/pglite` and `pg-gateway`, generate `package-lock.json` via `npm install`, and commit a minimal placeholder `server.mjs` that fails-fast (`BRIDGE_ERROR not implemented`) — the real implementation lands in T013.
- [ ] **T004** [P] Add a top-level `tools/d2net/README.md` section describing how to set up the bridge (`cd src/D2Net.Init/pgbridge && npm ci`) — this is the same content as `quickstart.md` but lives where contributors look first.

**Checkpoint**: `dotnet build tools/d2net/D2Net.sln` succeeds; `dotnet test` runs (zero tests yet).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Cross-cutting types and helpers every user story depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] **T005** [P] Create `tools/d2net/src/D2Net.Init/ExitCodes.cs` — `static class ExitCodes` with `const int` for codes 0-9 from `contracts/cli-contract.md`.
- [ ] **T006** [P] Create `tools/d2net/src/D2Net.Init/WorkspaceLayout.cs` — `record WorkspaceLayout(string RepoRoot, string WorkspaceDir, string SettingsFile, string PgDir, string BridgeScriptPath)` with a static `Resolve(string repoRoot)` factory. Includes the **repo-root validation** required by FR-002: returns success only if CWD has `.git/` OR `.D2NET/` OR a subdirectory matching the supplied source name.
- [ ] **T007** [P] Create `tools/d2net/src/D2Net.Init/OdbcConnectionStringBuilder.cs` — composes a `Driver={…};Server=…;Port=…;Database=…;Uid=…;Pwd=…;` string from the seven ODBC fields (research R6).
- [ ] **T008** Create `tools/d2net/src/D2Net.Init/PgBridgeProcess.cs` — `IDisposable` that `Process.Start`s `node pgbridge/server.mjs --pgdir <abs> --port <int>`, waits up to 30 s for a `BRIDGE_READY port=<n>` line on stdout, exposes `IsReady`/`Port`, and on `Dispose` closes stdin and waits up to 5 s for clean exit (force-kill if not). Throws `BridgeStartFailedException` on `BRIDGE_ERROR …` or timeout (mapped to ExitCode 7) and `PortInUseException` on port-in-use (ExitCode 5).
- [ ] **T009** Create `tools/d2net/src/D2Net.Init/SchemaInitializer.cs` — embeds `contracts/db-schema.sql` as a resource and runs it through an open `OdbcConnection`. Idempotency is NOT required; this only ever runs against a fresh empty DB.
- [ ] **T010** [P] Create `tools/d2net/src/D2Net.Init/OutputFormat.cs` — small helpers for plain-text TSV writes and compact JSON (System.Text.Json with `WriteIndented = false`). Keeps stdout/stderr separation logic in one place (FR-019a/FR-020).

**Checkpoint**: All foundational types compile and have placeholder smoke tests in `D2Net.Init.Tests`.

---

## Phase 3: User Story 1 — Fresh init (Priority: P1) 🎯 MVP

**Goal**: A single `d2net-init …` invocation against a clean repo creates `.D2NET/`, writes `D2NET-Settings.json`, brings up PGLite, applies the schema, and populates `setting`, `excluded_directories`, and `dart_files`. `phase_sequence` and `phase_status` are created empty.

**Independent Test**: `FreshInitTests.HappyPath` — temp repo with synthetic `.dart` files, run `Program.Main` non-interactively, assert the workspace exists, the JSON validates, and an Npgsql client (separate verification bridge) sees the expected rows.

### Tests for User Story 1 ⚠️ (write first, ensure they fail before T015–T021)

- [ ] **T011** [P] [US1] `tools/d2net/tests/D2Net.Init.Tests/Fixtures/TempRepoBuilder.cs` — disposable builder that creates `<tmp>/repo/<source>/{lib/foo/runner.dart,…}` and `<tmp>/repo/<source>/{archive_2024/…,old_stuff/…}`. Used by every integration test.
- [ ] **T012** [P] [US1] `tools/d2net/tests/D2Net.Init.Tests/Fixtures/DbVerifier.cs` — opens an Npgsql connection to a verification bridge (separate port, started/stopped per test) and offers `IReadOnlyList<DartFile> Read(string select)`-style helpers for assertions.
- [ ] **T013** [P] [US1] `FreshInitTests.cs` — covers SC-001 (under 10 s for ~500 files), SC-002 (`D2NET-Settings.json` parses + validates against `contracts/settings-schema.json`), SC-003 (five tables exist), SC-004 (`dart_files` row count matches expected), SC-005 (`excluded_directories` row count matches the approved list), SC-006 (`phase_sequence`/`phase_status` empty).
- [ ] **T014** [P] [US1] `InteractivePromptTests.cs` — drives `InteractivePrompter` with a scripted reader/writer pair: verifies the redisplay/remove/approve loop and that `q` aborts cleanly with ExitCode 9 (FR-008, FR-022).
- [ ] **T015** [P] [US1] `ExclusionHeuristicTests.cs` — feeds `ExclusionDetector` a fixture with 20 directory names (mix of marker hits and non-hits) and asserts ≥ 95 % correct classification (SC-010).

### Implementation for User Story 1

- [ ] **T016** [US1] **Real `pgbridge/server.mjs`** under `tools/d2net/src/D2Net.Init/pgbridge/` per `contracts/pgbridge-contract.md`: `import { PGlite } from '@electric-sql/pglite'; import { fromNodeSocket } from 'pg-gateway/node'; …`. Parse `--pgdir`, `--port`, `--bind`. On bind failure exit 2; on PGLite init failure exit 1. Print `BRIDGE_READY port=<n> pid=<pid>` exactly once after both PGLite and the listener are up. Handle stdin EOF / SIGTERM / SIGINT for clean shutdown. Replaces the placeholder shipped in T003.
- [ ] **T017** [P] [US1] `tools/d2net/src/D2Net.Init/InitOptions.cs` — record + parser. Validates: source/target/extension trio and exclusion-list flags. Throws `ArgumentParseException` on malformed input.
- [ ] **T018** [P] [US1] `tools/d2net/src/D2Net.Init/DartFileScanner.cs` — recursive walk of `<RepoRoot>/<SourceDir>` skipping the approved exclusion paths. Returns `IReadOnlyList<DartFileEntry>` with **forward-slash full paths** (FR-014, R3 in research).
- [ ] **T019** [P] [US1] `tools/d2net/src/D2Net.Init/ExclusionDetector.cs` — well-known tool list (R8) + archive/backup/old marker substring scan (R4). Returns `IReadOnlyList<ProposedExclusion>` with `Kind` set.
- [ ] **T020** [US1] `tools/d2net/src/D2Net.Init/InteractivePrompter.cs` — implements the prompt cycle from `contracts/cli-contract.md` (suggested-list display, remove/redisplay/approve/quit). Honours `--accept-suggested-exclusions` (skip prompt) and `--non-interactive` (error on missing input). Cleanup on `q` happens in T024.
- [ ] **T021** [US1] `tools/d2net/src/D2Net.Init/SettingsWriter.cs` — writes `D2NET-Settings.json` (validated post-write against the JSON Schema in `contracts/settings-schema.json`) and inserts the 10 required `setting` rows over an open OdbcConnection. Asserts JSON ↔ DB agreement on the `odbc_*` keys (FR-009).
- [ ] **T022** [US1] `tools/d2net/src/D2Net.Init/ExclusionsWriter.cs` — inserts one row per approved exclusion into `excluded_directories` with `kind` mapped from `ProposedExclusion.Kind`.
- [ ] **T023** [US1] `tools/d2net/src/D2Net.Init/DartFilesWriter.cs` — bulk-inserts rows into `dart_files`. Uses parameterised ODBC commands; one round-trip per file is acceptable for the MVP (≪ 1 s for 500 files).
- [ ] **T024** [US1] `tools/d2net/src/D2Net.Init/InitRunner.cs` — orchestrator implementing the **temp-staging atomic-rename pattern** from research R7: create `.D2NET.tmp.<guid>/`, spin up `BridgeProcess` against it, run schema init + the three writers, dispose the bridge, then `Directory.Move(tmp, ".D2NET")`. On any throw, `finally` blocks delete the temp folder. Maps exceptions to ExitCodes per the CLI contract.
- [ ] **T025** [US1] `tools/d2net/src/D2Net.Init/RunSummary.cs` — formats and writes the FR-021 summary block to stdout at the end of a successful init.
- [ ] **T026** [US1] `tools/d2net/src/D2Net.Init/Program.cs` — `System.CommandLine` root command wiring fresh-init mode (Phase 4 inspection wiring lands in T031). Resolves `WorkspaceLayout`, runs `InitRunner`, returns the right exit code.

**Checkpoint**: User Story 1 acceptance scenarios 1–4 all pass; SC-001..SC-006 and SC-010 are green.

---

## Phase 4: User Story 2 — Inspection options (Priority: P2)

**Goal**: `--list`, `--Exclusions`, `--current-phase` (each with `--json`) read the workspace database read-only.

**Independent Test**: After US1 has built a workspace, each inspection invocation produces the documented stdout shape (plain or JSON), modifies zero bytes under `.D2NET/`, and exits 0.

### Tests for User Story 2 ⚠️

- [ ] **T027** [P] [US2] `ListInspectorTests.cs` — runs `--list` and `--list --json` against a known-good fixture workspace; asserts plain output is sorted-by-`full_path` TSV and JSON validates against the shape in FR-019a.
- [ ] **T028** [P] [US2] `ExclusionsInspectorTests.cs` — same shape, for `--Exclusions` / `--Exclusions --json`.
- [ ] **T029** [P] [US2] `CurrentPhaseInspectorTests.cs` — manually inserts test rows into `phase_sequence`/`phase_status`, then verifies `--current-phase` returns the lowest-sequence non-`COMPLETED` row, and that an empty/all-COMPLETED state prints `no active phase` (or `{"phase":null}` in JSON mode).

### Implementation for User Story 2

- [ ] **T030** [P] [US2] `tools/d2net/src/D2Net.Init/InspectOptions.cs` — record + parser, mutually exclusive with init flags. `Mode` enum, `Json` bool, `BridgePort` int.
- [ ] **T031** [P] [US2] `tools/d2net/src/D2Net.Init/Inspectors/ListInspector.cs` — `SELECT id, filename, full_path FROM dart_files ORDER BY full_path ASC;` then prints via `OutputFormat`.
- [ ] **T032** [P] [US2] `tools/d2net/src/D2Net.Init/Inspectors/ExclusionsInspector.cs` — `SELECT path FROM excluded_directories ORDER BY path ASC;`.
- [ ] **T033** [P] [US2] `tools/d2net/src/D2Net.Init/Inspectors/CurrentPhaseInspector.cs` — joined query against `phase_status`/`phase_sequence` returning the lowest-sequence non-`COMPLETED` row (FR-019).
- [ ] **T034** [US2] Extend `Program.cs`: register `--list`, `--Exclusions`, `--current-phase`, `--json`. Refuse if no `.D2NET/` (ExitCode 6, FR-020). Each inspector spawns its own short-lived `BridgeProcess`.

**Checkpoint**: US2 acceptance scenarios pass; SC-009 holds (zero bytes modified under `.D2NET/` during inspection).

---

## Phase 5: User Story 3 — Destructive re-init (Priority: P3)

**Goal**: `--FORCE --DELETE-EXISTING` (both flags required together) deletes any pre-existing `.D2NET/` and runs a fresh init in its place. Either flag alone is rejected.

**Independent Test**: After US1 creates a workspace, running with neither flag exits 3; with one flag exits 1; with both flags exits 0 and the previous workspace has been replaced atomically.

### Tests for User Story 3 ⚠️

- [ ] **T035** [P] [US3] `ForceDeleteExistingTests.cs` — covers all three acceptance scenarios in spec §US3 plus the failure-mid-replace recovery contract from research R7.

### Implementation for User Story 3

- [ ] **T036** [US3] Extend `InitOptions` to surface `Force` and `DeleteExisting` and validate the both-or-neither rule.
- [ ] **T037** [US3] Extend `InitRunner` with the **rename-aside-then-replace** path: rename existing `.D2NET/` to `.D2NET.deleting.<guid>/`, run the same fresh-init pipeline, on success delete the renamed folder; on any failure rename it back to `.D2NET/` so the user does not lose their workspace. SC-008 verifies post-state shape parity with a fresh init.
- [ ] **T038** [US3] Extend `Program.cs` to accept the uppercase `--FORCE` / `--DELETE-EXISTING` spellings (and lowercase synonyms) and route through the force-delete path.

**Checkpoint**: US3 acceptance scenarios pass; SC-007 (no-flags = no changes) and SC-008 (with-flags = parity workspace) hold.

---

## Phase 6: Polish & Cross-Cutting

**Purpose**: Edge cases, exit-code coverage, documentation validation. Each task is independent.

- [ ] **T039** [P] `WrongCwdTests.cs` — verifies the FR-002 wrong-CWD case (no `.git/`, no `.D2NET/`, no matching source dir) exits 2 and creates nothing.
- [ ] **T040** [P] `PortInUseTests.cs` — pre-binds 54329, asserts that `d2net-init` exits 5 with the documented message (FR-011b).
- [ ] **T041** [P] `ExitCodeTests.cs` — table-driven assertion that every documented exit code (0-9) is reachable through the CLI surface.
- [ ] **T042** [P] `MissingNodeTests.cs` — temporarily removes `node` from PATH (or points the bridge launcher at a non-existent binary) and asserts ExitCode 7 with the documented "Node.js >= 20 required" message.
- [ ] **T043** Manual `quickstart.md` walkthrough on the real `glp_runtime` tree: time the run, verify counts, follow the inspect commands. Capture any deltas as follow-up issues.
- [ ] **T044** Update `tools/d2net/README.md` with a one-line entry for `D2Net.Init` next to the existing `D2Net.Scaffold` description.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: no deps — start immediately.
- **Phase 2 (Foundational)**: depends on Phase 1 — BLOCKS all user stories.
- **Phase 3 / US1 (P1)**: depends on Phase 2.
- **Phase 4 / US2 (P2)**: depends on Phase 2; US1 not strictly required (US2's tests build their own workspaces) but US1 is the natural source of fixtures.
- **Phase 5 / US3 (P3)**: depends on Phase 2 + US1 (re-init replays the fresh-init pipeline).
- **Phase 6 (Polish)**: depends on its target user story being implemented.

### Within each user story

- Tests are written first (T011–T015 before T016–T026; T027–T029 before T030–T034; T035 before T036–T038).
- Within Phase 3: T017/T018/T019 are independent ([P]). T020 depends on T019 (uses `ProposedExclusion`). T024 (orchestrator) depends on T008 + T009 + T021 + T022 + T023. T026 depends on T024.
- Within Phase 4: T031/T032/T033 are independent ([P]); T034 depends on all three.
- Within Phase 5: T036 → T037 → T038.

### Parallel Opportunities

- All [P]-marked tasks within the same phase can run simultaneously.
- Once Phase 2 completes, Phases 3 and 4 can proceed in parallel (different files, no shared state beyond the foundational types).
- All test files within a single user story are independent.

---

## MVP Definition

The User Story 1 acceptance scenarios passing (Phase 3 complete) constitutes a shippable MVP: a developer can initialise a workspace, query it manually via `psql` while a bridge is running, and proceed to the next D2NET phase. User Stories 2 and 3 are quality-of-life additions that bring the toolkit up to the full spec.
