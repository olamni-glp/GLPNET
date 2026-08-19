# Flow-gate audit — mechanisms that block or discard work while reporting healthy

**Date:** 2026-08-14 · **Host:** Ariellas · **Auditor:** ariellas session (076 lane)
**Trigger:** engineer report — *"64 of 70 ready WPs blocked on the single-day capacity gate,
while the board reports starved: false"*
**Method:** mechanical scan of `buildkit_cli/scheduler`, `buildkit_cli/roadmap`, and the glpnet
rule set. Every finding below carries a file:line or a command receipt. Nothing is asserted from
memory.

---

## 0. What I did NOT find — stated first, because it matters

**The glpnet rule set is clean of work-duration caps.** `CLAUDE.md`, `docs/DISCIPLINE.md`,
`docs/typed-glp-manual.md`, `docs/glp-cheat-sheet.md` and `docs/` contain **no** rule capping work
at 8h, 12h, one day, or one session. The only timeboxes present are two references to a *research
spike* explicitly marked "throwaway, NOT on the engine critical path"
(`docs/research/repl-engine-separation/llvm-feasibility.md:246`) — a legitimate scoping device for
a discardable experiment, not a cap on delivery work.

**There is no evidence of a rule being inserted into the glpnet rule set.** The gates that produce
the reported symptom are **implementation defaults in the buildkit scheduler**, present in the
algorithm from its design (`Implements: D-026, D-031, D-034…`), not edits to a policy document.
Recording that plainly because the remediation is different: this is a code fix in one repo, not a
rule purge across the corpus.

---

## T1 — Allocator gates multi-day effort against a single day's capacity 🔴 CLASS-A

**The named defect. Confirmed.**

`scheduler/engine/algorithms/allocator.py:250-260`

```python
cap  = capacity_by_engineer_day.get((engineer, day), 0.0)
used = pass_cap_used.get((engineer, day), 0.0)
if cap - used < float(e_t):
    eliminations.append({"gate": "capacity", ...})
    continue
```

`capacity_by_engineer_day` is keyed `(engineer, **day**)`. `e_t` is the WP's **total** expected
duration. The comparison is therefore *total effort vs one day's capacity*.

Structural cause: `scheduler/engine/algorithms/frontier.py:79` — `horizon: str = "daily"`. The
ready frontier is a daily view, so capacity is folded per-day, so any work package larger than a
single day's load factor is **permanently ineligible no matter how many days are free.** It is not
deferred to tomorrow; it is re-blocked identically every cycle, forever.

**Effect:** the scheduler can only ever schedule work that fits in one day. All genuine
engineering — anything profiled `effort: large`, `medium`, or `marathon` — is structurally
unschedulable. This is the mechanism that converts a fully-scored, fully-promoted backlog into
zero flow.

