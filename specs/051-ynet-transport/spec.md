# Feature Specification: YNET `ynet-transport` — consolidated QUIC leaf + browser/edge tier + Veilid-class overlay

**Feature Branch**: `051-ynet-transport`
**Created**: 2026-07-13
**Status**: Draft
**Input**: TASK 3 (GLPNET half) of the YNET design program. Scope fixed by engineer decisions
D1–D6 (`decisions-D1-D6.md`) and the external cross-verification cycle
(`curator_report_cycle2.md`, run `20260712T223008Z-c2a2`). This feature owns the **transport +
overlay tier**; its sibling qhstate feature **056 `ynet-service`** owns the service-embed tier
(first-class `Machine`/`ICapability.Invoke`, macaroon admission, leaf-never-relays *policy*,
durable mailbox). The two tiers are NEVER merged (FR-024 / tier-boundary invariant).

<!--
  TIER BOUNDARY (from decisions-D1-D6.md §"Consolidated tier boundary" + cycle-2 §7):
    THIS feature (GLPNET 051) OWNS (the mechanism, not the policy):
      - The consolidated YNET-owned QUIC leaf — a hardened superset that SUPERSEDES GLPNET
        `QuicTransport`; absorb iroh/`noq` (Ed25519-key-as-TLS-identity + in-handshake NAT
        traversal) and everything useful from GLPNET 050 native-QUIC (D1).
      - NAT hole-punch (ICE/DCUtR), relay-FORWARD mechanism, rendezvous, DHT store/lookup.
      - Sealed-route / onion-garlic layering, metadata protection, selectable-anonymity MECHANISM
        (adopt Veilid SafetySelection), routing-mode (normal|sealed) mechanism.
      - Crypto-envelope / sealed link (olamnit AES-256-GCM baseline, H2/H3 hardened), key mgmt
        (nodeId = H(pubkey), self-certified records).
      - Trusted-gate clearnet exit / egress (extend olamnit EgressService), exit-abuse policy.
      - The distinct browser/edge WebRTC-datachannel + WebTransport-uplink tier.
      - Leaf/edge transport mode + the enforcement HOOK the 056 leaf-never-relays policy binds to.
    SIBLING feature (qhstate 056 ynet-service) OWNS (consumed here / calls into here, NOT built here):
      - Service embed as a first-class qhstate Machine / ICapability (D4).
      - The verify-before-act macaroon gate at ICapability.Invoke + macaroon-gated trusted-relay
        ADMISSION decision (051 provides the relay mechanism; 056 decides who is admitted).
      - The leaf-never-relays POLICY declaration (051 enforces the mechanism; 056 owns the policy).
      - Durable exactly-once messaging above the wire (qhstate mailbox).
-->

## Clarifications

### Session 2026-07-13

- Q: Relay-forward mechanism (cycle-2 §5.2)? → A: **Hybrid by traffic class** — **libp2p
  circuit-relay-v2** (voucher-gated, aligns with 056 macaroon admission) for **most mesh traffic**;
  **Tor-style cell relay** as the **default for internet traffic and for critical message flows /
  workspaces**.
- Q: DHT ownership (cycle-2 §2 dht-store REFINE)? → A: **Build an embedded S-Kademlia** DHT — a
  curated overlay with self-certified records and Sybil-by-gating; **not** a public/external DHT.
- Q: Rendezvous mechanism for hole-punch coordination (cycle-2 §5.3)? → A: **DHT-address rendezvous**
  as the general standard; **hidden-service-style rendezvous** for **internet circuits** — a standard
  option, selectable / optional per user default.
- Q: Mix / anonymity trust model for sealed routes (cycle-2 §5.5)? → A: **Stake-weighted nodes via
  the new `057-yngenios-pocw-coin` (proof-of-cooperative-work) mechanism as the standard**, with
  **Loopix semi-trusted providers as the fallback** option.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - A consolidated native QUIC link authenticated by node key (Priority: P1)

Two yngenios nodes on reachable networks establish a **single consolidated YNET-owned QUIC link**
where **each node's Ed25519 key IS its TLS identity** (absorbing iroh/`noq`), sealed with the
olamnit AES-256-GCM crypto-envelope baseline. This one transport is the hardened **superset that
supersedes GLPNET `QuicTransport`** (D1: GLPNET native-QUIC from feature 050 is a migration source
to harvest, not a permanent co-leaf) — nothing useful in GLPNET transport is left behind, nothing is
duplicated in perpetuity.

**Why this priority**: Every other YNET capability (hole-punch, relay, sealed routes, exit) rides
this link. Without an authenticated wire there is nothing to punch out of, relay over, or seal. It
is the minimum viable transport slice, and the external cycle confirmed QUIC-P2P is viable via
iroh's key-as-TLS-identity model (cycle-2 §2 `quic-link` REFINE; §1.1).

