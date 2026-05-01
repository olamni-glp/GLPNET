---

description: "Task list for /D2NET-scaffold skill wrapper"
---

# Tasks: `/D2NET-scaffold` — Claude Code Skill Wrapper

**Input**: Design documents from `/specs/010-scaffold-skill/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/skill-contract.md

**Tests**: The skill is a markdown file with no programmatic test harness. Validation is a smoke walkthrough recorded in `validation.md`. The underlying binary's spec-009 test suite continues to provide end-to-end coverage of the binary itself.

**Organization**: Four user stories from the spec (P1 MVP scaffold, P2 idempotent re-scaffold, P2 destructive-confirm, P3 help/version) plus polish.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps task to a spec user story (US1 / US2 / US3 / US4) — `Foundational` for prerequisites, `Polish` for cross-cutting follow-ups.

## Path Conventions

- **Skill file**: `.claude/skills/D2NET-scaffold/SKILL.md`
- **Spec artifacts**: `specs/010-scaffold-skill/`
- **Underlying binary**: `tools/d2net/src/D2Net.Scaffold/` (UNTOUCHED by this feature)

---

## Phase 1: Setup

**Purpose**: Create the skill directory and the empty SKILL.md scaffold.

- [ ] **T001** [Foundational] Create `.claude/skills/D2NET-scaffold/` directory (sibling to existing `.claude/skills/D2NET-init/`).
- [ ] **T002** [Foundational] Create `.claude/skills/D2NET-scaffold/SKILL.md` with the verbatim frontmatter from `contracts/skill-contract.md` ("Frontmatter (verbatim contract)" section). Add the `## User Input` block with the standard `$ARGUMENTS` pattern. Add the `## Goal` and `## Operating Constraints` blocks per the contract. Validation: file parses as YAML+markdown, frontmatter `name = "D2NET-scaffold"`, `user-invocable: true`.

---

## Phase 2: Foundational

**Purpose**: Implement the procedural sections every user story relies on (binary discovery, build prompt, intent parsing, invoke).

- [ ] **T003** [Foundational] Add **Step 2 (locate the binary)** to SKILL.md per `contracts/skill-contract.md` Step 2: Release → Debug → `dotnet run` fallback. Include the explicit search paths (`tools/d2net/src/D2Net.Scaffold/bin/Release/net8.0/d2net-scaffold.exe` etc.) and the "slower fallback" notice when reaching step 3. Cover the FR-005 stop-when-no-dotnet-on-PATH case.
- [ ] **T004** [Foundational] Add **Step 3 (detect missing or stale binary)** to SKILL.md per contract Step 3. Include the staleness mtime comparison rule (R3 — exclude `pgbridge/` subtree), the missing-vs-stale confirmation-prompt templates, and the per-conversation "skip staleness" opt-out. The build command is `dotnet build tools/d2net/D2Net.sln`.
- [ ] **T005** [Foundational] Add **Step 4 (parse user intent)** to SKILL.md per contract Step 4. Cover the precedence-ordered branches: empty → `[]` (default scaffold mode, Q4); pure help → `[--help]`; pure version → `[--version]`; all-flag pass-through; mixed natural+flag; pure natural with markers; pure natural unrecognized → `[--help]` (FR-010a, Q5). Include the JSON-marker / bridge-port-marker / destructive-marker grammars.
- [ ] **T006** [Foundational] Add **Step 6 (invoke)** to SKILL.md per contract Step 6. Cover both forms: non-destructive `<binary> <flags>` and destructive `echo yes | <binary> --FORCE --DELETE-TARGET ...` (POSIX) / `'yes' | <binary> --FORCE --DELETE-TARGET ...` (PowerShell). Include the fallback `dotnet run --project tools/d2net/src/D2Net.Scaffold -- <flags>` form. Note that stdin drive only applies after the destructive gate has resolved affirmatively (Step 5).

