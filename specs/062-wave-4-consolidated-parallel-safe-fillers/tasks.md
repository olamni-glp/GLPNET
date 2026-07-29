<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: Wave 4 consolidated — parallel-safe fillers

**Feature**: `062-wave-4-consolidated-parallel-safe-fillers` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Organized by user story (priority order). Each user story is an independently testable, shippable
slice (spec FR-010). Baseline before / re-test after every slice (DISCIPLINE §2.2). `[P]` = may run
in parallel (different files, no incomplete-task dependency).

## Phase 1: Setup

- [ ] T001 Capture wave-start baselines and record in the marathon run `mrun-7b8d08899272`: REPL suite (`bash test/run_all_tests.sh`), codeconv pytest, and the C#/Gleam suites touched by US3
- [ ] T002 [P] Create feature-local `specs/062-wave-4-consolidated-parallel-safe-fillers/research/` for US2 studies
- [ ] T003 [P] Create feature-local `specs/062-wave-4-consolidated-parallel-safe-fillers/proposals/` for US5 §1.14 proposals

## Phase 2: Foundational (blocking prerequisites)

- [ ] T004 Confirm the US3 engine line with the operator/lead (research R-3 assumes the C#/.NET line); record the confirmation before any US3 code
- [ ] T005 Confirm access path to the FCP source + sibling GLP repo for US5 semantics (research R-5); flag on the scheduler board if off-host access is needed

## Phase 3: User Story 1 — Depgraph tooling (Priority: P1) 🎯 MVP

**Goal**: mark-and-recompute + cross-run trend reporting in the codeconv depgraph tool.
**Independent test**: fixture recompute touches only the marked subgraph; trend report byte-identical on re-run.

- [ ] T006 [P] [US1] Add pytest fixtures for a small multi-file project with a recorded depgraph run in `codeconv/tests/`
- [ ] T007 [US1] Implement `mark-and-recompute` subcommand (mark set → dirty transitive dependents → recompute only dirty) in `codeconv/src/codeconv/tools/depgraph/`
- [ ] T008 [US1] Implement `trends` view (≥2 runs → deterministic secret-redacted per-metric deltas) in `codeconv/src/codeconv/tools/depgraph/`
- [ ] T009 [P] [US1] Test: mark-and-recompute recomputes only the marked subgraph; unknown paths reported, nothing fabricated (exit 1) — `codeconv/tests/`
- [ ] T010 [P] [US1] Test: trends byte-identical on unchanged inputs; `<2` runs refused with clear message — `codeconv/tests/`
- [ ] T011 [US1] Verify additive-only persistence (no new Alembic head; single-head test still passes) and re-run codeconv pytest green vs T001 baseline

## Phase 4: User Story 2 — Feasibility studies (Priority: P2)

**Goal**: three decision-ready written studies. **Independent test**: each states go/no-go + risks.

- [ ] T012 [P] [US2] Write `research/research-programme-and-llvm-feasibility.md` (staged programme + LLVM feasibility: question, options, go/no-go, staged plan, risks)
- [ ] T013 [P] [US2] Write `research/cpp-engine-feasibility.md` (C++ engine+scheduler+compiler feasibility)
- [ ] T014 [P] [US2] Write `research/many-instances-shared-static-memory-cooperative-scheduling.md` (feasibility + recommendation)

## Phase 5: User Story 3 — Engine & transport (Priority: P2)

**Goal**: hardened compiled-IL-on-the-wire + factor-out-compiler, multi-accept transport, ZMQ base.
**Independent test**: ≥2 clients served; remote exec == local; malformed/version/failure rejected safely; ZMQ round-trip. Depends on T004.

- [ ] T015 [US3] Factor the compiler so compiled IL is produced independently of execution (engine line per R-3)
- [ ] T016 [US3] Implement the compiled-IL wire envelope per `contracts/compiled-il-wire-envelope.md` (il_version, compiled_form, integrity_digest, source_metadata)
- [ ] T017 [US3] Implement receiver: compile-on-A → send → execute-on-B, result equals local execution
- [ ] T018 [US3] Harden receiver (FR-005a): reject unknown/incompatible il_version + digest mismatch with diagnostic; mid-transfer failure leaves engine state unchanged
- [ ] T019 [P] [US3] Implement `multi-accept` transport extension (≥2 concurrent clients, none dropped)
- [ ] T020 [P] [US3] Implement `zmq-receiver-base` + `zmq-sender-base` behind the transport seam
- [ ] T021 [US3] Tests: multi-accept ≥2 clients; compiled-IL happy path + hardening (malformed/version/failure); ZMQ round-trip
- [ ] T022 [US3] Re-run the C#/engine suite green vs T001 baseline (no regression)

## Phase 6: User Story 4 — GLP multi-client control program (Priority: P3)

**Goal**: a GLP program coordinating N clients. **Independent test**: type-checks, compiles, runs to documented outcome.

- [ ] T023 [US4] Write the multi-client control program (type + procedure decls, SRSW-valid, `Channel(In,Out)`) in `programs/tests/typed/`
- [ ] T024 [US4] Load + run via the REPL pipeline; confirm type-check/compile/run to documented succeeded|suspended outcome
- [ ] T025 [US4] Add a REPL regression case in `test/run_all_tests.sh` (Section A/F); re-run suite green vs T001 baseline

## Phase 7: User Story 5 — §1.14 language items (Priority: P3, PROPOSAL-GATED) ⚠

**Goal**: implement abandon-operation (FCP-exact) + nested-structure-head-matching, each behind a
written §1.14 proposal. **Discipline**: proposal (sourced) BEFORE implementation; extend, never
remove, `_ClauseVar`/`_TentativeStruct`/fallbacks (IV-b). Depends on T005.

- [ ] T026 [US5] Write §1.14 proposal `proposals/abandon-operation.md` per `contracts/section-1-14-proposal-template.md`, semantics sourced from FCP (`kernels.c`/`emulate.c`); cite the 2026-07-29 approval
- [ ] T027 [US5] Write §1.14 proposal `proposals/nested-structure-head-matching.md`, semantics sourced from typed-GLP manual + sibling GLP runtime spec
- [ ] T028 [US5] STOP-gate: present both proposals; if a semantic snag surfaces, stop-and-report (Bug/Language protocol) — do not implement around it
- [ ] T029 [US5] Implement abandon-operation in `glp_runtime/lib/` per its proposal (extend internals; no removals)
- [ ] T030 [US5] Implement nested-structure HEAD-phase matching in `glp_runtime/lib/` (`_TentativeStruct`/`_ClauseVar` extended)
- [ ] T031 [P] [US5] Positive + negative REPL regression cases in `test/run_all_tests.sh` (Sections A/C) for both items
- [ ] T032 [P] [US5] Dart unit coverage in `glp_runtime/test/` for both items
- [ ] T033 [US5] Re-run REPL suite + `dart test` green vs T001 baseline (SRSW + type checker clean)

## Phase 8: Polish & cross-cutting

- [ ] T034 [P] Advance each delivered roadmap sub-item to its terminal state; confirm no item silently dropped (SC-008)
- [ ] T035 [P] `/bk-codify` any coordination/pipeline wins + improvements per the directive's GEPA/DSPy meta-task
- [ ] T036 Post stage-seam UPDATEs to the fleet lead at each phase completion (specify..close) with receipts (directive #4)
- [ ] T037 Final full-suite green sweep across all touched runtimes; then hand to `/bk-analyze` → `/bk-implement`

## Dependencies & order

- Setup (T001–T003) → Foundational (T004–T005) → user stories.
- US1, US2, US4 are independent and may proceed in parallel after Setup.
- US3 depends on T004 (engine-line confirm). US5 depends on T005 (semantic-source access) and the
  T028 proposal STOP-gate.
- MVP = US1 (Phase 3) alone: a complete, shippable increment.

## Parallel opportunities

- T002/T003 together; T012/T013/T014 (three studies) together; T019/T020 together; T031/T032 together.
- Across stories: US1 (T006–T011), US2 (T012–T014), and US4 (T023–T025) can run concurrently.
