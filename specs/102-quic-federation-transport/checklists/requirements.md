<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Specification Quality Checklist: QUIC federation transport for the ynet oracle

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-04
**Validated**: 2026-09-04 (iteration 2)
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

## Validation record — what iteration 1 actually failed on

This checklist is **not** a formality tick. Iteration 1 failed four items and the fixes changed the
spec's content, not its wording:

| item | iteration-1 failure | fix applied |
|---|---|---|
| No implementation details | FR-001/FR-005 named the wire protocol, the certificate format and the specific pinning mechanism. A spec that names the mechanism cannot later choose a different one without appearing to change requirements. | Rewritten as capability statements — "accepts federation connections", "verify the other's identity". The mechanism moves to plan.md where it belongs. |
| Success criteria technology-agnostic | SC-001 quoted a port number and SC-004 quoted a pin-list data structure. | Restated as observable outcomes; SC-004 now specifies a **negative control** instead of a structure. |
| Requirements testable and unambiguous | FR-019 said the status surface must "be accurate", which is untestable. | Replaced with four **separately-reported** named states plus FR-020/FR-021 forbidding inference and mandating an explicit *unknown*. |
| Scope clearly bounded | Leader election and PBFT were implied by the framing but never excluded, so the feature could absorb them silently. | Explicit **Out of Scope** section; the dependency direction is stated (election consumes this transport). |

## Notes

- **No [NEEDS CLARIFICATION] markers were used.** Four candidate ambiguities were resolved from
  engineer rulings already on record (`.specify/decisions/Q-GLPNETG27-20260904T1600Z.json`, BK-STD-2
  conformant): the term scheme, the admission-credential grade, the reachability authorisation, and
  the era's scope. Raising them again as clarifications would have re-asked settled questions.
- **The observability requirements (FR-019–FR-023) exist because of measured incidents, not
  hypotheticals.** Six false greens were recorded in this estate in one week, one of which survived
  CI. SC-007 is written as a paired positive/negative control specifically so that a status surface
  which reports the same thing in both cases fails the criterion.
- 🔴 **FR-013–FR-018 are a hard precondition of the first merge, not parallel work.** Term ordering
  is monotone; once boards fold, the ordering rule cannot be retrofitted. Any plan that sequences
  them after the first cross-host merge is wrong regardless of how it is estimated.
- **SC-001 says "physically separate" deliberately.** An existing proof between two roots on one
  machine is real evidence of the *mechanism* and is explicitly disqualified as evidence of
  *federation* by FR-022.