**Checkpoint**: Steps 2–4 + 6 are present in SKILL.md. The skill can locate the binary and invoke it for non-destructive cases. Help/version short-circuit works.

---

## Phase 3: User Story 1 — MVP scaffold from inside Claude Code (Priority: P1) 🎯 MVP

**Goal**: User types `/D2NET-scaffold` (empty arguments) and the binary runs in default scaffold mode, surfacing the success summary plus a Claude-side recap.

- [ ] **T007** [US1] Add **Step 7 (surface results)** to SKILL.md per contract Step 7. Implement the `--json` branch that **suppresses the recap entirely** (clarified Q1) and surfaces stdout verbatim regardless of size. Implement the plain-text branch with the 50-line truncation footer ("show all" / "filter <substring>"). Document that show-all/filter follow-ups rely on conversation context (Q3, FR-018) rather than programmatic sub-commands.
- [ ] **T008** [US1] Add the **success-mode recap** to Step 7: parse the binary's stdout summary block (target path, files copied, working dirs created, dart_files rows updated, wall-clock duration) and append the one-line recap. Recap format from the contract: `Target at <path>; <N> files copied; <M> working directories created; <K> dart_files rows updated; <T>s wall-clock.`
- [ ] **T009** [US1] Add **Step 8 (hint dispatch)** to SKILL.md per contract Step 8. Cover exit codes US1 might encounter: 22 (`ScaffoldWorkspaceMissing` → "Run /D2NET-init first"), 23 (`ScaffoldSourceMissing` → name path, offer parent inspection), 26 (`ScaffoldCopyError` → idempotency note), 27 (`ScaffoldDbWriteFailed`), 28 (`ScaffoldWorkspaceLocked`), 1 (`ArgumentError`).
- [ ] **T010** [US1] Smoke-walk-test the MVP: with the binary already built and the workspace populated by `/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net`, in a fresh Claude Code session in this repo, type `/D2NET-scaffold`. Verify (a) Claude resolves flags to `[]`, (b) the binary runs once and exits 0, (c) `glp_runtime_net/` is created with the expected mirrored layout, (d) Claude's reply contains the binary's summary + the recap line. Record outcome in `specs/010-scaffold-skill/validation.md` row 3.
- [ ] **T011** [US1] Smoke-walk-test JSON suppression: type `/D2NET-scaffold as json`. Verify Claude resolves flags to `[--json]`, the binary's JSON stdout is surfaced verbatim, and **no recap is appended**. Record in `validation.md` row 4.
- [ ] **T012** [US1] Smoke-walk-test workspace-missing: in a fresh repo with no `.D2NET/`, type `/D2NET-scaffold`. Verify the binary exits 22 and Claude appends the "Run /D2NET-init first" hint. Record in `validation.md` row 7.
- [ ] **T012a** [US1] Smoke-walk-test missing-binary build flow (SC-005 coverage): delete `tools/d2net/src/D2Net.Scaffold/bin/` (both Release and Debug configs), then type `/D2NET-scaffold`. Verify Claude (a) emits the build-confirmation prompt, (b) on `yes` reply runs **exactly two subprocess invocations** in this order: `dotnet build tools/d2net/D2Net.sln` followed by `d2net-scaffold.exe`, (c) on `no` reply runs **zero subprocess invocations**. Record both branches in `validation.md`.

**Checkpoint**: User Story 1 is fully functional. The MVP one-liner works end-to-end. SC-005 invocation-count behaviour explicitly verified.

---

## Phase 4: User Story 2 — Idempotent re-scaffold after changes (Priority: P2)

**Goal**: User runs `/D2NET-scaffold` again after editing the source tree or the exclusion list. The binary's idempotency reconciles the target tree; Claude's recap shows net additions / removals.