**Independent Test**: Stand up two nodes on the same reachable segment; establish a YNET QUIC link
keyed by each node's Ed25519 identity; complete an end-to-end `connect → send → receive`. Assert the
peer identity presented on the wire equals the node's public key (no separate certificate authority),
and that a peer presenting a key that does not match its handshake identity is rejected.

**Acceptance Scenarios**:

1. **Given** two reachable nodes each holding an Ed25519 node key, **When** node A dials node B,
   **Then** a QUIC link is established in which B's verified TLS identity equals B's node public key.
2. **Given** an established link, **When** A sends an application frame, **Then** B receives it intact
   over the AES-256-GCM sealed session and A receives B's response.
3. **Given** a dialer presenting a node key that does not match its handshake material, **When** it
   attempts to connect, **Then** the link is refused before any application frame is exchanged.
4. **Given** a capability exposed to consumers, **When** qhstate (056) resolves the YNET transport,
   **Then** it obtains a first-class `ICapability` (e.g. `CapabilityType.Udp`/`Socket`) for
   connect/send/receive without this tier implementing any service-embed logic.

---

### User Story 2 - Punch out of a NAT'd / firewalled network (Priority: P1)

A node behind NAT or a firewall establishes a direct link to a peer that is also NAT'd, by
**hole-punching** — exchanging in-handshake candidate addresses and performing coordinated
simultaneous open (ICE / DCUtR, as implemented by iroh and specified by the libp2p/QUIC literature),
**falling back to a relay** when a direct punch cannot be achieved. This is subject-brief R2 and the
**#1 internal gap** (absent in all four internal slices), now richly referenced externally (cycle-2
§1 "richly solved externally → absorb ICE/DCUtR/iroh, not invent").

**Why this priority**: The whole reason YNET exists over a plain link is to reach peers across NAT.
Without hole-punch, the transport only works on already-reachable segments. Co-P1 with US1.

**Independent Test**: Place two nodes behind separate simulated NATs with no inbound reachability;
run rendezvous + candidate exchange + coordinated open; assert a **direct** peer-to-peer path is
established for the punchable NAT class, and that for a non-punchable (e.g. symmetric-NAT) class the
transport transparently falls back to a relay path and still delivers frames.

**Acceptance Scenarios**:

1. **Given** two nodes behind cone NATs with no static inbound port, **When** they rendezvous and
   exchange in-handshake candidates, **Then** a direct hole-punched QUIC path is established.
2. **Given** a NAT class that cannot be punched, **When** a direct punch is attempted and fails,
   **Then** the transport falls back to a relay path (US4) without losing the pending frames and
   surfaces which path type (direct vs relayed) is in use.
3. **Given** a hole-punch attempt, **When** it exceeds its bounded time budget, **Then** it fails
   deterministically to the relay fallback rather than hanging.
4. **Given** peers coordinating a punch, **When** they rendezvous, **Then** the general standard is
   **DHT-address rendezvous** (over the embedded S-Kademlia DHT, US3); for **internet circuits** a
   **hidden-service-style rendezvous** is available as a selectable/optional-per-user-default mode
   (Clarifications 2026-07-13; cycle-2 §5.3).

---

### User Story 3 - Peers and records are discoverable via a DHT (Priority: P2)

A node **stores and looks up** peer-reachability and self-certified key→record entries in a
**distributed hash table** (Kademlia iterative lookup, XOR metric), so a caller can resolve a known
peer's current reachability without a central directory. Records are **self-certified**
(nodeId = H(pubkey), IPNS-style signed records) so a lookup result is verifiable without trusting the
DHT node that served it. This is BUILD-NEW (cycle-2 §2 `dht-store`/`dht-lookup`), proven embeddable
by Veilid.

**Why this priority**: Discovery underpins rendezvous (US2) and relay/route selection (US4/US5) once
peers are not statically configured. P2 because US1/US2 can run against statically-known peers first.

**Independent Test**: Populate a small DHT with N nodes; store a self-certified reachability record
under a key; from an unrelated node perform an iterative lookup; assert the record is returned and
its signature verifies against the claimed node key, and that a tampered record is rejected.

**Acceptance Scenarios**:

1. **Given** a DHT of participating nodes, **When** a node stores a signed reachability record under
   its key, **Then** an iterative lookup from another node returns that record.
2. **Given** a returned record, **When** the caller verifies it, **Then** a record whose signature
   does not match the claimed node key is rejected (self-certification holds against a malicious DHT
   hop).
3. **Given** the discovery surface, **When** a name beyond self-certified key→record resolution is
   requested (human-memorable naming), **Then** the transport returns an explicit "further resolver
   required" and does not fabricate a resolution (cycle-2 §6 — decentralized naming unsolved in the
   corpus; ties to the mstack R9 gap). YNET **builds an embedded S-Kademlia DHT** (a curated overlay
   with self-certified records and Sybil-by-gating, **not** a public/external DHT) — Clarifications
   2026-07-13; cycle-2 §2 dht-store REFINE.

---

### User Story 4 - Relay / punch-out via trustable mesh nodes (Priority: P2)

