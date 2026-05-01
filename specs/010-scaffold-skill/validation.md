# Validation — `/D2NET-scaffold` smoke walkthrough

**Date**: <to be filled by operator at validation run>
**Repo state**: a89bed718502af29efb0d85f0932dfcdc39ad7c2 (head at SKILL.md authoring; will advance once validation commits land)
**Binary version**: <output of `d2net-scaffold --version` at validation time>
**Workspace**: <summary of `/D2NET-init --list` at validation time>

This document records the manual smoke walkthrough of `/D2NET-scaffold` against this repo's `glp_runtime/` source tree after a `/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net` has populated `.D2NET/`.

The skill is a markdown-driven Claude Code procedure with no programmatic test harness (per `tasks.md` § Tests note). Validation is therefore a recorded interactive walkthrough; each row below corresponds to one or more T-tasks from `tasks.md`.

## Test cases

| # | Input                                                            | Expected resolved flags                                                                                                  | Source tasks            | Result    |
|---|------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------------|-------------------------|-----------|
| 1 | `/D2NET-scaffold help` / `/D2NET-scaffold --help` / `/D2NET-scaffold -h` | `--help`                                                                                                                 | T023                    | PENDING   |
| 2 | `/D2NET-scaffold version` / `/D2NET-scaffold --version`          | `--version`                                                                                                              | T024                    | PENDING   |
| 3 | `/D2NET-scaffold` (empty)                                        | `[]` — binary runs in default scaffold mode                                                                              | T010                    | PENDING   |
| 4 | `/D2NET-scaffold as json`                                        | `--json`; recap suppressed in skill response                                                                             | T011                    | PENDING   |
| 5 | `/D2NET-scaffold --json --bridge-port 55001`                     | `--json --bridge-port 55001` (pass-through); recap suppressed                                                            | T026                    | PENDING   |
| 6 | `/D2NET-scaffold force delete target` (against unmanaged target) | After skill-layer confirm + binary stdin-drive: `--FORCE --DELETE-TARGET`; `yes\n` piped to stdin                        | T018                    | PENDING   |
| 7 | `/D2NET-scaffold` (no `.D2NET/` present)                         | Exit 22 (`ScaffoldWorkspaceMissing`); skill surfaces "Run /D2NET-init first" hint                                        | T012                    | PENDING   |
| 8 | `/D2NET-scaffold please scaffold quickly`                        | `--help` (per FR-010a)                                                                                                   | T025                    | PENDING   |
| 9 | `/D2NET-scaffold` against existing target NOT scaffold-managed   | Exit 24 (`ScaffoldTargetNotEmptyAndNotManaged`); skill surfaces destructive-override hint without auto-applying          | T021                    | PENDING   |
| 10| `/D2NET-scaffold force delete target` (operator types `no` at skill prompt) | Zero binary invocations; skill stops cleanly; no filesystem changes                                                      | T020                    | PENDING   |

### Additional smoke walks (not in the seed table)

| Task   | Description                                                                                                                                                                                                                                                                                                       | Result    |
|--------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-----------|
| T012a  | Missing-binary build flow (SC-005): delete `tools/d2net/src/D2Net.Scaffold/bin/` (Release + Debug) then `/D2NET-scaffold`. On `yes` → exactly two subprocess invocations: `dotnet build tools/d2net/D2Net.sln` then `d2net-scaffold.exe`. On `no` → zero subprocess invocations.                                  | PENDING   |
| T013   | Idempotent re-run after T010: second `/D2NET-scaffold` exits 0; recap reports `0 files copied; 0 working directories created; 0 dart_files rows updated`.                                                                                                                                                          | PENDING   |
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

<freeform observations to fill in at validation time: bridge-port persistence, exact recap format strings, any edge cases observed, etc.>

### Coverage notes (from tasks.md § "Coverage notes")

- **SC-001 (latency 70 s)**: not measured at the skill layer; binary-side ceiling 60 s carried over from spec 009 SC-001. If a perf regression is suspected post-implement, time `/D2NET-scaffold` end-to-end manually against a 1000-dart workspace.
- **SC-006 (bridge-port collision)**: deferred to operator manual reproduction. To reproduce: bind port 54400 with another process, then `/D2NET-scaffold --bridge-port 54400` — verify exit 27 or 28 surfaces with the FR-019 hint.
- **SC-007 (plain-text truncation)**: rule inherited from `/D2NET-init` FR-017 and exercised by spec 006's `--list` smoke walks against a 1000-file tree. Scaffold's plain-text summary is concise enough that natural triggering is rare; for a synthetic test, run scaffold against a workspace with thousands of dart files. JSON-verbatim half is directly covered by row 4.
- **FR-019 "other" exit code**: surfacing stderr verbatim with no specific hint. No dedicated smoke task; covered by general FR-019 wording.
