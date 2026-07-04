# Feature Specification: CRDT Multi-Format Messaging MVP

**Feature Branch**: `041-crdtmsg-mvp`
**Created**: 2026-07-04
**Status**: Draft
**Input**: User description: "CRDT multi-format messaging MVP (feature slug: crdtmsg-mvp)"
**Authoritative source**: `docs/research/crdt-multiformat-messaging/buildingblocks-synthesis.md` — §7 (MVP cut, dependency-ordered) is the scope; §6 (Escalation register, all nine RULED by Gabi 2026-07-04) supplies the settled design decisions; §0 states the overriding constraints.

## Overriding Constraints (verbatim, synthesis §0)

- **OC-1** — capability layer: amulets + macaroon signatures.
- **OC-2** — multi-signature: whole-content **AND** sub-content.
- **OC-3** — transparent transport of formatting + additional formatting payloads.
- **OC-4** — CRDT-first: CRDT-capable services / stores / documents.

Every functional requirement traces to at least one OC and to the building blocks (BB-*) named in the synthesis. The nine escalations E1–E9 are ruled (§6); this spec encodes those rulings as settled constraints, not open questions.

## User Scenarios & Testing *(mandatory)*

The "users" of this feature are the **GLPNET runtime and its agents**. Each user story is an independently demonstrable capability slice of the single end-to-end goal: *one message, proven end-to-end — multi-format, multi-version, security-first, carrying a CRDT payload, over QUIC.* Stories are ordered by the dependency-ordered MVP cut (§7), honoring the E1 ruling that the **store layer ships first**.

### User Story 1 - Round-trip one message across all four encodings, losslessly (Priority: P1)

A runtime defines a message type **once** against an encoding-neutral abstract model, then serializes and deserializes it as **binary-term, JSON, YAML, and CBOR**. Any surface can be converted to any other surface without semantic loss — including fields the converting runtime does not understand, which travel verbatim. Every decoder either consumes all input bytes or fails loudly; nothing is silently tolerated.

**Why this priority**: The message-in-N-formats interchange is the foundation every later slice rides on. It is the smallest useful slice: a message that survives format conversion, with a conformance harness proving it, is a viable deliverable on its own (OC-3).

**Independent Test**: Author a golden corpus of the message. Run the pairwise round-trip conformance matrix across {binary-term, JSON, YAML, CBOR}: each surface re-encodes byte-identically to its own golden, and every cross-surface conversion preserves the abstract model including unknown/opaque sections. Feed malformed inputs (bad version byte, unknown must-understand tag, truncation, trailing bytes) and confirm each is rejected.

**Acceptance Scenarios**:

1. **Given** a message encoded on any of the four surfaces, **When** it is decoded to the abstract model and re-encoded on any other surface, **Then** the result is semantically identical and all opaque/unknown sections are preserved verbatim.
2. **Given** a message carrying a section the runtime does not understand but that is marked *ignorable* (skippable by length), **When** it is decoded, **Then** the section is skipped-by-length and carried through re-encoding unchanged.
3. **Given** a message carrying an unknown section marked *must-understand*, or a byte stream with a bad version / truncation / trailing bytes, **When** it is decoded, **Then** decoding fails loudly (no partial or silent acceptance).

---

### User Story 2 - Persist and converge the CRDT payload in the durable store (store ships first) (Priority: P2)

A runtime records the operations that make up a message's CRDT payload into an **append-only operation log (op-WAL)** and derives its live state from **rebuildable projections**. Two stores that have seen the same operations — in any order, possibly with gaps — reconcile to **identical state**. The store recovers its full state from the op-WAL after a crash with zero loss.

**Why this priority**: E1 rules the store layer ships first. Durable convergence is the backbone of CRDT-first (OC-4): without a converging, crash-recoverable store, message-level ops have nowhere sound to land.

**Independent Test**: Apply a randomized permutation of the same operation set to two independent stores and assert identical final projections (delta-state merge + Merkle-tree anti-entropy reconciliation, no causal-broadcast assumption). Kill and rebuild a store from its op-WAL at arbitrary points and assert zero-loss reconstruction.

**Acceptance Scenarios**:

1. **Given** two stores that have each observed the same set of operations in different orders, **When** they run anti-entropy reconciliation, **Then** both converge to identical state.
2. **Given** a store interrupted at an arbitrary point during operation, **When** it is restarted, **Then** it rebuilds its full projected state from the op-WAL with no lost or duplicated operations.
3. **Given** two stores with divergent operation sets, **When** they exchange delta-state mutators over Merkle-tree reconciliation, **Then** only the missing join-irreducible deltas are transferred and both converge.

---

### User Story 3 - Apply and converge message-level CRDT operations (Priority: P3)

A runtime carries a message's CRDT payload as **pure op-based JSON-CRDT operations**, each expressed as a **ground term**, each stamped with a **DVV dot** `(authenticated-peer-name, counter)` that is its stable identity, and each bearing a **hash-chained op id** from day one. Concurrent operations from different peers, delivered over the reliability substrate, converge. A **semantic tombstone** removes an element with observed-remove semantics — it never resurrects concurrent, unobserved additions.

**Why this priority**: This is the in-flight (message) face of CRDT-first (OC-4). It depends on US2's store seam (op_id = DVV dot, distinct from msg_id) and US1's encoding.

**Independent Test**: Deliver concurrent operations from two peers in adversarial orderings and assert convergence. Issue a tombstone for an element concurrently with an add of the same element and assert observed-remove semantics (the concurrent add survives). Verify each op id chains to its predecessor (tamper of history is detectable).

**Acceptance Scenarios**:

1. **Given** two peers issuing concurrent operations on the same document, **When** each peer applies the other's operations in any order, **Then** both reach identical document state.
2. **Given** an element with a concurrent add and remove, **When** both operations are applied, **Then** observed-remove semantics hold — the unobserved concurrent add is not tombstoned.
3. **Given** a duplicated operation (same DVV dot), **When** it is applied twice, **Then** the second application is idempotent (no double effect).
4. **Given** a payload declared with a `crdt_model` discriminator, **When** an ordinary (non-CRDT) request/response message is sent, **Then** it travels unimpeded (CRDT-capable, not CRDT-mandatory).

---

### User Story 4 - Enforce capabilities and multi-signatures on the message (Priority: P4)

Every routed action is **capability-gated** — a macaroon with fail-closed caveats (an unsatisfiable or un-understood caveat fails), plus a reserved **amulet** slot (Amoeba 4-field shape, Check field ≥128-bit) — and is **verified before acting**; a refusal is a distinct, recorded outcome, never a silent drop. Message content carries **whole-content AND sub-content signatures**: per-peer Ed25519 keys enrolled at mesh join, per-block seals expressed in COSE/JWS structures with a Biscuit-style append-only chain over sub-content. Any single-byte tamper of a signed block, or removal/reorder of signed sub-content, is detected. Signatures survive lossless transcode.

**Why this priority**: Delivers OC-1 (amulets + macaroons) and OC-2 (multi-signature whole + sub-content). It layers above US1's section identity (sub-content addressing) and the shared-cert membership layer.

**Independent Test**: Present actions with satisfying, unsatisfiable, and un-understood capabilities and assert allow / fail-closed / fail-closed with a recorded refusal in each case. Tamper one byte of a signed block, and separately remove and reorder signed sub-blocks, and assert 100% verification failure. Transcode a signed message across surfaces and assert signatures still verify.

**Acceptance Scenarios**:

1. **Given** a routed action whose macaroon caveats are all satisfied, **When** it is verified before acting, **Then** the action proceeds; **Given** an unsatisfiable or un-understood caveat, **Then** it fails closed and the refusal is recorded as a distinct outcome.
2. **Given** a message with whole-content and per-block sub-content signatures, **When** any signed byte is altered or any signed sub-block is removed or reordered, **Then** verification fails.
3. **Given** a validly signed message, **When** it is transcoded losslessly between surfaces, **Then** its signatures still verify (canonical form = the deterministic binary term encoding).
4. **Given** a peer that attenuates a capability, **When** it does so, **Then** content-history signatures remain valid (two signature classes never conflated: content Ed25519 ≠ capability HMAC).

---

### User Story 5 - Route the message over QUIC with unified header, policy fields, and version-skip tolerance (Priority: P5)

