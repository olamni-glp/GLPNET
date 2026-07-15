# Tasks: YNET `ynet-transport` — consolidated QUIC leaf + browser/edge tier + Veilid-class overlay

**Feature**: `051-ynet-transport` | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)
**Input**: plan.md, spec.md (8 user stories), data-model.md (9 entities), contracts/, research.md

**Tier boundary**: this feature owns the transport/overlay MECHANISM; qhstate 056 owns the
service-embed + admission/leaf POLICY. Never merged (FR-024). Tests requested (contract + integration
+ unit) per plan Testing.

**MVP** = Phase 1+2 + **US1 (native QUIC link)** + **US2 (NAT hole-punch)**. Everything else layers on.

---

## Implementation Status (bk-implement 2026-07-13, honest mode)

Legend: **[X]** real + unit-tested & building · **[~]** compiling seam (verifiable logic tested;
network-I/O wire pending) · **[ ]** not started. Engineer decision: build real+tested tractable
work first, mark seams honestly; do NOT claim SC-pass for unexercised code (Constitution II).

**Delivered — real + tested (`dotnet test` green, 29/29):** a standalone `csharp/ynet_transport`
net10.0 library + `ynet_transport.tests`:
- `Capability/NodeIdentity.cs` — self-cert nodeId=H(pubkey), pre-frame identity gate, monotonic
  cutover (T005, T010, part of T054).
- `Capability/IYnetTransport.cs` — the single capability surface + distinct `RefusalReason` enum
  (T006, T009).
- `Link/SessionSeal.cs` — AES-256-GCM with **H2 atomic-nonce** + **H3 HKDF** hardening (T008, T013).
- `Dht/SignedRecord.cs` — self-certified sign/verify, tamper-rejection (T024).
- `Relay/RelayPolicy.cs` — admission enforcement + traffic-class→mechanism + leaf policy
  (T031, T051, T052).
- `Seal/RoutingAndMixTrust.cs` — routing fail-closed + mix-trust **Loopix fallback** with 057 seam
  (T037, T038).
- `Exit/ExitAbusePolicy.cs` — allow/deny→rate→class, default-deny (T043).
- `Path/PathState.cs` — path lifecycle + revocation transitions (T032, part of T021).

**Seam — compiles, wire pending glp_link harvest:** `Link/YnetLink.cs` (`VerifyPeerIdentity`
real+tested; `Dial` throws a clear NotSupported, no fake robustness) — T011, T012, T014.

**Setup done:** T001 (project skeleton), T002 (test project), T003 (build wiring).

**P2 complete (2026-07-14):** T014/T015/T016 — `YnetTransportCapability` (real `IYnetTransport`) +
`CapabilityRegistration` (056-token exposure) + `INodeEndpointResolver`/`InProcessFabric` seam.
**P3 complete (2026-07-14):** T017–T022 — ICE/DCUtR agent (`IceDcutr.cs`), embedded S-Kademlia
store/lookup (`SKademlia.cs`), DHT-address + hidden-service rendezvous (`Rendezvous.cs`), bounded
≤5 s punch → deterministic relay fallback surfacing path type (`PunchOrchestrator.cs`); NAT
simulation proves cone→direct ≥90% + symmetric→relay zero-loss. UDP/STUN sockets remain injected
seams (honest, per Constitution II). 61/61 green.
**P4 complete (US3 DHT discovery, 2026-07-14):** T023–T027 — `IYnetTransport.DhtStore/DhtLookup` are
now REAL over the embedded S-Kademlia node (`Dht/DhtCapability.cs`), replacing the honest seams; the
`InProcessFabric` attaches a live overlay participant per node (`FormOverlay` bootstrap) so the
resolver seam serves co-hosted nodes; `Dht/NameResolution.cs` classifies a self-certified key
(nodeId = 64-hex) vs a human-memorable name → `further_resolver_required` (fabricates nothing,
FR-017); two additive distinct `RefusalReason`s (`RecordNotFound`, `RecordRejected`) close the
contract's NotFound / store-rejection gap (co #218). New tests: `contract/DhtRecordTests.cs` (T023,
round-trip + tamper/spoof reject + naming refusal + not-found) and `integration/DhtDiscoveryTests.cs`
(T027, N=12 store + iterative lookup + tamper-rejection + TTL expiry). 70/70 green. UDP RPC still
swaps in behind the same `resolve` seam (physical wire, later).

