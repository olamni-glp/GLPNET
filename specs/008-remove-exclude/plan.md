# Implementation Plan: D2NET.Init — Non-Destructive Exclusion Removal (`--remove-exclude`)

**Branch**: `008-remove-exclude` | **Date**: 2026-05-01 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/008-remove-exclude/spec.md`

## Summary

Amend the existing `d2net-init` CLI tool with the inverse of feature 007's `--add-exclude`: a new repeatable `--remove-exclude <path>` flag that mutates an already-initialised workspace by deleting matching rows from `excluded_directories`, walking the on-disk source tree under each removed path, and inserting one `dart_files` row per `.dart` file found whose path is not still covered by a surviving ancestor exclusion. The mutation is one all-or-nothing transaction. Phase tables are never touched. Concurrent invocations fail fast with the same lock-contention exit code as 007. Non-manual rows (init-time `'tool'` and `'pattern'` exclusions) are protected by default and require an explicit `--allow-system-exclusions` override flag (clarification 2026-05-01).

The feature reuses every piece of infrastructure shipped in 007: `WorkspaceLayout`, `PathValidator`, `SettingsWriter.PrepareTempSettingsWithExclusions`/`CommitTempFile`, the bridge subprocess, the temp-then-rename atomicity pattern, and the lock-contention detection. The diff is dominated by one new runner (`RemoveExcludeRunner.cs`), one new mutator (inverse of `ExclusionMutator`), and a small parser extension.

## Technical Context

**Language/Version**: C# / .NET 8.0 (matches `tools/d2net/src/D2Net.Init/D2Net.Init.csproj`).
**Primary Dependencies**: Npgsql (already in use), `System.Text.Json` for settings IO, vendored Node.js `bridge-direct.mjs` for the PGLite subprocess (no version change). `PathValidator`, `SettingsWriter`, `PgBridgeProcess`, `BridgeOptions`, and `DartFileScanner` from the existing project are reused.
**Storage**: PGLite at `.D2NET/pgdb/`, accessed via the per-invocation Node.js bridge subprocess. `D2NET-Settings.json` is the JSON projection of the same state.
**Testing**: xUnit, mirroring 007's pattern. New test classes under `tools/d2net/tests/D2Net.Init.Tests/`.
**Target Platform**: Windows + macOS + Linux (cross-platform tests required).
**Project Type**: CLI tool, single .NET project.
**Performance Goals**: SC-001 — full remove-exclude round-trip (process start → bridge ready → settings rewrite → DB transaction → re-index → process exit) under 15 seconds for a removed exclusion that covers up to 1,000 `.dart` files. The bridge cold-start dominates; the additional cost beyond `--add-exclude` is the file-walk and `dart_files` inserts.
**Constraints**: must be transactional across two storage targets (JSON file + Postgres DB); must not modify `phase_sequence` or `phase_status`; must not break the existing `--list` / `--Exclusions` / `--current-phase` / `--add-exclude` semantics; must not change init-mode or `--FORCE --DELETE-EXISTING` behaviour; non-manual rows MUST be refused without the explicit override flag.
**Scale/Scope**: SC-001 ceiling of 1,000 `.dart` files re-indexed per invocation. A single invocation may carry an unbounded number of paths; realistic skill-driven flow is 1–10 paths.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

`.specify/memory/constitution.md` is the unfilled template. Universal sanity gates only:

| Gate | Status | Notes |
|---|---|---|
| Single project, simplest tree that works | PASS | All changes land in `tools/d2net/src/D2Net.Init/` and `tools/d2net/tests/D2Net.Init.Tests/`. No new project. |
| New mode uses the same CLI surface model as 007 | PASS | Repeatable flag form; mutually exclusive with init/inspection/add-exclude. New `--allow-system-exclusions` is a binary safety flag. |
| No silent contradiction with 002 / 005 / 007 | PASS | Storage engine is PGLite-via-bridge (005); schema honoured (007); init-mode `--exclude`, `--add-exclude`, and the `--FORCE --DELETE-EXISTING` rebuild path remain unchanged. |
| Tests exist before merge | PASS (gate to enforce in Phase 2) | Plan reserves dedicated unit + integration test additions. |
| No backwards-compat shim creep | PASS | Additive feature only. |
| Default-safe behaviour for ambiguous cases | PASS | `--allow-system-exclusions` is opt-in; the safe default refuses non-manual rows (clarification 2026-05-01). |

**Result**: GATE PASSES.

## Project Structure

### Documentation (this feature)

```text
specs/008-remove-exclude/
├── plan.md              # This file
├── spec.md              # Feature specification (already written)
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── remove-exclude-cli-contract.md   # Phase 1 output
├── checklists/
│   └── requirements.md  # Spec quality checklist (already written)
└── tasks.md             # Phase 2 output (/speckit-tasks command)
```

### Source Code (repository root)

```text
tools/d2net/src/D2Net.Init/
├── Program.cs                       # ArgParser: add `--remove-exclude` and `--allow-system-exclusions` flags;
│                                    #   add `ParsedCli.RemoveExcludeMode` case; route to RemoveExcludeRunner.
├── ExitCodes.cs                     # Add 5 new constants (path-outside-source = 17, settings-write-failed = 18,
│                                    #   db-write-failed = 19, lock-contention = 20, system-exclusion-refused = 21).
├── RemoveExcludeOptions.cs          # NEW. Parsed --remove-exclude inputs + AllowSystemExclusions flag.
├── RemoveExcludeRunner.cs           # NEW. End-to-end remove-exclude flow.
├── ExclusionRemover.cs              # NEW. Single-transaction Postgres updates: DELETE from excluded_directories,
│                                    #   file-walk + INSERT into dart_files for non-ancestor-covered paths.
├── PathValidator.cs                 # READ-ONLY. Reuse Canonicalise / ResolveUnderSource / IsUnder / etc.
├── SettingsWriter.cs                # READ-ONLY. Reuse PrepareTempSettingsWithExclusions / CommitTempFile.
├── DartFileScanner.cs               # READ-ONLY. Reuse for the on-disk file walk under removed exclusions.
├── PgBridgeProcess.cs               # READ-ONLY. Reused as-is.
└── (other existing files)           # Untouched.