A runtime sends the message over the **shipped 036 QUIC/WS transport**. The message carries a **unified header** — `{msg_id, from, to, seq}` + routing-policy fields `{must-reach targets, ordered waypoints, exclude list}` + a **capability slot** — all **opaque to the router** (bytes forwarded unchanged, preserving end-to-end signatures). A directed **@name** address resolves against the authenticated peer set and delivers to that peer **only**; an unknown name is a reported error, never a silent default-fallback. The capability slot is an **additive envelope-version-2 field**: an older reader skips it and still processes known fields. Duplicates are suppressed via `msg_id` + per-link `seq`.

**Why this priority**: This is the end-to-end delivery slice that ties the others together over the transport of record. It depends on US1 (header encoding), US4 (capability slot), and the E8 ruling sanctioning the additive wire-contract bump.

**Independent Test**: Route a message @name to a known peer and assert single-peer delivery; @name to an unknown peer and assert a reported error with no fallback. Have an older (v1) reader receive a v2 envelope with the capability slot and assert it skips the slot and processes the rest. Replay a message with a duplicate `msg_id`/`seq` and assert dedup. Confirm the router forwards header bytes unchanged (signatures still verify at the destination).

**Acceptance Scenarios**:

1. **Given** a message addressed @name to an authenticated peer, **When** it is routed, **Then** it is delivered to that peer only; **Given** @name to an unknown peer, **Then** a reported error is raised and nothing is delivered by silent fallback.
2. **Given** a v1 (old) reader and a v2 envelope carrying the additive capability slot, **When** the reader decodes it, **Then** it skips the unknown additive field and processes all known fields.
3. **Given** a message routed through an intermediate relay, **When** it is forwarded, **Then** the relay forwards the opaque header/payload bytes verbatim and the destination's end-to-end signature verification still succeeds.
4. **Given** a message re-delivered with a `msg_id`/per-link-`seq` already seen, **When** it arrives, **Then** it is deduplicated (idempotent at the store boundary).

### Edge Cases

- **Unknown payloadType**: a message tagged with an unallocated payloadType is rejected loudly (BB-ENC-6), never guessed.
- **Cyclic term payload**: a payload whose active path contains a cycle raises a transport fault via `CycleGuard` (`CyclicTermException`), never a GLP Fail and never silent acceptance (BB-VER-6; D5/FORK-1 remains a standing owner gate).
- **Unsatisfiable routing policy**: a policy whose targets cannot all be reached fails loud, consistent with @name loud-fail (BB-RTE-1/3; E6).
- **Version byte on frame/codec vs envelope**: an unexpected version byte on the frame or term codec is hard-rejected; on the envelope, an additive-optional newer version is accepted with unknown fields skipped (BB-VER-2).
- **Concurrent tombstone and add of the same element**: observed-remove semantics — the unobserved concurrent add survives (BB-VER-3).
- **Amulet fidelity**: literal 16-byte Amoeba token is rejected; the Check field is widened to ≥128 bits on 2026 unguessability margins (E5).
- **Shared-cert name claim**: any mesh member can claim an *unused* name (shared-cert domain); a duplicate-of-an-in-use name is tracked-but-never-addressable, incumbent keeps the route (BB-HDR-4, 040-ruled). Per-peer credentials above the shared cert are provided for signing (E4) but per-peer *routing* identity beyond first-come is future work.

## Requirements *(mandatory)*

### Functional Requirements

**Encoding & interchange (OC-3)**
- **FR-001**: The system MUST define each message type once against an encoding-neutral abstract model and derive every surface encoding from it — never per-surface independent schemas (BB-ENC-1).
- **FR-002**: The system MUST support four MVP surfaces — binary-term, JSON, YAML, CBOR — with a mandatory pairwise lossless round-trip conformance matrix, including unknown-field preservation (BB-ENC-5; E3).
- **FR-003**: The binary encoding of GLP-term payloads MUST reuse the shipped Section-15 term codec (LEB128 varints, 8-byte LE int64, IEEE-754 doubles, varint+UTF-8 strings, tags 0x00–0x07), with TLV-outer / term-codec-inner nesting (BB-ENC-2/3; E3).
- **FR-004**: Envelope sections MUST be length-prefixed skippable TLV records whose type-number ranges encode criticality (ignorable vs must-understand); skip-by-length is lawful, skip-by-tag stays loud-fail (BB-ENC-3).
- **FR-005**: Every decoder MUST consume all input bytes or throw — rejecting bad version, unknown payloadType, unknown must-understand tag, truncation, and trailing bytes (BB-ENC-6). No silent tolerance except on declared-skippable unknowns.
- **FR-006**: The system MUST enforce a two-tier extension model (must-ignore default + explicit must-understand) with mandatory greasing to keep skip paths exercised (BB-VER-1).
- **FR-007**: The system MUST embed a schema-version id per message and apply emit-low/accept-range version discipline at the envelope, while hard-rejecting unexpected frame/codec version bytes (BB-VER-2).

