<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: Front-end goal-term acceptance completeness

**Feature**: `101-goal-term-acceptance` · **Branch**: `101-goal-term-acceptance` · **Stage**: tasks
**Spec**: `specs/101-goal-term-acceptance/spec.md` · **Plan**: `specs/101-goal-term-acceptance/plan.md`
**Marathon run**: `mrun-fb28dd92afe0` *(keyed on the ROADMAP SLUG, not this branch name)*

---

## 0 · 🔴 STATUS OF THIS DOCUMENT — WRITTEN AFTER THE WORK, AND SAYING SO

This `tasks.md` was authored **after** the implementation landed, not before it. The stage
recorder shows the drift honestly (`plan: recorded not_started but plan.md exists`), and this
file does not pretend otherwise: every task below is marked with what was **actually** done and
in **which commit**, so the record is auditable rather than tidy.

Writing it retrospectively is the deviation. Writing it *falsely* — as if it had guided the
work — would be the defect, so it is not done that way here.

**One task is NOT complete, and it is a spec defect rather than an implementation gap: T024.**

---

## Phase 1 · Setup

- [x] T001 Confirm the fixture module exists and loads through the full pipeline in `programs/tests/typed/goal_term_acceptance.glp`
- [x] T002 Establish the pre-change baseline suite result by running `test/run_all_tests.sh` (566 checks) before any edit

---

## Phase 2 · Foundational — the shared helpers every story depends on

Per `DISCIPLINE.md` §1.3 the eight defect sites get **two shared helpers**, not eight local
patches. These block every user story below.

- [x] T003 Add `GoalTermError` + `_describeGoalTerm` in `glp_runtime/lib/engine/glp_engine.dart` *(93f0ef4b)*
- [x] T004 Add `_anonymousGoalWriter` + `_refuseAnonymousReader` in `glp_runtime/lib/engine/glp_engine.dart` *(93f0ef4b)*
- [x] T005 [P] Mirror both helpers as `anonymous_writer` / `refuse_anonymous_reader` in `glp_gleam/src/glp/engine/goal_boot.gleam` *(02f39269)*
- [x] T006 [P] Mirror both as `GoalTermError` + `GoalTermDescribe.Describe` + `_AnonymousGoalWriter` + `_RefuseAnonymousReader` in `out/csharp/lib/engine/glp_engine.cs` *(d8dbd593)*

---

## Phase 3 · User Story 1 (P1) — anonymous variables are accepted in goals

**Goal**: `_` runs at every position a named variable does.
**Independent test**: load the fixture, run a goal with `_` in each of the four positions on each runtime.

- [x] T007 [US1] Accept `UnderscoreTerm` at the top-level goal argument in `_setupArgument`, `glp_runtime/lib/engine/glp_engine.dart` *(FR-001)*
- [x] T008 [US1] Same in the conjunction mirror `_setupConjunctionArg`, same file *(FR-002)*
- [x] T009 [US1] Same in `_buildStructTerm` (structure argument), same file *(FR-001)*
- [x] T010 [US1] Same in `_buildStructTermForConj`, same file *(FR-002)*
- [x] T011 [US1] Same in `_buildListTerm` head (list element), same file *(FR-001)*
- [x] T012 [US1] Same in `_buildListTermForConj` head, same file *(FR-002)*
- [x] T013 [P] [US1] Same four positions in `glp_gleam/src/glp/engine/goal_boot.gleam` *(02f39269)*
- [x] T014 [P] [US1] Same six sites in `out/csharp/lib/engine/glp_engine.cs` *(d8dbd593 — this runtime was **never implemented** before that commit)*
- [x] T015 [US1] Pin FR-003 (two `_` never alias) and FR-004 (no binding reported) **by construction**, not by filtering: allocate per occurrence, never key by name, never extend `queryVarWriters`
- [x] T016 [US1] Regression checks V-1..V-5 in `test/run_all_tests.sh` *(FR-009)*

---

## Phase 4 · User Story 2 (P2) — a malformed goal term is refused, never silently altered

**Goal**: an improper tail is refused, not answered.
**Independent test**: run `first_item([send(1,a)|foo], Y).` and confirm refusal with no bindings.

🔴 **This is the wrong-answer story.** Before this feature Dart *and* C# replaced
`tailTerm` with `ConstTerm(null)`, discarding the malformed tail and answering the goal —
`[send(1,a)|foo]` returned **byte-identically** to `[send(1,a)|[]]`.

- [x] T017 [US2] Replace the silent tail `else` with a refusal in `_buildListTerm`, `glp_runtime/lib/engine/glp_engine.dart` *(FR-005)*
- [x] T018 [US2] Same in `_buildListTermForConj`, same file *(FR-005)*
- [x] T019 [P] [US2] Same two tail sites in `out/csharp/lib/engine/glp_engine.cs` *(d8dbd593 — both were **still coercing** until then)*
- [x] T020 [P] [US2] Confirm Gleam already refuses and keep the refusal; reword only, to name the term typed *(02f39269)*
- [x] T021 [US2] Accept `_` as a legal list **tail** — a fourth position none of the three runtimes had *(FR-001)*
- [x] T022 [US2] Make every front-end refusal name what the programmer typed, including `_?`, which stays **invalid** *(FR-006, FR-012 unchanged)*
- [x] T023 [US2] Regression checks V-6..V-9, V-16, V-17 in `test/run_all_tests.sh` *(FR-009)*