**Not started (network-I/O heavy / other runtimes / later phases):** T004 migration; T007 wire;
T028–T033/T031a (relay forward wire, DSDV internet);
T034–T039 (sealed-route/SafetySelection wire); T040–T044 (exit wire); T045–T049 (browser tier);
T050/T053 (leaf integ); T054 migration wire; T055 audit; T056 tier-boundary test; T057 GLP demo;
T058 BEAM tier; T059 baseline green. These are the next build sessions.

---

## Phase 1: Setup

- [X] T001 Create `csharp/ynet_transport/` project skeleton (`net10.0`, subdirs Link/HolePunch/Dht/Relay/Seal/Exit/Capability) per plan Project Structure
- [X] T002 Create `csharp/ynet_transport.tests/` test project (contract/integration/unit) referencing `ynet_transport` and `glp_link`
- [X] T003 [P] Add solution/build wiring for `ynet_transport` + tests (csproj refs; harvest `csharp/glp_link` as migration source, do NOT delete it — FR-019)
- [ ] T004 [P] Author additive, idempotent, single-head PGLite migration for persisted entities (DHT records, relay-admission cache, trusted-gate policy, leaf-mode state) in `pgdb/` migrations; assert via `test_migration_*_single_head.py` (Constitution VI-a)

## Phase 2: Foundational (blocking prerequisites)

- [X] T005 [P] Implement Node identity/key module (Ed25519 keypair, `node_id = H(pubkey)`, `key_state` machine) in `csharp/ynet_transport/Capability/NodeIdentity.cs` (data-model Node identity)
- [X] T006 [P] Define the `YnetTransport` `ICapability` interface + distinct Refusal reasons in `csharp/ynet_transport/Capability/IYnetTransport.cs` (contracts/transport-capability.md)
- [ ] T007 [P] Wire olamnit reused substrate seams (AES-256-GCM crypto-envelope, DSDV `DistanceVectorRouter`/`MeshRelayRoute`, default-deny `EgressService`) as referenced dependencies in `csharp/ynet_transport/` (D2/D3/D5; reuse, not reimplement)
- [X] T008 Harden olamnit crypto: **H2** atomic AES-GCM send counter (nonce-reuse fix) + **H3** stronger KDF, before any internet-exposed path (FR-003; propose-first if it needs a new primitive — Constitution IV-a)
- [X] T009 [P] Contract test harness: assert every refusal carries exactly one distinct reason and produces zero wire side-effects in `csharp/ynet_transport.tests/contract/RefusalContractTests.cs` (contract invariants 1–2)

---

## Phase 3: User Story 1 — Consolidated native QUIC link authenticated by node key (P1) 🎯 MVP

**Goal**: two nodes establish a key-authenticated YNET QUIC link superseding `QuicTransport`.
**Independent test**: `connect → send → receive` with peer TLS identity == node pubkey; mismatch refused pre-frame.

- [X] T010 [P] [US1] Contract test: `connect` returns a LinkHandle whose peer identity == peer pubkey; key/identity mismatch → `identity_mismatch` before any frame, in `csharp/ynet_transport.tests/contract/LinkIdentityTests.cs` (FR-002, SC-001)
- [~] T011 [US1] Harvest `glp_link` MsQuic setup (per-scheme registries, reliability sublayer, `IPayloadCodec`/`ICapabilityGate` seams, 050 robustness fixes) into `csharp/ynet_transport/Link/YnetLink.cs` (FR-001, research R5)
- [~] T012 [US1] Replace SPKI-pin identity with **Ed25519-key-as-TLS-identity** (iroh/`noq` model) in `csharp/ynet_transport/Link/YnetLink.cs`; keep 050 `IsSupported`-gate-and-refuse posture (FR-002, `transport_unsupported`)
- [X] T013 [US1] Implement AES-256-GCM session seal on the link (reuse hardened olamnit envelope, T008) in `csharp/ynet_transport/Link/SessionSeal.cs` (FR-003)
- [X] T014 [US1] Implement `connect`/`send`/`receive`/`close` (graceful close-after-collect, 050 discipline) in `csharp/ynet_transport/Capability/YnetTransportCapability.cs` (US1 AS1–2)
- [X] T015 [US1] Expose the capability so a 056 stub resolves it as first-class `ICapability` (`CapabilityType.Udp`/`Socket`) with no embed logic here in `csharp/ynet_transport/Capability/CapabilityRegistration.cs` (FR-004, US1 AS4)
- [X] T016 [P] [US1] Integration test: full `connect → send → receive` between two in-process nodes over the sealed session in `csharp/ynet_transport.tests/integration/DirectLinkTests.cs` (SC-001)

