# Implementation Plan: D2NET.Init — Non-Destructive Incremental Exclusion Updates

**Branch**: `007-incremental-exclusions` | **Date**: 2026-05-01 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/007-incremental-exclusions/spec.md`

## Summary

Amend the existing `d2net-init` CLI tool to add a new repeatable `--add-exclude <path>` flag that mutates an already-initialised workspace: it records the new directory exclusions in `D2NET-Settings.json` and the `excluded_directories` PGLite table, and it removes every `dart_files` row whose `full_path` falls under any newly excluded directory. The mutation is one all-or-nothing transaction. Phase tables are never touched. Concurrent invocations fail fast with a distinct contention exit code. The feature reuses the existing per-invocation PGLite bridge subprocess (`bridge-direct.mjs`) rather than introducing any new storage path or migration.

## Technical Context

**Language/Version**: C# / .NET 8.0 (matches existing `tools/d2net/src/D2Net.Init/D2Net.Init.csproj`)
**Primary Dependencies**: Npgsql (Postgres wire-protocol client; already in use), `System.Text.Json` for `D2NET-Settings.json` IO, vendored Node.js `bridge-direct.mjs` for the PGLite subprocess (no version change)
**Storage**: PGLite (WASM-backed single-user Postgres) at `.D2NET/pgdb/`, accessed via the per-invocation Node.js subprocess on a local TCP port. Settings JSON at `.D2NET/D2NET-Settings.json` is the JSON projection of the same state.
**Testing**: xUnit (matches `tools/d2net/tests/D2Net.Init.Tests/` layout). New tests added under that project. Integration tests use the real bridge subprocess; isolated tests can mock the `INpgsqlConnection` boundary.
**Target Platform**: Windows + macOS + Linux (the binary is Windows-first per current operator workflow but the test suite must remain cross-platform; runtime requires Node.js ≥ 20 on PATH for the bridge).
**Project Type**: CLI tool (single .NET project, no client/server split; mirrors the existing `d2net-init` shape).
**Performance Goals**: SC-001 — full add-exclude round-trip (process start → bridge ready → settings rewrite → DB transaction → process exit) completes in under 2 seconds on a developer workstation for workspaces with up to 10,000 `dart_files` rows. The bridge cold-start (~5–10 s ceiling per feature 005's `D2NET_BRIDGE_READY_TIMEOUT_SECONDS`) dominates and is the same order as existing inspection commands.
**Constraints**: must be transactional across two storage targets (JSON file + Postgres DB); must not modify `phase_sequence` or `phase_status` rows nor their `last_updated` timestamps; must not break the existing `--list` / `--Exclusions` / `--current-phase` semantics; must not change init-mode behaviour at all.
**Scale/Scope**: SC-001 ceiling of 10,000 `dart_files` rows. A single add-exclude invocation may carry an unbounded number of paths but the realistic skill-driven flow is 1–10 paths per call.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

**Constitution status**: `.specify/memory/constitution.md` is the unfilled template (no project-specific principles ratified). Apply only the universal sanity gates:

| Gate | Status | Notes |
|---|---|---|
| Single project, simplest tree that works | PASS | All changes land in the existing `tools/d2net/src/D2Net.Init/` C# project; no new project, no new top-level directory. |
| New mode uses the same CLI surface model as existing modes | PASS | Repeatable flag form (`--add-exclude`) was confirmed in clarification 2026-05-01 and parallels `--exclude`, `--list`, `--Exclusions`, `--current-phase`. No subcommand grammar introduced. |
| No silent contradiction with prior 002 / 005 specs | PASS | Storage engine is PGLite-via-bridge (feature 005). Schema (`excluded_directories.kind`, `dart_files.full_path UNIQUE`) is honoured. SQLite era is out of scope. |
| Tests exist before merge | PASS (gate to enforce in Phase 2) | Plan reserves dedicated unit + integration test additions in `D2Net.Init.Tests`. |
| No backwards-compatibility shim creep | PASS | The new flag is additive only. Init-mode `--exclude` is not changed. The legacy SQLite-era detection path (`WorkspaceLayout.LegacySqliteFileName`) is read-only and untouched. |

**Result**: GATE PASSES. No violations to track in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/007-incremental-exclusions/
├── plan.md              # This file
├── spec.md              # Feature specification (already written)
├── research.md          # Phase 0 output (this command)
├── data-model.md        # Phase 1 output (this command)
├── quickstart.md        # Phase 1 output (this command)
├── contracts/
│   └── add-exclude-cli-contract.md   # Phase 1 output
├── checklists/
│   └── requirements.md  # Spec quality checklist (already written)
└── tasks.md             # Phase 2 output (/speckit-tasks command)
```

