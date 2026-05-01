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

- [ ] T001 Establish baseline build. Run `dotnet build tools/d2net/D2Net.sln -c Debug` and `dotnet test tools/d2net/D2Net.sln -c Debug`. Capture green pass count (post-008 baseline: 157 D2Net.Init tests; D2Net.Scaffold tests will partially obsolete during this refactor — expect adjustments). If any non-flaky pre-existing test fails, STOP and report.
- [ ] T002 Audit existing `tools/d2net/src/D2Net.Scaffold/` files. Identify which carry forward (DirectoryWalker, FileCopier, PreflightChecker, RunSummary) and which are obsoleted (CompanionFileWriter, RefreshRunner, parts of ScaffoldOptions / Program). Document the audit result inline in this tasks.md or in a comment block in the affected files. No code changes yet.
- [ ] T003 Audit existing `tools/d2net/tests/D2Net.Scaffold.Tests/` files. Identify which test classes are obsoleted by the refactor (refresh-mode tests, companion-file tests). Tests that cover preserved code paths stay; obsoleted tests will be removed in T031.

---

## Phase 2: Foundational (blocks all user stories)

- [ ] T004 [P] Create `tools/d2net/src/D2Net.Scaffold/ExitCodes.cs` with the 8 new constants per research R6: `ScaffoldWorkspaceMissing = 22`, `ScaffoldSourceMissing = 23`, `ScaffoldTargetNotEmptyAndNotManaged = 24`, `ScaffoldWorkdirCollision = 25`, `ScaffoldCopyError = 26`, `ScaffoldDbWriteFailed = 27`, `ScaffoldWorkspaceLocked = 28`, `ScaffoldOperatorCancelledTargetDeletion = 29`. Each with XML doc summary.
- [ ] T005 [P] Rewrite `tools/d2net/src/D2Net.Scaffold/ScaffoldOptions.cs` to a `record { string RepoRoot, bool Json, bool ForceDeleteTarget }`. Remove `SourceRoot`, `TargetRoot`, `Refresh`. Update all consumers in subsequent tasks.
- [ ] T006 [P] Create `tools/d2net/src/D2Net.Scaffold/DestructiveTargetGate.cs` implementing FR-012a: a static method `Confirm(string absTargetPath, TextReader stdin, TextWriter stderr)` that prints the prompt naming the absolute path, reads one line from stdin, and returns true iff the reply is `yes`/`y`/`confirmed`/`proceed` (case-insensitive). Returns false on any other reply, EOF, or empty input.
- [ ] T007 Create `tools/d2net/src/D2Net.Scaffold/TargetTreePlanner.cs`. Pure function: `Plan(SettingsSnapshot, IReadOnlyList<string> exclusions, NpgsqlConnection conn, string sourceRootAbs, string targetRootAbs)` returns a `ScaffoldPlan` record with: `addPaths`, `removePaths`, `dartFileTargets` (path + __workdir name), `nonDartFileTargets`, `collisions`, `targetIsManaged` (bool from `scaffold_tracker` query), `targetExists` (bool). No filesystem mutations. Used by collision detection (FR-013, research R4) and FR-012 classification.
- [ ] T008 Create `tools/d2net/src/D2Net.Scaffold/StagingMutator.cs`. Methods: `WriteStaging(ScaffoldPlan plan, string stagingDir, string sourceRootAbs)` — copies all non-dart and dart files into `<stagingDir>/<rel-path>`, creates `__<basename>/` empty dirs next to dart files, writes the empty sentinel file `<stagingDir>/.d2net-scaffold-tracker`. Throws `IOException` on any IO failure; the caller deletes the staging dir and maps to exit 26.
- [ ] T009 Create `tools/d2net/src/D2Net.Scaffold/ScaffoldDbWriter.cs`. Static method `ApplyScaffold(NpgsqlConnection conn, ScaffoldPlan plan, bool destructiveOverride)` runs the single transaction: ALTER TABLE add columns IF NOT EXISTS; CREATE TABLE IF NOT EXISTS scaffold_tracker; if destructiveOverride: DELETE FROM scaffold_tracker + UPDATE dart_files SET ... = NULL; DELETE removed-set rows; INSERT add-set rows ON CONFLICT DO UPDATE; UPDATE dart_files SET target_parent_dir, target_workdir_name; UPSERT phase_status. Returns counts.
- [ ] T010 Rewrite `tools/d2net/src/D2Net.Scaffold/Program.cs`: new ArgParser accepting only `--help`, `--version`, `--json`, `--FORCE`, `--DELETE-TARGET` (must come as a pair). Reject any positional arguments. Wire the new `ScaffoldOptions` shape into `ScaffoldRunner`.
- [ ] T011 Create `tools/d2net/src/D2Net.Scaffold/ScaffoldRunner.cs`. End-to-end orchestrator (research R-A4 sequence in data-model.md). Test seam: `RunForTesting(opts, BridgeFactory)`. Preflight → settings snapshot → bridge spawn → DB plan-build → FR-012 check (with FR-012a override path) → file walk → collision detection → staging copy → DB transaction → atomic rename → success summary → exit 0. Handles every documented failure mode by mapping to its dedicated exit code per R6.
- [ ] T012 Create `tools/d2net/src/D2Net.Scaffold/ScaffoldRunSummary.cs` (or extend `RunSummary.cs`) — text + JSON output formatters per the contract.