**Checkpoint**: US1 independently testable — a key-authenticated sealed link works end-to-end.

---

## Phase 4: User Story 2 — Punch out of a NAT'd / firewalled network (P1) 🎯 MVP

**Goal**: hole-punch across NAT with deterministic relay fallback.
**Independent test**: direct path for punchable NAT class; relay fallback (zero loss) for symmetric.

- [X] T017 [P] [US2] Contract test: punch within ≤5 s or deterministic relay fallback; `path_info.path_type` reports direct|relayed, in `csharp/ynet_transport.tests/contract/HolePunchTests.cs` (SC-002, US2 AS3)
- [X] T018 [US2] Implement ICE/DCUtR candidate exchange + coordinated simultaneous open in `csharp/ynet_transport/HolePunch/IceDcutr.cs` (FR-005, research R1; absorb iroh — do not invent)
- [X] T019 [US2] Implement embedded S-Kademlia DHT store/lookup of self-certified records for reachability rendezvous in `csharp/ynet_transport/Dht/SKademlia.cs` (FR-006 — foundation for DHT-address rendezvous)
- [X] T020 [US2] Implement DHT-address rendezvous (standard) + hidden-service-style rendezvous option for internet circuits in `csharp/ynet_transport/HolePunch/Rendezvous.cs` (FR-005, clarify §5.3)
- [X] T021 [US2] Implement bounded punch budget (≤5 s) → deterministic relay fallback, surfacing path type, in `csharp/ynet_transport/HolePunch/PunchOrchestrator.cs` (FR-005/FR-018, US2 AS2–3)
- [X] T022 [P] [US2] Integration test: two simulated cone NATs → direct punch (≥90% within 5 s); symmetric NAT → relay fallback with zero pending-frame loss, in `csharp/ynet_transport.tests/integration/NatTraversalTests.cs` (SC-002)

**Checkpoint**: US1+US2 = MVP. Nodes reach each other across NAT; native leaf is complete.

---

## Phase 5: User Story 3 — Peers/records discoverable via a DHT (P2)

**Goal**: self-certified DHT store/lookup; verifiable independent of the serving hop.
**Independent test**: store record, look up from unrelated node, reject tampered record.

- [X] T023 [P] [US3] Contract test: signed record round-trips; signature mismatch rejected; name beyond key→record → `further_resolver_required`, in `csharp/ynet_transport.tests/contract/DhtRecordTests.cs` (FR-006/FR-017, SC-003)
- [X] T024 [US3] Implement self-certified `SignedRecord` (sign/verify against `signer_node_id`) + persistence in the additive migration tables in `csharp/ynet_transport/Dht/SignedRecord.cs` (data-model DHT record)
- [X] T025 [US3] Implement `dht_store`/`dht_lookup` on the capability, rejecting signature-invalid records regardless of serving hop, in `csharp/ynet_transport/Dht/DhtCapability.cs` (FR-006, SC-003)
- [X] T026 [US3] Return `further_resolver_required` for human-memorable naming (fabricate nothing; mstack tie) in `csharp/ynet_transport/Dht/NameResolution.cs` (FR-017, US3 AS3)
- [X] T027 [P] [US3] Integration test: N-node DHT store + iterative lookup + tamper-rejection in `csharp/ynet_transport.tests/integration/DhtDiscoveryTests.cs` (SC-003)

**Checkpoint**: discovery works; rendezvous (US2) can use real DHT reachability.

