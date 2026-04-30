---

description: "Task list for /D2NET-init skill wrapper"
---

# Tasks: `/D2NET-init` — Claude Code Skill Wrapper

**Input**: Design documents from `/specs/006-d2net-init-skill/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/skill-contract.md

**Tests**: The skill is a markdown file with no programmatic test harness. Validation is a smoke walkthrough recorded in `validation.md`. The underlying binary's 89-test D2Net.Init suite continues to provide end-to-end coverage of the binary itself.

**Organization**: Three user stories from the spec (P1 init, P2 inspect, P2 destructive-confirm) plus polish.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Maps task to a spec user story (US1 / US2 / US3) — `Foundational` for prerequisites, `Polish` for cross-cutting follow-ups.

## Path Conventions

- **Skill file**: `.claude/skills/D2NET-init/SKILL.md`
- **Spec artifacts**: `specs/006-d2net-init-skill/`
- **Underlying binary**: `tools/d2net/src/D2Net.Init/` (UNTOUCHED by this feature)

---

## Phase 1: Setup

**Purpose**: Create the skill directory and the empty SKILL.md scaffold.

- [ ] **T001** [Foundational] Create `.claude/skills/D2NET-init/` directory.
- [ ] **T002** [Foundational] Create `.claude/skills/D2NET-init/SKILL.md` with the verbatim frontmatter from `contracts/skill-contract.md` ("Frontmatter (verbatim contract)" section). Add a placeholder `## User Input` block with the standard `$ARGUMENTS` pattern. Build verifies: file parses as YAML+markdown, frontmatter `name = "D2NET-init"`.

---

## Phase 2: Foundational

**Purpose**: Implement the procedural sections that every user story relies on (binary discovery, build prompt, intent parsing).

- [ ] **T003** [Foundational] Add Step 2 (binary discovery) to SKILL.md per `contracts/skill-contract.md`: Release → Debug → `dotnet run` fallback. Include the explicit search paths and the "slower fallback" notice when reaching step 3.
- [ ] **T004** [Foundational] Add Step 3 (missing/stale detection + build confirmation) to SKILL.md. Include the staleness mtime comparison rule (R3), the confirmation-prompt template, and the per-conversation "skip staleness" opt-out.
- [ ] **T005** [Foundational] Add Step 4 (parse user intent) to SKILL.md: empty/help branch, all-flag-style branch, mixed branch, pure-natural-language branch with key-value + verb + JSON-marker grammar.
- [ ] **T006** [Foundational] Add Step 7 (augment with `--non-interactive` and `--accept-suggested-exclusions`) to SKILL.md.
- [ ] **T007** [Foundational] Add Step 8 (invoke via Bash tool) to SKILL.md, with both binary-path and `dotnet run` invocation forms.

**Checkpoint**: Steps 2–4, 7, 8 are present in SKILL.md. The skill can locate the binary and invoke it for non-destructive, non-shortcut, non-inspection-specific cases.

---

## Phase 3: User Story 1 — Init from inside Claude Code (Priority: P1) 🎯 MVP

**Goal**: User types `/D2NET-init source=X extension=Y target=Z` and the binary runs end-to-end with derived flags.

- [ ] **T008** [US1] Add Step 9 (surface results — JSON-bypass + 50-line truncation for plain text) to SKILL.md. Include the "show all" / "filter <substring>" footer template.
- [ ] **T009** [US1] Add Step 10 (success-init recap parsing) to SKILL.md. Include the regex / line-pattern Claude uses to extract workspace path, dart-file count, bridge port from the binary's stdout summary.
- [ ] **T010** [US1] Add Step 11 (hint dispatch) to SKILL.md. Cover exit codes 2, 3, 5, 8, 10, 11 with the contract-specified hint messages.
- [ ] **T011** [US1] Smoke-walk-test: with the binary already built, in a fresh Claude Code session in this repo, type `/D2NET-init source=glp_runtime extension=_net target=glp_runtime_net` and verify the binary runs once and Claude surfaces the success summary + recap. Record outcome in `specs/006-d2net-init-skill/validation.md` row 2.

---

## Phase 4: User Story 2 — Inspection from inside Claude Code (Priority: P2)

**Goal**: User types `/D2NET-init list` / `/D2NET-init exclusions` / `/D2NET-init current-phase` (with optional `--json` markers) and gets the binary's inspection output back.

- [ ] **T012** [US2] Verify Step 4's verb branch correctly maps `list` → `--list`, `exclusions` → `--Exclusions`, `current-phase` → `--current-phase`, `version` → `--version`. Update SKILL.md if any mapping is missing.
- [ ] **T013** [US2] Verify Step 4's JSON-marker translation: phrases containing "json" / "as json" / "in json" / "give me json" all add `--json` to the resolved flag set.
- [ ] **T014** [US2] Smoke-walk-test inspection: after T011, type each of `/D2NET-init list`, `/D2NET-init exclusions`, `/D2NET-init exclusions in json`, `/D2NET-init current phase`. Verify expected resolved flags and outputs. Record in `validation.md` rows 3–5.
- [ ] **T015** [US2] Verify FR-017 truncation: with a workspace that has > 50 dart files indexed, run `/D2NET-init list` and confirm Claude truncates to 50 lines + footer. Then `/D2NET-init list --json` and confirm full verbatim output. Record in `validation.md`.

