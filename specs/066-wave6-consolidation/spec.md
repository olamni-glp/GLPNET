<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature Specification: Wave-6 roadmap consolidation

**Feature Branch**: `066-wave6-consolidation`
**Created**: 2026-08-03
**Status**: Draft
**Input**: User description: "wave-6 roadmap consolidation — consolidated wave over every not-closed roadmap item (18-item snapshot 150440Z), per the drafted description in the session scratchpad (wave6-specify-description.md): gates G1 064 ship-state / G2 065 completion / G3 open rulings; story groups S1 quick wins, S2 promoted singletons (post-wave gap closure CARVED OUT to ariellas), S3 ANTLR4 spike, S4 Full-Gleam chain in dependency order (consuming ariellas US1/US2 receipts), S5 captured intake"

## Overview

Wave-6 is the sixth consolidated roadmap wave (tradition of waves 1–5, specs 059–063): one
feature that drives **every not-closed roadmap item** (snapshot 2026-08-03T150440Z, 18 items)
to a terminal disposition — *closed*, or *explicitly deferred/rejected with a recorded
rationale*. Nothing is silently dropped, and work already owned elsewhere is consumed, never
duplicated.

### Gates (prerequisites; NOT re-specified scope)

- **G1 — 064 ship-state gate** (finish, don't rebuild): durable-listener-service-box must be
  shipped (v2026.08.03.2 announced, engineer keystroke) and closed before wave-6 stories that
  touch its surfaces begin. Precedent: 065's P1.
- **G2 — 065 completion gate**: YNET--consolidation (specs/065, marathon mrun-7939e12b5b70)
  proceeds on its own track; wave-6's YNET-adjacent items sequence behind it. 065's stories are
  themselves gated by the engineer's five 3rtask escalate rulings (its FR-008), which therefore
  cascade into wave-6.
- **G3 — open-ruling gate**: the engineer's open adjudication items (the five 3rtask escalates
  from run 20260803T134739Z-fa8a and the seven 064-review adjudication items) block any story
  whose direction they decide; a blocked story waits, it is never self-resolved.
- **External-ownership gate (ariellas)**: the roadmap feature
  *post-wave-consolidation-verified-gap-closure-repl-engine-full-gleam* is being delivered by
  ariellas as their feature 064-post-wave-gap-closure (mrun-35df7ddfe4ec; carve-out fanned
  153613Z, lead-confirmed 153920Z). Wave-6 treats it as an external gate; the Gleam-link and
  cross-runtime stories consume ariellas' US1/US2 receipts and sequence after their implement
  lands; their US4 059-acceptance sweep may partially discharge
  full-scope-gleam-glp-implementation and is reconciled from receipts.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Ship-state and ownership gates verified (Priority: P1)

As the engineer, before any wave-6 build work starts I can see, with evidence, that the gates
hold: 064 shipped+closed, 065's track state, ariellas' gap-closure receipts consumed, and each
open ruling mapped to the stories it blocks — so no wave-6 story rebuilds finished work or
duplicates a peer's.

**Why this priority**: every later story's scope depends on what the gates discharge; skipping
this is how duplicate/conflicting work happens (the wave-3 lesson).

**Independent Test**: produce the gate ledger (one row per gate: evidence link, state,
blocked stories); verify 064's ship/close receipts and ariellas' seam receipts are referenced,
and that every open ruling appears with its blocked story list.

**Acceptance Scenarios**:

1. **Given** the roadmap snapshot and the peers' receipts, **When** the gate ledger is built,
   **Then** every one of the 18 items maps to exactly one of: a wave-6 story, an external
   gate (ariellas / 064 / 065), or a captured-intake triage row.
2. **Given** an open engineer ruling, **When** a story it gates is reached, **Then** the story
   is parked with the ruling named, and work proceeds only on unblocked stories.

---

### User Story 2 - Standalone quick wins closed (Priority: P2)

The two refined no-blocker standalone items ship and close: *atomic toolchain installs (venv
swap + post-install smoke)* (WSJF 5.7) and *batch roadmap advance + CalVer version-dir
normalisation* (WSJF 3.7).

**Why this priority**: highest WSJF, zero dependencies, immediate operator value; they also
exercise the wave's per-item close discipline early.

**Independent Test**: each item independently reaches closed on the roadmap with its own
verification green and receipts published.

