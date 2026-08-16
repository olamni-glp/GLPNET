# Tasks: Type-checker body-atom moding — accept head-flipped readers at declared reader positions

**Input**: Design documents from `/specs/076-typechecker-body-atom-moding/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/body-atom-moding-rule.md, quickstart.md

**Tests**: REQUIRED — spec FR-007 mandates regression tests (2 positive + 1 negative, REPL suite) plus Dart unit tests. Test programs are written FIRST and verified to fail/pass as expected pre-change.

**Organization**: grouped by user story; US1 is the MVP. All checker code changes live in `glp_runtime/lib/analysis/type_checker/` — the two source files are shared across stories, so story phases are sequential (single developer, shared files), with [P] only where files are disjoint.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup (baseline discipline)

**Purpose**: Known-good baseline per CLAUDE.md Test Protocol before any change

- [X] T001 Baseline: run `DART=/d/BSTDEV/tools/dart-sdk/bin/dart.exe bash test/run_all_tests.sh` and `cd glp_runtime && dart test` from repo root; confirm green; record counts in specs/076-typechecker-body-atom-moding/baseline.md; checkpoint commit (files by name)

---

## Phase 2: Foundational (blocking prerequisites)

**Purpose**: The §1.14 gate and the spec-first amendment — MUST complete before ANY implementation task

**⚠️ CRITICAL**: T002 is the HARD GATE (Constitution IV-a; spec FR-001/FR-009/SC-005). No task in Phase 3+ may start before T002 and T003 are done.

- [X] T002 §1.14 ruling: present plan.md "§1.14 Semantics Proposal" to Gabi verbatim; on express approval record it as (a) a Clarifications entry in specs/076-typechecker-body-atom-moding/spec.md, (b) `buildkit-marathon trace --feature type-checker-body-atom-moding-accept-head-flipped-readers-unblock-2 --subject "1.14 body-atom licensing rule" --decision accept --evidence "<Gabi's words>"`, (c) satisfy the `1.14-ruling` discharge item (`mdi-019ff0c2511d-de5e7f0c-93cd-4f78-ac6a-37bcf6b4e5c0`); if approval is withheld or the rule differs, STOP per FR-009 and re-enter plan
- [X] T003 Amend authoritative spec `docs/type system/well-typed-clause.md`: add the approved licensing rule to Definition 5.7 clause 2 exactly as approved (quote the approved wording; reference, don't duplicate, contracts/body-atom-moding-rule.md); commit before any code change

**Checkpoint**: rule approved + authoritative spec amended — implementation may begin

---

## Phase 3: User Story 1 — Assignment with a head-flipped variable type-checks (P1) 🎯 MVP

**Goal**: the Issue-4 clause shape loads and runs via `=` directly

**Independent Test**: `echo -e 'load programs/tests/typed/issue4_bind_later.glp\n:quit' | dart run glp_runtime/bin/glp_repl.dart` loads with zero type errors

- [X] T004 [US1] Write positive regression program P1 `programs/tests/typed/issue4_bind_later.glp` (Issue-4 shape: `procedure bind_later(_).`, clause binding via `=`, plus the workaround form `done(Done)` alongside, both with full type/procedure declarations); verify it currently FAILS to load with "Variable mode mismatch"
- [X] T005 [US1] Thread head-occurrence context into body-atom leaf checking: extend `_checkBodyAtomWithTerm`/`_checkModedTermPerArg` call path in glp_runtime/lib/analysis/type_checker/well_typed_clause.dart to pass `callerVarTypes` (head entries only) down to leaf consistency; do NOT touch modedHead/producedTerm construction or any head-occurrence record
- [X] T006 [US1] Implement the licensing predicate in glp_runtime/lib/analysis/type_checker/program_dfa.dart variable-leaf consistency (contract row 3 ONLY: surface writer at ↓ licensed by head reader-pair with structural mode produce); rows 1-2 and 4-6 verdicts unchanged
- [X] T007 [US1] Verify P1 loads and the goal runs; wire `issue4_bind_later.glp` into test/run_all_tests.sh Section B; commit
- [X] T008 [US1] Dart unit tests for the licensing predicate in glp_runtime/test/ (beside existing type-checker tests): licensed acceptance (row 3), all four unchanged-verdict rows, head-record non-rewrite (bind-pattern head-head pair still checked by `_areDualTypesWithReason` byte-identically)

**Checkpoint**: Issue-4 shape green; full suites re-run green vs baseline (SC-001/SC-002 partial)

---

## Phase 4: User Story 2 — The rule is general, not an `=`-special-case (P2)

**Goal**: any procedure with a declared reader position accepts the licensed shape, at any depth

**Independent Test**: load P2 program (user-defined procedure, nesting depth ≥ 2) — type-checks and runs

- [X] T009 [P] [US2] Write positive regression program P2 `programs/tests/typed/head_flip_general.glp`: user-defined `procedure sink(T?)` receiving the licensed writer at top level AND a variant with the licensed occurrence at depth ≥ 2 inside a structure argument (flip composed per §2A); full declarations; wire into test/run_all_tests.sh Section B
- [X] T010 [US2] Verify the parameterized-procedure path: unit test in glp_runtime/test/ exercising licensing after Case B call-site instantiation (`_inferConcreteDecl` path) and confirming the existing inference-failure skip paths (well_typed_clause.dart:525-535) are behaviorally unchanged

**Checkpoint**: generality demonstrated; suites green

---

## Phase 5: User Story 3 — Ill-moded programs still rejected precisely (P2)

**Goal**: no over-acceptance; diagnostics stay precise (FR-005/FR-006)

**Independent Test**: N1 program fails type-check with the contract row-4 diagnostic; Section C stays green

- [X] T011 [P] [US3] Write negative regression program N1 `programs/tests/typed/head_flip_negative.glp`: writer-at-↓ occurrences with NO licensing head hole (pair absent; pair in body; pair at a head-↓ position — three clauses); wire into test/run_all_tests.sh Section C expecting type-check failure
- [X] T012 [US3] Diagnostics: extend the mode-mismatch message for unlicensed writer-at-↓ with the absent-license context ("no head-flipped reader pair in head licenses this occurrence") in glp_runtime/lib/analysis/type_checker/program_dfa.dart; unit test asserts variable name, position, expected/actual modes, and the context phrase; verify every pre-existing Section C negative test still rejects for its original reason

**Checkpoint**: all three stories independently verified; suites green

---

## Phase 6: Polish & Cross-Cutting

- [X] T013 [P] Close known-issues Issue 4 in docs/known-issues.md: Status → Fixed with resolution + feature/commit reference; correct the stale prelude claim (declarations live in programs/self.glp; built-in type prelude empty) per curator doc-corrections sweep
- [x] T014 Final verification: both suites green vs T001 baseline (zero regressions, SC-002); counts recorded in specs/076-typechecker-body-atom-moding/baseline.md; commit (files by name)
- [x] T015 Run quickstart.md end-to-end as written (repro line now loads; gate check shows `1.14-ruling` satisfied); fix any doc drift found

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2**: baseline before gate work (checkpoint discipline)
- **T002 BLOCKS EVERYTHING in Phases 3-6** (hard §1.14 gate); T003 follows T002, precedes all code
- **US1 (Phase 3)**: T004 (test-first) → T005 → T006 → T007 → T008; T005/T006 touch different files but T006 consumes T005's threaded context — sequential
- **US2 (Phase 4)**: T009 [P] may be authored any time after T003 (own file); T010 requires T006
- **US3 (Phase 5)**: T011 [P] may be authored any time after T003 (own file); T012 requires T006 and precedes T014
- **Polish**: T013 [P] after US1 verified; T014 after ALL prior tasks; T015 last

### Parallel Opportunities

- T009 and T011 (distinct new .glp files) are parallel with each other and with Phase 3 coding once T003 is done
- T013 (docs) parallel with Phase 4/5 coding
- Everything else is sequential: two shared checker source files, one developer

## Implementation Strategy

MVP = Phases 1-3 (US1): gate, spec amendment, Issue-4 shape green — ship-worthy increment. Phases 4-5 add generality evidence and the over-acceptance guard the spec requires before ship (FR-005/FR-007 are not optional), so the feature ships after Phase 6, not at MVP. Commit after each task or logical group; stop at any checkpoint to validate.
