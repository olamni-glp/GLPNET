<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T066 close-distribution-engine-sessions` (b2-c2-014)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-28
**Closes**: `distribution-protocol` (b2-c2-014) — engine-to-engine session establishment + remote
goal/result routing above the delivered wire layers.
**Backing detail_ids**: `distribution-protocol`

## Acceptance (FINAL plan, line 467)

> The T057 adversarial dist-deref suite (specs/050-full-gleam-combined/tasks.md:134, currently
> unchecked) passes, plus a **new two-engine goal/result round-trip test in glp_gleam** (today no test
> connects two engines or routes a goal/result across a link).

Both halves are now green under `gleam test` (**624 passed, 0 failures**; baseline 615 + 7 S1 codec + 2
S3 acceptance).

## What was delivered — three slices

| Slice | Artifact | State |
|---|---|---|
| **S1 — Message ↔ link-frame codec** | `glp_gleam/src/glp/mad/message_codec.gleam` (NEW) | Wraps a pending `Message(name, term, dest)` as ONE ground struct `_assign(NameTerm, T↑)` and ships it through the shared ground-relay codec `link_wire` (`term_codec` TLV inside one `frame_codec` Whole frame); `decode` splits it back to `#(GlobalName, Term)`. Both halves are ground (`global_name.to_term` mints a ground `_w`/`_r` struct; `_send` globalizes every var of `term` to a ground `_w`/`_r` name), so the wrapper passes `link_egress.serialize_ground` unchanged. `dest` is the routing header, NOT wire-carried (the receiver IS the destination). |
| **S1 test** | `glp_gleam/test/glp/mad/message_codec_test.gleam` (NEW, 7 tests) | Scalar / list / integer / serializer-cold-call-with-nested-names / writer-name-polarity round-trips; garbage bytes and a non-`_assign` wrapper refused loudly. |
| **S2 — session establishment** | `glp_gleam/src/glp/mad/dist_session.gleam` (NEW) | The dest-agent → established-`Endpoint` routing table — Gleam mirror of the Dart oracle `IsolateManager._agentPorts` (`Map<String, SendPort>`, isolate_manager.dart). `connect`/`send`/`recv` over the T045 transport seam; per-peer send-sequence counter seeds each frame's `message_id`. Point-to-point per peer (link primitives are point-to-point, D-9); a `Dict(Term, Endpoint)` of peers is forward-compatible with the wave-5 N-peer mesh (each peer = one pairwise link). |
| **S3 — two-engine round-trip over a REAL link** | `glp_gleam/test/glp/mad/dist_session_roundtrip_test.gleam` (NEW, 2 tests) | `client_monitor_value_flows_over_real_link` — the identical spec §10.1 value flow (writer Xs@p, reader Xs?@q; p assigns Xs := [add]; the value reaches q's reader) as `mad_multiagent_test`'s direct-delivery test, but every assignment message crosses a genuine **loopback `Endpoint` pair** (encode S1 → `ep.send` S2 → `ep.recv` → decode → `mad_engine.receive`). q's monitor reader converges to `[add]`, matching the direct test and the T083 Lean `deliver`/`deliver_binds_owner` proof. `duplicate_wire_delivery_is_refused` — the owner-only-bind discipline holds across the seam: a SECOND wire delivery of the same assignment is refused loudly (no duplicated/lost bind). The drive loop mirrors `link_pump.drive` (run to quiescence → drain M_p → ship → peer recv+Receive → re-drive), lifted from the link-goal engine to the MadEngine. |

### Acceptance half 1 — T057 adversarial dist-deref suite

Already green, delivered by **T083** (`glp_gleam/test/glp/mad/dist_deref_convergence_adversarial_test.gleam`,
14 tests) — the T083 evidence file records it as "the first half of the T066 acceptance". Its
`two_engine_deferred_assignment_converges` test connects two engines but delivers DIRECTLY (in-memory
`receive`); S3 is the missing "routes a goal/result **across a link**" half.

## Mesh/ring scope (G3 ruling, rulings.md:19)

`rule-mesh-ring-escalation` is **RULED IN-SCOPE** with **multi-peer acceptance breadth = wave 5**
(acceptance target: a Gleam equivalent of `programs/tests/quic/quic_mesh.glp`). Wave-3 T066 delivers the
**pairwise (point-to-point) session establishment + goal/result routing** the wave-5 breadth builds on;
`dist_session` already routes N peers as N pairwise links, so no rework of the value shape is implied.
This is flagged (not silently deferred): the N-peer mesh acceptance is wave 5, tracked under the accept
WPs, NOT a T066 residual.

## Discipline

Gleam-side only — four NEW files under `glp_gleam/src/glp/mad/` + `glp_gleam/test/glp/mad/`. No
`self.glp` touch, no new kernel/guard/directive (host-side plumbing over the existing transport seam, not
a language change — §1.14 not engaged). Dart runtime + REPL suite untouched. `gleam test` 624/0.
