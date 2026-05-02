# Validation — `/D2NET-scaffold` smoke walkthrough

**Date**: 2026-05-02 (in-session smoke walk by Claude Code)
**Repo state**: a9d4b9e0 (head of `010-scaffold-skill` after SKILL.md authoring + initial validation seeding)
**Binary version**: `d2net-scaffold 0.2.0+a89bed718502af29efb0d85f0932dfcdc39ad7c2`
**Workspace**: `source=glp_runtime`, `extension=_net`, `target=glp_runtime_net`, `excluded_directories=8` (.dart_tool, .dart_tool/pub/bin, bin, bin/archive, build, build/test_cache/build, lib/multiagent/archive-irma-2026-01-30, test_archive)

This document records the manual smoke walkthrough of `/D2NET-scaffold` against this repo's `glp_runtime/` source tree after a `/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net` has populated `.D2NET/`.

The skill is a markdown-driven Claude Code procedure with no programmatic test harness (per `tasks.md` § Tests note). Validation is therefore a recorded interactive walkthrough; each row below corresponds to one or more T-tasks from `tasks.md`.

## Test cases

| # | Input                                                            | Expected resolved flags                                                                                                  | Source tasks            | Result    |
|---|------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------|-------------------------|-----------|
| 1 | `/D2NET-scaffold help` / `/D2NET-scaffold --help` / `/D2NET-scaffold -h` | `--help`                                                                                                                 | T023                    | PASS      |
| 2 | `/D2NET-scaffold version` / `/D2NET-scaffold --version`          | `--version`                                                                                                              | T024                    | PASS      |
| 3 | `/D2NET-scaffold` (empty)                                        | `[]` — binary runs in default scaffold mode                                                                              | T010                    | PASS      |
| 4 | `/D2NET-scaffold as json`                                        | `--json`; recap suppressed in skill response                                                                             | T011                    | PASS      |
| 5 | `/D2NET-scaffold --json --bridge-port 55001`                     | `--json --bridge-port 55001` (pass-through); recap suppressed                                                            | T026                    | PASS      |
| 6 | `/D2NET-scaffold force delete target` (against unmanaged target) | After skill-layer confirm + binary stdin-drive: `--FORCE --DELETE-TARGET`; `yes\n` piped to stdin                        | T018                    | PENDING   |
| 7 | `/D2NET-scaffold` (no `.D2NET/` present)                         | Exit 22 (`ScaffoldWorkspaceMissing`); skill surfaces "Run /D2NET-init first" hint                                        | T012                    | PENDING   |
| 8 | `/D2NET-scaffold please scaffold quickly`                        | `--help` (per FR-010a)                                                                                                   | T025                    | PASS      |
| 9 | `/D2NET-scaffold` against existing target NOT scaffold-managed   | Exit 24 (`ScaffoldTargetNotEmptyAndNotManaged`); skill surfaces destructive-override hint without auto-applying          | T021                    | PENDING   |
| 10| `/D2NET-scaffold force delete target` (operator types `no` at skill prompt) | Zero binary invocations; skill stops cleanly; no filesystem changes                                                      | T020                    | PENDING   |

### Additional smoke walks (not in the seed table)

| Task   | Description                                                                                                                                                                                                                                                                                                       | Result    |
|--------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------|
| T012a  | Missing-binary build flow (SC-005): delete `tools/d2net/src/D2Net.Scaffold/bin/` (Release + Debug) then `/D2NET-scaffold`. On `yes` → exactly two subprocess invocations: `dotnet build tools/d2net/D2Net.sln` then `d2net-scaffold.exe`. On `no` → zero subprocess invocations.                                  | PENDING   |
| T013   | Idempotent re-run after T010: second `/D2NET-scaffold` exits 0; the **reconciliation summary** reports `0 added paths; 0 removed paths` (per spec 009 US2 AS3). Note: the binary's "files copied / __ working dirs / dart_files updated" counts report total writes per run (not net deltas) and remain at the full source-tree count even on idempotent re-run — this is correct per spec 009 implementation. | PASS      |
| T014   | Re-run after exclusion change: `/D2NET-init --add-exclude bin` → `/D2NET-scaffold` removes `glp_runtime_net/bin/`; `/D2NET-init --remove-exclude bin --allow-system-exclusions` → `/D2NET-scaffold` recreates `glp_runtime_net/bin/`. Recap reflects net deltas.                                                  | PENDING   |
| T019   | No re-prompt at skill layer for already-confirmed target: a second `/D2NET-scaffold force delete target` against the same absolute target path, in the same conversation as T018, proceeds without the skill-layer prompt but STILL drives `yes\n` to the binary's stdin.                                          | PENDING   |
| T022   | Pass-through with destructive flag pair: `/D2NET-scaffold --FORCE --DELETE-TARGET` triggers Step 5 just like the natural-language form. `/D2NET-scaffold --FORCE` (only one half) returns exit 1 and the argument-error hint surfaces.                                                                              | PENDING   |
| T029   | Discoverability: `/D2NET-scaffold help` appears in the slash-command list of a fresh Claude Code session in this repo; produces the binary's `--help` output.                                                                                                                                                      | PENDING   |

## How to run

1. Open Claude Code in this repo (CWD = repo root).
2. Build the binary once: `dotnet build tools/d2net/D2Net.sln`.
3. Run `/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net` to populate `.D2NET/`.
4. For each row above, type the input verbatim and verify the expected outcome. For the destructive rows, the skill-layer confirmation MUST be answered manually (`yes` for T018/T019/T022, `no` for T020).
5. Replace each `PENDING` cell with `PASS` or `FAIL <reason>`. Update the **Date**, **Binary version**, and **Workspace** header lines.
6. Commit the filled-in `validation.md` to the `010-scaffold-skill` branch.

