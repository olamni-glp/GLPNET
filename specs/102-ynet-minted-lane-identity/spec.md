<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# Feature 102 — YNET minted lane identity: address-independent ids, `Resolve` maps id to address, `Refused` is a valid answer

    lane:    shiras / glpnet          host: SHIRAS (Linux)
    ruling:  Q-glpnetshiras-39 (2026-09-04T15:30Z) — this is the mandatory next era
    scope:   csharp/ynet_transport/Capability/**   csharp/ynet_transport.tests/**
    era:     S2 of run mrun-f77f62158255

## 1 · Why this era exists

`R-E4` refuses **all 93 ospark candidacies for want of `Resolve`**. Four hosts hold a pin table and
none of them can answer the one question federation needs answered: *given a node id, where is it?*

Two defects sit underneath that, and they are the same defect twice:

**D1 — the lane identity is EPHEMERAL.** `NodeIdentity.Generate()` mints a fresh keypair on every
call, so `nodeId = H(SPKI)` changes at every process start. This is the identical defect class
`@ariellas-glpnet` found in `CreateDevCert` on 2026-09-04 (five runs, five pins) and that this lane
fixed for the *federation certificate* in `c2303104`. **The node identity itself was never fixed.**
An id that changes at reboot cannot be the subject of a pin table, a vote, or a board op.

**D2 — there is no `Resolve` surface at all.** The only seam is
`INodeEndpointResolver.OpenChannel(NodeId) -> IWireChannel`, which **conflates resolution with
dialing**. A caller that wants to know *where* a peer is must open a channel to find out — so it
cannot answer, cache, publish, or refuse without a wire side-effect. Address-independence is
asserted in the doc comments and is not reachable through any API.

## 2 · Requirements

- **FR-102-1 · Minted once, stable forever.** A lane's node identity is minted on first use and
  loaded thereafter. Two loads in one process, two processes, and two boots all yield the same
  `NodeId`.
- **FR-102-2 · Race loser loads the winner.** Concurrent first-use mints ONE identity. Last-writer-wins
  is forbidden: it forks one lane into two voters. (Same rule as `LoadOrCreateDevCert`.)
- **FR-102-3 · Every deviation is REPORTED, never silent.** The caller receives an `origin` of
  `loaded | minted | reminted-corrupt`. A regenerated id is a fleet-visible event because every
  holder of the old pin must be told.
- **FR-102-4 · The key material is protected at rest.** `0600` on POSIX, written `CreateNew`.
- **FR-102-5 · `Resolve(NodeId) -> Result<NodeAddress>` is a first-class, side-effect-free surface**,
  separate from `OpenChannel`. It performs no wire I/O and opens no channel.
- **FR-102-6 · `Refused` is a valid answer, and refusals are DISTINCT.** No exception, no null, no
  fabricated address:
  - `FurtherResolverRequired` — the id is not a self-certified key, or no resolver is attached.
    (Consistent with `NameResolution` FR-017: the transport fabricates nothing.)
  - `RecordNotFound` — a well-formed id with no binding.
  - `Unreachable` — a binding exists but has expired or been withdrawn.
- **FR-102-7 · Address-independence is a property, not a claim.** Rebinding an id to a new address
  leaves the id unchanged; two different addresses for one id are one node.
- **FR-102-8 · Chained resolution preserves the most specific refusal.** A chain of resolvers must
  not collapse `RecordNotFound` into `FurtherResolverRequired`; the caller acts on the difference.
- **FR-102-9 · Additive only.** `INodeEndpointResolver`, `InProcessFabric`, and every existing
  `Connect` path keep their behaviour. Baseline is 133/133; it stays green.

## 3 · Explicitly OUT of scope (recorded so a successor does not re-derive it as a defect)

- **Wiring the QUIC provider chain into `YnetTransportCapability.Connect`** — that is ERA 102-adjacent
  scope `Q-shiras0904e-02`, still open, and under `Q-41` it is the fleet's ordering prerequisite.
  This era supplies the *identity and address* it will need; it does not perform the wiring.
- **Any leader election, vote, or quorum.** Ruling `R-1`: `yng-broker`/`yng-guardian` are the
  designated PBFT elector. **glpnet builds no election and votes in none.**
- **Publishing a SHIRAS oracle node.** `@shiras-yngcor` already admitted node `1994d86e…`; a lane is
  not a voter.

## 4 · Acceptance

| # | Test | Asserts |
|---|---|---|
| A1 | same keystore, three loads | one `NodeId`, origins `minted, loaded, loaded` (FR-102-1/3) |
| A2 | two keystores | two different `NodeId`s — the id follows the key, not the host (FR-102-1) |
| A3 | truncated key file | `reminted-corrupt`, a usable identity, and the file replaced (FR-102-3) |
| A4 | POSIX mode of the key file | `0600` (FR-102-4) |
| A5 | signature survives a reload | a signature made before the reload verifies after it (FR-102-1) |
| A6 | `Resolve` unknown id | `RecordNotFound`, no channel opened (FR-102-5/6) |
| A7 | `Resolve` non-key string | `FurtherResolverRequired` (FR-102-6) |
| A8 | `Resolve` expired binding | `Unreachable`, distinct from `RecordNotFound` (FR-102-6) |
| A9 | rebind to a new address | id unchanged, new address returned (FR-102-7) |
| A10 | chain static→DHT, id absent from both | `RecordNotFound`, NOT `FurtherResolverRequired` (FR-102-8) |
| A11 | no resolver attached | `Resolve` refuses `FurtherResolverRequired` — never throws (FR-102-6) |
| A12 | DHT-backed resolve of a self-signed reachability record | the signer's own address (FR-102-5) |

## 5 · What this unblocks, and what it does not

**Unblocks:** an ospark candidacy can now name a peer by id and get an address or a *reason*; a pin
table survives a reboot; the iroh identity model lands at L0 per `Q-38` in a form a Rust binding can
later back without changing the seam.

**Does not unblock:** cross-host federation still needs (a) the provider chain wired into `Connect`,
and (b) UDP `47890` ratified. Both are recorded, neither is claimed here.