When a direct path is impossible, traffic is **forwarded through relay nodes** drawn from the
curated set of trusted — and *trustable-but-not-well-known* — yngenios nodes (subject-brief R4). This
tier owns the relay-**forward mechanism**; the **admission decision** (which node is authorized to
relay) is a macaroon-caveat decision owned by qhstate 056 — this tier consumes that decision and
enforces it at the forwarding hop. Sybil/DoS resistance is achieved **by curated-node gating**, not
crypto-puzzles (cycle-2 §2 `sybil-dos-resistance` INSIGHT: every strong Sybil result needs a trusted
authority; the curated node set IS that authority).

**Why this priority**: Relay is the fallback that makes NAT traversal reliable (US2) and the substrate
sealed routes are laid over (US5). P2 — direct links (US1/US2) deliver value before relaying exists.

**Independent Test**: Configure a relay node and two endpoints with no direct path; establish a
relayed path through it; assert frames are forwarded end-to-end. Then present the relay with a
forwarding request from a node the 056 admission decision has **not** authorized, and assert the
relay refuses to forward.

**Acceptance Scenarios**:

1. **Given** two endpoints with no direct path and one admitted relay, **When** a relayed path is
   requested, **Then** frames are forwarded end-to-end through the relay.
2. **Given** a relay whose 056-side admission has been revoked, **When** it is offered as a
   forwarding hop, **Then** the transport does not select it and any live path through it is handled
   per the revocation contract.
3. **Given** a relay node, **When** it forwards sealed traffic (US5), **Then** it forwards ciphertext
   only and cannot read the payload it relays.
4. **Given** the relay-forward mechanism, **When** a path is built, **Then** **libp2p
   circuit-relay-v2** (voucher-gated) carries **most mesh traffic**, while **Tor-style cell relay** is
   the **default for internet traffic and for critical message flows / workspaces** (Clarifications
   2026-07-13; cycle-2 §5.2). TURN/WebRTC relay is scoped to the browser tier (US7) only.

---

### User Story 5 - Waylet-seal + selectable anonymity over lower-trust relays (Priority: P2)

A caller sends traffic over routes that may traverse **lower-trust mesh segments** while preserving
confidentiality and **who-talks-to-whom metadata protection** via **sealed routes** (Veilid
private-route model + I2P-style garlic bundling, no rigid fixed 3-hop circuit). Anonymity is a
**selectable, per-context property** (adopt Veilid's `SafetySelection`: tunable hop count /
stability / sequencing; explicit Safe vs Unsafe), and routing is **always a choice** between
*normal* (more-encrypted but clear mesh routes) and *sealed* (encoded stable routes) — subject-brief
R5/R7/R8. This tier owns the mechanism; qhstate 056 surfaces the selection to callers.

**Why this priority**: Metadata protection + selectable anonymity is YNET's core differentiator over
a plain encrypted transport, but it builds on the relay substrate (US4). P2.

**Independent Test**: Send traffic in `normal` mode and assert a relay on the path can see the
next-hop addressing; send the same in `sealed` mode and assert no single relay on the path learns
both the origin and the final destination, and that raising the anonymity level demonstrably changes
the path characteristics (hop count / route stability).

**Acceptance Scenarios**:

1. **Given** a caller selecting `normal` routing, **When** traffic flows, **Then** it is
   confidential end-to-end but mesh next-hop routing is visible to relays on the path.
2. **Given** a caller selecting `sealed` routing at a stated anonymity level, **When** traffic flows,
   **Then** no single relay learns both endpoints, and the selected level maps to concrete path
   properties (hop count / sequencing) per the SafetySelection model.
3. **Given** an unspecified selection, **When** traffic flows, **Then** it resolves to the declared
   safe default and the mechanism never silently downgrades a requested seal to a clear route.
4. **Given** the mix / anonymity trust model, **When** sealed-route relays are selected, **Then** the
   standard is **stake-weighted node selection via the new `057-yngenios-pocw-coin`
   (proof-of-cooperative-work) mechanism**, with **Loopix-style semi-trusted providers as the
   fallback** when the pocw-coin signal is unavailable (Clarifications 2026-07-13; cycle-2 §5.5).
   `sealed` routing optimizes path selection for privacy; `normal` optimizes for latency
   (§5.1 — natural reading, confirmable at `/bk-plan`).

---

### User Story 6 - Reach known destinations and exit to clearnet via trusted gates (Priority: P2)

A node reaches **known trusted destinations** (subject-brief R3) and, when the caller wants it, exits
to the wider internet through **curated trusted gates** — extending olamnit's default-deny
`EgressService` into a **selectable trusted-gate exit** (D5). YNET deliberately does **not** use
volunteer exit nodes (Tor's volunteer-exit model is documented as an "unsolved arms race", cycle-2
§1.3 / §4 D5). The gate tier owns an **exit-abuse policy** because no drop-in reference exists.

**Why this priority**: Clearnet reach is subject-brief R6 and a named YNET gap over Veilid (which is
internal-only). P2 — internal mesh reach (US1–US5) is usable before clearnet exit is added.

