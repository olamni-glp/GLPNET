# Why no steady stream of features reaches this host — root cause, measured

**Date**: 2026-08-22 · **Host**: GAVRIELLA · **Board**: `D:/coop/glpnet/sched` (the live root;
G:/H: copies were last written 08-16 and are stale)

This closes a question carried since wave-15. It is answered here from the board's own artifacts —
op logs, hourly views and the CLI's own refusal messages — not from inference.

## Answer in one line

**Work reaches a host only when a human writes an *addressed* `allocate --to <actor>` by hand.**
Two links in the chain have no writer, so the "stream" is exactly the rate at which someone
transitions and addresses packets manually.

## The chain, link by link

```
roadmap ──▶ WP ──▶ [backlog] ──▶ [ready] ──▶ allocator ──▶ proposal ──▶ confirm ──▶ [in-progress]
                        (1)                       (2)          (3)
```

### Link 1 — `backlog → ready` has no automatic writer

Proposals track the size of the `ready` column **exactly**, and `ready` only ever moves when
someone runs `transition` by hand:

| allocate view | proposals | blocked | horizon | what happened |
|---|---|---|---|---|
| `2026-08-19T09Z` | 2 | 2 | 28 d | |
| `2026-08-19T21Z` | 4 | 0 | 28 d | |
| `2026-08-20T05Z` | 4 | 0 | 120 d | horizon widened — **proposals did not move** |
| `2026-08-22T12Z` | **6** | 0 | 120 d | gavriella transitioned **2** WPs |
| `2026-08-22T16Z` | **7** | 0 | 120 d | ariellas transitioned **3**, escalated 1 |

`blocked = 0` from 08-19 onward, and widening the horizon from 28 to 120 days changed nothing.
**Capacity was never the binding constraint.** Of 32 WPs, only 7 have ever been made ready.

This also retires an older hypothesis: the wave-16 finding that `--avail-hours 24` was the defect
is **not** the cause of the trickle. It was real (a one-day window cannot hold an 800 h critical
path) but it was not what kept the column empty.

### Link 2 — the allocator writes proposals to a VIEW, never back to the op log

`views/allocate/2026-08-22T17Z.json` holds 7 proposals with real engineers
(gavriella 3, ariellas 2, olamnit 2). The op log tells a different story: of 31 `allocate` ops,
**26 carry `proposed_actor: "unassigned"`**.

So the computed assignment is never committed to the CRDT that is the system of record. The view
is a derived artifact; the op log is the truth; **they disagree.**

### Link 3 — `confirm` reads the op log and refuses, correctly

```
REFUSED wave-5-consolidated-captured-triad: unaddressed-proposal — allocate ops carry no
proposed_actor; unaddressed work is not ready work and is never confirmed on assumption
```

This is the right behaviour. `confirm` will not dispatch on the strength of a derived view. The
result is a deadlock that looks like idleness: the allocator says "gavriella", the log says
nothing, and confirm refuses.

**Proof both ways, same session:** a WP allocated by hand *with* `--to gavriella` confirms
immediately —

```
OK  wp-verification-receipts-…: ready -> in-progress
```

— while the two WPs the allocator "proposed" refused, until an addressed allocate op was written
for them. Three WPs then confirmed into `in-progress`, and the board moved from
`backlog 28 / ready 4` to `backlog 23 · ready 3 · in-progress 4 · done 1 · escalated 1`.

## A fourth defect: `unassigned` satisfies the guard that exists to catch it

`buildkit-scheduler allocate --audit` reports:

```
missing proposed_actor  0   -> lock1_open=False
e_t_s <= 0              0   -> lock2_open=False
```

Both "locks closed" — while 26 of 31 allocations have no real owner. The check tests that the
field is **present**, and the string `"unassigned"` is present. A placeholder passes a presence
check. This is precisely feature 078's thesis — *a check that passes without verifying its
subject* — inside the scheduler's own integrity guard.

The tool is honest elsewhere, and that honesty is worth preserving: every confirm prints
`edge_coverage 0.0 — 0 declared / 0 confirmed edges across 33 WPs. 'prerequisites satisfied' here
is VACUOUS, not verified`. It says so on every op it writes.

## The durable fix — three writers and one guard

| # | Change | Size | Why |
|---|---|---|---|
| R1 | A **readiness writer**: a declared policy that promotes `backlog → ready` (e.g. spec+plan+tasks present, dependencies confirmed), rather than requiring a hand transition per WP | maxi | removes the manual pump that sets the entire stream rate |
| R2 | The allocator must **persist its proposals as addressed `allocate` ops**, not only into a view | midi | closes the view↔log divergence that makes `confirm` refuse |
| R3 | **Reject `unassigned`** as a proposed actor: the audit must verify the value names a *declared actor on this board*, not merely that the field is non-empty | mini | a placeholder must not satisfy an integrity guard |
| R4 | Declare **dependency edges** so `edge_coverage` stops being 0.0 and "prerequisites satisfied" means something | midi | the tool already reports this as vacuous on every op |

R3 is the cheapest and highest-signal: it converts a silent no-op into a loud refusal, and it
would have surfaced this entire chain on day one.

## What is NOT the cause — hypotheses retired

- **Not capacity.** `blocked = 0` since 08-19; widening 28 → 120 days moved nothing.
- **Not a missing `transition` verb.** It exists and works (withdrawn in wave-12, re-confirmed).
- **Not a broken allocator.** It proposes one packet per ready WP, with sane effort bands derived
  from roadmap free-text (`effort_source: derived-from-roadmap-freetext`, `e_t_s: 144000`).
- **Not peer absence.** ariellas cleared 3 stuck WPs within hours of being asked, and escalated a
  fourth rather than forcing it — the peer channel works.

## Scope note

Measured on one board (`glpnet`). The mechanism is in the shared `buildkit-scheduler` engine, so
the same three links should be checked on every board before the fix is called fleet-wide — that
verification is **not** done here and must not be assumed.
