# Implementation Plan: d2net-scaffold — Dart-to-.NET Conversion Scaffold

**Branch**: `001-d2net-scaffold` | **Date**: 2026-04-30 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-d2net-scaffold/spec.md`

## Summary

`d2net-scaffold` is the bootstrap step of the `d2net` (Dart-to-.NET) MVP code conversion toolkit. It walks the `glp_runtime` Dart source tree, mirrors its directory structure into `glp_runtime_net`, copies non-Dart files verbatim, preserves every `.dart` file as `<name>.dart.src`, generates nine stub companion files per Dart file (`.cs`, `.ana`, `.tst`, `.con`, `.dep`, `.cgn`, `.iss`, `.sta`, `.ver`), and writes a single `d2net-tracker.json` at the target root listing every Dart file with the per-companion status (initialised to `todo`). It excludes the `.dart_tool`, `build`, `.git`, `.idea`, `.vscode` directories, runs a pre-flight collision check before writing anything, and supports a `--refresh` flag that updates source-derived files while preserving in-progress companion edits and the tracker.

The toolkit is implemented in **C# on .NET 8** as a single console application (`dotnet run --project tools/d2net/src/D2Net.Scaffold`). C# is the eventual target language of the conversion effort, so dogfooding it for the toolkit itself reduces toolchain surface area and aligns the project ecosystem.

## Technical Context

**Language/Version**: C# 12 on .NET 8 (LTS)
**Primary Dependencies**: `System.CommandLine` (CLI parsing), `System.Text.Json` (tracker write). The stdout summary is written via `Console.Out` / `TextWriter` directly — no logging framework. No third-party packages outside the Microsoft ecosystem for the MVP.
**Storage**: Filesystem only — no database, no network. Source tree read; target tree + `d2net-tracker.json` written.
**Testing**: `xUnit` for unit & integration tests. Fixtures are tiny in-memory or temp-folder Dart-like trees built per test in `Path.GetTempPath()`; assertions diff against an expected target tree.
**Target Platform**: Cross-platform .NET 8 (developer workstations); primary host is Windows 11 (this repo) but the code uses `Path` and `Directory` APIs portably so Linux/macOS work.
**Project Type**: CLI tool (single console app inside a future `d2net` toolkit family).
**Performance Goals**: Complete a fresh scaffold of `glp_runtime` (≈few thousand files, low-hundreds of `.dart` files) in under 30 s on a typical workstation (per SC-007). The dominant cost is filesystem I/O; the algorithm is O(N) over source files.
**Constraints**: Atomicity on collision-failure (no partial output) is required by FR-012. The `--refresh` mode must never touch existing companion files or `d2net-tracker.json`. The tool is a one-shot CLI — no daemon, no persistent state outside the target tree.
**Scale/Scope**: One target tree per invocation. The current `glp_runtime` has on the order of a few hundred `.dart` files; extrapolation to similar repos is fine. No multi-repo or batch scenarios in the MVP.

## Constitution Check

The repository's `.specify/memory/constitution.md` contains only the unfilled `[PRINCIPLE_1_NAME]` … `[PRINCIPLE_5_NAME]` template placeholders — no project-specific gates have been ratified. There are therefore no constitution gates to evaluate. **Gate status: pass (vacuously)**.

A note for future sessions: when the constitution is populated, this section must be re-evaluated against any populated principles before re-running `/speckit-plan`.

## Project Structure

### Documentation (this feature)

```text
specs/001-d2net-scaffold/
├── plan.md              # This file (/speckit-plan command output)
├── spec.md              # Feature specification (already exists)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/
│   └── cli-contract.md  # CLI invocation contract
│   └── tracker-schema.json # JSON Schema for d2net-tracker.json
├── checklists/
│   └── requirements.md  # Spec quality checklist (already exists)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created here)
```

### Source Code (repository root)

```text
tools/
└── d2net/
    ├── D2Net.sln                              # Solution file (also future home for sibling tools)
    ├── src/
    │   └── D2Net.Scaffold/
    │       ├── D2Net.Scaffold.csproj          # net8.0, single executable
    │       ├── Program.cs                     # Entry point + System.CommandLine wiring
    │       ├── ScaffoldOptions.cs             # Parsed CLI options record
    │       ├── ScaffoldRunner.cs              # Orchestrator: pre-flight → walk → write → summary
    │       ├── PreflightChecker.cs            # FR-012 pre-flight collision detector
    │       ├── DirectoryWalker.cs             # Recursive walk with pruning (FR-002)
    │       ├── FileCopier.cs                  # Verbatim non-Dart copy (FR-003) + .dart.src copy (FR-004)
    │       ├── CompanionFileWriter.cs         # Nine-stub generator (FR-005, FR-006)
    │       ├── TrackerWriter.cs               # d2net-tracker.json writer (FR-007 – FR-010)
    │       ├── RefreshRunner.cs               # --refresh mode (FR-011)
    │       └── RunSummary.cs                  # Counts + stdout summary (FR-013)
    └── tests/
        └── D2Net.Scaffold.Tests/
            ├── D2Net.Scaffold.Tests.csproj    # net8.0, xunit
            ├── Fixtures/
            │   └── FixtureBuilder.cs          # Builds disposable temp source trees
            ├── ScaffoldFreshTests.cs          # End-to-end fresh scaffold: SC-001..SC-006
            ├── PreflightCollisionTests.cs     # FR-012, edge case: runner.dart + runner.cs
            ├── PrunedDirectoriesTests.cs      # FR-002 exclusion semantics
            ├── DartSrcRenameTests.cs          # FR-004, multi-dot filenames edge case
            ├── CompanionStubTests.cs          # FR-005, FR-006, comment format
            ├── TrackerSchemaTests.cs          # FR-007 – FR-010, valid JSON, status enum
            ├── RefreshModeTests.cs            # FR-011 override semantics, SC-008, SC-009
            └── ExitCodeTests.cs               # Non-zero on collision / target exists
```

`glp_runtime_net/` itself is NOT created or committed by this task — it is the *output* of running the tool. The tests build their own throwaway source trees and target trees in `Path.GetTempPath()`.

**Structure Decision**: Single-project layout at `tools/d2net/src/D2Net.Scaffold` with a sibling `tests/` project, both inside a single `D2Net.sln`. This carves out a `tools/` namespace at the repo root for future d2net-* siblings (a porter, a verifier, a status dashboard) without polluting the existing `glp_runtime` / `glp_multiagent` Dart layout. The toolkit is fully self-contained: it does not reference `glp_runtime` Dart code or the GLP runtime in any way — it just walks the filesystem.

## Complexity Tracking

No constitution violations to justify (constitution is unpopulated). No deviations from the simplest reasonable design.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | (n/a) | (n/a) |
