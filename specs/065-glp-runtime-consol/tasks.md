---
# SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
#
# SPDX-License-Identifier: MIT

description: "Task list for glp-runtime-consol"
---

# Tasks: glp-runtime-consol

**Input**: Design documents from `/specs/065-glp-runtime-consol/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: The spike's own IL-parity harness IS the verification for US1; no separate TDD phase
was requested. Baseline-green gating (Constitution VII) is enforced in Setup and Polish.

**Organization**: Grouped by user story. US1 (Scope A, gated spike) and US2 (Scope B, dead-code
cleanup) are fully independent. **Recommended implementation order: US2 first** (no gate, low
risk), then US1 (STOP at the §1.14 gate on T010).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: US1 = antlr4 grammar spike; US2 = abandon dead-stub cleanup

## Path Conventions

- Spike artifacts (additive): `spike/antlr4-glp-grammar/`
- C# engine baseline (read-only for US1): `out/csharp/lib/compiler/`
- Cleanup target: `out/csharp/lib/runtime/abandon.cs`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Establish the known-good baseline before any change (Constitution VII).

- [ ] T001 Capture and record a green test baseline across suites before any change: REPL
  (`export DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart; bash test/run_all_tests.sh`), C#
  engine (`dotnet build` + `dotnet test`), and Gleam (`glp_gleam/`); note the pass counts in the
  feature's marathon/verification trail.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Confirm the enabling facts both stories rely on.

**⚠️ CRITICAL**: T001 baseline MUST be green before any story work begins.

- [ ] T002 Confirm dependency posture for the IL-parity comparison: verify compiled-IL-on-the-wire
  (#11) and the il-codec round-trip (#4) are delivered/available (wave-4/062, specs/050) so
  bytecode can be serialized and compared deterministically; record the finding in research/report.

**Checkpoint**: Baseline green + dependency posture confirmed — story work can begin.

---

## Phase 3: User Story 2 - Abandon dead-stub cleanup (Priority: P2) 🎯 do first (no gate)

**Goal**: Remove the dead `AbandonOps.AbandonWriter` `NotImplementedException` stub; abandon is
delivered as the anonymous-writer discard semantic (062 US5).

**Independent Test**: Source search returns zero references to the removed stub and the C# solution
builds green with baselines unchanged (SC-005, SC-006).

- [ ] T003 [US2] Confirm the stub is dead: search the C#/Dart trees for `AbandonOps` /
  `AbandonWriter` references (`grep -rn` over `out/csharp/`, `csharp/`, excluding
  `runtime/abandon.cs`); expect zero production callers. If a caller exists → STOP and report
  (Bug-Protocol II); do not proceed.
- [ ] T004 [US2] Remove the dead stub `out/csharp/lib/runtime/abandon.cs` (`git rm`), only after
  T003 confirms zero callers.
- [ ] T005 [US2] Rebuild the C# engine solution to zero errors after removal.
- [ ] T006 [US2] Re-run the baselines (REPL + C# + Gleam) and confirm no new failures vs T001
  (SC-006).

**Checkpoint**: US2 fully functional and independently verified.

---

## Phase 4: User Story 1 - ANTLR4 shared-grammar feasibility spike (Priority: P1)

**Goal**: A `.g4` grammar + generated C# parser front-end that parses a working-GLP corpus and
produces IL identical to the hand-written parser (or enumerates divergences), yielding a go/no-go
feasibility report. Additive spike; production parsers untouched (FR-010).

**Independent Test**: `REPORT.md` states a verdict; coverage (SC-001) and IL parity (SC-002) are
recorded; zero accepted-syntax changes landed without a §1.14 approval (SC-004).

- [ ] T007 [P] [US1] Acquire the ANTLR4 complete jar (≥4.13) into `spike/antlr4-glp-grammar/`; if
  acquisition is blocked in-environment, document the degradation (grammar + manual coverage
  argument) per spec Assumption.
- [ ] T008 [P] [US1] Create spike scaffolding `spike/antlr4-glp-grammar/{corpus/,harness/,gen/}`.
- [ ] T009 [US1] Select a representative corpus subset from `programs/tests/typed/` (+ a few
  book/lib examples) into `spike/antlr4-glp-grammar/corpus/` (or a manifest referencing
  `programs/`), exercising declarations, guards, reader/writer modes, `::=` unions, `=..`/`..=`,
  module `#` calls, lists/structs, and ≥1 negative control (e.g. `abandon_reader_bad.glp`).