**Independent Test**: Route a request for a known internal destination and assert it is delivered
without any clearnet exit. Then request a clearnet destination through a curated gate and assert it
egresses only via an authorized gate, that a non-curated/volunteer exit is never selected, and that
an egress violating the exit-abuse policy is refused.

**Acceptance Scenarios**:

1. **Given** a known trusted internal destination, **When** a caller connects, **Then** the path
   stays inside the mesh and no clearnet egress occurs.
2. **Given** a caller electing clearnet exit, **When** it connects to an external destination,
   **Then** egress occurs only through a curated trusted gate and never through a volunteer/unknown
   exit.
3. **Given** the default-deny posture (from olamnit `EgressService`), **When** egress is not
   explicitly elected/authorized, **Then** it is denied by default.
4. **Given** the exit-abuse policy, **When** an egress request violates it, **Then** the gate refuses
   and the refusal is observable. [NEEDS CLARIFICATION: the exit-abuse policy is BUILD-NEW with no
   corpus reference (cycle-2 §4 D5) — what classes of egress does a curated gate refuse (destination
   allow/deny lists, rate/volume caps, content classes), and who administers them?]

---

### User Story 7 - Browser/edge nodes join over a distinct WebRTC/WebTransport tier (Priority: P3)

A node running **in a browser or constrained edge runtime** joins the YNET mesh over a **separate
transport tier** — **WebRTC datachannel** for symmetric browser-to-browser P2P (the only browser path
with NAT traversal: full ICE + STUN + TURN) plus a **WebTransport uplink** (QUIC-over-HTTP/3, strictly
client→gateway) to a YNET gateway, with relay fallback. This tier **cannot ride the native
consolidated QUIC** of US1: a browser cannot manage UDP sockets, and **MsQuic cannot target the
browser** (cycle-2 §3a, hard-flagged; verified three ways: Veilid-WASM is WS/WSS-only, iroh forces
RelayOnly in-browser, libp2p browser peers reach others only via WebRTC datachannel).

**Why this priority**: Browser/edge reach broadens YNET's applicability but is a distinct
implementation the external cycle proved cannot be folded into the native leaf (cycle-2 §3b:
Gleam's JS target has an incompatible concurrency model → expect ≥2 transport implementations). P3 —
the native tier delivers the core mesh first.

**Independent Test**: Connect a browser-tier endpoint to a native-tier peer through a YNET gateway;
establish a WebRTC datachannel to another browser peer; assert both carry frames end-to-end and that
no native MsQuic path is attempted from the browser tier.

**Acceptance Scenarios**:

1. **Given** two browser-tier endpoints, **When** they connect, **Then** a WebRTC datachannel (with
   ICE/STUN/TURN) carries frames peer-to-peer between them.
2. **Given** a browser-tier endpoint and a native-tier peer, **When** they connect, **Then** the
   browser reaches the native peer via a WebTransport uplink to a YNET gateway (and/or relay), never
   via a native MsQuic dial.
3. **Given** the browser tier, **When** it initializes, **Then** it never attempts to open a raw UDP
   socket or a native consolidated-QUIC path (which the browser cannot provide).

---

### User Story 8 - Leaf/edge transport mode is battery- and data-friendly (Priority: P3)

A node running in **leaf/edge mode** uses the transport in a **battery- and data-friendly** way: it
may **use relays to punch out** for its own traffic (R4) but the transport exposes the enforcement
**hook** by which a leaf **never serves as a relay for third-party transit** — the *policy* itself is
declared and owned by qhstate 056 (leaf-never-relays), this tier provides the mechanism that makes it
enforceable at the forwarding path. Extends olamnit's leaf/client tier (cycle-2 §2 `leaf-client-mode`
CONFIRM EXTEND).

**Why this priority**: The constrained-device tier is a named YNET goal (subject-brief R9), but it is
a mode of the existing transport rather than a new substrate. P3.

**Independent Test**: Run a node in leaf mode; assert its own originated/terminated traffic still
punches out via relays, while a third-party transit/forward offered to it is refused at the transport
forwarding hook (so the 056 policy has a real enforcement surface, not a heuristic).

**Acceptance Scenarios**:

1. **Given** a leaf-mode node, **When** it originates traffic that needs a relay to punch out,
   **Then** the transport uses a relay for the leaf's own traffic.
2. **Given** a leaf-mode node, **When** a third-party transit frame is offered for forwarding,
   **Then** the transport forwarding hook refuses it, giving the 056 leaf-never-relays policy a
   concrete enforcement point.
3. **Given** a leaf-mode node, **When** it is inspected, **Then** its use-relays-for-self /
   never-relays-for-others asymmetry is unambiguous (R4 vs R9).

---

### Edge Cases

- **All relays rejected / punch fails everywhere**: When neither a direct punch nor any admitted
  relay yields a path, the transport MUST report a distinct **unreachable** reason (not an auth
  failure and not a silent drop) so the consuming 056 service can surface authorized-but-unreachable.
