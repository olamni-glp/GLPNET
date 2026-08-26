<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — `shiras` / `glpnet` / `mrun-f77f62158255`

    LANE     shiras-glpnet          HOST shiras (Linux)
    RUN      mrun-f77f62158255      FEATURE glpnet-shiras-tidyup-and-scheduler-rootcause
    BRANCH   095-shiras-glpnet-onboard-and-scheduler-rootcause
    UPDATED  2026-08-26T12:55Z   ·  restart-safe: YES (--check exits 0)

> **RESUME WITH EXACTLY: `resume marathon`** — nothing else is needed. The pointer is durable.

---

## 1 · WHY BARE `resume marathon` NOW WORKS

It did not work at the start of this session (`marathon: no feature resolved`). Fixed durably:

```
.specify/feature.json   ->   { "id": "glpnet-shiras-tidyup-and-scheduler-rootcause" }
```

`buildkit-marathon resume` with **no arguments** now returns the run. Verified this session.

## 2 · 🔴 MANDATORY ENV — this host is Linux; two shipped defaults are Windows-only

**Export these before ANY buildkit takt/board command, or the measurements are silently dead:**

```bash
export BUILDKIT_TAKT_LAKE=/mnt/gavri/d/_takt-lake
export BUILDKIT_TAKT_LAKE_FLEET=/mnt/gavri/d/coop/_takt-lake
export PYTHONIOENCODING=utf-8
```

Without them `takt` reports `verdict: unreachable`, `local_records: 0` — **not an error, just zeros.**
With them: `reachable`, `local 955 / fleet-this-host 94`, and `scheduler_ops` moves
`unmeasurable → measured` (4/4 sources). Tracked as marathon item **S13**.

`sched_root` is already persisted in `config.local.json` (gitignored, machine-local):

```json
{ "sched_root": "/mnt/gavri/d/coop/glpnet/sched" }
```

## 3 · TOOL INVOCATION QUIRKS ON THIS HOST (do not re-derive)

| need | invocation |
|---|---|
| roadmap CLI | `buildkit-roadmap …` (system `python3 -m buildkit_cli.roadmap` **fails**, no module) |
| BK-STD-1 table | `python3 scripts/roadmap_open_table.py --roadmap-cmd "$(which buildkit-roadmap)"` |
| fleet SITREP | `/home/shira/.local/share/bkvenv/bin/python scripts/marathon_sitrep.py --marathon-cmd "$(which buildkit-marathon)"` |
| roadmap import | **ALWAYS** `--in-dir /mnt/gavri/d/coop/glpnet/roadmap-sync/inbox` (BK-STD-1 §4 wrong-dir trap) |
| big import | batch ~10 files via repeatable `--file`, one process per batch (OOM ceiling is per-process) |
| pgdb contention | retry loop with ~20s backoff; **never kill the holding PID** — it may be a peer's test run |

## 4 · WHAT THIS SESSION DELIVERED

- **S4 RESOLVED** — roadmap `empty → 20 epics / 118 features / 3806 journal lines`. Imported 173
  coop-inbox exports, 18/18 batches, **no OOM**. **`--allow-untagged` NOT used** (engineer ruling
  2026-08-24 rejects it; glpnet holds at honest partial convergence like mstack).
- Exported + published `shiras__glpnet__20260826T110647Z.json` to the coop inbox.
- **Fleet TAKT lake wired** on shiras (S13); takt retrieval live from the DuckLake.
- **`sched_root` wired** → `bk-flow` operational, board reads 32 packets.
- BK-STD-1 not-closed table and fleet SITREP both rendered from the **shipped** renderers (no fork).
- Coop: `ACK-20260826T1130Z` (P0 corroborated + **extended to five partitions**), and
  `CORRECTION-20260826T1145Z` (retracting my own §5).
- Marathon: S4 + S4-CORRECTED + S12 resolved; **S13** and **S14** captured.
- Commit `8a5fd60e` — **LOCAL ONLY, NOT PUSHED** (see §6).

## 5 · 🔴 WHAT'S NEXT — start here

**S14 is the live root cause and the highest-value next move.**

1. **S14 — binding is envelope-only.** Board: 32 packets, **1 envelope**, `by_source
   {envelope: 0, link: 0}`, so **0 of 32** bind — *even with a full roadmap*. **11 of 32 are
   id-resolvable** (3 exact + 8 after `wp-` strip) but the binder never tries an id match.
   **Blocked on engineer ruling `Q-GLPNETSHIRAS-07`**: is envelope/link-only binding by design
   (→ S3 owns the writer) or a regression? Do **not** self-serve a fallback resolver.
2. **S1/S3** — transition writers for claim→ready→dispatch→in-progress. S14 is the third instance of
   the same "detector exists, writer does not" shape; fold it in rather than treating it separately.
3. **S5** — repo tidy-up: 12 remote heads, 0 open PRs, develop ahead of main. Engineer/peer-gated.
4. **S9** — features stuck at `specified`. **Now unblocked** (S4 was its blocker); the not-closed
   table enumerates 23 (9 specified / 14 promoted, 6 epics).
5. **S8/S11 — RE-SIZE, do not execute as written.** There is **no marathon→bk-flow cutover**;
   bk-flow *layers on* marathon and `open` seeds a marathon run. Saga sizing was scoped for a
   migration that does not exist.

## 6 · 🔴 OPEN / BLOCKED — carry these forward

- ~~Commit not pushed~~ **DONE** — `08a02c5c..90aeb5bf` pushed to origin (engineer granted).
- **`buildkit release` NOT run.** RELEASE HELD stands (ariellas, 2026-08-24, pending quiescence);
  nothing was cut. Codex gate: ruled dischargeable by Claude reviewers (S7); codex CLI absent here.
- **SITREP `marathon.*` rows now populate** (fixed by `.specify/feature.json`). Still `—`: `host` and
  `marathon.done_items` only. **Not hand-filled.**
- **BK-STD-1 footer** prints `DEDUPE_GROUPS=?` / `RECONCILE=?`; true values are `0 groups` /
  `in sync (structurally blind)`. **Not hand-edited into the standard's output.**
- **`.specify/standards/` here holds only `bk_report_v1.py`** — no `bk_question.py` (BK-STD-2
  generator) and no `roadmap_open_table.py` copy (it lives in `scripts/`). Requested from the fleet.
- **`Q-05` SUPERSEDED then CLOSED** — union ruled; buildkit **PR #752 MERGED** (`0d159df8`, 12:20:46Z,
  by vonwenm); #749 merged (`757bb9c5`). Union reader is on `develop`. Follow-up owned by `@yngraw`:
  surface orphan `kind=token` in `rows_by_kind` + find the writer still emitting to it.
- **`Q-07` RULED** by design → S3 owns the envelope writer; id-match fallback REJECTED.
- **S13 PROVEN PHYSICALLY** — a literal `D:` dir inside the repo held **4 unique, never-lake'd**
  measurements. Recovered into `/mnt/gavri/d/_takt-lake` (verified), phantom dir removed, tree CLEAN.

## 7 · ONE-LINER TO RE-ESTABLISH STATE

```bash
cd /mnt/biwin/D_DRIVE/BSTDEV/research/crucible/glp/GLPNET && export BUILDKIT_TAKT_LAKE=/mnt/gavri/d/_takt-lake BUILDKIT_TAKT_LAKE_FLEET=/mnt/gavri/d/coop/_takt-lake PYTHONIOENCODING=utf-8 && buildkit-marathon resume
```
