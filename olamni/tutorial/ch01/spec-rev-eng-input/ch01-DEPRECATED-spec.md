# Feature Specification: Chapter 1 — Introduction

**Feature Branch**: `002-tutorial-ch01`
**Created**: 2026-04-28
**Status**: Draft (raw — produced by deep PDF scan, not by `/tutorial-specify`)
**Input**: `D:/BSTDEV/research/GLP/GLP/olamni/tutorial/ch01/ch01-sources.md` + `D:/BSTDEV/research/GLP/GLP/GLP_ART.pdf` book pp 3–7 (PDF pp 15–19).
**Constitution**: `.specify/memory/constitution.md` — Principle VI (Tutorial Charter Compliance); Principle I (extraction grounded in `GLP_ART.pdf`).
**Tutorial Mode**: cohesive-synthesis

## Clarifications
None outstanding.

## Source Programs (verified against PDF)
- **Program 1.1: Fair Stream Merger** — book p 5, 3 clauses (`merge/3`).

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Read §1.4–§1.6 alongside `ch-01-ex-01-fair-stream-merger.glp` (Priority: P1)

Learner reads §1.4 (Concurrent Logic Programming), §1.5 (Single-Reader/Single-Writer Insight), §1.6 (A First GLP Program — Program 1.1) and runs the chapter's single tutorial file.

**Why this priority**: Program 1.1 is the first GLP program shown in the book; the SRSW discipline it demonstrates is the foundational concept everything else depends on.

**Independent Test**: load `ch01/ch-01-ex-01-fair-stream-merger.glp` in the REPL; run `merge([1,2,3],[a,b],Xs).` and verify `Xs = [1,a,2,b,3]` (or `[1,a,2,3,b]` depending on fairness ordering — must match the trace shown in §3.1 / §4.2).

**Acceptance Scenarios**:
1. **Given** Program 1.1 verbatim from p 5 with `%%` paraphrase comments derived from the surrounding prose, **When** the learner runs the merge goal in the REPL, **Then** the goal succeeds and produces a fair interleaving of both input streams.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001** Tutorial output dir is `olamni/tutorial/ch01/`.
- **FR-002** Single tutorial file `ch01/ch-01-ex-01-fair-stream-merger.glp` containing the three `merge/3` clauses verbatim from p 5, with `%%` comments paraphrasing the surrounding prose (per charter §1.5).
- **FR-003** No type / mode declarations (chapter precedes Ch 5 type system).
- **FR-004** End-of-chapter exercises (none in Ch 1) and Formal-1.1 box (p 6) are out of scope per charter §1 / §2.
- **FR-005** Demo goals: `merge([1,2,3],[a,b],Xs).`, expected outcome documented in chapter plan.
- **FR-006** REPL-only chapter (no Flutter project, no module structure) per charter §1.
