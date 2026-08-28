# Phase 1 Data Model: YNET `ynet-transport`

Entities derived from spec Key Entities + FRs. Persisted entities live in the repo PGLite
working-data cluster (Constitution VI-b) via one additive, idempotent, single-head migration
(VI-a). Wire frames are **not** persisted.

## Node identity / key
- **Fields**: `node_id` (= H(pubkey), primary), `ed25519_pubkey`, `created_at`, `key_state`
  (`active` | `migrating` | `retired`), `shared_cert_ref?` (transitional, FR-020).
- **Rules**: `node_id` MUST equal H(`ed25519_pubkey`) (self-certification). The pubkey IS the TLS
  identity (FR-002). `key_state` transitions `migrating → active` only via an operator-signed
  migration record (research.md R4); never `active → migrating` (monotonic cutover).

## YNET link (transient, not persisted)
- **Fields**: `local_node_id`, `peer_node_id`, `path_ref`, `session_seal` (AES-256-GCM, H2/H3
  hardened), `established_at`, `link_kind` (`direct` | `relayed`).
- **Rules**: refuse if peer's presented key ≠ handshake identity before any application frame
  (FR-002). Seal every session (FR-003).

## Path
- **Fields**: `path_id`, `src`, `dst`, `path_type` (`direct` | `relayed`), `routing_mode`
  (`normal` | `sealed`), `anonymity_level`, `relay_hops[]?`, `state`
  (`establishing` | `live` | `tearing_down` | `unreachable`).
- **Rules**: `sealed` optimizes for privacy, `normal` for latency (research.md R6). Never silently
  downgrade `sealed → normal` (FR-011, fail-closed). Unspecified selection → declared safe default.

## DHT record (persisted)
- **Fields**: `key` (DHT key), `record_kind` (`reachability` | `key_to_record`), `payload`,
  `signer_node_id`, `signature`, `stored_at`, `expires_at`.
- **Rules**: self-certified — `signature` MUST verify against `signer_node_id`'s pubkey; a record
  whose signature fails is rejected regardless of the serving DHT hop (FR-006). Embedded S-Kademlia,
  curated overlay — never a public DHT.

## Relay node / admission cache (persisted)
- **Fields**: `relay_node_id`, `admission_macaroon_ref` (056-owned decision), `relay_mechanism`
  (`circuit_relay_v2` | `tor_cell`), `traffic_class` (`mesh` | `internet` | `critical`),
  `admitted` (bool), `revoked_at?`.
- **Rules**: selectable only when 056 has admitted it (FR-007). `circuit_relay_v2` for `mesh`;
  `tor_cell` default for `internet`/`critical` (clarify). Revocation: block new selection
  immediately, tear down live paths at next frame boundary (research.md R3). Never selectable for a
  leaf's third-party transit (FR-016). Sybil resistance by curated-node gating (FR-008).

## Sealed route
- **Fields**: `route_id`, `hop_set[]`, `safety_selection` (`hop_count`, `stability`, `sequencing`,
  `safe|unsafe`), `mix_trust_source` (`pocw_057` | `loopix_fallback`).
- **Rules**: no fixed 3-hop (garlic bundling). No single hop learns both endpoints (FR-009).
  Node selection stake-weighted via 057-pocw-coin standard, Loopix fallback; fabricate nothing when
  neither available (FR-010a, research.md R6).

## Trusted gate + exit-abuse policy (persisted)
- **Fields**: `gate_id`, `allow_deny_list`, `rate_caps`, `egress_class_filters`,
  `policy_signature`, `operator_node_id`.
- **Rules**: only authorized egress path to clearnet; never a volunteer/unknown exit (FR-012).
  Default-deny; enforce allow/deny → rate caps → class filters in order (research.md R2, FR-013).

## Browser/edge endpoint (transient)
- **Fields**: `endpoint_id`, `tier` (`webrtc_datachannel` | `webtransport_uplink`),
  `gateway_ref?`, `relay_fallback?`.
- **Rules**: never opens a raw UDP socket or native QUIC path (FR-014). Separate implementation
  from the native leaf (FR-015).

## Leaf mode state (persisted per node)
- **Fields**: `node_id`, `mode` (`full` | `leaf`), `never_relays_hook_bound` (bool).
- **Rules**: a leaf uses relays for its own egress but the transport hook refuses third-party
  transit — the 056 leaf-never-relays policy binds here (FR-016). Asymmetry must be unambiguous.

## State transitions (Path)
```
establishing → live            (punch or relay path succeeds)
establishing → unreachable     (no direct + no admitted relay — FR-018)
live → tearing_down            (relay revoked / seal invalidated — next frame boundary)
tearing_down → unreachable     (graceful drain complete; surface authorized-but-unreachable)
```
