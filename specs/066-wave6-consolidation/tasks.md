<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Tasks: Wave-6 roadmap consolidation

**Input**: Design documents from `specs/066-wave6-consolidation/` (plan.md, research.md D1–D7,
data-model.md, contracts/gate-ledger.md + disposition-protocol.md, quickstart.md)
**Prerequisites**: constitution v1.1.0 PASS (plan.md); marathon mrun-6dc97a88c769 open

**Organization**: tasks grouped by user story (US1–US6 = spec P1–P6). Ruling/gate-parked
tasks are marked ⛔GATE — they may not start until their ledger gate row is cleared with
evidence (FR-003/FR-005); parked is a normal, recorded state, not a failure.

## Phase 1: Setup

- [x] T001 Baseline checkpoint: verify tree clean on `066-wave6-consolidation`, record current suite baselines in the marathon trace (full REPL 551/551 @ 51be73c5-era, glp_link 172, glp_crdtmsg 188, gleam 569 — re-run only what a story later touches; note counts in trace row)
- [x] T002 Evidence inventory in `specs/066-wave6-consolidation/evidence-inventory.md`: enumerate rulings R1–R12 (5 × 3rtask run 20260803T134739Z-fa8a escalations.md + 7 × 064 codexreview adjudications, with source paths/stamps) and the current peer receipts (ariellas 153205Z/153920Z/205616Z; olamnit position) — the ledger's evidence sources

## Phase 2: Foundational

*(No further blocking work — the gate ledger itself is US1, first story below.)*

## Phase 3: US1 — Gate ledger (P1) 🎯 MVP

**Goal**: every one of the 18 snapshot items mapped; gates visible with blocked-story lists.
**Independent test**: contract invariants 1–6 of `contracts/gate-ledger.md` all hold; spot-check
three rows' evidence links resolve.

- [x] T003 [US1] Create `specs/066-wave6-consolidation/gate-ledger.md` gates table: G1 (064 ship-state, open, blocks S4-link stories), G2 (065 track, open, blocks S6 YNET rows), G3.R1–R12 (each with source ref from T002, blocked-story list), EXT.ariellas (open, blocks T015/T016 receipt-consumption starts) — per contracts/gate-ledger.md
- [x] T004 [US1] Fill the 18-row items table from the 150440Z snapshot (recompute the not-closed set via `python -m buildkit_cli.roadmap --json status` and reconcile against the snapshot list; any drift is recorded, snapshot stays the boundary per D1/assumptions), mapping each to story/external-gate/triage with initial state
- [x] T005 [US1] Mechanical completeness check: verify contract invariants 1–6 (18 rows, no empty disposition cells, parked⇒blocked_by, terminal⇒evidence) and record the check transcript in the marathon trace; commit ledger + inventory (file-scoped)

**Checkpoint**: ledger authoritative — all later tasks update it in the same commit as their event. Marathon discipline (FR-010): every story close = a marathon checkpoint/trace row; every disposition = a trace row; parked items = marathon park/sequence, so cold resume derives position from durable rows alone.

## Phase 4: US2 — Standalone quick wins (P2)

**Goal**: atomic-toolchain-installs + batch-roadmap-advance closed.
**Independent test**: each item's own verification green; roadmap rows closed; receipts published.