**Wire, header & routing (OC-1, OC-3)**
- **FR-008**: The system MUST unify the currently-duplicated payloadType constants into **one** registry artifact and allocate messaging kinds at 0x12+ (existing 0x10=IL, 0x11=RESULT_ENVELOPE preserved) (BB-WIRE-2).
- **FR-009**: The message MUST carry a unified header of `{msg_id, from, to, seq}` + routing-policy fields `{targets, waypoints, excludes}` + a capability slot, all opaque to the router (BB-HDR-1).
- **FR-010**: The router MUST forward header and payload bytes verbatim (payload-opacity), so end-to-end signatures survive routing; relays MUST NOT re-encode (BB-HDR-2, BB-WIRE-1; E2).
- **FR-011**: The capability slot MUST be introduced as an additive envelope-version-2 field with per-message granularity; old readers MUST skip it per FR-006 (BB-HDR-1; E8).
- **FR-012**: A directed @name address MUST resolve against the authenticated peer set and deliver to that peer only; an unknown name MUST raise a reported error and MUST NOT silently default-fallback (BB-RTE-3; 040-ruled).
- **FR-013**: The system MUST provide a fixed declarative three-field routing policy `{must-reach targets, ordered waypoints, exclude list}` evaluated per hop; an unsatisfiable policy MUST fail loud (BB-RTE-1; E6).
- **FR-014**: The system MUST deliver an **experimental GLP guard surface** for policy evaluation as a named deliverable. Its concrete guard signature/semantics remain propose-first under DISCIPLINE §1.14 — approval-in-principle is granted (E6), the concrete language change is not.
- **FR-015**: Duplicates MUST be suppressed via `msg_id` (end-to-end) + per-link `seq` (FIFO), with idempotent apply at the store boundary (BB-HDR-3).
- **FR-016**: The transport of record MUST be the shipped 036 QUIC/WS link (SPKI-pinned mutual TLS, RFC 6455 over one bidi stream), behind a link-transport abstraction; AtomVM/WASM runtimes delegate QUIC to a native side-process (Profile A) (BB-WIRE-3/4).

**Capabilities & signatures (OC-1, OC-2)**
- **FR-017**: The system MUST gate every routed action with a macaroon capability whose caveats are fail-closed (unsatisfiable OR un-understood caveat → fail) and MUST verify before acting; a refusal MUST be a distinct, recorded outcome, never a silent drop (BB-CAP-1/3).
- **FR-018**: The system MUST reserve an amulet token slot of the Amoeba 4-field shape `{Port, ObjNum, Rights, Check}` with the Check field ≥128-bit; Rights-bit semantics are a build-time design item, not blocking the wire slot (BB-CAP-2; E5).
- **FR-019**: SPKI-pinned mutual TLS (shared self-signed cert) MUST be treated as layer-0 membership only (possession = membership), never as per-peer identity (BB-CAP-4).
- **FR-020**: The system MUST provide first-class whole-content AND sub-content signatures — per-block seals in COSE/JWS structures with a Biscuit-style append-only chain over sub-content — such that any tamper, removal, or reorder of signed sub-content is detected (BB-SIG-1; E4; OC-2).
- **FR-021**: The system MUST keep two signature classes distinct — content attestation (Ed25519) and capability (macaroon HMAC) — each with its own verify path; attenuating a capability MUST NOT invalidate content history (BB-SIG-2).
- **FR-022**: The system MUST enroll a per-peer Ed25519 key at mesh join, bound to the peer's authenticated name, and use the deterministic binary term encoding as the canonical-for-signing form so signatures survive lossless transcode (BB-SIG-3, BB-ENC-4; E4).

