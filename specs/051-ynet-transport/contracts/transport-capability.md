# Contract: YNET Transport `ICapability` (consumed by qhstate 056)

The transport tier presents exactly one first-class capability. This contract is runtime-agnostic
(C#/.NET, Gleam/BEAM, JS/WASM all implement it); it is the ONLY coupling to 056 (FR-004/FR-024).
056 owns the embed, macaroon gate, admission decision, and leaf policy — this contract is what those
decisions bind to.

## Capability surface

```
capability YnetTransport {
  // --- core link (US1/US2) ---
  connect(peer_node_id, RoutingSelection) -> LinkHandle | Refusal(reason)
  send(LinkHandle, frame) -> Ack | Refusal(reason)
  receive(LinkHandle) -> frame | Closed
  close(LinkHandle) -> Done                 // graceful, close-after-collect (050 discipline)

  // --- selection surfaced by 056 (US5/US6) ---
  // RoutingSelection { routing_mode: normal|sealed, anonymity_level, exit: internal|clearnet_gate }
  // unspecified -> declared safe default; sealed never silently downgraded (FR-011)

  // --- discovery (US3) ---
  dht_store(SignedRecord) -> Stored | Refusal
  dht_lookup(key) -> SignedRecord | NotFound | FurtherResolverRequired   // FR-017 naming

  // --- relay mechanism, admission enforced from 056 (US4) ---
  offer_relay(relay_node_id, AdmissionProof) -> Admitted | Rejected(reason)
  // AdmissionProof is 056's macaroon decision; this tier enforces, does not decide

  // --- leaf mode (US8) ---
  set_mode(full|leaf) -> Ok
  // in leaf mode, transit-forward requests are refused at the hook (056 policy binds here)

  // --- introspection (FR-023 auditability) ---
  path_info(LinkHandle) -> { path_type, routing_mode, anonymity_level, relay_hops }
}
```

## Refusal reasons (distinct, observable — FR-018/FR-023)

| reason | when |
|---|---|
| `identity_mismatch` | peer key ≠ handshake identity (FR-002) |
| `unreachable` | no direct punch and no admitted relay (FR-018) |
| `authorized_but_unreachable` | auth ok, path could not establish / was torn down (R3) |
| `seal_unavailable` | sealed route requested, only clear paths available — fail closed (FR-011) |
| `relay_not_admitted` | 056 has not admitted this relay (FR-007) |
| `leaf_transit_refused` | leaf asked to forward third-party transit (FR-016) |
| `egress_denied` | exit-abuse policy / default-deny (FR-012/FR-013) |
| `further_resolver_required` | name beyond self-certified key→record (FR-017) |
| `record_not_found` | `dht_lookup` of a valid self-certified key with no live record (the NotFound outcome) |
| `record_rejected` | `dht_store` of a record that fails self-certification — tamper / key-spoof / expiry (FR-006) |
| `transport_unsupported` | native QUIC unavailable — refuse, never downgrade (050 gate) |

## Invariants (tested at contract tier)

1. Every refusal carries exactly one distinct reason above; no silent drops (FR-018).
2. A refused `connect`/`send` produces **zero** wire side-effects (no packet, DHT write, relay
   selection).
3. `sealed` selection is never fulfilled by a clear route (FR-011).
4. `offer_relay` admits **only** on a valid 056 AdmissionProof; revocation removes it (FR-007/FR-008).
5. In `leaf` mode, `transit-forward` always returns `leaf_transit_refused`; the node's own
   originated/terminated traffic is unaffected (FR-016).
6. This surface contains **no** service-embed, macaroon-minting, admission-deciding, or
   durable-mailbox operation — those belong to 056 (FR-024; SC-011).
