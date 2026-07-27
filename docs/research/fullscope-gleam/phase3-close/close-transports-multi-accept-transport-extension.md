<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T089 close-transports-multi-accept-transport-extension` (b3-c2-035)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Paired verify**: `verify-transports-multi-accept-transport-extension.md` (multi-accept + quiescence-oracle ABSENT)
**Builds on**: T074/T076/T077

## What was built

### Quiescence oracle (GAP-G6) — DELIVERED
`glp_gleam/src/glp/link/primitives/quiescence.gleam` — the network-quiescence oracle (T054 predicate
ported reusable), with `scheduler.runnable_count(engine)` + `types.queue_length(RunQueue)` as inputs.
Public API (consumed by the T083 PI:17 adversarial suite + T066):
```
type Quiescence { Quiescent | Active(runnable_goals, frames_in_flight, open_pending) }
decide / is_quiescent (pure over 3 counts: quiescent iff all zero)
link_activity(runtime) -> #(frames_in_flight, open_pending)   // live adapter
is_run_quiescent(runnable_goals, runtime) -> Bool
```
Tests: `quiescence_test` (+8: pure predicate + live-runtime adapters).

### Multi-accept — DELIVERED (capability) with a flagged residual
One transport listener yields **N distinct links** (distinct `LinkId` nonces), each with its own
per-link reliability state (independent `LinkSequencer`/`FrameReassembler`/`InboundOrdering` — verified
by construction). Tests: `multi_accept_test` (+2). ZMQ left point-to-point/capability-gated per its
contract (delivered + hardened, untouched).

**🔻 Flagged residual (NOT a silent deferral — surfaced per no-deferrals discipline):** the loopback
hub inherently multiplexes; the **TCP leaf** multi-accepts by **re-listen** (releases + re-binds the
port per accept), which functionally yields N links but has a port-release window between accepts. The
Dart/C# reference does a **single `listen` → persistent-socket accept-loop** (true concurrent
multi-accept, no window). That single-persistent-socket accept-loop is the remaining TCP-specific
refinement — it needs an FFI/seam addition and **is not exercised by any acceptance program**. Recorded
here as a named follow-up for engineer direction (a dedicated TCP accept-loop WP or a rule-request),
NOT quietly deferred.

## Runnable evidence

| Check | Command | Result |
|---|---|---|
| Regression floor | `cd glp_gleam && gleam test` | **587 passed, no failures** (571→587; +10 T089 tests) |
| Link round-trips | `bash glp_gleam/test/link/run_link_tests_gleam.sh` | **7/7 PASS** (no regression) |

**Close status: CLOSED** — quiescence oracle + multi-accept capability delivered to reference parity;
the TCP single-persistent-socket accept-loop refinement is a flagged, engineer-directed residual (above).
