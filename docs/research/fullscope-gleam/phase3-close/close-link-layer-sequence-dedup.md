<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T077 close-link-layer-sequence-dedup` (b2-c2-010)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Paired verify**: `verify-link-inbound-pump.md` (reliability sublayer above the frame codec = ABSENT)
**Builds on**: T076 primitives trunk + T074 pump (`f12b1d87`)

## What was built

The reliability sublayer proper, faithfully ported from `glp_runtime/lib/link/reliability/` (C# mirror
`csharp/glp_link/reliability/`) into `glp_gleam/src/glp/link/reliability/`, inserted between the pump
and the delivered frame_codec/crc32 floor:

- `link_sequencer` — per-link monotone outbound sequence, 2³²-wrapped (FR-020).
- `inbound_ordering` — reorder buffer + transport-level dedup (FR-020/021/023); out-of-order held until
  the gap fills, old/dup frames idempotent no-ops, bounded (FR-028).
- `frame_reassembler` — multi-frame payload reassembly (FR-022) — the Fragment path the T074 MVP left
  Whole-only; bounded + metadata-consistent, dup-fragment idempotent.
- `send_window` — bounded credit accounting for backpressure (FR-025).
- `fencing_registry` — split-brain fencing tokens + `EpochAllocator`, highest-epoch-wins (FR-047, SC-011).
- `cycle_guard` — send-path cycle detection keyed on heap cell address (FR-022/028; BEAM analogue of
  Dart reference-identity).
- `link_reclaimer` — distributed-GC coordinator, idempotent reclaim + straggler-after-teardown (FR-024, SC-014).
- `resource_snapshot` — reclamation baseline value (SC-014).
- `frame_exception` — reassembly/ordering error value (BEAM `Result`, not a throw).

**Live-path wiring**: the pump ingress was rewritten to `recv` → `frame_codec.parse_frame` →
`frame_reassembler.accept` → `inbound_ordering.accept` → decode each in-order payload → extend `In`;
egress draws its sequence from `link_sequencer`. Malformed / inconsistent / over-bound frames become a
`permFail` on the monitor (FR-028, never silent). `send_window`/`fencing_registry`/`link_reclaimer`/
`resource_snapshot`/`cycle_guard` are ported + unit-tested but not yet on the live path — faithful to
the Dart "seam now, wire later" (their activation is T075 fault/GC + T024 split-brain).

## Runnable evidence (fresh-session reproducible)

| Check | Command | Result |
|---|---|---|
| Regression floor (grow-only) | `cd glp_gleam && gleam test` | **571 passed, no failures** (544→571, +27 reliability tests) |
| Link round-trips (no regression) | `bash glp_gleam/test/link/run_link_tests_gleam.sh` | **7/7 PASS** — every round-trip now flows through sequencer(egress)+reassembler+ordering(ingress); `sr.glp` (link_recv_chain) still passes |

The +27 tests exercise: sequence monotonicity; in-order/out-of-order/dedup/bound ordering; **real
multi-frame reassembly** (fragments produced by `frame_codec.encode` under a small MTU, reassembled to
the original); window acquire/release/over-release; fencing admit/fence/epoch; cycle enter/leave/DAG;
reclaimer idempotency + straggler; snapshot baseline.

## Handoff

- **T075** (fault decoration): the pump already surfaces `closed(LinkId,eos)` + `permFail` on
  decode/reassembly faults; T075 adds the bounded-silence heuristic (`tempFail`→`permFail`),
  establishment-failure decoration, and fencing→`permFail`. `fencing_registry`/`link_reclaimer`/
  `resource_snapshot` are now in place for the reclaim/GC side.
- **T089** (multi-accept): reassembler/ordering are per-link, so each accepted link gets its own
  sublayer state.

**Close status: CLOSED** to named-reference parity — the reliability sublayer (sequencing/ordering/
dedup/reassembly/windowing/fencing/cycle-guard/reclaim) is ported with parity to Dart/C#, the ordering
+ reassembly stages carry the live round-trips, and `gleam test` grew to 571 green.
