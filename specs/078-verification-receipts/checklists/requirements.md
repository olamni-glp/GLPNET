<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Specification Quality Checklist: Verification receipts and loud failure

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-12
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

## Validation record

**Iteration 1 — 2026-08-12.** All items pass. Notes on the judgement calls, so a reviewer can
challenge them rather than having to reverse-engineer them:

- **Zero [NEEDS CLARIFICATION] markers, by decision not by omission.** Three candidates were
  considered and each had a defensible default, so all three were resolved into the Assumptions
  section instead of being asked:
  1. *Does "check" mean tests only, or every verdict-issuing mechanism?* → Resolved **broad**. The
     twelve witnessed instances span tests, reviews, gates, polls, imports and status probes; a
     contract covering only tests would leave most of the defect class open. Recorded as an
     assumption so it can be narrowed if the engineer disagrees.
  2. *Do receipts replace or accompany existing verdicts?* → Resolved **additive**. Replacement
     would break every consumer on day one and force big-bang adoption; additive permits the
     incremental retrofit FR-017/FR-018 already assume.
  3. *Is EMPTY a pass?* → Resolved **yes**. Making legitimate emptiness a failure would drive
     engineers to suppress the mechanism, reintroducing the defect through the back door. Called
     out explicitly in Edge Cases and Assumptions because it is the most likely thing to get wrong.

- **Technology-agnostic success criteria.** SC-001…SC-008 are stated as counts, percentages and
  elapsed time. No file formats, schemas, languages, CLI names or storage choices appear in the
  Requirements or Success Criteria sections — those are `/bk-plan` decisions. Tool and area names
  (3rtask, codexreview, roadmap-sync, …) appear only as *scope boundaries* in the "Why this feature
  exists" evidence table and the Assumptions, which is scope, not implementation.

- **Testability.** Every FR is observable from outside the mechanism. The riskiest was FR-003
  (target identity "as resolved, not as requested"); it is testable by pointing a check at a path
  that resolves elsewhere and asserting the receipt shows the resolved value — which is exactly
  fault-injection scenario 3.4.

- **Self-consistency check applied deliberately.** This feature could trivially have become an
  instance of the defect it fixes — a verification mechanism nobody verified. FR-016 and the
  edge case "the receipt itself is missing or malformed" subject the mechanism to its own
  invariant. SC-007 proves the suite can go red. This was the single most important review pass.

- **One unavoidable tension, recorded rather than hidden:** FR-012 forbids suppressible refusals,
  while the override path exists so engineers are not blocked. These are reconciled by making the
  override *recorded and permanently visible in the receipt* rather than a silent configuration
  switch. If `/bk-clarify` finds that too strict for routine work, that is the right place to
  revisit it.

## Notes

- Items marked incomplete require spec updates before `/bk-clarify` or `/bk-plan`. None are
  incomplete at this iteration.
- **Recommended next stage: `/bk-clarify`** rather than straight to `/bk-plan`. Not because
  anything is ambiguous, but because the three resolved-by-default decisions above are exactly the
  kind that are cheap to change now and expensive after planning.