- **Relay revoked mid-path**: A relay's 056-side admission is revoked while a path through it is
  live. [NEEDS CLARIFICATION: does revocation tear down in-flight paths immediately, or only prevent
  new path selection through that relay? — mirror of the 056 US3 revocation-race question.]
- **Sealed route requested but only clear paths available**: The transport MUST fail closed rather
  than silently downgrade a requested seal to a clear route (never weaken the requested anonymity).
- **GLPNET `QuicTransport` co-existence during migration**: While the dual-leaf transition is live
  (D1), a node MUST NOT end up with an ambiguous or duplicated transport identity — the YNET link is
  the single authority and GLPNET native-QUIC is harvested, not run as a competing permanent owner.
- **Per-node keying vs GLPNET shared-cert**: GLPNET's one club-wide certificate conflicts with YNET
  per-node keying (nodeId = H(pubkey)). Per-node keying wins in the YNET-owned transport; the
  migration path off the shared cert MUST be explicit. [NEEDS CLARIFICATION: migration sequencing —
  does a node run both identities during transition, and how is the cutover authorized?]
- **Browser tier asked for a native-only capability**: A browser-tier endpoint requests a capability
  only the native tier provides (e.g. serving as a hole-punch relay). The transport MUST refuse
  cleanly with a tier-capability reason rather than attempt an impossible native path.
- **Name resolution beyond self-certified records**: A caller asks the transport to resolve a
  human-memorable name. The transport MUST return "further resolver required" and MUST NOT fabricate
  a resolution (cycle-2 §6; subject-brief R9 / mstack tie).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a single **consolidated YNET-owned QUIC transport** that
  **supersedes and replaces GLPNET `QuicTransport`**, harvesting anything useful from GLPNET native
  QUIC (feature 050: MsQuic HTTP/3 + WS QUIC, per-scheme registries, reliability sublayer,
  `IPayloadCodec`/`ICapabilityGate` seams) — a hardened, hyper-optimised superset, not a perpetual
  co-leaf (D1).
- **FR-002**: Each node's **Ed25519 node key MUST be its transport (TLS) identity** (absorbing
  iroh/`noq`), and a peer whose presented key does not match its handshake identity MUST be rejected
  before any application frame is exchanged (cycle-2 §2 `quic-link` REFINE).
- **FR-003**: The transport MUST seal every session with the **olamnit AES-256-GCM crypto-envelope
  baseline**, reused (not re-invented), and MUST land the two required hardenings before internet
  exposure: **H2** (atomic AES-GCM send counter / nonce-reuse fix) and **H3** (stronger KDF) — D2.
- **FR-004**: The transport MUST expose itself to consumers as a first-class **`ICapability`** (e.g.
  `CapabilityType.Udp`/`Socket`) for connect/send/receive, and MUST NOT implement any service-embed,
  macaroon-admission, or durable-mailbox logic (owned by 056; tier boundary).
- **FR-005**: The transport MUST **hole-punch** out of NAT'd/firewalled networks using in-handshake
  candidate exchange + coordinated simultaneous open (ICE/DCUtR, absorbing iroh), coordinating
  rendezvous by **DHT-address rendezvous** over the embedded DHT as the general standard and by
  **hidden-service-style rendezvous** for internet circuits (selectable / optional per user default),
  and MUST **fall back to a relay path** deterministically (within a bounded time budget) when a
  direct punch cannot be achieved — surfacing whether the active path is direct or relayed (R2;
  cycle-2 §2 `nat-holepunch` / §5.3; Clarifications 2026-07-13).
- **FR-006**: The transport MUST provide **DHT store and lookup** over an **embedded S-Kademlia** DHT
  (iterative lookup, XOR metric; a curated overlay, **not** a public/external DHT) of
  **self-certified** key→record entries (nodeId = H(pubkey), signed records), such that a lookup
  result is verifiable independently of the DHT node that served it and a tampered record is rejected
  (cycle-2 §2 `dht-store`/`dht-lookup` REFINE; Clarifications 2026-07-13).
- **FR-007**: The transport MUST forward traffic through **relay nodes** when no direct path exists,
  using **libp2p circuit-relay-v2 (voucher-gated) for most mesh traffic** and **Tor-style cell relay
  as the default for internet traffic and for critical message flows / workspaces** (TURN/WebRTC
  relay is scoped to the browser tier, US7). It MUST enforce the qhstate-056 relay-**admission**
  decision at the forwarding hop (this tier owns the relay *mechanism*, 056 owns *who is admitted*),
  and MUST ensure a relay forwarding sealed traffic carries **ciphertext only** and cannot read the
  payload (R4; cycle-2 §2 `relay-forward` / `trusted-relay-node` / §5.2; Clarifications 2026-07-13).
- **FR-008**: The transport MUST achieve Sybil/DoS resistance **by curated-node gating** (the trusted
  / trustable-but-not-well-known node set as the authority), not by crypto-puzzles (cycle-2
  `sybil-dos-resistance` INSIGHT; Douceur impossibility).
