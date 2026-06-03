# Feature Specification: Chapter 2 — Logic Programs and Linear Logic

**Feature Branch**: `003-tutorial-ch02`
**Created**: 2026-04-28
**Status**: Draft (raw — produced by deep PDF scan)
**Input**: `olamni/tutorial/ch02/ch02-sources.md` + `GLP_ART.pdf` book pp 9–14 (PDF pp 21–26).
**Constitution**: `.specify/memory/constitution.md` — Principle VI; Principle I.
**Tutorial Mode**: cohesive-synthesis

## Clarifications
- Ch 2 is mostly **theoretical** (transition systems, definitions, linear-logic correspondence). The only executable code in the chapter is **Example 2.1 (Append)** — and that example is **classical Logic Programs**, not GLP. Decision: include it as a contrast piece, prefixed with prose explaining why it does NOT satisfy SRSW (each variable occurs >1 time as both reader and writer in classical LP).

## Source Programs (verified against PDF)
- **Example 2.1 (Append)** — book p 10, 2 clauses; classical LP, presented for contrast against GLP's SRSW discipline.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Compare classical LP append to GLP append (Priority: P2)

Learner reads §2.1 (Logic Programs syntax + operational semantics) and §2.2 (Linear Logic + GLP correspondence), then opens `ch02/ch-02-ex-01-classical-append-contrast.glp` to see the classical LP `append/3` annotated with comments showing why it does NOT satisfy SRSW.

**Why this priority**: P2 because the chapter is theoretical; no GLP Program is presented. The tutorial's job is to make the LP→GLP transition concrete by showing the broken classical version next to the GLP version (which appears in Ch 3).

**Independent Test**: file MUST NOT load in the REPL — it is presented as `% INTENTIONALLY ILL-FORMED FOR GLP — illustrates contraction` and the SRSW analyser is expected to reject it. Documented as a negative test.

**Acceptance Scenarios**:
1. **Given** the classical Append clauses verbatim from p 10, **When** the learner attempts to load `ch02/ch-02-ex-01-classical-append-contrast.glp` in the REPL, **Then** the SRSW analyser reports a violation (multiple writer/reader occurrences of `Ys`, `Zs`, etc.), and the file's prose comments explain why this is exactly the contraction that GLP forbids.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001** Output dir `olamni/tutorial/ch02/`.
- **FR-002** Single file `ch02/ch-02-ex-01-classical-append-contrast.glp` containing the verbatim Example 2.1 clauses from p 10 with `%%` comments mapping each variable occurrence to "writer" or "reader" in classical LP terms, then a `%%` block explaining how this fails SRSW.
- **FR-003** Document expected REPL behaviour: SRSW analyser MUST reject the load. Capture the analyser's output as the file's accompanying trace.
- **FR-004** Definitions 2.1–2.10 (transition systems, terms, mgu, runs, deductions) and Definitions 2.11–2.12 (linear logic) are **out of scope** for the tutorial — they are formal-track material per the book's "How to Read This Book" guidance.
- **FR-005** Formal 2.1 (Linear Equality Assertions correspondence table) is referenced in the file's header comment but not encoded as code.
- **FR-006** REPL-only, no module structure, no type declarations (chapter precedes Ch 5).