### Source Code (repository root)

```text
tools/d2net/src/D2Net.Init/
├── Program.cs                       # ArgParser: add `--add-exclude` flag handling and AddExcludeMode parse result
├── ExitCodes.cs                     # Add 5 new constants (path-outside-source, settings-write-failed,
│                                    #   db-write-failed, lock-contention, path-is-file). Reuse code 6
│                                    #   (WorkspaceMissingForInspection) for workspace-missing.
├── AddExcludeOptions.cs             # NEW. Parsed --add-exclude inputs + flags.
├── AddExcludeRunner.cs              # NEW. End-to-end add-exclude flow.
├── PathValidator.cs                 # NEW. Source-relative path validation, file-vs-dir detection,
│                                    #   path canonicalisation, redundancy classification.
├── ExclusionMutator.cs              # NEW. Single-transaction Postgres updates: insert into
│                                    #   excluded_directories, prefix-delete from dart_files.
├── SettingsWriter.cs                # MODIFY. Add UpdateExcludedDirectories(...) for in-place rewrite
│                                    #   while preserving every other settings field.
├── PgBridgeProcess.cs               # READ-ONLY. Reused as-is.
└── (other existing files)           # Untouched.

tools/d2net/tests/D2Net.Init.Tests/
├── AddExcludeArgParserTests.cs      # NEW. Flag parsing, positional rejection, multiple flags, --json combo.
├── AddExcludePathValidatorTests.cs  # NEW. Outside-source rejection, file-vs-dir, redundancy under existing.
├── AddExcludeRunnerTests.cs         # NEW. End-to-end with bridge: DB rows added/removed, JSON output, exit codes.
├── AddExcludeAtomicityTests.cs      # NEW. Mid-write failure leaves workspace bit-identical.
├── AddExcludeContentionTests.cs     # NEW. Two parallel invocations: loser exits with contention code.
├── AddExcludePhaseInvarianceTests.cs # NEW. phase_sequence + phase_status untouched after add-exclude.
└── (other existing test classes)    # Untouched.
```

**Structure Decision**: This feature is a pure additive change inside the existing `tools/d2net/src/D2Net.Init/` single-project layout. No new csproj, no new top-level directory, no schema migration. The new files isolate add-exclude logic so the diff is reviewable; the existing `Program.cs`, `ExitCodes.cs`, and `SettingsWriter.cs` see localised edits.

## Phase 0: Research (deferred to research.md)

The spec's three deliberately deferred decisions, plus three implementation questions surfaced by reading the existing source, are resolved in `research.md`:

1. Within-batch path subsumption: when one supplied path is an ancestor of another supplied path in the same invocation, what is the redundancy semantics?
2. File-vs-directory detection: stat-based, suffix-based, or both?
3. Exit code numbering: which integer values for the five new error conditions?
4. Settings JSON atomicity: write-temp-then-rename, in-place fsync-write, or rollback-via-snapshot?
5. Concurrent-invocation lock detection mechanism: bridge-port-in-use, PGLite data-dir lock failure, or .NET file lock on settings.json?
6. The `excluded_directories.kind` value used by add-exclude: `'manual'`, `'pattern'`, or a new value?

Each of these is resolved with a Decision / Rationale / Alternatives entry in `research.md`. After Phase 0, no `NEEDS CLARIFICATION` markers remain.

## Phase 1: Design & Contracts (deferred to data-model.md, contracts/, quickstart.md)

- `data-model.md`: enumerate the entities and tables touched, with the exact PGLite SQL behaviour for inserts and prefix-deletes, the JSON projection rules for `D2NET-Settings.json.excluded_directories`, and the read-modify-write sequence.
- `contracts/add-exclude-cli-contract.md`: the CLI surface contract for `--add-exclude`, including flag parsing rules, exit codes, stdout/stderr formats (text + `--json`), and the `--help` block to be added to `Program.cs`.
- `quickstart.md`: a one-page operator guide showing the canonical add-exclude invocations from a developer workstation, including the skill-driven batch flow.

After Phase 1, the agent context file (`CLAUDE.md`) is updated to point at this plan inside the `<!-- SPECKIT START --> ... <!-- SPECKIT END -->` markers.

## Complexity Tracking

> No Constitution-gate violations. This section intentionally left empty.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | | |
