# Session-Restart Handoff for `/speckit-implement` of feature 009

**Created**: 2026-05-01
**Author**: prior Claude Code session that completed `/speckit-specify` → `/speckit-clarify` → `/speckit-plan` → `/speckit-tasks` → `/speckit-analyze` (suggest+apply) and is now handing off before the long-running `/speckit-implement`.

This document is meant to be read at the start of the next session in lieu of re-deriving everything from the conversation transcript. It is self-contained.

---

## TL;DR — what to do next

1. Confirm you are on branch `009-scaffold-mirror` (`git branch --show-current` should print exactly that).
2. Run `/speckit-implement` with no arguments. The skill will read `specs/009-scaffold-mirror/tasks.md` and execute T001 → T041 in order.
3. Before T001's baseline build, also re-read this HANDOFF.md so you carry forward the critical context the prior session learned.
4. After implementation completes and tests pass, commit and push 009-scaffold-mirror. The merge into main + CalVer tag is the operator's call (Gabi); see "End-of-task playbook" at the bottom.

---

## Repo state at handoff

- **Branch**: `009-scaffold-mirror` (pushed to `origin/009-scaffold-mirror`).
- **HEAD commit on 009**: latest is `205cbc61` ("spec(009): D2NET.Scaffold source-tree mirror with per-dart working dirs (post-clarify)") plus subsequent plan/research/data-model/contracts/quickstart/tasks/HANDOFF commits made by the prior session.
- **`main` tip**: `4ed72f85` ("D2NET.Init: --remove-exclude with --allow-system-exclusions safety override"). Already tagged as `v2026.05.01` and released on GitHub. Feature 009 has NOT been merged to main yet.
- **Open follow-up branches**: 007-incremental-exclusions and 008-remove-exclude are pushed but already merged into main (linear history; their commits are part of `4ed72f85`).
- **Live workspace at `.D2NET/`**: cleanly re-initialised at 2026-05-01T06:50:41Z. 8 auto-detected exclusions (5 `tool` + 3 `pattern`). 128 `dart_files` rows. Phase tables empty. Settings: `source_dir=glp_runtime`, `target_dir=glp_runtime_net`, `target_extension=_net`, `connection.port=54400`. The earlier-session "wrongly-applied test/programs exclusion" episode is fully resolved.

## Test baseline at handoff

Run T001 in `tasks.md` first. Expected pre-implementation green numbers:

- `D2Net.Init.Tests`: **157/157 passing** (89 baseline + 41 from feature 007 + 27 from feature 008).
- `D2Net.Scaffold.Tests`: **33/34 passing** with 1 known flaky test (`PerfBudgetTests.Scaffold_500DartFiles_2000NonDartFiles_100Dirs_CompletesUnder30Seconds` runs 31s vs 30s budget on cold cache — pre-existing flake unrelated to 009; do NOT chase it). Note that several Scaffold test classes will become OBSOLETE during this refactor and are explicitly removed by T036 — the count after implementation will look different.

If T001 shows anything other than 157 D2Net.Init tests passing, STOP and report — that means something regressed since the prior session's last verification. Do not proceed to feature work until baseline is green.

## What feature 009 is, in one paragraph

Refactor the existing `d2net-scaffold` CLI tool. Its prior `<source> <target>` positional-args interface and `--refresh` mode are removed. The new tool reads source / target / extension / exclusions from the workspace at `<cwd>/.D2NET/` (settings JSON + `excluded_directories` table). It walks the source tree, skips every excluded subtree, and stages a complete target tree at `<target>.d2net-tmp/`: every non-excluded file is copied verbatim, AND for every `.dart` file an empty sibling `__<basename>/` directory is created. The workspace database gets two new `dart_files` columns (`target_parent_dir` native-separator absolute, `target_workdir_name` literal `__<basename>`) populated during the run, and a new `scaffold_tracker` table tracking what's been scaffolded for FR-010/FR-011 idempotency + reconciliation. After the DB transaction commits, the staging directory is atomically renamed over the live target. An explicit `--FORCE --DELETE-TARGET` flag pair allows the operator to authorise destruction of a non-scaffold-managed pre-existing target — gated by an interactive confirmation prompt naming the absolute path.

## Key files in `specs/009-scaffold-mirror/`

| File | Purpose | Read-priority for next session |
|---|---|---|
| `spec.md` | Feature behaviour. Authoritative. Read first. | **MUST READ** |
| `plan.md` | Tech context, project structure, phase pointers. | **MUST READ** |
| `research.md` | 7 design decisions resolved (R1–R7). Read R6 (exit codes) and R7 (phase semantics) before coding. | **MUST READ** |
| `data-model.md` | DB schema additions, read-modify-write sequence. | **MUST READ** |
| `contracts/scaffold-cli-contract.md` | CLI surface, exit codes, JSON shape, `--help` text. | **MUST READ** before T010/T011/T021 |
| `quickstart.md` | Operator guide. Useful for sanity-checking T038 smoke. | Read for T038 |
| `checklists/requirements.md` | Spec quality checklist (already passed). | Skim |
| `tasks.md` | 41 ordered tasks across 6 phases. **The implementation execution plan.** | **DRIVES IMPLEMENTATION** |
| `HANDOFF.md` | This file. | You're reading it |

