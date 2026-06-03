# Feature Specification: Chapter 4 — Basic Concurrent Programming

**Feature Branch**: `005-tutorial-ch04`
**Created**: 2026-04-28
**Status**: Draft (raw — produced by deep PDF scan)
**Input**: `olamni/tutorial/ch04/ch04-sources.md` + `GLP_ART.pdf` book pp 25–43 (PDF pp 37–55).
**Constitution**: `.specify/memory/constitution.md` — Principle VI; Principle I.
**Tutorial Mode**: cohesive-synthesis

## Clarifications
- Ch 4 is the largest pre-types chapter (~25 distinct Programs across 4 sections). Per charter §1, group by **substantial Program**; treat helpers (`producer`, `consumer`, `merge`) that recur across sections as shared in `useful-techniques.glp`.

## Source Programs (verified against PDF)
See `ch04-sources.md` code-block index for the full table. High-level groupings:
- §4.1 (constants): logic gates, NAND, half/full-adder.
- §4.2 (streams): producer/consumer, list reverse, fair-merge variants, dynamic merge, merge tree, distribute (broadcast/indexed/non-ground), observers, ripple-carry adder, sliding-window buffer, counter, accumulator with multiple clients.
- §4.3 (recursion): Peano arithmetic, integer arith, factorial (naïve + tail), Fibonacci (naïve + linear), flatten, tree_sum, insertion sort, merge sort, tree substitution.
- §4.4 (metaprogramming): trust-mode / fail-safe / control / tracing meta-interpreters with deterministic replay.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Logic gates and adder circuit (Priority: P1)
**Independent Test**: load `ch04/ch-04-ex-01-logic-gates.glp`; goal `adder([1,0,1],[1,1,0],0,R).`, expect `R = [0,0,0,1]` (= 5+6=11 in LSB-first binary).
**Acceptance**: Programs from §4.1 (gates → NAND → half_adder → full_adder → ripple-carry adder) verbatim with `%%` paraphrase comments.

### User Story 2 — Streams: producer / consumer / merge variants (Priority: P1)
**Independent Test**: load `ch04/ch-04-ex-02-stream-producer-consumer.glp`; goal `producer(H,5), consumer(H?,0,R).`, expect `R = 15`. Then `ch-04-ex-03-fair-merge.glp` and `ch-04-ex-04-dynamic-merge.glp`, `ch-04-ex-05-merge-tree.glp` etc.
**Acceptance**: each substantial Program from §4.2 lives in its own file.

### User Story 3 — Stream distribution (broadcast / indexed / non-ground) (Priority: P2)
**Independent Test**: `distribute_indexed([send(1,a), send(2,b)], Out1, Out2).` (note: REPL parser limitation per CLAUDE.md may block goals containing struct-in-list — document as REPL-test caveat).
**Acceptance**: `distribute/3`, `distribute_indexed/3`, `observer/3`, `distribute_ng/3` + `copy/3` from §4.2 / §4.3.

### User Story 4 — Buffered communication (sliding-window) (Priority: P2)
**Independent Test**: load `ch04/ch-04-ex-08-bounded-buffer.glp`; goal `bb_test.`, expect the alternation trace from p 35.

### User Story 5 — Objects and monitors (counter, accumulator) (Priority: P2)
**Independent Test**: `counter([add,add,add,read(X),clear,add,read(Y),[]]).`, expect `X=3, Y=1`.
**Acceptance**: `counter/1`, `accumulator/1` + `client1`, `client2`, `test_acc`.

### User Story 6 — Recursive numerics (factorial / Fibonacci variants) (Priority: P2)
**Independent Test**: `factorial(7, F).` → `F = 5040`; `fib_linear(20, F).` → `F = 6765`.

### User Story 7 — Sorting and tree manipulation (Priority: P2)
**Independent Test**: `mergesort([3,1,4,1,5,9,2,6], S).` → `S = [1,1,2,3,4,5,6,9]`.
**Acceptance**: `insertion_sort`, `mergesort`, `flatten`, `tree_sum`, `substitute`.

### User Story 8 — Meta-interpreter family (Priority: P3)
**Independent Test**: trust-mode `run(merge, merge([1,2],[3,4],Z)).` succeeds with `Z = [1,3,2,4]`. Then fail-safe / control / tracing variants.
**Acceptance**: §4.4 code: programs-as-data encoding + `run/2`, `run/4`, `run/5` + `suspended_run/4`, tracing `run/3` + indexed `reduce/3` + `replay/3`.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001** Output dir `olamni/tutorial/ch04/` with one `.glp` file per substantial Program (§4.1: ≤6 files, §4.2: ≤15 files, §4.3: ≤12 files, §4.4: ≤4 files).
- **FR-002** Shared helpers (`producer`, `consumer`, `merge`, `copy`) collected in `ch04/useful-techniques.glp` per charter §1.3.
- **FR-003** Every clause carries a `%%` paraphrase comment derived from the matching prose paragraph (charter §1.5).
- **FR-004** Each file's primary demo goal MUST `→ succeed` in the REPL; expected outcomes documented in chapter plan and verified by REPL-test traces saved on disk per charter §Testing.
- **FR-005** Formal boxes 4.1, 4.2, 4.3 are referenced in file headers but not encoded.
- **FR-006** End-of-chapter exercises (none explicit in §4.1–§4.4) are out of scope.
- **FR-007** REPL-only, no Flutter, no modules. No type/mode declarations.
