<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# Plan — feature 102

## Approach

Two files of new production code and one seam addition. **Nothing existing changes behaviour.**

| # | change | why this shape |
|---|---|---|
| P1 | `NodeIdentity` → `sealed partial`, + `Capability/NodeIdentityKeystore.cs` | `LoadOrMint` needs the private constructor; a partial keeps the crypto file and the persistence file separately readable, and leaves `NodeIdentity.cs` a one-word diff |
| P2 | `Capability/NodeAddressResolver.cs` — `NodeAddress`, `INodeAddressResolver`, and three implementations | the pin table, the self-certified DHT record, and the merge of the two are three different trust stories; one class conflating them would have to pick one |
| P3 | `IYnetTransport.Resolve` as a **default interface method** | additive: an implementer outside this repo (yngenios vendors these tests) keeps compiling, and its inherited behaviour — refuse `FurtherResolverRequired` — is honest rather than a stub that lies |
| P4 | `YnetTransportCapability` optional `addresses` ctor arg | matches the existing `dht:`/`relay:` optional-slice shape exactly |
| P5 | `Capability/QuicNodeEndpointResolver.cs` | the `INodeEndpointResolver` swap its own doc comment predicted; unblocked *because* P2 exists |
| P6 | `glp_quic_probe` prints the lane node id | the cross-process measurement, in the tool the fleet already runs |

## Decisions worth recording

**Persist PKCS#8 DER, not a per-algorithm format.** `PrivateKeyFactory.CreateKey` dispatches on the
parsed key type, so Ed25519 and P-256 identities load over one path and the algorithm in force
survives the round trip. A host that fell back to P-256 must NOT silently change identity when an
Ed25519 provider later appears — that would be this era's own defect, re-committed.

**Resolve before checking QUIC support in `OpenChannel`.** An unknown id is the caller's error and is
true whether or not this host has QUIC; the nearer cause is the actionable one. `Resolve` is
side-effect free, so there is no cost to asking it first.

**A DNS name is refused, not looked up.** FR-017 already says a human-memorable name is not this
tier's to resolve. Silently trusting the host resolver would put a peer's identity→address binding
in DNS, outside the self-certified overlay — a trust decision disguised as a convenience.

**The QUIC listener certificate stays ephemeral.** TLS here is transport confidentiality only;
identity is verified app-layer against `nodeId = H(pubkey)`. Pinning it would move the identity
decision to the wrong layer. This is the one place in this era where "persist the key" is the WRONG
answer, and it is worth the sentence.

## Risks

- **Duplicating another lane's work.** `@shiras-qhstate` owns the QUIC *provider chain*;
  `ynet_transport/Capability/**` is this lane's. P5 composes theirs, re-implements none of it.
- **Over-claiming federation.** Two hosts have never exchanged a frame. Recorded in spec §5 and in
  the 00:40Z broadcast §6, both times as a NOT.
