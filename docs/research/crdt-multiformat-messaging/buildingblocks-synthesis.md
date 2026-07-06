# Building-Blocks Synthesis — CRDT Multi-Format Messaging Epic

**Feature**: `crdtmsg-buildingblocks-synthesis` (F3). **Date**: 2026-07-04.
**Provenance**: F1 `priorart-sibling-scan.md` + F2 `webresearch-corpus.md` (both in this directory) + live glpnet repo at head (branch `037-virtual-3270-term`, post-040-implement).
**Verification**: audited & hardened by feature 042 — see [verification-report-042.md](verification-report-042.md) and the terminal change log.
**Method**: three-role pattern (see `docs/research/three-role-agent-teams/METHOD-AND-DOGFOOD.md`). Planning triad: Claude generator + blind codex validator + curator merge (8 recorded decisions; key: 3 scanners, one per evidence FAMILY — two same-family scanners cannot corroborate). Execution triad: 3 blind scanners (A=F1 doc only, B=F2 corpus only, C=live-repo pinned set only) emitted 86 schema-conformant claims (A:30, B:29, C:27); evaluator merged by design-slot clustering + family intersection + counter-queries; curator wrote this document. Conflicts are ESCALATED, never self-decided. Authority order (validator V-3.5, adopted): brief constraints > repo head > F1 > F2 > inference.

---

## 0. Overriding constraints (verbatim from the roadmap brief)

*"safety + security via amulets + macaroon signatures + multi-signature (whole-content AND sub-content), transparent transport of formatting + additional formatting payloads; CRDT-first (deliver CRDT-capable services / stores / documents)."*

- **OC-1** capability layer: amulets + macaroon signatures
- **OC-2** multi-signature: whole-content AND sub-content
- **OC-3** transparent transport of formatting + additional formatting payloads
- **OC-4** CRDT-first: CRDT-capable services / stores / documents

Shared vocabulary: signals S1–S8 (F1 §2) · gaps gap1–gap9 (F1 §13 = F2 §11) · clusters C1–C7 (F2 §12).

---

## 1. Executive block map

Bins: **ACC** accepted · **PROV** provisional · **ESC** escalated (owner decision required) · **FI** force-include (OC-mandated, gaps marked). Sources = contributing claim ids.

| Block | Title | Bin | MVP | OC | Signals/gaps | Sources |
|---|---|---|---|---|---|---|
| BB-ENC-1 | One abstract model, N encoding rules | ACC | CORE | OC-3 | S1 S7 gap2 C1 | B-01 A-5 C-4 |
| BB-ENC-2 | Binary term payload = 038/029 Section-15 codec | ACC | CORE | OC-3 OC-4 | S1 C1 | C-1 A-8 |
| BB-ENC-3 | Skip-tolerant section container (TLV, criticality ranges) | ACC | CORE | OC-3 | S4 gap8 C2 C3 | B-08 C-16 A-11 A-20 |
| BB-ENC-4 | Canonicalization-for-signing rule | **ESC E4** | CORE | OC-2 | S3 C1 C6 | B-06 C-4 A-16 |
| BB-ENC-5 | Interchange matrix as ONE artifact + conformance harness | ACC (surface set **ESC E3**) | CORE | OC-3 | gap2 S1 C1 | A-6 B-07 C-3 C-4 |
| BB-ENC-6 | Loud-fail decode invariant | ACC | CORE | INFRA | S4 C3 | C-2 B-11 A-13 |
| BB-ENC-7 | CBOR generic-payload surface | ACC (ruled E3) | CORE | OC-3 | S1 gap2 | B-05 (+F1 qmedit CBOR-TLV) |
| BB-ENC-8 | Markdown surface | PROV | POST | OC-3 | gap2 | A-7 |
| BB-ENC-9 | Codec soundness bar (goldens → verified parsers) | ACC | CORE | INFRA | S1 C1 | C-3 A-9 B-04 |
| BB-WIRE-1 | Unified wire: 3-envelope reconciliation | **FI + ESC E2** | CORE | OC-3 OC-1 | gap1 S2 C2 | A-1 B-10 C-9 |
| BB-WIRE-2 | Payload-type discriminator + registry | ACC | CORE | OC-3 | gap8 S5 C2 | A-2 B-08 C-6 |
| BB-WIRE-3 | Frame layer: 025 FrameCodec + reliability sublayer | ACC | CORE | INFRA | S5 C2 | A-4 C-5 B-09 |
| BB-WIRE-4 | QUIC/WS transport of record + runtime profiles | ACC | CORE | INFRA | S5 C2 | A-3 B-09 C-8 C-10 |
| BB-WIRE-5 | Endianness layering law (BE frame ⊃ LE payload) | ACC | CORE | INFRA | gap1 C2 | C-7 (+F1 §3) |
| BB-HDR-1 | Canonical header: routing + policy + capability slots | ACC (**ESC E8** for 036-contract change) | CORE | OC-1 | S2 gap7 gap9 C5 C7 | A-10 B-25 C-11 |
| BB-HDR-2 | Transparent passthrough law (opaque blocks verbatim) | ACC | CORE | OC-3 OC-2 | S3 S4 C3 | A-11 B-12 C-9 |
| BB-HDR-3 | Dedup/idempotency: msg_id + per-link seq + global op id | ACC | CORE | OC-4 | S2 S6 C2 C4 | A-12 C-19 B-21 |
| BB-HDR-4 | Identity law: name = link-authenticated identity | ACC (owner-ruled 040) | CORE | OC-1 | S2 S8 C5 | C-12 |
| BB-CAP-1 | Macaroon capability tokens (fail-closed caveats) | ACC | CORE | OC-1 | S2 C5 | A-13 B-23 C-13 |
| BB-CAP-2 | Amulet as distinct live token type | **FI + ESC E5** | CORE (slot) | OC-1 | gap9 S2 C5 | A-14 B-24 C-13 |
| BB-CAP-3 | Verify-before-act at every routed action | ACC | CORE | OC-1 | S2 C5 | A-15 B-25 C-13 |
| BB-CAP-4 | SPKI-pin mutual TLS = layer-0 membership ONLY | ACC | CORE | OC-1 | C5 S2 | C-14 A-3 |
| BB-SIG-1 | First-class multi-signature (whole + sub-content) | **FI**, format base **ESC E4** | CORE | OC-2 | gap5 S3 C6 | A-16 B-26 B-27 C-15 |
| BB-SIG-2 | Two signature classes: content Ed25519 ≠ capability HMAC | ACC | CORE | OC-2 OC-1 | S3 C5 C6 | A-17 B-26 |
| BB-SIG-3 | Ed25519 baseline + suite-agility/PQ seam | ACC | CORE | OC-2 | S3 C6 | B-28 A-19 |
| BB-SIG-4 | Hop/forwarding attestation chain | PROV | POST | OC-2 | S3 S8 C6 | A-18 B-27 |
| BB-VER-1 | Two-tier must-ignore/must-understand + greasing | ACC | CORE | OC-3 | S4 C3 | A-20 B-11 C-16 |
| BB-VER-2 | Version discipline: emit-low/accept-range envelope; hard-reject frame/codec | ACC | CORE | OC-3 | S4 C3 | A-21 B-16 C-16 |
| BB-VER-3 | Message-level semantic tombstone (NET-NEW) | ACC (semantics inside **E1**) | CORE | OC-4 OC-2 | gap3 S4 C3 | A-22 B-14 C-17 |
| BB-VER-4 | Repair segment (NET-NEW) | ACC | OPT | OC-4 OC-2 | gap4 S4 C3 | A-23 B-15 C-17 |
| BB-VER-5 | Cross-version translation: Avro fast path → lens seam | PROV | OPT | OC-4 OC-3 | S4 gap6 C3 | B-13 |
| BB-VER-6 | Acyclic-only payloads + CycleGuard (D5 standing gate) | ACC | CORE | OC-4 | C3 C4 | C-18 |
| BB-CRDT-1 | Two-tier CRDT: message-CRDT ≠ store-CRDT + seam | **FI + ESC E1** | CORE | OC-4 | gap6 S6 C4 | A-24 B-17 C-20 C-21 |
| BB-CRDT-2 | Store side: append-only op-WAL + rebuildable projections | ACC (engine pick in E1) | CORE | OC-4 | S6 gap6 C4 | C-20 A-25 B-18 |
| BB-CRDT-3 | CRDT-capable, not CRDT-mandatory (`crdt_model` discriminator) | ACC | CORE | OC-4 OC-3 | S6 S1 gap6 C4 | A-26 B-17 C-26 |
| BB-CRDT-4 | Causality: dotted version vectors; dot = stable op identity | ACC | CORE | OC-4 | S6 gap6 C4 | B-21 A-25 C-21 |
| BB-CRDT-5 | Delivery substrate reuse: ordering/dedup/fencing/backpressure | ACC | CORE | OC-4 | S6 S4 C4 | C-19 A-12 |
| BB-CRDT-6 | Region-LWW-with-recovery register (040 ruled semantics) | ACC (tie-break open) | OPT | OC-4 | S6 gap6 C4 | C-21 |
| BB-CRDT-7 | Sequence/rich-text CRDT: Fugue + Peritext spans | ACC (041-shipped) | OPT | OC-3 OC-4 | S6 S4 gap3 C4 | B-20 (+F1 §14 rec) |
| BB-CRDT-8 | Byzantine branch: hash-chained ops → blocklace | **ESC E7** | OPT | OC-4 OC-2 | S6 S3 gap5 C4 | B-19 |
| BB-CRDT-9 | Ground-terms-only law + explicit CorrIds | ACC (D-B2 standing gates) | CORE | OC-4 | S6 gap6 C4 | C-27 |
| BB-CRDT-10 | History compaction: columnar op-log encoding | PROV | POST | OC-4 | S1 S6 C4 | B-22 |
| BB-CRDT-11 | Durable provenance records incl. refusals | ACC | OPT | OC-1 OC-4 | S8 C4 | C-22 |
| BB-RTE-1 | Policy fields (targets/waypoints/excludes) + minimal matcher | **FI + ESC E6** | CORE (fields) | OC-1 | gap7 S2 C7 | A-27 B-29 C-23 |
| BB-RTE-2 | Routing kernel + DROP taxonomy + fault lattice | ACC | CORE | OC-3 INFRA | gap7 S2 S4 C3 C7 | C-23 C-25 A-3 |
| BB-RTE-3 | @name loud-fail addressing law | ACC (owner-ruled 040) | CORE | OC-1 OC-3 | S2 C7 | C-24 |
| BB-RTE-4 | Distance-vector routing substrate | PROV | OPT | INFRA | S2 C7 | A-28 B-29 |
| BB-SCH-1 | Schema language selection | **ESC E9** (MVP = functor registry) | CORE (MVP part) | OC-3 OC-4 | S7 C1 | A-29 B-02 C-26 |
| BB-SCH-2 | Unified message-type registry + compat modes | ACC | CORE | OC-3 OC-1 | S7 S4 gap1 C1 C2 | A-30 B-03 C-26 |
| BB-SCH-3 | Verified codegen path (Kaitai-style → EverParse bar) | PROV | POST | INFRA | S1 gap2 C1 | B-04 A-9 |

