<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T075 close-link-layer-fault-decoration` (b2-c2-009)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Paired verify**: `verify-link-inbound-pump.md` (fault-as-data decoration ABSENT) · **Builds on**: T074/T077

## What was built

Fault-as-data decoration ported to parity with Dart `link_faults.dart` / C# `csharp/glp_link/primitives/LinkFaults.cs`.
The `LinkFaults` delivery core (pump `deliver_fault` + `link_faults` fanout/`end_all`) was already
byte-parity; this close adds the three decorations:

- **Bounded-silence heuristic** (FR-045): `SilenceVerdict` + `classify_silence(elapsed_ms, LinkOptions)`
  — `< temp` → no fault; `≥ temp_fail_after_ms` → `tempFail`; `≥ perm_fail_after_ms` → `permFail` —
  the pure decision the timed-recv liveness driver consults.
- **Fencing → permFail** (FR-047): `fence_fault(FenceVerdict, id, name)` — `Fenced` → `permFail`
  (wired to the T077 `fencing_registry` via new `link_runtime` fields `fencing`/`epochs` + `check_fence`).
- **Establishment-failure** (FR-044): a failed path-A `_link_setup` / path-B `_link_request` now
  registers a closed/faulted handle and binds `permFail` on the establishment `Faults` monitor
  (`decorate_establishment_failure`), instead of the bare `LinkAbort` — the data goal stays suspended
  on its unbound `In` head (fault is DATA, never a logical Fail).

## Runnable evidence

| Check | Command | Result |
|---|---|---|
| Regression floor | `cd glp_gleam && gleam test` | **587 passed, no failures** (571→587; +7 T075 tests) |
| Fault observation | `bash glp_gleam/test/link/run_link_tests_gleam.sh` | **7/7 PASS** — `mon.glp` observes decoration (`res([7,8,9],[closed(link_id(...`) |

New tests: `link_faults_test` (+6: silence thresholds, fence mapping, establishment-failure term),
`link_driver_test` (+1: `_link_setup` with no transport → `permFail` bound on the monitor, `LinkEffect`
not `LinkAbort`). Adversarial/undecodable-frame faults surface as GLP fault terms via the pump
(`permFail` on decode/reassembly failure) — never silent, never try/catch-swallowed.

**Close status: CLOSED** to named-reference parity (C# `LinkFaults`).
