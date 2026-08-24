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
| written at | **2026-08-24T12:10Z** (session 4 close) |

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
| branch | `develop`, clean, pushed at `e5a707bf` |
| steps | **28 / 97** complete |
| outstanding items | **169** |
| develop ahead of main | **76** |
| open PRs | **0** |
| regression gate | **561 / 559 pass / 2 fail / 0 skip** (the 2 are pre-existing Section T 064 drills) |
| recovered 078 MVP | **29 / 29 targeted receipts tests pass on `develop`** |
| roadmap | 24 not-closed = **3 analyzed · 15 promoted · 6 specified**, across 6 epics; `--check` exit 0 |

## 🔴 THE RELEASE GATE IS A TOOL DEFECT, NOT MISSING WORK

**`/bk-codexreview` CANNOT be discharged on this host.** Attempted 3× this session; both
documented routes fail:

1. **`--scope diff` inlines the diff body** despite documenting the opposite ("size-invariant
   BRIEF … NEVER the diff body"). A 35-file / 66,236-insertion diff → **2.6 MB stderr** ending in
   `ERROR: Codex ran out of room in the model's context window.`
2. **`--scope <path>` refuses a subtree with 8 tracked files** (`refused: empty_scope`),
   reproduced twice. That is the escape hatch from (1), and it is closed.

**Ruled out by measurement — do NOT re-investigate:** the codex CLI (verified working,
`CODEX_OK`), the `review` subcommand, the stdin-prompt form, the Windows `.cmd` launcher capture,
and output discarding. Full evidence:
`docs/research/codexreview-two-blocking-defects-2026-08-24.md` (on `develop`).

**Consequence:** 078's MVP is implemented and green (29/29 targeted tests) but **no release can be
cut under the "codex reviewed" criterion** until buildkit fixes defect 1 or 2. Broadcast to 5
channels; PR #224 merged. **The gate is the review tool, not the code.**

**Next session: do not retry codexreview on a large diff.** Either wait for a buildkit fix, or ask
the engineer whether a Claude-reviewer-only run (`--reviewers N` without codex) satisfies the
release criterion.

## What's next, in order

| # | step | size | state | blocked-by |
|---:|:---|:---|:---|:---|
| 1 | **SCHED-R1 readiness writer** | maxi/17 | **unblocked** | — the ceiling-lifter; gates R7 |
| 2 | SCHED-R4 declare dependency edges | midi/11 | **unblocked** | — `edge_coverage` is 0.0 |
| 3 | TIDY-Y14 C2 remote cleanup | mini/7 | **unblocked** | must run **LAST** of the Y-series |
| 4 | TIDY-Y02 merge 085-onrestart | micro/3 | **held** | **PEER** — olamnit moved it 3× |
| 5 | TIDY-Y06/Y07 067 + 067b | midi/11 ×2 | **gated** | **ENGINEER** — graduate to own pipeline |
| 6 | TIDY-Y09 050-vs-059 survivor | midi/11 | **gated** | **ENGINEER** — X10 owed |
| 7 | TIDY-Y16/Y17/Y18 | midi/maxi/midi | **unblocked** | ERA metric · unique allocation · takt-only durations |
| 8 | `/bk-implement 078` | saga/35 | **re-measure first** | Q1 ruled; MVP already merged — **re-scope before starting** |
| 9 | `/bk-codexreview 078` | — | **TOOL-BLOCKED** | buildkit defect 1 or 2 |

**DONE this session:** Y05, Y08, Y10, Y11, Y12, Y13 + the Q1 MVP recovery. **Only Y14 remains of
the unblocked Y-series branch tidy-ups.** Origin is down to **14 heads**; **0 open PRs**.

## 🔴 Corrections carried forward (do not re-derive)

0. **NEVER sum raw `kind=stage` seconds from the lake — it double-counts.** I re-emitted the same
   19 marathon steps under a second verb and a naive read then reported **14.57 h / 57 facts**
   against the marathon's authoritative **4.65 h / 19**. `emit_stage` has no idempotence on
   `(feature, phase, seconds)`. I removed my 38 duplicate files. hatzinor established this for
   `kind=era` (`era_rows_for_takt` dedups, raw `query()` does not); **it is true for `kind=stage`
   too, and stage has no deduping reader at all.** **Quote the marathon figure per feature.**

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
