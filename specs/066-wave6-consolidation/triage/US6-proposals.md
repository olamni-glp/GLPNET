<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# US6 captured-intake triage proposals (T024–T027)

Per contracts/disposition-protocol.md: these are PROPOSALS — only the engineer's recorded
decision makes a disposition terminal (FR-009). Score inputs are agent-proposed (D7),
labeled as such, for the engineer to confirm or override.

## T024 — buildkit-coordination-optimisation-gepa-dspy (unblocked lane)

**Profile state**: already complete on the roadmap (problem/value/effort=medium/risk=medium/
notes) — no edit-feature needed. Declared lead: **ariellas** (their status queue already
carries "GEPA" as an engineer-gated item on their host).

**Agent-proposed scores** (for engineer confirm): WSJF ≈ 2.0 (cost-of-delay moderate —
friction is chronic not acute; job size medium). RICE ≈ 120 (reach: 3 hosts' pipeline runs;
impact: medium; confidence: medium — GEPA/DSPy engine availability varies per host; effort:
medium).

**Proposal: DEFER-ON-THIS-HOST (recommend)** — the item is ariellas-led by its own notes and
already sits on their engineer-gated queue; wave-6 building it here would duplicate their
lead and violate the carve-out spirit (FR-004 analog). Concrete disposition: roadmap state
stays captured→refined under ariellas' lead; wave-6 records "deferred (peer-led)" with this
rationale once the engineer confirms. Alternative if the engineer prefers: reject-here +
explicit hand-off note to ariellas' queue.

## T025 — ynet-human-memorable-decentralized-naming-resolver ⛔G2 (packaging deferred)

Parked: 065's track (G2) owns YNET sequencing and its 5-escalate gate cascades. Proposal
packaging will follow once G2 clears or the engineer directs early triage.

## T026 — ynet-mobile-background-battery-budget-scheduling-policy ⛔G2 (packaging deferred)

Same as T025.

## T027 — engineer decisions (pending)

| item | proposal | engineer decision | recorded via |
|---|---|---|---|
| coordination-optimisation | defer-on-this-host (peer-led) | — | roadmap note + ledger ITEM-10 |
| ynet-naming-resolver | (awaits G2) | — | — |
| ynet-mobile-scheduling | (awaits G2) | — | — |
