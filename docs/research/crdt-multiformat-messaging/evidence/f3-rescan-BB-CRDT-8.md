# Blind re-scan record — BB-CRDT-8 (Byzantine branch: hash-chained ops → blocklace)

**042 pass (FR-004/FR-014, research.md R4 protocol)** · date 2026-07-04 · baseline HEAD(6ff3a8c9)
**Original sourcing family**: B (F2, claim B-19). **Re-scanned families**: A (F1 doc only), C (repo, excluding the corpus dir).
**Blindness**: topic only — "Byzantine-tolerant CRDTs, hash-chained operation histories, equivocation handling".

## Family A scan (F1 only)

1. Blocklace prior art in glpnet's archived paper: "blocklace = 'eventual consistency similar to CRDTs' (cites Shapiro 2011)" (F1 L177). MED (pre-cutoff, definitional).
2. Nested attestation `forward(p,P)` verifiable forwarding chain (F1 L127). MED.
3. §15 row confirms `main_GLP_to_Dart (3).tex:619` carries both (F1 L299). HIGH.
4. Nested attestation chains recommended as research input (F1 L257). HIGH (prospective).
5. Weak adjacent: Ed25519-signed whole-export of append-only journal (F1 L174). LOW.
6. Not supported: BFT replication as such, equivocation detection, per-entry hash-linking.

## Family C scan (repo, HEAD)

1. Day-one op hash-chain SHIPPED: `csharp/glp_crdtmsg/crdt/Dot.cs` L96–116 (pred_hash = SHA-256 over sorted canonical predecessor dots + own dot, "preserves the blocklace/Byzantine upgrade path"); `Op.cs` L18–20. HIGH.
2. Hash chain tested: deterministic, dep-order-independent, dep-sensitive (`FoundationTests.cs` T009). HIGH.
3. Biscuit-style append-only per-block seal chain SHIPPED (`sig/Seals.cs`: removal/reorder/truncation/tamper breaks verification, SC-005). HIGH.
4. Ops form a hash-chained DAG; projection treats residual cycles as corruption with deterministic fallback (`store/Projection.cs` L74–79). HIGH.
5. Spec mandates hash-chained op ids day-one but DEFERS full Byzantine tolerance (041 spec FR-025 L158; L219 "Full Byzantine (BB-CRDT-8) is deferred"). HIGH.
6. GLP blocklace programs exist (`programs/typed_book/constitutional_consensus/`), without Byzantine/equivocation logic in the GLP code. HIGH/MED.

## Curator verdict (T018)

**CONFIRMED.** The E7-ruled split (hash-chained op ids now; full blocklace deferred until
untrusted peers) is corroborated by two additional families: A holds the blocklace/nested-
attestation lineage; C holds the SHIPPED day-one hash chain exactly as ruled. The block's
ESC-E7-ruled / OPT (deferred blocklace) status stands. No conflict; no escalation.