**CRDT convergence (OC-4)**
- **FR-023**: The message-CRDT MUST be a pure op-based JSON-CRDT over causal delivery, with operations carried as **ground terms** (ground-terms-only law preserved) (BB-CRDT-1/3/9; E1).
- **FR-024**: Causality MUST be tracked by dotted version vectors; the dot `(authenticated-peer-name, counter)` MUST be the stable operation identity that tombstones, repairs, and sub-signatures address (BB-CRDT-4).
- **FR-025**: Every operation MUST bear a hash-chained op id from day one (benign-mesh MVP, blocklace/Byzantine upgrade path preserved without redesigning op identity) (BB-CRDT-4/8; E7).
- **FR-026**: The store-CRDT MUST be delta-state CRDTs reconciled by Merkle-tree anti-entropy over an append-only op-WAL with rebuildable projections, ships first, and MUST rebuild with zero loss after interruption (BB-CRDT-2, BB-VER-6-store; E1).
- **FR-027**: The seam MUST be `op_id = DVV dot`, distinct from `msg_id`; the store layer ships before the message layer (E1).
- **FR-028**: The system MUST provide a `crdt_model` discriminator per document/message (op-based for the message document; state-based default when absent) so ordinary request/response messages travel unimpeded (BB-CRDT-3).
- **FR-029**: The system MUST deliver operations over the shipped reliability substrate (monotone link sequencer, bounded-reorder idempotent inbound ordering, credit-window backpressure, single-winner fencing) — at-least-once + idempotent merge, no exactly-once machinery (BB-CRDT-5).
- **FR-030**: A message-level semantic tombstone MUST be a first-class operation carrying removed-element identity (dots) + causal context + reason, with observed-remove semantics (never resurrects unobserved concurrent adds), unknowns preserved through transcode (BB-VER-3; E1).
- **FR-031**: Term payloads MUST be acyclic for MVP; an active-path cycle MUST raise a transport fault via `CycleGuard` (`CyclicTermException`), never a GLP Fail (BB-VER-6).

**Schema & registry (OC-3, OC-4)**
- **FR-032**: The MVP schema MUST be a ground GLP term with a registered functor per message kind, riding a registered payloadType, with one shared codec module per protocol (BB-SCH-1; E9).
- **FR-033**: The system MUST implement an experimental functor registry with dual-DSL round-trip: schema authored in qmedit plaintext DSL, agentically translated to CDDL (the formally registered artifact), and translated back to qmedit DSL for human readability; both forms stored (BB-SCH-2; E9).
- **FR-034**: The registry MUST carry compatibility modes (backward/forward/full/transitive) per message type and own the payloadType byte space and functor allocation (BB-SCH-2).

**Provenance (OC-1, OC-4)**
- **FR-035**: The system MUST record durable provenance for 100% of operations **including refusals** — `{peer, target, timestamps, SHA-256, outcome ∈ closed enum}` keyed to authenticated identity (BB-CRDT-11).

### Key Entities

