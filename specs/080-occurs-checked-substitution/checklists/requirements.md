# Specification Quality Checklist: Occurs-checked substitution pipeline

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-08-14
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs) — file/module names are the declared-area references the roadmap requires, not implementation prescriptions
- [x] Focused on user value and business needs (closing the F-069-1 crash class soundly)
- [x] Written for stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [ ] No [NEEDS CLARIFICATION] markers remain — **INTENTIONALLY OPEN**: FR-002 is a §1.14 language-authority decision reserved for Udi; the spec is propose-first and blocks implementation on it by design (FR-008). This is not a defect to resolve autonomously.
- [x] Requirements are testable and unambiguous (each FR has an acceptance path; FR-002 has both branches specified)
- [x] Success criteria are measurable (SC-001..SC-004)
- [x] Success criteria are technology-agnostic (outcomes, not mechanisms)
- [x] All acceptance scenarios are defined
- [x] Edge cases are identified (sharing-not-cycle, chained substitution, ground RHS, anonymous vars)
- [x] Scope is clearly bounded (producer side; runtime unifier out of scope)
- [x] Dependencies and assumptions identified (077 hard dependency; §1.14 gate)

## Feature Readiness

- [x] All functional requirements have clear acceptance criteria
- [x] User scenarios cover primary flows (catch-at-source, no-false-positive, one-shared-impl)
- [x] Feature meets measurable outcomes defined in Success Criteria
- [x] No implementation details leak into specification

## Notes

- The single open [NEEDS CLARIFICATION] (FR-002, §1.14 UnifyFail-vs-CompileError) is **deliberate and
  load-bearing**: it is Udi's express decision per CLAUDE.md §1.14 / DISCIPLINE.md §1.14. `/bk-clarify`
  surfaces it to Udi; `/bk-plan` and `/bk-implement` do NOT proceed on the semantics until he rules.
  All other checklist items pass, so the spec is READY for the clarify→Udi gate, and NOT ready for
  implement (correctly).
