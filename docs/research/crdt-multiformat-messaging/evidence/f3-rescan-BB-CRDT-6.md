# Blind re-scan record — BB-CRDT-6 (region-LWW-with-recovery register)

**042 pass (FR-004/FR-014, R4 protocol)** · 2026-07-04 · baseline HEAD(6ff3a8c9)
**Original family**: C (repo, claim C-21; 040 owner-ruled semantics). **Re-scanned**: A (F1 only), B (F2 only). Blind: topic only — "register-style conflict resolution for concurrent region edits (LWW variants, recovery of overwritten values)".

## Family A scan
1. Implemented LWW registers keyed `(hlc, origin_host_id)` — deterministic tie-break — in beacon roadmap CRDT (F1 L174). HIGH.
2. LWW scoped by mutability class (single-writer→LWW; concurrent-mutable→Automerge) in olamnit (F1 L176). HIGH.
3. Per-cell HLC + DVV columns = field-granular resolution machinery (F1 L173). MED.
4. "Parking" preservation-instead-of-drop analogue, migration-scoped (F1 L146). MED.
5. Negative/gap: message-level recovery of removed content declared absent everywhere (F1 L221–222). HIGH.
6. Transient-vs-permanent edit classification: no evidence (nearest: retention tiers, L60).

## Family B scan
1. LWW/MV registers in the canonical CRDT catalogue (RR-7506, F2 L259–260). HIGH.
2. Production per-column LWW (cr-sqlite, F2 L338–339). HIGH.
3. DVV/DVVS concurrent-version DETECTION — the mechanism making concurrent writes preservable (F2 L304–308). MED.
4. OpSets formal register semantics via total operation order (F2 L313). MED.
No tie-break-mechanics or overwritten-value-recovery literature beyond the above.

## Curator verdict (T018)
**CONFIRMED.** Both families corroborate the register family and, critically, the block's own
open item: the needed concurrency tie-break should be deterministic identity-order (beacon's
`(hlc, origin)` practice; DVV dot order per E1's seam) rather than arrival-order — exactly the
"tie-break open" note the block carries. 040-ruled recovery semantics (saved-original always
recoverable) stay owner-authority. ACC (tie-break open)/OPT standing stands; the tie-break
remains an open design item, already visible in the block text — no status change, no
escalation.
