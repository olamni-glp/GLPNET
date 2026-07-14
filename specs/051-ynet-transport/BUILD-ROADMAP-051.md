# BUILD-ROADMAP-051 — YNET `ynet-transport` full tier build (read FIRST on resume)

**Purpose:** self-contained handoff to drive the deferred 051 tiers to completion, then un-draft →
merge → release PR #111. Sibling: qhstate `056-ynet-service` (embed/policy tier — do NOT build here).

## 0. Where everything is

| Thing | Location |
|---|---|
| Worktree (DO NOT delete until ship) | `D:/bstdev/glp/GLPNET.worktrees/051-ynet-transport` |
| Branch | `051-ynet-transport` (off GLPNET `develop`) · pushed to `origin/051-ynet-transport` |
| Draft PR | `#111` (olamni-glp/GLPNET) — OPEN, DRAFT, MERGEABLE/CLEAN, no CI configured |
| Native lib | `csharp/ynet_transport/` (net10.0, BCL crypto only today) + `csharp/ynet_transport.tests/` |
| Tasks | `specs/051-ynet-transport/tasks.md` (T001–T056; core done, T011–T056 deferred) |
| Marathon run | `mrun-4056c02754bd` (OPEN at ship gate) |

## 1. State (2026-07-14)

- **Core done + codex-reviewed, 34/34 green:** NodeIdentity, SessionSeal (AES-256-GCM H2/H3),
  SignedRecord, RelayPolicy, RoutingAndMixTrust, ExitAbusePolicy, PathState. 6 review bugs fixed.
- **Deferred (this roadmap):** native wire (QUIC/hole-punch/DHT/relay/seal/exit), browser tier,
  BEAM/Gleam tier, leaf mode, polish.

## 2. ENGINEER DECISIONS (recorded — do not re-litigate autonomously)

