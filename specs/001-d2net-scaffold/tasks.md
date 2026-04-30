---

description: "Task list for d2net-scaffold (Dart-to-.NET conversion scaffold)"
---

# Tasks: d2net-scaffold — Dart-to-.NET Conversion Scaffold

**Input**: Design documents from `/specs/001-d2net-scaffold/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md (all present)

**Tests**: Included. The plan (R8 in `research.md`) specifies xUnit integration tests and lists concrete test files mapped to spec FRs. Test tasks are written so the test fails before its implementation lands, then passes after.

**Organization**: Tasks are grouped by user story to enable independent implementation and incremental delivery. User Story 1 (the one-shot fresh scaffold) is the MVP — completing through Phase 3 produces a fully usable tool.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3) — Setup/Foundational/Polish phases have no story label

## Path Conventions

All toolkit code lives under `tools/d2net/` at the repo root. Within that:

- Source: `tools/d2net/src/D2Net.Scaffold/`
- Tests:  `tools/d2net/tests/D2Net.Scaffold.Tests/`
- Solution: `tools/d2net/D2Net.sln`

The `glp_runtime_net/` directory is OUTPUT of running the tool — not part of this codebase, and not committed.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the .NET solution + project skeleton and register dependencies.

- [X] T001 Create directory `tools/d2net/` at the repo root with subdirectories `tools/d2net/src/` and `tools/d2net/tests/`
- [X] T002 Create empty solution file at `tools/d2net/D2Net.sln` (`dotnet new sln --name D2Net --output tools/d2net --format sln`)
- [X] T003 Create the main project: `dotnet new console --name D2Net.Scaffold --output tools/d2net/src/D2Net.Scaffold --framework net8.0` and add it to the solution at `tools/d2net/D2Net.sln`
- [X] T004 Edit `tools/d2net/src/D2Net.Scaffold/D2Net.Scaffold.csproj` to set `<Nullable>enable</Nullable>`, `<LangVersion>12.0</LangVersion>`, `<ImplicitUsings>enable</ImplicitUsings>`, and `<RootNamespace>D2Net.Scaffold</RootNamespace>`
- [X] T005 Add `System.CommandLine` (latest stable beta) and `System.Text.Json` package references to `tools/d2net/src/D2Net.Scaffold/D2Net.Scaffold.csproj`
- [X] T006 Create the test project: `dotnet new xunit --name D2Net.Scaffold.Tests --output tools/d2net/tests/D2Net.Scaffold.Tests --framework net8.0`, add it to `tools/d2net/D2Net.sln`, and add a project reference from the test project to `D2Net.Scaffold.csproj`
- [X] T007 [P] Add `.editorconfig` at `tools/d2net/.editorconfig` enforcing 4-space indent, LF endings, `var` preferences, and standard .NET naming conventions
- [X] T008 [P] Add `tools/d2net/README.md` with one-paragraph overview, link to `specs/001-d2net-scaffold/`, and the `dotnet run` invocation copied from `quickstart.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared types and infrastructure that every user story needs. No story work can begin until this phase is complete.

