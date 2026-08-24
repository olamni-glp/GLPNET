<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: terminal-disposition protocol

Every wave item ends in exactly one terminal disposition (FR-002). This contract defines what
each requires.

## closed

1. The item's own verification gate is green at the closing checkpoint: the suites its
   profile/spec names, plus the repo baseline (full REPL suite at current count; affected
   dotnet/gleam suites; affected drills) — FR-006.
2. Roadmap row advanced to `closed` through its legal state sequence (reconcile/advance).
3. Ledger row: state=closed, evidence = commit(s) + suite counts (+ receipt stamp if a peer
   surface was consumed, with local re-verify noted).
4. Published in the next sync round export; receipt fanned when fleet-relevant.

## deferred / rejected

1. Engineer decision recorded verbatim (the ruling/decision text or COOP message reference).
2. Roadmap `defer`-equivalent (edit + note) or `reject` with rationale.
3. Ledger row: state + evidence = decision reference. No silent defaults: the wave may
   PROPOSE a defer/reject; only the engineer's recorded decision makes it terminal.

## superseded

1. Surviving feature named; roadmap `supersede --by <survivor>` (or `merge` when descriptive
   fields must carry).
2. Ledger row: state=superseded, evidence = survivor id + roadmap row.

## Parking (non-terminal)

A story hitting an open gate/ruling parks: ledger state=parked with blocked_by set; marathon
item parked/sequenced accordingly; the wave status surface (COOP stamp + marathon status)
names the blocker (FR-003/SC-004). Un-parking requires the gate row cleared with evidence.

## Prohibitions

- No disposition without its evidence row (no trust-me closes).
- No self-resolved rulings; no scope absorption from a peer's owned feature (FR-004).
- No `git add -A`; disposition commits are file-scoped (constitution VII).