---

## Phase 5: User Story 3 — Single-token shortcut + Destructive-operation safety (Priority: P2)

**Goal**: One-token shortcut works with confirmation; destructive verbs trigger a confirmation prompt; confirmed paths skip subsequent prompts in the same conversation.

- [ ] **T016** [US3] Add Step 5 (single-token shortcut) to SKILL.md per `contracts/skill-contract.md`. Include the existing-subdirectory check, default-derivation rule (`extension=_net`, `target=<token>_net`), and confirmation prompt.
- [ ] **T017** [US3] Add Step 6 (destructive-operation gate) to SKILL.md. Include the closed marker word list, the literal `--FORCE --DELETE-EXISTING` detection, the conversation-scoped confirmed-destructive set, and the no-re-prompt-same-session rule.
- [ ] **T018** [US3] Smoke-walk-test single-token shortcut: in a fresh repo with `glp_runtime/` present and no `.D2NET/`, type `/D2NET-init glp_runtime`. Verify Claude derives the conventional defaults, asks for confirmation naming all three derived values, and on `yes` runs the binary. Record in `validation.md` row 6.
- [ ] **T019** [US3] Smoke-walk-test destructive flow: with an existing `.D2NET/`, type `/D2NET-init force rebuild`. Verify Claude prompts for confirmation naming the absolute path, and on `yes` runs the binary with `--FORCE --DELETE-EXISTING`. Record in `validation.md` row 7.
- [ ] **T020** [US3] Smoke-walk-test no-re-prompt: in the same conversation as T019, type another destructive request against the same `.D2NET/`. Verify Claude proceeds without re-prompting. Record in `validation.md`.
- [ ] **T021** [US3] Smoke-walk-test refused-destructive: with an existing `.D2NET/` and a non-destructive `/D2NET-init source=...` invocation, verify the binary exits with `WorkspaceAlreadyExists` (3) and Claude surfaces the hint without auto-applying `--FORCE --DELETE-EXISTING`. Record in `validation.md` row 8.

---

## Phase 6: Polish

**Purpose**: Validation document, CHANGELOG, README pointer.

- [ ] **T022** [P] [Polish] Create `specs/006-d2net-init-skill/validation.md` from the table seed in `data-model.md` "Validation artifact" section. Fill rows 1, 8 from the smoke walkthroughs of T011/T014/T015/T018/T019/T020/T021.
- [ ] **T023** [P] [Polish] Update root `CHANGELOG.md` — add a `## v2026.04.30-N` entry describing the skill's user-visible behavior, the slash-command name `/D2NET-init`, the casing requirement, and the spec link.
- [ ] **T024** [Polish] Verify the skill is discoverable: in a fresh Claude Code session, `/D2NET-init --help` should appear in the slash-command list and produce the binary's `--help` output. Document in `validation.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1**: T001 → T002 (T002 needs the directory).
- **Phase 2**: All of Phase 1 done. Within Phase 2: T003–T007 are sequential (each step builds on the previous in SKILL.md).
- **Phase 3 (US1)**: depends on Phase 2. Within US1: T008/T009/T010 add Steps 9/10/11; T011 is the smoke test that needs all three.
- **Phase 4 (US2)**: depends on Phase 3 (the smoke tests build a workspace via US1 first). Within US2: T012/T013 are doc-verification; T014/T015 are smoke tests.
- **Phase 5 (US3)**: T016/T017 add Steps 5/6 to SKILL.md and can land in parallel with Phase 3/4 doc updates if desired. T018–T021 are sequential smoke tests against the same workspace.
- **Phase 6 (Polish)**: T022 depends on validation.md updates from T011/T014/T015/T018/T019/T020/T021. T023/T024 are independent.

### Within Each Phase

- SKILL.md edits cannot run in parallel with each other (single file).
- Smoke walks cannot run in parallel with each other (one Claude Code session at a time on the same workspace).

### Parallel Opportunities

- **Phase 6**: T022 + T023 + T024 can run in parallel.

---

## Implementation Strategy

### MVP first (US1 only)

1. Phase 1 → Phase 2 → Phase 3 (T001–T011).
2. Stop and validate via T011 smoke test.
3. If green: ship.

### Incremental delivery

1. MVP (US1) lands. Slash-command `/D2NET-init source=... extension=... target=...` works end-to-end.
2. US2 adds inspection support (`list`, `exclusions`, `current-phase`).
3. US3 adds the shortcut + destructive-confirm.
4. Polish closes out the validation artifact and CHANGELOG.

---

## Notes

- The entire feature is one markdown file (`SKILL.md`). Most "tasks" are sections of that file.
- Smoke walkthroughs are manual by nature (model-driven skills don't have unit tests). Each smoke task explicitly records its outcome in `validation.md` so future maintainers can audit what was tested.
- Casing matters: the skill directory and frontmatter `name` must be exactly `D2NET-init`. On case-sensitive filesystems the user types the casing exactly to invoke.
- Keep the SKILL.md body in lockstep with `contracts/skill-contract.md`. Any deviation requires a corresponding spec/contract update.
