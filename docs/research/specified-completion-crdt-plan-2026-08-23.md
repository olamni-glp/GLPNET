<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# Specified-features completion CRDT plan — 2026-08-23

**Marathon**: `mrun-76da6e46bd44` · **Host**: OLAMNIT · **Origin**: /bk-3rtask run `20260823T093108Z-30dd`
(3 blind builders × 2 cycles, codex Critic, 82 CONFIRMED claims / 7 open ESCALATEs).

> **Why this file exists.** Same contract as the tidy-up workplan: marathon items are the **state
> machine**; this file is the **authoritative content**. Where an item name and this file disagree,
> **this file wins**. Update this file first, then reflect state in the marathon — never the reverse.

## Root cause of the "stuck at specified" stall (evidence-grounded)

1. **Not actually specified-complete — all three specs are `Status: Draft`** (082 spec:11, 083
   spec:12, 085 spec:11). PRs/roadmap advertise later states → state contradiction.
2. **Pipeline-pointer drift (dominant, executable cause).** `feature.json`→078, pipeline DB
   active→079, `buildkit-builder` records the drift warning; nothing drives 082/083/085, so no
   stage can advance.
3. **Mis-homing (engineer-bound).** 085→BUILDKIT lane (mstack canonical, do-not-fork, P02);
   083→allocated to `ariellas`; 082→engine-side split-from-078 / #13 coordination vehicle.
4. **Open §1.14 / engineer gates.** 083 FR-002 (gates specify→plan), 082 capability-name
   escalation, 085 FR-029 single-host + fleet-binding-authority.

## Per-feature completion spine (executed only for features ruled in-glpnet)

Each `Cxx` item is the ordered stage spine with **per-stage entry preconditions**; each `Gxx` is the
engineer gate that blocks its spine at the named stage. Engineer gates are **parked/deferred, never
dropped**. States: DONE / PARKED / DEFERRED / BLOCKED / ▶READY.

| ID | Item | Size | State | Gate/precondition |
|---|---|---|---|---|
| X00 | Reconcile pipeline pointer (drift 078/079 → intended feature) via `/bk-builder switch` | micro | ▶READY | in-lane, no engineer input |
| **082 — feature-stream-superset** | | | | |
| C082 | clarify→plan→tasks→analyze→implement→codexreview→ship→close | maxi | BLOCKED | on G082 + homing ruling |
| G082 | ENGINEER: capability-name normalisation escalation + is 082 a glpnet feature or the engine-side #13 vehicle? | midi | DEFERRED | engineer ruling |
| **083 — glptutorial-corpus-goldens** | | | | |
| C083 | clarify(cont.)→plan→…→close | maxi | BLOCKED | on G083a + G083b |
| G083a | ENGINEER: homing — 083 is ariellas-allocated (`ariellas:000035`); does olamnit drive it? | mini | DEFERRED | engineer ruling |
| G083b | ENGINEER: FR-002 §1.14 ruling — gates specify→plan (dependent work must not be planned before) | midi | DEFERRED | engineer/Udi ruling |
| **085 — onrestart-fleet-resume** | | | | |
| C085 | clarify→plan→…→close | maxi | BLOCKED | on G085 |
| G085 | ENGINEER: homing — bk-onrestart canonical=mstack, tracked P02 BUILDKIT lane; does 085 belong on the glpnet roadmap at all? + FR-029 fleet-binding-authority | midi | DEFERRED | engineer ruling |
| **Reconciliation (executable)** | | | | |
| X01 | Reconcile 083 state contradiction (spec Draft vs PR "Clarified") in the spec header | nano | ▶READY | after G083 rulings |
| X02 | Reconcile 085 state contradiction (spec Draft vs "checkpointed complete") | nano | ▶READY | — |
| X03 | Reconcile workplan↔memory execution-state drift (T02/T11/T12) | nano | ▶READY | — |

## Cross-run — first CRDT task of NEXT session (engineer directive 2026-08-23)

| ID | Item | Size | State |
|---|---|---|---|
| M01 | **/bk-marathon → /bk-flow migration** — plan+execute a SAFE, IDEMPOTENT migration via a dedicated /bk-3rtask run; includes automatic upgrade + verification that build succeeds and ALL `/bk-*` tools work correctly post-migration; evaluate migration safety before cutover | saga | ▶FIRST-NEXT-SESSION |

M01 is the **first** item to run next session (before resuming the per-feature spines). It is itself
a /bk-3rtask task: rootcause the marathon→flow delta, design the idempotent migration + rollback,
execute with auto-upgrade, then verify-and-evaluate (build green + every `/bk-*` tool exercised).

## Discharge condition
This programme discharges only when: X00–X03 done; each of C082/C083/C085 either driven to `close`
(if ruled in-glpnet and its gate resolved) or formally handed to its correct lane/host (if ruled
out); every Gxx resolved or explicitly re-parked with rationale; M01 completed. **No feature is
"completed" by silently advancing past an open gate** (method R3/R5).
