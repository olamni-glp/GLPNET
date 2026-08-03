<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Quickstart: Wave-6 roadmap consolidation

## Where am I? (any session, cold start)

```
python -m buildkit_cli.marathon resume            # run mrun-6dc97a88c769; objective position
python -m buildkit_cli.pipeline.cli status        # stage state for 066-wave6-consolidation
```

Then open `specs/066-wave6-consolidation/gate-ledger.md` (exists once US1 lands): the items
table is the wave's truth for what is active/parked/terminal, and every parked row names its
blocker.

## What's next?

1. If the ledger doesn't exist yet → US1 (build it per `contracts/gate-ledger.md`).
2. Else → the highest-priority unblocked story with pending items (D3 ordering: S1/S2 may
   run while S4 parks on gates; Gleam chain strictly by blocked-by edges).
3. A gate cleared since last session (064 shipped, a ruling landed, an ariellas receipt
   posted)? → update the gate row with evidence, un-park its stories, re-derive next.

## Closing an item

Follow `contracts/disposition-protocol.md` — suites green, roadmap advanced, ledger row +
evidence in the same commit, sync round after. Engineer keystrokes: any ship/release; any
defer/reject decision.

## Gates at a glance (snapshot 2026-08-03)

- **G1** 064 ship (v2026.08.03.2) + close — engineer keystroke pending.
- **G2** 065 track (specs/065, mrun-7939e12b5b70) — its 5-escalate gate cascades here.
- **G3** rulings R1–R12 open (5 × 3rtask-fa8a + 7 × 064-review adjudications).
- **EXT.ariellas** 064-post-wave-gap-closure (mrun-35df7ddfe4ec) — US1/US2 receipts gate the
  S4 link/cross-runtime stories; second-lander rebases on shared files.
