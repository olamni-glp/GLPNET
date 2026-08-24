<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: gate ledger

The ledger is the wave's single index surface (US1 deliverable). Instance lives at
`specs/066-wave6-consolidation/gate-ledger.md`.

## Format

Two markdown tables.

**Gates table** — one row per gate:

```
| gate_id | kind | state | evidence | blocks |
```

Required rows at creation: `G1` (064 ship-state), `G2` (065 track), one `G3.Rn` row per open
ruling (R1..R12 at snapshot time), `EXT.ariellas` (064-post-wave-gap-closure ownership).

**Items table** — one row per snapshot item, exactly 18 rows:

```
| item_id | group | disposition_path | state | blocked_by | evidence |
```

## Invariants (mechanically checkable)

1. Exactly 18 item rows; item_id set equals the 150440Z snapshot's not-closed set.
2. Every item row: `disposition_path` ∈ {story, external-gate, triage}; no empty cells in
   `disposition_path`/`state`.
3. `state=parked` ⇒ `blocked_by` non-empty and every referenced gate/ruling row has
   `state=open`.
4. `state ∈ {closed, deferred, rejected, superseded}` ⇒ `evidence` non-empty.
5. `state=active` ⇒ no `blocked_by` entry references an open gate/ruling.
6. Gate `state=cleared` ⇒ `evidence` non-empty.

## Update discipline

- The ledger changes in the same commit as the event it records (disposition, park, unpark).
- Rows are never deleted; a superseded mapping is struck through with the replacement noted.
- Peer-visible: ledger state summarized in COOP status stamps at each story close.
