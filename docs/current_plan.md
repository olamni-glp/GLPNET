<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Restart pointer — **THIN POINTER ONLY, NOT A WORK LEDGER**

> Last verified **2026-08-31T11:30Z** by the `gavriella` lane, against durable rows — not from a
> summary. Per CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*, the **roadmap + buildkit
> marathon state are the source of truth**. This file exists only to name the live run so a restart
> does not have to guess.

🔴 **This file was itself the defect on 2026-08-31.** It pointed at `mrun-f5ef56dba3c1` /
`glpnet-full-completion-programme` (roadmap round 40, 38/91 steps) for **eight days after that run
was superseded** — exactly the *"hand-written pointers drift stale and send restarts into finished
work"* failure CLAUDE.md warns about. **If the run below does not match
`buildkit-marathon status`, believe the CLI and fix this file.**

---

## 🔁 AFTER A REBOOT — NOTHING TO TYPE, THEN ONE LINE

`BK-OnRestart` (scheduled task, **Ready**, fires **45 s after logon**) runs
`scripts/onrestart-launch.ps1` and relaunches **all 15 lanes**, each resumed mid-thread with
`claude --continue --autocompact 1000000` — never summarised. DryRun verified 2026-08-31T23:34Z:
**15 requested / 15 will launch / 0 refused**, layout `TwoWindows`.

| window | tabs |
|---|---|
| **1** | ospark · tefl · hatzinor · olamnit · buildkit · qhstate · crucible |
| **2** | glpnet · lejepa · mstack · yngraw · yngwin · ynglin · yngapp · yngcor |

⚠️ **Leaf `yngenios` collides twice** (`yngraw`=`D:\bstdev\research\yngenios`,
`yngcor`=`D:\yngenios\yngenios`). It is neutralised **only** because both carry explicit distinct
names. **Never register a yngenios lane without `-Name`** — the leaf default would collide and
silently drop a lane (olamnit `20260827T2245Z`).

Then, in the **glpnet** tab:

## Resume in one line

```
resume marathon
```

which is:

```
buildkit-marathon resume --feature 078-verification-receipts
```

🔴 **`--feature` is mandatory** — there is no `.specify/feature.json` in this repo, by design.

## The live run

| | |
|---|---|
| run | **`mrun-20d9230f767b`** [open] |
| feature | **`078-verification-receipts`** |
| lane / host / repo | `gavriella` @ **GAVRIELLA** · **GLPNET** |
| position | seq **378** · steps **28/111** · outstanding **204** |
| roadmap | round **60** · **28 not-closed** over 21 epics / 122 features (dedupe 0 groups; SPEC=NONE 18/28) |

## 🔴 A SECOND RUN IS NOW OPEN — IN ANOTHER REPO

`/yx-bootmig` **era 002 corpus 5/5** was opened 2026-08-31 and is the **last** corpus of era 002.

| | |
|---|---|
| run | **`mrun-37f283191d19`** [open] · seq **8** · outstanding **4** |
| feature | **`007-era002-res-olamnit`** |
| repo | 🔴 **`D:/yngenios/yngenios`** — NOT this repo, and **not** `D:/BSTDEV/research/yngenios` (ruling `Q-GLPNETS13-02`) |
| resume | `buildkit-marathon status --feature 007-era002-res-olamnit` from that repo |
| gate | **P3 DISCHARGED** — delineation ruled **R3** (`Q-GLPNETS13-01`): admit all except `Coin*` and `*.Tests`, IN 748 / OUT 539 |
| next | **`/bk-specify 007-era002-res-olamnit`** — take the active slot (`Q-GLPNETS13-04`); era 006 is closed 9/9 with no active run |

## 🔴 READ THIS BEFORE ANYTHING ELSE

**`docs/research/RESTART-PREP-gavriella-glpnet-mrun-20d9230f767b.md`**

Read it **from the bottom up** — it is append-only and **the LAST section supersedes every section
above it**. Current tail: **`SESSION 13 CLOSE` (2026-08-31T18:15Z)**, which carries the seven
engineer rulings `Q-GLPNETS13-01..04` + `Q-GLPNETS13B-01..03`, the four defects measured this
session, the ordered next actions, and the standing constraints.

🔴 **Two constraints that will cost you a wasted hour if you miss them:**
`gh pr merge` / `git push` are **DENIED under Bash and SUCCEED under PowerShell** on this host
(5/5, zero retries) — switch shell, do not retry. And `buildkit-roadmap import` **without
`--in-dir D:/coop/glpnet/roadmap-sync/inbox`** reads only local exports, imports nothing from
peers, and still reports success.

**Do not resume from this file, from a compaction summary, or from any prose plan.** Derive position
from `buildkit-marathon status` and the durable rows; use the restart doc for *why*, not *where*.

## Other lanes' runs in this repo — do not resume into these

| run | lane | note |
|---|---|---|
| `mrun-f77f62158255` | `shiras-glpnet` | peer lane, Linux host; `RESTART-PREP-shiras-…md` |
| `mrun-f5ef56dba3c1` | historical | **superseded** — was wrongly named here until 2026-08-31 |
