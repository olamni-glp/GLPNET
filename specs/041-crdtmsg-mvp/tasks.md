---
description: "Task list for crdtmsg-mvp implementation"
---

# Tasks: CRDT Multi-Format Messaging MVP

**Input**: Design documents from `specs/041-crdtmsg-mvp/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/
**Tests**: INCLUDED — the spec's success criteria (SC-001..SC-013) and the project test protocol (CLAUDE.md) require them; each SC maps to a suite (quickstart.md).

**Organization**: by user story (US1–US5), in the spec priority order = the §7 dependency order. Within the CRDT concern the **store (US2) ships before the message-CRDT (US3)** per E1.

## Format: `[ID] [P?] [Story] Description with file path`
- **[P]** = parallelizable (different files, no incomplete-task dependency).
- **[Story]** = US1..US5 (user-story phases only).

## Path Conventions
C# workspace under `csharp/`; GLP proposal under `programs/crdtmsg/`; parity vectors under `test/parity/`.

---

## Phase 1: Setup (Shared Infrastructure)

- [ ] T001 Create new C# projects `csharp/glp_wire_registry/` + `csharp/glp_crdtmsg/` (internal dirs: model, envelope, header, cap, sig, crdt, crdt/richtext, store, route, schema) and add to the solution
- [ ] T002 Add package references to `csharp/glp_crdtmsg/GlpCrdtMsg.csproj`: `System.Text.Json`, `YamlDotNet`, `System.Formats.Cbor`, `NSec.Cryptography`; project refs to `glp_result_codec`, `glp_link`, `glp_quick_host`
- [ ] T003 [P] Create xUnit projects `csharp/glp_wire_registry.tests/` + `csharp/glp_crdtmsg.tests/` with a `goldens/` fixture dir (reuse `glp_result_codec` golden discipline)
- [ ] T004 [P] Create `test/parity/` for Gleam/Dart codec parity vectors sharing the same goldens

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: no user story may begin until this phase completes.

- [ ] T005 Implement the single payloadType/functor registry table in `csharp/glp_wire_registry/WireRegistry.cs` (0x10 IL, 0x11 RESULT_ENVELOPE, 0x12+ messaging kinds; functor allocation; compat modes backward|forward|full|transitive)
- [ ] T006 Repoint `csharp/glp_il_codec/PayloadHeader.cs` and `csharp/glp_result_codec/ResultEnvelope.cs`/`ResultEnvelopeCodec.cs` to reference `glp_wire_registry` — remove the duplicated constants (SC-010)
- [ ] T007 Define the abstract message model (`Message`, `Header`, `Section`, `CrdtModel` enum) in `csharp/glp_crdtmsg/model/AbstractModel.cs`
- [ ] T008 Wire reuse seams behind `ILinkTransport` in `csharp/glp_crdtmsg/route/LinkTransport.cs` — `glp_result_codec.TermCodec` (+ CycleGuard), `glp_link` FrameCodec, `glp_quick_host` QUIC/WS/SPKI
- [ ] T009 Implement DVV-dot + hash-chain primitives (`op_id = (peer_name, counter)`, `pred_hash`) in `csharp/glp_crdtmsg/crdt/Dot.cs` (foundational — store, crdt, sig all consume it)

**Checkpoint**: registry unified, model + transport seam + op-identity ready.

---

## Phase 3: User Story 1 - Multi-format round-trip (Priority: P1) 🎯 MVP

**Goal**: one message defined once, losslessly round-tripped across binary-term/JSON/YAML/CBOR, with loud-fail decode.
**Independent Test**: 16-cell conformance matrix passes (incl. unknown-field preservation); malformed inputs all reject.

### Tests (write first, must fail)
- [ ] T010 [P] [US1] Conformance-matrix test (16 surface pairs, golden corpus, unknown-field preservation) in `csharp/glp_crdtmsg.tests/ConformanceMatrixTests.cs` (SC-001)
- [ ] T011 [P] [US1] Loud-fail fuzz test (bad version, unknown must-understand tag, truncation, trailing bytes) in `csharp/glp_crdtmsg.tests/LoudFailTests.cs` (SC-002)
- [ ] T012 [P] [US1] Registry single-source test (zero duplicated constants across assemblies) in `csharp/glp_wire_registry.tests/SingleSourceTests.cs` (SC-010)

### Implementation
- [ ] T013 [P] [US1] TLV section codec (LEB128, criticality ranges, skip-by-length, mandatory greasing) in `csharp/glp_crdtmsg/envelope/TlvSection.cs`
- [ ] T014 [US1] Binary-term surface (TLV-outer / `TermCodec`-inner; CycleGuard fault on cyclic payload) in `csharp/glp_crdtmsg/model/BinaryTermCodec.cs`
- [ ] T015 [P] [US1] JSON surface codec in `csharp/glp_crdtmsg/model/JsonCodec.cs`
- [ ] T016 [P] [US1] YAML surface codec (YamlDotNet, model-level round-trip) in `csharp/glp_crdtmsg/model/YamlCodec.cs`
- [ ] T017 [P] [US1] CBOR surface codec (deterministic; unknown keys retained) in `csharp/glp_crdtmsg/model/CborCodec.cs`
- [ ] T018 [US1] Loud-fail decode invariant (consume-all-or-throw) enforced across all surfaces in `csharp/glp_crdtmsg/envelope/DecodeGuard.cs`
- [ ] T019 [US1] Two-tier version tolerance (envelope emit-low/accept-range; frame+codec hard-reject) in `csharp/glp_crdtmsg/envelope/VersionPolicy.cs`

**Checkpoint**: US1 MVP — a message round-trips across all four surfaces, malformed input rejects.

---

## Phase 4: User Story 2 - Store-CRDT, ships first (Priority: P2)

**Goal**: durable append-only op-WAL + rebuildable projections; two stores converge; zero-loss rebuild.
**Independent Test**: randomized-order convergence + crash-rebuild pass.

### Tests (write first, must fail)
- [ ] T020 [P] [US2] Convergence test (two stores, randomized op order → identical state) in `csharp/glp_crdtmsg.tests/StoreConvergenceTests.cs` (SC-003)
- [ ] T021 [P] [US2] Crash-rebuild zero-loss test (interrupt at arbitrary point → WAL replay) in `csharp/glp_crdtmsg.tests/StoreRebuildTests.cs` (SC-004)

### Implementation
- [ ] T022 [US2] Append-only op-WAL (temp → SHA-256 verify → atomic commit → journal, 040 shape) in `csharp/glp_crdtmsg/store/OpWal.cs`
- [ ] T023 [US2] Rebuildable projection + replay in `csharp/glp_crdtmsg/store/Projection.cs`
- [ ] T024 [US2] Delta-state CRDT mutators + Merkle-tree anti-entropy reconciliation in `csharp/glp_crdtmsg/store/DeltaMerkle.cs`
- [ ] T025 [US2] Seam wiring `op_id` (DVV dot) as store key, distinct from `msg_id` in `csharp/glp_crdtmsg/store/OpWal.cs`

**Checkpoint**: store converges and rebuilds zero-loss — the CRDT backbone is in place.

---

## Phase 5: User Story 3 - Message-CRDT + MANDATORY rich-text (Priority: P3)

**Goal**: op-based JSON-CRDT ops (ground-term, DVV dot, hash-chained) that **generate/derive Fugue+Peritext rich-text**; observed-remove tombstone.
**Independent Test**: op idempotence + observed-remove; Fugue no-interleaving; Peritext unknown-mark preservation.

### Tests (write first, must fail)
- [ ] T026 [P] [US3] Op idempotence + observed-remove tombstone tests in `csharp/glp_crdtmsg.tests/OpSemanticsTests.cs` (FR-015/030)
- [ ] T027 [P] [US3] Fugue no-interleaving convergence test (concurrent typing, randomized delivery) in `csharp/glp_crdtmsg.tests/FugueTests.cs` (SC-012)
- [ ] T028 [P] [US3] Peritext unknown-mark preservation (through convergence + 4-surface transcode) in `csharp/glp_crdtmsg.tests/PeritextTests.cs` (SC-013)

### Implementation
- [ ] T029 [US3] Op-based JSON-CRDT op model (ground-term ops, deps, pred_hash; ground-terms-only law) in `csharp/glp_crdtmsg/crdt/Op.cs`
- [ ] T030 [US3] Semantic tombstone op (observed-remove) in `csharp/glp_crdtmsg/crdt/Tombstone.cs`
- [ ] T031 [US3] Delivery over the shipped reliability substrate (monotone seq, bounded-reorder idempotent inbound, N=8 window, single-winner fencing) in `csharp/glp_crdtmsg/crdt/Delivery.cs`
- [ ] T032 [US3] Fugue sequence CRDT (stable `elem_id=(dot,side)`, left/right origin, maximal non-interleaving) in `csharp/glp_crdtmsg/crdt/richtext/Fugue.cs`
- [ ] T033 [US3] Peritext formatting spans (stable anchors, unknown-mark verbatim passthrough) in `csharp/glp_crdtmsg/crdt/richtext/Peritext.cs`
- [ ] T034 [US3] `crdt_model` discriminator (op_based / state_based; non-CRDT request/response unimpeded) in `csharp/glp_crdtmsg/model/AbstractModel.cs`

**Checkpoint**: rich-text CRDT converges without interleaving and preserves unknown marks — the mandatory bar.

---

## Phase 6: User Story 4 - Capabilities + multi-signature (Priority: P4)

**Goal**: macaroon verify-before-act (fail-closed) + amulet slot; whole + sub-content Ed25519 signatures surviving transcode.
**Independent Test**: capability allow/fail-closed + refusal recorded; tamper/remove/reorder detected; transcode-survive.

### Tests (write first, must fail)
- [ ] T035 [P] [US4] Capability tests (satisfy / unsatisfiable / un-understood; refusal recorded as provenance) in `csharp/glp_crdtmsg.tests/CapabilityTests.cs` (SC-006)
- [ ] T036 [P] [US4] Signature tamper tests (byte flip, sub-block remove/reorder, transcode-survive) in `csharp/glp_crdtmsg.tests/SignatureTests.cs` (SC-005/011)

### Implementation
- [ ] T037 [US4] Macaroon (HMAC caveat chain, fail-closed) + verify-before-act in `csharp/glp_crdtmsg/cap/Macaroon.cs`
- [ ] T038 [P] [US4] Amulet slot (Amoeba 4-field {Port,ObjNum,Rights,Check≥128b}) reserved in `csharp/glp_crdtmsg/cap/Amulet.cs`
- [ ] T039 [US4] Ed25519 provider (NSec) + per-peer key enrol at mesh join, bound to peer-name in `csharp/glp_crdtmsg/sig/PeerKeys.cs`
- [ ] T040 [US4] Whole + sub-content COSE/JWS seals + Biscuit-style append-only chain (canonical = deterministic binary term encoding) in `csharp/glp_crdtmsg/sig/Seals.cs`
- [ ] T041 [US4] Enforce two distinct signature classes (content Ed25519 ≠ capability HMAC) in `csharp/glp_crdtmsg/sig/Seals.cs`
- [ ] T042 [US4] Durable provenance records incl. refusals ({peer,target,timestamps,sha256,outcome∈enum}) in `csharp/glp_crdtmsg/cap/Provenance.cs`

**Checkpoint**: message is capability-gated and multi-signed; tampering is detected.

---

## Phase 7: User Story 5 - Routing over QUIC + header + version-skip (Priority: P5)

**Goal**: router-opaque unified header + fixed policy, @name loud-fail, dedup, additive v2 slot; full slice over QUIC.
**Independent Test**: @name loud-fail; v1-reader skips v2 slot; router opacity + dedup; e2e rich-text op converges over QUIC.

### Tests (write first, must fail)
- [ ] T043 [P] [US5] @name loud-fail test (unknown name → error, no fallback) in `csharp/glp_crdtmsg.tests/AddressingTests.cs` (SC-007)
- [ ] T044 [P] [US5] v1-reader / v2-envelope additive-slot skip test in `csharp/glp_crdtmsg.tests/VersionSkipTests.cs` (SC-008)
- [ ] T045 [P] [US5] Router payload-opacity + dedup test (bytes verbatim; msg_id + per-link seq) in `csharp/glp_crdtmsg.tests/RouterTests.cs`
- [ ] T046 [P] [US5] End-to-end demonstrator test (rich-text op over QUIC, single-host two clients, both converge) in `csharp/glp_crdtmsg.tests/EndToEndTests.cs` (SC-009)

### Implementation
- [ ] T047 [US5] Unified header {msg_id,from,to,seq,policy,capability_slot}, router-opaque, in `csharp/glp_crdtmsg/header/UnifiedHeader.cs`
- [ ] T048 [US5] v2 additive capability slot + old-reader skip-by-length in `csharp/glp_crdtmsg/header/CapabilitySlot.cs`
- [ ] T049 [US5] @name resolution against authenticated peer set + loud-fail addressing in `csharp/glp_crdtmsg/route/Addressing.cs`
- [ ] T050 [US5] Dedup (msg_id + per-link seq) idempotent at store boundary in `csharp/glp_crdtmsg/route/Dedup.cs`
- [ ] T051 [US5] Fixed policy matcher {targets,waypoints,excludes} + fail-loud + logged DROP taxonomy in `csharp/glp_crdtmsg/route/PolicyMatcher.cs`
- [ ] T052 [US5] Deliver over `glp_quick_host` behind ILinkTransport; single-host two-client demonstrator in `csharp/glp_crdtmsg/route/Mesh.cs`

**Checkpoint**: the full slice runs end-to-end over QUIC.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T053 [P] **Experimental GLP policy-guard PROPOSAL ONLY** — `programs/crdtmsg/policy-guard-proposal.glp` (proposed typed signature + three-valued semantics + worked example) + design note. 🔴 GATE: no guard implementation/compile/run until Gabi approves under DISCIPLINE §1.14 (Constitution IV-a). Fixed matcher (T051) is the shipped fallback.
- [ ] T054 [P] Dual-DSL functor registry (qmedit-DSL ↔ CDDL, **Claude-agentic** translation via Agent-tool/MCP — Constitution V; both forms stored) in `csharp/glp_wire_registry/SchemaRegistry.cs`
- [ ] T055 [P] Validate Gleam/Dart codec parity vectors against the goldens in `test/parity/`
- [ ] T056 Run `quickstart.md` full validation + the project baseline test protocol (`bash test/run_all_tests.sh` green before/after)
- [ ] T057 [P] Docs: update `docs/` + `docs/known-issues.md` if any error surfaces; record any escalations

---

## Dependencies & Execution Order

### Phase Dependencies
- Setup (P1) → Foundational (P2, blocks all stories) → US1..US5 → Polish.
- **CRDT ordering (E1)**: US2 (store) ships before US3 (message-CRDT) — US3 depends on the store seam (T022–T025).
- US4 (sig) depends on US1 section identity (sub-content addressing, T013) + op model (T029).
- US5 depends on US1 header encoding (T007/T013), US4 capability slot (T037/T048), and the transport seam (T008).

### Within Each Story
- Tests written and FAILING before implementation. Models → services → integration. Baseline-green before each change; commit after each task/logical group.

### Parallel Opportunities
- Setup T003/T004 in parallel. Surface codecs T015/T016/T017 in parallel (different files). All per-story test tasks marked [P] in parallel. Polish T053/T054/T055/T057 in parallel.

---

## Parallel Example: User Story 1
```
# Tests first (parallel):
T010 ConformanceMatrixTests.cs ; T011 LoudFailTests.cs ; T012 SingleSourceTests.cs
# Then surface codecs (parallel):
T015 JsonCodec.cs ; T016 YamlCodec.cs ; T017 CborCodec.cs
```

---

## Implementation Strategy

### MVP First (US1)
Setup → Foundational → US1 → **STOP & VALIDATE** (conformance matrix + loud-fail green) → demo. A message round-trippable across four surfaces is the first shippable increment.

### Incremental Delivery (store-first CRDT)
US1 (interchange) → US2 (store, ships first) → US3 (rich-text CRDT — the mandatory bar) → US4 (cap/sig) → US5 (routing/e2e over QUIC). Each is independently testable; checkpoint the marathon after implement (and after the US1 MVP within implement).

## Notes
- [P] = different files, no incomplete-task dependency. [Story] labels map to spec US1–US5.
- 🔴 The GLP guard (T053) is **propose-first** — DISCIPLINE §1.14 gate; the E9 DSL translation (T054) is **Claude-only** — Constitution V.
- Ground-terms-only + acyclic-payload + endianness-layering laws hold throughout.
- ~57 tasks: Setup 4, Foundational 5, US1 10, US2 6, US3 9, US4 8, US5 10, Polish 5.
