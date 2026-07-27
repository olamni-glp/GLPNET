<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T070 close-febe-embedded-switch-role-framing` (b3-c1-038)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Closes**: `verify-febe-embedded-switch-role-framing` (b3-c1-013) — closes at **requirements level**
**Hands off to**: `build-fe-be-process-split` (b3-c2-046) — this record is its signed-off spec input
**Backing detail_ids**: `embedded-switch-role-framing`, `engine-review-dossier`, `premise-reconciliation-compiler-location`, `repl-engine-split-binary-wire-mvp`, `engine-state-snapshot-persistence`, `liveness-crash-restart-host`, `restore-and-resume-link-reestablish`, `multi-client-control-program`

## What was verified DELIVERED (requirements level)

The Gleam instance is confirmed **single-process**: `glp_gleam/src/glp_gleam.gleam` is
`pub fn main() { repl.run() }` — a thin shell running compiler + engine + REPL in-process; the two
entry modules (`glp_gleam.gleam`, `atomvm_gated_probe.gleam`) spawn no FE/BE pair. The sweep terms
resolve to non-split origins (incidental `string.split(...)`; `socket`/`server`/`client` confined
to the M2 inter-instance link transport + the envelope builder's server-side deep-resolve — a
term-level peer-to-peer seam, not an intra-instance front-end/back-end boundary). The **design
authority** is DELIVERED: the 026 dossier (design areas a–g, `specs/026-engine-review-dossier/spec.md:32-68`),
the two premise reconciliations (US2, :70-97), and the C# REPL/engine split MVP with binary-wire IL
(roadmap:27 `[closed, delivered]`).

## Runnable / inspectable evidence

| Check | Command | Result |
|---|---|---|
| Single-process shape | `rg -nw 'split\|socket\|server\|client' glp_gleam/src` + read `glp_gleam.gleam` | no process-split constructs; `main = repl.run()` |
| 026 dossier design areas a–g present | file-inspect `specs/026-engine-review-dossier/spec.md:32-68` (US1) + `:70-97` (US2) | design authority DELIVERED |
| The four separation promises are DESIGNED-unstarted | `roadmap-snapshot-2026-07-19.md:32-35,39` | all `[refined]`, no spec dir |
| Split-payload codec substrate available | `roadmap-snapshot-2026-07-19.md:61` (`[released, delivered]`, specs/038) | blocker cleared — reuse verbatim |

## Disposition (each of the eight detail_ids bound to a named wave-4 build acceptance)

| detail_id | disposition | wave-4 acceptance it is bound to |
|---|---|---|
| `engine-review-dossier` (b1-c1-039) | **DESIGN-CONFIRMED** — 026 dossier areas a–g present; roadmap:28 `[closed, delivered]` | design input consumed by `build-fe-be-process-split` §3 baseline |
| `premise-reconciliation-compiler-location` (b1-c1-040) | **DECISION-TAKEN** — compiler engine-internal; MVP carries source text on the wire; relocation a follow-up | constrains: split MVP needs **no** FE-side compiler |
| `repl-engine-split-binary-wire-mvp` (b1-c1-048) | **DESIGN-CONFIRMED (C# reference side)** — roadmap:27 `[closed, delivered]` | the pattern `build-fe-be-process-split` externalizes on the Gleam side |
| `embedded-switch-role-framing` (b1-c1-074) | **DESIGNED — no code** — framing in 027 FR-051 (`specs/027-refinement-verification-framework/spec.md:178`); no switch/router module in `glp_gleam/src` | `build-fe-be-process-split` acceptance: the FE/BE switch-role router exists and passes its seam test |
| `multi-client-control-program` (b1-c1-047) | **BUILT-IN-WAVE-4** — DESIGNED-unstarted (roadmap:39 `[refined]`) | `build-fe-be-process-split` control-program-startup + multi-client model (026 area c) |
| `engine-state-snapshot-persistence` (b1-c1-043) | **BUILT-IN-WAVE-4 — carries OPEN FORK F1** (see below) | `build-fe-be-process-split` persistence slice (026 area e) — gated on F1 ruling |
| `liveness-crash-restart-host` (b1-c1-044) | **BUILT-IN-WAVE-4 — carries OPEN FORK F2** (see below) | `build-fe-be-process-split` liveness slice (026 area d) — gated on F2 ruling |
| `restore-and-resume-link-reestablish` (b1-c1-045) | **BUILT-IN-WAVE-4** — DESIGNED-unstarted (roadmap:35 `[refined]`), end of the separation chain | `build-fe-be-process-split` restore-and-resume slice (026 area e) — downstream of F1 |

## Two OPEN forks — surfaced, NOT resolved by this close

Per the 026 clarification (spec.md:28: the dossier presents options; the **owner decides**), this
requirements close carries the forks forward as explicit decisions-needed; it does **not** settle
them, and `build-fe-be-process-split` must not resolve them unilaterally.

- **F1 — snapshot / persistence API (026 area e).** The persistent-vs-ephemeral state model, the
  DB-abstraction choice, bootstrap, and restore-and-resume are owner-decision forks, unresolved
  (`engine-state-snapshot-persistence`, b1-c1-043, no spec dir). The runtime-IL reconciliation
  (premise 2: no bytecode synthesised at runtime; goal-term assembly + dispatch against pre-compiled
  bytecode) **constrains but does not settle** what a snapshot must capture. Handed to the engineer.
- **F2 — liveness-host vs BEAM supervision (026 area d).** The 039 monitor spike records that
  **BEAM supervision may supersede** the C# liveness/crash/restart-host design. Whether the Gleam
  split reuses the C# host design or adopts BEAM-native supervision is an open reconciliation for
  the owner/engineer, not a build-time default. Handed to the engineer.

**Close status: CLOSED at requirements level.** Single-process shape confirmed; design authority
(026 dossier + two premise reconciliations + C# MVP) DELIVERED; all eight detail_ids dispositioned
and each bound to a named `build-fe-be-process-split` acceptance (no paper close). F1 and F2 are
carried forward as explicit engineer decisions — this close does **not** resolve them.
