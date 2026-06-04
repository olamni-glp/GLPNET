# Feature Specification: Chapter 5 — Types and Modes

**Feature Branch**: `006-tutorial-ch05`
**Created**: 2026-04-28
**Status**: Draft (raw — produced by deep PDF scan)
**Input**: `olamni/tutorial/ch05/ch05-sources.md` + `GLP_ART.pdf` book pp 47–52 (PDF pp 59–64).
**Constitution**: `.specify/memory/constitution.md` — Principle VI; Principle I.
**Tutorial Mode**: cohesive-synthesis

## Clarifications
- §5.6 typed-quicksort is the chapter's canonical Program. §5.4 worked merge example and §5.5 counter-with-response-slot are SECONDARY but illustrate distinct concepts (mode checking flow, embedded modes). Decision: each gets its own tutorial file.

## Source Programs (verified against PDF)
- **§5.6 Typed Quicksort** (canonical) — p 51, NumList type + 3 procedure decls + 6 clauses.
- §5.4 typed `merge/3` worked example — p 49.
- §5.5 `CounterMsg`/`CounterStream` + `counter/2` with embedded `show(Number?)` mode — p 50.
- §5.7 type-error and mode-error illustrative snippets — p 51–52.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Typed quicksort end-to-end (Priority: P1)

Learner reads §5.1–§5.6 and runs the typed quicksort canonical program.

**Independent Test**: load `ch05/ch-05-ex-01-typed-quicksort.glp`; the REPL pipeline (SRSW → PE → type-check → compile) MUST succeed at load time. Then run `quicksort([3,1,4,1,5,9,2,6], Sorted).`, expect `Sorted = [1,1,2,3,4,5,6,9]`.

**Acceptance Scenarios**:
1. **Given** Program 5.6 verbatim from p 51 (NumList type + procedure declarations for `quicksort`/`qsort`/`partition` + 6 clauses), **When** the file is loaded, **Then** type checking passes; goal succeeds with sorted output.

### User Story 2 — Mode-checked merge (worked example) (Priority: P2)

**Independent Test**: load `ch05/ch-05-ex-02-mode-checked-merge.glp`; load succeeds. Comments inside the file enumerate the head- and body-checking steps from §5.4.

**Acceptance Scenarios**:
1. NumList type + `procedure merge(NumList?, NumList?, NumList).` + 3 merge clauses from p 49.

### User Story 3 — Counter with response-slot embedded mode (Priority: P2)

**Independent Test**: load `ch05/ch-05-ex-03-counter-with-response.glp`; type-check passes; query a `show(State?)` message via the chapter's demo trace.

**Acceptance Scenarios**:
1. CounterMsg / CounterStream type defs + `procedure counter(CounterStream?, Number?).` + the `show` clause from p 50, comments paraphrasing the involution rule from Formal 5.3.

### User Story 4 — Type errors and mode errors (negative examples) (Priority: P3)

**Independent Test**: load `ch05/ch-05-ex-04-type-mode-errors.glp`; load MUST fail with the specific errors from §5.7 (`'a' is not a Number`, mode mismatch on `bar`).

**Acceptance Scenarios**:
1. The two ill-typed snippets from p 51–52 plus the corrected `bar/2` clause as a passing counter-example.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001** Output dir `olamni/tutorial/ch05/`.
- **FR-002** File `ch-05-ex-01-typed-quicksort.glp` contains §5.6 Program 5.6 verbatim.
- **FR-003** File `ch-05-ex-02-mode-checked-merge.glp` contains §5.4 worked merge with `%%` step-by-step head/body checking annotations.
- **FR-004** File `ch-05-ex-03-counter-with-response.glp` contains §5.5 counter + embedded-mode example.
- **FR-005** File `ch-05-ex-04-type-mode-errors.glp` contains the negative examples; documented as MUST-fail at load.
- **FR-006** Files 1–3 MUST pass full REPL pipeline (SRSW + PE + types + compile) without errors and execute their demo goal.
- **FR-007** Formal 5.1, 5.2, 5.3 referenced in headers, not encoded.
- **FR-008** §5.8 Exercises out of scope per charter.
- **FR-009** REPL-only, no Flutter, no modules.
