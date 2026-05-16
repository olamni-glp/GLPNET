---
description: "Tasks for feature 017 — codeconv-planagents: orchestrated per-tombstone Dart→C#/.NET conversion-plan generation"
---

# Tasks: codeconv-planagents — orchestrated per-tombstone Dart→C#/.NET conversion-plan generation

**Input**: Design documents from `specs/017-conversion-plan-agents/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/ (plan_readiness_algorithm.md, planagents_cli.md, planagents_schema.md, conversion_plan_artefact_format.md, agent_orchestration.md), quickstart.md (all present)

**Tests**: Tests are REQUIRED. Spec SC-001…SC-009 are measurable outcomes that demand verification; US1–US5 each carry explicit acceptance scenarios and an Independent Test; the contracts enumerate structural/behavioural obligations. Tests are written BEFORE implementation per DISCIPLINE.md §2.4. The Python contract surface is fully deterministic and testable without spawning real LLM agents (a mocked-agent harness drives the orchestration primitives); only the artefact *content quality* is out of automated scope.

**Organization**: Tasks grouped by user story. US1 (P1) = first-wave plan generation for plan-ready leaves; US2 (P2) = frontier advance; US3 (P2) = SCC coordinated batch; US4 (P2) = escalation of non-incremental gaps; US5 (P3) = separate research sub-agent. **MVP = US1** (it delivers the table, readiness predicate, `next`/`plan-started`/`plan-completed`/`status`, artefact path/validation, and the skill orchestration loop end-to-end for leaves). US2/US3/US4 refine selection + escalation; US5 adds research delegation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Different files, no dependencies on incomplete tasks → can run in parallel
- **[Story]**: US1 (P1 first wave), US2 (P2 frontier), US3 (P2 SCC batch), US4 (P2 escalations), US5 (P3 research)
- Paths are relative to repo root `D:\BSTDEV\research\GLP\GLPNET\`

## Path Conventions

- Python source: `codeconv/src/codeconv/tools/planagents/` (new tool subpackage)
- Python tests: `codeconv/tests/`
- Alembic migrations: `codeconv/src/codeconv/db/migrations/versions/`
- Conversion-plan artefacts: `.codeconv/conversion-plans/` (checked in)
- Tombstone artefacts: `.codeconv/tombstones/` (4 appended YAML keys)
- Slash skill: `.claude/skills/codeconv-planagents/`
- Spec artefacts: `specs/017-conversion-plan-agents/`
- **`--data-dir C:/pglite/research/glpnet` is mandatory on this exFAT checkout for every bridge-touching command/test**

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish a green test baseline; confirm prerequisites; snapshot pre-feature state.

- [ ] T001 Confirm features 012 + 014 + 015 are present on this branch: `git log --oneline | findstr 015` shows feature-015 commits; `codeconv/src/codeconv/tools/depgraph/__init__.py` and `codeconv/src/codeconv/db/migrations/versions/0002_dart_depgraph.py` exist; `codeconv/src/codeconv/tools/discover/tombstone.py::_FIELD_ORDER` already contains the six feature-015 keys (`topo_level`…`target_path`)
- [ ] T002 Run baseline `codeconv/.venv/Scripts/python.exe -m pytest codeconv/tests/` and confirm green per memory `project_015_codeconv_depgraph_status.md` (≈116 pass / 3 skip; known flakes isolation-green). If unexpected reds, STOP and report (DISCIPLINE.md §2.3)
- [ ] T003 Confirm `--data-dir` is a wired top-level option: `codeconv --help | findstr data-dir`; confirm `codeconv.dart_depgraph` is populated (run `/codeconv-depgraph --data-dir C:/pglite/research/glpnet` if empty — this feature requires a non-empty depgraph, FR-018)
- [ ] T004 Snapshot baseline counts into `specs/017-conversion-plan-agents/baseline.json`: file total, edge total, leaf/isolated count, multi-file SCC count (read from `codeconv.dart_depgraph`; the leaf set is the expected US1 first wave)
- [ ] T005 Snapshot pre-feature `\dn`, `\dt codeconv.*`, `\dt public.*`, `\dt dbos.*` into `specs/017-conversion-plan-agents/pre_feature_schema_snapshot.txt` for the SC-007 isolation check

**Checkpoint**: Setup green → Foundational begins.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Land the Alembic migration + the pure readiness predicate + the tombstone field-order extension — the blocking prerequisites for ALL user stories.

- [ ] T006 Create `codeconv/src/codeconv/db/migrations/versions/0003_dart_plans.py` with the exact DDL from `contracts/planagents_schema.md`: `CREATE TABLE IF NOT EXISTS codeconv.dart_plans` (+ partial index `dart_plans_open_escalations_idx`) and `CREATE TABLE IF NOT EXISTS codeconv.planagents_runs` (+ conditional FK `dart_plans.plan_run_id → planagents_runs.id`). `downgrade()` = single `DROP TABLE IF EXISTS codeconv.planagents_runs, codeconv.dart_plans CASCADE`. Revision metadata: `revision="0003"`, `down_revision="0002"`
- [ ] T007 [P] Extend `codeconv/src/codeconv/tools/discover/tombstone.py::_FIELD_ORDER` by APPENDING the four feature-017 keys (`plan_started_at`, `plan_completed_at`, `plan_path`, `open_escalation_count`) AFTER the six feature-015 keys; preserve the null-vs-missing handling exactly as for the feature-015 keys (data-model §2). No other change to `tombstone.py`
- [ ] T008 [P] Create the tool subpackage skeleton: `codeconv/src/codeconv/tools/planagents/__init__.py` exporting `app: typer.Typer` (subcommands `status`/`next`/`plan-started`/`plan-completed`/`aggregate-escalations`/`stamp-tombstones`/`rebuild-plans-from-tombstones`, `status` as default) per feature-012 `codeconv_tool_contract.md`; empty `readiness.py`, `workflow.py`, `tombstone_writer.py`, `artefact.py` modules with signatures from the contracts
- [ ] T009 Implement `codeconv/src/codeconv/tools/planagents/readiness.py` — the pure plan-readiness predicate + four-state classify + topo/SCC `select_next(limit=7)` exactly per `contracts/plan_readiness_algorithm.md` (no bridge, no I/O; takes nodes/depgraph/cross_scc_deps/plans dicts)
- [ ] T010 [P] Write `codeconv/tests/test_planagents_readiness.py` — pure unit tests (no `@needs_bridge`) for: leaf/isolated ⇒ `plan_ready`; `plan_in_progress` dep does NOT unblock (FR-004); cross-SCC ordering invariant (SC-002); SCC-batch grouping + partial-batch resume + downstream gating (FR-011); `--limit` soft cap with SCC units never split (FR-021); determinism (same input → same output)
- [ ] T011 Add `codeconv/tests/test_planagents_schema_isolation.py` (`@needs_bridge`) per `contracts/planagents_schema.md` § Verification: only `codeconv` schema changed; `dart_plans`/`planagents_runs` created with the exact columns/constraints; `open_escalation_count >= 0` CHECK rejects negatives; FK CASCADE on `dart_files` delete; feature-012/-014/-015 tables byte-identical to T005 snapshot; downgrade-then-upgrade idempotent. **(FR-020 runtime write-surface assertion — analyze C2 remedy)**: additionally assert that after a full `next`/`plan-started`/`plan-completed`/`aggregate`/`stamp`/`rebuild` exercise, the row contents of `codeconv.dart_files`, `dart_imports`, `dart_callers`, `dart_files_orphaned`, `discover_runs`, `dart_depgraph`, and `dart_conversions` are byte-identical to a pre-exercise snapshot (the workflow issues ZERO writes to the seven protected tables — FR-020, not only schema-level SC-007 isolation)
- [ ] T012 Run the migration: `codeconv runner migrate --data-dir C:/pglite/research/glpnet`; confirm `codeconv.dart_plans` + `codeconv.planagents_runs` appear under `codeconv` only; run `pytest codeconv/tests/test_planagents_readiness.py codeconv/tests/test_planagents_schema_isolation.py` → green

**Checkpoint**: Foundational complete → the bridge has the two new empty tables; the readiness predicate is proven pure-green. All user stories can begin.

---

## Phase 3: User Story 1 — Generate the first wave of conversion plans (Priority: P1) 🎯 MVP

**Goal**: `/codeconv-planagents` plans every depgraph leaf: for each plan-ready tombstone the skill records `plan-started`, spawns a planning sub-agent that inspects the real `.dart` and writes a structurally-valid artefact at `.codeconv/conversion-plans/<rel>.dart.md`, then records `plan-completed`; ≤7 agents concurrent; idempotent re-run.

**Independent Test**: On a discovered+depgraph baseline, run `/codeconv-planagents`; for every leaf an artefact exists at the mirrored path, `codeconv.dart_plans` has a `plan_completed_at IS NOT NULL` row, every artefact has sections 1–6, and ≤7 agents were ever concurrent.

- [ ] T013 [US1] Implement `workflow.py` bridge acquire + `status` subcommand per `contracts/planagents_cli.md` § status (depgraph-empty ⇒ exit 2 unconditionally incl. `--json`; classify all non-orphaned nodes; emit counts; write nothing)
- [ ] T014 [US1] Implement `workflow.py` `next` subcommand: read depgraph + `dart_files` − orphaned + `dart_plans`, call `readiness.select_next(limit)`, emit the `next` JSON shape from `contracts/planagents_cli.md` (alphabetical row keys, topo+lex order, SCC siblings contiguous); empty batch ⇒ exit 0 `"nothing to plan"` (FR-018)
- [ ] T015 [US1] Implement `workflow.py` `plan-started <path>` per `contracts/planagents_schema.md` write protocol: validate path ∈ `dart_files` & not orphaned (else exit 2); `INSERT … ON CONFLICT (path) DO NOTHING`; idempotent already-started/already-completed warnings; optional `planagents_runs` row; tombstone `plan_started_at` stamp via `tombstone_writer.py` unless `--no-tombstone-update`
- [ ] T016 [US1] Implement `workflow.py` `plan-completed <path> [--plan-path] [--escalations n]`: row-absent ⇒ exit 2 `"must call plan-started first"`; already-completed ⇒ warn exit 0; else `UPDATE … WHERE plan_completed_at IS NULL`; stamp `plan_completed_at`/`plan_path`/`open_escalation_count`
- [ ] T017 [P] [US1] Implement `tombstone_writer.py` — append-only stamp/read of the four plan-state keys using the existing canonical YAML emitter + `_canonicalise`; null-vs-missing per data-model §2; byte-identical re-stamp
- [ ] T018 [P] [US1] Implement `artefact.py` — artefact + escalations-report path resolution (default roots, overridable) and `validate(path)` structural check per `contracts/conversion_plan_artefact_format.md` § "Structural validation" (front-matter keys; sections 1–6 ordered; §7 iff scc_siblings; `### E<n>` five-field schema; `generated_at` sole volatile field)
- [ ] T019 [US1] Author `.claude/skills/codeconv-planagents/SKILL.md`: YAML frontmatter (name/description/argument-hint/compatibility); venv/repo-root/pre-execution resolution copied from `.claude/skills/codeconv-depgraph/SKILL.md`; the orchestration loop pseudocode from `contracts/planagents_cli.md` § "Skill orchestration loop"; the planning sub-agent prompt contract from `contracts/agent_orchestration.md` (real-`.dart` inspection FR-006, mandated sections, escalate-don't-guess FR-008, research-delegation FR-009); ≤7-concurrent rule (R3)
- [ ] T020 [US1] Add `codeconv/tests/test_planagents_next.py` (`@needs_bridge`): leaves are exactly the `plan_ready` set on an empty `dart_plans`; topo+lex order; `--limit` honoured; already-`plan_in_progress` excluded; depgraph-empty ⇒ exit 2 (US1 AC2 / SC-002)
- [ ] T021 [US1] Add `codeconv/tests/test_planagents_lifecycle.py` (`@needs_bridge`): `plan-started`→`plan-completed` happy path; idempotent re-`plan-started` (no dup row), re-`plan-completed` (no-op warn); `plan-completed` before `plan-started` ⇒ exit 2; tombstone keys round-trip (US1 AC3 / SC-003)
- [ ] T022 [US1] Add `codeconv/tests/test_planagents_artefact_validation.py` (no bridge): `validate()` accepts a well-formed fixture artefact and rejects: missing section, out-of-order sections, §7-without-siblings, malformed `### E`, missing front-matter key (SC-004)
- [ ] T023 [US1] Add `codeconv/tests/test_planagents_orchestration_mock.py` (`@needs_bridge`, mocked planning agent): drive the loop against a 2-leaf fixture with a stub agent that writes a canned valid artefact; assert exactly N artefacts, N completed `dart_plans` rows, ≤7 concurrent (counter), idempotent second run = zero new rows/artefacts/diff except `generated_at` (SC-001/SC-003)
- [ ] T024 [US1] Run `pytest codeconv/tests/test_planagents_next.py test_planagents_lifecycle.py test_planagents_artefact_validation.py test_planagents_orchestration_mock.py` → green; then end-to-end `/codeconv-planagents --data-dir C:/pglite/research/glpnet` on the live baseline; verify every leaf has a checked-in artefact + completed row (US1 Independent Test)

**Checkpoint**: US1 green → MVP delivered (leaves planned end-to-end, idempotent). US2/US3/US4/US5 independent from here.

---

## Phase 4: User Story 2 — Advance the planning frontier (Priority: P2)

**Goal**: Re-invocation re-computes plan-readiness from `dart_plans`; next-`topo_level` files whose every SCC-external dep is `planned` become plan-ready.

**Independent Test**: Chain A→B→C, empty `dart_plans`: run 1 plans only A; after A completes, run 2 plans B; after B, run 3 plans C.

- [ ] T025 [US2] Add `codeconv/tests/test_planagents_frontier.py` (`@needs_bridge`) with a synthetic A→B→C fixture in `specs/017-conversion-plan-agents/scripts/chain_fixture/`: assert run-1 selects only A; A `plan_in_progress` ⇒ B still NOT ready (US2 AC2); A `planned` ⇒ B ready, C not (US2 AC1/AC3)
- [ ] T026 [US2] Verify/extend `readiness.select_next` so a re-run after partial completion advances exactly one level (no recompute of depgraph — FR-003; consume `dart_depgraph` only); fix any ordering defect surfaced by T025
- [ ] T027 [US2] Add `codeconv/tests/test_planagents_sc002.py` (`@needs_bridge`): the SQL self-join over `codeconv.dart_imports × dart_plans × dart_depgraph` proves no cross-SCC `(A→B)` had A planned before B `plan_completed` on the live baseline after a full pass (SC-002)
- [ ] T028 [US2] Run `pytest codeconv/tests/test_planagents_frontier.py test_planagents_sc002.py` → green

**Checkpoint**: US2 green → dependency-ordered frontier advance proven.

---

## Phase 5: User Story 3 — Plan a circular-import group as a coordinated batch (Priority: P2)

**Goal**: A multi-file SCC is planned as one batch — each member gets its own artefact cross-referencing siblings; downstream blocked until every member `planned`.

**Independent Test**: 3-file SCC A↔B↔C + downstream D→A: one run plans A,B,C as a batch (3 artefacts, each §7 referencing the other two, same `cycle_group_id`); D not plan-ready until all of A,B,C `plan_completed`.

- [ ] T029 [P] [US3] Create the SCC fixture `specs/017-conversion-plan-agents/scripts/scc_fixture/` (A↔B↔C + D→A) — mirror feature-015 `scripts/cycle_fixture/` layout
- [ ] T030 [US3] Add `codeconv/tests/test_planagents_scc_batch.py` (`@needs_bridge`, mocked agents): all three members emitted in one `next` unit with shared `cycle_group_id` + full `scc_siblings`; each artefact has §7 listing the other two; D NOT ready until all three `plan_completed`; partial-batch (A,B done, C in progress) ⇒ D still blocked + C resumable, A/B not re-spawned (US3 AC1/AC2/AC3, edge "SCC member subset already planned", SC-006)
- [ ] T031 [US3] Implement/verify SCC-unit handling in `workflow.next` + the SKILL.md SCC protocol from `contracts/agent_orchestration.md` § "SCC coordinated-batch protocol" (one agent per member, siblings passed, loop does not advance past the SCC until all members completed); fix defects from T030
- [ ] T032 [US3] Run `pytest codeconv/tests/test_planagents_scc_batch.py` → green

**Checkpoint**: US3 green → cycles handled as coordinated batches.

---

## Phase 6: User Story 4 — Escalate non-incremental gaps to the engineer (Priority: P2)

**Goal**: A consistency-pass gap that is not verbatim-derivable becomes a structured escalation (no guess); verbatim-derivable gaps are auto-fixed in-artefact; all open escalations aggregate into one report; escalations gate conversion, not planning.

**Independent Test**: A tombstone whose source uses an unmapped Dart construct ⇒ artefact §6 has an open `### E1`, file still `planned`, `open_escalation_count>0`, report lists it, no silently-chosen mapping.

- [ ] T033 [US4] Implement `workflow.aggregate-escalations` per `contracts/planagents_cli.md` § aggregate-escalations: walk `.codeconv/conversion-plans/**.dart.md`, parse `## 6. Escalations` open entries, write `.codeconv/conversion-plans/_escalations-report.md` (overridable, atomic rename, ordered `(path, E#)`, back-links); `--dry-run` writes nothing
- [ ] T034 [US4] Encode the FR-008 escalate-don't-guess boundary verbatim into the SKILL.md planning prompt contract (verbatim-derivable-only auto-fix; language-semantics/unwritten-mapping/scope-growth ⇒ escalate); ensure `plan-completed --escalations <n>` records the open count into `dart_plans.open_escalation_count` and `--replan` carries forward prior open escalations with a "carried from <prior generated_at>" note (R9 / artefact format § idempotence)
- [ ] T035 [US4] Add `codeconv/tests/test_planagents_escalations.py` (`@needs_bridge`, mocked agent emitting (a) a pre-specified-incremental fixed gap and (b) a non-incremental escalation): assert (a) ⇒ "fixed (pre-specified, incremental)" note, no escalation, `open_escalation_count=0`; (b) ⇒ open `### E1`, `open_escalation_count=1`, file still `planned` & unblocks downstream planning (FR-017) but flagged conversion-blocking (index query); `aggregate-escalations` report contains (b) and not (a); zero un-escalated unresolved gaps (US4 AC1/AC2/AC3, SC-005)
- [ ] T036 [US4] Run `pytest codeconv/tests/test_planagents_escalations.py` → green

**Checkpoint**: US4 green → no-silent-guessing discipline enforced + engineer report produced.

---

## Phase 7: User Story 5 — Delegate web/external research to a separate agent (Priority: P3)

**Goal**: When a planning agent needs external info it issues a research request; a SEPARATE research sub-agent performs it; findings + verbatim external requests embedded in artefact §4 with provenance; failure/timeout ⇒ escalation, plan completes best-effort.

**Independent Test**: A tombstone needing an external-API mapping triggers a separate research agent; §4 contains findings + provenance + verbatim requests; the planning agent cites them rather than re-deriving.

- [ ] T037 [US5] Encode the separate-research-agent contract into SKILL.md from `contracts/agent_orchestration.md` § "Research sub-agent prompt contract": planning agent MUST NOT inline-research; skill spawns a distinct research Agent; findings + provenance + verbatim external requests returned and embedded in §4; raw-snippet transmission permitted (Clarification Q4)
- [ ] T038 [US5] Encode research-failure handling (Clarification Q6 / R10): failure/timeout/empty ⇒ planning agent records `### E… research unavailable`, completes best-effort, `plan-completed --escalations ≥1`; MUST NOT stall `plan_in_progress`, MUST NOT guess
- [ ] T039 [US5] Add `codeconv/tests/test_planagents_research.py` (`@needs_bridge`, mocked agents): (a) no-research file ⇒ §4 = "none required", no research agent spawned (US5 AC1); (b) research-needed ⇒ exactly one separate research agent, §4 has findings+provenance+verbatim request, planning agent cites them (US5 AC2); (c) research-fail ⇒ open escalation, file `planned` not stalled, no guessed mapping (edge case)
- [ ] T040 [US5] Run `pytest codeconv/tests/test_planagents_research.py` → green

**Checkpoint**: US5 green → research delegation auditable.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T041 [P] Add `codeconv/tests/test_planagents_stale.py` (`@needs_bridge`): edit a planned file's `.dart` so `dart_files.sha256 ≠ sha256_of_dart_at_plan_start`; `status` reports it stale; default re-run does NOT re-plan it; `--replan <sel>` UPDATEs the row + new artefact carries forward prior open escalations (FR-015 / R9)
- [ ] T042 [P] Add `codeconv/tests/test_planagents_dry_run.py` (`@needs_bridge`): `/codeconv-planagents --dry-run` and each subcommand `--dry-run` spawn no agents and leave `git status` clean + `SELECT count(*) FROM codeconv.dart_plans` unchanged (SC-008)
- [ ] T043 [P] Add `codeconv/tests/test_planagents_stamp_rebuild.py` (`@needs_bridge`): `stamp-tombstones` is byte-identical on re-run (SC-003); `rebuild-plans-from-tombstones` reconstructs `dart_plans` from YAML after a simulated DB wipe (sha re-snapshot caveat documented); `--dry-run` writes nothing (FR-013)
- [ ] T044 Run the full suite `codeconv/.venv/Scripts/python.exe -m pytest codeconv/tests/`; confirm zero regressions vs the T002 baseline; confirm the four new tombstone keys appear (in order, after the six feature-015 keys) and the 14 pre-existing keys are byte-identical to a pre-feature tombstone snapshot
- [ ] T045 Verify SC-009 on the live baseline: after a full `/codeconv-planagents` pass every non-orphaned inventoried file is either `planned` (possibly with recorded escalations) or explicitly behind a recorded escalation/stale flag — no file in an undiagnosed state; record the result in `specs/017-conversion-plan-agents/quickstart.md` (or a verification note)
- [ ] T046 Update `docs/known-issues.md` if any PGLite/agent-orchestration gotcha surfaced; update CHANGELOG per `docs/VERSIONING.md`; confirm `CLAUDE.md` SPECKIT marker points at `specs/017-conversion-plan-agents/plan.md`

---

## Dependencies & Story Completion Order

```
Phase 1 (Setup) ─► Phase 2 (Foundational: migration + readiness + _FIELD_ORDER) ─► ┐
                                                                                    ├─► Phase 3 US1 (P1, MVP)
                                                                                    │        │
                              ┌─────────────────────────────────────────────────────┘        ▼
                              ├─► Phase 4 US2 (P2 frontier)     ── independent of US3/US4/US5
                              ├─► Phase 5 US3 (P2 SCC batch)    ── independent of US2/US4/US5
                              ├─► Phase 6 US4 (P2 escalations)  ── independent of US2/US3/US5
                              └─► Phase 7 US5 (P3 research)     ── independent of US2/US3/US4
                                                                                    │
                                                                                    ▼
                                                                          Phase 8 (Polish)
```

- **Foundational (Phase 2) blocks everything** — migration + pure readiness predicate + `_FIELD_ORDER` extension.
- **US1 is the MVP** and is the only story that must precede the others (it builds `workflow.py`/`SKILL.md`/`artefact.py` the rest extend). US2–US5 are mutually independent and can be implemented/verified in any order after US1.
- Tests precede implementation within each story (DISCIPLINE.md §2.4).

## Parallel Execution Examples

- **Phase 2**: T007 (`_FIELD_ORDER`), T008 (subpackage skeleton), T010 (pure readiness tests) are `[P]` — different files, no interdependency. T006 (migration) and T009 (readiness impl) gate T011/T012.
- **Phase 3 (US1)**: T017 (`tombstone_writer.py`) and T018 (`artefact.py`) are `[P]` (different files) and can proceed alongside T013–T016 (`workflow.py`) once T009 lands.
- **Phase 8**: T041/T042/T043 are `[P]` (independent test files).

## Implementation Strategy

1. **MVP first**: Phases 1–3 → leaves planned end-to-end, idempotent, ≤7 concurrent. Ship/checkpoint here.
2. **Incremental P2**: add US2 (frontier), US3 (SCC), US4 (escalations) in any order — each independently testable.
3. **P3**: US5 (research delegation) last.
4. **Polish**: stale/replan, dry-run, stamp/rebuild, full-suite regression, SC-009 audit.
5. Baseline-before / re-test-after every code task (DISCIPLINE.md §2.2); `--data-dir C:/pglite/research/glpnet` on every bridge-touching command (exFAT).