### DEC-CRYPTO-1 — Node identity algorithm (2026-07-14)
**Ruling:** **Ed25519 primary** (via a third-party dep) with **ECDsa/P-256 (BCL-native) fallback in
exceptional cases**, both behind the existing pluggable **`INodeSigner`** seam (option 3 "where
useful"). Engineer answer verbatim: "2 with 1 fallback in exceptional cases and support for 3 where
useful".
- **Provider:** `BouncyCastle.Cryptography` (pure-managed, already in local NuGet cache, no native
  binary → preserves the "standalone by design" posture). **Needs a DEP-registry entry** (first
  third-party crypto dep in this lib).
- **Fallback trigger ("exceptional cases"):** Ed25519 provider unavailable/unusable on the host →
  `NodeIdentity.Generate()` degrades to P-256, LOUDLY (never silent).
- **Ripple:** self-cert verification must become **algorithm-agnostic** — `SignedRecord.Verify` and
  `PeerIdentityMatches` dispatch on the SPKI algorithm OID (Ed25519 vs P-256), not hardcode ECDsa.
- **FR-002:** satisfied by Ed25519-primary; P-256 documented as the fallback algorithm (not a
  divergence — the pluggable seam is the contract).

### (inherited, cycle-2 clarify) mix-trust = 057-pocw-coin stake, **Loopix fallback** until 057 exists.

## 3. Build sequence (session-sized phases; each ends green + a scoped commit)

- **P1 — Ed25519 identity foundation (T012).** BouncyCastle dep; `Ed25519NodeSigner`; `Generate()`
  Ed25519-primary + P-256 fallback; algorithm-agnostic verify; keep 34 green + add Ed25519 tests.
  ← **START HERE.**
- **P2 — US1 native wire (T011,T014,T015,T016).** ~PARTIAL~ **T014+T016 DONE** (commit `282c9226`):
  `YnetSession` = real handshake (identity-verified, ECDH → per-direction `SessionSeal`) +
  sealed send/receive/close over `IWireChannel`; `InProcessDuplexChannel` (real loopback) proves
  two-in-process connect→send→receive→close (43/43 green; Ed25519↔P-256 interop).
  **T011 DONE** (commit `c2fadaa3`): real `QuicWireChannel` (MsQuic connection + bidi stream +
  length-prefixed frames, sync-bridged; ephemeral self-signed cert; gates on `IsSupported`, refuses
  never simulates) — verified by a real QUIC loopback test (~200 ms handshake, not a skip). Handshake
  hardened to **authenticated ECDH** (identity signs the ephemeral key; MITM-resistant, FR-002).
  44/44 green. **T015 DONE — P2 COMPLETE:** `YnetTransportCapability` (real `IYnetTransport` over
  live `YnetSession`s) + `CapabilityRegistration` (056-token exposure, `Udp`/`Socket` strings — zero
  056 coupling) + `INodeEndpointResolver` seam backed by the real `InProcessFabric`; DHT/relay ops
  are honest NotSupported seams (T025/T028+). 49/49 green incl. 056-stub first-class resolution +
  full capability-level connect→send→receive→close (T014/T016 also [X]).
- **P3 — US2 hole-punch + DHT foundation (T017–T022).** ✅ **DONE (2026-07-14).** `IceDcutrAgent`
  (RFC-8445 candidate priority + DCUtR coordinated open); embedded `SKademliaNode` (secure self-cert
  node-ids, iterative XOR store/lookup, reject-invalid-regardless-of-hop, curated overlay);
  `RendezvousService` (DHT-address + hidden-service modes); `PunchOrchestrator` (≤5 s budget →
  deterministic relay fallback, surfaces direct|relayed, `Unreachable` when no admitted relay,
  FR-018). Deterministic NAT simulation: cone→direct ≥90% within 5 s + symmetric→relay with a real
  YnetSession proving zero pending-frame loss. UDP/STUN sockets = injected seams. 61/61 green.
- **P4 — US3 DHT records + naming (T023–T027).** signed-record store/lookup w/ tamper-reject
  (`SKademliaNode` foundation now landed — wire `IYnetTransport.DhtStore/DhtLookup` onto it, replacing
  the honest NotSupported seams); `further_resolver_required` for human-memorable naming (fabricate
  nothing). ← **RESUME HERE.**
- **P5 — US4 relay forward (T028–T033).** circuit-relay-v2 (mesh) + Tor-cell (internet/critical) +
  DSDV internet route; ciphertext-only forwarding; only 056-admitted relays selected.
- **P6 — US5 sealed routes + anonymity (T034–T039).** garlic bundling (no fixed 3-hop); Veilid
  `SafetySelection`; zero silent downgrades (`seal_unavailable` fail-closed).
- **P7 — US6 trusted-gate exit (T040–T044).** internal in-mesh reach (no egress) + curated-gate exit
  (never volunteer exits); default-deny abuse policy.
- **P8 — US7 browser tier (T045–T049).** separate `ynet_browser/` JS/WASM: WebRTC datachannel +
  WebTransport uplink; guard: never a raw UDP/native-QUIC path; shares the `ICapability` contract.
- **P9 — BEAM/Gleam tier (T054-area).** services/workstation impl extending `gleam_quic/`; `gleam test`.
- **P10 — US8 leaf + polish (T050–T056).** leaf transit-refusal; migration cutover; audit-emit;
  tier-boundary + reuse self-check test.
- **P11 — Ship.** un-draft PR #111 → review → merge → release. **NOTE:** `gh pr ready`/merge/ship are
  **blocked by the auto-mode classifier** — hand the exact commands to the engineer to run via `!`.

## 4. Discipline

- **Tier boundary (FR-024):** never implement service-embed / macaroon-mint / admission-decision /
  durable-mailbox here — those are 056. T056 self-check enforces this.
- **Honest seams:** never mark a network-I/O leaf done until it is real + tested. `[~]` = partial.
- Work only in this worktree; keep it until ship. Each phase: build green → scoped commit (no `-A`).
- Reuse-not-reinvent: olamnit crypto/DSDV/egress + `glp_link` QUIC seams; don't duplicate substrate.