**Acceptance Scenarios**:

1. **Given** a quick-win item, **When** its build completes, **Then** its verification gate is
   green, the roadmap row advances to closed, and a receipt is published to the fleet.

---

### User Story 3 - Promoted singletons delivered (Priority: P3)

The two promoted singletons that remain this host's: *glp-runtime-consol* (complete
specified-but-unimplemented GLP runtime features) and *qr-link-provisioning* (QR-code link +
cert provisioning via generated PDF or hub display page, WSJF 4 / RICE 252).

**Why this priority**: promoted state means the roadmap review already judged them ready;
they unblock nothing downstream but carry standing engineer intent.

**Independent Test**: each delivers against its roadmap profile and reaches closed (or an
engineer-recorded defer) independently of the other.

**Acceptance Scenarios**:

1. **Given** glp-runtime-consol's specified-but-unimplemented inventory, **When** the story
   completes, **Then** each inventoried runtime feature is implemented+verified or explicitly
   deferred with rationale.
2. **Given** the qr-link-provisioning profile, **When** delivered, **Then** a peer can
   establish a link + certificate from the generated artifact (PDF or hub display) as profiled.

---

### User Story 4 - ANTLR4 shared-grammar spike (Priority: P4)

The *ANTLR4 shared-grammar multi-target spike* (RICE 640) runs to its spike conclusion —
a recorded go/no-go with evidence — because it is also the declared prerequisite of the Gleam
compiler+loader chain.

**Why this priority**: it gates the whole S4 chain; as a spike its output is a decision, not
a product, so it is cheap relative to what it unblocks.

**Independent Test**: the spike report exists with a go/no-go recommendation, evidence, and
the engineer's recorded decision; the roadmap row advances accordingly.

**Acceptance Scenarios**:

1. **Given** the spike question (one grammar, multiple targets), **When** the spike concludes,
   **Then** the recommendation + evidence are recorded and the engineer's decision determines
   whether the Gleam compiler+loader story unblocks or re-plans.

---

### User Story 5 - Full-Gleam chain advanced in dependency order (Priority: P5)

The Full-Gleam chain advances strictly in its roadmap dependency order — compiler+loader
(after the spike) → bytecode runner → REPL → test-corpus port → link layer → cross-runtime
tests → full-scope acceptance (specs/059) — with the link-layer and cross-runtime stories
CONSUMING ariellas' delivered US1/US2 receipts rather than re-implementing them.

**Why this priority**: largest value block but longest chain; every earlier priority either
gates it (spike) or feeds it (ariellas' receipts, 064's link surfaces).

**Independent Test**: each chain link independently reaches its own green gate before the next
starts; no wave-6 commit re-implements a surface ariellas' receipts already cover; blocked-by
edges on the roadmap are respected in the recorded order of work.

**Acceptance Scenarios**:

1. **Given** ariellas' US1/US2 receipts, **When** the link-layer story starts, **Then** its
   scope is the receipt-verified delta only, and any gap found is reported to the board rather
   than silently rebuilt.
2. **Given** the chain order, **When** any link's gate is red, **Then** downstream links do
   not start and the blocker is surfaced.

---

### User Story 6 - Captured intake triaged (Priority: P6)

The three captured items — *YNET human-memorable decentralized-naming resolver*, *YNET mobile
background/battery-budget scheduling policy*, *buildkit coordination optimisation (GEPA/DSPy)* —
are profiled and refined to an engineer decision: build (graduating into a follow-on feature),
defer, or reject, each with recorded rationale.

**Why this priority**: intake hygiene closes the wave honestly; captured items must not ride
along unexamined, and the two YNET items additionally sit behind 065's track (G2).

**Independent Test**: each captured item's roadmap row leaves the captured state via a
recorded engineer decision; none remains captured at wave close.

**Acceptance Scenarios**:

1. **Given** a captured item, **When** triage completes, **Then** its profile is filled, a
   WSJF/RICE proposal exists, and the engineer's build/defer/reject decision is recorded.

---

### Edge Cases

- Ariellas' 064-post-wave-gap-closure slips or its scope changes: the external gate re-opens;
  affected S4/S5 stories re-park and the board is notified — wave-6 never silently absorbs the
  peer's scope.
