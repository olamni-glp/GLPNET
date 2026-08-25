<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART PREP — resume with `resume marathon`

🔴 **Trap 13: never select a restart document by filename.** This table identifies the run. If
these fields do not match your session, **this is not your document** — there are sibling restart
docs in this same directory for the `gavriella` and `ariellas` lanes.

| field | value |
|---|---|
| **run_id** | `mrun-f77f62158255` |
| **lane** | `shiras` |
| **host** | `shiras` (**Linux**, not Windows) |
| **repo** | `GLPNET` at `/mnt/biwin/D_DRIVE/BSTDEV/research/crucible/glp/GLPNET` |
| feature | `glpnet-shiras-tidyup-and-scheduler-rootcause` |
| branch | `095-shiras-glpnet-onboard-and-scheduler-rootcause` |
| written at | **2026-08-25T08:45Z** (session 1 close) |

## Resume in one line

```
buildkit-marathon resume --feature glpnet-shiras-tidyup-and-scheduler-rootcause
```

🔴 **`--feature` is mandatory** — there is no `.specify/feature.json` in this repo, by design.
🔴 **Run buildkit commands SERIALLY** — concurrent invocations contend for the deploy-home lock.
🔴 **`PYTHONUTF8=1`** is set by habit on this fleet; harmless on Linux, required on the Windows peers.

## State at hand-off

| field | value |
|---|---|
| branch | `095-shiras-glpnet-onboard-and-scheduler-rootcause`, pushed at `12dea5e7` |
| **PR** | 🔴 **NOT OPENED — GitHub API rate limit, not a conflict.** See "First action" below. |
| marathon | `seq=10`, **0/0 steps**, **10 outstanding items**, all `parked` |
| develop vs main | **94 ahead**, 0 open PRs |
| release | **HELD by engineer ruling** — dry-run green (`v2026.08.24.1`), deliberately not cut |
| scheduler | shiras onboarded: **18 caps · 105 calendar windows · 1 claimed WP** |
| board fold | 32 WPs — backlog 23 · ready 3 · in-progress 4 · done 1 · escalated 1 |
| roadmap | 🔴 **catalog will not project here (OOM).** Read from signed exports only. |
| roadmap (from export) | **25 not-closed = 1 implemented · 3 analyzed · 6 specified · 15 promoted, across 8 epics** |
| engineer rulings | **7 recorded** in `.specify/decisions/engineer-decisions.jsonl` |

## 🔴 First action next session — open the blocked PR

The branch is pushed and safe. The PR failed **only** because the shared GitHub account was at
`core: remaining 0 / 5000`. **Check the budget before believing any `gh` error:**

```bash
gh api rate_limit --jq '.resources.core'
gh pr create --base develop --head 095-shiras-glpnet-onboard-and-scheduler-rootcause
```

🔴 **`gh pr create` reported `"No commits between develop and <branch>"` — that message was FALSE.**
It is what an exhausted core budget looks like through the GraphQL PR path. Do not delete the branch
on the strength of it.

## 🔴 Corrections carried forward (do not re-derive)

1. **The "classifier blocks commit on 2nd host" hypothesis is WITHDRAWN** — mine, from
   `ACK-20260824T112831Z`. It was the shared GitHub rate limit all along. Retry "fixed" it only
   because the hourly budget reset.
2. **`buildkit-roadmap status` reporting "Roadmap is empty" on this host is FALSE-EMPTY.** The
   import OOMed materialising HEAD and `replay` then found 0 lines. Never quote that as a roadmap
   reading. Use `.specify/roadmap-sync/exports/` (newest: `gavriella__glpnet__20260824T170210Z.json`).
3. **The not-closed renderer's `{promoted,specified,captured}` whitelist drops `analyzed` AND
   `implemented`** — not just `implemented` as previously reported. Renderer 23 vs signed-export
   fold 25. The dropped set includes `verification-receipts-and-loud-failure`, the **highest-WSJF
   row on the board (7.80)**. Always fold `state != 'closed'`.
4. **Do NOT codify a new scheduler-healing feature.** Two already exist:
   `scheduler-feature-stream-durable-healing-and-hardening` (promoted, WSJF 2.62) and
   `coordination-feature-stream-durable-superset-fix` (specified, WSJF 4.25 — **now claimed by shiras**).
5. **Do NOT codify a new bk-onrestart feature either.** Two already exist:
   `bk-onrestart-per-host-configurable-auto-install` (**specified**, WSJF 4.20) and
   `bk-onrestart: per-host reboot lane relaunch` (**promoted**). The engineer's requested
   1-or-2-window topology work belongs **in the specified one**, not a third row.
6. **shiras cannot run** the Dart/Flutter GLP REPL suite, `glp_repl.exe`, or **anything needing
   `codex`** (not installed). Declared under olamnit's lane/host-affinity mandate §5.

## The root cause, settled — do not re-investigate

**The feature stream does not stall at one gate; it leaks at three.** From the board's own 18
`gap-to-backlog` cards:

| stage | detector | count | worst age |
|---|---|--:|---|
| claim → **ready** | *"the allocator cannot see it"* | 6 | **1,711,349s (19.8 d)** |
| ready → **dispatch** | *"ready-undispatched"* | 5 | 68,169s |
| dispatch → **in-progress** | *"a live allocation exists"* | 3 | ~270,000s |

**A detector exists at every transition; a writer at none.** Fixing only `ready` moves the stall one
stage downstream — and the later two card classes are the evidence that it already did.

**Second, independent cause:** a lane with **zero `caps/` records is structurally unallocatable**.
shiras had 0 until 2026-08-25. Every lane should verify `caps/<actor>/` is non-empty.

## What's next, in order

| # | step | size | state | blocked-by |
|--:|:---|:---|:---|:---|
| 1 | Open the blocked PR (above) | nano | **unblocked** | rate limit reset only |
| 2 | S3 — design the three transition writers via `/bk-3rtask` | saga | **gated** | **PEER** — board is ariellas-owned |
| 3 | Drive the claimed `wp-coordination-feature-stream-durable-superset-fix` specify→close | saga | **unblocked** | engineer permission recorded `Q-GLPNETSHIRAS2-01` |
| 4 | S4 — file the roadmap OOM as a buildkit defect | mini | **unblocked** | — |
| 5 | S9 — enumerate features stuck at `specified` **from the signed export** | maxi | **unblocked** | ruling `Q-GLPNETSHIRAS2-03` reopened this |
| 6 | S8 — marathon → `/bk-flow` migration readiness | saga | **unblocked** | engineer marked critical |
| 7 | S5 — tidy-up: 12 remote heads | midi | **gated** | **ENGINEER/PEER** per lane affinity |
| 8 | S6 — release | nano | **HELD** | **PEER** — gavriella has not answered ariellas' 06:55Z ask |

**Cheapest real progress next session: #1 → #4 → #5.** All unblocked, all cheap, and #5 is the work
the engineer marked imperative that this session could not reach.

## Evidence caveats

- **This lane contributes ZERO takt rows.** 0 phases measured, no takt capability installed here.
  Do **not** fold shiras into any fleet takt figure — quoting an absent lane as 0 is the takt-lake
  silent-loss defect in a new costume.
- All roadmap figures here are from a **signed peer export**, not a local fold. Stated so no one
  mistakes provenance.
- Marathon shows **0/0 steps with 10 outstanding items**: the items are `parked` intake, not
  sequenced steps. **Report items, not a completion ratio.**
