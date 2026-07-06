# Blind re-scan record — BB-RTE-3 (@name loud-fail addressing law)

**042 pass (FR-004/FR-014, R4 protocol)** · 2026-07-04 · baseline HEAD(6ff3a8c9)
**Original family**: C (repo, claim C-24; 040 owner-ruled, born from the 037 silent-fallback defect). **Re-scanned**: A (F1 only), B (F2 only). Blind: topic only — "failure semantics of directed name addressing (unknown recipient: error vs fallback)".

## Family A scan
1. CONTRASTING prior art: olamnit degrades unknown NameId to `unknown:<id>` — "skip/degrade not crash" (F1 L145) — silent-fallback-family semantics in a sibling unit. HIGH.
2. Absence: glpnet L5 fault list enumerates terminal faults with no unknown-recipient fault (F1 L48). MED (inference from omission).
3. Adjacent: intake brief names a dead-letter queue for undeliverables (F1 L60). LOW-MED.
4. Adjacent: same olamnit header refuses forged capability refs (fail) while degrading names — contrasting rules in one envelope (F1 L96). LOW.

## Family B scan
**NO EVIDENCE IN THIS FAMILY.** No entry addresses unknown-recipient semantics; nearest-adjacent:
SOAP mustUnderstand (unrecognized HEADERS, not recipients; F2 L228).

## Curator verdict (T018)
**CONFIRMED — no-further-corroborating-evidence; authority = owner ruling + shipped code.** The
re-scan surfaced no independent corroboration and one CONTRASTING sibling design (olamnit's
degrade). This does not contest the block: BB-RTE-3 is an explicit 040 OWNER RULING (authority
order: owner ruling > repo head > F1 > F2) created precisely to reject fallback semantics after
the 037 defect, and it is implemented twice at HEAD (`glp_quick/src/glp_quick/terminal/
routing.py` `resolve()` — unknown name → ok=False + report, FR-040; 041
`csharp/glp_crdtmsg/route/Addressing.cs` — unknown @name → `CrdtMsgException`, SC-007). The
olamnit contrast is recorded here as design-space context, not a conflict. ACC/CORE standing
stands. No escalation.
