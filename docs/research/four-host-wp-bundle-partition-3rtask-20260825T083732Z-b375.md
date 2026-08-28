# Curator report — four-host WP bundle partition (glpnet)

Run `20260825T083732Z-b375` · task-type `plan` · 3 blind Builders · Critic on **codex**
(cross-provider, `independence_warning: false`) · method `method-20260825T083732Z-b375`.

## Headline

**On the evidence available today, ZERO work packets can be derived `RUNNABLE-VERIFIED` on ANY
of the four hosts — ARIELLAS included.** A four-way partition in which every assigned packet is
*verified* runnable on its host is therefore **empty**, not merely unbalanced.

This is a statement about the RECORD, not about the hosts. Nothing here says a host cannot do the
work. It says nobody has measured or declared the facts the verification requires.

## How the answer was reached

Three Builders, each blind, each reading exactly two files of a six-file corpus partitioned so
that every evidence domain (board / hosts / protocol) is covered by exactly two Builders.
Independence audit: 5 roles, **0 violations**, sibling-content checks exercised.

- 368 claims parsed, **0 unparsed**.
- 224 distinct `(SUBJECT, DIM, HOST)` keys → **34 corroborated · 189 singleton · 1 conflict**.

### The one conflict
`ACTOR:ariellas [H-MAPPING]` — `MAPPING-UNSTATED` (builder-2, builder-3) vs `MAPS-TO-ARIELLAS`
(builder-1). The actor→host mapping is **not stated by the records**; two Builders independently
say so. **ENGINEER'S to resolve.**

## The derivation (L1, corroborated inputs only)

`RUNNABLE-VERIFIED` requires a corroborated locality row permitting the host AND every corroborated
requirement member satisfied by corroborated host-platform facts.

| host | RUNNABLE-VERIFIED | NOT-RUNNABLE | UNDERIVABLE (no corroborated locality) | UNDERIVABLE (no corroborated requirement) |
|---|---:|---:|---:|---:|
| ARIELLAS | **0** | 2 | 24 | 11 |
| GAVRI | **0** | 2 | 24 | 11 |
| OLAMNIT | **0** | 2 | 24 | 11 |
| SHIRAS | **0** | 2 | 24 | 11 |

### Why nothing verifies

1. **Locality is unestablished for 24 of 37 packets** (17 with no locality claim corroborated,
   7 corroborated by one Builder only). `UNDECIDABLE` is *not* a permission — the Critic REFUTED
   an earlier draft of this report that treated it as neutral, and was right to.
2. **Requirements are undeclared.** Only **2** packets carry a corroborated platform-requirement
   member. The board's own poll states the cause: `capability_gate_inert` — **no work packet
   declares a `required_capability`**, so `missing_capability=0` means UNMEASURED, not clear.
3. **Three of four hosts have no measured platform at all.** Only ARIELLAS has
   `HOSTPLAT-WINDOWS` and `HOSTPLAT-WSL` measured present. GAVRI, OLAMNIT and SHIRAS are
   `HOSTFACT-UNMEASURED` on all four platform properties.

## Prerequisites, per host — complete, not curated

| host | unmet | detail |
|---|---:|---|
| ARIELLAS | 0 | clone present; platform measured (Windows + WSL) |
| GAVRI | 1 | platform unmeasured |
| OLAMNIT | 1 | platform unmeasured |
| SHIRAS | **6** | clone absent · board-identity absent · caps-stream absent · oplog absent · calendar stale · platform unmeasured |

An earlier draft proposed a SHIRAS prerequisite bundle covering only clone, board identity and
platform. The Critic **REFUTED** it for omitting caps-stream, oplog and the stale calendar. All six
are listed above.

## Board-wide blockers (independently corroborated)

- **Binding gap** — 1 of 32 packets resolves to a feature; **31 cannot**. `bk-flow open` binds a
  claimed packet to a feature + marathon run, so 31 packets cannot enter a pipeline on any host.
  The count is repo-UNSCOPED (no `--repo` passed).
- **Readiness starvation** — 25 `backlog`, 3 `claimed_by_other`, 2 `not_claimed`, **3 `ready`**.
  Four non-trivial bundles cannot be drawn from `ready`.
- **Readiness authority is unresolved** — the engine has no readiness writer.

## Open escalations — ENGINEER's, not resolved here

- **E17** — what "equal" means: packet count, effort-size weight (needs a rule for the ~20
  not-closed features with no recorded effort), or era count.
- **E28** — SHIRAS: provision first / prerequisite-gated non-compliant bundle / reallocate its share.
- **H-MAPPING conflict** — which host each board actor belongs to.

## Status

Stopped at the **budget gate** (`warn_confirm`, 1.18M vs 900k) before cycle 2. Cycle 2 would run a
directed pass at the singleton and no-corroboration keys — which is precisely where the missing
locality and requirement evidence sits. Residual state is persisted; nothing is lost.

## Recommendation

The first deliverable is not four bundles of feature work. It is the **prerequisite bundle** that
makes a verified partition possible at all: measure the three unmeasured hosts, declare
`required_capability` per packet so the capability gate stops being inert, repair the 31-packet
binding gap, resolve readiness authority, and provision SHIRAS across all six of its gaps.
