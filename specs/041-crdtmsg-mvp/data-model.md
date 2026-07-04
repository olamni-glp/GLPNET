# Phase 1 Data Model — crdtmsg-mvp

Entities derive from spec §Key Entities; fields/validation/transitions below. Modes: all wire values are **ground terms** (BB-CRDT-9). "opaque" = carried verbatim, never re-encoded.

## 1. Abstract message model
The single encoding-neutral definition. Every surface (binary-term/JSON/YAML/CBOR) is a codec against this.
- **Fields**: `schema_version` (int, emit-low/accept-range), `payload_type` (registry id, §5), `header` (§2), `sections` (ordered list of Section, §3), `crdt_model` (enum: `op_based` | `state_based`(default when absent) | none).
- **Validation**: decoder consumes all bytes or throws (FR-005); unknown `payload_type` → reject.

## 2. Unified header  (FR-009; router-opaque)
- **Fields**: `msg_id` (globally-unique, ground), `from` (authenticated peer-name), `to` (peer-name or @name), `seq` (per-link FIFO counter), `policy` (§6), `capability_slot` (§7, **v2 additive-optional**).
- **Validation**: `from`/`to` resolve against the authenticated peer set; unknown @name → reported error, never fallback (FR-012). Router forwards these bytes **verbatim** (FR-010).
- **Version**: `capability_slot` is envelope-v2 additive; a v1 reader skips it by length (FR-011/FR-006).

## 3. Section (TLV)  (FR-004; BB-ENC-3)
- **Fields**: `type_number` (LEB128; range encodes criticality: ignorable vs must-understand), `length` (LEB128), `value` (bytes; term-codec-inner for term payloads).
- **Transitions/rules**: unknown + ignorable → skip-by-length, carry verbatim; unknown + must-understand → loud-fail (FR-005). Greasing sections MUST be emitted to keep skip paths exercised (FR-006).

## 4. CRDT operation  (FR-023/024/025; op-based JSON-CRDT)
- **Fields**: `op_id` = **DVV dot** `(peer_name, counter)` (stable identity, = store seam, distinct from `msg_id`), `deps` (causal predecessors, dotted-version-vector context), `pred_hash` (hash of predecessor(s), day-one hash-chain), `op` (ground-term operation body: map-set / seq-insert / seq-delete / mark-add / mark-remove / **tombstone**), `crdt_model` tag.
- **Validation**: idempotent on duplicate `op_id` (FR-015/FR-029); acyclic payload (FR-031, CycleGuard).
- **Tombstone** (§4a): first-class op carrying `{removed_id (dot), causal_context, reason}`; observed-remove — never resurrects unobserved concurrent adds (FR-030).

## 5. Wire registry entry  (FR-008/032/034; BB-WIRE-2/SCH-2)
- **Fields**: `payload_type` (byte: 0x10 IL, 0x11 RESULT_ENVELOPE, 0x12+ messaging kinds), `functor` (registered term functor per message kind), `compat_mode` (backward | forward | full | transitive), `qmedit_dsl` (authoring form), `cddl` (formally registered form). Both DSL forms stored; CDDL is the registered artifact (E9, Claude-agentic translation).
- **Validation**: single source — no constant duplicated across assemblies (SC-010).

## 6. Routing policy  (FR-013; BB-RTE-1)
- **Fields**: `targets` (must-reach list), `waypoints` (ordered), `excludes` (list). Evaluated per hop by a fixed matcher.
- **Rules**: unsatisfiable policy → **fail loud** (consistent with @name loud-fail). The experimental GLP guard (proposed, §8) is an *alternative evaluator*, not shipped until §1.14 approval.

## 7. Capability token + Amulet  (FR-017/018/019; BB-CAP)
- **Macaroon**: `{location, identifier, caveats[], hmac_chain}`; fail-closed (unsatisfiable OR un-understood caveat → fail); verified before acting (FR-017).
- **Amulet** (slot reserved): `{Port (48b), ObjNum (24b), Rights (8b), Check (≥128b)}` — Amoeba 4-field shape, Check widened per E5; Rights-bit semantics deferred (build-time).
- **Membership**: SPKI-pin shared cert = layer-0 membership only, not per-peer identity (FR-019).

## 8. Signature seal  (FR-020/021/022; E4)
- **Fields**: `whole_content_sig` (Ed25519 over the deterministic binary term encoding), `sub_content_seals[]` (per-block COSE_Sign seals in a Biscuit-style append-only chain), `signer` (per-peer key bound to peer-name, enrolled at mesh join).
- **Rules**: any tamper / removal / reorder of signed sub-content → verification fails (SC-005); two signature classes distinct — content Ed25519 ≠ capability macaroon-HMAC (FR-021); signatures survive lossless transcode (SC-011).

## 9. Store: op-WAL + projections + Merkle  (FR-026; E1)
- **op-WAL entry**: `{seqno, op (§4), sha256, committed_at}` — append-only, temp→verify→atomic-commit→journal (040 shape).
- **Projection**: rebuildable live state; crash → replay WAL → zero loss (SC-004).
- **Merkle tree**: over committed ops for anti-entropy; reconciliation exchanges only join-irreducible deltas (FR-026, R8).

## 10. Provenance record  (FR-035; BB-CRDT-11)
- **Fields**: `{peer, target, timestamps, sha256, outcome ∈ {applied, refused, dropped-noroute, malformed, over-capacity, ...closed enum}}` keyed to authenticated identity. Refusals are recorded, never silent (SC-006).

## 11. Rich-text CRDT document  (FR-036/037; MANDATORY demonstrator)
- **Sequence (Fugue)**: nodes `{elem_id=(dot,side), origin_left, origin_right, value}`; ordering by Fugue tree → maximal non-interleaving (SC-012).
- **Formatting (Peritext)**: marks `{mark_id, type, start_anchor, end_anchor, value}` over stable positions; **unknown mark types preserved verbatim** through convergence + transcode (SC-013, OC-3).
- **Relationship**: both are op-based JSON-CRDT documents (§4 ops) over the store (§9); the end-to-end demonstrator (SC-009) carries a `seq-insert` + `mark-add` op.
