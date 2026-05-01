# Implementation Plan: D2NET.Scaffold — Source-Tree Mirror with Per-Dart-File Working Directories

**Branch**: `009-scaffold-mirror` | **Date**: 2026-05-01 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/009-scaffold-mirror/spec.md`

## Summary

Refactor the existing `d2net-scaffold` CLI tool. Remove its CLI-arg-based source/target/refresh interface entirely. The new tool reads source / target / extension / exclusions from the workspace at `.D2NET/`, walks the on-disk source tree skipping every excluded subtree, copies every non-excluded file (including `.dart`) into a target tree it creates at `<target>/`, and creates an empty sibling `__<basename>/` working directory next to every copied `.dart` file. The `dart_files` table gains two new columns (`target_parent_dir` with native separators absolute, `target_workdir_name` literal `__<basename>`) populated during the run. A workspace-DB tracker row marks the target tree as scaffold-managed; a non-semantic sentinel file inside the target tree provides operator visibility but no authority. All filesystem mutations happen in a sibling staging directory that is atomically renamed over the live target only after the database transaction commits. An explicit `--FORCE --DELETE-TARGET` flag pair allows the operator to authorise destruction of a non-scaffold-managed target, gated by an interactive confirmation prompt naming the absolute path.

## Technical Context

**Language/Version**: C# / .NET 8.0 (matches `tools/d2net/src/D2Net.Scaffold/D2Net.Scaffold.csproj`).
**Primary Dependencies**: Npgsql, `System.Text.Json`, `System.IO` (file copy + directory walk), the vendored Node.js `bridge-direct.mjs` PGLite subprocess. Reuses `WorkspaceLayout`, `BridgeOptions`, `DbConnectionStringBuilder`, `PgBridgeProcess`, `SettingsWriter.TryReadSnapshot`, `SettingsWriter.TryReadPersistedPort` from the D2Net.Init project.
**Storage**: PGLite at `.D2NET/pgdb/` via the per-invocation Node.js bridge. Workspace settings JSON at `.D2NET/D2NET-Settings.json` (read-only by this feature).
**Testing**: xUnit, mirroring 007/008's pattern. New test classes under `tools/d2net/tests/D2Net.Scaffold.Tests/`.
**Target Platform**: Windows + macOS + Linux. The new `target_parent_dir` column carries native separators per spec clarification — workspace state is host-OS-specific by design.
**Project Type**: CLI tool, single .NET project (`D2Net.Scaffold` already exists; this feature largely rewrites it).
**Performance Goals**: SC-001 — full scaffold round-trip (process start → bridge ready → tree walk → staged copy → DB transaction → atomic rename → process exit) under 60 seconds for 1,000 `.dart` + 5,000 non-`.dart` files. Bridge cold-start (~10s) and the file-copy IO dominate.
**Constraints**:
- Must read source / target / extension / exclusions from the workspace; no CLI args for these.
- Must not modify any phase row whose phase name is not `scaffold`.
- Must not break the existing `d2net-init` `--list` / `--Exclusions` / `--current-phase` semantics.
- Must produce `dart_files` rows with the two new columns populated (or auto-extend the schema if the columns don't exist yet).
- Must be idempotent (FR-010) and reconciliatory (FR-011).
- Must refuse non-scaffold-managed targets unless `--FORCE --DELETE-TARGET` is supplied with operator confirmation (FR-012, FR-012a).
- Must write to a staging sibling directory and rename atomically (research R-A4).
**Scale/Scope**: SC-001 ceiling 1,000 dart + 5,000 non-dart files. Realistic source tree (`glp_runtime` example) is ~130 dart files plus a few hundred non-dart files.

## Constitution Check

`.specify/memory/constitution.md` is the unfilled template. Universal sanity gates only:

| Gate | Status | Notes |
|---|---|---|
| Single project, simplest tree that works | PASS | All changes land in `tools/d2net/src/D2Net.Scaffold/` and `tools/d2net/tests/D2Net.Scaffold.Tests/`. Reuses Init-project helpers via project reference. |
| New mode uses the same CLI surface model as 007/008 | PASS | Workspace-driven; `--help`, `--version`, `--json`, plus the destructive `--FORCE --DELETE-TARGET` flag pair (interactive). |
| No silent contradiction with 002 / 005 / 007 / 008 | PASS | `dart_files` schema gains two additive columns (`ALTER TABLE ... ADD COLUMN IF NOT EXISTS`). Existing rows stay valid. Init / add-exclude / remove-exclude untouched. |
| Tests exist before merge | PASS (gate to enforce in Phase 2) | Plan reserves dedicated unit + integration test additions. |
| No backwards-compat shim creep | PASS | The prior `d2net-scaffold <source> <target>` CLI is removed. The refactor is a deliberate breaking change to that tool's CLI surface; `d2net-init`, `d2net-init --add-exclude`, `d2net-init --remove-exclude`, and `d2net-init --FORCE --DELETE-EXISTING` are unchanged. |
| Default-safe behaviour for ambiguous cases | PASS | Refuses non-scaffold-managed targets by default; `--FORCE --DELETE-TARGET` requires both the flag pair AND an interactive confirmation. |

**Result**: GATE PASSES. No violations to track in Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/009-scaffold-mirror/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   └── scaffold-cli-contract.md   # Phase 1 output
├── checklists/
│   └── requirements.md
├── HANDOFF.md           # Session-restart prep (Phase 2 output for cross-session implementation)
└── tasks.md             # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
tools/d2net/src/D2Net.Scaffold/
├── Program.cs                       # REWRITE. New ArgParser: --help, --version, --json, --FORCE --DELETE-TARGET.
│                                    #   Routes to ScaffoldRunner.
├── ExitCodes.cs                     # NEW. 7 distinct codes for FR-016 + the existing 0/1.
├── ScaffoldOptions.cs               # REWRITE. record { RepoRoot, Json, ForceDeleteTarget }. No source/target args.
├── ScaffoldRunner.cs                # NEW. End-to-end orchestrator: settings -> bridge -> snapshot -> walk
│                                    #   -> stage -> commit -> rename. Test seam mirroring 007/008.
├── TargetTreePlanner.cs             # NEW. Compares source-tree-snapshot vs db-tracker-inventory; produces
│                                    #   add-set, remove-set, dart-file-set with target paths and __workdir names.
├── StagingMutator.cs                # NEW. Writes the entire scaffold output into <target>.d2net-tmp/.
│                                    #   Knows how to copy files, create __ working dirs, write the sentinel file.
├── ScaffoldDbWriter.cs              # NEW. Single-transaction Postgres updates: ALTER TABLE add columns
│                                    #   if missing; UPDATE dart_files SET target_parent_dir, target_workdir_name;
│                                    #   UPSERT scaffold_tracker / setting row.
├── DestructiveTargetGate.cs         # NEW. Implements --FORCE --DELETE-TARGET interactive confirmation per FR-012a.
├── DirectoryWalker.cs               # MAY REUSE / refactor. Honours exclusion-aware walk semantics.
├── FileCopier.cs                    # MAY REUSE / refactor. Verbatim file copy with progress.
├── PreflightChecker.cs              # MAY REUSE / refactor. Workspace exists, source dir exists, etc.
├── RunSummary.cs                    # MAY REUSE / refactor. Text + JSON output formatter.
├── CompanionFileWriter.cs           # DELETE (or archive). Prior tool's per-dart-file companion-file logic
│                                    #   is replaced by the empty __<basename>/ directory model.
├── RefreshRunner.cs                 # DELETE (or archive). Refresh mode is subsumed by FR-010/FR-011.
└── (other existing files)           # Audit; keep what aligns with the new model, remove what doesn't.

tools/d2net/tests/D2Net.Scaffold.Tests/
├── ScaffoldArgParserTests.cs               # NEW. Flag parsing, conflicts, --FORCE --DELETE-TARGET pair.
├── ScaffoldPreflightTests.cs               # NEW. Workspace-missing, source-missing, etc.
├── ScaffoldHappyPathTests.cs               # NEW. Single-tree scaffold round-trip; __workdir creation.
├── ScaffoldIdempotencyTests.cs             # NEW. Re-run with no changes -> no-op.
├── ScaffoldReconciliationTests.cs          # NEW. Add/remove exclusion -> target tree reconciles.
├── ScaffoldDestructiveOverrideTests.cs     # NEW. --FORCE --DELETE-TARGET + interactive confirmation.
├── ScaffoldCollisionTests.cs               # NEW. __workdir collision rejection.
├── ScaffoldAtomicityTests.cs               # NEW. Mid-run failure leaves target byte-identical.
├── ScaffoldPhaseInvarianceTests.cs         # NEW. Only `scaffold` phase row touched.
└── (existing tests)                        # Audit; remove tests for deleted code paths.
```

