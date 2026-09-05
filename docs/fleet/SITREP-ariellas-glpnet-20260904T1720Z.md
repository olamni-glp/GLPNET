<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# SITREP — `ariellas` / `glpnet` — 2026-09-04T17:20Z

Per `docs/SITREP-FORMAT.md` (canonical). **Every field below is measured from a durable source at
the stated time.** Where a value cannot be measured it says `unmeasurable` — never `0`, never a guess.

---

## Table A — SITREP header

| field | value | how measured |
|---|---|---|
| `host` | `ariellas` (192.168.0.142) | `Get-NetIPAddress`, `$env:COMPUTERNAME` |
| `repo` | `GLPNET` @ `D:/BSTDEV/research/glp/GLPNET` | — |
| `branch` | `develop` @ `632bba70`, clean, 0 ahead / 0 behind origin | `git status`, `git rev-list --left-right --count` |
| `run_id` | `mrun-f5ef56dba3c1` (`glpnet-full-completion-programme`) | `buildkit-marathon status` |
| `steps` | **50 / 135** (was 42/135 at session open) | same |
| `outstanding_items` | 167 | same |
| `seq` | 385 → 386 | same (`--json`) |
| `board_root` | `\\192.168.0.108\GAVRI_D\coop\glpnet\sched` **exists=True** | `buildkit-scheduler root` |
| `board_root_id` | `803713a4-95fb-4527-a9b8-a9b22def7fc5` — 🔴 **NOT PINNED** | same |
| `wp_open_here` | `unmeasurable` this session — durable-ops fold not run (board is under a HOLD, §Blocks) | — |
| `prs_open` | **0** | `gh pr list --state open` |
| `develop_ahead_of_main` | **10** | `git rev-list --count origin/main..origin/develop` |
| `blocks_open` | **4** (§Blocks) | this session's measurements |

🔴 **`board_root_id` is NOT PINNED on this repo.** The tool reports: *"this repo accepts whatever
root resolves."* Given the fleet is actively fighting actor-identity splitting and double-counted
hosts, an unpinned root means **a wrong board would be accepted silently instead of failing loud**.
Recommend setting `sched_root_id` in `config.local.json` on every lane, fleet-wide.

---

## Table B — roadmap: features NOT closed

Folded from the **signed export `heads`** (`ariellas__glpnet__20260904T153315Z.json`, round 71),
**not** from `buildkit-roadmap status` — status is blind to epic-less features.

| state | count |
|---|---|
| analyzed | 2 |
| implemented | 3 |
| specified | 5 |
| promoted | 19 |
| captured | 1 |
| **TOTAL NOT-CLOSED** | **30** |

Totals: **21 epics · 124 features · 94 closed · 30 not-closed.**
Full 30-row table: `docs/fleet/BKSTD1-ariellas-glpnet-20260904T1700Z-NOT-CLOSED.md`.

**Reconcile findings:** 75 of 124 features carry no `spec_path` and can never bind by basename;
**9** pipeline records are UNBOUND and cannot move a roadmap state; dedupe scanned 123 live features
across id-stem and title strategies → **0 duplicate groups**.

🔴 **Six of the 30 are recorded pre-implementation while their branch is ALREADY an ancestor of
`origin/develop`** — 065, 066, 067, 078, 080, 082. Not advanced, deliberately (see Table D / W23).

---

## Table C — takt

```
takt: 21/135 steps measurable (3 declared phase, 132 derived)
sources: 4/4 measurable
unmeasurable steps: 114  (stated as a count, NOT folded in as zero)
```

| phase | n | p50 | p80 | max | band | verdict |
|---|---|---|---|---|---|---|
| specify | 3 | 0.06h | 0.31h | 0.31h | 0.5–3.0h | over |
| clarify | 1 | 0.53h | 0.53h | 0.53h | 0.5–3.0h | **in-band** |
| plan | 1 | 0.08h | 0.08h | 0.08h | 0.5–3.0h | under |
| tasks | 4 | 272.21h | 273.14h | 273.14h | 0.5–3.0h | over |
| analyze | 7 | 0.05h | 0.09h | 0.12h | 0.5–3.0h | over |
| implement | 4 | 93.36h | 278.77h | 278.77h | 0.5–3.0h | over |
| close | 1 | 0.01h | 0.01h | 0.01h | 0.5–3.0h | under |
| codexreview | 0 | — | — | — | — | pending |
| ship | 0 | — | — | — | — | pending |

**ERA TOTAL ELAPSED 1034.07h over 21 measured steps; band 1.5–6.0h → `unmeasurable`.**
The era is unmeasurable because **no phase is finished** (specify…close all `in_progress` or
`pending`). Cross-size: mean 14.00h, p50 20.29h over **6** eras across 3 sizes — **94 eras
unmeasurable, listed and NOT counted as zero.**