- **FR-009**: The transport MUST provide **sealed routes** with **who-talks-to-whom metadata
  protection** (Veilid private-route model + I2P-style garlic bundling; no rigid fixed 3-hop
  circuit), such that no single relay on a sealed path learns both origin and final destination
  (R5/R7; cycle-2 §2 `sealed-route`/`metadata-protection`/`onion-garlic-layering`).
- **FR-010**: The transport MUST make **anonymity a selectable, per-context property** by adopting
  **Veilid's `SafetySelection`** model (tunable hop count / stability / sequencing; explicit Safe vs
  Unsafe), consumed via a per-invocation selection surfaced by 056 (R5; cycle-2 §2
  `selectable-anonymity` REFINE).
- **FR-010a**: For sealed-route relay selection the transport MUST use **stake-weighted node
  selection via the `057-yngenios-pocw-coin` (proof-of-cooperative-work) mechanism** as the standard,
  degrading to **Loopix-style semi-trusted-provider** selection as the fallback when the pocw-coin
  signal is unavailable — and MUST NOT fabricate a trust weighting when neither is available
  (cycle-2 §5.5; Clarifications 2026-07-13; depends on feature 057).
- **FR-011**: The transport MUST make **routing a CHOICE** between **`normal`** (more-encrypted but
  clear mesh routes) and **`sealed`** (encoded stable routes), resolve an unspecified selection to a
  declared **safe default**, and MUST **fail closed** — never silently downgrade a requested seal to
  a clear route (R8).
- **FR-012**: The transport MUST reach **known trusted destinations** inside the mesh without clearnet
  egress (R3), and MUST provide **selectable clearnet exit through curated trusted gates** by
  extending olamnit's **default-deny `EgressService`** — never using volunteer exit nodes (D5;
  cycle-2 §4 D5 gap CONFIRMED hard).
- **FR-013**: The trusted-gate exit MUST enforce an **exit-abuse policy** (BUILD-NEW; no corpus
  reference) and MUST refuse — observably — any egress that violates it; egress not explicitly
  elected/authorized MUST be **denied by default** (D5).
- **FR-014**: The transport MUST provide a **distinct browser/edge tier** — **WebRTC datachannel**
  (full ICE/STUN/TURN) for symmetric browser P2P + a **WebTransport uplink** to a YNET gateway (with
  relay fallback) — and this tier MUST NOT attempt a native UDP socket or native consolidated-QUIC
  path (cycle-2 §3a; MsQuic cannot target the browser).
- **FR-015**: The system MUST treat the browser/edge tier as a **separate transport implementation**
  from the native leaf (cycle-2 §3b: Gleam JS-target concurrency model is incompatible → no single
  cross-tier binary; expect ≥2 implementations sharing one design owner).
- **FR-016**: The transport MUST provide a **leaf/edge mode** that lets a leaf **use relays to punch
  out for its own traffic** while exposing the enforcement **hook** by which a leaf **never forwards
  third-party transit** — the leaf-never-relays *policy* is declared and owned by 056; this tier
  makes it enforceable at the forwarding path (R4/R9; cycle-2 §2 `leaf-client-mode` EXTEND).
- **FR-017**: The transport MUST provide **key management** with self-certified identity (nodeId =
  H(pubkey)) and MUST resolve **key→record** entries, but MUST return an explicit **"further resolver
  required"** for human-memorable **decentralized naming** and MUST NOT fabricate such a resolution
  (cycle-2 §2 `name-record`/`key->record-resolution`/`decentralized-naming`; §6).
- **FR-018**: When neither a direct punch nor any admitted relay yields a path, the transport MUST
  report a distinct **unreachable** reason (separate from any authorization outcome) and MUST NOT
  silently drop traffic (edge cases).
- **FR-019**: The transport MUST support the **dual-leaf migration** off GLPNET `QuicTransport`
  (transitional side-by-side → converge to the single YNET-owned transport) without producing an
  ambiguous or duplicated transport identity during the transition (D1).
- **FR-020**: Per-node keying MUST be authoritative in the YNET-owned transport, superseding GLPNET's
  club-wide shared certificate; the migration path off the shared cert MUST be explicit and
  authorized (D1; carried tension from `decisions-D1-D6.md` §"Still UNDECIDED").
- **FR-021**: The transport MUST extend (not duplicate) olamnit's **DSDV `DistanceVectorRouter` +
  durable `MeshRelayRoute`** from LAN-only into the NAT-piercing internet overlay as the routing
  substrate the BUILD-NEW overlay sits above (D3).
- **FR-022**: The transport MUST reuse (not re-invent) existing yngenios capability: olamnit crypto
  envelope / session / egress / DSDV mesh, GLPNET QUIC seams, and consume — not implement — the
  qhstate service-embed, macaroon-admission, and durable-mailbox tiers (R10 de-dup mandate).
