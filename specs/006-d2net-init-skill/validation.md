# Validation — `/D2NET-init` smoke walkthrough

**Date**: 2026-04-30
**Repo state**: branch `006-d2net-init-skill` based on `63986f28f801cc473eede0c563fcef8456233d7b`
**Binary version**: `d2net-init 0.2.0` (spec 005, v2026.04.30-4)

## Lint checks (automated)

| # | Check                                                                           | Result |
|---|---------------------------------------------------------------------------------|--------|
| L1 | `.claude/skills/D2NET-init/SKILL.md` exists and is tracked.                    | PASS — created under T001/T002. |
| L2 | Frontmatter parses as YAML; required keys present.                             | PASS — verified via PowerShell parse during /speckit-implement. |
| L3 | Frontmatter `name` equals `"D2NET-init"`; `user-invocable: true`.              | PASS. |
| L4 | Body contains all 11 procedural step headings (Step 1 through Step 11).        | PASS — measured 11 `### Step` headings. |
| L5 | Body contains examples for each documented invocation form.                    | PASS — Examples section covers init, single-token, inspection, force-rebuild, help/version, pass-through. |

## Smoke walkthrough (manual; expected to be run by the user in a fresh Claude Code session)

The table below is the seed structure from `data-model.md`. Each row is the contract against which a manual smoke run is compared. Mark the **Result** column `PASS` / `FAIL` with the observed resolved flag set after running each input.

| # | Input                                                                            | Expected resolved flags                                                                                              | Result |
|---|----------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------------|--------|
| 1 | `/D2NET-init`                                                                    | `[--help]`                                                                                                            | _pending user smoke_ |
| 2 | `/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net`           | `--source glp_runtime --target-extension _net --target glp_runtime_net --accept-suggested-exclusions --non-interactive` | _pending user smoke_ |
| 3 | `/D2NET-init list`                                                               | `--list`                                                                                                              | _pending user smoke_ |
| 4 | `/D2NET-init exclusions in json`                                                 | `--Exclusions --json`                                                                                                 | _pending user smoke_ |
| 5 | `/D2NET-init current phase`                                                      | `--current-phase`                                                                                                     | _pending user smoke_ |
| 6 | `/D2NET-init glp_runtime` (single-token shortcut, with `glp_runtime/` present)   | After confirm: `--source glp_runtime --target-extension _net --target glp_runtime_net --accept-suggested-exclusions --non-interactive` | _pending user smoke_ |
| 7 | `/D2NET-init force rebuild` (with existing `.D2NET/`)                            | After confirm: prior flags + `--FORCE --DELETE-EXISTING`                                                              | _pending user smoke_ |
| 8 | `/D2NET-init` re-invocation against existing workspace (no destructive verb)     | Binary exits 3 (`WorkspaceAlreadyExists`); skill surfaces hint without retry                                          | _pending user smoke_ |
| 9 | `/D2NET-init version`                                                            | `[--version]` (short-circuit; no augmentation)                                                                        | _pending user smoke_ |
| 10 | `/D2NET-init list --json` against a workspace with > 50 dart files              | `--list --json`; output surfaced verbatim regardless of size (no truncation footer)                                   | _pending user smoke_ |
| 11 | `/D2NET-init list` against a workspace with > 50 dart files (plain text)        | `--list`; output truncated at 50 lines + "show all" footer                                                            | _pending user smoke_ |

## Notes

- The skill is markdown — there is no automated harness that can drive Claude Code from outside the IDE. The smoke walkthrough above MUST be executed by a real user in a fresh Claude Code session and the table marked.
- The lint checks (L1–L5) are automatable and were run during `/speckit-implement` Phase 6.
- Underlying binary's 89-test D2Net.Init.Tests suite continues to provide end-to-end coverage of the binary itself; the skill is a thin invocation wrapper and does not need its own Dart-file fixtures.
- After context compaction in long-running conversations, row 7's destructive-confirmation cache may be cleared and re-prompt — that is the documented acceptable degradation per research.md R4.