- An open ruling lands mid-wave and reverses a story's direction: the story re-plans from the
  ruling before any further commits; already-landed work that contradicts the ruling is
  surfaced to the engineer, never silently reverted.
- A roadmap item is discovered to be obsolete/superseded mid-wave: it takes the
  defer/reject/supersede path with rationale — terminal disposition, not silent removal.
- The roadmap snapshot drifts (new captures land during the wave): new items are NOT wave-6
  scope; they are recorded for the next wave, keeping the 18-item boundary stable.
- A CalVer ship collides with a peer's claim: the existing fleet CalVer claim/confirm protocol
  applies; ships remain engineer-keystroke only.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The wave MUST produce a gate ledger mapping all 18 snapshot items to exactly one
  of: wave-6 story, external gate (064 / 065 / ariellas' gap-closure), or captured-intake
  triage — with evidence links.
- **FR-002**: Every wave-6 item MUST reach a terminal disposition: closed, or
  deferred/rejected/superseded with recorded engineer rationale. No silent drops.
- **FR-003**: Stories gated by an open engineer ruling MUST park until the ruling lands; the
  ruling and its blocked stories MUST be visible in the wave's status surface at all times.
- **FR-004**: Wave-6 MUST NOT re-implement any surface covered by ariellas'
  064-post-wave-gap-closure receipts; link-layer/cross-runtime stories consume receipts and
  build only the verified delta. Gaps found in receipts are board-escalated, not rebuilt.
- **FR-005**: The Full-Gleam chain MUST advance in the roadmap's blocked-by order; a red gate
  on any link blocks all downstream links.
- **FR-006**: Every story completion MUST show the repo's quality gates green at its
  checkpoint (full REPL suite at current baseline, affected unit suites, affected drills)
  before its roadmap advance.
- **FR-007**: Roadmap advances, syncs (import/reconcile/dedupe/export/replay-verify), and
  receipts MUST be published per the established fleet sync protocol after each story close.
- **FR-008**: Ships and releases within the wave MUST be engineer-keystroke only, with CalVer
  announced and tag-verified per fleet protocol before each cut.
- **FR-009**: Captured-intake triage MUST record profile, score proposal, and the engineer's
  build/defer/reject decision per item; a build decision graduates the item to a follow-on
  feature, never silently into wave-6 scope.
- **FR-010**: The wave MUST maintain a durable, resumable run record (marathon) with per-story
  checkpoints so a session loss never loses position.

### Key Entities

- **Gate ledger**: one row per gate/item — evidence link, state, blocked stories.
- **Wave item**: a not-closed roadmap row in the 150440Z snapshot, with its disposition path.
- **External receipt**: a peer-published seam/ship receipt consumed as evidence (ariellas'
  US1/US2/US4; 064 ship/close receipts).
- **Ruling**: an open engineer adjudication item with the story set it blocks.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: At wave close, 0 of the 18 snapshot items lack a terminal disposition; 100% of
  deferrals/rejections carry a recorded rationale.
- **SC-002**: 0 wave-6 commits duplicate a surface covered by a consumed peer receipt
  (spot-checked at each link-layer/cross-runtime story close).
- **SC-003**: Every story close shows its quality gates green at the checkpoint; the wave
  introduces 0 regressions to the repo's baseline suites.
- **SC-004**: Every ruling-blocked story is visibly parked with its ruling named within one
  status surface; 0 rulings are self-resolved by the wave.
- **SC-005**: A cold session resume from the wave's durable run record reaches the correct
  next action without re-running any completed story (verified at least once mid-wave).

## Assumptions

- The 150440Z snapshot (18 not-closed items) is the wave boundary; later captures belong to
  wave-7+.
- 064 ships as v2026.08.03.2 (lead-confirmed uncontested) on the engineer's keystroke before
  wave-6's link-surface stories start; if the ship slips, G1 parks those stories only, not the
  whole wave.
- Ariellas delivers 064-post-wave-gap-closure on their announced track; the second-lander-
  rebases norm (agreed 153920Z) governs any shared-file collision.
- 065 continues on its own branch/marathon; wave-6 does not absorb its stories.
- The engineer's rulings may land in any order; the wave plan treats each as an independent
  unblock event.
- Quick wins and singletons (S1/S2) have no dependency on G1/G2 and may start immediately
  after US1's ledger exists.