- **FR-023**: Every path establishment, relay selection/refusal, sealed-route setup, exit decision,
  and leaf-transit refusal MUST be **observable/auditable** (consistent with the yngenios append-only
  journal), so the consuming 056 service can attribute outcomes.
- **FR-024** *(hard invariant)*: This feature MUST NOT merge, fold, or duplicate the qhstate
  `ynet-service` (056) tier; the only coupling is capability provision/consumption and the enforcement
  of 056-owned decisions (admission, leaf policy) at this tier's mechanism points. Any service-embed,
  macaroon-admission, or durable-mailbox concern belongs to 056 (tier-boundary invariant; R10).

### Key Entities *(include if feature involves data)*

- **YNET link**: A consolidated QUIC connection between two nodes, keyed by Ed25519 node identity and
  sealed with AES-256-GCM; the unit over which all frames flow. Supersedes GLPNET `QuicTransport`.
- **Node identity / key**: An Ed25519 keypair; the public key IS the TLS identity and the basis of
  nodeId = H(pubkey) and self-certified records.
- **Path**: A route between two endpoints — **direct** (hole-punched) or **relayed** — with a
  declared type and a selected routing mode (normal|sealed) and anonymity level.
- **DHT record**: A self-certified, signed key→record entry (peer reachability / key→record
  resolution) stored and looked up over Kademlia.
- **Relay node**: A curated node that forwards ciphertext for others; selectable only when 056 has
  admitted it; never selectable for a leaf's third-party transit.
- **Sealed route**: A metadata-protecting private route (Veilid private-route + garlic bundling) with
  a SafetySelection-defined hop/stability/sequencing profile.
- **Trusted gate**: A curated clearnet-exit point extending olamnit `EgressService`, governed by the
  exit-abuse policy; the only authorized egress to the wider internet.
- **Browser/edge endpoint**: A WebRTC-datachannel + WebTransport-uplink participant on the distinct
  browser tier; never a native QUIC / UDP socket holder.
- **Leaf mode state**: The transport mode that uses relays for its own egress but exposes the
  never-relays-for-others enforcement hook that 056's policy binds to.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Two reachable nodes establish a YNET QUIC link in which each verified peer identity
  equals its node public key, and complete an end-to-end `connect → send → receive`; a key/identity
  mismatch is rejected **100%** of the time before any application frame (US1).
- **SC-002**: For the punchable NAT class, **≥ a defined success rate** of direct hole-punches
  succeed; for the non-punchable class, **100%** of connections fall back to a relay path with **zero
  lost pending frames** and a correctly reported path type (US2). [NEEDS CLARIFICATION: target direct
  hole-punch success rate per NAT class.]
- **SC-003**: A self-certified DHT record stored by one node is returned by an iterative lookup from
  an unrelated node, and **100%** of records whose signature does not match the claimed key are
  rejected (US3).
- **SC-004**: Across a matrix of relay offers, **only** nodes admitted by the 056 decision are
  selected as forwarding hops (**0%** non-admitted or revoked selected), and a relay forwarding
  sealed traffic can decrypt **0%** of the payloads it forwards (US4).
- **SC-005**: On a sealed path, **no single relay** learns both origin and final destination
  (measured over the path set), and a requested seal is **never** silently downgraded to a clear
  route (**0** downgrade events) (US5).
- **SC-006**: **100%** of caller-declared routing-mode/anonymity selections map to concrete,
  observable path properties (hop count / stability), and every unspecified selection resolves to the
  declared safe default (US5, FR-010/FR-011).
- **SC-007**: Known-internal-destination traffic egresses to clearnet **0%** of the time; elected
  clearnet traffic egresses **only** via curated trusted gates (**0%** via volunteer/unknown exits);
  un-elected/unauthorized egress is denied by default **100%** of the time (US6).
- **SC-008**: The browser/edge tier carries frames peer-to-peer (WebRTC) and to native peers (via
  WebTransport/gateway) while attempting a native UDP/MsQuic path **0%** of the time (US7).
- **SC-009**: A leaf-mode node punches out for its **own** traffic via relays while refusing **100%**
  of third-party transit offers at the forwarding hook (US8, FR-016).
- **SC-010**: When no path (direct or relayed) is available, **100%** of attempts return a distinct
  unreachable reason and **0%** silently drop traffic (FR-018).
- **SC-011**: A review of the delivered surface shows **no** service-embed, macaroon-admission, or
  durable-mailbox implementation in this feature — all are consumed from / enforced on behalf of 056
  (FR-024; tier-boundary invariant).

## Assumptions

- **GLPNET native QUIC (feature 050 `050-glp-native-quic-link`, shipped v2026.07.13.1)** is present
  on `develop` as the **migration source** this feature harvests and supersedes (D1). This feature is
  created off the updated `develop` that already contains 050.
- **olamnit** supplies the reused substrate: Ed25519 amulet crypto-envelope (AES-256-GCM sealed
  links), DSDV `DistanceVectorRouter` + durable `MeshRelayRoute`, and default-deny `EgressService`.
  This tier extends and hardens them (H2/H3) rather than rebuilding (D2/D3/D5; R10).