---

## Phase 5 · 🔴 SPEC DEFECT — FR-008a / SC-003a rest on a premise measurement has falsified

- [ ] **T024 [US2] ESCALATE: FR-008a and SC-003a require a regression test asserting that Gleam
  REFUSES a conjunctive goal. Measured 2026-09-05: Gleam ACCEPTS conjunctive goals.**

**Evidence.** `goal_boot.setup_goals` routes every argument through **the same `setup_args`** the
single-goal path uses. A conjunction containing `_` boots and reports `["Y", "Z"]` — the same
verdict Dart and C# give. Pinned by
`glp_gleam/test/glp/engine/goal_boot_101_test.gleam::underscore_in_a_conjunction_is_accepted_and_stays_independent_test`,
which asserts the `Ok` branch rather than accepting either outcome.

**Why this is a spec defect and not an implementation gap.** The spec's clarification bounded the
three-runtime parity obligation *on the premise that Gleam's conjunction path was deferred*, and
`goal_boot.gleam`'s own module header still says *"STILL DEFERRED, surfaced LOUDLY"*. **Both the
header and the clarification were describing a state that no longer holds.** Satisfying SC-003a
**literally would require BREAKING Gleam** — deliberately making a working path refuse — to
satisfy a criterion written to excuse its absence.

**Recommended resolution (engineer decision, not taken here).** Retire FR-008a and SC-003a, and
widen FR-008/SC-003 to include conjunctive shapes, since all three runtimes now demonstrably
agree on them. The Gleam module header is corrected to say so in
`docs/restart/...20260905-rev1.md` and in commit `af77d284`; the **spec** is left untouched
pending the ruling, because editing a spec to match code is precisely the inversion
`DISCIPLINE.md` §1.10 forbids.

*(Nothing downstream is blocked: the shipped behaviour is correct and measured in all three
runtimes. What is open is which sentence in the spec is right.)*

---

## Phase 6 · User Story 3 (P3) — the recorded limitations match the product

- [x] T025 [US3] Correct the retired L1 claim (`=..` in clause bodies works) in `CLAUDE.md` and `docs/known-issues.md`, marked retired with date + evidence *(FR-010)*
- [x] T026 [US3] Correct retired L2 (structs inside lists work) **and its wrong source location** — the builders are in `glp_engine.dart`, not `glp_repl.dart` *(FR-010, FR-011)*
- [x] T027 [US3] Record L3 as cross-runtime rather than C#-only *(FR-010)*
- [x] T028 [US3] Record L4 (the previously unrecorded wrong answer) *(FR-010)*
- [x] T029 [US3] Pin the two **retired** claims by test — V-14, V-15 — so a stale note cannot recur unnoticed *(SC-005)*
- [x] T030 [US3] 🔴 Correction of record: both documents said the fix had landed, and named the C# lines, **while those lines were still defective**. Corrected in place, not rewritten *(af77d284)*

---

## Phase 7 · Polish & cross-cutting

- [x] T031 🔴 Close the SC-003 hole: V-18..V-23 run one script through **both** REPLs and require **byte-identical** transcripts, with a non-empty guard first because two empty transcripts also compare equal
- [x] T032 Verify the new check is a **real detector**: revert the C# fix, rebuild, confirm V-20 fails and prints the divergence, V-22 fails, V-23 catches the leaked class name
- [x] T033 Add the 20-check Gleam test file `02f39269` never wrote, with explicit negative controls
- [x] T034 Attribute the two Section T failures rather than assume them: stash the C# change, rebuild, re-run the drill → **5/2 identical** ⇒ pre-existing, host-specific (absent `glpquick.pfx`)
- [x] T035 Apply engineer ruling `Q-101-02` to 7 sites — the improper tail is permanently invalid in a **goal term**; **FR-012 unchanged** *(f248cc03)*
- [x] T036 `/bk-codify` the transferable rule as a scored + promoted roadmap feature *(WSJF 19.5 / RICE 774,000)*

---

## Dependencies

```
Setup (T001-T002)
   └─> Foundational (T003-T006)   [blocks every story]
          ├─> US1 (T007-T016)     P1 — independently testable
          ├─> US2 (T017-T023)     P2 — independently testable, no US1 dependency
          │      └─> T024         SPEC DEFECT, escalated, non-blocking
          └─> US3 (T025-T030)     P3 — documentation only
                 └─> Polish (T031-T036)
```

**Parallel opportunities**: T005/T006, T013/T014, T019/T020 are all `[P]` — different runtimes,
different files, no shared state. The three runtimes were in fact implemented in three separate
commits for exactly this reason.

---

## Verification state

| suite | result |
|---|---|
| REPL unified | **582 total · 580 pass · 2 fail · 1 skip** (was 566) |
| Gleam | **645 passed, no failures** (was 625) |
| C# `out/csharp` | **0 errors** |

The 2 failures are Section T (T034: attributed, pre-existing). The 1 skip is `ms_message`
(venv absent), which is why the suite's honest-exit guard returns non-zero rather than 0.

**MVP scope**: US1 alone is a shippable increment. It was not shipped alone because US2 is the
story that removes a **wrong answer**, which outranks an inconvenient refusal.