**Already captured** in the buildkit roadmap as
`scheduler-allocator-gates-multi-day-effort-against-single-day-capacity` ("Allocator gates
multi-day effort against a single day's capacity"), **WSJF 5.80**.

**Durable fix:** the allocator must place a WP across a *span* of days (or against a horizon
capacity sum), not against `capacity[day]`. Acceptance must be fault-injected: a WP with
`e_t` = 3× daily capacity must schedule across 3 days, and a test must fail if it is blocked.

---

## T2 — Missing calendar is reported as a capacity limit 🔴 CLASS-A

Same line. `capacity_by_engineer_day.get((engineer, day), **0.0**)`.

An engineer with **no calendar record for that day** gets capacity `0.0`, so `cap - used < e_t` is
true for every WP, so **every WP is blocked and attributed to the `capacity` gate.**

This is a distinct defect from T1 and worse diagnostically: the operator reads "blocked on
capacity" and concludes the team is oversubscribed, when the true cause is *no calendar data was
ever supplied*. A missing input is being reported as a resource limit. Note `onboard --avail-hours`
and `--window` are optional flags — nothing requires a calendar to exist.

**Durable fix:** absent calendar ⇒ a distinct `no-calendar` gate (or an explicit hold-out), never
`0.0` silently folded into the capacity comparison. `0.0` capacity asserted by a *real* record is a
legitimate different case and must remain distinguishable from an absent one.

---

## T3 — `starved` fires only on ZERO proposals; partial starvation reads green 🟠 PARTIALLY FIXED

`scheduler/__main__.py:211-214`, in-source, verbatim:

> *"Partial starvation: `starved` only fires on ZERO proposals and is short-circuited by the
> dispatched one-way trap, so a board that placed 5 of 69 and blocked 64 on capacity reported
> itself healthy."*

**The engineer's 64-of-70 figure is already written in the source as 64-of-69.** Someone found this
and patched the **reporting** half — `if blocked_n and not starved:` now emits
`cycle PARTIALLY BLOCKED`. The **gate** (T1/T2) was left intact.

This is the exact pattern the engineer names: the symptom was made visible and the cause was left
in place, so the board now *says* "partially blocked" every cycle forever while flow stays at zero.
**A warning that fires every cycle and changes nothing is indistinguishable from noise.**

**Durable fix:** T1+T2. Then `starved`/`PARTIALLY BLOCKED` becomes a real signal again because it
will be rare.

---

## T4 — Roadmap import silently refuses entities at scale 🔴 CLASS-A — LARGEST BY VOLUME

**glpnet:** `.specify/roadmap-sync/.import-refused.json` — **2470 refused entities**
(1370 `untagged`, 1100 `foreign`), file 2.6 MB.
**buildkit:** same file — **1333 refused** (946 `untagged`, 387 `foreign`).

**1100 of the glpnet refusals are stamped today, newest `2026-08-14T13:59:06Z` — inside this
session's own import runs.** Every one of those runs printed:

```
imported N new file(s), applied N new line(s); 0 slot re-sequence(s); skipped 96 already-applied
file(s); 76 file(s) missing .license sidecar: …
```

**Not one word about 1100 refusals.** Files, lines, skips and licences are all reported; refusals
are written to a side file and omitted from the result. `replay --verify` then reports
`HEAD matches the journal projection ✓` — because HEAD *does* match the journal it was allowed to
build. **The verification cannot see what was refused before the journal existed.**

This is the same shape gavriella logged as an F1 instance ("import refused 954 untagged entities
and applied 0 lines while `replay --verify` still reported OK — silent split-brain, 20-line
divergence measured"). It is now measured at **2470 on this host**.

Refusal reasons are not equally defensible:
- **`foreign` (1100)** — cross-project entities (`yngenios-windows`, `buildkit`, `olamnit-assistant`).
  Refusing another project's roadmap rows is arguably *correct policy*; being silent about it is not.
- **`untagged` (1370)** — `declared_project: null`. These carry no project claim at all, most likely
  pre-dating project tagging. **These are being dropped on the floor with no record in any surface a
  human reads.**

**Sharp instance:** the fix for T1 was itself refused. `allocator-capacity-gate-spreads-multi-day-work-packages`,
`entity_kind: feature`, `declared_project: yngenios-windows`, `local_project: buildkit`,
`reason: **foreign**`, `refused_at: 2026-08-14T13:19:59Z`. In this case no work was lost — buildkit
holds its own equivalent row (T1, WSJF 5.80) — **but the mechanism that would have lost it is
live, and it operated on the remediation for the very defect under audit.**

**Durable fix:** `import` must print refusal counts by reason in its normal output and exit
non-zero (or emit a flagged line) when refusals occur; `replay --verify` must reconcile against the
refused set and refuse to report ✓ while unexplained refusals stand.

---

## T5 — Cascading fail-closed hold-outs remove WPs from the frontier 🟠 CLASS-B

Individually defensible, collectively a disappearance path:

| Site | Condition | Result |
|---|---|---|
| `allocator.py:181-185` | `e_t is None or e_cost is None` | `held-out: missing inputs` |
| `budget.py:124-128` | `e_cost_tokens is None or balance is None` | not feasible (FR-056) |
| `gate_classes.py:44` | estimator returns None from empty sample | WP **HELD OUT** |
| `calendars.py:13-14, 215` | no window within the 366-day horizon | `{unbounded}` + hold-out |
| `cpm.py:73` | `held_out` set | excluded from the ready set |

Each is correctly fail-closed (better than silently promoting). The problem is **aggregate
visibility**: a WP lacking a PERT sample, a token estimate, or a calendar window leaves the
frontier through one of five doors, and `ready_undispatched` — the number an operator reads — has
already had it removed. A held-out WP is invisible in exactly the surface built to show unscheduled
work.

**Durable fix:** a single `held_out` roll-up with per-reason counts on every `cycle`/`board`
output, so hold-out is a reported state and not an absence.

---

## T6 — 76 export files skipped for a missing `.license` sidecar 🟡 CLASS-B

`buildkit-roadmap import` reports `76 file(s) missing .license sidecar` and skips them. Named, so
better than T4 — but a skipped file is still indistinguishable from an absent one in every
downstream fold, and 76 historical exports are outside the sync. Not new, not mine to fix
unilaterally.

---

## T7–T9 — already keyed this round, restated for completeness

- **D9 / T7** — `review propose-scores` emits an identical `{5,5,5,5}`/`{100,1,50,5}` for seven
  features with materially different profiles. **It does not read the profile it claims to score.**
  A confident number produced without consulting the evidence — the F1 shape in the prioritisation
  surface.
- **T8** — `review rank` ranks **closed** features alongside live ones (ranks 2 and 5 in the current
  board are both `closed`). An operator following the rank is pointed at finished work.
- **D8 / T9** — `roadmap status` is blind to 12 of 27 not-closed features (no epic linkage);
  independently confirmed from two hosts' folds. The 12 invisible rows include the highest-WSJF
  feature on the board (7.80) and the merged coordination spec.

---

## T10 — The coordination protocol itself is the largest overhead sink 🔴 — and it is mine

Stated against my own work, because the engineer's charge is about ratio and the honest answer
implicates the process I authored.

**This session's output:** 8 channel documents (broadcasts, directed distributions, ACK handshakes,
R2 round notes), 5 roadmap sync rounds, 5 git commits — and **zero features advanced.** 076 has not
moved since `7821fd2a`. The scoring round was real but small; the rest was coordination *about*
work that is not happening.

The R1–R4 reporting contract I published at `115246Z` mandates a per-feature ACK, a WIP line every
active round, a completion report, and a filename-naming handshake per ask — **reciprocally, across
three hosts.** Every round therefore generates at least six documents before any code is written.
It was built for a real defect (three recorded silence-misreads) and it did fix that. But it is now
consuming the majority of the cycle, and it produces the appearance of intense activity — receipts,
folds, corroborations — while the delivery number stays flat.

**This is the 98%-overhead pattern in its clearest form, and no code change fixes it.** T1–T5 explain
why the *scheduler* delivers nothing. T10 explains why the *session* delivers nothing even when
unblocked: the ceremony is load-bearing for coordination and parasitic on delivery, and nothing in
the contract caps its cost.

**Durable fix — proposed, engineer's call:**
1. **Round notes only on delta.** Kill §5.3's "a round with nothing to report still gets a note".
   Silence with a *published cursor* carries the same information at zero cost; the silence clause
   already requires a scanned-paths receipt before any absence *claim*, which is the part that was
   actually load-bearing.
2. **Collapse R1/R4 into one per-round document per host** instead of one per ask.
3. **Cap coordination at a stated share of the round** and report the ratio, so the overhead is
   measured rather than assumed benign.

---

## Remediation order

Strict dependency order — the first item is the only one that restores flow.

| # | Item | Class | Fixes |
|---|---|---|---|
| 1 | **T1** allocator: span-based capacity, not per-day | A | Unblocks all multi-day work — the whole backlog |
| 2 | **T2** absent calendar ⇒ distinct gate, never `0.0` | A | Stops misattributing missing data as oversubscription |
| 3 | **T4** import must report refusals; `replay --verify` must reconcile them | A | 2470 entities currently outside every fold |
| 4 | **T10** cut coordination overhead | — | Restores the delivery ratio; engineer's call, not mine |
| 5 | **T5** single `held_out` roll-up with per-reason counts | B | Makes hold-out a reported state |
| 6 | **T3** re-tune `starved` once T1/T2 land | B | Makes the warning meaningful again |
| 7 | **T7/D9, T8, T6** prioritisation-surface and sidecar fixes | B | Stops the ranking pointing at wrong work |

**T3 is deliberately late.** It is the most visible symptom and the least valuable fix — patching
the alarm again without T1/T2 would repeat exactly the error this audit found.

---

## Standing caveat

`scheduler-allocator-gates-multi-day-effort-against-single-day-capacity` (WSJF 5.80) lives in the
**buildkit** roadmap, not glpnet's. A parallel session is active in that repo (exports
`ariellas__buildkit__20260814T132139Z` → `135424Z`). **Coordinate before implementing T1** — two
sessions fixing the same allocator is the collision class already open on `078`.
