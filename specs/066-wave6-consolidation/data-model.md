<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Data Model: Wave-6 roadmap consolidation

The wave introduces no new persistent schema. Its entities live in the gate ledger (git
markdown, contract: `contracts/gate-ledger.md`), the existing roadmap journal (CRDT rows),
and the existing marathon run (mrun-6dc97a88c769). This file defines the logical model those
surfaces share.

## WaveItem

One of the 18 rows of the 150440Z not-closed snapshot.

| Field | Meaning | Constraints |
|---|---|---|
| item_id | roadmap feature_id | immutable; must exist in the roadmap journal |
| group | S1..S6 story group or EXTERNAL | exactly one |
| disposition_path | story / external-gate / triage | exactly one (FR-001) |
| state | pending → active → parked(ruling/gate) → terminal | terminal = closed \| deferred \| rejected \| superseded |
| evidence | receipt stamp(s), commit hash(es), roadmap row state | required non-empty at terminal (FR-002) |
| blocked_by | gate ids + ruling ids | may be empty; parked requires non-empty |

**State transitions**: pending→active (gates clear); active→parked (ruling/gate opens
mid-story); parked→active (ruling lands); active→terminal (disposition recorded). Terminal is
final for the wave; a revived item is a wave-7 concern.

## Gate

| Field | Meaning | Constraints |
|---|---|---|
| gate_id | G1, G2, G3.<ruling-n>, EXT.ariellas | unique |
| kind | ship-state / track / ruling / external-ownership | — |
| state | open / cleared | cleared requires evidence |
| evidence | receipt / tag / ruling text reference | required at cleared |
| blocks | list of item_ids or story ids | maintained current (SC-004) |

## ExternalReceipt

| Field | Meaning | Constraints |
|---|---|---|
| stamp | COOP artifact UTC stamp | mechanical (C1a) |
| source | peer host | ariellas / olamnit |
| claim | what the receipt asserts | — |
| verified_by | local re-verify evidence (suite run, tag check) | required before a story consumes it (D2) |

## Ruling

| Field | Meaning | Constraints |
|---|---|---|
| ruling_id | stable short id (R1..R12) | maps to the adjudication lists: 5 × 3rtask-fa8a escalates + 7 × 064-review items |
| state | open / landed | landed records the engineer's decision text reference |
| blocks | story/item ids | surfaced in the wave status at all times (FR-003) |

## Validation rules

- Ledger completeness: exactly 18 WaveItem rows; every row has exactly one disposition_path
  (FR-001 / SC-001 mechanical check).
- No terminal WaveItem without evidence (FR-002).
- No active WaveItem whose blocked_by contains an open gate/ruling (FR-003/FR-005).
- Every consumed ExternalReceipt has verified_by before first use (FR-004 / D2).
