---
description: "Task list for D2NET.Init — Non-Destructive Incremental Exclusion Updates"
---

# Tasks: D2NET.Init — Non-Destructive Incremental Exclusion Updates

**Input**: Design documents from `specs/007-incremental-exclusions/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/add-exclude-cli-contract.md

**Tests**: Included. The spec has explicit acceptance scenarios per user story and explicit measurable success criteria; existing `tools/d2net/tests/D2Net.Init.Tests/` layout mirrors the source. Tests are written before the implementation that satisfies them inside each story.

**Organization**: Tasks are grouped by user story so each story can be implemented and validated independently. Setup and Foundational phases precede story work.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Different file, no ordering dependency on tasks in the same phase. Can run in parallel.
- **[Story]**: Maps the task to a user story (US1, US2, US3) or marks setup/foundational/polish.
- File paths are absolute or repository-relative.

---

## Phase 1: Setup (baseline)

- [X] T001 Establish baseline build. Run `dotnet build tools/d2net/D2Net.sln -c Debug` and `dotnet test tools/d2net/D2Net.sln -c Debug`. Capture the green pass count to compare against post-change runs. If baseline is not green, STOP and report to Gabi before any changes.

---

## Phase 2: Foundational (blocks all user stories)

**Purpose**: shared infrastructure — exit codes, parser entrypoint, path validator, settings extension, mutator, runner shell. After this phase the new mode is reachable end-to-end with stub success; user stories then exercise the real behaviour.

- [X] T002 [P] Add five new exit code constants to `tools/d2net/src/D2Net.Init/ExitCodes.cs` per research R3 and the contract: `AddExcludePathOutsideSource = 12`, `AddExcludeSettingsWriteFailed = 13`, `AddExcludeDbWriteFailed = 14`, `AddExcludeWorkspaceLocked = 15`, `AddExcludePathIsFile = 16`. Update the XML doc on `WorkspaceMissingForInspection = 6` to read "no workspace at CWD; applies to inspection and add-exclude alike" (semantic broadening only, value unchanged).
- [X] T003 [P] Create `tools/d2net/src/D2Net.Init/AddExcludeOptions.cs` — a `record` capturing the parsed `--add-exclude` paths (raw and canonicalised), the `--json` flag, and the optional `--bridge-port` override. Match the immutability style of `InitOptions` and `InspectOptions`.
- [X] T004 [P] Create `tools/d2net/src/D2Net.Init/PathValidator.cs` with a static API: `Canonicalise`, `IsUnderSourceRoot`, `LooksLikeFilePath`, `ClassifyAgainstExisting` (returns `Added | RedundantSelf | RedundantUnderAncestor("ancestor")`), and `DedupeIntraBatch`. The validator does NOT touch the database; it operates on the path strings and on a snapshot of current exclusions handed in by the caller.
- [X] T005 Extend `ArgParser` in `tools/d2net/src/D2Net.Init/Program.cs` to recognise repeatable `--add-exclude <path>` flags. Add a new `ParsedCli.AddExcludeMode(AddExcludeOptions Options)` case. Reject combination with init-mode flags (`--source`, `--target-extension`, `--target`, `--exclude`, `--accept-suggested-exclusions`, `--FORCE`, `--DELETE-EXISTING`, `--non-interactive`) and inspection flags (`--list`, `--Exclusions`, `--current-phase`) with `ParsedCli.Error`. `--bridge-port` and `--json` are accepted alongside `--add-exclude`. Wire the new case into `Program.Run` to invoke a stub `AddExcludeRunner` that returns `ExitCodes.ArgumentError` until T011.
- [X] T006 Extend `tools/d2net/src/D2Net.Init/SettingsWriter.cs` with `UpdateExcludedDirectories(string settingsFilePath, IReadOnlyList<string> newExcludedDirectoriesAscending)`. The method deserialises the existing JSON via the existing `SettingsJsonRoot` POCO, replaces `excluded_directories` only (preserving every other field including connection details and created_at), serialises with the existing `JsonOpts`, writes to a sibling `*.tmp` file with `fsync`, and exposes a `CommitTempFile(...)` helper for the caller to perform the atomic rename after the database transaction commits. Keep the existing `WriteSettingsFile` overload untouched.
- [X] T007 Create `tools/d2net/src/D2Net.Init/ExclusionMutator.cs`. Single static method `ApplyAddExclude(NpgsqlConnection conn, string sourceDir, IReadOnlyList<string> newPaths)` that:
  - Begins an `NpgsqlTransaction`.
  - For each path in `newPaths` (already canonical and de-duped by the validator), executes `INSERT INTO excluded_directories (path, kind) VALUES (@p, 'manual') ON CONFLICT (path) DO NOTHING` and captures `RowsAffected` for the insert.
  - For each path, executes the boundary-aware delete from `dart_files` per `data-model.md` and captures `RowsAffected`.
  - Commits.
  - Returns a `MutationResult` record with `InsertedRows`, `RemovedRowsByExclusion` (Dictionary<string,int>).
  Throws `NpgsqlException` (or wraps it) on failure; the caller maps to `ExitCodes.AddExcludeDbWriteFailed`.
- [X] T008 Create `tools/d2net/src/D2Net.Init/AddExcludeRunner.cs` with `int Run(AddExcludeOptions options, TextWriter stdout, TextWriter stderr)`. The runner:
  - Calls `WorkspaceLayout.Resolve(cwd)` and verifies `.D2NET/` exists (else exit 6).
  - Reads `D2NET-Settings.json` to obtain `source_dir` and the current `excluded_directories` snapshot.
  - Runs `PathValidator` to canonicalise, validate-under-source-root, file-vs-dir check, intra-batch dedupe, classify-against-existing.
  - On any path-level rejection: emit stderr per the contract, no temp JSON, no bridge spawn, exit 12 / 16.
  - Computes the new exclusion list (existing ∪ accepted, ascending lexicographic).
  - Calls `SettingsWriter.UpdateExcludedDirectories` to write the temp JSON.
  - Spawns `PgBridgeProcess.StartAsync`. If startup fails, parses `LastBridgeError` against the lock-contention pattern set (research R5) — match → exit 15; else delegate to existing exit-code mapping (7 / 8).
  - Calls `ExclusionMutator.ApplyAddExclude`. On `NpgsqlException` → delete the temp JSON, exit 14.
  - Calls `SettingsWriter.CommitTempFile` (atomic rename). On IO exception → emit stderr divergence warning, exit 13 (database is updated; settings file is stale).
  - On full success: emit text or JSON summary per the contract, exit 0.
  - Always disposes the bridge process in `finally`.
  Wire `Program.Run`'s `AddExcludeMode` case to invoke this runner.

**Checkpoint**: foundation ready; `d2net-init --add-exclude foo` reaches the runner end-to-end and reports a path-rejection or success on a real workspace, but no story-specific behaviour is asserted yet.

---

## Phase 3: User Story 1 — Add a directory to the exclusion list (Priority: P1) 🎯 MVP

**Goal**: a single-path or multi-path add-exclude invocation against an existing workspace records the new exclusions in `D2NET-Settings.json` and `excluded_directories`, removes the corresponding `dart_files` rows, leaves `phase_sequence` and `phase_status` byte-identical, and surfaces the change immediately to `--list` and `--Exclusions`.

**Independent Test**: from a workspace with 200 indexed `.dart` files (11 under `test_archive/`), `d2net-init --add-exclude test_archive` exits 0; `--Exclusions` lists `test_archive`; `--list` returns 189 paths; `phase_*` snapshots match.

### Tests for User Story 1 (write first; must FAIL before T012–T015)

- [X] T009 [P] [US1] Add `tools/d2net/tests/D2Net.Init.Tests/AddExcludeArgParserTests.cs`. Cases: `--add-exclude foo` parses to `AddExcludeMode([foo])`; `--add-exclude foo --add-exclude bar` parses to `AddExcludeMode([foo, bar])`; `--add-exclude` without a path → `Error`; `--add-exclude foo --source x` → `Error`; `--add-exclude foo --json --bridge-port 54401` parses with both options set.
- [X] T010 [P] [US1] Add `tools/d2net/tests/D2Net.Init.Tests/AddExcludePathValidatorTests.cs`. Cases: relative path under source accepted; absolute path that resolves under source accepted; path with backslash separators canonicalises to forward slashes; trailing slash stripped; same path twice in one batch collapses; path not in existing list classified `Added`; path equal to existing classified `RedundantSelf`; path under existing classified `RedundantUnderAncestor` with the ancestor reported.
- [X] T011 [P] [US1] Add `tools/d2net/tests/D2Net.Init.Tests/AddExcludeRunnerTests.cs`. Use the existing `Fixtures` infrastructure to spin up a fresh workspace seeded with N indexed dart files and K existing exclusions, then invoke the runner programmatically (no process boundary). Cases:
  1. Single new exclusion that covers M of the N files: stdout summary names the path with `M row(s)`; `excluded_directories` row count grows by 1; `dart_files` row count shrinks by M; exit 0. **Then run `--Exclusions` and `--list` programmatically and assert the new exclusion appears and the M removed files are absent (FR-013).**
  2. Same as 1 with `--json`: stdout is parseable JSON with the contract schema; `removed_rows[0].rows == M`.
  3. Three exclusions in one call, two of which contain dart files and one that's empty: a single transaction succeeds; per-path counts surface independently in the summary.
  4. Edge case: exclude `does_not_exist_yet/` (no filesystem entry, no dart_files match) — exit 0, 1 inserted, 0 removed.
  5. **Idempotent re-run (SC-004)**: invoke case 1 a second time with identical arguments. Assert exit 0; `excluded_directories` row count unchanged from after the first run; `dart_files` row count unchanged; the run summary reports 0 added, 1 redundant (`<path> -- already excluded`), 0 removed.

### Implementation for User Story 1

- [X] T012 [US1] Implement preflight + validation paths in `AddExcludeRunner` so that the four T011 cases pass.
- [X] T013 [US1] Implement the success summary text formatter per the contract's "Success output — text" layout. Place the formatter in `AddExcludeRunner` (or a small private helper class within the same file).
- [X] T014 [US1] Implement the `--json` success formatter per the contract's "Success output — JSON" schema. Use `System.Text.Json` `JsonSerializerOptions { WriteIndented = false }` so the document fits on one or a few lines.
- [X] T015 [US1] Update `Program.PrintUsage` to insert the new "Incremental exclusion update" block per the contract, plus the closing-Notes addition. Add a single golden-file test under `D2Net.Init.Tests` (or extend `ArgParserTests`) that asserts `--help` output contains the new lines verbatim.

**Checkpoint**: P1 acceptance scenarios 1, 2, and 4 from the spec pass against a real workspace; the binary's `--help` documents the new mode.

---

## Phase 4: User Story 2 — Drive incremental exclusions from the /D2NET-init skill (Priority: P2)

**Goal**: multiple successive invocations applied as batches preserve cumulative state, never lose an exclusion, and never disturb phase rows. Intra-batch path subsumption is reported as redundant rather than silently dropped.

**Independent Test**: run three successive `d2net-init --add-exclude ...` commands with batches of sizes 5, 5, 4. After the third, the union of all 14 directories is in `excluded_directories`, the cumulative `dart_files` reduction equals the sum of per-batch reductions, and `phase_sequence` / `phase_status` rows are byte-identical to their pre-first-batch state.

### Tests for User Story 2 (write first)

- [ ] T016 [P] [US2] Extend `AddExcludeRunnerTests` (or add `AddExcludeBatchTests.cs`) with three tests:
  1. Three successive runner invocations with batches of 5/5/4 paths: final exclusion-set count = 14; final `dart_files` count = initial − sum-of-per-batch removals.
  2. Same path supplied twice in one batch (`--add-exclude foo --add-exclude foo`): collapses to one `Added`, reports the second as `RedundantSelf`.
  3. Two paths in one batch where the second is a sub-path of the first (`--add-exclude bin --add-exclude bin/archive`): only `bin` is inserted; `bin/archive` is reported as `RedundantUnderAncestor("bin")` in the summary; only the `bin` prefix delete fires against `dart_files`.
- [X] T017 [P] [US2] Add `tools/d2net/tests/D2Net.Init.Tests/AddExcludePhaseInvarianceTests.cs`. Seeds `phase_sequence` and `phase_status` with non-trivial rows (multiple phases, mixed statuses, non-default `last_updated`), captures their full row sets and column values, runs a multi-path add-exclude invocation, and asserts row-by-row equality after the run.

### Implementation for User Story 2

- [X] T018 [US2] Implement intra-batch dedupe and ancestor-classification in `PathValidator.DedupeIntraBatch` per research R1. Sort paths ancestor-first lexicographically, walk forward marking each path either `Added` or `RedundantUnderAncestor`. Hand the resulting classified list to the runner.
- [X] T019 [US2] Confirm the mutator already wraps multi-path inserts and deletes in a single transaction (T007). Add an explicit assertion in `AddExcludeRunnerTests` Case 1 of T016 that the transaction is one COMMIT, by spying on the `NpgsqlTransaction` lifecycle (existing test infrastructure or a small wrapper).
- [ ] T020 [US2] Confirm `phase_sequence` and `phase_status` are never named in any SQL emitted by the mutator. Add a static assertion test (e.g., reflection or a regex-grep over the source) so future edits don't accidentally introduce phase mutations.

**Checkpoint**: P2 acceptance scenarios 1 and 2 from the spec pass; the runner safely handles overlapping paths within a single batch.

---

## Phase 5: User Story 3 — Inspect, diagnose, and recover from misuse (Priority: P3)

**Goal**: every misuse mode produces a distinct, documented exit code and a human-readable stderr message; on every failure the workspace is left bit-identical to its pre-invocation state.

**Independent Test**: trigger each of the five rejection modes (workspace-missing, path-outside-source, settings-write-failed, db-write-failed, lock-contention) plus the file-path mode; assert the exit codes match the contract and that `D2NET-Settings.json` and the database are bit-identical to their pre-run snapshots in every error case.

### Tests for User Story 3 (write first; the [P] marks indicate independent files)

- [X] T021 [P] [US3] Add `tools/d2net/tests/D2Net.Init.Tests/AddExcludeWorkspaceMissingTests.cs`. From a temp directory with no `.D2NET/`, invoke the runner with `--add-exclude foo`; assert exit 6 and that the directory is unchanged after the run.
- [X] T022 [P] [US3] Add `tools/d2net/tests/D2Net.Init.Tests/AddExcludePathOutsideSourceTests.cs`. Cases: `../outside/foo` → exit 12; `/etc` (or `C:\Windows` on Windows) → exit 12; `..` alone → exit 12; the workspace folder itself (`.D2NET`) → exit 12; supply three paths where the second escapes — entire invocation rejected, exit 12, `excluded_directories` row count unchanged.
- [X] T023 [P] [US3] Add `tools/d2net/tests/D2Net.Init.Tests/AddExcludePathIsFileTests.cs`. Cases: a path that exists as a regular file (e.g., `pubspec.yaml`) → exit 16; a non-existing path ending in `.dart` or `.zip` → exit 16; a non-existing extensionless name accepted (`Makefile.foo` not in suffix list).
- [ ] T024 [P] [US3] Add `tools/d2net/tests/D2Net.Init.Tests/AddExcludeAtomicityTests.cs`. Use a fault-injection seam (an `INpgsqlConnection` wrapper or `IFailureInjector`) to throw between INSERT and COMMIT; assert exit 14, no rows added or removed, temp JSON cleaned up, settings file byte-identical to pre-run. A second test injects a rename failure after COMMIT; assert exit 13, database updated, stderr emits the divergence warning, settings file is the pre-run version.
- [ ] T025 [P] [US3] Add `tools/d2net/tests/D2Net.Init.Tests/AddExcludeContentionTests.cs`. Spawn two `dotnet run` processes that both attempt `--add-exclude foo` against the same workspace; the loser's exit code MUST be 15. The winner's MUST be 0. Use a barrier + retry loop because the race window is short. If the platform / test infrastructure can't reliably stage the race, replace with a unit test that injects a `BRIDGE_ERROR data directory in use` payload into a fake `PgBridgeProcess` and asserts the runner maps it to exit 15.

### Implementation for User Story 3

- [X] T026 [US3] Implement the workspace-missing preflight: before reading settings JSON, `if (!Directory.Exists(layout.WorkspaceDir)) { stderr.WriteLine(...); return ExitCodes.WorkspaceMissingForInspection; }`. Mirror the existing inspector preflight wording.
- [X] T027 [US3] Implement the path-outside-source rejection in `PathValidator.IsUnderSourceRoot`. Use `Path.GetFullPath` rooted at the source directory's absolute path; reject if the canonical resolved path does not begin with the source directory's canonical absolute path + separator. All-or-nothing: the runner collects all path errors, emits all of them on stderr, and returns the first applicable exit code (precedence: 12 > 16).
- [X] T028 [US3] Implement the file-path rejection in `PathValidator.LooksLikeFilePath`. Existence check + suffix fallback per research R2. Suffix list lives in `PathValidator.LikelyFileSuffixes` and is documented inline.
- [X] T029 [US3] Implement the settings-write-failure path in `AddExcludeRunner`: catch `IOException` (and `UnauthorizedAccessException`) around the temp-file write and the rename; emit the contract's stderr message; return 13.
- [X] T030 [US3] Implement the DB-write-failure path: catch `NpgsqlException` from `ExclusionMutator.ApplyAddExclude`; rollback is automatic via the using-statement transaction; delete the temp JSON; emit stderr; return 14.
- [X] T031 [US3] Implement the lock-contention detection in `AddExcludeRunner`: when `PgBridgeProcess.StartAsync` reports `BRIDGE_ERROR`, inspect `LastBridgeError` against the substring set `{"EBUSY", "EACCES", "data directory in use", "could not lock", "another process"}`. Match → exit 15; non-match → fall through to existing 7 / 8 mappings.
- [X] T032 [US3] Implement the `--json` error envelope: when the runner is exiting with a non-zero code AND `options.Json == true`, emit `{ "result": "error", "code": <exit>, "message": "<one line>" }` on stdout in addition to the human-readable stderr.

**Checkpoint**: every documented error path produces the contracted exit code and leaves the workspace bit-identical to its pre-run state. SC-006 satisfied.

---

## Phase 6: Polish & cross-cutting

- [ ] T033 Run `dotnet build tools/d2net/D2Net.sln -c Debug` and `dotnet test tools/d2net/D2Net.sln -c Debug`. The full suite — including all pre-existing tests — must pass. If any pre-existing test fails, STOP and report; do not adjust pre-existing tests to accommodate new code.
- [X] T034 [P] Smoke-test the Debug binary against the live workspace at `.D2NET/`. Run `tools/d2net/src/D2Net.Init/bin/Debug/net8.0/d2net-init.exe --Exclusions` (baseline), then a timed `--add-exclude glp --add-exclude docs --json` (record wall-clock duration via PowerShell `Measure-Command` or shell `time`), then `--Exclusions` again, and confirm (a) the new entries appear, (b) the JSON stdout matches the contract schema, and (c) the wall-clock duration is under 2 seconds (SC-001). Capture stdout and the timing into a one-line operator log.
- [X] T035 [P] Verify `--help` output. Run `d2net-init --help` and grep for the literal lines added by T015. Add a test in `ArgParserTests` (or a new `HelpTextTests`) that asserts the lines are present.
- [ ] T036 If quickstart.md required any tweak during implementation, update `specs/007-incremental-exclusions/quickstart.md` accordingly. Otherwise leave untouched.
- [ ] T037 Stage and commit the diff: only files under `tools/d2net/src/D2Net.Init/`, `tools/d2net/tests/D2Net.Init.Tests/`, and `specs/007-incremental-exclusions/` (plus the `CLAUDE.md` SPECKIT marker change made by `/speckit-plan`). Commit message format: single line, e.g. `D2NET.Init: --add-exclude for non-destructive incremental exclusions`. Stage by file name, not `git add -A`.

---

## Dependencies & execution order

### Phase dependencies

- **Setup (T001)**: independent. Must succeed before any other phase begins.
- **Foundational (T002–T008)**: depends on Setup. T002 / T003 / T004 are `[P]` (different files). T005 depends on T003. T006 / T007 are independent of T003/T004. T008 depends on T002–T007.
- **US1 (T009–T015)**: depends on Foundational. T009 / T010 / T011 are `[P]` test files. T012–T015 depend on T011 failing first.
- **US2 (T016–T020)**: depends on Foundational + US1 (because T020 reuses validation introduced in US1).
- **US3 (T021–T032)**: depends on Foundational. Test files T021–T025 are `[P]`. Implementation tasks T026–T032 depend on the corresponding test failing first; they are mostly independent of each other but share `AddExcludeRunner.cs` so they MUST be applied sequentially within T026–T032.
- **Polish (T033–T037)**: depends on every preceding phase.

### Within each story

- Test files first; verify they fail.
- Implementation second; verify the targeted test passes; re-run the full suite.
- Commit per logical step.

### Parallel opportunities

- T002 / T003 / T004 in parallel (Foundational, different files).
- T009 / T010 / T011 in parallel (US1 tests, different files).
- T016 / T017 in parallel (US2 tests, different files).
- T021 / T022 / T023 / T024 / T025 in parallel (US3 tests, different files).
- T034 / T035 / T036 in parallel (Polish, different artefacts).

---

## Out of scope (do NOT do as part of this feature)

- `--remove-exclude` flag.
- File-level exclusions (`.zip`, `.dart`, `.DS_Store`, etc.).
- Any change to init-mode `--exclude` behaviour.
- Any change to the `/D2NET-init` skill contract at `.claude/skills/D2NET-init/`. The skill amendment is a separate feature track that will reference this feature once shipped.
- Schema migrations on `excluded_directories` (e.g., adding an `'incremental'` kind).
- Adjusting pre-existing tests in `D2Net.Init.Tests` to accommodate new code. If a pre-existing test fails because of a new edit, STOP and report.

---

## Implementation strategy

### MVP (US1 only)

1. T001 (baseline).
2. T002 → T003 → T004 → T005 → T006 → T007 → T008 (Foundational).
3. T009 / T010 / T011 (US1 tests, parallel; verify all fail).
4. T012 → T013 → T014 → T015 (US1 implementation; verify each test now passes).
5. STOP and validate with the smoke test (T034 portion).
6. Demoable: a single-path add-exclude works end-to-end.

### Incremental delivery

1. Ship MVP (US1) → smoke-test → commit.
2. Add US2 (T016–T020) → re-run suite → commit.
3. Add US3 (T021–T032) → re-run suite → commit.
4. Run Polish phase (T033–T037) → commit.

### Per-task discipline

- Single-line commit messages.
- Stage by name. No `git add -A`.
- Re-run the suite after every implementation task; never skip a re-run.
- If any pre-existing test fails, revert and discuss with Gabi before continuing.