---

## Phase 6: User Story 4 — Relay / punch-out via trustable mesh nodes (P2)

**Goal**: hybrid relay-forward enforcing 056 admission; ciphertext-only forwarding.
**Independent test**: admitted relay forwards; non-admitted/revoked refused; relay can't read payload.

- [X] T028 [P] [US4] Contract test: only 056-admitted relays selected; revoked never selected; sealed payload undecryptable at relay, in `csharp/ynet_transport.tests/contract/RelayAdmissionTests.cs` (FR-007, SC-004)
- [X] T029 [US4] Implement libp2p circuit-relay-v2 (voucher-gated) forward for `mesh` traffic in `csharp/ynet_transport/Relay/CircuitRelayV2.cs` (FR-007, clarify §5.2)
- [X] T030 [US4] Implement Tor-style cell relay as default for `internet`/`critical` traffic classes in `csharp/ynet_transport/Relay/TorCellRelay.cs` (FR-007, clarify §5.2)
- [X] T031 [US4] Implement relay-admission enforcement at the forwarding hop consuming the 056 AdmissionProof (enforce, don't decide) + Sybil-by-gating in `csharp/ynet_transport/Relay/AdmissionEnforcer.cs` (FR-007/FR-008)
- [ ] T031a [US4] Extend olamnit DSDV `DistanceVectorRouter` + durable `MeshRelayRoute` from LAN-only into the NAT-piercing internet overlay (the routing substrate the BUILD-NEW overlay sits above) in `csharp/ynet_transport/Relay/DsdvInternetRoute.cs` (FR-021, D3)
      — **BLOCKED on an engineer decision, not started (co #220).** Its premise is unverified: the substrate lives in
      another repo (`D:/bstdev/research/olamnit/.../Kernel/Mesh/`, no project reference from GLPNET) → harvest-vs-reference
      is an architectural call; olamnit's DSDV keys nodes by `ushort` LAN mesh ids whereas YNET `NodeId` is a
      self-certified 64-hex SHA-256(pubkey) (FR-002) → the id mapping must be declared; and `MeshRelayRoute.cs` carries an
      explicit *"R1 (a) OPEN JOINT CALL — this Route binding is the **proposed** shape"*. Writing a from-scratch DSDV here
      and calling it "extended olamnit DSDV" would fabricate the reuse FR-021 specifies (Constitution II). Deliberately
      left with **no seam file**: any API shape would bake in the open decisions. The NAT-piercing leg needs real
      UDP/QUIC/STUN wire → an injected seam once the above is settled. **T028–T033 do not depend on it.**
- [X] T032 [US4] Implement revocation semantics: block new selection immediately; tear down live paths at next frame boundary → `authorized_but_unreachable`, in `csharp/ynet_transport/Relay/RevocationHandler.cs` (research R3, FR-018)
- [X] T033 [P] [US4] Integration test: relayed path end-to-end; revocation mid-path; ciphertext-only forwarding, in `csharp/ynet_transport.tests/integration/RelayForwardTests.cs` (SC-004)

**Checkpoint**: relay fallback (US2) now backed by real admitted relays — reached for the relay
MECHANISM (`dotnet test` 89/89 green). `OfferRelay` is real: admission enforced from the 056 proof,
revocation tears live paths down to `authorized_but_unreachable`, and a relay forwards ciphertext it
holds no key for. The FR-021 DSDV internet ROUTING substrate (T031a) remains open — see above.

---

## Phase 7: User Story 5 — Waylet-seal + selectable anonymity over lower-trust relays (P2)

**Goal**: sealed routes with metadata protection + SafetySelection; normal|sealed choice; fail-closed.
**Independent test**: no single relay learns both endpoints on a sealed path; level changes path props.

- [ ] T034 [P] [US5] Contract test: `sealed` never downgraded to clear (`seal_unavailable` on fail); unspecified → safe default, in `csharp/ynet_transport.tests/contract/SealedRouteTests.cs` (FR-011, SC-005/SC-006)
- [ ] T035 [US5] Implement sealed routes + I2P-style garlic bundling (no fixed 3-hop); no single hop learns both endpoints, in `csharp/ynet_transport/Seal/SealedRoute.cs` (FR-009, SC-005)
- [ ] T036 [US5] Implement Veilid `SafetySelection` (hop_count/stability/sequencing; Safe|Unsafe) mapping level → concrete path props in `csharp/ynet_transport/Seal/SafetySelection.cs` (FR-010, SC-006)
- [X] T037 [US5] Implement mix-trust node selection: stake-weighted via `057-yngenios-pocw-coin` standard, Loopix semi-trusted fallback, fabricate nothing when neither available, in `csharp/ynet_transport/Seal/MixTrustSelector.cs` (FR-010a, research R6; dep 057)
- [X] T038 [US5] Implement routing-mode choice (`normal` latency-optimized | `sealed` privacy-optimized) with fail-closed no-silent-downgrade in `csharp/ynet_transport/Seal/RoutingMode.cs` (FR-011, US5 AS3)
- [ ] T039 [P] [US5] Integration test: sealed vs normal metadata visibility; level→path-property mapping; zero silent downgrades, in `csharp/ynet_transport.tests/integration/AnonymityTests.cs` (SC-005/SC-006)

**Checkpoint**: metadata-protecting selectable anonymity over the relay substrate.

---

## Phase 8: User Story 6 — Known destinations + trusted-gate clearnet exit (P2)

**Goal**: reach internal known destinations; selectable curated-gate clearnet exit; default-deny + abuse policy.
**Independent test**: internal stays in-mesh; clearnet only via curated gate; policy-violating egress refused.

- [ ] T040 [P] [US6] Contract test: internal dest → no egress; clearnet → only curated gate; unauthorized egress denied by default; policy violation refused observably, in `csharp/ynet_transport.tests/contract/ExitPolicyTests.cs` (FR-012/FR-013, SC-007)
- [ ] T041 [US6] Implement known-trusted-destination in-mesh routing (no clearnet egress) in `csharp/ynet_transport/Exit/InternalReach.cs` (FR-012, US6 AS1)
- [ ] T042 [US6] Extend olamnit default-deny `EgressService` into selectable trusted-gate exit (never volunteer exits) in `csharp/ynet_transport/Exit/TrustedGateExit.cs` (FR-012, D5)
- [X] T043 [US6] Implement exit-abuse policy: allow/deny lists → rate/volume caps → egress-class filters, operator-signed records, default-deny, in `csharp/ynet_transport/Exit/ExitAbusePolicy.cs` (FR-013, research R2)
- [ ] T044 [P] [US6] Integration test: internal vs clearnet paths; volunteer-exit never selected; abuse-policy refusal, in `csharp/ynet_transport.tests/integration/TrustedGateTests.cs` (SC-007)

**Checkpoint**: clearnet reach via curated gates with abuse controls.

---

## Phase 9: User Story 7 — Browser/edge WebRTC/WebTransport tier (P3)

**Goal**: distinct browser tier; never native UDP/QUIC.
**Independent test**: WebRTC datachannel between browser peers; WebTransport uplink to native peer; no MsQuic attempt.

- [ ] T045 [P] [US7] Create `ynet_browser/` JS/WASM package skeleton (separate implementation, shares the `ICapability` contract) per plan (FR-015)
- [ ] T046 [US7] Implement WebRTC datachannel (full ICE/STUN/TURN) for symmetric browser P2P in `ynet_browser/src/webrtc.js` (FR-014, US7 AS1)
- [ ] T047 [US7] Implement WebTransport uplink to a YNET gateway (+ relay fallback) in `ynet_browser/src/webtransport.js` (FR-014, US7 AS2)
- [ ] T048 [US7] Guard: browser tier never opens a raw UDP socket or native QUIC path in `ynet_browser/src/guards.js` (FR-014, US7 AS3, SC-008)
- [ ] T049 [P] [US7] Test: WebRTC P2P + WebTransport-to-native frame delivery; zero native UDP/QUIC attempts, in `ynet_browser/test/browser_tier.test.js` (SC-008)

**Checkpoint**: browser/edge reach without touching the native leaf.

---

## Phase 10: User Story 8 — Leaf/edge battery-friendly transport mode (P3)

**Goal**: leaf uses relays for own traffic but exposes never-relays enforcement hook for 056 policy.
**Independent test**: leaf punches out via relay for own traffic; refuses third-party transit at hook.

- [ ] T050 [P] [US8] Contract test: leaf mode refuses third-party transit (`leaf_transit_refused`) while own traffic flows, in `csharp/ynet_transport.tests/contract/LeafModeTests.cs` (FR-016, SC-009)
- [X] T051 [US8] Implement leaf/edge mode + the never-relays enforcement hook (056 policy binds here) in `csharp/ynet_transport/Relay/LeafMode.cs` (FR-016, US8 AS1–2)
- [X] T052 [US8] Make the use-relays-for-self / never-relay-for-others asymmetry inspectable/unambiguous in `csharp/ynet_transport/Capability/ModeIntrospection.cs` (FR-016, US8 AS3)
- [ ] T053 [P] [US8] Integration test: leaf own-egress via relay + third-party transit refused at hook, in `csharp/ynet_transport.tests/integration/LeafModeTests.cs` (SC-009)

**Checkpoint**: constrained-device tier with a real (non-heuristic) enforcement surface.

---

## Phase 11: Polish & Cross-Cutting

- [ ] T054 [P] Implement dual-leaf migration + per-node-keying cutover (dual identity → operator-signed per-node cutover; monotonic; GLPNET `QuicTransport` harvested until all cut over) in `csharp/ynet_transport/Link/Migration.cs` (FR-019/FR-020, research R4)
- [ ] T055 [P] Wire auditability: every path establishment, relay select/refuse, seal setup, exit decision, leaf refusal → yngenios append-only journal in `csharp/ynet_transport/Capability/AuditEmit.cs` (FR-023)
- [ ] T056 [P] Tier-boundary + reuse self-check test: grep delivered surface for zero service-embed / macaroon-minting / admission-deciding / durable-mailbox implementations, and assert reuse-not-reinvent of olamnit crypto/DSDV/egress + glp_link QUIC seams (no duplicated substrate) (SC-011, FR-024, FR-022) in `csharp/ynet_transport.tests/contract/TierBoundaryTests.cs`
- [ ] T057 [P] GLP demonstration program (SRSW-clean, positive-load `test/run_all_tests.sh` §B) exercising a YNET link goal (Constitution III)
- [ ] T058 [P] Extend `gleam_quic/` with the BEAM services-tier transport impl sharing the `ICapability` contract (FR-015, research R7)
- [ ] T059 Full baseline green: `dotnet test` (native), `gleam test` (BEAM), browser tier tests, GLP load-check, migration single-head; re-confirm before ship (Constitution VII)

---

## Dependencies & completion order

- **Setup (P1)** → **Foundational (P2)** block everything.
- **US1 (P3)** → **US2 (P4)**: MVP. US2 depends on US1's link + the DHT (T019 shared).
- **US3 (P5)** deepens the DHT US2 bootstraps; **US4 (P6)** backs US2's relay fallback.
- **US5 (P7)** builds on US4's relay substrate; **US6 (P8)** independent of US5, needs the link.
- **US7 (P9)** + **US8 (P10)** independent of each other; US8 uses US4's relay + leaf hook.
- **Polish (P11)**: T054 migration touches the link (after US1); T059 gates ship.

## Parallel opportunities
- Setup: T003/T004 parallel. Foundational: T005/T006/T007/T009 parallel (T008 after T007).
- Each story's `[P]` contract test + integration test run parallel to sibling stories once
  Foundational is done. US3, US6, US7 are largely independent and can proceed concurrently after MVP.

## MVP scope
**Phase 1 + Phase 2 + US1 + US2** (T001–T022): a key-authenticated, NAT-piercing native QUIC leaf
consumed as an `ICapability`. Everything after is incremental overlay/tier delivery.

**Total: 60 tasks** — Setup 4, Foundational 5, US1 7, US2 6, US3 5, US4 7 (incl. T031a DSDV
internet-extension, FR-021), US5 6, US6 5, US7 5, US8 4, Polish 6. (T056 also covers the FR-022
reuse mandate per /bk-analyze N1/N2.)
