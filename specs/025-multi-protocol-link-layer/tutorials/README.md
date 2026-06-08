---
title: "Transport Tutorials — Index (feature 025 multi-protocol link layer)"
subtitle: "Plan-stage, PRE-IMPLEMENTATION. The link primitives are PROPOSED (pending Gabi's language-authority approval) and NOT YET IMPLEMENTED; every tutorial's GLP is ILLUSTRATIVE and every test is SPEC-LEVEL."
date: "2026-06-06"
status: "PROPOSED / spec-level. Nothing here is runnable yet. Each tutorial becomes runnable only once the PROPOSED base link primitives + the reliability sublayer + the named transport leaf land. No runnable test code is written against non-existent primitives."
---

# 0. What this index is

This is the index of the **per-transport tutorials** for the multi-protocol peer-to-peer
link layer (feature 025). Each tutorial takes ONE transport scheme, grounds it in a
realistic real-world deployment + the protocol's own RFC/spec semantics, shows the
**ILLUSTRATIVE** GLP that rides the PROPOSED base link primitives over that transport
(hand-checked for SRSW and writer/reader modes), and specifies the **SPEC-LEVEL** unit
and integration tests (scenario + exemplar GLP + observable outcome + pass/fail oracle)
that become runnable once the primitives are implemented.

**Hard framing (carried from every tutorial and the DESIGN-DOSSIER):**

- The nine base link primitives — `link_setup`, `server_listener`, `client_connector`,
  `request_link`, `accept_link`, `link_send`, `link_recv`, `link_monitor`, `link_close` —
  are **PROPOSED, pending language-authority approval, NOT YET IMPLEMENTED**
  (DESIGN-DOSSIER §2). All exemplar GLP is **ILLUSTRATIVE**; all tests are **SPEC-LEVEL**.
- **The base link is ALWAYS peer-to-peer to the IMMEDIATE peer.** Any broker/relay (an
  MQTT broker, a server-mediated hop) is at ANOTHER level and is **OUT OF SCOPE** here
  (DESIGN-DOSSIER §6; the MQTT corrected-framing ruling, §1).
- **GLP semantics are preserved exactly**: SRSW (one reader / one writer per variable per
  clause, never relaxed by a flag), writer-MGU (binds only writers), three-valued
  unification (an un-arrived remote value behaves as an unbound local reader ⇒ **Suspend**,
  never a spurious **Fail**), suspend-on-reader / reactivate-on-bind, bind-once, per-link
  FIFO, three-phase HEAD→GUARD→BODY. GLP is **not** Prolog — writer-mode outputs are built
  in clause **heads**, never via `=` in a body.
- **The scheme is the only thing that changes** across tutorials. Switching `"https"` →
  `"wss"` / `"mqtt"` / `"coap"` / `"ble-l2cap"` changes ONLY the `Scheme` in `LinkId`; the
  role-parameterized GLP program above the seam is unchanged (FR-006/FR-013; DESIGN-DOSSIER §5).

Read these first for the design these tutorials conform to:
[`../DESIGN-DOSSIER.md`](../DESIGN-DOSSIER.md), [`../spec.md`](../spec.md), and the contracts
[`../contracts/link-primitives.md`](../contracts/link-primitives.md),
[`../contracts/guards.md`](../contracts/guards.md),
[`../contracts/architecture-context.md`](../contracts/architecture-context.md),
[`../contracts/example-http-link.md`](../contracts/example-http-link.md).
The shared test rig every integration test below targets is
[`../tests/integration-harness-design.md`](../tests/integration-harness-design.md);
the consolidated coverage matrix is [`../tests/test-matrix.md`](../tests/test-matrix.md).

---

# 1. The tutorial index

