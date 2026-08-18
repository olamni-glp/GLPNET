<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# glpnet Consolidated Completion Plan (2026-08-17)

**Marathon run:** `mrun-7f0b400450f3` (feature `glpnet-consolidated-completion`; durable mirror under the deploy-home, survives safe restarts).
**Scope (engineer-ruled):** everything outstanding in glpnet — roadmap not-closed (~20) + 079 in-flight + 080 Udi-blocked + PR/chore cleanup — **plus the two mandatory inclusions**: the consolidated-hardening charter (3rtask run `d920`) and the scheduler/Programme-B remediation (#13 + A1–A4).
**Ship-gate policy (engineer-ruled):** drive each item through the pipeline to its ship gate, then **STOP for a SHIP-TOKEN + go**. No outward ship/release/close unattended. Core-touch (079 heap_fcp.dart) and fleet (scheduler) have hard gates.
**Authority (engineer-ruled):** olamnit designs + curates; cross-repo/all-host rollout is coordinator-driven (gavriella) per-host via COOP.
**Blocked items (engineer-ruled):** included and PARKED with named blocker + unblock action (4 durably captured in the marathon).

## Worktree/branch reality
- ONE worktree (this repo). No detached worktrees.
- Unmerged branches map to roadmap items below; stale chore/upgrade branches handled in Wave 0.

## Dependency-ordered execution (waves)

### Wave 0 — finish in-flight + cheap closes + cleanup (safe, mostly non-gated) — START HERE
| Item | Roadmap | State | Next action | Gate |
|---|---|---|---|---|
| 079 madglp writer-reader | #7 | analyze DONE | `/bk-implement` **US3→US2 only** (non-core) | US1 → SHIP-TOKEN |
| Close released-not-closed | #12,#14,#28,#6 | released | `/bk-close` each | — |
| Hardening charter → roadmap | (new) | charter DONE (d920) | `/bk-codify` + `/bk-roadmap` add feature | — |
| PR cleanup | #163–168,#111,#49 | open | merge latest sync, close 4 redundant; decide #111/#49 | — |

### Wave 1 — SHIPS-FIRST anchor
| #1 verification-receipts-and-loud-failure | WSJF 7.80 | promoted | full pipeline `/bk-specify`→ship | ship gate |
Unblocks Wave 3 (5 features). This is the prerequisite for every other acceptance suite being trustworthy.

### Wave 2 — scheduler remediation (fleet — gated, coordinator-driven)
| #13 coordination-feature-stream + Programme B | WSJF 4.25 | promoted | A1 release develop → A2 gavriella lands B3 + `__main__` fold → A3 per-host deploy → A4 B6 normaliser | cross-host, A1–A4 |
NOTE: the durable-fix code is already merged to buildkit develop; this wave is release+deploy+one-merge, NOT design. Parked blocker: cross-host authority.

### Wave 3 — unblocked by #1 (after Wave 1 ships)
#24 per-host-toolchain-contract · #30 multi-host-state-discipline · #34 seam-specification · #35 single-source-of-truth · #59 product-defect-burn-down.

### Wave 4 — high-WSJF promoted (GLP/runtime + connectivity)
#3 glptutorial-golden (6.50) · #15 type-checker-body-atom-moding/076 (4.20) · #21 front-end-goal-term (3.60) · #43 full-scope-gleam (specified, 059) · #33 041-cross-runtime-two-host (2.62) · #36 crdtmsg-post-mvp (2.40).

### Wave 5 — remaining promoted/specified
#49 wave6-consolidation (066) · #32 ynet-consolidation (065) · #46 buildkit-coord-opt GEPA/DSPy (2.00) · #54 distributed-unification-quiescence (1.62) · #60/#57 ynet naming+mobile · the new consolidated-hardening feature (after its charter→roadmap).

## Parked blockers (marathon-durable; safe-restart on clear)
1. **080 occurs-check** — needs Udi §1.14 FR-002 ruling → action: draft + relay proposal (D3).
2. **079-US1 core heap_fcp.dart** — needs SHIP-TOKEN → action: surface diff, request token.
3. **scheduler remediation** — needs A1 buildkit release + per-host deploy (cross-host) → coordinator/operators.
4. **#24/#30/#34/#35/#59** — blocked-by #1 → clear when Wave 1 ships.

## The two mandatory 3rtask work-products (already produced, folded in)
- **Scheduler rootcause + durable remediation** — Programme B decision brief (`docs/handover/coordination-remediation-programmeB-decision-brief-2026-08-17.md`, `c4976297`) + A1–A4 engineer rulings → Wave 2.
- **Consolidated-hardening charter** — 3rtask run `d920` curator_report (7 capabilities → one spine; 4 contracts resolved) → Wave 0 (codify+roadmap) then Wave 5 (build).

## Refinements (JIT engineer rulings, 2026-08-17)
- **079** ships as a **US3+US2 increment now** (FR-001 deferred per C1); **US1** is its own SHIP-TOKEN-gated follow-up (de-risks the release from the R-1b core-touch uncertainty).
- **#1 verification-receipts** builds **MVP receipt-primitive first** (EMPTY/UNREAD/UNSEARCHABLE + fault-injection harness → fastest unblock of the 5 dependents), then per-seam rollout across the 6 seams.
- **consolidated-hardening-spine** stays **Wave 5** (builds after #1 + traceability items harden its component capabilities).
- **Connectivity/Gleam overlap cluster** (#43/#33/#32/#34/#35/#54/ynet): a cheap read-only **shipped-state verification** (git log / suites / spec status) runs **before Wave 4** to catch done-but-not-closed items and avoid re-doing delivered work.

## Execution protocol (JIT engineer rulings, tier 3)
- **079 baseline**: baseline the **multiagent Dart suite** (`dart test test/multiagent/`) + REPL suite **through Section S**; the **Section-T abort is pre-existing/orthogonal** (064 unguarded-abort + missing glpquick.pfx) — do NOT pull 064 into Wave 0.
- **Wave 2 (scheduler)**: does NOT block the program — **Waves 3–5 proceed in parallel**; Wave 2 stays **coordinator-pending**, unblocks on gavriella's COOP confirm of the A1+A2 release train + per-host deploy.
- **Marathon shape**: keep the ONE program-marathon (`mrun-7f0b400450f3`) as portfolio index (roadmap authoritative); **spawn a dedicated per-feature marathon ONLY for the LARGE items** (#1 verification-receipts, consolidated-hardening-spine, scheduler) for fine checkpoint/resume. Light closes/chores stay roadmap-tracked.
- **Wave 0 autonomy**: the fresh session **runs each item to its natural gate autonomously**; JIT-question the engineer only at a genuine block/tension or a ship gate (ship gates always STOP for SHIP-TOKEN).

## Safe-restart protocol
On restart: mandatory reading → `buildkit-marathon status --feature glpnet-consolidated-completion` (position from durable rows) → resume the current wave/item at its pipeline stage → JIT structured questions to the engineer at every open block. Ship gates always STOP for SHIP-TOKEN.
