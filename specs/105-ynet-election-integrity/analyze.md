<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Cross-artifact analysis — 105-ynet-election-integrity

**Date**: 2026-09-05 · **Stage**: `/bk-analyze` · Non-destructive; read-only over spec/plan/tasks.

## Coverage: every FR maps to at least one task, and every task to at least one FR

| FR | Tasks | FR | Tasks |
|---|---|---|---|
| FR-001 | T009, T010 (verified, pre-built) | FR-008 | **T001–T006** |
| FR-002 | T009 (forged-refused control) | FR-009 | T008 |
| FR-003 | T009 | FR-010 | T009 |
| FR-004 | T009 | FR-011 | T007, T011 |
| FR-005 | T010 | FR-012 | T002, T003, T012 |
| FR-006 | T008, T009 | FR-013 | T013 |
| FR-007 | T003, T009 | | |

| SC | Tasks | SC | Tasks |
|---|---|---|---|
| SC-001 | T013 (one instrument, all lanes) | SC-004 | T009, T010 |
| SC-002 | T010 | SC-005 | T002, T003, T012 |
| SC-003 | T009, T010 | SC-006 | T011 |

**No orphan tasks. No uncovered requirement.**

## Findings

### A1 — 🔴 The spec was written after most of the code. That is a real risk and it was tested, not asserted.

FR-001..FR-007 and FR-009..FR-013 describe an artifact that already existed. A specification
written to fit its implementation validates nothing, and the checklist ticks would be free.

**The test applied: could the spec have failed?** It did. **FR-008 is unimplemented** — found by
auditing the code against the written requirements, not by reading the code and describing it. A
specification that produces a live gap on its first audit is doing its job; one that produces none
should be suspected.

**Residual risk, disclosed and not mitigated away:** FR-001..FR-007 remain
implementation-shaped-by-construction. They were *derived* from the delegation mechanism rather
than chosen independently of it. **Their content was decided by measurement (five verified
signatures), not by design**, so the spec-first breach is real. The mitigation is that the
measurement is reproducible by any lane on any host — the requirement is falsifiable even though
it was written second.

### A2 — Terminology is consistent across all three artifacts

`franchise`, `direct`, `delegated`, `REFUSED`, `F1`–`F6` are used identically in spec, plan and
tasks. `F5` (multi-epoch) is deliberately **non-fatal** everywhere: it is live in term 2 because
one record predates the field, and making it fatal would retroactively void a term over a schema
migration.

### A3 — One genuine inconsistency found and resolved

The spec's User Story 3 acceptance scenario 2 says a conflicting franchise "is reported as a
conflict, and the tally does not silently pick one". The first plan draft satisfied that by
reporting **and still counting the first submission** — technically not *silent*, but it still
picks one. **Resolved in favour of the spec's intent**: T006 **excludes** a conflicted franchise
entirely. Recorded here because the plan was wrong and the spec was right, which is the direction
this discipline is supposed to run.

### A4 — Scope boundary holds under pressure

Three items sit outside the boundary and each names an owner rather than trailing off:
the board tally fix (owner of the election code, ruling G31-06, handed over by T015); key
distribution / revocation / replay (needs its own feature); the two Section T failures
(environmental, trust material absent since 2026-08-12).

🔴 **T014 explicitly forbids absorbing the Section T failures into this era's result.** They are
unrelated, they are red, and an era that quietly inherits someone else's red is how a known defect
becomes invisible.

### A5 — The exit-code contract is the interface, and it is fully specified

`0` clean · `1` findings · `2` could not measure. FR-011 and T011 cover all three, and `2` covers
the three distinct unmeasurable cases (no records, unreadable root, no signature library). This
matters more than usual here: the era-102 retrospective closed on *"a zero from a failed build is
not a zero"*, and this tool's output is consumed by other hosts' gates.

## Remediations applied before implementation

| # | Finding | Action |
|---|---|---|
| R1 | A3 — plan contradicted the spec on conflict handling | Plan corrected to **exclude**; T006 rewritten |
| R2 | A1 — spec risked being written to fit the code | FR-008 kept as a **live, unimplemented** gap; T002 required to FAIL first |
| R3 | Risk that F6 subsumes the benign F4 case | **T003** added as an explicit negative control |

## Verdict

**Ready to implement.** One requirement to build, one control that must fail first, and three live
measurements that must come out unchanged — if T010 shows a different tally, FR-008 has altered a
decided election and the era stops.
