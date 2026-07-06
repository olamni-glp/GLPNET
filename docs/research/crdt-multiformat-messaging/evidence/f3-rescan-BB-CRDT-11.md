# Blind re-scan record — BB-CRDT-11 (durable provenance records incl. refusals)

**042 pass (FR-004/FR-014, R4 protocol)** · 2026-07-04 · baseline HEAD(6ff3a8c9)
**Original family**: C (repo, claim C-22; 040 FR-037/SC-009 shape). **Re-scanned**: A (F1 only), B (F2 only). Blind: topic only — "durable provenance/audit records of operations including refused/denied actions".

## Family A scan
1. Identity-keyed refusal VERDICTS exist (040 `rcopy_verdict reject(quota|perm|path)` keyed to authenticated PeerId, F1 L49) — verdicts, not stated as durably recorded. MED.
2. Durable append-only per-op journal with op_id/identity/timestamp + signed exports (beacon, F1 L174). MED.
3. Custody Record / Delivery Ledger entities (mstack, F1 L192, L178). LOW-MED.
4. Field-grained replayable journal (buildkit, F1 L173). LOW-MED.
5. Durable relay journaling + DLQ (olamnit K2, F1 L176). LOW.
Explicit negative: refused-action audit RECORDS described nowhere in F1.

## Family B scan
1. Append-only Merkle signed logs as the tamper-evident op-history model (CT v2/CT, F2 L478, L481–482). HIGH.
2. Production signature-transparency log (Rekor, F2 L484). HIGH.
3. Append-only authenticated dictionaries as the op-log backbone (F2 L493). HIGH.
4. Keybase sigchain: per-link signed, seqno + prev-hash (F2 L496–497). HIGH.
5. §12 designates CT/CONIKS/SEEMless/AAD for the tamper-evident signed op-log (F2 L586). HIGH.
Explicit negative: no literature on auditing REFUSED/denied actions specifically.

## Curator verdict (T018)
**CONFIRMED (partial corroboration).** Both families corroborate the durable, tamper-evident
per-operation provenance substrate; the block's distinctive INCLUDING-REFUSALS clause remains
repo authority (040 FR-037/SC-009 shape; carried into 041's provenance surface) — an owner-ruled
extension beyond the literature, not a contradiction. ACC/OPT standing stands. No conflict; no
escalation.