All entries are **spec-level / PROPOSED**. "Unit" = single-REPL Section-A/B/C tests (no
transport; pin the GLP-surface contract). "Integration" = cross-instance tests over the
shared harness. The integration-test ID scheme differs slightly per tutorial (the leaf
author's local convention) and is given in the rightmost column so the matrix can be read
back to the file.

| Scheme(s) | Real-world scenario (web-grounded) | Status | Tutorial file | Unit / Integration test IDs |
|---|---|---|---|---|
| **loopback** + **file** | (1) **loopback** — the deterministic two-instance producer/consumer split that is the SC-001 byte-identical headline, built like a FoundationDB-style deterministic-simulation harness (single run seed; seeded drop/reorder/duplicate). (2) **file** — an append-only / write-ahead-log / event-sourcing channel: producer appends length-prefixed frames; an offline consumer replays them in order; graceful close = an fsync'd end-of-log marker (`[]`), abrupt close = a truncated log (EOF-without-marker → `tempFail`/`permFail`). | spec-level / PROPOSED | [`file-loopback.md`](file-loopback.md) | Unit `unit-A-01..07`, `unit-B-01..03`, `unit-C-01..03` (13); Integration `unit-INT-01..08` (8) |
| **ws** / **wss** | A live **collaborative-editing session** (Figma / Google-Docs / Miro class) and the equivalent real-time **telemetry dashboard**: each side streams small messages while the other is pushed updates instantly with no polling. `Link(In,Out)` maps straight onto the two WebSocket directions; the listener pushes B→A frames natively. CRDT/OT conflict resolution and all-participant fan-out are above the seam and out of scope. | spec-level / PROPOSED | [`websocket.md`](websocket.md) | Unit `A-WS-1..7`, `B-WS-1..3`, `C-WS-1..3` (13); Integration `IT-WS-1..7` (7) |
| **https** (HTTP/2 + mTLS) | **Cross-organization B2B data exchange with mutual authentication**: a payment-initiation fintech (org A) streams transaction/confirmation records to an account-holding bank (org B) over the public Internet under Open Banking / FAPI 2.0 / PSD3 (HIPAA as an analogue), where both parties must cryptographically prove identity at the channel level. mTLS strict mode (X.509 client cert required+verified both ways). Records ride one long-lived bidirectional HTTP/2 stream; the reverse direction also carries the bounded-pipe credit/demand stream. | spec-level / PROPOSED | [`https-http2-mtls.md`](https-http2-mtls.md) | Unit `A-https-01..06`, `B-https-01..03`, `C-https-01..03` (12); Integration `I-https-01..08` (8) |
| **mqtt** / **mqtts** | A **device ↔ gateway command/telemetry link** — the immediate peer-to-peer hop in an edge-gateway IoT deployment (ThingsBoard / Azure IoT Edge / AWS SiteWise / GCP style). A = constrained field device, B = gateway (the immediate peer). The device streams telemetry out and reads commands in over one bilateral `Link(In,Out)`. CORRECTED FRAMING: the base link is P2P to the immediate peer only; any MQTT-broker fan-out to the cloud is a separate higher level, explicitly OUT OF SCOPE; the seq/dedup sublayer enforces per-link FIFO + at-least-once end-to-end (FR-023), not the broker. | spec-level / PROPOSED | [`mqtt.md`](mqtt.md) | Unit `A-MQTT-1..6`, `B-MQTT-1..3`, `C-MQTT-1..3` (12); Integration `IT-MQTT-1..7` (7) |
| **coap** / **coaps** | A **battery-constrained IoT sensor mesh reporting to a collector over CoAP/UDP** (RFC 7252): a sensor (producer / CoAP client / writer) pushes a stream of readings to a collector (consumer / CoAP server / reader) on one bilateral link; A→B via CON PUT/POST, B→A back-channel via OBSERVE (RFC 7641) carrying data + credits + the fault monitor. Grounded in the published Cairngorm (Scotland) 6LoWPAN+RPL+CoAP-over-UDP deployment. Small ~1 KB MTU drives blockwise fragment/reassemble (RFC 7959); DTLS (`coaps`, port 5684) is the secure variant. | spec-level / PROPOSED | [`coap.md`](coap.md) | Unit `A1..A11`, `B1..B3`, `C1..C3` (17); Integration `IT-COAP-1..6` (6) |
| **ble-l2cap** | **Phone (central/app) ↔ battery-constrained wearable peripheral** (fitness band / smartwatch) bulk data sync over a Bluetooth LE **L2CAP Connection-Oriented Channel (CoC)** in LE Credit-Based Flow-Control Mode: the wearable streams up logged high-frequency sensor data while the phone streams down a firmware/config image — a TCP-like reliable, ordered, bilateral full-duplex stream with native per-direction credit flow control (SDUs up to 64 KB). Grounded in Nordic's Object Transfer Service (OTS) over an L2CAP CoC. Android (API 29+) is the accepted single platform; BIS true-multi-reader is OUT OF MVP (open SRSW-tension item). | spec-level / PROPOSED | [`ble-l2cap.md`](ble-l2cap.md) | Unit `A-BLE-1..10`, `B-BLE-1..3`, `C-BLE-1..3` (16); Integration `I-BLE-1..8` (8) |

**Totals (spec-level):** 6 transport tutorials → 83 unit-test specs + 44 integration-test
specs = **127 leaf test specs**, on top of the shared harness's 13 substrate test specs
(T-01..T-13). See the matrix for SC coverage and gaps.

---

# 2. The shared substrate (read alongside every tutorial)

Every integration test above targets the **same** spec-level harness interface
(`StartInstances` / `OpenLink` / `Inject` / `Drive` / `Capture` / `AssertEquiv` /
`CloseLink` / `Stop`), with the C# reference shape and a behaviour-identical Dart mirror,
all randomness from one run seed for deterministic replay. The harness's deterministic,
hermetic **loopback** transport is the SC-001 "simplest available transport" and is where
all **wire faults** (drop / reorder / duplicate / delay / partition) are injected — so each
real leaf (ws/coap/...) only has to prove **bind-reactivation feasibility (SC-003)** +
graceful/abrupt close on one platform (Windows OR Android, T4 / FR-063), keeping the
per-leaf cost cheap. The harness is itself spec-level and is a documented `SKIP` (proposed
Section **R** of `test/run_all_tests.sh`) until the primitives land, so the baseline stays
green (FR-067 / SC-017). Full design:
[`../tests/integration-harness-design.md`](../tests/integration-harness-design.md).

---

# 3. How to read a tutorial

Each file is organized identically: (1) scenario + protocol-semantics grounding with cited
sources; (2) how the protocol maps onto the uniform link seam (open / send-bytes /
recv-bytes / close + fault) and the B→A back-channel; (3) the ILLUSTRATIVE GLP (with
`procedure` + type declarations and a hand-SRSW check on every clause); (4) UNIT test specs
(Section A runtime / B positive type-check / C negative type-check); (5) INTEGRATION test
specs over the harness; (6) the permanent regression set tied to the baseline gate; (7)
transport-specific open items.

The **single common worked example** all tutorials specialize is the producer/consumer
split in [`../contracts/example-http-link.md`](../contracts/example-http-link.md) /
DESIGN-DOSSIER §5 — one role-parameterized program, the shared variable replaced by a link.

---

# 4. Sources

Per-transport RFC/spec and real-world-scenario citations live inside each tutorial's own
Sources section. Two cross-cutting groundings for this index:

- Multi-protocol communication-layer abstraction and the per-protocol fit (WebSocket =
  low-latency full-duplex; MQTT/CoAP = low-bandwidth/intermittent; CoAP for <64 KB-RAM
  6LoWPAN/Thread mesh; BLE short-range low-power) — and the explicit caution that a gateway
  that translates between protocols is a *separate level* (consistent with this feature's
  "broker is at another level, out of scope" framing):
  - Designing multi-protocol communication layers — https://palospublishing.com/designing-multi-protocol-communication-layers/
  - MQTT vs CoAP vs HTTP vs WebSocket (IoT) — https://www.agilesoftlabs.com/blog/2026/04/mqtt-vs-coap-vs-http-vs-websocket-iot
  - Transport/application-layer protocols for IoT (review) — https://www.mdpi.com/2227-7080/13/12/583
- Deterministic-simulation-testing discipline that the hermetic loopback transport and the
  seeded fault taxonomy follow (single seeded PRNG everywhere; control concurrency, time,
  randomness, failure injection; FoundationDB lineage; reproducible drop/partition/latency):
  - Antithesis — Deterministic simulation testing — https://antithesis.com/docs/resources/deterministic_simulation_testing/
  - Pierre Zemb — Testing: prevention vs discovery — https://pierrezemb.fr/posts/testing-prevention-vs-discovery/
