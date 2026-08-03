<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Gate ledger — wave-6 (boundary: not-closed snapshot 20260803T150440Z, 18 items)

**Contract**: `contracts/gate-ledger.md`. **Evidence sources**: `evidence-inventory.md`.
**Created**: 2026-08-03T21xx. Rows update in the same commit as the event they record.

## Gates

| gate_id | kind | state | evidence | blocks |
|---|---|---|---|---|
| G1 | ship-state (064) | open | 064 ship-ready (551/551, codexreview capped@5-escalations-only, 51be73c5); ship+close await engineer keystroke @ v2026.08.03.2 | ITEM-07 disposition; T022 |
| G2 | track (065) | open | specs/065 specified @ d2ea81e9; mrun-7939e12b5b70; its FR-008 5-escalate gate cascades | ITEM-11 disposition; T025/T026 |
| G3.R1–R5 | ruling (3rtask fa8a) | open | evidence-inventory.md R1–R5 | 065 stories (cascade via G2); any wave story touching the audited seams |
| G3.R6–R12 | ruling (064 review) | open | evidence-inventory.md R6–R12 | T022 (R6/R7 replay semantics); T023 (R6–R12 as 059-acceptance caveats) |
| EXT.ariellas | external-ownership | open | 064-post-wave-gap-closure seams 1–5 receipts (153205Z); carve-out CONFIRM 153920Z; **implement receipts NOT yet posted** | T021/T022 receipt consumption; ITEM-04; verification caveat on ITEM-12..18 closures |

## Items (18 rows = the 150440Z snapshot)

| item_id | group | disposition_path | state | blocked_by | evidence |
|---|---|---|---|---|---|
| ITEM-01 atomic-toolchain-installs-venv-swap-post-install-smoke | US2 | story | pending | — | roadmap: refined |
| ITEM-02 batch-roadmap-advance-calver-version-dir-normalisation | US2 | story | pending | — | roadmap: refined |
| ITEM-03 glp-runtime-consol | US3 | story | pending | — | roadmap: promoted; handover seed bc5ea232 |
| ITEM-04 post-wave-consolidation-verified-gap-closure-repl-engine-full-gleam | EXTERNAL | external-gate | parked | EXT.ariellas | ariellas' feature (mrun-35df7ddfe4ec); their US receipts will dispose it |
| ITEM-05 qr-link-provisioning | US3 | story | pending | — | roadmap: promoted |
| ITEM-06 antlr4-shared-grammar-spike | US4 | story | **superseded (by peer rider)** | — | ariellas 210601Z: "antlr4 superseded (G5)" under the Option-B re-scope ruling; roadmap rider executed with their implement-complete receipts @ 459be1b2 |
| ITEM-07 durable-listener-service-box (064) | EXTERNAL | external-gate | parked | G1 | own track; ship-ready; engineer keystroke pending |
| ITEM-08 ynet-human-memorable-decentralized-naming-resolver | US6 | triage | pending | G2 | roadmap: captured |
| ITEM-09 ynet-mobile-background-battery-budget-scheduling-policy | US6 | triage | pending | G2 | roadmap: captured |
| ITEM-10 buildkit-coordination-optimisation-gepa-dspy | US6 | triage | pending | — | roadmap: captured |
| ITEM-11 ynet-consolidation (065) | EXTERNAL | external-gate | parked | G2 | own track (specs/065); 5-escalate gate |
| ITEM-12 glp-gleam-compiler-and-loader | US5 | story | **closed (by peer)** | — | journal: closed 20260803T205504Z actor-ce1ef684db6c; same caveat as ITEM-06 |
| ITEM-13 glp-gleam-bytecode-runner | US5 | story | **closed (by peer)** | — | journal: closed 20260803T205459Z; same caveat |
| ITEM-14 glp-gleam-repl | US5 | story | **closed (by peer)** | — | journal: closed 20260803T205454Z; same caveat |
| ITEM-15 glp-test-corpus-port-and-runner | US5 | story | **closed (by peer)** | — | journal: closed 20260803T205509Z; same caveat |
| ITEM-16 glp-gleam-link-layer | US5 | story | **closed (by peer)** | — | journal: closed 20260803T205606Z; same caveat; overlaps their US1 touch-set |
| ITEM-17 cross-runtime-csharp-gleam-distributed-tests | US5 | story | **closed (by peer)** | — | journal: closed 20260803T205513Z; same caveat |
| ITEM-18 full-scope-gleam-glp-implementation | US5 | story | pending | EXT.ariellas (US4 sweep receipts), G3.R6–R12 (059-acceptance caveats) | roadmap: specified (specs/059); T023 reconcile is the wave's verification hook for ITEM-06/12–17 closures too |

## Drift record (T004 reconcile, 2026-08-03 ~2110Z)

- Live not-closed set = 13 vs snapshot 18: ITEM-06 (superseded) and ITEM-12..17 (closed) were
  disposed by ariellas' engineer-directed roadmap rider (batch 20:54–20:56Z; "Option-B
  re-scope" ruling on their record, 210601Z), consumed via imports e21edf62 + 210353Z. Wave-6
  does NOT rebuild them (FR-004). **Receipts landed 210601Z**: their implement COMPLETE @
  459be1b2 pushed (REPL 381 · C# 360 · gleam 618 · corpus 206/206, zero regression); their
  ship (T041) still pending → T023's 059-reconcile remains the wave's final verification hook,
  and EXT.ariellas stays open until their ship/close receipts post.
- FR-001/FR-002 of their 064 (dist-unify + distributed quiescence) were TRANSFERRED to the new
  capture `distributed-unification-quiescence-protocol-two-runtime-spec-first` — outside the
  wave boundary (wave-7+), now carrying real transferred requirements (noted for the roadmap).
- Post-snapshot capture `distributed-unification-quiescence-protocol-two-runtime-spec-first`
  (ariellas, 20:52Z) is OUTSIDE the wave boundary (assumption: wave-7+).
- `wave6-consolidation` itself appears in the live not-closed set (it is this wave's own
  feature, not a wave item).