- [ ] **T013** [US2] Smoke-walk-test idempotent re-run with no changes: after T010, type `/D2NET-scaffold` again. Verify exit 0; verify the recap shows `0 files copied; 0 working directories created; 0 dart_files rows updated`. Record in `validation.md`.
- [ ] **T014** [US2] Smoke-walk-test re-run after exclusion change: type `/D2NET-init --add-exclude bin`, then `/D2NET-scaffold`. Verify `glp_runtime_net/bin/` no longer exists; verify the recap reflects net removals. Then `/D2NET-init --remove-exclude bin --allow-system-exclusions` and `/D2NET-scaffold` — verify `glp_runtime_net/bin/` is recreated. Record in `validation.md`.

**Checkpoint**: User Story 2 confirmed. Idempotency works as the binary advertises.

---

## Phase 5: User Story 3 — Destructive-operation safety (skill prompt + binary stdin drive) (Priority: P2)

**Goal**: When user input contains a destructive marker or the literal `--FORCE --DELETE-TARGET`, Claude prompts at the skill layer, on `yes` records a structured marker AND drives the binary's own interactive prompt by piping `yes\n` to stdin.

- [ ] **T015** [US3] Add **Step 5 (destructive-operation gate)** to SKILL.md per contract Step 5. Include the closed marker word list (`force`, `delete`, `rebuild`, `reset`, `recreate`, `reinitialise`, `reinitialize`, `nuke`, `wipe`, `redo`), the `--FORCE` + `--DELETE-TARGET` literal-pair detection (in any order), the **target-directory absolute path** cache key derivation (read `.D2NET/D2NET-Settings.json`, resolve `target` against `<cwd>`, clarified Q2), the conversation-transcript marker pattern `[D2NET-scaffold: destructive-confirmed = <abs target path> @ <ISO timestamp>]`, the no-re-prompt-same-session rule, and the FR-016 unbalanced-pair pass-through.
- [ ] **T016** [US3] Update Step 6 (invoke) to drive the binary's stdin with `yes\n` when the resolved flag set contains `--FORCE --DELETE-TARGET`. Include both POSIX (`echo yes |`) and PowerShell (`'yes' |`) command forms. Surface the binary's prompt text along with the driven `yes` in the response so the safety flow is auditable (FR-014).
- [ ] **T017** [US3] Add the destructive-specific exit-code hints to Step 8: 24 (`ScaffoldTargetNotEmptyAndNotManaged` → suggest `/D2NET-scaffold force delete target`), 25 (`ScaffoldWorkdirCollision` → name offending paths), 29 (`ScaffoldOperatorCancelledTargetDeletion` → clean stop, suggest re-running without destructive markers if the cancel was a mistake).
- [ ] **T018** [US3] Smoke-walk-test destructive flow against an unmanaged target: hand-create `glp_runtime_net/__bogus.txt` (or use a fresh `glp_runtime_net/` not produced by scaffold), then type `/D2NET-scaffold force delete target`. Verify Claude (a) emits the skill-layer confirmation prompt naming the absolute target path, (b) on `yes` writes the structured marker, (c) invokes the binary as `echo yes | d2net-scaffold.exe --FORCE --DELETE-TARGET`, (d) the binary prompts and accepts the piped `yes`, (e) the target is deleted and re-scaffolded, (f) Claude's response contains both confirmations (skill layer + binary's). Record in `validation.md` row 6.
- [ ] **T019** [US3] Smoke-walk-test no-re-prompt: in the same conversation as T018, type another `/D2NET-scaffold force delete target` against the same target absolute path. Verify Claude proceeds without re-prompting at the skill layer (uses the cached marker) BUT still drives the binary's interactive prompt with `yes`. Record in `validation.md`.
- [ ] **T020** [US3] Smoke-walk-test refused-destructive at skill layer: type `/D2NET-scaffold force delete target` and reply `no` at the skill-layer prompt. Verify zero binary invocations occur; verify the response is a clean stop with no filesystem changes. Record in `validation.md` row 10.
- [ ] **T021** [US3] Smoke-walk-test target-not-empty-and-not-managed: with an unmanaged target tree present and a NON-destructive input `/D2NET-scaffold`, verify the binary exits 24 and Claude surfaces the "force delete target" hint without auto-applying. Record in `validation.md` row 9.
- [ ] **T022** [US3] Smoke-walk-test pass-through with destructive flags: type `/D2NET-scaffold --FORCE --DELETE-TARGET`. Verify the destructive gate fires (skill prompts), and on `yes` the same flow as T018 executes. Verify `--FORCE` alone (without `--DELETE-TARGET`) returns exit 1 and Claude surfaces the argument-error hint.

