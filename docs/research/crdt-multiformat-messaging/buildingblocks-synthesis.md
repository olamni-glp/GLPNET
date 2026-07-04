# Building-Blocks Synthesis — CRDT Multi-Format Messaging Epic

**Feature**: `crdtmsg-buildingblocks-synthesis` (F3). **Date**: 2026-07-04.
**Provenance**: F1 `priorart-sibling-scan.md` + F2 `webresearch-corpus.md` (both in this directory) + live glpnet repo at head (branch `037-virtual-3270-term`, post-040-implement).
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
| BB-ENC-7 | CBOR generic-payload surface | PROV | OPT | OC-3 | S1 gap2 | B-05 (+F1 qmedit CBOR-TLV) |
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
| BB-CRDT-7 | Sequence/rich-text CRDT: Fugue + Peritext spans | PROV | OPT | OC-3 OC-4 | S6 S4 gap3 C4 | B-20 (+F1 §14 rec) |
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

## 2. Block catalog — decisions (condensed; full field detail in the three claim sets, appendix §16)

### ENC — encodings & interchange
- **BB-ENC-1**: Define every message type once in an encoding-neutral abstract model; each surface (binary/JSON/YAML/term/md) is an encoding rule against it (ASN.1 X.680 discipline; qmedit's 3-surface design is the built instance). Never per-surface independent schemas.
- **BB-ENC-2**: The binary encoding of GLP-term payloads is the shipped Section-15 codec: LEB128 varints, 8-byte LE int64, IEEE-754 doubles, varint+UTF-8 strings, tags 0x00–0x07 (0x07 = unbound VarRef/GlobalVarId). Reuse `csharp/glp_result_codec/TermCodec.cs` + Gleam/Dart parity harness. GATES: float tag (ED-6 AtomVM), int64 bignum edges, "final" status rides D4 freeze.
- **BB-ENC-3**: Envelope sections are length-prefixed skippable TLV records (NDN-TLV model; LEB128; the 038 `capturedLen` field is the in-repo precedent); type-number ranges encode criticality (ignorable vs must-understand). Skip-by-LENGTH is lawful; skip-by-TAG stays loud-fail.
- **BB-ENC-4** (**E4**): signing must operate over canonical bytes; WHICH canonical form (deterministic-binary per surface vs one abstract-model canonical form; surface-pinned vs re-derivable signatures across transcode) is an owner ruling — it jointly gates OC-2 × OC-3.
- **BB-ENC-5**: The ≥3-way interchange (gap2) is ONE artifact: abstract model + per-surface codecs + a mandatory pairwise lossless round-trip conformance matrix incl. unknown-field preservation. Method: 038 golden-corpus discipline (one truth runtime authors goldens; every runtime byte-identical; gated cases quarantined explicitly). WHICH surfaces are MVP is **E3**.
- **BB-ENC-6**: Every decoder consumes all bytes or throws — reject bad version/payloadType/tag, truncation, trailing bytes (`ResultEnvelopeCodec.Decode` invariant; RFC 9413 anti-tolerance guardrail: strict on knowns, tolerant only on declared-skippable unknowns).
- **BB-ENC-7** (PROV): CBOR (RFC 8949) as the generic (non-term) binary payload surface — self-describing, tags pass through, standardized deterministic profile. Counter-query: F1 corroborates the family (qmedit CBOR-TLV; F1 §14 recommends CBOR/MessagePack TLV to F2). Adopt when a generic-payload need materializes.
- **BB-ENC-8** (PROV, POST-MVP): markdown surface exists nowhere (F1); design later, decide lossless vs render-only projection.
- **BB-ENC-9**: acceptance bar per surface pair: golden byte-parity vectors now (038 method), parse∘serialize=id property tests, EverParse-grade verified parsing as the long-term bar (029 Lean proof is the in-repo precedent).

### WIRE — framing, transport, unified wire
- **BB-WIRE-1** (**FI**, **E2**): build the ONE canonical carrier reconciling beacon-JSON / olamnit-48B / glpnet-L5 (buildkit-047-D2 mandate). Head-state fact: the running mesh routes JSON L5 via `JsonDocument` and never touches the binary codecs — the unified wire migrates `Mesh.TryRoute` onto a binary payload-typed header while PRESERVING router payload-opacity (bytes forwarded unchanged is what keeps end-to-end signatures valid). Escalated: byte layout, lens-vs-projection reconciliation, router opacity rule.
- **BB-WIRE-2**: payload-type discriminator: adopt the shipped 2-byte payload header (0x01 version + payloadType); registry today 0x10=IL, 0x11=RESULT_ENVELOPE; allocate 0x12+ for messaging kinds; FrameKind CANNOT serve this role (HANDOFF-36, code-verified). Create ONE registry artifact (feeds BB-SCH-2) — the constants are currently duplicated in two assemblies.
- **BB-WIRE-3**: reuse 025 FrameCodec (version byte, 22-byte BE header, per-chunk CRC-32, Whole/Fragment, 64 MiB guard, forged-length rejection) + 047 reliability sublayer semantics (FIFO seq/dedup, credit-window backpressure) as the frame discipline; QUIC-stream/H3-typed-frame mapping per B-09.
- **BB-WIRE-4**: transport of record = shipped 036 QUIC/WS (MsQuic + RFC 6455 over one bidi stream, SPKI pinning, enumerated terminal faults) behind `ILinkTransport`; runtime-reach law: AtomVM/WASM nodes delegate QUIC to a native side-process (Profile A), full-BEAM uses quicer (Profile C) — contract-level interchangeable, never assume in-runtime QUIC.
- **BB-WIRE-5**: endianness is layered and FROZEN: BE frame header ⊃ LE term payload. Never harmonize — either direction breaks a shipped golden/parity suite.

### HDR — canonical header
- **BB-HDR-1**: unified header = L5 fields {msg_id, from, to, seq} + routing-policy fields (must-have targets, waypoints, exclude lists — the brief's exact set, wire slots at MVP even if the DSL comes later) + a capability slot (macaroon/amulet), opaque to the router. **E8**: extending the 036 wire contract needs owner sanction (040 declared it out of scope) + per-link vs per-message capability granularity.
- **BB-HDR-2**: transparent passthrough law: formatting + additional payloads travel as opaque head-tagged blocks carried VERBATIM (qmedit skip-by-length; proto3 unknown-field retention; the #2289 transcode-loss regression is the named test). Relays never re-encode; field-by-field copy APIs forbidden.
- **BB-HDR-3**: `msg_id` end-to-end + per-link `seq` FIFO dedup + a globally-unique op id above the transport (link seq is per-session); idempotent apply at the store boundary; the DVV dot (actor, counter) is the natural op identity (ties to BB-CRDT-4).
- **BB-HDR-4**: identity law (040-ruled): a peer's name IS its link-authenticated mesh identity; first-come owns the routable id; dup-id tracked-never-addressable; incumbent-keeps-route on removal. CRDT actor ids key to this identity. Known limit: shared-cert domain means any member can claim an UNUSED name — per-peer credentials are future work (with BB-CAP-4).

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
- **BB-CRDT-4**: causality = dotted version vectors (O(actors) metadata; production lineage); the dot is the stable identity that tombstones, repairs, and sub-signatures address. Head-state warning (C-21): current region-LWW is arrival-order — NOT convergent under true concurrency without this.
- **BB-CRDT-5**: deliver ops over the shipped reliability sublayer: LinkSequencer (monotone), InboundOrdering (bounded reorder buffer, idempotent dedup), SendWindow (N=8 backpressure), FencingRegistry (stale writer → Fenced, single-winner). At-least-once + idempotent merge; no exactly-once machinery.
- **BB-CRDT-6**: region edits: LWW-per-region with saved-original always recoverable + transient/permanent classification (040 owner-ruled). Needs a concurrency tie-break (dot order) — currently arrival-order.
- **BB-CRDT-7** (PROV): ordered/rich content: Fugue (maximal non-interleaving) + Peritext formatting spans over stable IDs, preserving unhandled marks (the CRDT-native face of BB-HDR-2).
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
- **BB-SCH-2**: unified cross-runtime message-type registry (D2-governed): fuse beacon's declarative registry (type → required/optional fields, extra tolerated, `_TYPE_CAP` type→capability map) + 040 tmsg additive-kinds; carry Confluent-style compat modes (backward/forward/full/transitive) per type; owns the payloadType byte space (today duplicated constants) and functor allocation.
- **BB-SCH-3** (PROV, POST): schema-driven multi-runtime codegen (Kaitai-style) with EverParse-grade verified parse/serialize as the end state; MVP hand-writes codecs but pins the property tests now.

---

## 3. Constraint coverage matrix (no empty rows — gate holds)

| OC | Covering blocks (core) |
|---|---|
| OC-1 amulets+macaroons | BB-CAP-1/2/3/4, BB-HDR-1/4, BB-RTE-1/3, BB-CRDT-11 |
| OC-2 multi-signature whole+sub | BB-SIG-1/2/3(/4), BB-ENC-4, BB-HDR-2, BB-VER-3/4 |
| OC-3 transparent formatting transport | BB-HDR-2, BB-ENC-1/3/5/6, BB-VER-1/2, BB-WIRE-1/2, BB-CRDT-7 |
| OC-4 CRDT-first | BB-CRDT-1..11, BB-VER-3/4, BB-HDR-3 |

## 4. Closure ledger (28/28 — every row covered; none out-of-scope)

| Row | Covered by |
|---|---|
| OC-1..OC-4 | see §3 |
| S1 multi-format | BB-ENC-1/2/5/7, BB-SCH-1 |
| S2 routing+cap header | BB-HDR-1/4, BB-CAP-1/2/3, BB-RTE-1/3 |
| S3 multi-signatures | BB-SIG-1/2/3/4, BB-ENC-4 |
| S4 version tolerance | BB-VER-1/2/3/4/5, BB-ENC-3/6 |
| S5 QUIC/HTTP-3 framing | BB-WIRE-2/3/4 |
| S6 CRDT substrate | BB-CRDT-1..10 |
| S7 schema architecture | BB-SCH-1/2/3, BB-ENC-1 |
| S8 provenance | BB-CRDT-11, BB-SIG-4, BB-HDR-4 + this doc's provenance header |
| gap1 unified wire | BB-WIRE-1/5, BB-SCH-2 (**E2**) |
| gap2 ≥3-way interchange | BB-ENC-5 (+1/2/7/8/9) (**E3**) |
| gap3 message tombstone | BB-VER-3 |
| gap4 repair segment | BB-VER-4 |
| gap5 multi-signature format | BB-SIG-1/2/3 (**E4**) |
| gap6 CRDT message-vs-store | BB-CRDT-1/2/3/4/6 (**E1**) |
| gap7 policy DSL | BB-RTE-1/2/4 (**E6**) |
| gap8 payload discriminator | BB-WIRE-2, BB-ENC-3 |
| gap9 amulet live token | BB-CAP-2 (**E5**) |
| C1..C7 | C1→ENC-1/5/7 SCH-1 · C2→WIRE-1..5 · C3→VER-1..5 · C4→CRDT-1..10 · C5→CAP-1..4 HDR-1 · C6→SIG-1..4 · C7→RTE-1..4 |

## 5. Provisional register (what promotes each)

| Block | Promotes when |
|---|---|
| BB-ENC-7 CBOR surface | a non-term generic payload consumer appears; then goldens per BB-ENC-9 |
| BB-ENC-8 markdown | owner picks lossless vs render-only; post-MVP |
| BB-SIG-4 hop attestation | E4 base format ruled; nesting-growth bound designed |
| BB-VER-5 lenses | first restructuring migration need; Avro path insufficient |
| BB-CRDT-7 Fugue/Peritext | first ordered/rich-content document type ships |
| BB-CRDT-10 columnar history | history sync becomes bandwidth-relevant |
| BB-RTE-4 distance-vector | mesh grows beyond static topology |
| BB-SCH-3 codegen | >2 runtimes hand-maintaining codecs |

## 6. Open decisions requiring Gabi ruling (escalation register — curator does NOT self-decide)

- **E1 (gap6, the central one)**: ratify the two-tier CRDT architecture: message-CRDT substrate (qmedit models vs pure op-based JSON-CRDT), store-CRDT substrate (roadmap_crdt engine vs delta-CRDT+Merkle), the seam (op_id = msg_id = DVV dot?), and which layer MVP ships first. Also fixes BB-VER-3 tombstone semantics (op vs state marker).
- **E2 (gap1)**: unified-wire design: byte layout (047-D2 field carrier vs olamnit fixed-binary header), reconciliation approach (symmetric lenses vs canonical-projection), router payload-opacity preservation rule.
- **E3 (gap2 scope)**: the MVP encoding-surface set (minimum on the table: binary-term ⇄ JSON; qmedit adds YAML; B recommends +CBOR; brief ambition: +md +Gleam-term) and the binary nesting rule (TLV-outer/038-inner vs flat tag space).
- **E4 (gap5/OC-2)**: canonicalization + signature validity across transcode (surface-pinned vs abstract-canonical) and the multi-signature base (qmedit seal design vs COSE/JWS + Biscuit-chain) + key management/signer identity model.
- **E5 (gap9)**: amulet constant: literal 16-byte Amoeba layout fidelity vs widened Check field; Rights-bit semantics. Principal-defined (interview); implementer must not reinterpret.
- **E6 (gap7)**: policy MVP subset; unsatisfiable-policy semantics (fail/queue/degrade); GLP-guard surface question (→ §1.14 language authority if yes).
- **E7**: threat-model declaration: adversarial mesh or not (selects BB-CRDT-8 branch; hash-chained op ids are cheap either way and recommended from day one).
- **E8**: sanction extending the 036 wire contract with the capability slot (040 declared transport/security-model changes out of scope) + capability granularity (per-link vs per-message).
- **E9 (S7)**: schema-language selection: qmedit plaintext DSL vs CDDL vs functor-registry-only-for-now.
- **Standing gates carried, not re-opened**: D4 ISA freeze (parity finality), D5/FORK-1 cyclic terms, ED-6 float decode, D-B2/OPEN-proofs (distributed variables), §1.14 language authority for any GLP-surfaced primitive.

## 7. MVP cut (dependency-ordered feed to `crdtmsg-mvp`)

1. **BB-WIRE-2** payloadType registry artifact (unify the duplicated constants; allocate 0x12+) — smallest unblocking step.
2. **BB-ENC-2 + BB-ENC-3** term codec reuse + skippable-section container (extends shipped code).
3. **BB-ENC-6 + BB-VER-1/2** decode invariants + two-tier tolerance + version discipline.
4. **BB-WIRE-3/4** frame + QUIC/WS reuse (already shipped — conformance vectors extended).
5. **BB-HDR-1/3/4** unified header fields + dedup identity + identity law *(needs E8, E2)*.
6. **BB-CAP-1/3/4** macaroon + verify-before-act over SPKI-pin membership.
7. **BB-CRDT-9/5/3/4** ground-term law, delivery substrate, crdt_model discriminator, DVV *(E1 selects substrates)*.
8. **BB-SIG-2/3** signature classes + Ed25519 baseline *(E4 unlocks BB-SIG-1 proper)*.
9. **BB-VER-3** tombstone *(after E1)*; **BB-CAP-2** amulet slot *(E5)*; **BB-RTE-1** policy fields + minimal matcher *(E6)*.
10. **BB-SCH-2** registry fusion (beacon + tmsg, compat modes).
Everything else: provisional/post-MVP per §5.

## 8. Appendix — merge log summary & method audit

- **Claim volumes**: A=30, B=29, C=27; 0 discarded in sanitize (all in-manifest, schema-conformant).
- **Clustering**: scanner-local block numbering normalized by design slot; mapping recorded via source claim-ids in §1 (deterministic: same design slot ⇒ same canonical block).
- **Corroboration rule**: ≥2 distinct FAMILIES (F1/F2/REPO), per the false-consensus guard. 3-family: 14 blocks. 2-family: 17. Single-family survivors: 9 (each counter-queried; e.g. BB-ENC-7 confirmed via F1 qmedit CBOR-TLV + §14 recs; BB-CRDT-7 via F1 §14 Fugue rec; BB-WIRE-5/BB-HDR-4/BB-RTE-3 accepted on repo-head + owner-ruling authority; BB-CRDT-10/BB-SCH-3 held PROVISIONAL).
- **Conflicts resolved by authority order** (recorded, losers → alternatives_rejected): binary encoding CBOR-vs-term-codec → layered composition (repo head wins for term payloads; CBOR held provisional for generic payloads); emit-low/accept-range vs hard-reject → split by layer (envelope vs frame/codec); scanner count 3-vs-6 (planning) → 3 families.
- **Conflicts NOT resolved** → §6 escalation register (9 items). Curator made zero self-decisions on genuine conflicts.
- **Cycles**: 1 full cycle + targeted counter-queries; ledger closed 28/28 on cycle 1 → stop rule met, no cycle-3 re-scan.
- **Known drift observed by scanner C** (feasibility veto duty): mesh routes JSON-only (binary codecs unused by the router); payloadType constants duplicated; spec-vs-plan store naming (PGlite-DuckLake vs file-WAL, owner-flagged in 040 plan); 037 @name promise shipped unimplemented once (origin of BB-RTE-3).
- **Full claim sets**: preserved in the F3 run records (session transcripts); the three scanners' complete 86-claim output is the evidence appendix of record, per-claim fields intact.
