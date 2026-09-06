<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Specification Quality Checklist: M6 QUIC mailbox adapter

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-06
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
      — *Deviation declared and accepted*: the "Context" section names the exact file, commit and
      grep that measured the gap. That is evidence for why the feature exists, not a design. The
      requirements (FR-001..FR-023) and success criteria are stated behaviourally. Naming the
      measurement is required by this repo's discipline (a claim without a date and a command is a
      hypothesis); removing it would make the spec less honest, not more compliant.
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders — the four user stories are readable without the
      Context section
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain — zero markers; open questions are carried to the
      engineer as BK-STD-2 questions rather than left as inline markers
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic — SC-001..SC-008 state observable outcomes
      (an endpoint exists, two encodings are byte-identical, zero unreachable realizations)
- [x] All acceptance scenarios are defined — 4 stories, 9 scenarios
- [x] Edge cases are identified — 7, each traceable to a measured incident or a named threat
- [x] Scope is clearly bounded — kernel-managed hosting (M6-d) is explicitly OUT, and said so
- [x] Dependencies and assumptions identified — 6 assumptions, each naming its source

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows — receive, send, observe, both-at-once
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification — see the declared deviation above

## Notes

- **SC-004 is the anti-recurrence criterion.** It fails against the code as it stands today
  (two realizations, zero control-surface paths) and is the criterion that keeps this defect
  class from returning. It is deliberately phrased as a *measurement over the shipped assembly*
  rather than a review instruction, because a review instruction is exactly what failed here:
  the carrier was reviewed, tested and merged with no consumer.
- **SC-005 requires mutation proof.** Three guards on the wire plane were already mutation-proven
  when the carrier was written; SC-005 makes that the standing bar rather than a one-off.
- The spec was validated in one iteration. No item required a spec rewrite.
