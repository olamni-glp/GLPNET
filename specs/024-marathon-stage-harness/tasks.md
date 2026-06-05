---
description: "Task list for Marathon Stage Harness implementation"
---

# Tasks: Marathon Stage Harness

**Input**: Design documents from `/specs/024-marathon-stage-harness/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: INCLUDED. The spec defines 10 measurable success criteria (SC-001…SC-010) and
a per-story Independent Test; CLAUDE.md's Test Protocol is mandatory. Each user story
therefore has a test task mapped to its SC. Write the test FIRST and confirm it FAILS
before implementing (TDD per CLAUDE.md).

**Organization**: Tasks grouped by user story for independent implementation/testing.

**⚑ Architecture decision pending (research.md D3)**: file paths below assume the
recommended home `codeconv/src/codeconv/marathon/`. If Gabi chooses a standalone package
at the plan-approval gate, only the import-path prefix shifts — task structure is
unchanged.

**Ordering note (FR-011)**: the spec mandates the Workflow-composition verification spike
"as the first implementation task." It therefore runs as **Phase 3 (US4)**, before the P1
keystone (US1, Phase 4), because US1's resume design (research.md D4) depends on the
verified substrate. US1 remains the business-priority MVP.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: can run in parallel (different files, no incomplete-task dependency)
- **[Story]**: US1–US7 from spec.md; Setup/Foundational/Polish carry no story label
- All paths are repo-relative from `D:\bstdev\research\glp\glpnet\`

## Path Conventions

Single project inside `codeconv` (plan.md Structure Decision). Source:
`codeconv/src/codeconv/marathon/`. Migrations:
`codeconv/src/codeconv/db/migrations/versions/`. Tests: `codeconv/tests/`.
Invoke pytest from the codeconv venv, serial: `codeconv/.venv/Scripts/python.exe -m pytest codeconv/tests/ --test-concurrency=1`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: package skeleton + CLI registration + data shapes

- [X] T001 Create the harness package skeleton `codeconv/src/codeconv/marathon/__init__.py` exporting an (initially empty) Typer `app` with help text, plus empty module files `store.py checkpoint.py gate.py cadence.py orchestrate.py verify_spike.py status.py gitblock.py trace.py escalation.py models.py`
- [X] T002 [P] Register the `marathon` Typer app statically in `codeconv/src/codeconv/cli.py` (mirror the bridge-free `tutorials` registration; do NOT route through the `tools/` auto-discovery registry per research.md D3)
- [X] T003 [P] Define row dataclasses in `codeconv/src/codeconv/marathon/models.py` for Marathon, StageBlock, Checkpoint, Approval, StatusReport, VerificationTrace, GitBlock, Escalation (fields per data-model.md)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: schema, durable substrate wiring, and the dual-store primitives every story builds on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Create Alembic migration `codeconv/src/codeconv/db/migrations/versions/0010_marathon_schema.py` — `CREATE SCHEMA marathon` + all 8 tables (marathons, stage_blocks, checkpoints, approvals, status_reports, verification_traces, git_blocks, escalations) with constraints/indices per data-model.md; verify `codeconv --data-dir C:/pglite/research/glpnet migrate` applies it cleanly  ⟶ single-head 0010 verified offline; "applies cleanly" exercised by isolated-cluster fixtures (NOT run against the shared cluster unprompted — discipline)
- [X] T005 Wire the durable substrate in `codeconv/src/codeconv/marathon/store.py` by reusing `codeconv.bridge_client.acquire_or_discover`, `codeconv.db.engine` (build_url/setup_dbos), and `codeconv.durable` deterministic id-derivation — NO re-implementation of bridge/DBOS (research.md reuse map, FR-009/010)
- [X] T006 Implement primary-store primitives in `codeconv/src/codeconv/marathon/store.py`: `write_checkpoint` (append-only, allocate strictly-monotonic marathon-wide `sequence_no`) and `read_position` (max `sequence_no`) per contracts/checkpoint-store.md (I1–I4)
- [X] T007 [P] Implement the JSON-fallback mirror (writer/reader) under `.codeconv/marathon/<marathon_id>/` in `codeconv/src/codeconv/marathon/store.py`, each record carrying its `sequence_no` (data-model.md fallback layout; FR-020)
- [X] T008 [P] Implement stage→block cadence mapping in `codeconv/src/codeconv/marathon/cadence.py` (specify=1, clarify=1, plan+task+analyze=1, implement=N sessions, review=1) per FR-019/D9
- [X] T009 Implement `marathon start` in `codeconv/src/codeconv/marathon/__init__.py` — create/re-attach the marathon row and record the two standing preauthorizations (commit/push, Workflow opt-in); idempotent (FR-014/023, contracts/cli.md)
- [X] T010 [P] Implement `marathon doctor` in `codeconv/src/codeconv/marathon/__init__.py` — bridge reachability, active store (primary/fallback), last `sequence_no` per store, open escalations, budget headroom (contracts/cli.md)
- [X] T011 [P] Add marathon test fixtures to `codeconv/tests/conftest.py` reuse path (serial, `@needs_bridge`); provide a helper to spin up a throwaway marathon + tear it down per data-model schema

**Checkpoint**: durable store + schema + CLI skeleton ready — story work can begin

---

## Phase 3: User Story 4 - Compose Workflow tool & verify resumability + budget (Priority: P2, runs FIRST per FR-011) 🎯 de-risking spike

**Goal**: prove the chosen substrate (Workflow tool + harness) actually delivers safe,
cached-prefix-resumable, budget-bounded chunks before the marathon relies on it.

**Independent Test**: run a small multi-step Workflow as a stage-block; re-invoke with an
unchanged prefix → unchanged steps return cached, execution resumes at first changed/new
step; spent/remaining observed throughout.

### Tests for User Story 4

- [ ] T012 [P] [US4] Write `codeconv/tests/test_marathon_verify_spike.py` asserting cached-prefix resume (unchanged prefix → cached, resumes at first changed/new step) and that `budget.spent()/remaining()` are observable throughout; assert a `verification_traces` row `subject=workflow-spike` is recorded (SC-008) — confirm it FAILS first

### Implementation for User Story 4

- [ ] T013 [US4] Implement the Workflow-composition layer in `codeconv/src/codeconv/marathon/orchestrate.py`: run one stage-block as one Workflow run, capture `runId` as run-linkage, verify the Workflow opt-in preauthorization before launch, expose `budget.spent()/remaining()` to the harness (FR-009/010/023, contracts/workflow-composition.md)
- [ ] T014 [US4] Implement the FR-011 spike in `codeconv/src/codeconv/marathon/verify_spike.py`: a small multi-step Workflow exercising `resumeFromRunId` cached-prefix + budget tracking; record the verification result durably via `store.write` to `verification_traces` (FR-011)
- [ ] T015 [US4] Add the `marathon verify-spike` subcommand in `codeconv/src/codeconv/marathon/__init__.py` (contracts/cli.md)

**Checkpoint**: substrate verified and recorded — US1 may now build cross-session resume on it

---

## Phase 4: User Story 1 - Restart-safe resume with no context loss (Priority: P1) 🎯 MVP

**Goal**: on restart, objectively locate stage + WIP from durable state, resume from the
last checkpoint, skip completed work, zero re-instruction.

**Independent Test**: start a block, do work, induce interruption (end session / simulate
compaction / kill process), restart → correct stage + WIP reported, resumes from last
checkpoint, re-executes none of the completed units.

### Tests for User Story 1

- [ ] T016 [P] [US1] Write `codeconv/tests/test_marathon_resume.py`: write N checkpoints, drop the process, restart → `read_position` returns the Nth and 0 completed units re-execute; position derived from durable state not a summary (SC-001/002, I3/I4/I8) — confirm it FAILS first
- [ ] T017 [P] [US1] Write `codeconv/tests/test_marathon_store.py`: fallback episode fast-forwards on reconcile; a true fork escalates (exit 2, no silent pick); boundary interruption neither double-executes nor skips (SC-007, I5/I6/I7/I9) — confirm it FAILS first

### Implementation for User Story 1

- [ ] T018 [US1] Implement objective resume-locate in `codeconv/src/codeconv/marathon/checkpoint.py`: order roadmap (`buildkit-roadmap next`) → buildkit pipeline state → spec/plan/tasks, then max-`sequence_no` checkpoint; never read a conversation summary (FR-002/D4)
- [ ] T019 [US1] Implement skip-completed resume in `codeconv/src/codeconv/marathon/checkpoint.py` (completed_units never re-executed; recorded decisions remain in effect) (FR-003/SC-002)
- [ ] T020 [US1] Implement `reconcile()` in `codeconv/src/codeconv/marathon/store.py`: strictly-higher `sequence_no` wins + fast-forward stale store; true fork → write an `escalations` row (kind=`store_divergence`) and stop (FR-021/D5, contracts/checkpoint-store.md I6/I7)
- [ ] T021 [US1] Implement fallback-mode detection + surfacing in `codeconv/src/codeconv/marathon/store.py` (`active_store()` returns `fallback` when the bridge is unreachable; resume capability preserved) (FR-020/SC-007)
- [ ] T022 [US1] Add `marathon resume` and `marathon reconcile` subcommands in `codeconv/src/codeconv/marathon/__init__.py` (contracts/cli.md)
- [ ] T023 [US1] Implement boundary-interruption safety in `codeconv/src/codeconv/marathon/checkpoint.py` (resume at a block boundary executes the boundary unit exactly once) (edge case, I9)

**Checkpoint**: US1 is the deliverable MVP — interrupt/resume works end-to-end and is independently testable

---

## Phase 5: User Story 2 - Per-stage plan → approval → durable gate (Priority: P2)

**Goal**: present each mutating block's plan, record approve/change durably, honor it on
resume without re-asking.

**Independent Test**: reach a block boundary → plan presented + waits; record approval;
interrupt + resume → approval not re-requested.

### Tests for User Story 2

- [ ] T024 [P] [US2] Write `codeconv/tests/test_marathon_gate.py`: gate blocks until approved; recorded approval re-requested 0 times across resumes; a `change` supersedes but retains the prior decision (SC-004, US2-AS3) — confirm it FAILS first

### Implementation for User Story 2

- [ ] T025 [US2] Implement the approval gate in `codeconv/src/codeconv/marathon/gate.py`: present plan, record approve/change as append-only `approvals` rows with `supersedes_id` chain (FR-004/D6)
- [ ] T026 [US2] Wire resume to honor a recorded approval (short-circuit, no re-ask) in `codeconv/src/codeconv/marathon/gate.py` + `checkpoint.py` (FR-005/SC-004)
- [ ] T027 [US2] Add the `marathon gate` subcommand in `codeconv/src/codeconv/marathon/__init__.py` (contracts/cli.md)

**Checkpoint**: gate enforced and durable; US1+US2 work together

---

## Phase 6: User Story 3 - Re-runnable per-stage and per-subagent on failure (Priority: P2)

**Goal**: re-run a failed stage from its last checkpoint, or a single failed subagent in
isolation, without redoing succeeded units.

**Independent Test**: fan out multiple subagents, force one to fail, re-run → only the
failed subagent re-executes; succeeded siblings untouched.

### Tests for User Story 3

- [ ] T028 [P] [US3] Write `codeconv/tests/test_marathon_rerun.py`: per-subagent re-run re-executes only the failed subagent (0 siblings repeated); per-stage re-run restarts from last checkpoint not marathon start; failure history preserved alongside success; changed-input unit treated as new work (SC-003, FR-006/007/008, edge case) — confirm it FAILS first

### Implementation for User Story 3

- [ ] T029 [US3] Implement per-stage re-run from last checkpoint in `codeconv/src/codeconv/marathon/orchestrate.py` (FR-006)
- [ ] T030 [US3] Implement per-subagent isolated re-run by composing Workflow `resumeFromRunId` cached-prefix in `codeconv/src/codeconv/marathon/orchestrate.py` (FR-007, contracts/workflow-composition.md)
- [ ] T031 [US3] Preserve failure history append-only (failure rows retained beside the eventual success) in `codeconv/src/codeconv/marathon/store.py` (FR-008)
- [ ] T032 [US3] Treat changed-input units as new work (no stale cache) in `codeconv/src/codeconv/marathon/orchestrate.py` (edge case)
- [ ] T033 [US3] Add the `marathon rerun` subcommand (`--block`, optional `--subagent`) in `codeconv/src/codeconv/marathon/__init__.py` (contracts/cli.md)

**Checkpoint**: targeted re-runs work; US1–US3 integrate

---

## Phase 7: User Story 5 - Token budget + periodic standardized status (Priority: P3)

**Goal**: track spend against a ceiling; emit a 4-field status on a ~5-min cadence; halt
or escalate at the ceiling (0 overruns).

**Independent Test**: run long enough to cross the cadence → ≥1 report with all four
fields; set a low ceiling → work halts/escalates at the ceiling.

### Tests for User Story 5

- [ ] T034 [P] [US5] Write `codeconv/tests/test_marathon_status.py`: a status report contains all four fields (done/issues/tokens spent+remaining/to-do) and appears at least once per cadence interval (SC-005) — confirm it FAILS first
- [ ] T035 [P] [US5] Write `codeconv/tests/test_marathon_budget.py`: at the ceiling, work halts/escalates with 0 overrun; the in-flight unit ends at a safe checkpoint (SC-006, edge case) — confirm it FAILS first

### Implementation for User Story 5

- [ ] T036 [US5] Implement the standardized 4-field status report + persistence in `codeconv/src/codeconv/marathon/status.py` (FR-013/D8)
- [ ] T037 [US5] Implement budget tracking + ceiling halt/escalate (safe-checkpoint then stop, never overrun) in `codeconv/src/codeconv/marathon/orchestrate.py` reading `budget.spent()/remaining()` (FR-012/D7)
- [ ] T038 [US5] Add the `marathon status [--emit]` subcommand + the ~5-min cadence driver in `codeconv/src/codeconv/marathon/__init__.py` (contracts/cli.md)

**Checkpoint**: runs are legible + budget-bounded

---

## Phase 8: User Story 6 - Preauthorized commit + push per logical block (Priority: P3)

**Goal**: commit + push each completed block under the standing grant, staging only that
block's files; escalate (never force) on a blocked push.

**Independent Test**: complete a block → automatic commit+push of only that block's files;
a blocked push escalates instead of forcing.

### Tests for User Story 6

- [ ] T039 [P] [US6] Write `codeconv/tests/test_marathon_gitblock.py`: commit stages only the block's files (no sweeping); a non-fast-forward push escalates (kind=`push_blocked`) and never force-pushes (SC-010, FR-015) — confirm it FAILS first

### Implementation for User Story 6

- [ ] T040 [US6] Implement preauthorized commit in `codeconv/src/codeconv/marathon/gitblock.py` staging exactly the block's files (no `git add -A`), never bypassing hooks (FR-014, CLAUDE.md commit discipline)
- [ ] T041 [US6] Implement push with non-fast-forward/conflict detection → write an `escalations` row (kind=`push_blocked`) and stop; never force (FR-015/SC-010)
- [ ] T042 [US6] Integrate gitblock at block completion (final checkpoint → commit+push) in `codeconv/src/codeconv/marathon/orchestrate.py` (FR-019)

**Checkpoint**: git-level durable checkpoints land per block

---

## Phase 9: User Story 7 - Durable verification-trace substrate (Priority: P3)

**Goal**: per stage/primitive, durably record experiment inputs, metric scores,
accept/reject, and ordered refine history — append-only; substrate only, no optimizer.

**Independent Test**: record an input + score + decision; interrupt + resume → trace
(incl. refine history) durably recoverable and append-only.

### Tests for User Story 7

- [ ] T043 [P] [US7] Write `codeconv/tests/test_marathon_trace.py`: records persist across restart; multiple iterations preserve `(subject, refine_seq)` order with no overwrite; an external reader can reconstruct history without harness internals (US7-AS1/2/3) — confirm it FAILS first

### Implementation for User Story 7

- [ ] T044 [US7] Implement the append-only trace substrate in `codeconv/src/codeconv/marathon/trace.py` (experiment_input/metric_score/decision/refine_seq), substrate only — NO optimizer/loop (FR-016/017)
- [ ] T045 [US7] Add the `marathon trace` subcommand in `codeconv/src/codeconv/marathon/__init__.py` (contracts/cli.md)

**Checkpoint**: trace substrate ready for a future external optimizer (out of scope)

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: auto-mode policy glue, buildkit-stage hook skill, docs, full-suite gate

- [ ] T046 [P] Consolidate the auto-mode policy in `codeconv/src/codeconv/marathon/escalation.py`: exactly two block-points (gate + escalation), the decision table, and the standing-preauthorization checks (FR-022/023/D11, contracts/escalation.md)
- [ ] T047 [P] Create the buildkit-stage hook skill `.claude/skills/marathon-stage-harness/SKILL.md` integrating each pipeline stage as a marathon block and rooting into the CLAUDE.md memory chain / Restart-Resume order (FR-018, contracts/buildkit-hooks.md)
- [ ] T048 [P] Update `docs/current_plan.md` thin pointer and the CLAUDE.md *Multi-Stage Task Persistence & Restart-Resume* note to reference the implemented harness (FR-018)
- [ ] T049 [P] Run the quickstart.md walkthrough end-to-end (all 8 steps) against `multi-protocol-link-layer` across ≥3 deliberate session boundaries; record SC-009 result. **Explicitly verify restart-safe resume across all four cadence block types — specify; clarify; plan+task+analyze; an implement session — per SC-001** (and `review` if confirmed in FR-019, finding F1)
- [ ] T050 Run the full gate: `codeconv/.venv/Scripts/python.exe -m pytest codeconv/tests/ --test-concurrency=1` (marathon + regression) AND `bash test/run_all_tests.sh` (GLP REPL baseline unaffected); record pass/fail per Test Protocol

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup (P1)**: no dependencies — start immediately.
- **Foundational (P2)**: depends on Setup — BLOCKS all stories.
- **US4 (Phase 3)**: depends on Foundational; **runs first among stories (FR-011)**; produces the verified substrate US1 builds on.
- **US1 (Phase 4)**: depends on Foundational + US4 (substrate verified, research.md D4). The MVP.
- **US2, US3 (Phases 5–6)**: depend on Foundational + US1 (resume/checkpoint core).
- **US5, US6, US7 (Phases 7–9)**: depend on Foundational; US5/US6 also on US4 orchestrate.py + US1 checkpoints; independently testable.
- **Polish (Phase 10)**: depends on all targeted stories complete.

### Story dependency summary

```text
Setup → Foundational → US4 (spike, first) → US1 (MVP)
                                              ├→ US2
                                              ├→ US3
                                              ├→ US5 (needs US4 orchestrate)
                                              ├→ US6 (needs US1 checkpoints)
                                              └→ US7
                                                   → Polish
