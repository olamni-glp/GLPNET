<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# Tasks — feature 102   (all complete; era S2)

| id | task | state | evidence |
|---|---|---|---|
| T01 | baseline the suite before touching anything | ✅ | 133/133 green, pre-change |
| T02 | `NodeIdentity` → partial | ✅ | `b5a9911b` |
| T03 | `LoadOrMint` + PKCS#8 keystore, 0600, CreateNew, race-loser-loads-winner | ✅ | `Capability/NodeIdentityKeystore.cs` |
| T04 | `NodeAddress` + `TryParse` | ✅ | 12 round-trip / malformed cases |
| T05 | `INodeAddressResolver` + static / DHT / chained | ✅ | `Capability/NodeAddressResolver.cs` |
| T06 | `Resolve` on the 056-facing interface, non-breaking | ✅ | default interface method |
| T07 | wire the optional slice into `YnetTransportCapability` | ✅ | matches the `dht:`/`relay:` shape |
| T08 | tests A1–A12 + race + traversal + positive control | ✅ | 35 tests, 168/168 |
| T09 | cross-PROCESS measurement, not an in-process assertion | ✅ | 3 probe runs, one id `76b66c25…`, mode `0600` |
| T10 | `QuicNodeEndpointResolver` — dial by id over a real wire | ✅ | `f60acbbf` |
| T11 | refusal-passthrough + bounded-dial + IP-literal-only tests | ✅ | 12 tests |
| T12 | end-to-end: two nodes connect BY ID over real QUIC, sealed frame across | ✅ | passed in 493 ms — a genuine handshake, not a fabric |
| T13 | full suite green | ✅ | **180/180**, `env -u LD_LIBRARY_PATH` |
| T14 | publish to BOTH coop roots, hashes verified | ✅ | 3 docs × 2 roots, sha256 identical |

## Deliberately not done (recorded, not deferred silently)

- **No leader election, no vote, no oracle node.** Ruling `R-1` / `Q-42`; five rival elections already
  exist over one shipped mechanism. glpnet builds none and votes in none.
- **No cross-host handshake.** UDP `47890` is unratified and no two hosts have exchanged a frame.
  A bound listener on each of four hosts is not a link between any two.
