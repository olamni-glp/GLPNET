<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Research: Wave-6 roadmap consolidation

No NEEDS CLARIFICATION markers existed in the spec; research consolidates the orchestration
decisions the plan relies on. All decisions below are wave-internal defaults; the four
engineer config items recorded at specify time (wave-boundary policy, S1 start gate,
graduation target, spike no-go re-plan policy) can override them when confirmed.

## D1 — Gate ledger form

**Decision**: a single git-tracked markdown table at
`specs/066-wave6-consolidation/gate-ledger.md`, one row per snapshot item (18 rows) +
one row per gate (G1/G2/G3/external), each row carrying: item id, disposition path
(story / external gate / triage), state, evidence link (receipt stamp, commit, or roadmap
row), and blocked-by (ruling ids where applicable).
**Rationale**: mechanical completeness check (18 rows, no blanks) satisfies FR-001/SC-001;
git-tracked = fleet-visible + survives sessions; references (not duplicates) roadmap rows —
constitution VIII.
**Alternatives considered**: PGlite-only ledger (rejected: not diffable/reviewable on the
wire); per-item files (rejected: completeness check becomes a directory walk, weaker).

## D2 — Receipts consumption method

**Decision**: peer receipts are consumed by stamp reference — a ledger evidence cell cites the
COOP artifact (e.g. ariellas seam UPDATE 153205Z, CONFIRM 153920Z, future US1/US2 ship
receipts) plus, where code is consumed, the peer's pushed commit hash. A story consuming a
receipt re-verifies the claim locally (run the relevant suite) before building its delta.
**Rationale**: matches the established fleet norm (receipts on the board, independent
re-verify before trust — the CalVer/tag re-check precedent); keeps FR-004 auditable.
**Alternatives considered**: trusting receipts without local re-verify (rejected: wave-3's
"18/18 was timing luck" lesson).

## D3 — Ordering policy within and across story groups

**Decision**: across groups, priority order P1→P6 gates starts but does NOT serialize
everything: S1/S2 (no G1/G2 dependency) may run while S4 is parked on gates. Within a group,
WSJF descending; the Gleam chain strictly follows roadmap blocked-by edges. A red gate on a
chain link blocks downstream links only (FR-005), never unrelated groups.
**Rationale**: matches the spec's Assumptions; maximizes safe parallel progress under gates.
**Alternatives considered**: strict global priority serialization (rejected: parks the whole
wave on the 064 keystroke for no integrity gain).

## D4 — Terminal-disposition mechanics

**Decision**: closed = roadmap `advance` through its states with the item's own verification
green at the checkpoint; deferred/rejected/superseded = the corresponding roadmap command with
`--rationale`-style note recorded in the ledger row AND a marathon trace row. Every
disposition publishes in the next sync round's export.
**Rationale**: the roadmap journal is the fleet's system of record (CRDT-synced); the ledger
is the wave's index into it — no duplicate truth.
**Alternatives considered**: wave-local disposition file only (rejected: invisible to peers,
violates the sync protocol norm).

## D5 — Ship cadence

**Decision**: roadmap item closes do NOT each require a CalVer cut. The wave branch ships via
buildkit GitFlow (engineer keystroke) when a coherent increment lands (default: one ship at
wave end; quick wins MAY ship earlier if the engineer chooses). Every cut follows the fleet
CalVer announce/tag-verify protocol.
**Rationale**: wave-1..5 precedent (one ship per wave feature); FR-008 keeps every cut
engineer-gated regardless of cadence.
**Alternatives considered**: per-story ships (rejected as default: keystroke overhead without
integrity gain; remains available to the engineer).

## D6 — Spike output form (S3/ANTLR4)

**Decision**: the spike produces a written report in the ledger's evidence trail (go/no-go +
evidence + measured criteria from the item profile: one grammar, multiple targets — Dart/C#
minimum) and an explicit engineer decision gate before the Gleam compiler+loader story
unblocks. On no-go, the chain re-plans per the recorded config-item policy (engineer-
confirmed) rather than improvising.
**Rationale**: RICE 640 rests on the multi-target claim; a spike that just "tries things"
without a decision record would leave the chain gate ambiguous.
**Alternatives considered**: folding the spike into the compiler story (rejected: hides the
go/no-go decision the roadmap explicitly modeled as a separate refined item).

## D7 — Captured-intake triage mechanics (S6)

**Decision**: per captured item: fill the roadmap profile (`edit-feature`), run a review
`propose-scores` for WSJF/RICE proposals, surface build/defer/reject to the engineer with the
proposal, record the decision via the matching roadmap command. A build decision graduates the
item to the roadmap (promoted) for a follow-on feature — never into wave-6 scope (FR-009).
**Rationale**: uses the roadmap's own review surfaces; keeps the engineer the deciding layer.
**Alternatives considered**: none serious.
