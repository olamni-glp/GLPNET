# Phase 0 Research — crdtmsg-mvp

All nine design escalations are ruled (synthesis §6) and the three clarifications (spec §Clarifications) close the rest, so this phase carries **no open `NEEDS CLARIFICATION`**. It (a) restates the ruled decisions the plan depends on and (b) resolves the remaining *library/implementation* picks. Format: Decision / Rationale / Alternatives.

## R1 — Multi-surface codec strategy (FR-001/002/003; E3)
- **Decision**: one abstract message model; four surface codecs derive from it. Binary = TLV-outer / Section-15-term-codec-inner (reuse `glp_result_codec/TermCodec.cs`). JSON/YAML/CBOR are generic-object encodings of the same model. A pairwise round-trip conformance matrix (16 cells) authored from ONE truth runtime's goldens (038 discipline).
- **Rationale**: ASN.1-style "define once, encode N ways" is the ruled OC-3 approach (BB-ENC-1); reusing the shipped term codec keeps the binary surface byte-frozen (BB-ENC-2, endianness law).
- **Alternatives**: per-surface independent schemas — rejected (BB-ENC-1, drift risk); protobuf/flatbuffers as the model — rejected (not term-native, breaks ground-term law).

## R2 — CBOR surface (E3)
- **Decision**: `System.Formats.Cbor` (in-box .NET) in a deterministic/canonical write mode; unknown keys retained as opaque map entries.
- **Rationale**: in-box, no new dependency, supports deterministic encoding + tag passthrough (BB-ENC-7 family).
- **Alternatives**: PeterO.Cbor — richer but an extra dependency; not needed for MVP.

## R3 — YAML surface (E3)
- **Decision**: `YamlDotNet`, round-tripping through the abstract model (not free-form YAML), unknown fields preserved as model-level opaque sections.
- **Rationale**: only mature .NET YAML lib; lossless if we serialize the model, not arbitrary YAML.
- **Alternatives**: hand-rolled YAML — rejected (round-trip + anchors too costly for MVP).

## R4 — Ed25519 provider + COSE/JWS seals (FR-020/021/022; E4)
- **Decision**: **NSec** (libsodium) for Ed25519 sign/verify; seals expressed as COSE_Sign structures (hand-built CBOR maps via R2) with a Biscuit-style append-only chain over sub-content blocks. Per-peer key enrolled at mesh join, bound to the BB-HDR-4 name.
- **Rationale**: NSec is deterministic Ed25519 with a clean .NET API; COSE-over-CBOR reuses R2; Biscuit append-only chain makes removal/reorder detectable (E4). Canonical-for-signing bytes = the deterministic binary term encoding (signatures survive transcode).
- **Alternatives**: BouncyCastle — heavier, keep as fallback; raw JWS-detached — JSON-only, doesn't cover the binary surface canonicalization.

## R5 — Macaroon capability (FR-017; BB-CAP-1)
- **Decision**: implement a minimal HMAC-chained macaroon (fail-closed caveats) modeled on the internal mstack/beacon reference; verify-before-act at every routed action; refusal is a recorded provenance outcome.
- **Rationale**: five STRONG internal implementations already exist (synthesis §6 BB-CAP-1); MVP needs the fail-closed verify path, not third-party public-key verification (Biscuit is the named later escalation).
- **Alternatives**: external macaroon lib — rejected (Constitution V forbids external-API LM, and a local HMAC macaroon is trivial + auditable).

## R6 — Fugue sequence CRDT (FR-036; BB-CRDT-7)
- **Decision**: implement Fugue (maximal-non-interleaving list CRDT) over stable element IDs `(DVV dot, side)`; each insert op names its left/right origin; convergence by the Fugue tree ordering.
- **Rationale**: Fugue is the ruled choice (synthesis §5/§6 BB-CRDT-7) precisely because it avoids the interleaving anomalies of RGA/Logoot under concurrent typing — the acceptance bar (SC-012).
- **Alternatives**: RGA — simpler but interleaves (fails SC-012); Logoot/LSEQ — allocation drift. Rejected.