**Checkpoint**: User Story 3 is confirmed. Two-confirmation safety flow (skill + binary) works as designed; the cache key uses the target absolute path; FR-016 unbalanced-pair behaviour is correct.

---

## Phase 6: User Story 4 — Help / version / unrecognized routing (Priority: P3)

**Goal**: Help and version tokens short-circuit; empty input does NOT route to help (per Q4); unrecognized non-empty input DOES route to help (per Q5).

- [ ] **T023** [US4] Smoke-walk-test help: type `/D2NET-scaffold help`, `/D2NET-scaffold --help`, `/D2NET-scaffold -h`. Verify each runs the binary's `--help` form and surfaces the result verbatim. Record in `validation.md` row 1.
- [ ] **T024** [US4] Smoke-walk-test version: type `/D2NET-scaffold version`, `/D2NET-scaffold --version`. Verify each runs the binary's `--version` form. Record in `validation.md` row 2.
- [ ] **T025** [US4] Smoke-walk-test unrecognized non-empty: type `/D2NET-scaffold please scaffold quickly`. Verify Claude resolves to `[--help]` (FR-010a, Q5) and surfaces the help text. Verify Claude does NOT silently run the binary against the unrecognized tokens. Record in `validation.md` row 8.
- [ ] **T026** [US4] Smoke-walk-test pass-through with bridge-port: type `/D2NET-scaffold --json --bridge-port 55001`. Verify pass-through; verify recap suppression (Q1). Record in `validation.md` row 5.

**Checkpoint**: All four user stories are independently confirmed.

---

## Phase 7: Polish

**Purpose**: Validation document, CHANGELOG, README pointer, and final discoverability check.

- [ ] **T027** [P] [Polish] Create `specs/010-scaffold-skill/validation.md` from the table seed in `data-model.md` "Validation artifact" section. Fill all 10 rows from the smoke walkthroughs of T010/T011/T012/T013/T014/T018/T019/T020/T021/T022/T023/T024/T025/T026.
- [ ] **T028** [P] [Polish] Update root `CHANGELOG.md` — add a `## v2026.05.01-N` entry describing the skill's user-visible behavior, the slash-command name `/D2NET-scaffold`, the two-confirmation destructive safety flow (skill layer + binary stdin drive), the casing requirement, the empty-vs-unrecognized routing, and the link to `specs/010-scaffold-skill/spec.md`.
- [ ] **T029** [Polish] Verify the skill is discoverable: in a fresh Claude Code session, `/D2NET-scaffold help` should appear in the slash-command list and produce the binary's `--help` output. Document in `validation.md`.
- [ ] **T030** [Polish] Update `CLAUDE.md` (project instructions) if needed to mention the new skill alongside `D2NET-init` (e.g., in a "Known skills" reference section, if one exists). If no such section exists, skip.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1**: T001 → T002 (T002 needs the directory).
- **Phase 2**: All of Phase 1 done. T003 → T004 → T005 → T006 are sequential (each adds a numbered Step section to SKILL.md; they share the same file).
- **Phase 3 (US1)**: depends on Phase 2. T007 → T008 → T009 are sequential SKILL.md edits. T010/T011/T012 are smoke tests that need the binary built and a workspace; T010 must run before T011 (T011 reuses the workspace from T010); T012 needs a clean directory without `.D2NET/`.
- **Phase 4 (US2)**: depends on Phase 3 (T013/T014 reuse the workspace from T010).
- **Phase 5 (US3)**: T015/T016/T017 are sequential SKILL.md edits and CAN land in parallel with Phase 3 doc-edit tasks (different sections of SKILL.md, but caution — same file). T018–T022 are sequential smoke tests against the same workspace.
- **Phase 6 (US4)**: T023/T024/T025/T026 are independent smoke tests; can run in any order against an existing workspace.
- **Phase 7 (Polish)**: T027 depends on validation.md updates from all smoke tests. T028 / T029 / T030 are independent.

