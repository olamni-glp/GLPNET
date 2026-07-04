# Contract — CRDT ops, Store, Rich-text

Traces: FR-015, FR-023..FR-031, FR-036/037/038; SC-003/004/009/012/013.

## C7. Op-based JSON-CRDT ops (FR-023/024/025/029)
- **Invariant**: every op is a **ground term** with `op_id = DVV dot (peer_name, counter)`, causal `deps`, and a `pred_hash` (day-one hash-chain). Delivery over the shipped reliability substrate (monotone seq, bounded-reorder idempotent inbound, N=8 window, single-winner fencing).
- **Invariant**: applying the same op twice (same dot) is idempotent (FR-015).
- **Test**: randomized op-permutation over 2 replicas → identical state (SC-003).

## C8. Semantic tombstone (FR-030)
- **Invariant**: tombstone is an op `{removed_id(dot), causal_context, reason}` with **observed-remove** semantics — an unobserved concurrent add survives.
- **Test**: concurrent add+remove of the same element → add survives.

## C9. crdt_model discriminator (FR-028)
- **Invariant**: `op_based` for the message document; absent → `state_based`. Ordinary (non-CRDT) request/response travels unimpeded.

## C10. Store: op-WAL + projections + Merkle (FR-026/027/031)
- **Invariant**: op-WAL is append-only in the 040 shape (temp→SHA-256→atomic-commit→journal); state is a rebuildable projection; crash→replay→**zero loss** (SC-004). Store **ships first**.
- **Invariant**: two stores converge by exchanging only join-irreducible **delta mutators** discovered via **Merkle** comparison (no causal-broadcast assumption).
- **Invariant**: `op_id` (dot) is the seam, distinct from `msg_id`. Active-path cycle → `CyclicTermException` transport fault (never GLP Fail).

## C11. Fugue sequence (FR-036)
- **Invariant**: insert ops name left/right origins over stable `elem_id=(dot,side)`; ordering by the Fugue tree yields **maximal non-interleaving** — concurrent typing never interleaves.
- **Test**: two peers type concurrently, delivered in randomized order → converged text with **zero interleaving anomaly** (SC-012).

## C12. Peritext formatting (FR-037)
- **Invariant**: marks `{mark_id, type, start_anchor, end_anchor, value}` anchor to stable positions; **marks of unknown type are preserved verbatim** through convergence AND lossless transcode across all 4 surfaces.
- **Test**: overlapping concurrent spans converge; unknown marks survive (SC-013). This is the CRDT-native face of OC-3.

## C13. End-to-end demonstrator (SC-009)
- **Invariant**: one message carrying a `seq-insert` + `mark-add` rich-text op, routed over QUIC between two runtime endpoints, converges on both.
