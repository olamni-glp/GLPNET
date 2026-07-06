# Blind re-scan record — BB-CRDT-10 (history compaction: columnar op-log encoding)

**042 pass (FR-004/FR-014, research.md R4 protocol)** · date 2026-07-04 · baseline HEAD(6ff3a8c9)
**Original sourcing family**: B (F2, claim B-22). **Re-scanned families**: A (F1 doc only), C (repo, excluding the corpus dir).
**Blindness**: topic only — "compact storage/transfer encodings of CRDT operation histories (columnar, RLE, varint)".

## Family A scan (F1 only)

**NO EVIDENCE IN THIS FAMILY.** No mention of columnar op-log layouts, RLE, varint-packed
histories, or log compaction anywhere in F1 (L1–L308). Nearest-adjacent: L173 (HLC+DVV+slot
columns, field-grained journal with fold/project/replay — op-history handling, no encoding
claim).

## Family C scan (repo, HEAD)

1. Append-only op-WAL exists (one self-verifying file per op) — no compact/columnar/batched layout (`store/OpWal.cs`). HIGH.
2. NEGATIVE: no compaction/snapshot/prune of the WAL anywhere (exhaustive grep). HIGH.
3. Adjacent only: LEB128 varints in the TLV/term codecs (message encoding, not history encoding). HIGH.
4. Bandwidth reduction is delta/Merkle anti-entropy (`store/DeltaMerkle.cs`), not compact history encoding; causal-broadcast op-log design explicitly rejected (041 research.md). HIGH.

## Curator verdict (T018)

**NO-FURTHER-EVIDENCE.** Neither non-corroborating family holds independent evidence for (or
against the design of) columnar history encoding; the block legitimately rests on its single
B-22 (Automerge columnar format) source. PROV/POST standing and the trigger "history sync
becomes bandwidth-relevant" remain correct at HEAD (nothing shipped is bandwidth-bound on
history sync; delta+Merkle is the current answer). Explicit ruling recorded per US2 acceptance
scenario 1. No conflict; no escalation.