- [X] T009 [P] Create `tools/d2net/src/D2Net.Scaffold/ScaffoldOptions.cs` defining the `ScaffoldOptions` record (fields: `string SourceRoot`, `string TargetRoot`, `bool Refresh`) per data-model.md
- [X] T010 [P] Create `tools/d2net/src/D2Net.Scaffold/Models/RelPath.cs` defining `RelFile` and `RelDir` records (fields: `string RelPath`, `string AbsSourcePath`) and a static helper that normalises native paths to forward-slash form
- [X] T011 [P] Create `tools/d2net/src/D2Net.Scaffold/Models/Collision.cs` defining the `Collision` record (fields: `string DartFileRelPath`, `string CollidingExtension`, `string ExistingFileRelPath`)
- [X] T012 [P] Create `tools/d2net/src/D2Net.Scaffold/Models/CompanionExtensions.cs` exposing the closed list of nine companion extensions in stable order: `cs`, `ana`, `tst`, `con`, `dep`, `cgn`, `iss`, `sta`, `ver`
- [X] T013 [P] Create `tools/d2net/src/D2Net.Scaffold/Models/PrunedDirectories.cs` exposing the closed set of pruned directory names: `.dart_tool`, `build`, `.git`, `.idea`, `.vscode`
- [X] T014 [P] Create `tools/d2net/src/D2Net.Scaffold/Models/CompanionStatus.cs` exposing the closed status enumeration `todo` / `in-progress` / `done` / `blocked` as string constants, with `Todo` as the default
- [X] T015 Create `tools/d2net/src/D2Net.Scaffold/DirectoryWalker.cs` implementing recursive walk of the source tree that prunes directories whose names match `PrunedDirectories` and yields `RelDir` and `RelFile` results in deterministic (sorted) order; used by both fresh and refresh runners
- [X] T016 Create `tools/d2net/src/D2Net.Scaffold/WorkPlan.cs` defining the `WorkPlan` record (fields: `IReadOnlyList<RelDir> Directories`, `IReadOnlyList<RelFile> NonDartFiles`, `IReadOnlyList<RelFile> DartFiles`, `IReadOnlyList<Collision> Collisions`) and a static `Build(ScaffoldOptions, DirectoryWalker)` factory that classifies files by extension
- [X] T017 Create `tools/d2net/src/D2Net.Scaffold/RunSummary.cs` defining the mutable `RunSummary` class with counter fields per data-model.md and a `WriteTo(TextWriter)` method that produces the stdout summary block specified in `contracts/cli-contract.md`
- [X] T018 [P] Create `tools/d2net/tests/D2Net.Scaffold.Tests/Fixtures/FixtureBuilder.cs` exposing an `IDisposable` helper that creates a unique throwaway directory under `Path.GetTempPath()`, lets a test populate the source tree fluently (`AddFile(relPath, content)`, `AddDartFile(relPath, content)`), and recursively cleans up on `Dispose`

**Checkpoint**: Foundation ready — DirectoryWalker, WorkPlan builder, shared types, and the test fixture helper all exist. User story implementation can now begin.

---

## Phase 3: User Story 1 — One-shot fresh scaffold (Priority: P1) 🎯 MVP

**Goal**: Running the tool against `glp_runtime` (fresh) produces a complete `glp_runtime_net` mirror with `.dart.src` copies, nine companion stubs per Dart file, the verbatim non-Dart files, and a populated `d2net-tracker.json` at the target root.

**Independent Test**: Build a small fixture tree containing a mix of Dart and non-Dart files at multiple depths, run the tool, then assert: directory structure mirrored, every non-Dart file byte-equal, every `.dart` file preserved as `.dart.src` byte-equal, every `.dart` file has nine `.cs/.ana/.tst/.con/.dep/.cgn/.iss/.sta/.ver` companion stubs each containing the TODO line, tracker file at target root parses as JSON with one record per Dart file.

### Tests for User Story 1 (write first, watch them fail)

- [X] T019 [P] [US1] Create `tools/d2net/tests/D2Net.Scaffold.Tests/PrunedDirectoriesTests.cs` with tests verifying that `DirectoryWalker` skips files under `.dart_tool/`, `build/`, `.git/`, `.idea/`, `.vscode/` at any depth and that those directory names are not created in the target tree (covers FR-002 and SC-001/002/003)
- [X] T020 [P] [US1] Create `tools/d2net/tests/D2Net.Scaffold.Tests/DartSrcRenameTests.cs` with tests verifying `foo.dart → foo.dart.src` and the multi-dot edge case `foo.bar.dart → foo.bar.dart.src` plus byte-equality of the preserved file against source (covers FR-004 and the "multi-dot Dart filenames" edge case)
- [X] T021 [P] [US1] Create `tools/d2net/tests/D2Net.Scaffold.Tests/CompanionStubTests.cs` with tests verifying each `.dart` file produces exactly nine companion files with the correct extensions, that each contains a `// TODO: d2net …` single-line comment, and that the comment includes the basename and extension (covers FR-005, FR-006, SC-004, SC-005)
- [X] T022 [P] [US1] Create `tools/d2net/tests/D2Net.Scaffold.Tests/PreflightCollisionTests.cs` with a fixture that places `runner.dart` and `runner.cs` in the same source folder and asserts the runner aborts with exit code 5, prints all collisions to stderr, and writes nothing to the target (covers FR-012 and the "filename collisions" edge case)
- [X] T023 [P] [US1] Create `tools/d2net/tests/D2Net.Scaffold.Tests/ScaffoldFreshTests.cs` with an end-to-end test that builds a representative fixture (≈10 files, mixed Dart/non-Dart, two depth levels including a pruned `build/` directory) and asserts every spec invariant: folder count, non-Dart byte-equality, `.dart.src` count, companion-stub presence and content, tracker existence and array length

