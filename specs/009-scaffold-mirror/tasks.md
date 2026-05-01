---
description: "Task list for D2NET.Scaffold — Source-Tree Mirror with Per-Dart-File Working Directories"
---

# Tasks: D2NET.Scaffold — Source-Tree Mirror with Per-Dart-File Working Directories

**Input**: Design documents from `specs/009-scaffold-mirror/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/scaffold-cli-contract.md

**Tests**: Included. Mirrors features 007/008's pattern.

**Organization**: by user story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Different file, no ordering dependency on tasks in the same phase. Can run in parallel.
- **[Story]**: Maps to a user story (US1, US2, US3) or marks setup/foundational/polish.
- File paths are absolute or repository-relative.

---

## Phase 1: Setup (baseline)

- [x] T001 Establish baseline build. Pre-009 baseline confirmed at this branch's HEAD: full solution build green; `D2Net.Init.Tests` green; `D2Net.Scaffold.Tests` 33/34 (1 known flaky perf test, dropped by T036 refactor).
- [x] T002 Audit existing `tools/d2net/src/D2Net.Scaffold/` files. KEEP: nothing — every file was either obsoleted (`CompanionFileWriter`, `RefreshRunner`, `TrackerWriter`, `RunSummary`, `WorkPlan`, `PreflightChecker`, `Models/Collision`, `Models/CompanionExtensions`, `Models/CompanionStatus`, `Models/PrunedDirectories`, `Models/TrackerRecord`) or rewritten (`ScaffoldRunner`, `Program`, `ScaffoldOptions`). RETAIN: `Models/RelPath` (path utility still useful).
- [x] T003 Audit existing `tools/d2net/tests/D2Net.Scaffold.Tests/` files. ALL OBSOLETED: `CompanionStubTests`, `DartSrcRenameTests`, `ExitCodeTests`, `HelpAndVersionTests`, `PerfBudgetTests`, `PreflightCollisionTests`, `PrunedDirectoriesTests`, `RefreshModeTests`, `RelPathNormalizationTests`, `ScaffoldFreshTests`, `TrackerSchemaTests`, `Fixtures/`. Replaced by 009 tests in T013-T034.

---

## Phase 2: Foundational (blocks all user stories)

- [x] T004 [P] Create `tools/d2net/src/D2Net.Scaffold/ExitCodes.cs` with the 8 new constants per research R6.
- [x] T005 [P] Rewrite `tools/d2net/src/D2Net.Scaffold/ScaffoldOptions.cs` to a `record { string RepoRoot, bool Json, bool ForceDeleteTarget, int? BridgePortOverride }`.
- [x] T006 [P] Create `tools/d2net/src/D2Net.Scaffold/DestructiveTargetGate.cs` implementing FR-012a.
- [x] T007 Create `tools/d2net/src/D2Net.Scaffold/TargetTreePlanner.cs`. Returns `ScaffoldPlan` with addPaths, removePaths, dartFileTargets, nonDartFileTargets, collisions, targetIsManaged, targetExists.
- [x] T008 Create `tools/d2net/src/D2Net.Scaffold/StagingMutator.cs`. Includes `WriteStaging`, `AtomicRenameStagingToLive`, `DeleteTargetTree`.
- [x] T009 Create `tools/d2net/src/D2Net.Scaffold/ScaffoldDbWriter.cs`. Single-transaction DDL + UPDATE/UPSERT + phase row management.
- [x] T010 Rewrite `tools/d2net/src/D2Net.Scaffold/Program.cs`: new ArgParser accepting only `--help`, `--version`, `--json`, `--FORCE --DELETE-TARGET`, and `--bridge-port <n>`.
- [x] T011 Create `tools/d2net/src/D2Net.Scaffold/ScaffoldRunner.cs`. End-to-end orchestrator with `RunForTesting(opts, BridgeFactory)` test seam.
- [x] T012 Create `tools/d2net/src/D2Net.Scaffold/ScaffoldRunSummary.cs` text + JSON formatters per contract.

**Checkpoint**: foundation ready. `d2net-scaffold` reaches the runner end-to-end on a real workspace.

---

## Phase 3: User Story 1 — Mirror source to target with per-dart workdirs (Priority: P1) 🎯 MVP

**Goal**: a fresh scaffold against an initialised workspace produces a target tree that mirrors the non-excluded source, with `__<basename>/` working dirs next to every `.dart` file, with `dart_files` rows updated, and with phase rows untouched.

**Independent Test**: see spec US1 independent test.

### Tests for User Story 1

- [x] T013 [P] [US1] `ScaffoldArgParserTests.cs` — 15 tests covering all flag-parsing cases.
- [x] T014 [P] [US1] `ScaffoldPreflightTests.cs` — workspace-missing (exit 22), JSON envelope, source-dir-missing (exit 23).
- [x] T015 [P] [US1] `ScaffoldHappyPathTests.cs` — fresh-run + JSON-shape cases. Exclusion uses `archive_2024` (auto-detected); `extra/` instead of `bin/` to avoid auto-tool-exclusion of `bin`.

### Implementation for User Story 1

- [x] T016 [US1] Happy path wired Program.Run → ArgParser → ScaffoldRunner → success summary.
- [x] T017 [US1] Non-dart copy in StagingMutator.WriteStaging.
- [x] T018 [US1] Dart copy + `__<basename>/` empty dir creation in StagingMutator.
- [x] T019 [US1] DB writer real impl in ScaffoldDbWriter.ApplyScaffold (single transaction, ALTER + CREATE + DELETE + UPSERT + UPDATE + phase upsert).
- [x] T020 [US1] Atomic rename in StagingMutator.AtomicRenameStagingToLive (3-step on Windows when live target exists).
- [x] T021 [US1] ScaffoldRunSummary text + JSON formatters per contract.

**Checkpoint**: P1 acceptance scenarios pass.

---

## Phase 4: User Story 2 — Idempotent reconciliation (Priority: P2)

**Goal**: re-running scaffold reconciles the target tree with the current exclusion list; idempotent re-run is a true no-op.

**Independent Test**: see spec US2 independent test.

### Tests for User Story 2

- [x] T022 [P] [US2] `ScaffoldIdempotencyTests.cs` — re-run no-op; target byte-identical.
- [x] T023 [P] [US2] `ScaffoldReconciliationTests.cs` — exclusion add removes subtree from target; manual exclusion remove restores subtree (uses `extra/` since `bin` is auto-excluded as 'tool' which would require `--allow-system-exclusions` to remove).

### Implementation for User Story 2

- [x] T024 [US2] TargetTreePlanner add-set / remove-set in `Plan()`.
- [x] T025 [US2] StagingMutator's atomic rename handles deleted subtrees by renaming whole live tree aside before the new staging rename.

**Checkpoint**: P2 acceptance scenarios pass.

---

## Phase 5: User Story 3 — Diagnose misuse & destructive override (Priority: P3)

**Goal**: distinct exit codes for every documented misuse; FR-012a destructive override flow works correctly.

**Independent Test**: see spec US3 independent test.

### Tests for User Story 3

- [x] T026 [P] [US3] `ScaffoldDestructiveOverrideTests.cs` — 4 cases: not-managed-no-override exit 24; --FORCE --DELETE-TARGET + yes proceeds; + no exits 29; flag pair when target absent skips prompt.
- [x] T027 [P] [US3] `ScaffoldCollisionTests.cs` — non-empty `__<basename>/` and real-file collisions (exit 25); empty `__<basename>/` is benign.
- [x] T028 [P] [US3] `ScaffoldAtomicityTests.cs` — DB write fails (DROP dart_files post-init); target absent post-run; staging cleaned up.
- [x] T029 [P] [US3] `ScaffoldPhaseInvarianceTests.cs` — seed analyze/port phase rows; assert byte-identical post-run; scaffold row added/updated.
- [x] T029a [P] [US3] `ScaffoldContentionTests.cs` — BridgeFactory test seam injects lock-contention payloads; exit 28 mapping; NodeMissing maps to 10 (not 28).

### Implementation for User Story 3

- [x] T030 [US3] DestructiveTargetGate implemented per contract prompt text.
- [x] T031 [US3] FR-012 detection: TargetTreePlanner.Plan() computes targetIsManaged from scaffold_tracker rows under targetRootAbs.
- [x] T032 [US3] FR-012a wired in ScaffoldRunner: destructive prompt → on cancel exit 29; on confirm DeleteTargetTree() then proceed with destructiveOverride=true.
- [x] T033 [US3] Collision pre-walk in TargetTreePlanner.DetectCollisions.
- [x] T034 [US3] Static SQL audit `ScaffoldSqlAuditTests.cs` — runner has no SQL keyword + phase table on the same line; DbWriter every phase mention paired with `'scaffold'`.

**Checkpoint**: P3 acceptance scenarios pass.

---

## Phase 6: Polish & cross-cutting

- [x] T035 Removed obsoleted source files: `CompanionFileWriter.cs`, `RefreshRunner.cs`, `TrackerWriter.cs`, `RunSummary.cs` (old), `WorkPlan.cs`, `PreflightChecker.cs`, `DirectoryWalker.cs` (replaced by walker inside `TargetTreePlanner`), `FileCopier.cs` (subsumed by `StagingMutator`), `Models/Collision`, `Models/CompanionExtensions`, `Models/CompanionStatus`, `Models/PrunedDirectories`, `Models/TrackerRecord`. Retained: `Models/RelPath.cs`.
- [x] T036 Removed obsoleted test files: all 11 pre-009 test classes + `Fixtures/`. New fixtures in T013-T034 cover the new surface.
- [x] T037 Full suite green: `D2Net.Init.Tests` 157/157 (no regression vs post-008 baseline) + `D2Net.Scaffold.Tests` 34/34 (new 009 surface; old PerfBudgetTests + 10 obsoleted classes removed).
- [x] T038 Live smoke against `.D2NET/`. The pre-existing `glp_runtime_net/` is the legacy old-tool output (contains `d2net-tracker.json` JSON tracker + verbatim file copies). New scaffold correctly detected it as non-scaffold-managed and exited 24 with the expected stderr + JSON envelope (`{"result":"error","code":24,...,"target_abs":"D:\\BSTDEV\\RESEARCH\\glp\\glpnet\\glp_runtime_net"}`). No workspace mutation. Operator may now run `d2net-scaffold --FORCE --DELETE-TARGET` to migrate to the new format when ready.
- [x] T039 [P] `--help` text validated by `ScaffoldArgParserTests.Help_BlockMentionsDestructiveFlagPair` and `Help_BlockMentionsAtomicityAndStaging`.
- [x] T040 quickstart.md required no changes during implementation.
- [x] T041 Stage and commit by name (single commit on `009-scaffold-mirror`).

---

## Dependencies & execution order

- Setup (T001–T003) before everything else.
- Foundational T004 / T005 / T006 in parallel; T007 / T008 / T009 in parallel after T004/T005; T010 depends on T005; T011 depends on T004–T010; T012 depends on T011.
- US1 tests (T013 / T014 / T015) in parallel; US1 impl T016–T021 mostly sequential against ScaffoldRunner / DbWriter / StagingMutator.
- US2 tests (T022 / T023) in parallel; impl T024–T025 mostly sequential.
- US3 tests (T026 / T027 / T028 / T029) in parallel; impl T030–T034 mostly sequential.
- Polish (T035–T041) after every story; T038 / T039 / T040 in parallel.

## Out of scope (do NOT do as part of this feature)

- Conversion of `.dart` files to `_net` artefacts.
- Schema migrations beyond the additive `dart_files` columns and the new `scaffold_tracker` table.
- Modifications to D2Net.Init's behaviour.
- Updating `db-schema.sql` to include the new columns/table for fresh inits (follow-up cleanup; scaffold's runtime ALTER is sufficient).
- Pattern-based or glob-based path matching.
- Any change to the `/D2NET-init` skill contract.
