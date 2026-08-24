<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# SCHED-R6 and SCHED-R7 — measured, and R7 re-classified as a DEPENDENT of R1

| Field | Value |
|---|---|
| host | `gavriella` |
| repo | `GLPNET` |
| lane | `gavriella@GAVRIELLA` |
| board root | `D:\coop\glpnet\sched` (`exists=True`, `root_id` none) |
| marathon run | `mrun-20d9230f767b` (feature `078-verification-receipts`) |
| measured at | **2026-08-24T00:1xZ** |
| coverage | **32 of 32** work packets on this root; **1 of 14** boards (this root only) |

🔴 Every figure below is measured from `bk-flow poll` and the durable op-logs under
`<root>/ops/<actor>/*.jsonl`. Nothing here is estimated.

---

## SCHED-R6 — the capability gate is inert, and the tool already says so

`bk-flow poll --actor gavriella` on the root above:

> `capability_gate_inert: no work packet declares a required_capability, so the capability-fit
> ranking never executed — missing_capability=0 here means UNMEASURED, not clear. 50 capabilities
> published by this actor were never compared against anything.`

Corroborated directly against the substrate: `required_capability` occurs **1 time** across all
op-logs on this root, and not as a work-packet declaration.

**Finding: R6's "or" branch is already satisfied in the tool.** The honest-reporting half — never
letting a reader read `missing_capability=0` as a clear — is implemented and firing. What remains
of R6 is only the *data* half: declaring `required_capability` on work packets so the gate becomes
live rather than honestly inert.

This is 078's own thesis working inside the scheduler's reporting: **an unmeasured check declares
itself unmeasured instead of passing.** R6 should therefore be re-sized — the expensive half is
done.

---

## SCHED-R7 — binding is 1 of 32, and it is NOT independently fixable

`bk-flow poll` reports:

> `binding: 1 of 32 packet(s) resolve to a feature; 31 cannot.`

The single bound packet is `078-verification-receipts`.

### The causal chain, proven by dry-run (nothing was written)

`bk-flow open` is the only bind verb. It requires a **claim**. `bk-flow claim` refuses a
`backlog` packet — measured, verbatim:

```
$ bk-flow claim --actor gavriella --dry-run wp-wave6-consolidation
bk-flow: wp wp-wave6-consolidation is backlog, not one of ready, claimed, in-progress.
`bk-flow poll` classifies it not_ready, and a claim would not make it actionable — it would
only add a claim the board never acts on. Nothing was written.
```

Control, same command against a `ready` packet:

```
$ bk-flow claim --actor gavriella --dry-run wp-occurs-checked-substitution-...
DRY RUN — would append one claim op ... Nothing was written.
```

So the chain is: **`backlog` ⇒ no claim ⇒ no open ⇒ no feature binding.**

### The measured ceiling on R7 while R1 is unbuilt

Board state by reason, 32 packets:

| reason | count |
|---|---:|
| `not_ready:backlog` (+ `escalated`/`done`) | **25** |
| `claimed_by_other` | 3 |
| `not_claimed` | 2 |
| `ok` (dispatchable by me) | 2 |

25 packets are unclaimable **by construction**. Only 7 are not `not_ready`, and 3 of those are
claimed by peers. **The maximum binding rate reachable today is ~4–5 of 32 (≈15%)**, and no
action on this lane can raise it further.

### Consequence — a sizing correction

`docs/research/consolidated-hardening-2026-08-23.md` lists SCHED-R7 as an independent **midi (11)**
item. That is wrong at the premise: **R7 is a dependent of SCHED-R1** (the readiness writer,
**maxi 17**, still `pending`). Scheduling R7 as independent work would burn a midi against a
hard 15% ceiling and then stall.

**R7 must be sequenced after R1, or explicitly re-scoped to "refuse to report a board as
dispatchable when its binding rate is near zero"** — which is the cheap honest-reporting half,
mirroring what R6 already does.

### A second defect surfaced while measuring this

Of the **2** packets reported `OK dispatchable`, one — `wave-5-consolidated-captured-triad` — is
in the `unresolvable` binding list. The poll therefore reports a packet as **dispatchable** while
that same packet **cannot resolve to a feature**, i.e. dispatching it has nowhere to land. The
`dispatchable` claim is not qualified by the binding failure. This is exactly the reporting gap
R7's "or" branch names, and it is live on this root today.

---

## What was NOT done, and why

**No bind ops were written.** The substrate is grow-only with no delete verb, so every op is
irreversible, and 31 of the unbound packets are either peer-claimed or backlog. Bulk-writing
feature bindings unilaterally would (a) be unremovable, (b) collide with `TIDY-Y17` (unique
allocation of one feature to one repo on one host across all boards), which is unresolved. The
dry-run route established the causal claim without a single write.

## Bands quoted with the figures (trap 10)

Sizes referenced use the canonical scale: `nano 1 · micro 3 · mini 7 · midi 11 · maxi 17 ·
saga 35`. No takt verdict is quoted here, because a takt verdict is recomputed at read time and
is not a record.
