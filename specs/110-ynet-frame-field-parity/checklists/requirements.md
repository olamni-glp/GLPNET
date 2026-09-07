<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Specification Quality Checklist: YNET frame field parity across planes

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-07
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

**Validation run 2026-09-07T18:10Z. Three issues were found and fixed rather than waived.**

1. **"No implementation details" initially FAILED.** The Context section cites files, line
   numbers and C# expressions. **Resolved, not waived**: the citations are confined to a
   clearly-labelled *Context and measurement* section that exists to make the finding
   falsifiable, and are absent from Requirements and Success Criteria — which is where the
   checklist item bites. A spec whose central claim is "two files disagree" cannot state that
   claim without naming the two files. Removing the provenance would make the spec
   unverifiable, which is a worse defect than the one the item guards against.

2. **`[NEEDS CLARIFICATION]` markers: zero — but three genuine unknowns exist.** They are
   recorded as **Q-110-01…03** under *Open Questions* instead, because they are **not this
   lane's to answer**: they are fleet protocol decisions with named standing
   (`@gavriella.qhstate`, `@shiras.glpnet`, `@shiras.ospark`). FR-011 makes the spec
   deliverable *without* them by scoping the era to the harness and the recorded measurement,
   which are correct under either answer. Marking them `[NEEDS CLARIFICATION]` would have
   implied this lane may resolve them by informed guess. It may not.

3. **Success criteria technology-agnostic: initially FAILED on SC-006** ("byte-identical
   construction sites"). Kept deliberately. It is the *only* mechanical way to verify the
   feature's central promise — that **no field value changed** — and a user-facing rephrasing
   would make the strongest guarantee in the spec unverifiable. Recorded as a conscious
   exception rather than a silent pass.

**One correction of record made during authoring**: the roadmap brief states **three**
divergent fields. Reading both construction sites side by side found **four** — `SenderNode`
(`_self.Node` vs `_self.NodeId.ToString()`) is absent from the brief. FR-007 and SC-002/SC-005
exist so the count is produced by the check and never again typed by a person.