## Critical context the prior session learned (don't repeat the discoveries)

1. **Storage is PGLite, not SQLite.** Feature 005 swapped the workspace DB to PGLite WASM via a per-invocation Node.js `bridge-direct.mjs` subprocess. The legacy SQLite layout from feature 002 is detected by `WorkspaceLayout.LegacySqliteFileName` and is NOT in play here. Don't introduce SQLite dependencies. Use Npgsql against the bridge's local TCP port. See `tools/d2net/src/D2Net.Init/PgBridgeProcess.cs`, `BridgeOptions.cs`, `DbConnectionStringBuilder.cs`.

2. **Reuse, don't rewrite, the Init helpers.** Features 007 and 008 already shipped `PathValidator`, the snapshot-and-rename pattern in `SettingsWriter` (`PrepareTempSettingsWithExclusions`, `CommitTempFile`, `TryReadSnapshot`, `TryReadPersistedPort`), and `DartFileScanner` (which already honours exclusion-aware walk semantics). Reuse all of these via project reference from `D2Net.Scaffold.csproj`. Do NOT re-implement walking, path canonicalisation, or settings IO.

3. **Bridge cold-start can exceed the production 15s budget on cold caches.** Tests under `D2Net.Init.Tests` set `D2NET_BRIDGE_READY_TIMEOUT_SECONDS=60` via a `ModuleInitializer` in `AssemblyAttributes.cs`. Mirror this for `D2Net.Scaffold.Tests`. The first scaffold smoke test (T038) on the live workspace may benefit from a one-shot `$env:D2NET_BRIDGE_READY_TIMEOUT_SECONDS = "60"` if the prior session's experience repeats.

4. **The lock-contention exit code is detected via the `BRIDGE_ERROR` payload.** `AddExcludeRunner.MapBridgeStartFailure` and `RemoveExcludeRunner.MapBridgeStartFailure` both inspect `BridgeStartException.Bridge?.LastBridgeError` against a substring set: `["EBUSY", "EACCES", "data directory in use", "could not lock", "another process"]`. Match → lock-contention exit code. Replicate this pattern in `ScaffoldRunner` and map to exit 28.

5. **Phase-row discipline.** `phase_sequence` and `phase_status` are owned per-phase. Scaffold owns ONLY the row whose `phase = 'scaffold'`. Static SQL audit (T034) is required to prove no other phase rows are touched. Mirror feature 007's T020 audit pattern.

6. **Tests are slow because they spawn the real bridge.** Each integration test takes ~10s of bridge cold-start. The full `D2Net.Init.Tests` suite ran for ~6m25s in the prior session. Plan for the 009 implementation test runs to take 5–15 minutes total. Run them in the background (Bash `run_in_background: true`) and continue with other work while waiting.

7. **The skill contract for `/D2NET-init` (`.claude/skills/D2NET-init/`) has NOT been amended to know about `--add-exclude` (007), `--remove-exclude` (008), or `d2net-scaffold` (009).** Operators who want to use these go via direct-binary invocation. The skill amendment is a separate spec track, not part of 009.

8. **CLAUDE.md project instructions still speak of GLP / Dart / SRSW / WAM** — that's the broader glpnet context. For 009 we're working only in the `tools/d2net/` C# subproject. Don't be distracted by the GLP language sections of CLAUDE.md while implementing 009.

## Open implementation decisions deliberately deferred to the implementer

These are documented in `research.md` or `plan.md` but call them out:

- **Tracker table column on Windows for case-insensitive comparison**: `scaffold_tracker.source_path` and `target_parent_dir` are TEXT. SQL comparisons are case-sensitive by default. On Windows the filesystem is case-insensitive. If a developer renames a directory only changing case, the tracker rows may not match. Decide during implementation whether to normalise to lowercase on insert OR use `ILIKE` / `LOWER()` comparisons OR ignore (defer to a future cleanup). Ignore is the simplest and probably correct default for Dart codebases.