**Checkpoint**: foundation ready. `d2net-scaffold` reaches the runner end-to-end on a real workspace.

---

## Phase 3: User Story 1 — Mirror source to target with per-dart workdirs (Priority: P1) 🎯 MVP

**Goal**: a fresh scaffold against an initialised workspace produces a target tree that mirrors the non-excluded source, with `__<basename>/` working dirs next to every `.dart` file, with `dart_files` rows updated, and with phase rows untouched.

**Independent Test**: see spec US1 independent test.

### Tests for User Story 1

- [ ] T013 [P] [US1] Add `tools/d2net/tests/D2Net.Scaffold.Tests/ScaffoldArgParserTests.cs`. Cases: empty args parse to scaffold-mode; `--json` parses; `--FORCE --DELETE-TARGET` parses; `--FORCE` alone → exit 1; `--DELETE-TARGET` alone → exit 1; positional arg → exit 1; `--help` → exit 0 with usage; `--version` → exit 0.
- [ ] T014 [P] [US1] Add `tools/d2net/tests/D2Net.Scaffold.Tests/ScaffoldPreflightTests.cs`. No-workspace → exit 22; source-dir-missing → exit 23.
- [ ] T015 [P] [US1] Add `tools/d2net/tests/D2Net.Scaffold.Tests/ScaffoldHappyPathTests.cs`. Cases:
  1. Init workspace with 4 dart + 2 non-dart files in lib/ and bin/, plus an excluded archive_2024/ with content. Run scaffold. Assert: target tree exists; non-excluded files present byte-identical; archive_2024/ absent in target; `__<basename>/` empty dirs next to every dart file; `dart_files` updated with target_parent_dir + target_workdir_name; `scaffold_tracker` populated; sentinel file present in target; exit 0. **Then run `d2net-init --list --json` and assert every row in the response has populated `target_parent_dir` and `target_workdir_name` values matching the on-disk state (FR-018 / SC-009).**
  2. Same as 1 with `--json`. Validate JSON shape against contract.

### Implementation for User Story 1

