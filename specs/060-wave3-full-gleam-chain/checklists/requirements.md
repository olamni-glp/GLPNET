# Specification Quality Checklist: Wave 3 consolidated — Full Gleam chain

**Purpose**: Validate specification completeness and quality before proceeding to planning
**Created**: 2026-07-27
**Feature**: [spec.md](../spec.md)

## Content Quality

- [x] No implementation details (languages, frameworks, APIs)
- [x] Focused on user value and business needs
- [x] Written for non-technical stakeholders
- [x] All mandatory sections completed

## Requirement Completeness

- [ ] No [NEEDS CLARIFICATION] markers remain
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

- Items marked incomplete require spec updates before `/bk-clarify` or `/bk-plan`.

### Open items

**3 [NEEDS CLARIFICATION] markers remain — all scope-level, none resolvable by a default:**

1. **FR-001 — shared grammar**: feature 059 recorded the ANTLR4 shared-grammar path as *superseded* (G5 ruling). Whether wave 3 still owes a shared grammar artifact, or accepts per-runtime parsing, changes whether roadmap item 1 of 7 is in scope at all.
2. **FR-025 — required transports**: loopback + TCP only, versus also proving QUIC/WebSocket and ZMQ. Materially changes the size of stories 4 and 5.
3. **FR-031 — AtomVM**: whether running on embedded AtomVM is a wave-3 acceptance gate or deferred. This is the wave's recorded primary risk; deferring it removes the risk, requiring it dominates the schedule.

**Content-quality note**: this feature is a language-runtime deliverable, so a small number of domain terms that are also implementation names (Gleam, C#, AtomVM, BEAM) appear in the spec. They are unavoidable — they identify *which* runtime is being specified, which is the feature's subject, not a design choice being smuggled in. Success criteria (SC-001…SC-009) are stated in outcome terms and carry no such names beyond naming the runtimes under comparison.
