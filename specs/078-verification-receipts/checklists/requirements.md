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
- [ ] Requirements are testable and unambiguous — **FAILS at Iteration 2, see below**
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic (no implementation details)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified
- [x] Scope is clearly bounded
- [x] Dependencies and assumptions identified

## Feature Readiness

- [ ] All functional requirements have clear acceptance criteria — **FAILS at Iteration 2 (FR-013)**
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

**Iteration 2 — 2026-08-18. Two items REVOKED. This checklist was a stale green.**

Iteration 1 was validated on 2026-08-12 and never re-run. Since then `/bk-clarify` rewrote FR-008
to phased and added FR-019/FR-020/FR-021. The checklist was still reporting **16 of 16 pass** on a
specification it had not read in that form.

That is this feature's own defect class, sitting in this feature's own quality gate: *a check that
reports a pass without having run against its intended target.* It is recorded here rather than
quietly re-ticked, because it is the cheapest available demonstration of why FR-001 exists — and
because the ticks were **true when made**, which is precisely what makes a stale gate dangerous.

**Revoked — "Requirements are testable and unambiguous".** Five requirements are not, each raised
as an engineer decision on 2026-08-18:

| block | requirement | what is missing |
|---|---|---|
| 24 | FR-002 / FR-004 | a receipt has no defined location, so FR-008's consumer cannot know where to look or what "absent" means |
| 25 | FR-013 | *"expected"* is undefined, so a run containing zero checks satisfies it trivially — the same vacuity shape FR-019/020/021 just closed for FR-008, one requirement later |
| 27 | FR-005 | "bounded in size" names no bound, so SC-004 has nothing to assert against |
| 28 | FR-012 | the override has no scope, expiry or authority, so one recorded override can silently authorise every future refusal of its kind |
| 29 | SC-003 | the reader and the 20 samples are undefined, so the criterion is unfalsifiable by its own author |

**Revoked — "All functional requirements have clear acceptance criteria".** FR-013 has none, for
the reason in block 25: an undefined *expected* set cannot be asserted against.

**Not revoked, and worth saying explicitly.** Iteration 1's judgement calls hold up. The three
resolved-by-default decisions were resolved correctly and were flagged as revisitable. The
self-consistency pass (FR-016, the malformed-receipt edge case, SC-007) was the right call and is
why this feature is not merely an instance of its own defect. And Iteration 1 *itself* named the
FR-012 override tension and said clarify was the right place to revisit it — which is block 28,
found independently six days later. The failure here is not judgement, it is the **absence of a
re-validation trigger**: nothing invalidates a checklist when the spec it checks is edited.

**Open recommendation (engineer, register block 37):** add that trigger, under the same
"absence is an error" rule as FR-020 — a checklist whose spec has changed since its last
validation reports UNREAD, never a pass.

## Notes

- Items marked incomplete require spec updates before `/bk-clarify` or `/bk-plan`. **Two are
  incomplete at Iteration 2** and are the substance of the open clarify stage.
- **Recommended next stage: `/bk-clarify`** rather than straight to `/bk-plan`. Not because
  anything is ambiguous, but because the three resolved-by-default decisions above are exactly the
  kind that are cheap to change now and expensive after planning.
