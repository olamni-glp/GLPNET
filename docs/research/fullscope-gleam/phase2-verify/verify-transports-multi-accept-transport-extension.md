<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-transports-multi-accept-transport-extension` (b3-c1-010)

**Feature**: 059 · **Wave**: 2 (verify) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27 · **Backing detail_ids**: `multi-accept-transport-extension`, `quiescence-oracle`, `transport-parity-all-gating`, `untrusted-frame-hardening`, `zmq-comm-base`

## Runnable evidence (fresh-session reproducible)

| Check | Command | Result |
|---|---|---|
| Transport leaves | `ls glp_gleam/src/glp/link/transports/` | `loopback.gleam`, `tcp.gleam`, `zmq.gleam` — **no `quic.gleam` leaf** |
| Multi-accept | `rg -n 'one.link\|multi.?accept\|per.listen' glp_gleam/src` | `tcp.gleam:91` = **"One link per listen (MVP)"** — no multi-accept |
| Quiescence oracle | `find glp_gleam -iname '*quiescen*'` | **none** |
| Untrusted-frame hardening | reliability `frame_codec.gleam` + `crc32.gleam`; `glp_gleam/test/glp/link/frame_codec_test.gleam` (reject/malformed/truncated) | **present** |
| ZMQ in contract | `ls .../transports/zmq.gleam` | **present** (hardened 2026-07-27, codex review) |

## Verdict

| detail_id | verdict | basis |
|---|---|---|
| `multi-accept-transport-extension` | **ABSENT** | TCP is one-link-per-listen MVP; no multi-accept loop. |
| `quiescence-oracle` | **ABSENT** | No `quiescence_test.gleam` / quiescence module. |
| `transport-parity-all-gating` | **PARTIAL** | loopback/tcp/zmq leaves present behind the T045 seam; the **QUIC transport leaf is absent** from `transports/` (the `gleam_quic` relay is separate, and engine-side QUIC-WS is ABSENT per `verify-quicws-link-completion-live-repl-bridge`). |
| `untrusted-frame-hardening` | **DELIVERED** | `frame_codec.gleam` (length+CRC+type) + `crc32.gleam` + `frame_codec_test.gleam` reject/malformed adversarial cases. |
| `zmq-comm-base` | **SUPERSEDED → PRESENT** | see drift note. |

## Drift surfaced (routed, not buried)

The WP's premise **"confirmation of ZMQ absence from the Gleam transport contract"** is **outdated**.
The 2026-07-23 owner ruling (`rulings.md`) **OVERRULED** the G5 out-of-scope disposition: ZMQ is
**mandatory / in-contract**, and the `zmq.gleam` leaf now exists (and was hardened 2026-07-27 per the
codex review — bounded establishment handshake, erlzmq-absent fault, malformed-frame fault). So
`zmq-comm-base` is **in-scope = required and present**, not "confirm-absent". This verify records the
premise drift rather than emitting a stale "ZMQ absent" verdict.

## Decision this surfaces

Transport parity is **owner-clarified all-gating**. The two ABSENT items (`multi-accept`,
`quiescence-oracle`) and the PARTIAL QUIC-leaf gap must route through their close WPs
(`close-transports-multi-accept-transport-extension`, plus the QUIC closes gated by T098) — any
per-transport deferral requires a rule-request, never a quiet verdict.