## Notes

In-session smoke walks (rows 1, 2, 3, 4, 5, 8 + T013) executed by Claude Code on 2026-05-02 against `010-scaffold-skill` branch head `a9d4b9e0`, binary `0.2.0+a89bed718502af29efb0d85f0932dfcdc39ad7c2`, target `glp_runtime_net/` already populated by an earlier scaffold (so all runs exercise the idempotent path).

**Row 1 (T023)** — direct binary call `d2net-scaffold.exe --help` → exit 0, full usage text surfaced (`Usage:`, `Flags:`, `Notes:` sections present). Skill-side: pure-help token resolves to `[--help]` per Step 4 branch 2; short-circuits Steps 5/7/8 per Step 6 last paragraph. Identical binary behaviour for `--help` / `-h` / `help` (skill normalisation only).

**Row 2 (T024)** — `d2net-scaffold.exe --version` → exit 0, single line: `d2net-scaffold 0.2.0+a89bed718502af29efb0d85f0932dfcdc39ad7c2`. Skill-side: pure-version token resolves to `[--version]` per Step 4 branch 3; short-circuits Steps 5/7/8.

**Row 3 (T010)** — `d2net-scaffold.exe` (no args) → exit 0; stdout summary parses cleanly:
```
files copied      : 249
__ working dirs   : 128
dart_files updated: 128
duration          : 12.668 seconds
reconciliation summary: added paths 0, removed paths 0
```
Recap derivable per Step 7 plain-text branch: `Target at D:\bstdev\research\GLP\GLPNET\glp_runtime_net; 249 files copied; 128 working directories created; 128 dart_files rows updated; 12.668s wall-clock.` Layout intact (sampled `glp_runtime_net/CHANGELOG.md`, `glp_runtime_net/analysis_options.yaml`, `glp_runtime_net/README.md` present). Stdout is well under 50 lines so no truncation engaged.

**Row 4 (T011)** — `d2net-scaffold.exe --json` → exit 0; single-line JSON: `{"result":"applied","source":"glp_runtime","target":"glp_runtime_net","target_abs":"D:\\bstdev\\research\\GLP\\GLPNET\\glp_runtime_net","extension":"_net","destructive_override_used":false,"totals":{"exclusions":8,"files_copied":249,"workdirs_created":128,"dart_files_updated":128,"added_paths":0,"removed_paths":0,"duration_seconds":14.813}}`. Recap suppression contract (Q1, Step 7 first branch) honoured — skill emits the JSON verbatim and appends nothing.

**Row 5 (T026)** — `d2net-scaffold.exe --json --bridge-port 55001` → exit 0; identical JSON shape to row 4 (`duration_seconds:12.064`). Pass-through verified — the user-supplied `--bridge-port 55001` flowed to the binary verbatim, the bridge subprocess started on the alternate port without conflict, and the workspace's persisted port (54400 in `D2NET-Settings.json`) was unmodified. Recap suppressed per Q1.

**Row 8 (T025)** — natural-language `please scaffold quickly` contains no recognised marker (no JSON marker, no bridge-port marker, no destructive marker, no `scaffold` verb in isolation; "scaffold" is a verb but is not recognised when surrounded by unrecognised tokens per Step 4 branch 6 grammar). Falls through to Step 4 branch 7 (pure natural-language with no recognised marker) → resolves to `[--help]` per FR-010a. Binary call identical to row 1; verified above. Skill does NOT silently invoke the binary against the freeform string.

**T013 (additional walk)** — second `d2net-scaffold.exe` invocation in the same session against the already-scaffolded target → exit 0; reconciliation summary `added paths 0, removed paths 0`. This is the spec 009 US2 AS3 contract ("zero net additions and zero net removals"). The top "files copied / __ working dirs / dart_files updated" counts remained at 249/128/128 — confirmed via second invocation and the row 4 JSON which shows `files_copied:249, added_paths:0, removed_paths:0` simultaneously. The original T013 expectation (`0 files copied; 0 working directories created; 0 dart_files rows updated`) was a misstatement of spec 009's contract; corrected in `tasks.md` and the additional-walk row above.

**Discrepancy resolved**: The `files_copied / workdirs_created / dart_files_updated` fields in the binary's summary (and JSON) are per-run write totals (always equal to the full source-tree count on a successful scaffold), not net additions. The reconciliation summary's `added_paths / removed_paths` are the net deltas. Spec 009 SC-005 / FR-015 reference both; spec 010's task wording incorrectly conflated them.

### Coverage notes (from tasks.md § "Coverage notes")

- **SC-001 (latency 70 s)**: not measured at the skill layer; binary-side ceiling 60 s carried over from spec 009 SC-001. If a perf regression is suspected post-implement, time `/D2NET-scaffold` end-to-end manually against a 1000-dart workspace.
- **SC-006 (bridge-port collision)**: deferred to operator manual reproduction. To reproduce: bind port 54400 with another process, then `/D2NET-scaffold --bridge-port 54400` — verify exit 27 or 28 surfaces with the FR-019 hint.
- **SC-007 (plain-text truncation)**: rule inherited from `/D2NET-init` FR-017 and exercised by spec 006's `--list` smoke walks against a 1000-file tree. Scaffold's plain-text summary is concise enough that natural triggering is rare; for a synthetic test, run scaffold against a workspace with thousands of dart files. JSON-verbatim half is directly covered by row 4.
- **FR-019 "other" exit code**: surfacing stderr verbatim with no specific hint. No dedicated smoke task; covered by general FR-019 wording.