- **Abstract message model** — the single encoding-neutral definition of a message type; source of all surface encodings.
- **Envelope / Section (TLV)** — the length-prefixed, skippable, criticality-ranged container units that make up a message.
- **Unified header** — `{msg_id, from, to, seq}` + routing policy `{targets, waypoints, excludes}` + capability slot; opaque to the router.
- **PayloadType registry entry** — the single-source discriminator (0x10 IL, 0x11 RESULT_ENVELOPE, 0x12+ messaging kinds) + functor allocation + compatibility mode.
- **Schema entry (dual-DSL)** — qmedit-DSL source form ↔ CDDL registered form, both stored.
- **Capability token (macaroon)** — HMAC-chained fail-closed caveats gating routed actions.
- **Amulet** — Amoeba 4-field static token `{Port, ObjNum, Rights, Check≥128b}`; slot reserved.
- **Signature seal** — per-block (sub-content) + whole-content signatures (Ed25519 in COSE/JWS + Biscuit chain).
- **CRDT operation** — a ground-term op-based JSON-CRDT operation bearing a DVV dot and a hash-chained op id.
- **DVV dot** — `(authenticated-peer-name, counter)`: stable operation identity and causal coordinate; the store↔message seam (op_id).
- **Op-WAL entry** — an append-only durable record from which store projections are rebuilt.
- **Semantic tombstone** — a first-class observed-remove operation.
- **Routing policy** — the fixed three-field `{targets, waypoints, excludes}` evaluated per hop.
- **Provenance record** — durable `{peer, target, timestamps, SHA-256, outcome}` for every operation including refusals.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of a golden message corpus round-trips losslessly across all four surfaces (binary-term ⇄ JSON ⇄ YAML ⇄ CBOR), byte-identical on same-surface re-encode and semantically identical across surfaces, with unknown/opaque sections preserved verbatim.
- **SC-002**: 0% silent acceptance of malformed input — every bad-version, unknown-must-understand-tag, unknown-payloadType, truncated, and trailing-byte case is rejected loudly.
- **SC-003**: Two replicas that observe the same operation set in any randomized order reach identical state in 100% of permutations tested.
- **SC-004**: The store rebuilds its full projected state from the op-WAL with zero lost or duplicated operations after simulated interruption at any point.
- **SC-005**: 100% of single-byte content tampers and 100% of signed-sub-block removals/reorders are detected (verification fails); no tampered message is accepted.
- **SC-006**: 0% of unauthorized routed actions succeed silently — every capability failure is refused and recorded as a distinct provenance outcome.
- **SC-007**: 0% silent default-fallback delivery — every @name to an unknown peer yields a reported error and no delivery.
- **SC-008**: An older (v1) reader accepts a newer (v2) envelope carrying the additive capability slot, skipping the unknown field while processing 100% of known fields.
- **SC-009**: The full slice is demonstrated at least once end-to-end: one message routed over QUIC between two runtime endpoints, its CRDT payload converging on both.
- **SC-010**: PayloadType constants exist in exactly one registry artifact (zero duplication across assemblies), with messaging kinds allocated at 0x12+.
- **SC-011**: Signatures verify after lossless transcode across all four surfaces in 100% of signed-corpus cases.

## Assumptions

- **Benign mesh (E7)**: the MVP targets the shared-cert LAN family (benign peers), but hash-chained op ids are present from day one so the blocklace/Byzantine upgrade path needs no op-identity redesign. Full Byzantine (BB-CRDT-8) is deferred.
- **Store-first, single-writer acceptable at MVP**: the store ships first (E1); multi-replica merge machinery is the delta-CRDT + Merkle engine; single-writer is acceptable for the initial slice.
- **Ground-terms-only across the wire**: only ground terms cross the wire; reply variables are local pairs + ground CorrIds; distributed variables are gated on standing owner rulings/open proofs and are out of scope (BB-CRDT-9).
- **Transport reuse**: QUIC/WS is the shipped 036 transport; no new transport is built. Profile C (quicer NIF) and two-host e2e (the `gavri` host) acceptance are host-blocked and out of MVP scope.
- **Experimental GLP guard is a deliverable but concrete design is propose-first**: FR-014's guard surface is required in principle (E6); its concrete signature/semantics go propose-first under DISCIPLINE §1.14 before any language change (§1.14 authority preserved).
- **Amulet Rights-bit semantics deferred**: the wire slot is reserved with Check ≥128-bit; Rights-bit meaning is a build-time design item (E5).
- **XSD-style higher-level schema language is out of scope**: its capture is critical+mandatory but deferred to the separate roadmap feature `crdtmsg-xsd-style-schema-language` (E9 addendum).
- **Standing owner gates carried, not re-opened**: D4 ISA freeze, D5/FORK-1 cyclic terms, ED-6 float decode, D-B2 / OPEN-proofs (distributed variables), and §1.14 for the E6 guard.

## Dependencies

- **Synthesis §6/§7** — `docs/research/crdt-multiformat-messaging/buildingblocks-synthesis.md` (ruled decisions + MVP cut).
- **036** — shipped QUIC/WS link (transport of record; envelope v2 additive slot rides its wire contract).
- **025** — FrameCodec (frame discipline) + reliability sublayer semantics.
- **038 / 029** — Section-15 term codec (binary payload) + golden-corpus conformance discipline.
- **040** — append-only-WAL store shape + rebuildable projections, identity law, @name loud-fail law.
