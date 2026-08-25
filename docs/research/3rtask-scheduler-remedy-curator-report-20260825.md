# Curator report — durable remedy for the glpnet scheduler feature-stream leak

**Run** `20260825T101634Z-90d4` · lane `shiras` / `glpnet` · task_type `plan` · review-only, 1 cycle
**Critic**: NOT RUN — see §4. **Independence**: REDUCED (codex absent; recorded at preflight).

## 1 · 🔴 THE HEADLINE IS A TOOL FINDING, NOT A PLAN FINDING

**This run's corroboration signal is worthless, and I measured that rather than reporting it.**

`merge` reported **8 corroborated** rows over 96 claims / 43 identities. I applied the exact-text
membership test that gavriella prescribed in `ops/gavriella note gavriella:000019`:

```
multi-builder rows: 8 | GENUINE corroboration: 0 | ghost-carrying: 8
```

**8 of 8 carry at least one ghost builder. Genuine corroboration is ZERO.** Row 1 credits
builder-1, builder-2 AND builder-3 with a claim only builder-1 wrote.

This **independently reproduces TOOL-R9** ("TWELVE OF TWELVE CARRY AT LEAST ONE GHOST BUILDER …
GENUINE CORROBORATION ZERO") on a **different host, different subject, different slice set**.
The mechanism gavriella named at source holds: default `key_mode=concept` union-finds related
claims into one finding and then **unions the builder lists**, which over disjoint slices
manufactures agreement.

**Consequence for this report:** every claim below stands on **its own citation only**. Nothing here
is cross-verified. I have not promoted a single element on a corroboration count.

## 2 · 🔴 MY OWN ROOT CAUSE IS CORRECTED BY THE BOARD'S OWN OPS LOG

My `075318Z` finding — *"a detector at every transition, a writer at none"* — was derived from the
18 **cards**. The **ops notes**, which I had not read, correct it in two material ways:

**(a) The missing LINK-1 writer is BY CONTRACT, not a bug.** `ariellas:000049` records
*"BREAK 1 PRIMARY readiness has no engine writer **by contract**"*, and the healing row's risk field
warns *"a naive fix mass-promotes an edgeless backlog and **manufactures exactly the false green
this exists to end**"*. **A remedy that simply adds the missing writer is the wrong remedy.**

**(b) It is FIVE breaks in series, not three transitions.** `ariellas:000049`: *"BREAK 0 REFUTED —
supply is fine … BREAK 1 readiness … BREAK 2 recommender vacuous at 0 confirmed edges … BREAK 3
efforts 288000/144000 exceed 86400 capacity, unplaceable proposal emitted silently, lane reads idle
… BREAK 4 the allocate view contradicts every durable allocate op. **They are in SERIES so fixing
any one yields nothing.**"* Plus `SCHED-R7`: **1 of 32 packets bind to a feature**; fixing readiness
alone *"moves the stall one hop downstream"*.

**(c) The true cause of my third card class is D5, not a missing writer.** `ariellas:000047`:
*"the allocate VIEW contradicts every durable allocate OP — it re-proposes from scratch each cycle …
**This is the root cause of the dispatched-but-never-converted cards on all three lanes** and it is
strictly worse than D2."*

**My finding was directionally right and mechanistically wrong.** Recorded as a correction.

## 3 · THE REMEDY IS ALREADY PRESCRIBED — do not design a new one

`gavriella:000017` already names it, with sizes:

| id | fix | size |
|---|---|---|
| **R1** | a readiness writer **with a declared promotion policy** | maxi |
| **R2** | the allocator must **PERSIST** proposals as addressed allocate ops, not only into a view | midi |
| **R3** | reject `unassigned` — the audit must verify the value names a **DECLARED ACTOR** | mini — *cheapest, highest signal* |
| **R4** | declare dependency edges so `edge_coverage` stops being 0.0 | — |

**R3 is the cheapest real progress available on this board.** The guard today *"tests PRESENCE, and
the string `unassigned` **is** present"* — so `allocate --audit` reports `missing proposed_actor 0`
and `lock1_open=False` while **26 of 31 allocations have no real owner**.

## 4 · WHY THERE IS NO CRITIC ADJUDICATION

The Critic was not run. codex is absent on shiras, so the Critic would be a **same-provider Claude**
sub-agent adjudicating Claude Builders' claims — and §1 has just established that this run's
corroboration layer manufactures agreement. **Adding a same-provider adjudication on top of a
known-manufactured merge would produce a confident verdict with no independent basis.** Declared as
a gap, not silently skipped. Verdict: `review_only`, not `converged`.

## 5 · OPEN ESCALATES — ENGINEER'S TO RESOLVE (I have not resolved any)

1. **Superset/subset ownership is UNDECIDED.** The healing row's notes: 082 *"should be folded into
   this or explicitly scoped as its engine half; **STILL UNDECIDED**"* — and the healing row
   **mischaracterises** 082 (says it scopes BREAK 3/4 as US2/US3; 082's US1 is the readiness story).
2. **The superset scores BELOW the subset it supersedes** — healing row WSJF 2.62 / rank null vs
   082 WSJF 4.25 / rank 13. **Ranking-driven selection will keep picking 082 over its own superset.**
3. **"May a central allocator assign work to another person?"** — open engineer escalation
   **unanswered since 2026-08-13**; its author recorded that the allocation design **cannot be
   completed without it**.
4. **Bootstrap hazard** — the remedy WP is itself carded under two of the leaks it exists to fix
   (dispatched-never-converted 270093s; ready-undispatched 9826s). It needs an **out-of-band**
   dispatch path for its own first deployment.
5. **The toolchain is off-release** — TOOL-R8: every `bk-*` call runs an **uncommitted working tree**
   via an editable `.pth` while all 29 targets report `2026.08.23.1`. FR-019's "inherited by all
   repositories via the existing deployment mechanism" rests on a mechanism the record shows bypassed.

## 6 · Fleet-scope findings that bound any rollout

- **Fleet membership has no single referent**: rows variously name 3 hosts, 5 hosts, and a 14-board
  sweep. "All four fleet hosts" is not a defined set in the record.
- **All 196 caps records carry `evidence=null, verified=true`** — verification asserted with zero
  supporting evidence.
- **Capability names are un-normalised**: 7 capabilities exist under both `bk-*` and `buildkit-*`
  spellings, so exact-name matching misses holders.
- **All 609 calendar records are `kind='available'`** — the substrate **cannot express** a freeze
  window or a host outage.
- **Declared capacity is not physically realisable**: per-date sums reach 1704 h in a 24 h day.
- **The 5-minute ACK SLA is recorded twice as structurally unsatisfiable** on an async SMB
  file-drop transport with no delivery receipt.
- **Scope caveat, verbatim**: *"measured on the glpnet board only; the mechanism is in the shared
  engine so every other board must be CHECKED not assumed."*

## 7 · What I recommend, and what I did not do

**Recommend:** do **R3** first (mini, cheapest, highest signal, no contract violation), then **R2**,
and treat **R1** as gated on a declared promotion policy so it cannot mass-promote an edgeless
backlog. **Do not** open a new remedy feature — three promoted rows already claim overlapping
"durable healing" scope.

**Did not do:** resolve any ESCALATE · write any board transition · design a new remedy over the
prescribed R1–R4 · promote anything on a corroboration count.
