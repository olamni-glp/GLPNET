---
description: "Task list for D2NET.Init — Non-Destructive Exclusion Removal (--remove-exclude)"
---

# Tasks: D2NET.Init — Non-Destructive Exclusion Removal (`--remove-exclude`)

**Input**: Design documents from `specs/008-remove-exclude/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/remove-exclude-cli-contract.md

**Tests**: Included. Mirrors feature 007's pattern. Tests are written before the implementation that satisfies them inside each story.

**Organization**: by user story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Different file, no ordering dependency on tasks in the same phase. Can run in parallel.
- **[Story]**: Maps to a user story (US1, US2, US3) or marks setup/foundational/polish.
- File paths are absolute or repository-relative.

---

## Phase 1: Setup (baseline)

- [X] T001 Establish baseline build. Run `dotnet build tools/d2net/D2Net.sln -c Debug` and `dotnet test tools/d2net/D2Net.sln -c Debug`. Capture the green pass count (post-007 baseline: 130 D2Net.Init tests, 33–34 Scaffold tests; the Scaffold perf-budget test is a known flake). If any non-flaky pre-existing test fails, STOP and report.

---

## Phase 2: Foundational (blocks all user stories)

**Purpose**: shared infrastructure — exit codes, parser entrypoint, options record, mutator, runner shell. After this phase the new mode is reachable end-to-end.

- [X] T002 [P] Add five new exit code constants to `tools/d2net/src/D2Net.Init/ExitCodes.cs`: `RemoveExcludePathOutsideSource = 17`, `RemoveExcludeSettingsWriteFailed = 18`, `RemoveExcludeDbWriteFailed = 19`, `RemoveExcludeWorkspaceLocked = 20`, `RemoveExcludeSystemKindRefused = 21`. Each with an XML doc summary.
- [X] T003 [P] Create `tools/d2net/src/D2Net.Init/RemoveExcludeOptions.cs` — a `record` capturing `RepoRoot`, `RawPaths` (IReadOnlyList<string>), `AllowSystemExclusions` (bool), `Json` (bool), `BridgePortOverride` (int?). Mirrors `AddExcludeOptions` shape from feature 007.
- [X] T004 [P] Create `tools/d2net/src/D2Net.Init/ExclusionRemover.cs`. Single static method `ApplyRemoveExclude(NpgsqlConnection conn, IReadOnlyList<string> acceptedPaths, IReadOnlyList<DartFileEntry> rowsToInsert)` that:
  - Begins an `NpgsqlTransaction`.
  - Executes `DELETE FROM excluded_directories WHERE path = ANY(@paths)` with `@paths` parameterised. Captures `RowsAffected`.
  - For each `DartFileEntry`, executes `INSERT INTO dart_files (filename, full_path) VALUES (@f, @p) ON CONFLICT (full_path) DO NOTHING`. Captures aggregate insert count.
  - Commits.
  - Returns a `MutationResult` record (`DeletedRows`, `InsertedRows`).
  Throws `NpgsqlException` on failure; the caller maps to exit 19.
- [X] T005 Extend `ArgParser` in `tools/d2net/src/D2Net.Init/Program.cs` to recognise repeatable `--remove-exclude <path>` and the binary `--allow-system-exclusions` flag. Add a `ParsedCli.RemoveExcludeMode(RemoveExcludeOptions Options)` case. Reject combination with init flags (`--source`, `--target`, `--target-extension`, `--exclude`, `--accept-suggested-exclusions`, `--FORCE`, `--DELETE-EXISTING`, `--non-interactive`), inspection flags (`--list`, `--Exclusions`, `--current-phase`), and `--add-exclude`. `--bridge-port`, `--json`, and `--allow-system-exclusions` are accepted alongside `--remove-exclude`. Wire the new case into `Program.Run` to invoke a stub `RemoveExcludeRunner` that returns 0 until T011.
- [X] T006 Create `tools/d2net/src/D2Net.Init/RemoveExcludeRunner.cs` with `int Run(RemoveExcludeOptions options)` (and a `RunForTesting(opts, BridgeFactory)` test seam mirroring `AddExcludeRunner`). The runner:
  - `WorkspaceLayout.Resolve` + verify `.D2NET/`. Else exit 6.
  - `SettingsWriter.TryReadSnapshot` to obtain `source_dir` and current exclusions.
  - For each raw path: `PathValidator.Canonicalise`, `ResolveUnderSource`, file-vs-directory check. Reject the entire invocation on any path-level error (exit 17 / `AddExcludePathIsFile`-equivalent — share the existing 007 file-path code or allocate a new one; default to reusing 16 for symmetry). Refuse `.` (the source root itself).
  - Deduplicate and lexicographically sort the supplied paths.
  - Spawn the bridge. Lock contention → exit 20 via the same pattern set as feature 007's `AddExcludeRunner.MapBridgeStartFailure`.
  - Open Npgsql; run `SELECT path, kind FROM excluded_directories WHERE path = ANY(@paths)` (research R3).
  - Classify: `not-currently-excluded` (no row), `manual-removable` (`kind='manual'`), or `system-kind` (`kind != 'manual'`).
  - If any `system-kind` AND `--allow-system-exclusions` not supplied → emit one stderr line per offending path-and-kind pair, optional `--json` error envelope, exit 21. No transaction.
  - Compute the post-removal exclusion list (existing minus accepted-for-removal, sorted ascending).
  - Pre-walk classify accepted-for-removal paths against post-removal list for ancestor-survival (`PathValidator.IsUnder` reused). Paths covered by a survivor get a `covered-by-ancestor` summary entry and are excluded from the file walk.
  - Run `DartFileScanner.Scan(layout.RepoRoot, sourceDir, postRemovalExclusions)` to produce the new `dart_files` rows.
  - Filter the scanner's results to only include rows whose `full_path` lies under one of the **non-ancestor-covered** removed paths (so we don't accidentally re-insert rows for the rest of the source tree).
  - Prepare temp settings JSON via `SettingsWriter.PrepareTempSettingsWithExclusions`.
  - Call `ExclusionRemover.ApplyRemoveExclude`. On `NpgsqlException` → delete temp JSON, exit 19.
  - `SettingsWriter.CommitTempFile`. On IO exception → emit divergence stderr, exit 18.
  - Emit success summary (text or `--json`) per the contract; exit 0.
  Always disposes the bridge in `finally`.

**Checkpoint**: foundation ready. `d2net-init --remove-exclude foo` reaches the runner end-to-end and produces a real result on a workspace.

---

## Phase 3: User Story 1 — Undo a wrongly-applied exclusion (Priority: P1) 🎯 MVP

**Goal**: a single-path or multi-path remove-exclude invocation removes the matching `excluded_directories` rows, re-indexes the on-disk `.dart` files, leaves `phase_sequence` and `phase_status` byte-identical, and surfaces the change immediately to `--list` and `--Exclusions`.

**Independent Test**: from a workspace where `lib/legacy/` is excluded and contains 12 `.dart` files, invoke `d2net-init --remove-exclude lib/legacy`; verify exit 0, the row is gone, and `dart_files` grew by exactly 12 rows. Phase tables unchanged.

### Tests for User Story 1 (write first)

- [X] T007 [P] [US1] Add `tools/d2net/tests/D2Net.Init.Tests/RemoveExcludeArgParserTests.cs`. Cases: `--remove-exclude foo` parses; `--remove-exclude foo --remove-exclude bar` parses; `--remove-exclude` without value → `Error`; `--remove-exclude foo --add-exclude bar` → `Error`; `--remove-exclude foo --list` → `Error`; `--remove-exclude foo --allow-system-exclusions --json` parses with both flags; `--allow-system-exclusions` without `--remove-exclude` → `Error`.
- [X] T008 [P] [US1] Add `tools/d2net/tests/D2Net.Init.Tests/RemoveExcludePathRejectionTests.cs`. Cases (using a hand-fabricated skeleton workspace, no bridge): no workspace → exit 6; `../escape` → exit 17; absolute path outside source → exit 17; existing file path → exit 16 (or whichever code is chosen — consistent with 007); `.` (source root) → exit 17; all-or-nothing rejection when one of three paths is invalid; `--json` error envelope present on every rejection.
- [X] T009 [P] [US1] Add `tools/d2net/tests/D2Net.Init.Tests/RemoveExcludeRunnerTests.cs`. Use `TempRepoBuilder`. Cases:
  1. Init with N=200 dart files, K=8 exclusions seeded; manually add a `--add-exclude lib/legacy` for a sub-directory containing 3 dart files; then `--remove-exclude lib/legacy`. Assert: exit 0; `excluded_directories` shrunk by 1; `dart_files` grew by exactly 3 (NOT 200 — the walk must filter to the removed path); `--list` includes the re-indexed `lib/legacy` paths AND no spurious extra paths; `--Exclusions` no longer lists `lib/legacy`. (FR-013 post-success-visibility + data-model "filter scanner results" invariant.)
  2. Same as 1 with `--json`. Validate the JSON shape against the contract.
  3. Multiple paths in one call: 2 manual exclusions removed in one transaction; counts surface independently in the summary.
  4. `not-currently-excluded` no-op: `--remove-exclude something_never_excluded` → exit 0, summary names it under the not-present block, 0 rows inserted.
  5. Idempotent re-run: invoke case 1 twice. Second run: 0 removed, 1 not-present (named), exit 0.

### Implementation for User Story 1

- [X] T010 [US1] Wire happy-path through `Program.Run` → `RemoveExcludeRunner` → success (T009 cases pass).
- [X] T011 [US1] Implement the success summary text formatter per the contract.
- [X] T012 [US1] Implement the `--json` success formatter per the contract schema.
- [X] T013 [US1] Update `Program.PrintUsage` to insert the new "Incremental exclusion removal" block per the contract; add the `--add-exclude` Notes addendum.

**Checkpoint**: P1 acceptance scenarios pass.

---

## Phase 4: User Story 2 — Drive review-then-undo from /D2NET-init skill (Priority: P2)

**Goal**: multiple successive add/remove invocations preserve cumulative state; phase rows untouched.

**Independent Test**: 14 batched additions across 3 invocations, then 1 multi-path remove invocation that takes 5 of them away. Final exclusion-set count = 9; cumulative `dart_files` count = original − (covered-by-survivor count).

### Tests for User Story 2

- [X] T014 [P] [US2] Add `tools/d2net/tests/D2Net.Init.Tests/RemoveExcludeAncestorSurvivalTests.cs`. Seed a workspace with both `bin` (manual) and `bin/archive` (manual) excluded; run `--remove-exclude bin/archive`; assert exit 0, `excluded_directories` shrinks by 1, `dart_files` grew by 0, summary names `bin/archive` as `covered-by-ancestor: bin`.
- [X] T015 [P] [US2] Extend `RemoveExcludeRunnerTests` with a "10-batch round-trip" case: alternate add and remove invocations 10 times against the same workspace; assert phase rows are byte-identical to pre-test state. (May be a longer-running test; `[Trait("Category","Slow")]` it.)
- [X] T016 [P] [US2] Add `tools/d2net/tests/D2Net.Init.Tests/RemoveExcludePhaseInvarianceTests.cs`. Same shape as 007's invariance test: seed `phase_sequence` + `phase_status` with non-trivial rows, run `--remove-exclude`, assert byte-identity.

### Implementation for User Story 2

- [X] T017 [US2] Implement the ancestor-survival pre-walk classification in `RemoveExcludeRunner` (research R2). Verify the post-removal `DartFileScanner.Scan` correctly skips ancestor-covered subtrees.
- [X] T018 [US2] Confirm the mutator wraps the multi-path DELETE + multi-row INSERT in one transaction. Add an explicit assertion in `RemoveExcludeRunnerTests` case 3 (T009.3).
- [X] T019 [US2] Confirm `phase_sequence` and `phase_status` are never named in `RemoveExcludeRunner` or `ExclusionRemover` SQL (static grep + reflection assertion mirroring 007's T020).

**Checkpoint**: P2 acceptance scenarios pass.

---

## Phase 5: User Story 3 — Inspect, diagnose, and recover (Priority: P3)

**Goal**: every misuse mode produces a distinct exit code; on every failure the workspace is bit-identical to its pre-run state; the system-kind protection is enforced by default and overridable via `--allow-system-exclusions`.

**Independent Test**: trigger every documented error path and the system-kind override path; assert exit codes and bit-identical workspace post-failure.

### Tests for User Story 3

- [X] T020 [P] [US3] Add `tools/d2net/tests/D2Net.Init.Tests/RemoveExcludeSystemKindTests.cs`. Cases:
  1. Workspace where `bin` is excluded with `kind='tool'` (init's auto-detection). Run `--remove-exclude bin` without override → exit 21; stderr names `bin` and its `kind='tool'`; `excluded_directories` byte-identical to pre-run.
  2. Same workspace + `--remove-exclude bin --allow-system-exclusions` → exit 0; `bin` is removed; the summary shows `kind: tool` annotation on the removed entry; the dart files under `bin/` are re-indexed.
  3. Mixed batch: `--remove-exclude bin --remove-exclude lib/legacy` (where `lib/legacy` is manual). Without `--allow-system-exclusions` → exit 21; both stderr offenders surfaced (only `bin`); the manual-`lib/legacy` is NOT removed (all-or-nothing). With `--allow-system-exclusions` → both removed.
- [X] T021 [P] [US3] Add `tools/d2net/tests/D2Net.Init.Tests/RemoveExcludeContentionTests.cs`. Use the `BridgeFactory` test seam (mirroring 007's contention test): inject a `BridgeStartException` whose `LastBridgeError` matches the lock-contention pattern set; assert exit 20.
- [X] T022 [P] [US3] Extend the existing atomicity discipline: a fault-injection test (or a documentation entry if not feasible) that confirms the rollback path leaves the workspace bit-identical when DELETE / INSERT / COMMIT raises. May be a unit test using a fake `INpgsqlConnection`; if infeasible, skip and document in tasks.md notes.

### Implementation for User Story 3

- [X] T023 [US3] Implement the system-kind classification + refuse-or-allow branching in `RemoveExcludeRunner` (research R3 + R6).
- [X] T024 [US3] Implement the `--json` error envelope including the `offenders` array for the system-exclusion-refused case.
- [X] T025 [US3] Implement the `kind` annotation in both text and JSON success-summary blocks for removed entries.

**Checkpoint**: P3 acceptance scenarios pass.

---

## Phase 6: Polish & cross-cutting

- [X] T026 Run the full suite: `dotnet build tools/d2net/D2Net.sln -c Debug && dotnet test tools/d2net/D2Net.sln -c Debug`. Compare against post-007 baseline (130 D2Net.Init tests + Scaffold). All previously-green tests must remain green; new feature 008 tests must pass.
- [X] T027 [P] Smoke-test the Debug binary against the live workspace at `.D2NET/`. The workspace currently has `test/programs` from feature 007's earlier interactive review (which was a wrong call). Run a timed `tools/d2net/src/D2Net.Init/bin/Debug/net8.0/d2net-init.exe --remove-exclude test/programs --json` (capture wall-clock duration via PowerShell `Measure-Command` or shell `time`). Verify (a) exit 0; (b) `test/programs` is gone from `--Exclusions`; (c) since `test/programs` contains 0 `.dart` files, `--list` count is unchanged; (d) the wall-clock duration is under 15 seconds (SC-001); (e) the smoke test concludes that the wrongly-applied exclusion is reversed without a destructive rebuild.
- [X] T028 [P] Verify `--help` output contains the new lines. Add an `ArgParserTests` (or `HelpTextTests`) assertion.
- [X] T029 If quickstart.md required tweaks during implementation, update it. Otherwise leave untouched.
- [X] T030 Stage and commit by name. Files: `tools/d2net/src/D2Net.Init/{ExitCodes,Program,RemoveExcludeOptions,RemoveExcludeRunner,ExclusionRemover}.cs` + new test files + `specs/008-remove-exclude/` + `CLAUDE.md`. Single-line commit message: `D2NET.Init: --remove-exclude with --allow-system-exclusions safety override`.

---

## Dependencies & execution order

- Setup (T001) before everything else.
- Foundational T002 / T003 / T004 in parallel; T005 depends on T003; T006 depends on T002–T005.
- US1 tests (T007 / T008 / T009) in parallel; impl T010–T013 sequential against the same `RemoveExcludeRunner` file.
- US2 tests (T014 / T015 / T016) in parallel; impl T017–T019 mostly in `RemoveExcludeRunner` so sequential.
- US3 tests (T020 / T021 / T022) in parallel; impl T023–T025 sequential against `RemoveExcludeRunner`.
- Polish (T026–T030) after every story; T027 / T028 / T029 in parallel.

## Out of scope (do NOT do as part of this feature)

- Changes to `--add-exclude`, init-mode `--exclude`, or `--FORCE --DELETE-EXISTING`.
- Bulk operations (e.g., remove all manual exclusions).
- Pattern / glob path matching.
- File-level removals.
- Re-running the auto-detection heuristics.
- Any change to the `/D2NET-init` skill contract — that is a separate spec track.