- [ ] T010 [US1] 🔴 Author `spike/antlr4-glp-grammar/Glp.g4` covering the token vocabulary of
  `out/csharp/lib/compiler/token.cs` — **faithful description of the EXISTING accepted syntax
  only**. **§1.14 / Constitution IV-a STOP GATE**: if faithful expression requires a change to the
  accepted GLP syntax, STOP and write an owner proposal (Gabi + Udi) before any such change
  (FR-005, SC-004).
- [ ] T011 [US1] Generate the C# parser front-end into `gen/`
  (`java -jar antlr-...-complete.jar -Dlanguage=CSharp -o gen Glp.g4`).
- [ ] T012 [US1] Build the IL-parity harness in `spike/antlr4-glp-grammar/harness/`: parse each
  corpus example with the generated parser and with the hand-written parser; compile both through
  the shared downstream pipeline; compare `BytecodeProgram` instruction sequences (per
  `contracts/grammar-spike.md`).
- [ ] T013 [US1] Run the harness; record per-example `ILParityResult` (accepted-by-each, identical
  IL or divergence-cause) — coverage (SC-001) and IL parity (SC-002).
- [ ] T014 [P] [US1] Attempt a trial C++/Dart/Gleam generation target OR document an explicit
  deferral with rationale (multi-target cost for the report).
- [ ] T015 [US1] Write `spike/antlr4-glp-grammar/REPORT.md` per `contracts/feasibility-report.md`:
  go/no-go verdict, coverage, IL parity, multi-target cost, dependency posture, §1.14 status,
  residual risks (SC-003).

**Checkpoint**: US1 feasibility report complete; grammar/harness additive; no syntax change without
approval.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [ ] T016 [P] Run `quickstart.md` validation end-to-end (both scopes).
- [ ] T017 Final baseline-green confirmation across all suites (REPL + C# + Gleam); note counts vs
  T001 (SC-006).

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (T001)**: no dependencies; run first (baseline gate).
- **Foundational (T002)**: after T001.
- **US2 (T003–T006)**: after T002; independent of US1.
- **US1 (T007–T015)**: after T002; independent of US2.
- **Polish (T016–T017)**: after the implemented stories.

### Within US1

- T007, T008 (parallel) → T009 → **T010 (§1.14 gate)** → T011 → T012 → T013 → T015.
- T014 parallel after T010/T011.

### Recommended order (handover directive)

Do **US2 first** (no gate), then US1 (STOP at T010's §1.14 gate before any syntax-affecting change).

### Parallel Opportunities

- T007 and T008 can run in parallel.
- US2 and US1 are independent and could run in parallel if staffed, but the handover recommends
  US2 first.

---

## Implementation Strategy

### MVP / incremental

1. Phase 1 Setup (T001) → Phase 2 Foundational (T002).
2. US2 (Scope B) → independently verified dead-code removal (fast, no gate) → a shippable slice.
3. US1 (Scope A) → grammar spike, STOP at the §1.14 gate (T010) → feasibility report.
4. Polish (T016–T017) → quickstart validation + final baseline green.

### Notes

- 🔴 T010 is the language-authority STOP gate (Constitution IV-a / DISCIPLINE §1.14).
- Spike artifacts are additive under `spike/antlr4-glp-grammar/`; production parsers untouched.
- Commit after each task or logical group; stage by name only (Constitution VII).