---

## 2. Block catalog — decisions (condensed; full per-claim field detail lived in the three scanners' claim sets, held only in the F3 run-record session transcripts, which were not persisted — dispositioned unrecoverable by the 042 pass, superseded as evidence-of-record by the 042 re-execution records in [evidence/](evidence/) — see §8; no in-doc appendix exists (042 report CF-F3-2))

### ENC — encodings & interchange
- **BB-ENC-1**: Define every message type once in an encoding-neutral abstract model; each surface (binary/JSON/YAML/term/md) is an encoding rule against it (ASN.1 X.680 discipline; qmedit's 3-surface design is the built instance). Never per-surface independent schemas.
- **BB-ENC-2**: The binary encoding of GLP-term payloads is the shipped Section-15 codec: LEB128 varints, 8-byte LE int64, IEEE-754 doubles, varint+UTF-8 strings, tags 0x00–0x07 (0x07 = unbound VarRef/GlobalVarId). Reuse `csharp/glp_result_codec/TermCodec.cs` + Gleam/Dart parity harness. GATES: float tag (ED-6 AtomVM), int64 bignum edges, "final" status rides D4 freeze.
- **BB-ENC-3**: Envelope sections are length-prefixed skippable TLV records (NDN-TLV model; LEB128; the 038 `capturedLen` field is the in-repo precedent); type-number ranges encode criticality (ignorable vs must-understand). Skip-by-LENGTH is lawful; skip-by-TAG stays loud-fail.
- **BB-ENC-4** (**E4**): signing must operate over canonical bytes; WHICH canonical form (deterministic-binary per surface vs one abstract-model canonical form; surface-pinned vs re-derivable signatures across transcode) is an owner ruling — it jointly gates OC-2 × OC-3.
- **BB-ENC-5**: The ≥3-way interchange (gap2) is ONE artifact: abstract model + per-surface codecs + a mandatory pairwise lossless round-trip conformance matrix incl. unknown-field preservation. Method: 038 golden-corpus discipline (one truth runtime authors goldens; every runtime byte-identical; gated cases quarantined explicitly). WHICH surfaces are MVP is **E3**.
- **BB-ENC-6**: Every decoder consumes all bytes or throws — reject bad version/payloadType/tag, truncation, trailing bytes (`ResultEnvelopeCodec.Decode` invariant; RFC 9413 anti-tolerance guardrail: strict on knowns, tolerant only on declared-skippable unknowns).
- **BB-ENC-7** (ACCEPTED — E3-ruled, MVP-CORE): CBOR (RFC 8949) as the generic (non-term) binary payload surface — self-describing, tags pass through, standardized deterministic profile. Counter-query: F1 corroborates the family (qmedit CBOR-TLV; F1 §14 recommends CBOR/MessagePack TLV to F2). Adopted by E3 as the fourth MVP surface (binary-term ⇄ JSON ⇄ YAML ⇄ CBOR, conformance matrix from day one).
- **BB-ENC-8** (PROV, POST-MVP): markdown surface exists nowhere (F1); design later, decide lossless vs render-only projection.
- **BB-ENC-9**: acceptance bar per surface pair: golden byte-parity vectors now (038 method), parse∘serialize=id property tests, EverParse-grade verified parsing as the long-term bar (029 Lean proof is the in-repo precedent).

### WIRE — framing, transport, unified wire
- **BB-WIRE-1** (**FI**, **E2**): build the ONE canonical carrier reconciling beacon-JSON / olamnit-48B / glpnet-L5 (buildkit-047-D2 mandate). Head-state fact: the running mesh routes JSON L5 via `JsonDocument` and never touches the binary codecs — the unified wire migrates `Mesh.TryRoute` onto a binary payload-typed header while PRESERVING router payload-opacity (bytes forwarded unchanged is what keeps end-to-end signatures valid). Escalated: byte layout, lens-vs-projection reconciliation, router opacity rule.
- **BB-WIRE-2**: payload-type discriminator: adopt the shipped 2-byte payload header (0x01 version + payloadType); registry today 0x10=IL, 0x11=RESULT_ENVELOPE; allocate 0x12+ for messaging kinds; FrameKind CANNOT serve this role (HANDOFF-36, code-verified). ONE registry artifact SHIPPED by 041 (`csharp/glp_wire_registry`: `WireRegistry`/`PayloadType`, with 0x12 = `crdt_message` allocated; feeds BB-SCH-2) — the former two-assembly constant duplication is resolved: `PayloadHeader` and `ResultEnvelopeCodec` const-alias the registry (SC-010).
- **BB-WIRE-3**: reuse 025 FrameCodec (version byte, 22-byte BE header, per-chunk CRC-32, Whole/Fragment, 64 MiB guard, forged-length rejection) + 047 reliability sublayer semantics (FIFO seq/dedup, credit-window backpressure) as the frame discipline; QUIC-stream/H3-typed-frame mapping per B-09.
- **BB-WIRE-4**: transport of record = shipped 036 QUIC/WS (MsQuic + RFC 6455 over one bidi stream, SPKI pinning, enumerated terminal faults) behind `ILinkTransport`; runtime-reach law: AtomVM/WASM nodes delegate QUIC to a native side-process (Profile A), full-BEAM uses quicer (Profile C) — contract-level interchangeable, never assume in-runtime QUIC.
- **BB-WIRE-5**: endianness is layered and FROZEN: BE frame header ⊃ LE term payload. Never harmonize — either direction breaks a shipped golden/parity suite.

### HDR — canonical header
- **BB-HDR-1**: unified header = L5 fields {msg_id, from, to, seq} + routing-policy fields (must-have targets, waypoints, exclude lists — the brief's exact set, wire slots at MVP even if the DSL comes later) + a capability slot (macaroon/amulet), opaque to the router. **E8**: extending the 036 wire contract needs owner sanction (040 declared it out of scope) + per-link vs per-message capability granularity.
- **BB-HDR-2**: transparent passthrough law: formatting + additional payloads travel as opaque head-tagged blocks carried VERBATIM (qmedit skip-by-length; proto3 unknown-field retention; the #2289 transcode-loss regression is the named test). Relays never re-encode; field-by-field copy APIs forbidden.
- **BB-HDR-3**: `msg_id` end-to-end + per-link `seq` FIFO dedup + a globally-unique op id above the transport (link seq is per-session); idempotent apply at the store boundary; the DVV dot (actor, counter) is the natural op identity (ties to BB-CRDT-4). E7/E1-ruled: the op id is hash-chained from day one, and op_id = DVV dot (authenticated-peer-name, counter), distinct from `msg_id`.
- **BB-HDR-4**: identity law (040-ruled): a peer's name IS its link-authenticated mesh identity; first-come owns the routable id; dup-id tracked-never-addressable; incumbent-keeps-route on removal. CRDT actor ids key to this identity. Known limit: shared-cert domain means any member can claim an UNUSED name. E4-ruled: per-peer Ed25519 signing keys are enrolled at mesh join and bound to this name (per-peer identity above the shared cert); per-peer transport certificates remain future work (with BB-CAP-4).

### CAP — capabilities
- **BB-CAP-1**: macaroons (HMAC-chained caveats, offline attenuation) as the MVP capability token — five STRONG internal implementations exist (mstack/beacon reference; qmedit fail-closed rule: unsatisfiable OR un-understood caveat → fail). Biscuit is the named escalation path if public-key third-party verify becomes required. Byte-format winner (mstack chain vs beacon composite) to be ruled under D2 unification.
- **BB-CAP-2** (**FI**, **E5**): amulet = Amoeba-style static sparse token {Port 48b, ObjNum 24b, Rights 8b, Check 48b} per the principal interview — a DISTINCT token type coexisting with macaroons; defined but built NOWHERE (gap9). Owner must rule: literal 16-byte fidelity vs modern Check-width, Rights-bit semantics. MVP reserves the header slot; CHERI-style monotonic narrowing on derivation.
- **BB-CAP-3**: verify-before-act at EVERY routed action (047 FR-017; beacon gate-then-funnel; 036/040 PeerId-gating is the in-repo generalization seed); refusal is a distinct outcome, never a silent drop. Revocation via CapabilityKernel seam post-MVP.
- **BB-CAP-4**: SPKI-pin mutual TLS (shared self-signed cert, `ClientCertificateRequired=true`) is layer-0 MEMBERSHIP only — possession=membership, NOT per-peer identity. Layer capabilities above it; never mistake it for per-peer PKI.

### SIG — signatures
- **BB-SIG-1** (**FI**, format base in **E4**): first-class whole+sub-content multi-signature is greenfield in glpnet (head state: CRC-32 + shared-key TLS only). Design anchors, to be reconciled by owner ruling: qmedit per-block seals (ed25519+macaroon per block, union-of-seals footer, SC-005 tamper→100% fail — the only built prototype) vs COSE_Sign/JWS-detached (standards, native multi-signer) with Biscuit-style append-only per-block chain (removal/reorder detectable), MTL amortization, BLS reserved. Blocking: sub-content addressing scheme (needs BB-ENC-3 section identity). Good news from head state: the mesh forwards bytes unchanged, so end-to-end signatures survive routing.
- **BB-SIG-2**: two signature classes, never conflated: content attestation (Ed25519) and capability (macaroon HMAC) — each with its own verify path; attenuating a capability must not invalidate content history.
- **BB-SIG-3**: Ed25519 (deterministic, no per-signature RNG) is the baseline; suite-identifier field reserved (interview mandates negotiated suites later); PQ path = SPHINCS+/SLH-DSA specifically because stateless hash-based signing is the only PQ family safe for concurrent CRDT signers.
- **BB-SIG-4** (PROV, POST-MVP): hop/forwarding attestation from the archived `forward(p,P)` nesting design; per-link caveats authorize hops but do not attest provenance.

### VER — versioning, tombstone, repair
- **BB-VER-1**: two-tier extension model: must-ignore default + explicit must-understand flag (SOAP archetype), criticality encoded in TLV type ranges; mandatory greasing keeps skip paths exercised; strict validation of knowns (RFC 9413).
- **BB-VER-2**: envelope: emit-low/accept-range with additive-optional-superset evolution (beacon discipline, tested); frame + term codec: hard-reject version bytes (shipped invariant). Schema-version id embedded per message; migrations must be convergent/idempotent (concurrent duplicate migration must merge).
- **BB-VER-3** (NET-NEW): message-level semantic tombstone: first-class sealed block type carrying removed-element identity + causal context (dots) + reason; observed-remove semantics (never resurrects unobserved concurrent adds); unknowns-preserved through transcode; compaction criterion = causal stability (deferrable). Store-side precedent: WAL replay (040); message-side is new. Interaction with the CRDT model is settled inside **E1**.
- **BB-VER-4** (NET-NEW, OPT): repair segment: minimal join-irreducible delta restoring a damaged/removed element, addressed by stable element identity; discovery via range-based set reconciliation; repair segments are THEMSELVES signed sub-content (else repair = unauthenticated overwrite channel); 040's "resume = re-send still-missing items" is the shipped file-granularity analogue.
- **BB-VER-5** (PROV): cross-version translation: MVP = Avro-style reader/writer resolution (add/remove fields); reserve the Cambria edit-lens seam for restructuring migrations and CRDT-op streams crossing schema versions.
- **BB-VER-6**: acyclic-only term payloads for MVP; reuse `CycleGuard` (DAG sharing OK, active-path cycle → `CyclicTermException` as transport fault, never GLP Fail). D5/FORK-1 cyclic-term ruling is a STANDING OWNER GATE — the codec never self-defines cycle behavior.

### CRDT — convergence
- **BB-CRDT-1** (**FI**, **E1** — the epic's central decision): two-tier model, corroborated by all three families: CRDT-of-the-MESSAGE (in-flight convergence semantics; candidates: qmedit's three selectable models / pure op-based JSON-CRDT over causal delivery) ≠ CRDT-of-the-STORE (durable convergence; candidates: buildkit/beacon roadmap_crdt HLC/DVV/OR-Set/journal-fold — property-tested, or delta-CRDT + Merkle anti-entropy), joined by a defined seam (op identity: does msg_id = op_id = DVV dot?). Owner must ratify substrates + seam + which layer MVP ships first.
- **BB-CRDT-2**: store skeleton = append-only op-WAL as source of truth + rebuildable projections (040 responder store shape: temp → SHA-256 verify → atomic commit → journal; SC-010 zero-loss rebuild). Single-writer today; multi-replica merge machinery comes from the E1 engine pick.
- **BB-CRDT-3**: CRDT-capable, not CRDT-mandatory: per-doc/message `crdt_model` discriminator (state-based default; op-based; append-prepend log; absent → state-based); payload-type kinds distinguish op-stream vs store-sync vs plain messages. Ordinary request/response must travel unimpeded.
- **BB-CRDT-4**: causality = dotted version vectors (O(actors) metadata; production lineage); the dot is the stable identity that tombstones, repairs, and sub-signatures address. Head-state warning (C-21): current region-LWW is arrival-order — NOT convergent under true concurrency without this. E7-ruled: op ids hash-chain their causal predecessors from day one (benign mesh for MVP; blocklace/Byzantine upgrade path preserved without redesigning op identity).
- **BB-CRDT-5**: deliver ops over the shipped reliability sublayer: LinkSequencer (monotone), InboundOrdering (bounded reorder buffer, idempotent dedup), SendWindow (N=8 backpressure), FencingRegistry (stale writer → Fenced, single-winner). At-least-once + idempotent merge; no exactly-once machinery.
- **BB-CRDT-6**: region edits: LWW-per-region with saved-original always recoverable + transient/permanent classification (040 owner-ruled). Needs a concurrency tie-break (dot order) — currently arrival-order.
- **BB-CRDT-7** (ACCEPTED — 041-shipped): ordered/rich content: Fugue (maximal non-interleaving) + Peritext formatting spans over stable IDs, preserving unhandled marks (the CRDT-native face of BB-HDR-2). Shipped by 041 (`csharp/glp_crdtmsg/crdt/richtext/` — `Fugue.cs`, `Peritext.cs`, `RichTextDoc.cs`; 041 SC-012/SC-013, tag `v2026.07.04.4`).
- **BB-CRDT-8** (**E7**): IF the mesh is adversarial: hash-chained causal history in op ids from day one (cheap), blocklace as the target log model (equivocation exclusion; echoes the in-repo blocklace note). Owner threat-model declaration selects the branch.
- **BB-CRDT-9**: ground-terms-only law: only GROUND terms cross the wire (025 GRL discipline, 036 L5 contract); reply variables = local pairs + ground CorrIds. Moving to distributed variables is gated on D-B2 rulings + the two OPEN proofs (writer-MGU, dist-deref) — standing owner gates.
- **BB-CRDT-10** (PROV, POST): op-history storage/bulk transfer: Automerge-style columnar + RLE + LEB128 (~1.1 B/op).
- **BB-CRDT-11**: durable provenance for 100% of operations INCLUDING refusals: {peer, target, timestamps, SHA-256, outcome ∈ closed enum} keyed to authenticated identity (040 FR-037/SC-009 shape); signed provenance is the OC-2 composition point.

### RTE — routing
- **BB-RTE-1** (**FI**, **E6**): the Policy DSL (gap7 — thinnest gap, no prior art anywhere) starts as declarative DATA, not a language: fixed three-field policy {must-reach targets, ordered waypoints, exclude list} in the header, evaluated per hop SIENA-style; generalize post-MVP (predicate covering, Astrolabe aggregates, ≤3-route multipath). Owner rulings needed: MVP subset, unsatisfiable-policy semantics (fail/queue/degrade), and whether the DSL surfaces as GLP guards (→ language authority §1.14).
- **BB-RTE-2**: keep the running mesh kernel: route by `to`, broadcast fan-out excluding source, logged-never-silent DROP taxonomy (no-route, malformed-envelope, send-failed isolated per destination, over_capacity = clear reject); faults surface in the shipped lattice ok|closed|tempFail|permFail as ground terms on a monitor stream (never a fourth unification verdict) + 036 terminal tokens with distinct exit codes.
- **BB-RTE-3**: @name loud-fail law (040-ruled, born from the 037 silent-fallback defect): a directed address resolves against the authenticated peer set and delivers to that peer ONLY; unknown name = reported error, NEVER silent default-fallback. Language-level invariant for every addressing form.
- **BB-RTE-4** (PROV): route computation substrate: olamnit 016 distance-vector (split-horizon, poison-reverse, reconvergence) as the behaviour the policy filters — partial fit (no waypoint/exclude semantics natively).

### SCH — schema & registry
- **BB-SCH-1** (**E9**): MVP schema = ground GLP term with registered functor per message kind + ONE shared codec module per protocol (040 `tmsg` discipline) riding a registered payloadType. The FULL schema language is a genuine 3-way conflict: qmedit plaintext DSL (F1, built) vs CDDL RFC 8610/9682 (F2, one grammar for CBOR+JSON) vs staying functor-only — owner selection required before the registry freezes a notation.
- **BB-SCH-2**: unified cross-runtime message-type registry (D2-governed): fuse beacon's declarative registry (type → required/optional fields, extra tolerated, `_TYPE_CAP` type→capability map) + 040 tmsg additive-kinds; carry Confluent-style compat modes (backward/forward/full/transitive) per type; owns the payloadType byte space (single-sourced in 041 `csharp/glp_wire_registry` since v2026.07.04.4) and functor allocation. E9-ruled: hosts BOTH schema representations — qmedit plaintext DSL (human authoring/display) and CDDL (formal registered artifact), agentic translation both directions, both forms stored.
- **BB-SCH-3** (PROV, POST): schema-driven multi-runtime codegen (Kaitai-style) with EverParse-grade verified parse/serialize as the end state; MVP hand-writes codecs but pins the property tests now.

---

## 3. Constraint coverage matrix (no empty rows — gate holds)

| OC | Covering blocks (every §1 carrier; tier per §1) |
|---|---|
| OC-1 amulets+macaroons | BB-CAP-1/2/3/4, BB-HDR-1/4, BB-RTE-1/3, BB-CRDT-11, BB-WIRE-1, BB-SIG-2, BB-SCH-2 |
| OC-2 multi-signature whole+sub | BB-SIG-1/2/3/4, BB-ENC-4, BB-HDR-2, BB-VER-3/4, BB-CRDT-8 |
| OC-3 transparent formatting transport | BB-ENC-1/2/3/5/7/8, BB-WIRE-1/2, BB-HDR-2, BB-VER-1/2/5, BB-CRDT-3/7, BB-RTE-2/3, BB-SCH-1/2 |
| OC-4 CRDT-first | BB-CRDT-1..11, BB-VER-3/4/5/6, BB-HDR-3, BB-ENC-2, BB-SCH-1 |

## 4. Closure ledger (28/28 — every row covered; none out-of-scope)

| Row | Covered by |
|---|---|
| OC-1..OC-4 | see §3 |
| S1 multi-format | BB-ENC-1/2/5/7/9, BB-CRDT-3/10, BB-SCH-3 |
| S2 routing+cap header | BB-HDR-1/3/4, BB-CAP-1/2/3/4, BB-RTE-1/2/3/4, BB-WIRE-1 |
| S3 multi-signatures | BB-SIG-1/2/3/4, BB-ENC-4, BB-HDR-2, BB-CRDT-8 |
| S4 version tolerance | BB-VER-1/2/3/4/5, BB-ENC-3/6, BB-HDR-2, BB-CRDT-5/7, BB-RTE-2, BB-SCH-2 |
| S5 QUIC/HTTP-3 framing | BB-WIRE-2/3/4 |
| S6 CRDT substrate | BB-CRDT-1..10, BB-HDR-3 |
| S7 schema architecture | BB-SCH-1/2, BB-ENC-1 |
| S8 provenance | BB-CRDT-11, BB-SIG-4, BB-HDR-4 + this doc's provenance header |
| gap1 unified wire | BB-WIRE-1/5, BB-SCH-2 (**E2**) |
| gap2 ≥3-way interchange | BB-ENC-5 (+1/7/8), BB-SCH-3 (**E3**) |
| gap3 message tombstone | BB-VER-3, BB-CRDT-7 |
| gap4 repair segment | BB-VER-4 |
| gap5 multi-signature format | BB-SIG-1, BB-CRDT-8 (**E4**) |
| gap6 CRDT message-vs-store | BB-CRDT-1/2/3/4/6/9, BB-VER-5 (**E1**) |
| gap7 policy DSL | BB-RTE-1/2, BB-HDR-1 (**E6**) |
| gap8 payload discriminator | BB-WIRE-2, BB-ENC-3 |
| gap9 amulet live token | BB-CAP-2, BB-HDR-1 (**E5**) |
| C1..C7 | C1→ENC-1/2/4/5/9 SCH-1/2/3 · C2→WIRE-1..5 ENC-3 HDR-3 SCH-2 · C3→VER-1..6 ENC-3/6 HDR-2 RTE-2 · C4→CRDT-1..11 HDR-3 VER-6 · C5→CAP-1..4 HDR-1/4 SIG-2 · C6→SIG-1..4 ENC-4 · C7→RTE-1..4 HDR-1 |

## 5. Provisional register (what promotes each)

| Block | Promotes when |
|---|---|
| BB-ENC-7 CBOR surface | **RESOLVED (042): promoted** — E3-ruled ACC/MVP-CORE; 041 shipped the surface (`CborCodec.cs`, four-surface conformance matrix with goldens per BB-ENC-9). Report §7 RG-042-1 |
| BB-ENC-8 markdown | owner picks lossless vs render-only; post-MVP *(re-affirmed 042: no owner pick, nothing shipped — RG-042-2)* |
| BB-SIG-4 hop attestation | nesting-growth bound designed *(restated 042: the E4 base-format condition was met by the 2026-07-04 ruling; this is the remaining condition — RG-042-3)* |
| BB-VER-5 lenses | first restructuring migration need; Avro path insufficient *(re-affirmed 042: 041 shipped version handling, zero translation machinery — RG-042-4)* |
| BB-CRDT-7 Fugue/Peritext | **RESOLVED (042): trigger met, self-promoted PROV → ACC** — 041 shipped Fugue + Peritext + RichTextDoc (SC-012/013, `v2026.07.04.4`). Report §7 RG-042-5 |
| BB-CRDT-10 columnar history | history sync becomes bandwidth-relevant *(re-affirmed 042: no columnar/compaction machinery at HEAD; delta+Merkle is the current answer — RG-042-6)* |
| BB-RTE-4 distance-vector | mesh grows beyond static topology *(re-affirmed 042: mesh still the 036 shared-cert static-peer LAN family — RG-042-7)* |
| BB-SCH-3 codegen | >2 runtimes hand-maintaining parsers of the same wire formats *(clarified 042: count at HEAD = 2 — Dart + C#; the Python terminal JSON sub-envelope is a distinct format — RG-042-8)* |

## 6. Escalation register — RULED by Gabi 2026-07-04 (all nine)

- **E1 (gap6) RULED: option (b) on BOTH layers, with ground-term ops** (store side confirmed 2026-07-04 follow-up) — message-CRDT = **pure op-based JSON-CRDT** (Kleppmann/PO-Log over causal delivery) with **ops carried as ground terms** (BB-CRDT-9 law preserved; DVV dots supply the causal context). Store-CRDT = **delta-state CRDTs + Merkle-tree anti-entropy** (join-irreducible delta mutators; Merkle-CRDT/Merkle-Search-Tree reconciliation, no causal-broadcast assumption) over the 040 append-only-WAL + rebuildable-projections storage shape; the in-family roadmap_crdt engine is demoted to a concept reference (OR-Set/HLC machinery), not the store engine. Seam = **op_id = DVV dot (authenticated-peer-name, counter)** distinct from `msg_id`, and **store layer ships first**. Effects: BB-CRDT-1 → ACCEPTED(ruled); BB-CRDT-2 engine = delta+Merkle (B-18/B-17 anchors); BB-VER-3 tombstone = an **op** (observed-remove), not a state marker; `crdt_model` discriminator (BB-CRDT-3) remains, with op-based as the message-document model.
- **E2 (gap1) RULED: (a)** — 047-D2 field carrier as a payload-typed binary section (layered endianness law kept); reconciliation via **symmetric lenses** with round-trip law tests (no legacy envelope demoted); **router payload-opacity preserved** (bytes forwarded unchanged). BB-WIRE-1 → ACCEPTED(ruled), stays FI.
- **E3 (gap2) RULED: (a) + YAML + CBOR** — MVP surface set = **binary-term ⇄ JSON ⇄ YAML ⇄ CBOR** (four surfaces, conformance matrix from day one; markdown + further surfaces later). Binary nesting = **TLV-outer / 038-term-codec-inner**. Effects: BB-ENC-7 (CBOR) promotes PROV → ACCEPTED/MVP-CORE; BB-ENC-5's surface set fixed; BB-ENC-8 stays POST-MVP.
- **E4 (gap5/OC-2) RULED** — multi-signature base = **qmedit per-block-seal semantics expressed in COSE/JWS envelope structures + Biscuit-style append-only chain for sub-content**. Key management **approved**: **per-peer Ed25519 keys enrolled at mesh join, bound to the BB-HDR-4 name** (per-peer identity above the shared cert). Canonicalization: abstract-model canonical form realized as the deterministic binary term encoding (signatures survive lossless transcode). BB-ENC-4 + BB-SIG-1 → ACCEPTED(ruled).
- **E5 (gap9) RULED: (b)** — amulet keeps the Amoeba 4-field shape {Port, ObjNum, Rights, Check} with the **Check field widened to ≥128-bit**; literal 16-byte fidelity rejected on 2026 unguessability margins. Rights-bit semantics remain a principal design item inside BB-CAP-2's build (not blocking the wire slot).
- **E6 (gap7) RULED: (a) + experimental GLP guard REQUIRED** — MVP = fixed declarative 3-field policy {targets, waypoints, excludes}, per-hop matcher; unsatisfiable policy = **fail loud** (consistent with BB-RTE-3). **Owner-mandated: an EXPERIMENTAL GLP guard surface for policy evaluation is required** — approval-in-principle granted here; the concrete guard signature/semantics still goes propose-first under DISCIPLINE §1.14 before any implementation. BB-RTE-1 → ACCEPTED(ruled) with the guard as an explicit deliverable.
- **E7 RULED: YES** — benign mesh for MVP (shared-cert LAN family) **but hash-chained op ids from day one**; blocklace/Byzantine upgrade path preserved without redesigning op identity. Effect: hash-chained op-id construction joins the MVP core (folds into BB-CRDT-4/BB-HDR-3); full BB-CRDT-8 blocklace stays deferred until untrusted peers are admitted.
- **E8 RULED: YES** — the 036 wire-contract capability slot is **sanctioned as an additive envelope-version bump** (v2 additive-optional field, old readers skip per BB-VER-2), **per-message** capability granularity (per-link session tokens later as an optimization). BB-HDR-1 → ACCEPTED(ruled).
- **E9 (S7) RULED: rich experimental functor registry with dual-DSL round-trip** — design and implement a registry where schema input is **authored in qmedit plaintext DSL**, captured + stored, **agentically translated to tighter CDDL** which is the **formally registered artifact**, then translated **back to qmedit plaintext DSL for human readability**. The functor registry (BB-SCH-1 MVP part) is the core; qmedit DSL = human authoring/display surface, CDDL = formal registered output, agentic translation both directions, both forms stored. BB-SCH-1 → ACCEPTED(ruled, experimental); BB-SCH-2 hosts both representations. **Addendum (Gabi 2026-07-04): a HIGHER-LEVEL schema language in the XML-Schema style is DEFERRED for later but its capture is CRITICAL+MANDATORY — recorded as roadmap feature `crdtmsg-xsd-style-schema-language` (epic crdt-multiformat-messaging), layered above the qmedit-DSL ↔ CDDL registry.**
- **Standing gates carried, not re-opened**: D4 ISA freeze (parity finality), D5/FORK-1 cyclic terms, ED-6 float decode, D-B2/OPEN-proofs (distributed variables), §1.14 for the E6 experimental guard's concrete design.

**Interpretation resolution (curator)**: "E1 b" initially encoded as the message-substrate choice only; Gabi confirmed 2026-07-04 that the store side is ALSO option (b) — **delta-CRDT + Merkle for the store too** — now encoded above. Where a §1-table bin says ESC, this ruled register supersedes it; the supersession likewise covers every En-marked passage in §2/§5/§7 (042 pass: ruling outcomes propagated inline wherever a location would otherwise mislead — see the change log).

## 7. MVP cut (dependency-ordered feed to `crdtmsg-mvp`) — UNBLOCKED: all E-gates ruled 2026-07-04 (§6); remaining principal constants: E5 Rights-bit semantics, E6 concrete guard design (§1.14 propose-first)

1. **BB-WIRE-2** payloadType registry artifact (unify the duplicated constants; allocate 0x12+) — smallest unblocking step.
2. **BB-ENC-2 + BB-ENC-3 + BB-ENC-7** term codec reuse + skippable-section container + CBOR surface (E3 four-surface set: binary-term ⇄ JSON ⇄ YAML ⇄ CBOR, conformance matrix from day one).
3. **BB-ENC-6 + BB-VER-1/2** decode invariants + two-tier tolerance + version discipline.
4. **BB-WIRE-3/4** frame + QUIC/WS reuse (already shipped — conformance vectors extended).
5. **BB-HDR-1/3/4** unified header fields + dedup identity + identity law *(needs E8, E2)*.
6. **BB-CAP-1/3/4** macaroon + verify-before-act over SPKI-pin membership.
7. **BB-CRDT-9/5/3/4** ground-term law, delivery substrate, crdt_model discriminator, DVV *(E1-ruled substrates)*; **store skeleton first: BB-CRDT-2 + BB-CRDT-1** *(E1-ruled: store = delta-state CRDTs + Merkle anti-entropy over the 040 WAL shape; message = pure op-based JSON-CRDT; store layer ships first; seam op_id = DVV dot ≠ msg_id)*.
8. **BB-SIG-2/3** signature classes + Ed25519 baseline *(E4 unlocks BB-SIG-1 proper)*.
9. **BB-VER-3** tombstone *(after E1)*; **BB-CAP-2** amulet slot *(E5)*; **BB-RTE-1** policy fields + minimal matcher + experimental GLP guard surface *(E6-mandated deliverable; concrete design §1.14 propose-first)*.
10. **BB-SCH-2** registry fusion (beacon + tmsg, compat modes), hosting the E9 dual representation (qmedit-DSL ↔ CDDL agentic round-trip) around the **BB-SCH-1** functor-registry core (E9-ruled, experimental).
Everything else marked PROV in §1: provisional/post-MVP per §5.

## 8. Appendix — merge log summary & method audit

- **Claim volumes**: A=30, B=29, C=27; 0 discarded in sanitize (all in-manifest, schema-conformant).
- **Clustering**: scanner-local block numbering normalized by design slot; mapping recorded via source claim-ids in §1 (deterministic: same design slot ⇒ same canonical block).
- **Corroboration rule**: ≥2 distinct FAMILIES (F1/F2/REPO), per the false-consensus guard. Histogram recomputed from the shipped §1 table by the 042 verification pass — the delivered tally ("3-family: 14. 2-family: 17. Single-family survivors: 9" over an implied 40 blocks) matched no bucket of the actual 50-row table (report CF-F3-7). Actual: **3-family: 28. 2-family: 9. Single-family survivors: 13** — BB-ENC-7, BB-ENC-8, BB-WIRE-5, BB-HDR-4, BB-VER-5, BB-VER-6, BB-CRDT-6, BB-CRDT-7, BB-CRDT-8, BB-CRDT-9, BB-CRDT-10, BB-CRDT-11, BB-RTE-3. Three of these carry recorded counter-query corroboration (BB-ENC-7 via F1 qmedit CBOR-TLV + §14 recs; BB-CRDT-7 via F1 §14 Fugue rec; BB-WIRE-5 via F1 §3); BB-HDR-4/BB-RTE-3 were accepted on repo-head + owner-ruling authority. All 13 were re-adjudicated by the 042 pass — see verification-report-042.md §3. (BB-SCH-3, named a singleton in the delivered text, is in fact 2-family: B-04 + A-9.)
- **Conflicts resolved by authority order** (recorded, losers → alternatives_rejected): binary encoding CBOR-vs-term-codec → layered composition (repo head wins for term payloads; CBOR held provisional for generic payloads); emit-low/accept-range vs hard-reject → split by layer (envelope vs frame/codec); scanner count 3-vs-6 (planning) → 3 families.
- **Conflicts NOT resolved** → §6 escalation register (9 items). Curator made zero self-decisions on genuine conflicts.
- **Cycles**: 1 full cycle + targeted counter-queries; ledger closed 28/28 on cycle 1 → stop rule met, no cycle-3 re-scan.
- **Known drift observed by scanner C** (feasibility veto duty): mesh routes JSON-only (binary codecs unused by the router); payloadType constants duplicated (corrected by 041 `glp_wire_registry`); spec-vs-plan store naming (PGlite-DuckLake vs file-WAL, owner-flagged in 040 plan); 037 @name promise shipped unimplemented once (origin of BB-RTE-3). All four items dispositioned 2026-07-04 by the 042 pass — verification-report-042.md §5.
- **Full claim sets**: were held only in the F3 run-record session transcripts, which were NOT persisted — dispositioned **unrecoverable** by the 042 pass (report §8, EP-F3-012/-024). Lost: per-claim verbatim quotes and per-claim confidence fields. Survives in-doc: every claim id, family, and attribution (§1 Sources cells). Superseded as evidence-of-record by the 042 targeted re-execution: [evidence/f3-merge-rederivation.md](evidence/f3-merge-rederivation.md) (all 37 multi-family merge decisions re-derived from in-doc data, 37/37 COHERENT) + 13 blind re-scan records under [evidence/](evidence/) (report §3). Confidence impact: bounded — every merged decision and singleton was re-verified without the transcripts.

---

## Change log — 042 verification pass (2026-07-04)

> All amendments below were made by feature 042-crdtmsg-verify-harden; the finding ids
> resolve in docs/research/crdt-multiformat-messaging/verification-report-042.md.

| # | Section touched | Change | Why (finding id) | Baseline |
|---|---|---|---|---|
| 1 | (new terminal section) | Added this change-log section skeleton (contract rule 4) | SETUP-042-F3 | HEAD(6ff3a8c9) |
| 2 | §3 header + OC-1 row | Header "(core)" → "(every §1 carrier; tier per §1)"; OC-1 list completed (+WIRE-1, SIG-2, SCH-2) | LR-042-F3-1 | DELIVERY(6ecc975f) |
| 3 | §3 OC-2 row | List completed (+CRDT-8); SIG-4 hedge "(/4)" removed (carries OC-2 in §1) | LR-042-F3-1 | DELIVERY(6ecc975f) |
| 4 | §3 OC-3 row | Removed ENC-6 (§1 OC=INFRA — sole overclaim); completed (+ENC-2/7/8, VER-5, CRDT-3, RTE-2/3, SCH-1/2) | LR-042-F3-1 | DELIVERY(6ecc975f) |
| 5 | §3 OC-4 row | List completed (+ENC-2, VER-5/6, SCH-1) | LR-042-F3-1 | DELIVERY(6ecc975f) |
| 6 | §4 S1 row | −SCH-1 (no S1 in §1), +ENC-9, CRDT-3, CRDT-10, SCH-3 | LR-042-F3-2 | DELIVERY(6ecc975f) |
| 7 | §4 S2 row | +WIRE-1, HDR-3, CAP-4, RTE-2, RTE-4 | LR-042-F3-2 | DELIVERY(6ecc975f) |
| 8 | §4 S3 row | +HDR-2, CRDT-8 | LR-042-F3-2 | DELIVERY(6ecc975f) |
| 9 | §4 S4 row | +HDR-2, CRDT-5/7, RTE-2, SCH-2 | LR-042-F3-2 | DELIVERY(6ecc975f) |
| 10 | §4 S6 row | +HDR-3 | LR-042-F3-2 | DELIVERY(6ecc975f) |
| 11 | §4 S7 row | −SCH-3 (no S7 in §1; the S1/S7 pair was a likely SCH-1↔SCH-3 transposition) | LR-042-F3-2 | DELIVERY(6ecc975f) |
| 12 | §4 gap2 row | −ENC-2, ENC-9 (no gap2 in §1), +SCH-3 | LR-042-F3-2 | DELIVERY(6ecc975f) |
| 13 | §4 gap3 row | +CRDT-7 | LR-042-F3-2 | DELIVERY(6ecc975f) |
| 14 | §4 gap5 row | −SIG-2, SIG-3 (no gap5 in §1), +CRDT-8 | LR-042-F3-2 | DELIVERY(6ecc975f) |
| 15 | §4 gap6 row | +CRDT-9, VER-5 | LR-042-F3-2 | DELIVERY(6ecc975f) |
| 16 | §4 gap7 row | −RTE-4 (no gap7 in §1), +HDR-1 | LR-042-F3-2 | DELIVERY(6ecc975f) |
| 17 | §4 gap9 row | +HDR-1 | LR-042-F3-2 | DELIVERY(6ecc975f) |
| 18 | §4 C1..C7 line | All seven cluster lists completed to §1-derived carrier sets (−ENC-7 from C1) | LR-042-F3-3 | DELIVERY(6ecc975f) |
| 19 | §2 header | Dangling "appendix §16" reference → session-transcript run-records statement | CF-F3-2 | DELIVERY(6ecc975f) |
| 20 | §8 corroboration bullet | Histogram corrected (14/17/9 over 40 → 28/9/13 over 50); 13 actual singletons enumerated; BB-SCH-3 mis-listing noted | CF-F3-7 | DELIVERY(6ecc975f) |
| 21 | §2 BB-WIRE-2 | "constants currently duplicated in two assemblies" → 041 `glp_wire_registry` shipped, duplication resolved (SC-010) | DD-042-2 | HEAD(6ff3a8c9) |
| 22 | §2 BB-SCH-2 | "(today duplicated constants)" → single-sourced in 041 `glp_wire_registry` since v2026.07.04.4 | DD-042-2 | HEAD(6ff3a8c9) |
| 23 | §8 drift list | Item 2 annotated "(corrected by 041 glp_wire_registry)"; pointer to report §5 dispositions added | DD-042-2 | HEAD(6ff3a8c9) |
| 24 | §1 BB-ENC-7 row | Bin PROV → ACC (ruled E3); MVP OPT → CORE | RP-042-01 | HEAD(6ff3a8c9) |
| 25 | §2 BB-ENC-7 | "(PROV)" → "(ACCEPTED — E3-ruled, MVP-CORE)"; "Adopt when…" → adopted as fourth MVP surface | RP-042-02 | HEAD(6ff3a8c9) |
| 26 | §2 BB-HDR-4 | E4 propagated: per-peer Ed25519 keys enrolled at join, bound to the name; transport certs remain future | RP-042-05 | HEAD(6ff3a8c9) |
| 27 | §2 BB-HDR-3 | E7/E1 propagated: op id hash-chained; op_id = DVV dot (authenticated-peer-name, counter) ≠ msg_id | RP-042-08 | HEAD(6ff3a8c9) |
| 28 | §2 BB-CRDT-4 | E7 propagated: op ids hash-chain causal predecessors from day one | RP-042-07 | HEAD(6ff3a8c9) |
| 29 | §2 BB-SCH-2 | E9 propagated: hosts both representations (qmedit DSL ↔ CDDL, both stored) | RP-042-09 | HEAD(6ff3a8c9) |
| 30 | §7 item 2 | +BB-ENC-7 and the E3 four-surface set | RP-042-04 | HEAD(6ff3a8c9) |
| 31 | §7 item 7 | "(E1 selects substrates)" → "(E1-ruled substrates)"; store-first BB-CRDT-2+1 skeleton added with the ruled substrates/seam | RP-042-11 | HEAD(6ff3a8c9) |
| 32 | §7 item 9 | +experimental GLP guard surface (E6-mandated deliverable, §1.14 propose-first) | RP-042-10 | HEAD(6ff3a8c9) |
| 33 | §7 item 10 + terminal line | E9 dual representation around the SCH-1 core; terminal line scoped to "marked PROV in §1" | RP-042-12 | HEAD(6ff3a8c9) |
| 34 | §6 curator note | Supersession note extended to En-marked §2/§5/§7 passages | RP-042-15 | HEAD(6ff3a8c9) |
| 35 | §1 BB-CRDT-7 row | Bin PROV → ACC (041-shipped); MVP tier unchanged (OPT) | RG-042-5 | HEAD(6ff3a8c9) |
| 36 | §2 BB-CRDT-7 | "(PROV)" → "(ACCEPTED — 041-shipped)"; shipped richtext artifacts + SC refs appended | RG-042-5 | HEAD(6ff3a8c9) |
| 37 | §5 BB-ENC-7 row | Row resolved: promoted (E3-ruled + 041-shipped CBOR surface); trigger text replaced by resolution | RG-042-1 | HEAD(6ff3a8c9) |
| 38 | §5 BB-ENC-8 row | Re-affirmed annotation appended (no owner pick, nothing shipped) | RG-042-2 | HEAD(6ff3a8c9) |
| 39 | §5 BB-SIG-4 row | Trigger restated to the remaining condition (E4 base-format condition met 2026-07-04) | RG-042-3 | HEAD(6ff3a8c9) |
| 40 | §5 BB-VER-5 row | Re-affirmed annotation appended (no restructuring migration shipped or needed) | RG-042-4 | HEAD(6ff3a8c9) |
| 41 | §5 BB-CRDT-7 row | Row resolved: trigger met (041 rich-text shipped), self-promoted PROV → ACC | RG-042-5 | HEAD(6ff3a8c9) |
| 42 | §5 BB-CRDT-10 row | Re-affirmed annotation appended (bandwidth trigger unmet) | RG-042-6 | HEAD(6ff3a8c9) |
| 43 | §5 BB-RTE-4 row | Re-affirmed annotation appended (static topology stands) | RG-042-7 | HEAD(6ff3a8c9) |
| 44 | §5 BB-SCH-3 row | Trigger clarified: ">2 runtimes hand-maintaining parsers of the same wire formats"; HEAD count = 2 noted | RG-042-8 | HEAD(6ff3a8c9) |
| 45 | §2 header | Transcript clause → not-persisted/unrecoverable disposition + supersession by evidence/ re-execution records | EP-F3-012 (report §8) | HEAD(6ff3a8c9) |
| 46 | §8 full-claim-sets bullet | "evidence appendix of record, per-claim fields intact" → unrecoverable disposition: what was lost, what survives, supersession + confidence impact | EP-F3-024 (report §8) | HEAD(6ff3a8c9) |
| 47 | header block | Verification reference line added (SC-009) | SC-009 (report §12) | HEAD(6ff3a8c9) |
