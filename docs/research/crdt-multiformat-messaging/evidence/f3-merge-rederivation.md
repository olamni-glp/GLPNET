# F3 Merge Re-Derivation — Multi-Family Corroborated Blocks (T016)

**Purpose**: FR-014 in-doc re-derivation of F3's merge decisions for all 37 MULTI-FAMILY
corroborated blocks of `buildingblocks-synthesis.md`, from in-doc data only.
**Method**: for each block, re-derive whether the merged §2 decision is supported by
(a) the §1 Sources cell's claim ids and their families (A-* = F1 sibling-repo prior art,
B-* = F2 external literature, C-* = glpnet repo head state); (b) coherence of the §2
decision text with those families' known scopes (shipped-code reliance ⇒ C-*, standards/
paper reliance ⇒ B-*, sibling-prototype reliance ⇒ A-*); (c) internal consistency of
bin/MVP tier vs decision prose vs the §4 closure-ledger coverage rows. Verdict per block:
COHERENT or CONTESTED. No outside knowledge imported.
**Baseline**: DELIVERY(6ecc975f) for the merge logic; the 042-pass amendments listed in
the document's change log (corrected §3/§4 carrier sets, §8 histogram, ruled-E propagation)
are treated as verified corrections. Where a §1 bin says ESC En, the §6 ruled register
supersedes it per the document's own curator supersession note — such blocks are judged
on that declared mechanism, not marked contested for the stale bin label.
**Date**: 2026-07-04. **Author**: 042 pass (Claude curator agent, T016).

## Re-derivation table (37 blocks)