### Within Each Phase

- SKILL.md edits cannot run in parallel with each other (single file). Coordinate Phase 2 + Phase 3 + Phase 5 doc edits sequentially.
- Smoke walks cannot run in parallel with each other (one Claude Code session at a time on the same workspace).

### Parallel Opportunities

- **Phase 7**: T027 + T028 + T029 + T030 can run in parallel.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 → Phase 2 → Phase 3 (T001–T012).
2. Stop and validate via T010/T011/T012 smoke tests.
3. If green: ship as MVP.

### Incremental delivery

1. MVP (US1) lands. Slash-command `/D2NET-scaffold` works end-to-end for default scaffold + JSON + workspace-missing.
2. US2 confirms idempotent re-runs (one-line recap of net deltas).
3. US3 adds the two-confirmation destructive flow (the central new mechanic vs `/D2NET-init`).
4. US4 adds help/version/unrecognized routing.
5. Polish closes out the validation artifact and CHANGELOG.

---

## Notes

- The entire feature is one markdown file (`SKILL.md`). Most "tasks" are sections of that file.
- Smoke walkthroughs are manual by nature (model-driven skills don't have unit tests). Each smoke task explicitly records its outcome in `validation.md` so future maintainers can audit what was tested.
- Casing matters: the skill directory and frontmatter `name` must be exactly `D2NET-scaffold` (uppercase `D2NET`, lowercase `scaffold`). On case-sensitive filesystems the user types the casing exactly to invoke.
- Keep the SKILL.md body in lockstep with `contracts/skill-contract.md`. Any deviation requires a corresponding spec/contract update.
- The destructive flow is the central new mechanic vs `/D2NET-init`: TWO confirmations (skill layer + binary stdin drive) are required, not one. T015–T020 verify this explicitly.
- The `--json` recap suppression (Q1) is the central new output mechanic. T011 verifies it.

### Coverage notes (deferred from /speckit-analyze remediation)

- **SC-001 (latency 70s)**: Inherited from spec 009 SC-001 (binary-side ceiling 60s). The skill adds <1s; explicit timing measurement at the skill layer is not in this task list. If a perf regression is suspected post-implement, time `/D2NET-scaffold` end-to-end manually against a 1000-dart workspace.
- **SC-006 (bridge-port collision)**: Treated as a manual operator test (spec.md Assumptions). Not in the smoke matrix. To reproduce: bind port 54400 with another process, then `/D2NET-scaffold --bridge-port 54400` — verify exit 27 or 28 surfaces with the FR-019 hint.
- **SC-007 (plain-text truncation)**: The 50-line truncation rule is identical to `/D2NET-init` FR-017 and inherits coverage from spec 006's `--list` smoke walks (which exercise the rule against a 1000-file tree). The `/D2NET-scaffold` plain-text summary is concise enough that natural triggering of the truncation is rare; if a synthetic test is desired, run scaffold against a workspace with thousands of dart files. JSON-verbatim half is directly covered in T011.
- **FR-019 "other" exit code**: For exit codes outside the documented 22–29 / 1 catalogue (e.g., a user typo'd flag producing a different non-zero exit), the skill surfaces stderr verbatim with no specific hint. No dedicated smoke task; covered by general FR-019 wording.
