<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# Specified-features completion — **ZA-series** CRDT plan (ariellas lane, 2026-08-24)

**Marathon**: `mrun-f5ef56dba3c1` · **Lane**: `ariellas` · **Host**: `ARIELLAS` · **Repo**: `GLPNET`
**Parent item**: `mitem-01a035f2-a1a3-778a-9e5f-9ae17bdfdf3e` · **Steps**: ZA00–ZA19 (20 durable steps)

> **EXTENDS, DOES NOT REPLACE**, `docs/research/specified-completion-crdt-plan-Z-series-2026-08-24.md`
> (gavriella lane, `mrun-20d9230f767b`) — which itself extends the OLAMNIT 08-23 plan.
> **The Z-series document remains the AUTHORITATIVE CONTENT.** This file adds only (a) an
> independent re-measurement, (b) the ariellas-lane state machine, and (c) the coordination problem
> the Z-series could not see from its own lane.

**Adopt-before-inventing**: I searched before writing. The Z-series surfaced via `git log --grep`
and `git ls-files`, exactly as its own author warned. **I did not author a fourth plan for this
task** — this is the third document in one lineage and it defers to its predecessor on content.

---

## 1 · Independent re-measurement — the Z-series is CONFIRMED on all six

Measured on this host, 2026-08-24T23:0xZ, without consulting the Z-series table first:

| feature | spec | clarify§ | plan | tasks | analyze | Status header | ▶ next stage |
|---|:--:|:--:|:--:|:--:|:--:|---|---|
| **083** glptutorial-corpus-goldens | ✅ | ✅ | ❌ | ❌ | ❌ | **Clarified — ALL RULINGS CLOSED** | **`/bk-plan` — READY NOW** |
| **079** madglp-writer-reader | ✅ | ❌ | ✅ | ✅ **0/20** | ❌ | Draft | `/bk-analyze` |
| **085** onrestart-fleet-resume | ✅ | ✅ | ❌ | ❌ | ❌ | Draft | `/bk-plan` — gated |
| **080** occurs-checked-substitution | ✅ | ✅ | ❌ | ❌ | ❌ | Draft — 🔴 BLOCKED §1.14 (Udi) | `/bk-plan` — gated |
| **082** feature-stream-superset | ✅ | ❌ | ❌ | ❌ | ❌ | Draft | `/bk-clarify` — gated |
| **065** ynet-consolidation | ✅ | ❌ | ❌ | ❌ | ❌ | Draft | gated |

079 additionally carries `research.md`, `data-model.md`, `quickstart.md`. **Every cell agrees with
the Z-series.** Two independent lanes, same measurement — this inventory is corroborated, not asserted.

### The headline claim, independently verified

The Z-series' load-bearing claim is that all six already have code on `develop`. I tested the four
cited merge commits with `git merge-base --is-ancestor`:

| commit | on `origin/develop` | subject |
|---|:--:|---|
| `8a83bfc2` | ✅ | Merge **083**-glptutorial-corpus-goldens into develop (TIDY-Y01) |
| `fb038d11` | ✅ | Merge PR #172 from **079**-madglp-writer-reader-discipline |
| `3037f155` | ✅ | Merge PR #210 from **085**-onrestart-fleet-resume |
| `78c056a4` | ✅ | Merge **080**-occurs-checked-substitution into develop (TIDY-Y04) |

> **CONFIRMED: the stall is in the record, not the work.**
> And therefore — **`close` must not be reached by stamping the record.** Code that reached
> `develop` without `/bk-codexreview` is precisely the class feature **078
> (verification-receipts-and-loud-failure)** exists to eliminate. Every ZA spine below routes
> through `codexreview` before `ship`, with no exception and no batching.

## 2 · What changed since the Z-series was written (~3 h)

| Z-series statement | now | evidence |
|---|---|---|
| `G083b` FR-002 gate | ✅ **DISCHARGED** — ruled **(b) record the rejection**; FR-009 **IN SCOPE** | `88174d1b`; 083 spec:11 |
| develop 100 ahead of main, unreleased | ✅ **RELEASED `v2026.08.24.1`** — develop now **1** ahead | PR #226 merged + tagged |
| — | 🆕 **Coordination hazard**, §4 below | two marathons now drive the same six |

## 3 · The ZA ledger — 20 durable steps in `mrun-f5ef56dba3c1`

Sizes: nano 1 · micro 3 · mini 7 · midi 11 · maxi 17 · saga 35.