tools/d2net/tests/D2Net.Init.Tests/
├── RemoveExcludeArgParserTests.cs           # NEW. Flag parsing, conflicts, --allow-system-exclusions, --json.
├── RemoveExcludePathRejectionTests.cs       # NEW. Outside-source, file-vs-dir, no-workspace.
├── RemoveExcludeRunnerTests.cs              # NEW. Single-path & multi-path round-trips, --json shape.
├── RemoveExcludeAncestorSurvivalTests.cs    # NEW. Removing a child while the ancestor still excludes; 0 inserts.
├── RemoveExcludeSystemKindTests.cs          # NEW. Refuse non-manual default; allow with override flag.
├── RemoveExcludePhaseInvarianceTests.cs     # NEW. phase_sequence + phase_status untouched.
├── RemoveExcludeContentionTests.cs          # NEW. Fake-bridge lock-contention exit 20.
└── (other existing test classes)            # Untouched.
```

**Structure Decision**: pure additive change inside the existing C# project, mirroring 007's footprint. New files isolate remove-exclude logic so the diff is reviewable; the existing `Program.cs` and `ExitCodes.cs` see localised edits only.

## Phase 0: Research (deferred to research.md)

Three implementation questions to resolve in research.md:

1. **File-walk implementation**: reuse `DartFileScanner` from init, or write a new walker? Walking semantics (symlink-following, junction handling, `.dart_tool/` skipping) must produce rows byte-identical to those init would produce, so reusing `DartFileScanner` is the conservative choice.
2. **Ancestor-survival check ordering**: when do we decide "this path is covered by a surviving ancestor and skip the walk"? Pre-walk (cheap; uses the read-modify-write snapshot) vs post-walk (always walks; INSERT ON CONFLICT does the dedup). Pre-walk is correct and avoids wasted IO on potentially huge trees.
3. **Kind-aware preflight**: a single `SELECT path, kind FROM excluded_directories WHERE path = ANY(@paths)` before the transaction is sufficient. The result feeds (a) FR-004a kind-validation, (b) FR-009 not-currently-excluded classification, and (c) FR-006 ancestor-survival classification.

## Phase 1: Design & Contracts (deferred to data-model.md, contracts/, quickstart.md)

- `data-model.md`: enumerate the entities and tables touched, the SQL behaviour for the transaction (DELETE excluded_directories, INSERT dart_files ON CONFLICT DO NOTHING), the JSON projection update, and the read-modify-write sequence (snapshot → kind+ancestor preflight → walk → transaction → rename).
- `contracts/remove-exclude-cli-contract.md`: the CLI surface contract for `--remove-exclude` and `--allow-system-exclusions`, including flag parsing rules, exit codes (17–21), text and JSON output formats, and the `--help` block addition.
- `quickstart.md`: a one-page operator guide covering single-path removal, multi-path removal, the `--allow-system-exclusions` override, the ancestor-survival case, and the `not-currently-excluded` no-op case.

After Phase 1, the agent context file (`CLAUDE.md`) is updated to point at this plan inside the `<!-- SPECKIT START --> ... <!-- SPECKIT END -->` markers.

## Complexity Tracking

> No Constitution-gate violations.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | | |
