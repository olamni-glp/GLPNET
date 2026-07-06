# Blind re-scan record — BB-CRDT-9 (ground-terms-only law + explicit CorrIds)

**042 pass (FR-004/FR-014, R4 protocol)** · 2026-07-04 · baseline HEAD(6ff3a8c9)
**Original family**: C (repo, claim C-27). **Re-scanned**: A (F1 only), B (F2 only). Blind: topic only — "restricting wire payloads to ground terms; handling of unbound variables/references across the wire".

## Family A scan
1. DIRECT corroboration: "ground-relay discipline (no `_w`/`_r` placeholders cross the wire)" (F1 L43). HIGH.
2. "payload = ground GLP term" in the L5 envelope schema (F1 L48). HIGH.
3. Family-level: "glpnet uses a ground-GLP-term L5 envelope" (F1 L18). HIGH.
4. Correlation half: msg_id/seq attributed to dedup, not reply-correlation (F1 L18) — silent on CorrIds as reply mechanism. LOW.

## Family B scan
1. Content-addressed/value-only replication: replicas exchange hash-identified value nodes, causality via hash links, no live cross-node references (Merkle-CRDTs, F2 L365). HIGH (analogue).
2. IPFS CIDs — identity purely by value hash (F2 L368). HIGH.
3. Fingerprint-only reconciliation without live coordination (MST, F2 L362). MED-HIGH.
4. Deterministic encodings so payloads are self-contained hashable values (F2 L103). MED.
5. Causal deps embedded as hashes IN the payload, replacing external references (BFT-CRDTs, F2 L377–378). MED.
No literature on variable-freeness per se or correlation-id patterns.

## Curator verdict (T018)
**CONFIRMED (strengthened).** Family A directly corroborates the law (F1's independent record of
the 025/036 ground-relay discipline); family B corroborates the value-only/self-contained-payload
design family it belongs to. The explicit-CorrIds half remains repo-designed with D-B2 standing
owner gates, as the block records. ACC/CORE standing stands. No conflict; no escalation.
