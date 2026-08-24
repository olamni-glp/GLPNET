<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART PREP — resume with `resume marathon`

🔴 **Trap 13: never select a restart document by filename.** This table identifies the run. If
these four fields do not match your session, this is not your document.

| field | value |
|---|---|
| **run_id** | `mrun-20d9230f767b` |
| **lane** | `gavriella` |
| **host** | `GAVRIELLA` |
| **repo** | `GLPNET` (`D:\BSTDEV\research\GLP\GLPNET`) |
| feature | `078-verification-receipts` |
| written at | **2026-08-23T23:45Z** |

## Resume in one line

```
buildkit-marathon resume --feature 078-verification-receipts
```

🔴 **`--feature` is mandatory** — there is no `.specify/feature.json` in this repo, by design.

🔴 **Do NOT use `glpnet-full-completion-programme`.** That feature name resolves to
`mrun-f5ef56dba3c1`, which is the **ariellas lane's** run and **does not exist in this machine's
store** (checked: 600 targets, absent). `docs/current_plan.md` is the ariellas pointer, not this
lane's. Marathon state is per-machine and out-of-repo — it does not travel with the repo.

🔴 **Run buildkit commands SERIALLY.** Two concurrent invocations contend for the deploy-home
registry lock and the second reports a "STUCK lock" naming a **dead PID**. Measured this session:
PID 11260 was reported stuck and was genuinely not running, while `.lock` was simultaneously held
by a live handle and `.lock.meta` had already moved to PID 12472. The message is unreliable in
both directions — serialise instead of reaping.

## State at hand-off

| field | value |
|---|---|
| branch | `develop`, clean, pushed at `8d1707a2` |
| steps | **24 / 97** complete |
| outstanding items | **166** |
| develop ahead of main | **32** |
| open PRs | **1** — draft #111 only |
| regression gate | **561 / 559 pass / 2 fail / 0 skip** (the 2 are pre-existing Section T 064 drills) |
| roadmap | 24 not-closed = 1 analyzed · 15 promoted · 8 specified, across 6 epics; `--check` exit 0 |

## What's next, in order

| # | step | size | state | blocked-by |
|---:|:---|:---|:---|:---|
| 1 | `/bk-implement 078` | saga/35 | **gated** | **ENGINEER** — Block 51 (two-repo ship) |
| 2 | TIDY-Y15 author `.claude/skills/bk-flow/SKILL.md` | mini/7 | **held** | **ENGINEER** — Block 51 |
| 3 | TIDY-Y04 / Y01 / Y03 | — | **done** | already merged |
| 4 | TIDY-Y02 merge 085-onrestart | micro/3 | **held** | **PEER** — live branch, olamnit moved it 3× |
| 5 | TIDY-Y06 / Y07 067 + 067b | midi/11 ×2 | **gated** | **ENGINEER** — graduate to own pipeline |
| 6 | TIDY-Y08 051-ynet-transport | midi/11 | **gated** | **ENGINEER** — never triaged |
| 7 | TIDY-Y09 050-vs-059 survivor | midi/11 | **gated** | **ENGINEER** — X10 owed |
| 8 | TIDY-Y10 030-phase8-polish ×2 | mini/7 | **unblocked** | — |
| 9 | TIDY-Y12 backup-upgrade-buildkit | micro/3 | **unblocked** | — |
| 10 | TIDY-Y13 016 + 017 archive-and-drop | mini/7 | **unblocked** | — |
| 11 | TIDY-Y14 C2 remote cleanup | mini/7 | **unblocked** | must run **LAST** |
| 12 | SCHED-R1 readiness writer | maxi/17 | **unblocked** | — gates R7 |
| 13 | SCHED-R4 dependency edges | midi/11 | **unblocked** | — |

**The cheapest real progress next session is #8 → #10 → #9** (Y10, Y13, Y12): all unblocked,
all class-C2 branch tidy-ups, and the exact route that worked twice this session — probe with
`git merge-tree` first, verify preservation **by content**, then drop.

## 🔴 Corrections carried forward (do not re-derive)

1. **SCHED-R7 sizing WITHDRAWN.** `consolidated-hardening-2026-08-23.md` sizes it an independent
   midi/11. It is a **dependent of SCHED-R1** — proven by dry-run: `backlog ⇒ no claim ⇒ no bind`,
   with 25 of 32 packets in backlog giving a hard **≈15%** ceiling.
2. **SCHED-R2's `complete` mark on this run is FALSE and cannot be undone** (recorded in the
   ledger by a prior session). **Do not trust step-completion counts on this run** — report
   points, not ratios.
3. **The dropped Y11 branch was not merely a backup.** It carried a complete **078 MVP
   implementation** (`codeconv/src/codeconv/receipts/`, 8 modules, 29 tests green) that is **not on
   `develop`**. It lives only at tag `archive/backup__078-olamnit-impl-preserve-20260820`.
   **Read that tag before planning any 078 implementation** — it materially changes Block 51.

## TAKT DuckLake — required config on this host

`config.local.json` (gitignored, machine-local) MUST carry:

```json
{ "sched_root": "D:/coop/glpnet/sched",
  "takt_lake_root": "D:/_takt-lake",
  "takt_lake_fleet_root": "D:/coop/_takt-lake" }
```

🔴 **Without `takt_lake_fleet_root` the tool defaults to `I:\coop\_takt-lake`, which is NOT
mounted on this host, and every fleet write fails SILENTLY.** That hid 47 records and made
`host=gavriella` absent from the fleet lake entirely. Full write-up:
`docs/research/takt-ducklake-fleet-root-defect-2026-08-24.md`.

**Report takt FROM the lake**, not only from the CLI:

```python
import duckdb; L="D:/coop/_takt-lake/takt"
duckdb.connect().execute(f"SELECT phase,count(*),median(seconds)/3600 FROM read_parquet('{L}/kind=stage/**/*.parquet',hive_partitioning=1,union_by_name=1) WHERE host='gavriella' AND seconds IS NOT NULL GROUP BY 1").fetchall()
```

Lake and marathon agree at **4.65 h / 19 measured facts** — quote the agreement, not one source.

## Engineer questions are asked in BK-STD-2 shape

`BK-STD-2` (ariellas' proposal, **adopted here unchanged**) is the fleet question format. There is
**no precoded template file anywhere** — that absence is established, broadcast, and not worth
re-searching. Do not author a variant; contribute amendments to ariellas' hardening.

## Evidence caveats

- **Takt bands must be quoted with any takt figure** (trap 10): feature total **4.65 h over 19
  measured steps**, band **1.5–6.0 h**, verdict in-band — but **78 of 97 steps are unmeasurable**
  and are *not* folded in as zero. The verdict is recomputed at read time and is not a record.
- **Owner coverage in `.specify/roadmap-owners.json` is 2 of 24 rows**, each backed by a durable
  op. The other 22 are deliberately undeclared — a guessed owner is how the 077 duplicate
  allocation happened.
- Pipeline-derived counts are **not** comparable across lanes (trap 12); only roadmap counts are.