- **qhstate 056 `ynet-service`** consumes this transport via a first-class `ICapability` and owns the
  service embed, the macaroon verify-before-act gate, the trusted-relay **admission** decision, the
  leaf-never-relays **policy**, and the durable exactly-once mailbox. This feature provides the
  mechanisms those decisions bind to; until 056 lands, admission/policy inputs are stubbed in tests.
- The curated set of trusted / trustable-but-not-well-known yngenios nodes is administered elsewhere
  and is the authority backing Sybil/DoS resistance-by-gating (cycle-2 `sybil-dos-resistance`).
- "mstack" here means the internal yngenios/Ingenious NATO-DIANA narrative corpus consumed into
  qhstate specs 034/035 — **not** the unrelated on-disk open-source "gstack" toolkit.
- The **mechanism-divergent choices** handed to TASK 3 (cycle-2 §5) are the cycle-2 analog of D1–D6.
  Four were resolved at `/bk-clarify` (see Clarifications 2026-07-13): relay-forward (hybrid
  circuit-relay-v2 + Tor-cell by traffic class), DHT ownership (embedded S-Kademlia), rendezvous
  (DHT-address + hidden-service for internet circuits), and mix trust (057-pocw-coin stake-weighted +
  Loopix fallback). Crypto-envelope is decided by D2 (FR-003). The residual operational unknowns
  (hole-punch success-rate target SC-002, exit-abuse policy content US6, relay revocation semantics,
  per-node-keying migration sequencing) are deferred to `/bk-plan`, not silently defaulted.
- **Feature `057-yngenios-pocw-coin`** (proof-of-cooperative-work coin) provides the stake-weighting
  signal used for standard sealed-route relay/node selection (FR-010a). This feature depends on 057
  for the standard path and degrades to the Loopix-style semi-trusted-provider fallback when the
  pocw-coin signal is unavailable; until 057 lands, the fallback path is exercised in tests.

## Out of Scope *(owned by qhstate 056 `ynet-service`, or honestly deferred)*

- **Service embed** as a first-class qhstate `Machine`/`ICapability.Invoke`, the **verify-before-act
  macaroon gate**, the trusted-relay **admission decision**, the **leaf-never-relays policy**
  declaration, and **durable exactly-once messaging** (qhstate mailbox) — all owned by 056; this tier
  provides/enforces the mechanisms, it does not own the policy or the embed.
- The **full mstack (diana/nato) domain-resolution** resolver and **human-memorable decentralized
  naming** — unsolved in the external corpus (cycle-2 §6); the transport serves only self-certified
  key→record resolution and names the further resolver required.
- **Mobile background / battery-budget scheduling policy** — an honestly-open BUILD-NEW gap; the
  external corpus reference was a mislabeled file (cycle-2 §6 corpus-integrity defect) and Veilid has
  zero battery awareness, so this MUST be revisited once a real mobile-P2P energy reference is fetched.

## Traceability

- **Decisions**: **D1 (consolidated YNET-owned QUIC superset — the spine of this feature; GLPNET
  `QuicTransport` = migration source)**, D2 (olamnit crypto baseline + H2/H3 harden — owned here),
  D3 (extend olamnit DSDV/`MeshRelayRoute` to the internet — owned here), D4 (qhstate embed —
  consumed), D5 (extend olamnit `EgressService` → trusted-gate exit — owned here), D6 (naming +
  metadata-protection + sealed-route + selectable-anonymity in the overlay — owned here; leaf extends
  olamnit) — `decisions-D1-D6.md`.
- **External cross-verification**: `curator_report_cycle2.md` run `20260712T223008Z-c2a2` — D1
  REFINE (QUIC a choice not a necessity; absorb iroh key-as-TLS-identity + in-handshake NAT
  traversal; MsQuic-can't-browser flag), nat-holepunch gap CONFIRMED with strong references
  (ICE/DCUtR/iroh), trusted-relay-node CONFIRMED BUILD-NEW (admission ≠ eligibility),
  selectable-anonymity REFINE (adopt Veilid SafetySelection), D5 exit gap CONFIRMED hard (no
  reference), Sybil-by-gating INSIGHT, browser tier RESOLVED (distinct WebRTC/WebTransport tier), 5
  mechanism-choices (§5) carried as `[NEEDS CLARIFICATION]`.
- **Subject brief**: `ynet-subject-brief.md` R1–R10 (R2 hole-punch, R3 known links, R4 relay via
  trustable nodes, R5 selectable anonymous exit, R6 beyond-the-punch reach, R7 waylet-seal over
  untrusted relays, R8 routing-is-a-choice, R9 mstack slice + leaf tier, R10 de-dup mandate).
- **Sibling feature**: qhstate **056 `ynet-service`** — the service-embed tier that consumes this
  transport and owns the admission/policy decisions this tier enforces.