- [ ] T016 [US1] Wire happy path through Program.Run → ScaffoldRunner → success summary; T015 cases pass.
- [ ] T017 [US1] Implement non-dart file copy in StagingMutator (preserves verbatim content; no transformation).
- [ ] T018 [US1] Implement dart file copy + `__<basename>/` empty dir creation in StagingMutator.
- [ ] T019 [US1] Implement DB writer (T009 stub → real) with ALTER + UPDATE + UPSERT statements per data-model.md.
- [ ] T020 [US1] Implement atomic rename of staging → live target. On Windows: `Directory.Move` with retries on transient sharing violations; if live target exists, rename it to `<target>.d2net-old/` first then delete after the new rename succeeds.
- [ ] T021 [US1] Implement success summary text + JSON formatters (T012 stub → real).

**Checkpoint**: P1 acceptance scenarios pass.

---

## Phase 4: User Story 2 — Idempotent reconciliation (Priority: P2)

**Goal**: re-running scaffold reconciles the target tree with the current exclusion list; idempotent re-run is a true no-op.

**Independent Test**: see spec US2 independent test.

### Tests for User Story 2

- [ ] T022 [P] [US2] Add `tools/d2net/tests/D2Net.Scaffold.Tests/ScaffoldIdempotencyTests.cs`. Run scaffold twice with no changes between runs; assert second run reports zero added / zero removed / target tree byte-identical / `dart_files` byte-identical / `scaffold_tracker` byte-identical except `last_scaffold_at` (which may update — implementation choice).
- [ ] T023 [P] [US2] Add `tools/d2net/tests/D2Net.Scaffold.Tests/ScaffoldReconciliationTests.cs`. Cases:
  1. After initial scaffold, `--add-exclude bin`, run scaffold, assert bin/ removed from target + scaffold_tracker rows for bin/* deleted + dart_files columns for bin/* set to NULL.
  2. After 1, `--remove-exclude bin --allow-system-exclusions`, run scaffold, assert bin/ recreated with original files + __workdirs + scaffold_tracker rows + dart_files columns repopulated.

### Implementation for User Story 2

- [ ] T024 [US2] Implement TargetTreePlanner's add-set / remove-set computation (T007 stub → real). Source walk vs scaffold_tracker rows, intersected with current exclusions.
- [ ] T025 [US2] Confirm StagingMutator's clean rename path correctly handles the case where some live-target subtrees are deleted (because they're now excluded).

**Checkpoint**: P2 acceptance scenarios pass.

---

## Phase 5: User Story 3 — Diagnose misuse & destructive override (Priority: P3)

**Goal**: distinct exit codes for every documented misuse; FR-012a destructive override flow works correctly.

**Independent Test**: see spec US3 independent test.

### Tests for User Story 3

- [ ] T026 [P] [US3] Add `tools/d2net/tests/D2Net.Scaffold.Tests/ScaffoldDestructiveOverrideTests.cs`. Cases:
  1. Pre-create a target dir with non-scaffold content. Run `d2net-scaffold` (no override). Assert exit 24, target untouched.
  2. Same setup. Run `d2net-scaffold --FORCE --DELETE-TARGET` with `yes\n` on stdin. Assert prompt is emitted naming the absolute path; target is recreated correctly; scaffold_tracker reset.
  3. Same setup. Run `d2net-scaffold --FORCE --DELETE-TARGET` with `no\n` on stdin. Assert exit 29, target byte-identical to pre-prompt state.
  4. Run `d2net-scaffold --FORCE --DELETE-TARGET` when target does NOT exist. Assert no prompt, scaffold proceeds normally, exit 0.
- [ ] T027 [P] [US3] Add `tools/d2net/tests/D2Net.Scaffold.Tests/ScaffoldCollisionTests.cs`. Construct a source tree where `__<basename>` clashes (e.g., `bin/glp_repl.dart` plus `bin/__glp_repl/` already in source). Run scaffold. Assert exit 25, collision named in stderr, target untouched.
- [ ] T028 [P] [US3] Add `tools/d2net/tests/D2Net.Scaffold.Tests/ScaffoldAtomicityTests.cs`. Inject a fault between staging-write and DB-COMMIT. Assert: target byte-identical to pre-run; staging dir cleaned up; `dart_files` and `scaffold_tracker` byte-identical to pre-run.
- [ ] T029 [P] [US3] Add `tools/d2net/tests/D2Net.Scaffold.Tests/ScaffoldPhaseInvarianceTests.cs`. Seed phase_sequence and phase_status with multiple non-scaffold rows. Run scaffold. Assert non-scaffold phase rows byte-identical; only the scaffold row changed (UPSERT IN_PROGRESS → COMPLETED).
- [ ] T029a [P] [US3] Add `tools/d2net/tests/D2Net.Scaffold.Tests/ScaffoldContentionTests.cs`. Use the `BridgeFactory` test seam (mirroring 007's `AddExcludeContentionTests`). Inject a `BridgeStartException` whose payload matches the lock-contention pattern set (`EBUSY`, `data directory in use`, etc.); assert exit 28 (`ScaffoldWorkspaceLocked`). Also test that a non-lock `BridgeStartException` (e.g., `NodeMissing`) maps to its existing exit code (10), not 28.

### Implementation for User Story 3

- [ ] T030 [US3] Implement DestructiveTargetGate (T006 stub → real) with the exact prompt text from the contract.
- [ ] T031 [US3] Wire FR-012 detection in ScaffoldRunner (consult scaffold_tracker rows; check if any reference target_parent_dir under the configured target root).
- [ ] T032 [US3] Wire FR-012a destructive override branch in ScaffoldRunner: when `--FORCE --DELETE-TARGET` AND target exists AND not scaffold-managed → call DestructiveTargetGate; on cancel → exit 29; on confirm → set destructiveOverride=true and proceed (DB writer handles the cleanup).
- [ ] T033 [US3] Implement collision pre-walk detection in TargetTreePlanner (T007).
- [ ] T034 [US3] Static SQL audit assertion: ensure ScaffoldRunner / ScaffoldDbWriter never emit SQL referencing `phase_sequence` or `phase_status` except the documented scaffold-row UPSERT.

**Checkpoint**: P3 acceptance scenarios pass.

---

## Phase 6: Polish & cross-cutting

- [ ] T035 Audit and remove obsoleted source files: `CompanionFileWriter.cs`, `RefreshRunner.cs`, parts of the old `ScaffoldRunner` (replaced by the new one). Move to a `.archive/` subdirectory if reviewers prefer to keep them visible.
- [ ] T036 Audit and remove obsoleted test files in `D2Net.Scaffold.Tests/` (refresh-mode tests, companion-file tests). Tests covering the (now-removed) old CLI surface must go.
- [ ] T037 Run the full suite: `dotnet build tools/d2net/D2Net.sln -c Debug && dotnet test tools/d2net/D2Net.sln -c Debug`. Compare against post-008 baseline. New 009 tests must pass; previously-green tests for the old scaffold are expected to be replaced by new equivalents (audit per T036).
- [ ] T038 [P] Smoke-test against the live workspace at `.D2NET/`. Run `tools/d2net/src/D2Net.Scaffold/bin/Debug/net8.0/d2net-scaffold.exe --json` (timed). Verify the live target tree at `D:\BSTDEV\RESEARCH\glp\glpnet\glp_runtime_net\` is created; spot-check a few `__<basename>/` directories; verify `dart_files` rows have the new columns populated; verify wall-clock under 60s (SC-001).
- [ ] T039 [P] Verify `--help` text. Add `ScaffoldArgParserTests` assertion that the new lines per the contract are present.
- [ ] T040 If quickstart.md required tweaks during implementation, update it.
- [ ] T041 Stage and commit by name. Files: `tools/d2net/src/D2Net.Scaffold/*` + new test files + `specs/009-scaffold-mirror/` + `CLAUDE.md`. Single-line commit message: `D2NET.Scaffold: source-tree mirror with per-dart workdirs (009)`.

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
