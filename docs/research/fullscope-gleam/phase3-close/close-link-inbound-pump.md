<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T074 close-link-inbound-pump` (b3-c1-033) + `T076 close-link-layer-glp-primitives` (b2-c2-008)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Paired verify**: `verify-link-inbound-pump.md` (ABSENT — full US4 remainder T050–T058 on the T045–T049 substrate)
**Commits**: `a6347f30` (T076 primitives trunk), `1c42f006` (T074 driver + pump)

These two WPs **share the acceptance bar** (the six `programs/tests/link/*.glp` over loopback+TCP);
they close together. T076 delivered the primitives layer; T074 delivered the driver/pump that runs it.

## What was built

**T076 primitives trunk** (`glp_gleam/src/glp/link/primitives/`, faithful port of `glp_runtime/lib/link/primitives/`):
`link_terms` (GLP ground-term ↔ host mapping + fault lattice), `transport_registry` (scheme→leaf),
`link_handle`, `link_registry` (idempotent), `link_runtime` (per-engine state), `link_egress`
(ground-relay ship pipeline), `link_faults` (fault-as-data plans), `link_kernels` (the 7 kernel
recognition table matching `prelude.gleam:67-73`). +36 gleeunit tests.

**T074 driver + pump**: `link_driver` (effectful `_link_*` dispatch over `LinkRuntime` — path A
`_link_setup` + path-B `_link_request`/`_link_listen`/`_link_accept` handshake), `link_pump` (drive
to quiescence → egress: walk bound `Out` prefix → `ship_ground` → `endpoint.send`; ingress: block on
`endpoint.recv`, extend `In`, `closed(LinkId,eos)` on peer eos — the **faithful stand-in for the Dart
`heap.onBind` egress drainer the Gleam heap lacks**, lowered to a `known(Out?)`-driven goal exactly as
madGLP lowers `global_send`), `link_wire` (term↔single-frame codec), `link_repl` (link-aware scripted
entry). Core threading: `link: Option(LinkRuntime)` on `RunnerContext`/`Reduced` + `with_link` +
`step_link`/`run_link` in the scheduler — a **faithful mirror of the `mad`/`step_mad` precedent**; the
pure `run` path is untouched (a `_link_*` kernel with no driver still fails non-fatally).

## Runnable evidence (fresh-session reproducible)

| Check | Command | Result |
|---|---|---|
| Regression floor (grow-only) | `cd glp_gleam && gleam test` | **544 passed, no failures** (508→544; +36 T076 tests; runner/scheduler surgery regressed nothing) |
| Two-process link acceptance | `bash glp_gleam/test/link/run_link_tests_gleam.sh` | **PASS=7 FAIL=0** over real TCP, two `gleam run` processes |
| Dart-oracle parity | Dart rig `test/link/run_link_tests_dart.sh` (this host) | 8/8 PASS, byte-identical outcome strings |

Per-program (Gleam two-process ≡ Dart oracle): **bidi** (consumer `[1,2,3]` / producer `[10,20,30]`),
**pathb** (`[100,200,300]`), **mon** (`res([7,8,9],[closed(link_id(...))])`), **sr** (`[10,20,30]`),
**pc** integers `[10,20,30]` + terms `[pt(1,2),pt(3,4)]` + `link_send`/`link_recv` wrappers. **krepro**
(kernel-free) still all-suspend — parity preserved.

## Discovered pre-existing finding (flagged, not patched — Bug Protocol)

`pc.glp`'s `producer_strs` variant: string values round-trip **correctly** (alice/bob/carol arrive
intact), but the shared Gleam REPL renderer prints a `ConstString` **unquoted**
(`results.format_term`: `ConstString(s) -> s`) → `Got = [alice, bob, carol]` vs Dart's
`["alice","bob","carol"]`. This is a **shared-renderer parity gap independent of the link layer**,
affecting the whole corpus surface; the parity corpus (T080, 206 agree) has no top-level-string-output
case so it did not surface there. Changing `format_term` risks the golden/corpus parity, so it is
**recorded here for a renderer-parity follow-up**, not patched inside T074. Does NOT block this close
(the link layer round-trips values correctly).

## Handoff (build on this)

- **T077** (`close-link-layer-sequence-dedup`): fragment reassembly + send-window backpressure (this
  close handles the `Whole`-frame MVP the acceptance programs produce); slots onto `link_handle`'s `seq`.
- **T075** (`close-link-layer-fault-decoration`): establishment-failure fault decoration on the monitor
  (`link_faults` already produces the delivery plans).
- **T089** (`close-transports-multi-accept`): `link_runtime.pending` + `link_terms.parse_request_token`
  are the path-B foundation; multi-accept extends the transports.
- LinkId-keyed `_link_send`/`_link_monitor`/`_link_close` kernels are unported (no acceptance program
  exercises them — base send/close/monitor ride the self.glp channel wrappers over the pump).

**Close status: BOTH CLOSED** to named-reference parity — the six link acceptance programs round-trip
on the Gleam instance, two-process over real TCP, with Dart-equivalent outcomes; `gleam test` grew to
544 green.
