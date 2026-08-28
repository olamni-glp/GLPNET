# Quickstart: YNET `ynet-transport`

The fastest path to a working slice and how to validate it. MVP = US1 + US2 (native leaf +
hole-punch); the overlay and browser tiers layer on afterward.

## Prerequisites
- .NET 10 SDK (`net10.0`); MsQuic available (native leaf gates on `QuicTransport.IsSupported`).
- Erlang/Gleam on PATH (BEAM tier); a JS toolchain (browser tier) — only for those tiers.
- olamnit substrate reachable (crypto-envelope / DSDV / EgressService) — reused, hardened H2/H3.
- Repo PGLite working-data cluster at `pgdb/` (records + admission cache).

## MVP walkthrough (US1 → US2)
1. **Two-node direct link (US1)**: start two `ynet_transport` nodes on a reachable segment, each with
   an Ed25519 node key. Node A `connect(B.node_id)`. Assert B's verified TLS identity == B's pubkey;
   complete `connect → send → receive`. A key/identity mismatch is refused pre-frame
   (`identity_mismatch`).
2. **Punch across NAT (US2)**: place both nodes behind simulated cone NATs; rendezvous over the
   embedded DHT; assert a direct hole-punched path for the punchable class (≥90% within 5 s), and a
   clean relay fallback with zero frame loss for the symmetric class (`path_info.path_type` reports
   which).
3. **Capability handoff**: from a 056 stub, resolve the `YnetTransport` capability and drive the same
   `connect/send/receive` — confirming the tier boundary (no embed logic here).

## Validate
- **Native**: `dotnet test csharp/ynet_transport.tests` (contract + integration + unit).
- **BEAM tier**: `gleam test` under `gleam_quic/`.
- **Browser tier**: JS test runner under `ynet_browser/` (WebRTC datachannel + WebTransport;
  assert **no** native UDP/QUIC attempt).
- **GLP demo**: positive-load check `test/run_all_tests.sh` §B (SRSW-clean, Constitution III).
- **Migration**: `test_migration_*_single_head.py` stays single-head (Constitution VI-a).

## Tier-boundary self-check (SC-011)
Grep the delivered surface: it must contain **no** service-embed, macaroon-minting,
admission-deciding, or durable-mailbox implementation — all consumed from / enforced on behalf of
qhstate 056 (FR-024).

## What is NOT here (owned by 056 / deferred)
Service embed, macaroon gate, relay-admission decision, leaf-never-relays policy, durable mailbox
(→ 056). Human-memorable naming + mobile battery-budget (→ honestly deferred; roadmap).