| Block | Families (from Sources) | Decision essence | Verdict | Note |
|---|---|---|---|---|
| BB-ENC-1 | A,B,C (B-01 A-5 C-4) | One abstract model, N encoding rules (ASN.1 discipline; qmedit built instance) | COHERENT | Standard→B-01, sibling qmedit→A-5, repo→C-4; §4 S1/S7/gap2/C1 rows all carry it |
| BB-ENC-2 | A,C (C-1 A-8) | Binary term payload = shipped Section-15 codec (TermCodec.cs reuse) | COHERENT | Decision rests on shipped code ⇒ C-1 present; parity harness→A-8; no literature invoked, none needed |
| BB-ENC-3 | A,B,C (B-08 C-16 A-11 A-20) | Skippable TLV sections, criticality ranges (NDN-TLV; 038 capturedLen precedent) | COHERENT | Standard→B-08, repo precedent→C-16; §4 S4/gap8/C2/C3 consistent |
| BB-ENC-4 | A,B,C (B-06 C-4 A-16) | Sign over canonical bytes; which canonical form was E4 | COHERENT | §1 bin ESC superseded by §6 E4 ruling → ACCEPTED(ruled), per declared supersession; §4 S3/C1/C6 + §3 OC-2 consistent |
| BB-ENC-5 | A,B,C (A-6 B-07 C-3 C-4) | Interchange matrix as ONE artifact + conformance harness (038 golden discipline) | COHERENT | Repo method→C-3/C-4; surface set E3-ruled (four surfaces), supersession covers the §2 "E3" passage |
| BB-ENC-6 | A,B,C (C-2 B-11 A-13) | Loud-fail decode invariant (ResultEnvelopeCodec; RFC 9413) | COHERENT | Shipped invariant→C-2, standard→B-11; §1 OC=INFRA matches its 042 removal from §3 OC-3 |
| BB-ENC-9 | A,B,C (C-3 A-9 B-04) | Codec soundness bar: goldens now → verified parsers (EverParse) later | COHERENT | Repo (038 goldens, 029 Lean)→C-3, literature bar→B-04; INFRA ⇒ correctly absent from §3 OC rows |
| BB-WIRE-1 | A,B,C (A-1 B-10 C-9) | ONE canonical carrier reconciling 3 envelopes; router opacity preserved | COHERENT | Prior-art envelopes→A-1, head-state JSON-routing fact→C-9; FI+E2 ruled → ACCEPTED(ruled); §4 gap1/S2/C2 consistent |
| BB-WIRE-2 | A,B,C (A-2 B-08 C-6) | Adopt shipped 2-byte payload header + ONE registry (041 glp_wire_registry) | COHERENT | Shipped-code reliance (HANDOFF-36 code-verified, 041 artifact)→C-6 + verified HEAD amendment; §4 gap8/S5/C2 consistent |
| BB-WIRE-3 | A,B,C (A-4 C-5 B-09) | Reuse 025 FrameCodec + 047 reliability semantics; H3 mapping per B-09 | COHERENT | Repo FrameCodec→C-5, mapping literature→B-09 (cited by id in prose), prior-art sublayer semantics→A-4 |
| BB-WIRE-4 | A,B,C (A-3 B-09 C-8 C-10) | Shipped 036 QUIC/WS as transport of record + runtime-reach profiles | COHERENT | Shipped transport→C-8/C-10, RFC 6455/QUIC literature→B-09 |
| BB-HDR-1 | A,B,C (A-10 B-25 C-11) | Canonical header: L5 fields + policy fields + capability slot | COHERENT | Repo L5 fields + 040 scope fact→C-11; E8 ruled → ACCEPTED(ruled); §4 S2/gap7/gap9/C5/C7 all carry it (042-corrected) |
| BB-HDR-2 | A,B,C (A-11 B-12 C-9) | Transparent passthrough: opaque blocks verbatim, relays never re-encode | COHERENT | qmedit skip-by-length→A-11, proto3/#2289→B-12, mesh-forwards-bytes head fact→C-9 |
| BB-HDR-3 | A,B,C (A-12 C-19 B-21) | msg_id + per-link seq + global op id (= DVV dot, E7/E1-ruled) | COHERENT | Shipped link-seq machinery→C-19, DVV literature→B-21; E7/E1 annotations are sanctioned ruling propagation |
| BB-CAP-1 | A,B,C (A-13 B-23 C-13) | Macaroons as MVP capability token; Biscuit as escalation path | COHERENT | Sibling implementations (mstack/beacon/qmedit)→A-13, Biscuit literature→B-23, repo→C-13 |
| BB-CAP-2 | A,B,C (A-14 B-24 C-13) | Amulet as distinct live token type; slot reserved (gap9, E5-ruled) | COHERENT | Prose declares "built NOWHERE (gap9)" — C-13 corroborates the macaroon-coexistence/slot context (it also sources CAP-1/3), not an amulet build; FI+gap-marked is the sanctioned form; E5 ruled |
| BB-CAP-3 | A,B,C (A-15 B-25 C-13) | Verify-before-act at every routed action; refusal ≠ silent drop | COHERENT | Prior art (047 FR-017, beacon gate-then-funnel)→A-15, repo seed (036/040 PeerId-gating)→C-13 |
| BB-CAP-4 | A,C (C-14 A-3) | SPKI-pin mutual TLS = layer-0 membership ONLY, not per-peer identity | COHERENT | Rests on shipped 036 TLS facts ⇒ C-14 present; scoping law needs no literature claim |
| BB-SIG-1 | A,B,C (A-16 B-26 B-27 C-15) | Whole+sub-content multi-signature; qmedit seals vs COSE/JWS, E4-ruled base | COHERENT | Greenfield head-state (absence) fact→C-15, only-built-prototype→A-16, standards→B-26/B-27; E4 ruled → ACCEPTED(ruled) |
| BB-SIG-2 | A,B (A-17 B-26) | Two signature classes: content Ed25519 ≠ capability HMAC | COHERENT | Design principle from prior art→A-17 + standards→B-26; prose invokes no shipped code, so no C-* required |
| BB-SIG-3 | A,B (B-28 A-19) | Ed25519 baseline + suite-agility seam; PQ = SPHINCS+/SLH-DSA | COHERENT | PQ rationale→B-28; interview/suite mandate rides the F1-family claim A-19; no repo reliance |
| BB-SIG-4 | A,B (A-18 B-27) | Hop/forwarding attestation chain (PROV, POST-MVP) | COHERENT | Archived forward(p,P) nesting design is prior art→A-18, chain literature→B-27; PROV bin matches §5 promotion row and §2 prose |
| BB-VER-1 | A,B,C (A-20 B-11 C-16) | Two-tier must-ignore/must-understand + greasing (SOAP archetype, RFC 9413) | COHERENT | Archetype/standard→B-11, TLV-range carrier→C-16/A-20 |
| BB-VER-2 | A,B,C (A-21 B-16 C-16) | Emit-low/accept-range envelope; hard-reject frame/codec | COHERENT | Beacon discipline→A-21, shipped hard-reject invariant→C-16, literature→B-16; split-by-layer matches §8 recorded conflict resolution |
| BB-VER-3 | A,B,C (A-22 B-14 C-17) | Message-level semantic tombstone (NET-NEW; observed-remove; E1-settled semantics) | COHERENT | NET-NEW as message-side construct while claims corroborate ingredients: store precedent (040 WAL)→C-17, semantics literature→B-14, F1 gap3→A-22 — prose states this split explicitly |
| BB-VER-4 | A,B,C (A-23 B-15 C-17) | Repair segment (NET-NEW, OPT): signed join-irreducible delta | COHERENT | Set-reconciliation literature→B-15, 040 resume analogue→C-17; OPT tier consistent with absence from the §7 feed |
| BB-CRDT-1 | A,B,C (A-24 B-17 C-20 C-21) | Two-tier CRDT message ≠ store + seam (E1-ruled substrates, store first) | COHERENT | Prose itself asserts "corroborated by all three families" and the Sources cell bears it out; FI+E1 ruled → ACCEPTED(ruled) |
| BB-CRDT-2 | A,B,C (C-20 A-25 B-18) | Store = append-only op-WAL + rebuildable projections (040 shape); engine = E1 pick | COHERENT | Shipped store shape→C-20, delta+Merkle engine anchor→B-18 (named in E1 ruling), sibling→A-25 |
| BB-CRDT-3 | A,B,C (A-26 B-17 C-26) | CRDT-capable not CRDT-mandatory: crdt_model discriminator | COHERENT | §4 S1/S6/gap6/C4 rows all carry it (S1 added by 042 correction, matching §1 signals) |
| BB-CRDT-4 | A,B,C (B-21 A-25 C-21) | Causality = DVV; dot = stable op identity; hash-chained (E7-ruled) | COHERENT | Literature lineage→B-21; head-state warning cites C-21 by id in prose — exemplary family-scoped use |
| BB-CRDT-5 | A,C (C-19 A-12) | Reuse shipped reliability sublayer; at-least-once + idempotent merge | COHERENT | Rests on shipped code (LinkSequencer/InboundOrdering/SendWindow/Fencing) ⇒ C-19 present; no standards reliance in prose |
| BB-RTE-1 | A,B,C (A-27 B-29 C-23) | Policy fields as declarative data + minimal per-hop matcher (E6-ruled + guard) | COHERENT | "No prior art anywhere" scopes the DSL (gap7 absence claim→A-27); matcher style SIENA→B-29, header carrier→C-23; FI+gap-marked form; E6 ruled |
| BB-RTE-2 | A,C (C-23 C-25 A-3) | Keep running mesh kernel + DROP taxonomy + shipped fault lattice | COHERENT | Rests on shipped kernel/lattice ⇒ C-23/C-25 present; A-3 corroborates; no literature invoked, none needed |
| BB-RTE-4 | A,B (A-28 B-29) | Distance-vector substrate (olamnit 016) as behaviour the policy filters (PROV) | COHERENT | Sibling prototype→A-28, routing literature→B-29; "partial fit" honesty matches PROV/OPT bin and §5 row |
| BB-SCH-1 | A,B,C (A-29 B-02 C-26) | MVP = functor registry (040 tmsg); full language was 3-way conflict → E9 | COHERENT | qmedit DSL built→A-29, CDDL RFC→B-02, tmsg discipline→C-26 — exactly the three conflict legs; E9 ruled → ACCEPTED(ruled, experimental) |
| BB-SCH-2 | A,B,C (A-30 B-03 C-26) | Unified registry: beacon registry + 040 tmsg + Confluent compat modes; hosts E9 dual DSL | COHERENT | Beacon→A-30, Confluent modes→B-03, tmsg + 041 glp_wire_registry single-sourcing→C-26 + verified HEAD amendment |
| BB-SCH-3 | A,B (B-04 A-9) | Kaitai-style codegen with EverParse bar (PROV, POST) | COHERENT | Literature bar→B-04, sibling→A-9; §8's 042-corrected note confirms 2-family (delivered text mis-listed it as a singleton); §4 S1/gap2/C1 rows carry it after correction |

## Tallies

- COHERENT: **37 / 37**
- CONTESTED: **0 / 37**

Cross-cutting observations (informational, not contested):
1. Criterion (c) passes largely by construction against the 042-corrected §3/§4 rows —
   the change log records that those rows were rebuilt to §1-derived carrier sets
   (LR-042-F3-1/2/3), and every §1 Signals/gaps cell of the 37 blocks was re-checked
   here against the corrected rows: no residual mismatch found.
2. Nine ESC-binned §1 rows among the 37 (ENC-4, ENC-5 surface set, WIRE-1, HDR-1,
   CAP-2, SIG-1, CRDT-1, RTE-1, SCH-1) are all covered by the §6 ruled register and the
   curator supersession note; none was marked contested for the stale bin label.
3. The two FI blocks whose prose declares a build gap (BB-CAP-2 "built NOWHERE",
   BB-RTE-1 "no prior art anywhere" for the DSL) carry C-*/A-* claims that corroborate
   context or absence, not implementations — internally consistent with the FI
   "OC-mandated, gaps marked" bin definition in §1.

## Contested blocks (join the T017 re-scan queue)

*(empty — no block's merged decision contradicts its recorded family corroboration)*