## R7 — Peritext formatting spans (FR-037; BB-CRDT-7)
- **Decision**: Peritext-style formatting marks anchored to stable sequence positions (before/after boundaries), stored as a separate mark-set CRDT; **unknown marks preserved verbatim** through convergence + transcode.
- **Rationale**: Peritext keeps formatting stable under concurrent edits and is the CRDT-native face of BB-HDR-2 / OC-3 (transparent formatting passthrough). Unknown-mark preservation is the SC-013 bar and the OC-3 link.
- **Alternatives**: inline formatting ops in the sequence — rejected (breaks span semantics under concurrency).

## R8 — Store: delta-CRDT + Merkle anti-entropy over the 040 op-WAL (FR-026; E1)
- **Decision**: append-only op-WAL in the 040 responder-store shape (temp → SHA-256 → atomic commit → journal); live state = rebuildable projection; replicas reconcile by exchanging **join-irreducible delta mutators** discovered via **Merkle-tree** comparison (no causal-broadcast assumption). **Store ships first** (E1).
- **Rationale**: reuses the shipped 040 zero-loss storage shape (SC-010 precedent); delta+Merkle is the ruled store engine (E1 option b), demoting roadmap_crdt to a concept reference.
- **Alternatives**: state-based full-state merge — bandwidth-heavy; causal-broadcast op-log — assumes reliable broadcast the mesh doesn't guarantee. Rejected per E1.

## R9 — Causality: DVV dots + hash-chained op ids (FR-024/025; E7)
- **Decision**: dotted version vectors; the dot `(authenticated-peer-name, counter)` is the stable op identity and the store↔message seam (`op_id`, distinct from `msg_id`). Every op carries a hash of its causal predecessor(s) from day one.
- **Rationale**: DVV is O(actors) production-lineage causality; hash-chaining preserves the blocklace/Byzantine upgrade path without redesigning identity (E7).
- **Alternatives**: plain version vectors — no stable per-op identity; Lamport-only — loses concurrency detection. Rejected.

## R10 — payloadType registry unification (FR-008; BB-WIRE-2)
- **Decision**: new `glp_wire_registry` assembly owns the single constant table (0x10 IL, 0x11 RESULT_ENVELOPE, 0x12+ messaging kinds) + functor allocation + compat modes; `glp_il_codec/PayloadHeader.cs` and `glp_result_codec/ResultEnvelope*.cs` reference it instead of each redefining constants.
- **Rationale**: the constants are currently duplicated across two assemblies (code-verified: `glp_il_codec` + `glp_result_codec`); one registry is the smallest unblocking step (§7 step 1) and SC-010.
- **Alternatives**: leave duplicated + assert-equal test — rejected (still two sources of truth).

## R11 — Dual-DSL functor registry (FR-032/033/034; E9) — Claude-only
- **Decision**: schema authored in qmedit plaintext DSL → **Claude-agentic** translation to CDDL (the formally registered artifact) → Claude-agentic back-translation to qmedit DSL for display; both forms stored in `glp_wire_registry`. Translation runs via the Agent-tool/MCP seam (Constitution V), never an external API.
- **Rationale**: E9 ruling; Constitution V mandates Claude-only LM. The functor registry (BB-SCH-1 MVP part) is the core; the DSLs are authoring/display + formal-artifact surfaces.
- **Alternatives**: external LLM translation — **forbidden** (Constitution V). Hand-written CDDL only — loses the human-authoring surface E9 requires.

## R12 — Experimental GLP policy guard (FR-013/014; E6) — propose-first
- **Decision**: MVP ships the fixed declarative 3-field policy `{targets, waypoints, excludes}` + a per-hop matcher (pure C#), unsatisfiable → fail loud. The **experimental GLP guard is designed + proposed** as a `programs/crdtmsg/` artifact (proposed typed signature + semantics); **no guard implementation until §1.14 owner approval** (Constitution IV-a).
- **Rationale**: E6 grants approval-in-principle only; §1.14 forbids landing a language extension without concrete-design approval. The data-field matcher delivers the routing behavior regardless.
- **Alternatives**: implement the guard now — **rejected** (violates IV-a/§1.14).

## R13 — Transport reuse (FR-016; BB-WIRE-4)
- **Decision**: `glp_quick_host` (MsQuic QUIC + RFC 6455 WS over one bidi stream, SPKI pin) behind a link-transport seam; single-host multi-client for the SC-009 demonstration.
- **Rationale**: transport of record is shipped (036); reuse, don't rebuild (IV-b). Two-host/Profile-C are host-blocked (out of scope).
- **Alternatives**: new transport — rejected.
