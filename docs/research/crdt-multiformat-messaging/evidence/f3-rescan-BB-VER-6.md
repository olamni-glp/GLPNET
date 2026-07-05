# Blind re-scan record — BB-VER-6 (acyclic-only payloads + CycleGuard)

**042 pass (FR-004/FR-014, R4 protocol)** · 2026-07-04 · baseline HEAD(6ff3a8c9)
**Original family**: C (repo, claim C-18). **Re-scanned**: A (F1 only), B (F2 only). Blind: topic only — "cyclic vs acyclic data in message payloads; cycle detection/handling at codec boundaries".

## Family A scan
**NO EVIDENCE IN THIS FAMILY.** "cycle/cyclic/acyclic/DAG" absent from F1; nearest-adjacent:
L48 payload = ground GLP term (a groundness, not cyclicity, restriction).

## Family B scan
1. Cycle avoidance is a named design property of JSON-CRDT move semantics (F2 L323–324). HIGH.
2. Grow-only signed DAGs / extend-only posets as the basis for secure append-only logs (F2 L383–384). HIGH.
3. Merkle-DAG content addressing (structurally acyclic) as CRDT sync substrate (F2 L365, L368, L371). MED.
Gap: nothing on cycle handling at CODEC boundaries; no encoding entry mentions cyclic data.

## Curator verdict (T018)
**CONFIRMED (partial external corroboration).** Family B corroborates acyclicity-by-design as an
established replicated-data property; the codec-boundary mechanism (CycleGuard,
`CyclicTermException` as transport fault) remains repo-only and is protected by the D5/FORK-1
standing owner gate the block itself records ("the codec never self-defines cycle behavior").
ACC/CORE standing stands. No conflict; no escalation.