⚠️ **These p50s are wall-clock elapsed, not effort, and the `over` verdicts are an artefact of
steps spanning multi-day gaps** (specify shows 15.82h elapsed against 0.38h effort — a 15.43h gap).
**Do not calibrate fleet takt on this run.**

---

## Table D — what's next

| rank | step | size | state | blocked-by |
|---|---|---|---|---|
| 1 | **W18** Gleam cluster 050/059 | mini 7 | **gated** | engineer ruling — but the surface is now **44 core files**, not 248 (§W18 below) |
| 2 | **W19** delete the contained origin refs | mini 7 | **gated** | ref-deletion ownership `Q-GLPNETA18-02` (unruled) |
| 3 | **W20** delete contained local branches | micro 3 | **gated** | PREREQ W19 |
| 4 | **W21** retire second clone + worktree | micro 3 | **gated** | PREREQ W15 ✅ + W19 (preservation now COMPLETE, §below) |
| 5 | **W23** reconcile roadmap to post-merge reality | micro 3 | **gated** | PREREQ W19 — **input pre-computed, 6 rows** |
| 6 | **W24** codexreview + ship `084-host-tidy-up-and-merge-closure` | mini 7 | **gated** | PREREQ W20 W21 W22 ✅ W23 |
| 7 | **W25** takt projection + bk-flow readiness delta | micro 3 | **gated** | PREREQ W24 |

🔴 **Every remaining step is gated, and 4 of the 7 trace back to ONE unruled question** —
ref-deletion ownership (`Q-GLPNETA18-02`). Their measurement work is complete; they are waiting on a
decision, not on effort.

---

## Blocks open (4)

| # | block | owner | blocks |
|---|---|---|---|
| 1 | **Ref-deletion ownership** `Q-GLPNETA18-02` — two lanes claim branch-deletion scope on one repo | engineer | W19 → W20 → W21 → W23 → W24 → W25 |
| 2 | **W18 core collision** — 44 shared-core `.gleam` files | engineer | W18 |
| 3 | **J2 / `Q-GLPNETA19-01`** — §1.14 occurs-check semantics (`UnifyFail` vs `CompileError`) | **Udi** (reserved) | no longer blocks W11; still open |
| 4 | **Federation UDP port unpublished** — rule authorised, port never named | `@gavriella-glpnet` | my §6.4 ACK compliance |

---

## Session delta (2026-09-04, session 19)

- **Marathon 42/135 → 50/135.** W11, W12, W13, W14, W15, W16, W17, W22 discharged **by measurement**.
- 🔴 **W11 was recorded BLOCKED ON AN ENGINEER RULING for 11 days and was never blocked** —
  `origin/080` was already an ancestor of `develop`. **Three systems disagreed: roadmap said
  `specified`, marathon said `blocked`, git said `landed`. Only git was measured.**
- **W18's contradiction resolved by measurement** — 148 shared `.gleam` paths, **104 byte-identical**,
  44 differing, 41 only-on-059, 30 only-on-develop. N12 and C1 are *both* partly wrong; it is one
  lineage that forked. Decision surface: ~41 mostly-test files + a 44-file core call.
- 🔴 **Preservation gap found and CLOSED** — commits `57fa2066`/`fd305b5a` were reachable from **no**
  origin ref and **no** archive tag. Verified bundle **+** `archive/secondclone-main-20260904`
  pushed to origin (`1865b2f7`).
- **Mandatory ACK published** to git and the coop board; **stop order HELD** (my fold is not
  term-space-aware). Measured **zero** term ops across all 25 glpnet op-logs.
- Roadmap round 71 (reconcile → dedupe → export) + BK-STD-1 table.

### Two self-reported defects

1. **I ran `git fetch --prune` in the second clone**, deleting 5 remote-tracking refs. Nothing was
   lost **only because the archive tags are on ORIGIN**. A SHA-list preservation would have lost five
   branches to a routine command.
2. **I reported a W23 trace as recorded when its command had been killed.** I could not verify it
   landed (one confirmed trace = +1 seq; the window showed +2 with other writers active), so I
   **re-recorded it** rather than assume — `trace_id 726, recorded: true`.

### One tooling defect found

🔴 **The on-disk marathon mirror is stale and traceless.** `marathon-mrun-f5ef56dba3c1.md` has mtime
15:33Z and still says *"Steps (42/135 complete)"* against an authoritative 50/135, and contains **no
traces section at all** — a confirmed-accepted trace is as absent from it as a killed one.
**Grepping the mirror is not a valid detector for whether a marathon write landed, and a restart
reading the mirror would under-report by 8 steps.** Use `buildkit-marathon status`/`position`.
Recorded as a trace this session.

---

**`ariellas.glpnet` · ARIELLAS 192.168.0.142 · 2026-09-04T17:20Z · resume with `resume marathon`**
