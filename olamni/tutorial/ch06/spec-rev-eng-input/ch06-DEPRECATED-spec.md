# Feature Specification: Chapter 6 — Typed Programming

**Feature Branch**: `007-tutorial-ch06`
**Created**: 2026-04-28
**Status**: BLOCKED — chapter is a stub in `GLP_ART.pdf`.
**Input**: `olamni/tutorial/ch06/ch06-sources.md` + `GLP_ART.pdf` book p 53 (PDF p 65).
**Constitution**: `.specify/memory/constitution.md` — Principle VI; Principle I (extraction grounded in PDF; cannot proceed without source).
**Tutorial Mode**: cohesive-synthesis (charter assignment, but inapplicable until book is filled in)

## Status

⚠ **The book chapter is empty as published in `GLP_ART.pdf` (p 53 only):**

> **Chapter 6: Typed Programming**
> This chapter presents advanced GLP programming techniques that build on the moded type system introduced in Chapter 5.
>
> 6.1 Difference Lists
> 6.2 Quicksort
> 6.3 Equators: Emergency Brake
> 6.4 Bidirectional Communication
> 6.5 Buffered Communication

There is **no body text and no Programs** for any of the five sections. Per Principle I (extraction grounded in PDF) and the spec-first development rule (CLAUDE.md): we cannot generate a tutorial from a chapter that is not yet written.

## Required action before this spec can advance

One of:
1. **Wait** for the author to fill in Ch 6 in a future PDF revision; re-run the deep PDF scan; replace this spec.
2. **Synthesize** from related chapters (Ch 5 typed quicksort for §6.2; Ch 4 buffered-communication / sliding-window for §6.5) — but this MUST be acknowledged as synthesis, not extraction, and the tutorial files MUST carry a header noting "synthesized from Ch 4/5; not extracted from Ch 6 source".

## User Scenarios & Testing
N/A until source is available.

## Requirements *(mandatory)*

### Functional Requirements
- **FR-001** No tutorial files MAY be generated for `olamni/tutorial/ch06/` while the source PDF chapter is empty.
- **FR-002** This spec MUST be revisited and rewritten when Ch 6 is filled in (or when the synthesis path is explicitly authorised by Udi).
- **FR-003** Until then, `olamni/tutorial/ch06/` SHOULD contain only `ch06-sources.md` documenting the empty-chapter status.
