<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-febe-embedded-switch-role-framing` (WP b3-c1-013, wave 2)

**Date**: 2026-07-23
**Method**: source-verification (entry-module sweep confirming single-process shape) + requirements-baseline extraction from the 026 dossier, the two premise reconciliations, the C# split MVP, and the four designed-unstarted FE/BE promises. No code executed (nothing FE/BE-split exists to run).
**Paired close**: `close-febe-embedded-switch-role-framing` (b3-c1-038).
**Feeds**: `build-fe-be-process-split` (b3-c2-046) — the §3 requirements baseline below is its direct input.
**Backing detail_ids**: `embedded-switch-role-framing`, `engine-review-dossier`, `engine-state-snapshot-persistence`, `liveness-crash-restart-host`, `multi-client-control-program`, `premise-reconciliation-compiler-location`, `repl-engine-split-binary-wire-mvp`, `restore-and-resume-link-reestablish`.

## Environment / commands run

- `rg -nw 'split|socket|server|client' glp_gleam/src` + per-file counts of `server|client|wire|socket|split`.
- `glp_gleam/src/glp_gleam.gleam` read (the `gleam run` entry point).
- File-inspected `specs/026-engine-review-dossier/spec.md` US1 (design areas a–g, :32-68) + US2 (premise reconciliations, :70-97) + the 2026-06-09 clarification (:28).
- Roadmap rows `roadmap-snapshot-2026-07-19.md` :27, :28, :32, :33, :34, :35, :39, :61 and `specs/027-refinement-verification-framework/spec.md:178` (FR-051).

## 1. Single-process confirmation — no Gleam-side FE/BE split (CONFIRMED)

The Gleam instance is one process. `glp_gleam/src/glp_gleam.gleam` is "a thin shell over the REPL loop" — `pub fn main() { repl.run() }` — the standalone instance runs the compiler + engine + REPL **in-process**. The two entry modules are `glp_gleam.gleam` and `atomvm_gated_probe.gleam`; neither spawns an FE/BE pair.

The sweep terms resolve to **non-split** origins:
- **`split` (7 hits)** — all incidental: `string.split(...)` call sites (`runner.gleam:1861`, `commands.gleam:73`, `goal_format.gleam:56`, `well_typed_term.gleam:323`) and prose comments ("int/float split", "own file split", "payload split across several frames"). **Zero** process-split constructs.
- **`socket`/`server`/`client` (all hits)** — confined to the **M2 inter-instance link transport** (`glp_link_tcp_ffi.erl:22`, `link/transports/tcp.gleam:6-148`, `link/seam/transport.gleam:30` "bind this end as the transport server") and to the envelope builder's **server-side deep-resolve** (`codec/result_envelope_builder.gleam:1`). These are the term-level link seam between *two peer instances* and the result-envelope value surface — **not** an intra-instance front-end/back-end process boundary.

This matches the recorded design (P7 dossier: "M1 = one zero-spawn process; the M2 term-link seam is the only place processes/network appear"). `embedded-switch-role-framing` (b1-c1-074) is therefore **DESIGNED-only** — a framing requirement in the 027 verification framework (FR-051, `specs/027-refinement-verification-framework/spec.md:178`), with no implementing feature in the Gleam corpus.

## 2. Verdict table (8 detail_ids)

| # | detail_id (id) | verdict | basis |
|---|---|---|---|
| 1 | `embedded-switch-role-framing` (b1-c1-074) | **DESIGNED — no code** | framing in 027 FR-051; no FE/BE switch/router module in `glp_gleam/src` |
| 2 | `engine-review-dossier` (b1-c1-039) | **DELIVERED** | 026 dossier design areas a–g present (spec.md:32-68); roadmap:28 `[closed, delivered]` |
| 3 | `premise-reconciliation-compiler-location` (b1-c1-040) | **DELIVERED (decided)** | 026 US2 (:70-97): compiler engine-internal as-built; MVP carries source text on the wire; relocation a deliberate follow-up |
| 4 | `repl-engine-split-binary-wire-mvp` (b1-c1-048) | **DELIVERED (C# reference side)** | roadmap:27 `[closed, delivered]` — C# REPL/engine split MVP w/ binary wire-format IL |
| 5 | `engine-state-snapshot-persistence` (b1-c1-043) | **DESIGNED-unstarted; OPEN forks** | roadmap:33 `[refined]`, blocked-by repl-engine-process-split-mvp; no spec dir; 026 area (e) forks unresolved |
| 6 | `liveness-crash-restart-host` (b1-c1-044) | **DESIGNED-unstarted; OPEN reconciliation** | roadmap:34 `[refined]`; 039 monitor spike notes BEAM supervision **may supersede** the C# liveness-host design |
| 7 | `restore-and-resume-link-reestablish` (b1-c1-045) | **DESIGNED-unstarted** | roadmap:35 `[refined]`, end of the separation dependency chain; no spec dir |
| 8 | `multi-client-control-program` (b1-c1-047) | **DESIGNED-unstarted** | roadmap:39 `[refined]`; design area covered by the 026 dossier; no spec dir |

**Tally**: FE/BE process split ABSENT on the Gleam side. Design authority (026 dossier + 2 premise reconciliations + C# MVP) DELIVERED. Four separation promises + the switch-role framing DESIGNED-unstarted, two of them carrying open forks the build must not resolve unilaterally.

## 3. Consolidated FE/BE requirements baseline (input for `build-fe-be-process-split`)

**Build target**: `repl-engine-process-split-mvp` — the Gleam **two-process split MVP (TCP loopback)**, roadmap:32 `[refined]`, blocked-by `result-codec-and-framecodec-ride`. That blocker is **cleared** (roadmap:61 `[released, delivered]`, specs/038), so the codec/transport substrate the split payload rides is available.

**026 dossier design areas (a–g) the build inherits (spec.md:47-53):**
- **(a) seam contract** — front-end/client vs embeddable engine; what crosses each way (delivered design). The delivered Gleam engine-value facade (`engine.gleam`) + ED-1 result envelope are the in-process shape the split externalizes.
- **(b) binary wire shapes** — client→engine request + the net-new engine→client **result envelope** (status, bindings, var-name→writer map, suspended-goal detail, captured output, errors, unbound-var-in-suspended-result encoding). Codec reuse decided (038 FrameCodec/term codec) — **DELIVERED**, reuse verbatim.
- **(c) control-program startup + client model** — designed; the Gleam realization is `multi-client-control-program` (#8), unstarted.
- **(d) long-running / liveness / crash / restart model** — designed; Gleam realization is `liveness-crash-restart-host` (#6). ⚠ **decision-needed** (see below).
- **(e) persistent-vs-ephemeral state model** — DB-abstraction, bootstrap, restore-and-resume; Gleam realizations are `engine-state-snapshot-persistence` (#5) + `restore-and-resume-link-reestablish` (#7). ⚠ **open forks** (see below).
- **(f) mailbox decision** — covered as a 026 design area; the build inherits the recorded decision rather than re-opening it.
- **(g) MVP slice** — the minimal FE/BE process-split increment; this is precisely the roadmap:32 target.

**Two premise reconciliations (US2, DECIDED — do not re-open, spec.md:90-97):**
1. **Compiler location**: parser/compiler is **engine-internal as-built**; the MVP **carries source text on the wire**; compiler relocation is a deliberate follow-up feature. Consequence: the split MVP does *not* need a compiler-on-the-FE-side.
2. **Runtime-IL generation**: **no bytecode is synthesised at runtime**; the mechanism is runtime goal-term assembly + dispatch against pre-compiled bytecode circulating as heap data. Consequence flows into the persistence design (what a snapshot must capture).

## 4. Decisions-needed — surfaced, NOT resolved (honoring the WP Risk)

Per the 026 clarification (spec.md:28) the dossier **presents options; the owner decides** — so this baseline flags the open forks rather than settling them:
- **F1 — snapshot/persistence API forks (area e).** The persistent-vs-ephemeral state model, the DB-abstraction choice, bootstrap, and restore-and-resume were surfaced by the 026 dossier as **owner-decision forks** and remain unresolved (`engine-state-snapshot-persistence`, b1-c1-043, no spec dir). `build-fe-be-process-split` MUST carry these as decisions-needed, not pick one. The runtime-IL reconciliation (§3 premise 2) constrains but does not settle what a snapshot captures.
- **F2 — liveness-host reconciliation (area d).** The 039 monitor spike records that **BEAM supervision may supersede** the C# liveness/crash/restart-host design. Whether the Gleam split reuses the C# host design or adopts BEAM-native supervision is an **open reconciliation** for the owner/engineer, not a build-time default.

## Activation

- **`close-febe-embedded-switch-role-framing` (b3-c1-038)** is **activated**: it ratifies this consolidated baseline and carries F1/F2 forward as explicit decisions-needed.
- **`build-fe-be-process-split` (b3-c2-046)** receives §3 as its requirements input; its blocker (038 codec) is cleared, but it is gated on the owner rulings for F1 (snapshot API) and F2 (liveness reconciliation) before those areas can be built.
- No code work is in scope here; the Gleam side is confirmed single-process, and the FE/BE split is a designed-unstarted build target.
