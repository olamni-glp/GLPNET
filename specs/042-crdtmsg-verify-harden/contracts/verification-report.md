# Contract: Verification Report (FR-012, FR-015, SC-009)

**Artifact**: `docs/research/crdt-multiformat-messaging/verification-report-042.md`

The single consolidated output of the pass. Downstream consumers and future audits read this
first; the three hardened docs each reference it (SC-009). Every finding row carries a
baseline label `DELIVERY(<commit>)` or `HEAD(<commit>)` (FR-015).

## Required sections (order fixed)

```markdown
# Verification Report — 042-crdtmsg-verify-harden
> Run date · pass executor · baselines table (the 5 rows from plan.md) · verdict summary line

## 1. Method reconstruction (FR-001)
   3 subsections (F1/F2/F3), each a 5-row MethodElement table (RECORDED/RECONSTRUCTED).

## 2. Conformance ledgers (FR-002/FR-003, SC-001)
   3 subsections, one ledger each: element × verdict(PASS/GAP/DEVIATION) × verbatim evidence.
   Deviations carry class + affected-claims enumeration. Totals line per ledger
   (e.g. "F3: 5 elements → 4 PASS · 1 GAP · 0 DEVIATION").

## 3. Singleton re-adjudication (FR-004/FR-014, SC-002)
   First: the derivation of the authoritative 9-block list from F3 §1 (research.md R3.1).
   Then a 9-row verdict table; each row links its evidence/ rescan record(s).

## 4. Coverage-ledger re-derivation (FR-005, SC-004)
   4 subsections (F1 §12, F2 §11, F3 §3, F3 §4): REPRODUCED-EXACTLY or the full
   discrepancy enumeration + correction refs. The F3 §4 subsection states the final
   28/28 (or corrected) closure count.

## 5. Drift dispositions (FR-006, SC-005)
   4-row table: item × disposition × evidence × follow-up ref.

## 6. Ruling propagation (FR-007, SC-006)
   E1–E9 × appearance sweep results; corrections made; final inconsistency count (must be 0).

## 7. PROVISIONAL register closure (FR-008/FR-009, SC-003)
   8-row adjudication table. Then TWO mandatory batch lists:
   7a. **Promotions for owner review** — every self-promoted row with its quoted evidence.
   7b. **Escalations** — ambiguous triggers / judgment calls, stated neutrally.

## 8. Evidence-pointer census (FR-010, SC-007)
   Totals by class × resolution; the full census lives in evidence/evidence-index.md and is
   linked; unrecoverable/host-blocked/link-rot items are listed inline with disposition notes.

## 9. Owner escalations (FR-013)
   Consolidated list: every escalated item from §3/§6/§7 + any hardened-verdict-vs-041
   contradiction (stated, never self-ruled, never patched into 041).

## 10. Proposed roadmap follow-ups (FR-009)
   Named, one-paragraph proposals; explicitly NOT implemented here.

## 11. Amendment index (FR-011, SC-008)
   Per document: count of change-log entries + link to each doc's change-log section.
   Every finding_id referenced by an amendment must appear in §1–§8.

## 12. Success-criteria checklist
   SC-001..SC-009, each with its measured value and PASS/FAIL.
```

## Invariants

- No section may be omitted; an empty section states why it is empty.
- Every claim of fact quotes or links its evidence (doc line, commit, file, or evidence/ record).
- Finding ids are unique across the report and are the join key for change-log entries.
- The report never rules a contested decision — contested = §9 escalation.
