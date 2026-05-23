---
description: "Task list for codeconv-codegen implementation"
---

# Tasks: codeconv-codegen — GEPA/DSPy-optimized Dart→C#/.NET code generation

**Input**: `specs/019-codeconv-codegen/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)
**Tests**: INCLUDED — the codeconv package (012–018) is test-backed (pytest, `@needs_bridge`, baseline-before/after per DISCIPLINE §2.2).
**Organization**: by user story (US1–US4) for independent implementation/testing. All paths relative to repo root `D:\BSTDEV\research\GLP\GLPnet`.

## Format: `[ID] [P?] [Story?] Description (file path)`

---

## Phase 1: Setup

- [ ] T001 Baseline: run `pytest codeconv/tests/ -q` and record green/known-fail counts before any change (DISCIPLINE §2.2)
- [ ] T002 Confirm prereqs: `dotnet --version` (≥10), `codeconv/.venv` has `dspy/gepa/litellm/openai`, bridge `--data-dir C:/pglite/research/glpnet` reachable (`codeconv doctor`)
- [ ] T003 [P] Create empty package skeletons `codeconv/src/codeconv/tools/codegen/__init__.py` and `codeconv/src/codeconv/tools/codegen_opt/__init__.py` (Typer apps, bare = status / help)

## Phase 2: Foundational (blocking — schema, state, shared reads)

- [ ] T004 Create migration `codeconv/src/codeconv/db/migrations/versions/0007_codegen.py` (revision "0007", down_revision "0006") with `dart_codegen` DDL per contracts/codegen_schema.md (CREATE TABLE IF NOT EXISTS, codeconv schema only)
- [ ] T005 [P] `test_migration_0007_single_head.py` — `alembic upgrade head` reaches one head; 0 dup/multi-head (codeconv/tests/)
- [ ] T006 [P] `test_schema_isolation` for 0007 — Alembic authors no public/dbos object (codeconv/tests/)
- [ ] T007 Extend `codeconv/src/codeconv/tools/discover/tombstone.py` `_FIELD_ORDER` (append-only, AFTER plan keys): codegen_started_at, codegen_completed_at, target_cs_path, build_status, codegen_open_escalation_count
- [ ] T008 [P] `test_tombstone_codegen_stamp_rebuild.py` — append-only round-trip idempotent (codeconv/tests/)

## Phase 3: User Story 1 — Generate compilable lib/ C#, batched in dep order (P1) — MVP

**Goal**: produce build-passing C# for production `lib/`, dependency-ordered, build-gated, escalate-don't-guess.
**Independent test**: codegen a leaf + dependents; each `out/csharp/*.cs` builds; re-run = no spurious change.

- [ ] T009 [P] [US1] `codeconv/src/codeconv/tools/codegen/readiness.py` — pure codegen-readiness predicate (deps codegen-complete; SCC=one batch) over 015 depgraph
- [ ] T010 [P] [US1] `test_codegen_readiness.py` — predicate + SCC batch (pure) (codeconv/tests/)
- [ ] T011 [P] [US1] `codeconv/src/codeconv/tools/codegen/artefact.py` — produced-`.cs` path + validate-IS-real-C# (inverse of convspec rule) per contracts/codegen_artifact_format.md
- [ ] T012 [US1] `codeconv/src/codeconv/tools/codegen/buildgate.py` — deterministic `dotnet build` invoke + error parse → pass/fail (contracts/metric_contract.md hard gate)
- [ ] T013 [P] [US1] `test_codegen_buildgate.py` — build pass/fail parse on a tiny fixture .csproj (dotnet-gated/skip) (codeconv/tests/)
- [ ] T014 [US1] `codeconv/src/codeconv/tools/codegen/prompt.py` — load optimized-prompt artifact (or baseline if absent) for the codegen sub-agent
- [ ] T015 [US1] `codeconv/src/codeconv/tools/codegen/workflow.py` — `run_codegen_step`/`run_codegen_ingest`: two-phase `dart_codegen` write + build gate + escalations; `needs_agent_work` sentinel; `--respec` drift re-open (contracts/dbos_codegen_stage.md)
- [ ] T016 [US1] `codeconv/src/codeconv/tools/codegen/__init__.py` — Typer commands `status`/`next`/`ingest`/`retry`/`aggregate-escalations` (contracts/codegen_cli.md); auto-discovered
- [ ] T017 [P] [US1] `test_codegen_frontier.py` — @needs_bridge: topo/SCC order; dependency-before invariant (FR-002)
- [ ] T018 [P] [US1] `test_codegen_ingest_step.py` — @needs_bridge: deterministic ingest; build gate; needs_agent_work; replay-safe (FR-003/005)
- [ ] T019 [P] [US1] `test_codegen_escalations.py` — @needs_bridge: escalate-don't-guess; report counts match state (FR-007/009)
- [ ] T020 [US1] `.claude/skills/codeconv-codegen/SKILL.md` — codegen sub-agent prompt contract + frontier driver loop (contracts/agent_orchestration.md); emit-real-C#, escalate-don't-guess, ≤7 concurrency, SCC batch
- [ ] T021 [US1] `.codeconv/conversion-code/` tree + `aggregate-escalations` writes `_escalations-report.md` (FR-009)

## Phase 4: User Story 2 — Active GEPA/DSPy optimization (P2)

**Goal**: optimize the codegen instructions offline so they score ≥ baseline on a held-out set; budget-capped.
**Independent test**: optimizer (mocked LM) beats baseline on a fixture metric; budget cap honored.

- [ ] T022 [P] [US2] `codeconv/src/codeconv/tools/codegen_opt/program.py` — dspy.Module signature (plan+convspec+dep-interfaces+idioms → C#)
- [ ] T023 [P] [US2] `codeconv/src/codeconv/tools/codegen_opt/metric.py` — composite metric (build hard-gate, 0.6/0.4, human feed) reusing buildgate.py (contracts/metric_contract.md)
- [ ] T024 [P] [US2] `codeconv/src/codeconv/tools/codegen_opt/dataset.py` — held-out eval/train split over plans/convspecs
- [ ] T025 [US2] `codeconv/src/codeconv/tools/codegen_opt/optimize.py` — GEPA driver + HARD budget cap; serialize best instructions
- [ ] T026 [US2] `codeconv/src/codeconv/tools/codegen_opt/__init__.py` — Typer `optimize`/`eval`/`export-prompt`/`show`; OPENAI_API_KEY from env; NOT durable-registered (contracts/codegen_opt_cli.md)
- [ ] T027 [US2] Optimized-prompt artifact writer → `.codeconv/codegen-prompt/optimized.md` (provenance block + instructions) (contracts/codegen_artifact_format.md §B)
- [ ] T028 [P] [US2] `test_codegen_prompt_artifact.py` — (de)serialize round-trip; production load (pure)
- [ ] T029 [P] [US2] `test_codegen_opt_metric_mocked.py` — GEPA beats baseline on fixture metric w/ MOCKED LM; budget cap honored (FR-004/SC-003/SC-006)
- [ ] T030 [US2] `.claude/skills/codeconv-codegen-opt/SKILL.md` — offline optimizer driver; LM-backend resolution; budget cap

## Phase 5: User Story 3 — Human-review gate + testing feedback (P2)

**Goal**: batch promotion gated on build + sampled human review; Increment 2 adds ported-test pass-rate.
**Independent test**: batch blocks promotion until human gate met; failing gate prevents promotion.

- [ ] T031 [P] [US3] `codeconv/src/codeconv/tools/codegen/review.py` — record sampled review + promotion gate (100% build + median≥4/5)
- [ ] T032 [US3] Add `record-review`/`promote-batch` commands to codegen `__init__.py` (contracts/codegen_cli.md)
- [ ] T033 [P] [US3] `test_codegen_metric.py` — composite-metric math (hard-gate floor, 0.6/0.4, median≥4/5) (pure)
- [ ] T034 [P] [US3] `test_codegen_review_gate.py` — @needs_bridge: sampled review recording + promotion gate (FR-006)
- [ ] T035 [US3] Human-review loop in `codeconv-codegen/SKILL.md` (sample max(3,20%), record, promote, feed free-text to optimizer dataset)
- [ ] T036 [US3] Increment 2: extend `buildgate.py` with `dotnet test` + `test_pass_rate` parse; metric switches to 0.6/0.4 (FR-012)
- [ ] T037 [US3] Enable test-tree (`test/`) files in `readiness.py`/`next` for Increment 2

## Phase 6: User Story 4 — Resumable, idempotent, escalation-aware (P3)

**Goal**: resume skips completed; re-run no diff; stale drift detected; escalations block only their file.
**Independent test**: interrupt + resume skips done files; re-run no diff; stale only re-generates under retry.

- [ ] T038 [US4] Durable: register codegen step in `codeconv/src/codeconv/durable/steps.py`; add `codegen` stage after `plan` in `durable/workflows.py` (contracts/dbos_codegen_stage.md)
- [ ] T039 [P] [US4] `test_durable_codegen_stage.py` — @needs_bridge: codegen stage after plan; needs_agent_work surfaced; replay-safe (R3)
- [ ] T040 [P] [US4] `test_codegen_resume_idempotent.py` — @needs_bridge: resume skips completed; re-run no diff; stale drift + retry (FR-008/SC-005)
- [ ] T041 [US4] Stale-drift detection + `retry` re-open in workflow.py (sha vs dart_files); status surfaces `stale`

## Phase 7: Polish & Cross-Cutting

- [ ] T042 [P] `test_capability_preservation`-analog — every 015/016/017/018 entrypoint still reachable (no regression)
- [ ] T043 [P] Update `codeconv/README` / tool docs for the codegen + codegen-opt tools
- [ ] T044 Re-run `pytest codeconv/tests/ -q`; confirm green vs T001 baseline; commit
- [ ] T045 RESOLVE R11 git policy (commit `out/csharp/` vs gitignore) — gated by `/buildkit-analyze` + Gabi before bulk generation
- [ ] T046 Smoke: `codeconv codegen status` + `codegen-opt show` ≤5 s; quickstart.md Flow C end-to-end on a 2–3 file dependency-closed subset

## Dependencies & order

- Setup (T001–T003) → Foundational (T004–T008) → US1 (T009–T021, MVP) → US2 (T022–T030) ∥ US3 (T031–T037) → US4 (T038–T041) → Polish.
- US2 and US3 are largely independent after US1 (US2 = offline optimizer; US3 = production gate); both consume US1's buildgate/metric.
- US4 (durable) depends on US1's `workflow.py`.
- **MVP = US1** (T001–T021): build-passing lib/ C# with escalate-don't-guess, no optimizer/review-gate yet.

## Parallel example (US1)

`T009, T010, T011, T013, T017, T018, T019` are `[P]` (distinct files / pure or independent fixtures). Serial within US1: T012→T015→T016 (buildgate→workflow→CLI), T020 after the CLI exists.

## Implementation strategy

Ship US1 first (compiling lib/ C# + escalations) as the demonstrable MVP. Layer US2 (optimizer) and US3 (human/test gate) next. US4 (durability) and Polish last. **T045 (the R11 git-policy gate) MUST be resolved before bulk `out/csharp/` generation.**
