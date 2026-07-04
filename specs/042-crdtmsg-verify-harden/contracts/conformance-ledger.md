# Contract: Conformance Ledger (FR-001..FR-003, SC-001)

One ledger per deliverable (F1/F2/F3), embedded as report §2 subsections. The ledger is the
per-deliverable table of method elements × verdict × evidence (spec Key Entities).

## Row format

| Column | Rule |
|---|---|
| finding_id | `CF-<F#>-<seq>`, unique, stable once assigned |
| element | one of: source manifest · claim schema · rubric · failure-mode guards · stop rules — ALL FIVE rows mandatory per deliverable, plus one row per additional method element discovered during reconstruction (e.g. F3's authority order, cycle protocol) |
| provenance | RECORDED \| RECONSTRUCTED (from the §1 MethodElement table) |
| verdict | PASS \| GAP \| DEVIATION |
| evidence | verbatim quote(s) with `doc:line` refs at the DELIVERY baseline commit; "no evidence" is not a legal value — a GAP's evidence is the quote showing the absence or the pointer to where the record was said to live |
| baseline | `DELIVERY(<commit>)` — always delivery-time for conformance (FR-015) |

## Verdict semantics

- **PASS** — the deliverable demonstrably implements the element; evidence quoted.
- **GAP** — the element's execution record is absent/incomplete in-repo (US1 acceptance
  scenario 2): the row records what is missing, where it was said to live, and the chosen
  disposition (e.g. FR-014 targeted re-execution, in-doc-summary baseline, or unrecoverable).
- **DEVIATION** — the pipeline executed differently from its frozen method. Mandatory
  extra fields: `deviation_class ∈ {harmless, weakens-a-claim, invalidates-a-claim}` and,
  for the non-harmless classes, the enumerated affected downstream claims/blocks (FR-003).

## Completeness gate (SC-001)

A ledger is complete iff: every method element from report §1 has exactly one row; zero rows
have empty evidence; every DEVIATION row is classified. The report's §2 totals line per
ledger asserts this arithmetic.