| ID | Step | Size | State |
|---|---|---|---|
| **ZA00** | Reconcile pipeline pointer + roadmap state for all six; move each row off `specified` to its true state | micro 3 | ▶READY |
| **ZA01** | **083** `/bk-plan` — zero blockers | midi 11 | ▶ **READY — START HERE** |
| ZA02 | **083** `/bk-tasks` — FR-009 in scope per the ruling | mini 7 | follows ZA01 |
| ZA03 | **083** `/bk-analyze` | mini 7 | follows ZA02 |
| ZA04 | **083** `/bk-implement` — **VERIFY against merged `8a83bfc2`**, not a fresh build | midi 11 | follows ZA03 |
| ZA05 | **083** `/bk-codexreview` — **MANDATORY**, code reached develop unreviewed | mini 7 | follows ZA04 |
| ZA06 | **083** `/bk-ship` | mini 7 | follows ZA05 |
| ZA07 | **083** `/bk-close` | micro 3 | follows ZA06 |
| ZA08 | **079** record **why** `/bk-clarify` produced no Clarifications section — a skipped stage with no trace is a false green | nano 1 | ▶READY |
| ZA09 | **079** `/bk-analyze` | mini 7 | follows ZA08 |
| ZA10 | **079** `/bk-implement` — 0/20 tasks checked yet code merged via PR #172; **verify each task against merged code** | maxi 17 | follows ZA09 |
| ZA11 | **079** `/bk-codexreview` — MANDATORY | mini 7 | follows ZA10 |
| ZA12 | **079** `/bk-ship` | mini 7 | follows ZA11 |
| ZA13 | **079** `/bk-close` | micro 3 | follows ZA12 |
| **ZA14** | **GATE G080** — Udi §1.14: on occurs-check fire, `UnifyFail` or `CompileError`? Spec presents both and must not decide | mini 7 | 🔴 BLOCKED |
| **ZA15** | **GATE G085** — homing: does 085 belong on the glpnet roadmap at all, given mstack is canonical and its code merged here via PR #210? | mini 7 | 🔴 BLOCKED |
| **ZA16** | **GATE G082** — fold ruling **and** 082 has **no `feature_pipeline` row**, so `/bk-clarify` is default-denied. Both must clear | mini 7 | 🔴 BLOCKED |
| **ZA17** | **GATE G065** — G2 / FR-008 five-escalate ruling, owed since 2026-08-23 | mini 7 | 🔴 BLOCKED |
| ZA18 | **COORDINATION** — broadcast the lane split (§4) | nano 1 | ▶READY |
| ZA19 | **ZA-DISCHARGE** — no feature reaches `close` with an open gate or an unrecorded skipped stage | mini 7 | last |

**Ready now: ZA00 · ZA01 · ZA08 · ZA18 = 22 pts.** **Blocked on four engineer/Udi gates: ZA14–ZA17.**

## 4 · 🔴 The coordination hazard the Z-series could not see

**Two marathons now hold spines for the same six features**: gavriella's `mrun-20d9230f767b`
(Z00–Z08, 14 steps) and this lane's `mrun-f5ef56dba3c1` (ZA00–ZA19, 20 steps). These lanes have
**already collided once today** — we both ran roadmap `sync --round 47` on this repo.

**Two lanes driving one feature through `/bk-implement` and `/bk-ship` is worse than either lane
driving none.** It is the same failure class as the duplicate-standard fork that cost this fleet a
day, and the same class as the two plans that nearly became three.

**PROPOSED SPLIT — ariellas takes 083 and 079** (the only two with **no** engineer gate, and 083's
FR-002 was ruled through this lane so the context is here), **gavriella keeps the gated four**
(080/082/085/065) since their Z-series already carries those gates and their homing arguments.
ZA14–ZA17 then become *gate-tracking* steps in this lane rather than execution spines.

**This is a proposal, not a claim.** It is broadcast under ZA18 and is not acted on until gavriella
ACKs or the engineer rules. Until then **this lane starts only ZA00/ZA01/ZA08** — 083 and 079 — and
touches none of the gated four.

## 5 · Execution order

1. **ZA18** — broadcast the split **first**, so the other lane can object before any work lands.
2. **ZA00** — reconcile the record; every later stage reads it.
3. **ZA01 → ZA07 (083)** — the only feature whose own spec header says *"Ready for `/bk-plan`"*.
4. **ZA08 → ZA13 (079)** — furthest through the pipeline; 20 tasks to verify against merged code.
5. **ZA14–ZA17** — gate tracking only. **Do not start the gated features on assumption.**
6. **ZA19** — discharge.

## 6 · Discharge condition

Discharges only when **ZA00 done**; **083 and 079 driven to `/bk-close`**; the four gates either
ruled or formally re-parked **with a rationale**; the lane split ACKed or ruled; and **every feature
whose code reached `develop` unreviewed has passed `/bk-codexreview`**.

🔴 **No feature is "completed" by silently advancing past an open gate, and none is completed by
stamping a record to match code that was never reviewed.** Carried verbatim from the Z-series,
because it is the whole point.

— `ariellas` · `glpnet` · `mrun-f5ef56dba3c1` · `2026-08-24T23:05:00Z`
