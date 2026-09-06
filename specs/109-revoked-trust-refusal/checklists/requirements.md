# Specification Quality Checklist: Revoked trust material is refused at load

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-09-06
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
      *Caveat, deliberate and declared: the spec names `SharedCertMaterial.cs` and
      `QuicTransport.SpkiPin` in the "precise gap" section. That is EVIDENCE of the measured
      defect, not a design instruction — the requirement (FR-001..FR-010) names no file, and the
      plan is free to place the guard elsewhere if a better seam exists.*
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders — the "why this exists" section leads with the
      measurement and its consequence, not the code
- [x] All mandatory sections completed

## Requirement Completeness

- [x] No [NEEDS CLARIFICATION] markers remain — **zero**; engineer ruling G-03 settled the one
      question that would have been a marker (constant vs. config denylist) BEFORE the spec was
      written
- [x] Requirements are testable and unambiguous
- [x] Success criteria are measurable
- [x] Success criteria are technology-agnostic
- [x] All acceptance scenarios are defined (3 user stories, 7 scenarios)
- [x] Edge cases are identified (4, including the guard's own constant being wrong)
- [x] Scope is clearly bounded — explicit "Out of scope" naming rotation, history rewrite, and the
      derived-credential revocation set
- [x] Dependencies and assumptions identified (4 assumptions, each traced to a measurement)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into the requirements themselves

## Validation notes

**Iteration 1 — all items pass.**

Two things this checklist deliberately records rather than waves through:

1. **FR-009 mandates BOTH controls.** The repo's own recorded history is that a green self-written
   suite is not evidence (wave-26: nine guard suites green while codex found six false-green
   holes). A positive control alone would pass against a guard that refuses everything; a negative
   control alone would pass against a guard that refuses nothing. The pair is the requirement.

2. **FR-004 exists because FR-001 is structurally insufficient.** A denylist can only refuse what
   someone already enumerated. gen-2 is not in the list because no evidence of it was found — which
   is exactly the kind of gap a denylist cannot close. The current-generation assertion is
   fail-closed by construction and is specified as a complement, per the engineer's own framing.