### Implementation for User Story 1

- [X] T024 [P] [US1] Create `tools/d2net/src/D2Net.Scaffold/PreflightChecker.cs` exposing `static IReadOnlyList<Collision> Detect(WorkPlan plan)` that, for each Dart file in the plan, generates the nine candidate companion paths and returns a `Collision` for any that already exist as a non-Dart source file in the same source folder
- [X] T025 [P] [US1] Create `tools/d2net/src/D2Net.Scaffold/FileCopier.cs` exposing `CopyVerbatim(absSource, absTarget)` (used for non-Dart files, FR-003) and `CopyAsDartSrc(absSource, absTarget)` (used for `.dart` → `.dart.src`, FR-004) — both creating parent directories on demand and using `File.Copy` with overwrite semantics suitable for fresh mode
- [X] T026 [P] [US1] Create `tools/d2net/src/D2Net.Scaffold/CompanionFileWriter.cs` exposing `WriteAllNine(targetDir, dartBaseName)` that writes nine files with the closed extensions, each containing the line `// TODO: d2net — port from <basename>.dart.src (artifact: <ext>)` (FR-005, FR-006, R5)
- [X] T027 [US1] Create `tools/d2net/src/D2Net.Scaffold/TrackerWriter.cs` exposing `WriteFreshTracker(targetRoot, IReadOnlyList<RelFile> dartFiles)` that builds the `TrackerRecord[]` array with every companion status set to `todo` and serializes it with `System.Text.Json` and `WriteIndented = true` to `<targetRoot>/d2net-tracker.json` (FR-007 – FR-010)
- [X] T028 [US1] Create `tools/d2net/src/D2Net.Scaffold/ScaffoldRunner.cs` orchestrating fresh-mode flow: validate `ScaffoldOptions`, build `WorkPlan` via `DirectoryWalker` + `WorkPlan.Build`, run `PreflightChecker` (early return on collisions), then execute the write pass — create directories, copy non-Dart files, copy `.dart.src` files, write companion stubs, write tracker, return populated `RunSummary`. Depends on T015–T017, T024–T027.
- [X] T029 [US1] Create `tools/d2net/src/D2Net.Scaffold/Program.cs` wiring `System.CommandLine` per `contracts/cli-contract.md`: positional `<source>` and `<target>` arguments, `--refresh` flag (parsed but routed only in US3), `--help` (handled by `System.CommandLine`'s built-in help), and `--version` printing the assembly's `InformationalVersion` (`AssemblyInformationalVersionAttribute`, falling back to the assembly version) then exiting 0. Set `<Version>0.1.0</Version>` (or equivalent) in `D2Net.Scaffold.csproj` to seed the version metadata. For US1, route to `ScaffoldRunner`, print the `RunSummary` block to stdout, and return exit code 0 on success or one of {1, 2, 4, 5} per the contract. Depends on T028.

**Checkpoint**: At this point, US1 is fully functional. Running `dotnet run --project tools/d2net/src/D2Net.Scaffold -- <src> <tgt>` against a fresh target produces the complete scaffold (incl. tracker). MVP complete.

---

## Phase 4: User Story 2 — JSON tracker as the single source of truth (Priority: P2)

**Goal**: The `d2net-tracker.json` produced by US1 is well-formed and schema-conformant, ready to be consumed by downstream conversion tools and dashboards.

**Independent Test**: After running US1, validate the tracker file: it parses as JSON, the top level is a single array, the array length equals the count of `.dart` files outside pruned directories, every record references a real `.dart.src` file in the target, every record lists exactly nine companion entries with the correct extensions, every status is `todo`.

### Tests for User Story 2

- [X] T030 [P] [US2] Create `tools/d2net/tests/D2Net.Scaffold.Tests/TrackerSchemaTests.cs` with tests asserting the tracker JSON validates against `specs/001-d2net-scaffold/contracts/tracker-schema.json`: top level is an array; every record has `source` ending in `.dart.src` and a `companions` object with exactly the nine fixed keys; every status equals `todo` after a fresh run; record count equals the count of `.dart` files yielded by `DirectoryWalker` over the same source tree (covers FR-007–FR-010, SC-006)

### Implementation for User Story 2

- [X] T031 [US2] Add `tools/d2net/src/D2Net.Scaffold/Models/TrackerRecord.cs` defining the `TrackerRecord` record with `[JsonPropertyName("source")] string Source` and `[JsonPropertyName("companions")] Dictionary<string, string> Companions`, used by `TrackerWriter` (refactor T027 to use this record). This formalises the schema contract in code and is what `TrackerSchemaTests` reads back.
- [X] T032 [US2] Update `TrackerWriter` (`tools/d2net/src/D2Net.Scaffold/TrackerWriter.cs`) so the companions dictionary is written in the canonical extension order from `CompanionExtensions` (T012), matching the JSON Schema contract and giving deterministic git diffs

**Checkpoint**: Tracker is schema-conformant; downstream tools can rely on the file shape. US1 + US2 together deliver a fully usable scaffold artefact.

---

## Phase 5: User Story 3 — Safe re-run / `--refresh` (Priority: P3)

**Goal**: Re-running the tool against an existing target is predictable: default mode refuses without touching anything; `--refresh` mode refreshes source-derived files (`.dart.src`, non-Dart copies) but never overwrites companion files (`.cs`, `.ana`, …) or the tracker.

**Independent Test**: (a) Run US1, then re-run without `--refresh` → tool exits with code 3 and the target tree is byte-identical. (b) Run US1, edit one of the generated `.cs` files to contain real C# code, re-run with `--refresh` → exit 0, every `.dart.src` and non-Dart file refreshed from current source, the edited `.cs` file is byte-identical to the edited version (untouched), `d2net-tracker.json` is byte-identical (untouched).

### Tests for User Story 3

- [X] T033 [P] [US3] Create `tools/d2net/tests/D2Net.Scaffold.Tests/ExitCodeTests.cs` with tests covering: exit code 2 (source missing), exit code 3 (target exists, no `--refresh`), exit code 4 (target nested in source), exit code 6 (refresh with missing target), exit code 5 already covered by `PreflightCollisionTests` (US1) (covers FR-011, FR-014 and the contract's exit-code matrix)
- [X] T033a [P] [US1] Add a test `tools/d2net/tests/D2Net.Scaffold.Tests/HelpAndVersionTests.cs` asserting that `--help` exits 0 and that `--version` exits 0 with a non-empty version string on stdout (covers the CLI contract's metadata flags)
- [X] T034 [P] [US3] Create `tools/d2net/tests/D2Net.Scaffold.Tests/RefreshModeTests.cs` covering: (a) edited `.cs` companion file is preserved byte-identical after `--refresh`; (b) `d2net-tracker.json` is byte-identical after `--refresh`; (c) a `.dart.src` whose source was edited is byte-identical to the new source content; (d) a newly-added `.dart` source file gets nine fresh companion stubs but no tracker entry, and is listed in the run summary's "New Dart files" line (covers FR-011 (a)–(f), SC-008, SC-009)

### Implementation for User Story 3

- [X] T035 [US3] Create `tools/d2net/src/D2Net.Scaffold/RefreshRunner.cs` implementing the refresh-mode pipeline: walk the source via `DirectoryWalker`, for each non-Dart file call `FileCopier.CopyVerbatim` (overwrite), for each `.dart` file call `FileCopier.CopyAsDartSrc` (overwrite the existing `.dart.src`), for each `.dart` file check whether its companion stubs exist and call `CompanionFileWriter.WriteAllNine` only when they don't (track those as "newly-discovered" in `RunSummary.NewlyDiscoveredDartFiles`), and explicitly DO NOT call `TrackerWriter`
- [X] T036 [US3] Update `tools/d2net/src/D2Net.Scaffold/RunSummary.cs` so `WriteTo(TextWriter)` adds the "Mode: fresh|refresh" line and, in refresh mode, the "New Dart files (no tracker entry, please update d2net-tracker.json manually):" block specified in `contracts/cli-contract.md`
- [X] T037 [US3] Update `tools/d2net/src/D2Net.Scaffold/CompanionFileWriter.cs` to expose a second method `WriteIfMissing(targetDir, dartBaseName)` returning `bool` (true iff stubs were freshly written) so `RefreshRunner` can avoid overwriting existing companion files; `WriteAllNine` continues to be used unchanged in fresh mode
- [X] T038 [US3] Update `tools/d2net/src/D2Net.Scaffold/Program.cs` to: (a) reject the run with exit code 3 when the target exists in default mode, (b) reject with exit code 6 when `--refresh` is set but the target does not exist, (c) reject with exit code 4 when target == source or is nested inside source (FR-014), (d) when `--refresh` is set, route to `RefreshRunner` instead of `ScaffoldRunner`. Depends on T035, T036.

**Checkpoint**: All three user stories are independently functional. The toolkit is feature-complete per the spec.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T039 [P] Run `quickstart.md` end-to-end against the real `glp_runtime` directory: wrap the `dotnet run` invocation in a stopwatch, verify the produced `glp_runtime_net` matches all of SC-001 through SC-006, and assert that wall-clock time is under 30 s (SC-007). Record the observed elapsed time and counts in a one-line note in `tools/d2net/README.md`. If the assertion fails, file a follow-up rather than relaxing the budget without spec discussion.
- [X] T040 [P] Add a unit test `tools/d2net/tests/D2Net.Scaffold.Tests/RelPathNormalizationTests.cs` covering `RelPath` normalization on Windows separators (forward-slash output even when input uses backslash) — guards the JSON contract on non-Windows hosts too
- [X] T040a [P] Add a perf test `tools/d2net/tests/D2Net.Scaffold.Tests/PerfBudgetTests.cs` that builds a synthetic fixture with 500 `.dart` files and 2,000 non-Dart files distributed across 100 directories (depths 1-4), runs `ScaffoldRunner` against a temp target, asserts wall-clock time is under 30 s on the test host, and tears down the fixture (covers SC-007 as an automated guardrail, independent of the real `glp_runtime` tree)
- [X] T041 [P] Run `dotnet format tools/d2net/D2Net.sln` to apply `.editorconfig` consistently across all files
- [X] T042 Run the full suite: `dotnet test tools/d2net/D2Net.sln` and `dotnet build tools/d2net/D2Net.sln -c Release` and confirm zero warnings, zero failing tests

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001 → T002 → T003 → T004 → T005 → T006; T007 and T008 can run any time after T002.
- **Foundational (Phase 2)**: depends on Setup; T009–T014 all parallel; T015 depends on T010, T013; T016 depends on T010, T015; T017 stand-alone after T010; T018 depends on T006.
- **User Story 1 (Phase 3)**: depends on Foundational. Tests (T019–T023) can be authored in parallel with one another and with the implementation tasks (T024–T029) — though they will fail until the implementation lands.
- **User Story 2 (Phase 4)**: depends on US1 (specifically T027, the initial `TrackerWriter`). T030 in parallel with T031/T032.
- **User Story 3 (Phase 5)**: depends on Foundational + US1 (CompanionFileWriter, FileCopier, ScaffoldRunner). T033/T034 in parallel; implementation T035 → T036/T037 (parallel) → T038.
- **Polish (Phase 6)**: depends on US1 + US2 + US3 if all three are in scope; T039–T041 parallel; T042 final gate.

### User Story Dependencies

- US1 is the MVP and blocks nothing else (depends only on Foundational).
- US2 strictly tightens the contract on the tracker that US1 already writes — code-level dep on `TrackerWriter`.
- US3 is fully independent of US2 (it only needs the fresh-mode pieces from US1).

### Within Each User Story

- Tests are written first and observed to fail before implementation lands.
- Models/types before services; services before the runner that orchestrates them; the runner before `Program.cs`.

### Parallel Opportunities

- T007, T008 (Setup polish) parallel with T003+ once the project exists.
- T009–T014 (foundational pure-data types) all parallel.
- T019–T023 (US1 tests) all parallel; T024–T026 (US1 leaf services) all parallel.
- T030 (US2 test) parallel with T031 (US2 model) if you write the test against the schema file directly.
- T033 and T034 (US3 tests) parallel; T036 and T037 (US3 supporting changes) parallel after T035.
- T033a (help/version test, US1) parallel with T029; T040a (perf budget test) parallel with T040 and T039.

---

## Parallel Example: User Story 1

```bash
# After Foundational (Phase 2) is green, launch all US1 tests in one go:
Task: "Pruned directories test in tools/d2net/tests/D2Net.Scaffold.Tests/PrunedDirectoriesTests.cs"
Task: ".dart.src rename test in tools/d2net/tests/D2Net.Scaffold.Tests/DartSrcRenameTests.cs"
Task: "Companion stub test in tools/d2net/tests/D2Net.Scaffold.Tests/CompanionStubTests.cs"
Task: "Pre-flight collision test in tools/d2net/tests/D2Net.Scaffold.Tests/PreflightCollisionTests.cs"
Task: "End-to-end fresh scaffold test in tools/d2net/tests/D2Net.Scaffold.Tests/ScaffoldFreshTests.cs"

# In parallel, the leaf services can be authored:
Task: "PreflightChecker in tools/d2net/src/D2Net.Scaffold/PreflightChecker.cs"
Task: "FileCopier in tools/d2net/src/D2Net.Scaffold/FileCopier.cs"
Task: "CompanionFileWriter in tools/d2net/src/D2Net.Scaffold/CompanionFileWriter.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 only)

1. Phase 1: Setup (T001–T008).
2. Phase 2: Foundational (T009–T018).
3. Phase 3: User Story 1 (T019–T029, plus T033a for the `--help`/`--version` test).
4. Stop and validate: run `dotnet test tools/d2net/D2Net.sln`; run a fresh scaffold against `glp_runtime` and inspect `glp_runtime_net` plus `d2net-tracker.json`.
5. **Demo / commit MVP** — the toolkit already does what the user originally asked for.

### Incremental Delivery

1. After MVP: complete Phase 4 (US2) → tracker is schema-validated → safer for downstream tools.
2. Then Phase 5 (US3) → re-run is now safe (refuse-by-default + `--refresh`).
3. Finally Phase 6 (Polish) → run quickstart end-to-end against real `glp_runtime`, format, full test sweep.

### Parallel Team Strategy

Two-developer split after Foundational completes:
- Developer A: Phase 3 (US1, MVP path).
- Developer B: Drafts Phase 5 tests (T033, T034) and Phase 6 polish in parallel; merges once US1 is in.

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks.
- The `.dart_tool`, `build`, `.git`, `.idea`, `.vscode` exclusion list is fixed by spec Q1; any tests should treat it as a closed set.
- Tracker filename `d2net-tracker.json` and status enum `{todo, in-progress, done, blocked}` are fixed by spec Q3; downstream consumers will assume these.
- Companion stubs MUST contain a `// TODO: d2net …` single-line comment (R5); empty stubs would break US1 acceptance scenario #2.
- Verify each test fails before its implementing task lands (write tests first, then implement, then watch them go green).
- Commit after each completed task or coherent group; never bundle US1 work into a US3 commit.
- Avoid: cross-story file edits that couple stories, vague task descriptions without file paths, shared in-memory state between `ScaffoldRunner` and `RefreshRunner`.