**Structure Decision**: The refactor lives in the existing `D2Net.Scaffold` project (no new csproj). Helpers from `D2Net.Init` are reused via project reference (already present per the existing solution layout). Some existing files (`CompanionFileWriter`, `RefreshRunner`) are obsoleted by the new model and should be removed during implementation; archive into a `.archive/` folder if reviewers prefer to keep the prior code visible.

## Phase 0: Research (deferred to research.md)

Five implementation questions to resolve in research.md:

1. **Schema-migration mechanism**: how does scaffold add the two new `dart_files` columns on first run without breaking existing init flows? `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` inside the same transaction as the row updates is the natural choice (PGLite supports this).
2. **Tracker storage shape**: a single `setting` row with key `scaffold.target_inventory_hash` (and value = a hash of the inventory) vs a small `scaffold_tracker` table (one row per copied source path). The `setting` row is simpler; the table gives finer-grained reconciliation.
3. **Source-tree snapshot mechanism**: re-walk the source tree on every scaffold invocation (always-fresh snapshot) vs read the snapshot from the `dart_files` rows that init / add-exclude / remove-exclude have been maintaining (always-stale-by-one-second). The former is correct-by-construction; the latter is faster but risks divergence if files were added/removed since the last init or *-exclude run.
4. **`__<basename>` collision detection ordering**: pre-walk (cheap; before any copy) vs at-write-time (during the staging copy). Pre-walk is correct and fail-fast.
5. **Sentinel file content**: empty / minimal / hash-of-tracker-row. Empty is simplest and matches the "non-semantic" intent.

## Phase 1: Design & Contracts (deferred to data-model.md, contracts/, quickstart.md)

- `data-model.md`: enumerate the entities and tables touched, the SQL behaviour for the transaction (`ALTER TABLE ... ADD COLUMN IF NOT EXISTS` once; `UPDATE dart_files`; UPSERT tracker row), the JSON projection of new columns into `--list --json` output, and the read-modify-write sequence (snapshot → plan → stage → commit → rename → cleanup).
- `contracts/scaffold-cli-contract.md`: the CLI surface contract. Flags, positional-args-not-allowed, exit codes (with concrete numeric assignments TBD in research R3-equivalent), text and JSON output formats, the `--help` block, and the `--FORCE --DELETE-TARGET` interactive prompt format.
- `quickstart.md`: a one-page operator guide covering the canonical scaffold flow, the `--FORCE --DELETE-TARGET` override, and the `--json` output for skill consumption.

After Phase 1, the agent context file (`CLAUDE.md`) is updated to point at this plan inside the `<!-- SPECKIT START --> ... <!-- SPECKIT END -->` markers.

## Complexity Tracking

> No Constitution-gate violations.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| (none) | | |