- **Atomic-rename on Windows when the live target exists**: `Directory.Move` does not overwrite. The implementation needs a 3-step "rename old aside → rename staging in → delete old" sequence. Spec FR-014 has been softened to acknowledge a post-COMMIT rename-failure window (analogous to 007/008's exit 13 / 18). Implementation choice: which order, and whether to use `FileSystem.MoveDirectory` from `Microsoft.VisualBasic.FileIO` (cleaner) or hand-roll. Prior session left this open; either is acceptable.

- **Sentinel file timestamp**: empty file with no content. Whether to set its `LastWriteTime` to anything specific is undefined; the OS default is fine.

## Execution playbook for the next session

1. **Initialise**: read CLAUDE.md (mandatory), read this HANDOFF.md, read `specs/009-scaffold-mirror/{spec,plan,research,data-model,tasks}.md` and `contracts/scaffold-cli-contract.md`. Skip the docs/DISCIPLINE.md / typed-glp-manual.md / glp-cheat-sheet.md mandatory readings — those are for GLP-language work; 009 is C# tooling.

2. **Confirm baseline** (T001): `dotnet build tools/d2net/D2Net.sln -c Debug` then `dotnet test tools/d2net/D2Net.sln -c Debug` (run in background — takes 6–15 min). Expect 157/157 D2Net.Init green, 33/34 Scaffold green (1 flaky perf test acceptable).

3. **Audit** (T002 / T003): identify obsoleted source/test files in D2Net.Scaffold.

4. **Foundation** (T004–T012): exit codes → options → destructive gate → planner → mutator → DB writer → Program rewrite → runner → summary. Most are independent (T004/T005/T006 in parallel) but T011 depends on everything else.

5. **US1 MVP** (T013–T021): tests first (T013/T014/T015 in parallel — they're all in different files), then impl T016–T021 mostly sequential against `ScaffoldRunner`. Verify: `dotnet test --filter "FullyQualifiedName~ScaffoldHappyPath"` should turn green incrementally.

6. **US2 reconciliation** (T022–T025): test, impl, verify.

7. **US3 destructive override + collisions + atomicity** (T026–T034): tests in parallel, impl sequential. The `--FORCE --DELETE-TARGET` interactive flow is the trickiest piece — read FR-012a and the contract carefully.

8. **Polish** (T035–T041): obsolete file removal (T035/T036), full suite re-run (T037), live smoke (T038), `--help` audit (T039), commit (T041).

9. **End-of-task playbook**:
   - Push the branch: `git push origin 009-scaffold-mirror`.
   - Offer the merge template:
     ```
     cd D:\BSTDEV\RESEARCH\glp\glpnet
     git checkout main
     git pull origin main
     git fetch origin 009-scaffold-mirror
     git merge -m "Merge 009-scaffold-mirror into main" origin/009-scaffold-mirror
     git push origin main
     ```
   - Wait for Gabi to instruct on merge / tag. Today's date may be 2026-05-01 still (same-day release: `v2026.05.01-2`) or a later date.
   - Do NOT auto-merge, auto-push to main, or auto-tag without explicit instruction.

## What the next session MUST NOT do

- Do not modify `.D2NET/D2NET-Settings.json` directly. Only the binary writes settings.
- Do not invent CLI flags beyond what the contract enumerates.
- Do not change `D2Net.Init`'s behaviour. Feature 009 is additive on the workspace-DB side and Scaffold-binary-only on the source side.
- Do not skip pre-existing tests' green requirement. If a 007/008 test fails after a 009 change, that's a regression and must be investigated, not bypassed.
- Do not push to main directly. Only Gabi merges into main.
- Do not auto-confirm `--FORCE --DELETE-TARGET` in any test or smoke. The interactive prompt MUST be exercised real-input.

## What the next session SHOULD do

- Make commits along the way (per phase or per logical step), not one giant commit at the end.
- Run the test suite after every phase to catch regressions early.
- Use background bash (`run_in_background: true`) for any test run that takes more than 30 seconds.
- Update `tasks.md` checkboxes as tasks complete.
- If you discover a real spec gap during implementation, STOP and report. Do not paper over it.

---

## Quick reference: file paths

### Source

- `tools/d2net/src/D2Net.Scaffold/` — the project to refactor
- `tools/d2net/src/D2Net.Init/` — read-only reuse target (PathValidator, SettingsWriter, PgBridgeProcess, BridgeOptions, DbConnectionStringBuilder, DartFileScanner, ExitCodes)

### Tests

- `tools/d2net/tests/D2Net.Scaffold.Tests/` — where new test files land
- `tools/d2net/tests/D2Net.Init.Tests/AssemblyAttributes.cs` — pattern for the bridge-timeout module initializer to mirror

### Solution and binaries

- `tools/d2net/D2Net.sln` — the .NET solution; build with `dotnet build` from repo root
- `tools/d2net/src/D2Net.Init/bin/Debug/net8.0/d2net-init.exe` — for smoke tests
- `tools/d2net/src/D2Net.Scaffold/bin/Debug/net8.0/d2net-scaffold.exe` — what 009 produces

### Operator-visible

- `.D2NET/D2NET-Settings.json` — workspace settings (read-only by 009)
- `.D2NET/pgdb/` — PGLite data dir (locked by per-invocation bridge)
- `<repo>/glp_runtime/` — source tree
- `<repo>/glp_runtime_net/` — target tree (created by 009 scaffold)

---

End of handoff. Good luck.