- [x] T006 [P] [US2] atomic-toolchain-installs implemented — buildkit branch feat/atomic-toolchain-installs @ 554836f6 pushed (fresh-venv + junction flip + post-flip verify + rollback at the ship/release reinstall seam — the repo's only pip-based toolchain seam; deploy installer already atomic, documented); 11/11 new tests + ship package 204/204
- [ ] T007 [US2] Dispose `atomic-toolchain-installs-venv-swap-post-install-smoke` — ⛔GATE(engineer landing): roadmap advance → closed once feat/atomic-toolchain-installs lands on the buildkit side (merge/ship = engineer)
- [x] T008 [P] [US2] batch-roadmap-advance + CalVer normalisation implemented — buildkit branch feat/batch-roadmap-advance-calver-normalisation @ 634d4a0a pushed (multi-id + --from/--all single-window batch, single-id contract preserved; normalize at read/compare seams + reuse-before-create at install); +13 tests green (1 pre-existing baseline failure reproduced on clean tree)
- [ ] T009 [US2] Dispose `batch-roadmap-advance-calver-version-dir-normalisation` — ⛔GATE(engineer landing): advance → closed once feat/batch-roadmap-advance-calver-normalisation lands on the buildkit side

## Phase 5: US3 — Promoted singletons (P3)

**Goal**: glp-runtime-consol + qr-link-provisioning delivered or engineer-deferred.
**Independent test**: per-item acceptance from spec US3 scenarios.

- [x] T010 [US3] glp-runtime-consol: read `docs/` handover seeded at develop HEAD (`git log --oneline -1 bc5ea232` — "glp-runtime-consol pipeline restart handover, 3rtask gap-audit seed") + the roadmap brief; build the inventory of specified-but-unimplemented runtime features with per-feature disposition proposal (implement here vs defer with rationale). LANGUAGE-AUTHORITY SCREEN (constitution IV-a): any inventory entry touching the GLP language surface (guards, system predicates, body kernels, directives, type-system features, primitive types) is PROPOSAL-ONLY — surfaced for the owner's explicit approval, never implement-here
- [x] T011 [US3] glp-runtime-consol: implement the inventory's implement-here set — DONE: implement-here set = sub-scope (B) only (dead stub `out/csharp/lib/runtime/abandon.cs` → error-level [Obsolete] tombstone, zero live call sites verified; dotnet build 0 errors, glp_link 165/165 + glp_crdtmsg 184/184 at develop baseline); sub-scope (A) superseded by the Option-B rider — see runtime-consol-inventory.md
- [x] T012 [US3] Dispose glp-runtime-consol — DONE: advance → closed; evidence = runtime-consol-inventory.md ((B) implemented, (A) superseded per the recorded engineer-directed rider); flagged in the wave report for engineer reversal if the (A)-drop reading is wrong
- [x] T013 [P] [US3] qr-link-provisioning: brief read — SUPERSEDED BY GRADUATION PROPOSAL: the item carries mandatory non-waivable security hardening scope (Gabi correction 2026-07-08: permanent trunk-credential posture — derived short-lived per-device material, encrypted payloads, audit+revocation as PREconditions) + an out-of-repo Android consumer; the brief itself hands off to a standalone /bk-specify. In-wave delivery with loopback acceptance would violate the item's own posture. Proposal packaged in the ledger (ITEM-05); engineer confirm graduates it to its own feature
- [ ] T014 [US3] Dispose qr-link-provisioning per protocol (ledger + evidence + receipt) — ⛔GATE(engineer): executes the graduation decision (deferred-to-own-feature) once confirmed

## Phase 6: US4 — ANTLR4 shared-grammar spike (P4)

**Goal**: recorded go/no-go with evidence; chain gate decision.
**Independent test**: spike report exists; engineer decision recorded; roadmap row advanced.

- [ ] T015 [US4] Run the spike per the roadmap brief and D6: one grammar, ≥2 targets (Dart + C# minimum), measured against the profile's criteria; write the report (evidence, measurements, go/no-go recommendation) into `specs/066-wave6-consolidation/spike-antlr4-report.md`
- [ ] T016 [US4] Surface the report to the engineer; record the decision (marathon gate present/decide + ledger); on go → clear the chain gate row; on no-go → invoke the confirmed re-plan config-item policy; advance the roadmap row accordingly

## Phase 7: US5 — Full-Gleam chain (P5) ⛔GATE (spike go + EXT.ariellas receipts + G1 for link surfaces)

**Goal**: chain advanced strictly in blocked-by order, consuming ariellas receipts.
**Independent test**: per-link green gates in order; zero receipt-covered surfaces rebuilt (SC-002).

- [ ] T017 [US5] ⛔GATE(spike) glp-gleam-compiler-and-loader: scope per roadmap brief against the spike decision; implement in `glp_gleam/` under gleam-suite gates; green before T018
- [ ] T018 [US5] ⛔GATE(T017) glp-gleam-bytecode-runner: per brief, `glp_gleam/` runner/engine work; gleam suite green before T019
- [ ] T019 [US5] ⛔GATE(T018) glp-gleam-repl: standalone Gleam GLP REPL per brief (deps: runner, compiler+loader, result-envelope, structured-output seam — verify those receipt/ship states first); green before T020
- [ ] T020 [US5] ⛔GATE(T019) glp-test-corpus-port-and-runner: shared corpus ported per brief; corpus runner green before T021
- [ ] T021 [US5] ⛔GATE(T020, EXT.ariellas-US1) glp-gleam-link-layer: verify ariellas' US1 receipts locally (D2), build only the receipt-verified delta in `glp_gleam/src/glp/link` (second-lander rebases on shared files); board-escalate any receipt gap
- [ ] T022 [US5] ⛔GATE(T021, EXT.ariellas-US2, G1) cross-runtime-csharp-gleam-distributed-tests: verify ariellas US2 + 064 ship state; delta-only cross-runtime suite work (`test/parity/cross_runtime/` conventions); 10-loop stability per the 060 norm
- [ ] T023 [US5] ⛔GATE(T022) full-scope-gleam-glp-implementation: reconcile specs/059 acceptance against everything delivered (incl. ariellas US4 059-sweep receipts); dispose (advance or explicit remainder-defer with engineer rationale); ledger + receipts per protocol

## Phase 8: US6 — Captured-intake triage (P6) ⛔GATE(G2 for the two YNET rows)

**Goal**: 3 captured items leave captured state via recorded engineer decisions.
**Independent test**: no captured-state rows at wave close; decisions recorded.

- [x] T024 [P] [US6] buildkit-coordination-optimisation-gepa-dspy: fill profile (`edit-feature`), run review propose-scores, package build/defer/reject proposal for the engineer per D7 — DONE: profile already complete; agent-proposed scores + DEFER-ON-THIS-HOST (peer-led, ariellas queue) proposal packaged in triage/US6-proposals.md; decision = engineer's (T027)
- [ ] T025 [P] [US6] ⛔GATE(G2) ynet-human-memorable-decentralized-naming-resolver: same triage packaging (profile + scores + proposal), noting 065-track dependency
- [ ] T026 [P] [US6] ⛔GATE(G2) ynet-mobile-background-battery-budget-scheduling-policy: same triage packaging
- [ ] T027 [US6] Record the engineer's three decisions (roadmap command per decision, ledger rows, marathon trace); a build decision graduates to promoted for a follow-on feature (FR-009), never into wave-6

## Phase 9: Polish & wave close

- [ ] T028 Wave-close sweep: ledger invariant check (SC-001: 0 items non-terminal or documented-parked-on-open-ruling), SC-002 receipt-duplication spot-check on S4 closes, SC-004 ruling-visibility check
- [ ] T029 Mid-wave resume drill (SC-005): cold `marathon resume` + quickstart.md path reaches the correct next action without re-running completed stories; record transcript in trace
- [ ] T030 Final sync round (import/reconcile/dedupe/export/replay-verify) + wave receipts fanned; COOP status stamped with the ledger summary

## Dependencies & execution order

- T001→T002→(T003→T004→T005)=US1 gate → everything else.
- US2 (T006–T009) and US3 (T010–T014) independent of G1/G2 — may run immediately after US1;
  T006∥T008, T010-chain∥T013.
- US4 (T015–T016) independent — may run parallel to US2/US3.
- US5 strictly serial T017→…→T023, start gated on T016-go + EXT receipts (+G1 at T022).
- US6 T024∥T025∥T026 (gated rows on G2) → T027.
- T028–T030 last.
- ⛔GATE tasks park per FR-003 — a parked chain does NOT block US2/US3/US6 progress (D3).
- Sync discipline (FR-007): EVERY story close (each phase checkpoint) triggers a sync round
  (import/reconcile/export/replay-verify) publishing its dispositions; T030 is the FINAL round,
  not the only one.

## Implementation strategy

MVP = US1 (the ledger) — it alone delivers the wave's visibility promise and satisfies FR-001.
Then maximize unblocked throughput (US2/US3/US4 in parallel lanes) while gates clear; US5
consumes gates as they open; US6 proposals can go to the engineer early so decisions return
while other lanes run. Every disposition follows contracts/disposition-protocol.md and updates
the ledger in the same commit.
