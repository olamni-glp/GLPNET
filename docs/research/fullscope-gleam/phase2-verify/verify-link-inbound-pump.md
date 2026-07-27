<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-link-inbound-pump` (WP b3-c1-008, wave 2)

**Date**: 2026-07-23
**Method**: module-list diff (`glp_gleam/src/glp/link` vs `glp_runtime/lib/link/` + `csharp/glp_link/`) + source-verification + `specs/050-full-gleam-combined/tasks.md` 122-135 checkbox cross-check + runtime load/run of `programs/tests/link/bidi.glp`. In-suite: `loopback_test`/`tcp_test`/`frame_codec_test` (green in the 465 floor).
**Paired close**: `close-link-inbound-pump` (b3-c1-033, L) — **ACTIVATED** (US4 remainder T050-T058); coordinated with builder-2 `close-link-layer-glp-primitives` (b2-c2-008), `-sequence-dedup` (b2-c2-010), `-fault-decoration` (b2-c2-009).

Per the WP risk note, this is a **per-sublayer boundary**, not one batch verdict. The clean line is **T045-T049 delivered / T050-T058 open** (tasks.md 122-135).

## Verdict table

| # | detail_id | verdict | basis |
|---|-----------|---------|-------|
| 1 | `link-seam` | **DELIVERED** | `seam/{transport,endpoint,link_scheme,link_address,link_id,link_options,link_fault}.gleam` = T045 port of `i_link_transport`/`i_link_endpoint` (record-of-functions vtables, below-GLP host seam). tasks **T045 [X]**. In-suite `loopback_test`/`tcp_test` drive the seam directly, green. |
| 2 | `link-transport-seam` | **DELIVERED** (loopback+TCP) | Per-scheme transport seam + `transports/loopback.gleam` (T048) + `transports/tcp.gleam` (T049, real gen_tcp FFI, 4-byte BE length framing). tasks **T048/T049 [X]**. QUIC-WS (T055) open; the scheme→impl **transport registry** (`transport_registry`, part of T050) is **absent** (0 hits) — the two transports exist but the runtime registry that selects them is open. |
| 3 | `link-reliability` | **PARTIAL** | `reliability/{frame_codec,crc32}.gleam` (T046) DELIVERED + golden-parity-tested (T047, `frame_codec_test`). But the reliability **sublayer proper** is ABSENT: no `send_window` (windowing), `inbound_ordering`/`link_sequencer` (ordering), `fencing_registry` (fencing), `cycle_guard`, `link_reclaimer` (reclaim), `frame_reassembler`, `resource_snapshot` — all 0 hits (Dart/C# have all 11). Open as T050-T052. |
| 4 | `inbound-pump` | **ABSENT** | No `link_pump` module (`pump` grep = a `tcp.gleam` comment only). Dart `link_pump.dart` / C# `LinkPump.cs` unported. tasks **T050 [ ]**. |
| 5 | `link-acceptance` | **ABSENT** | No link-accept kernel (Dart `link_accept_kernel.dart` / C# `LinkAcceptKernel.cs` unported). The transports have host-level listen/accept, but the GLP link-accept primitive is not wired. tasks **T050 [ ]**. |
| 6 | `link-capability-gate` | **ABSENT** | No capability module (`capability` grep = a `scheduler.gleam` comment). C# `CapabilityGateRegistry`/`ICapabilityGate` unported. |
| 7 | `instance-network-join` | **ABSENT** | No network-join module; part of the open primitives + T051 distributed-unification path. |

## Evidence

### Module-list diff
- **Gleam (11 modules)**: seam ×7, reliability ×2 (`crc32`, `frame_codec`), transports ×2 (`loopback`, `tcp`).
- **Dart reference (39)**: **primitives ×18** (`link_pump`, `link_accept_kernel`, `link_send_kernel`, `link_establish`, `link_egress`, `link_handle`, `link_registry`, `link_runtime`, `transport_registry`, `link_{listen,monitor,request,setup,close,teardown}_kernel`, `link_faults`, `link_kernels`, `link_terms`), **reliability ×11** (adds `send_window`, `inbound_ordering`, `link_sequencer`, `fencing_registry`, `cycle_guard`, `link_reclaimer`, `frame_reassembler`, `resource_snapshot`, `frame_exception`), seam ×8, transports ×2. C# `glp_link/` mirrors it (+ `CapabilityGateRegistry`, `PayloadCodecRegistry`, `ICapabilityGate`, `IPayloadCodec`).
- ⇒ Gleam has delivered the **seam + loopback/TCP + frame-codec/CRC floor**; the **entire `primitives/` layer** and the **reliability sublayer above the frame codec** are absent.

### tasks.md 122-135 (US4) — checkbox state matches the code exactly
- **[X] T045** transport seam · **[X] T046** FrameCodec+CRC32 · **[X] T047** frame-codec parity tests · **[X] T048** loopback · **[X] T049** TCP.
- **[ ] T050** link primitives (link_send/recv, establish/listen/accept/request/setup/close/monitor, registry, **pump**, egress) · **[ ] T051** distributed unification · **[ ] T052** fault-as-data/untrusted-frame hardening · **[ ] T053** adversarial input tests · **[ ] T054** quiescence oracle · **[ ] T055** QUIC-WS · **[ ] T056** round-trip tests · **[ ] T057** dist-deref adversarial · **[ ] T058** Lean proof PI:17.

### Runtime confirmation (`bidi.glp`)
`client_connector`/`server_listener` are defined in `programs/self.glp`; the `_link_*` kernels are declared in `prelude.gleam` (`is_builtin_procedure`) so link programs **type-check**. `bidi.glp` **loads on both** Dart and Gleam (`✓ Loaded`). But `main(peera, Got).` on **Gleam → `Got = X2, → failed`** — the link primitives (T050) are unimplemented at runtime, so the link never establishes. (`bidi.glp`/`mon.glp` are two-process TCP tests; single-process confirms the primitive layer's absence, not end-to-end link behavior — that is the close's acceptance.)

## Activation

`close-link-inbound-pump` (b3-c1-033) — **ACTIVATED** for the full US4 remainder **T050-T058**, on the delivered T045-T049 substrate:
- **Delivered, confirm-only**: `link-seam`, `link-transport-seam` (loopback+TCP), and the FrameCodec/CRC floor of `link-reliability`.
- **To build**: the `primitives/` layer (`inbound-pump`, `link-acceptance`, kernels, `transport_registry`, egress → also owned/shared with `close-link-layer-glp-primitives` b2-c2-008), the reliability sublayer (windowing/ordering/fencing/cycle-guard/reclaim → `close-link-layer-sequence-dedup` b2-c2-010), fault decoration (`close-link-layer-fault-decoration` b2-c2-009), `link-capability-gate`, `instance-network-join` + distributed unification (T051). Sequencing dependency (primitives → reliability → gate) per the close risk note.
- Acceptance bar (close): `programs/tests/link/` (bidi, pathb, mon, sr, pc, krepro) pass on Gleam over loopback + TCP; QUIC-WS (T055) intersects `verify-quicws-link-completion-live-repl-bridge` (b3-c1-009).