```

### Within each story

- Test task FIRST (confirm FAIL) → implementation → subcommand wiring.
- Models/schema before services; services before CLI subcommands.

### Parallel opportunities

- Setup: T002, T003 in parallel.
- Foundational: T007, T008, T010, T011 in parallel after T004–T006.
- All `[P]` test tasks within a story can be written in parallel.
- After US1, stories US2/US3/US5/US6/US7 can proceed in parallel where staffed (different modules).

## Parallel Example: User Story 1

```text
# Write both US1 test files in parallel (confirm they FAIL):
Task: "test_marathon_resume.py — cross-session resume + skip-completed (SC-001/002)"
Task: "test_marathon_store.py — reconciliation + fallback + boundary (SC-007)"
```

## Implementation Strategy

### MVP first (the keystone)

1. Phase 1 Setup → Phase 2 Foundational → Phase 3 US4 spike (verify substrate; FR-011).
2. Phase 4 US1 → **STOP and VALIDATE**: interrupt/resume with zero re-instruction (SC-001/002).
3. This is the demoable MVP — the single capability distinguishing the harness from running stages by hand.

### Incremental delivery

US1 (MVP) → US2 (gate) → US3 (re-run) → US5 (budget/status) → US6 (git) → US7 (trace) →
Polish. Each story adds value without breaking earlier ones; commit/push per logical block
(US6) once available.

## Notes

- [P] = different files, no incomplete-task dependency.
- Tests are mandatory here (10 SCs + CLAUDE.md Test Protocol); write them first and confirm FAIL.
- Commit per logical block; stage only the block's files; never force-push; never bypass hooks.
- ⚑ Confirm the research.md D3 home decision with Gabi at the plan-approval gate before T001.
- Reuse codeconv infra as libraries (bridge_client/db.engine/durable) — do not re-implement (FR-009/010).
