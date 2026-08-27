<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — `shiras` / `glpnet` / `mrun-f77f62158255`

    LANE     shiras-glpnet          HOST shiras (Linux)
    RUN      mrun-f77f62158255      FEATURE glpnet-shiras-tidyup-and-scheduler-rootcause
    BRANCH   095-shiras-glpnet-onboard-and-scheduler-rootcause
    UPDATED  2026-08-27T22:30Z   ·  restart-safe: YES   ·  REBOOT-SAFE: YES (bk-onrestart preflight exit 0)

> **RESUME WITH EXACTLY: `resume marathon`** — nothing else is needed. The pointer is durable.

---

## 0 · 🔴 REBOOT — WHAT HAPPENS AND HOW TO VERIFY IT

**Just reboot.** `bk-onrestart` fires automatically at logon and restores the whole fleet.

    layout        1 WINDOW  (shiras uses ONE window for ALL lane tabs; ariellas uses TWO)
    tabs          15        window1=15, window2=0, skipped=0
    command       claude --continue --autocompact 1000000   (resumes MID-THREAD, never summarises)
    triggers      systemd user unit (enabled, WantedBy=default.target) + XDG autostart (--delay 45)
    terminal      xfce4-terminal
    preflight     exit 0  ->  SAFE TO REBOOT

**All 12 named fleet lanes have a tab**, plus 3 more:
`ospark · ulpnit(hatzinor) · tefl · buildkit · olamnit · qhstate · yngraw · yngwin · crucible ·
glpnet · lejepa · mstack` + `yngorg · yngapp · ynglin`.

**By hand, only if the trigger does not fire:**

```bash
~/.local/share/buildkit/deploy-home/onrestart/bk-onrestart.sh launch --wait-for-mounts
```

**VERIFY BY COUNTING PROCESSES, never by trusting the launch message** — 12 tabs opening and
running nothing is a measured failure mode on this fleet:

```bash
~/.local/share/buildkit/deploy-home/onrestart/bk-onrestart.sh verify    # expect 15
```

**Before rebooting again:** `bk-onrestart.sh preflight` — it is an executable gate (exit 0 = safe),
not a sentence typed optimistically. If `/mnt/gavri/d` is absent afterwards that means
**"I cannot see the board"**, NEVER "the board is empty".

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
With them: `reachable`, `local 991 / fleet-this-host 138`, and `scheduler_ops` moves
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
| 🔴 phantom `D:` | **CHECK `git status` FOR AN UNTRACKED `D:/` EVERY SESSION.** It REGENERATES (S13). It holds REAL, never-lake'd parquet — recover into `/mnt/gavri/d/_takt-lake` preserving `kind/host/date`, verify by basename, THEN delete. 18 unique records were recovered this way on 2026-08-26 alone |
| cross-board poll | **UNSCOPED** (`bk-flow poll --root <r> --actor shiras`). Passing `--repo` while polling ANOTHER repo's board refuses foreign envelopes by design and yields a false `bound=0` |

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
- Commits `8a5fd60e` + `90aeb5bf` + `2a8e0fab` — **PUSHED** to origin.
- **S13 PROVEN PHYSICALLY**: 4 unique measurements recovered from a literal `D:` dir; phantom removed.

## 5 · 🔴 WHAT'S NEXT — start here

**S14 is the live root cause and the highest-value next move.**

1. **S14 — binding is envelope-only.** Board: 32 packets, **1 envelope**, `by_source
   {envelope: 0, link: 0}`, so **0 of 32** bind — *even with a full roadmap*. **11 of 32 are
   id-resolvable** (3 exact + 8 after `wp-` strip) but the binder never tries an id match.
   **RULED (`Q-07`, 2026-08-26): BY DESIGN → S3 owns the envelope writer.** An id-match fallback is
   **REJECTED**; 11-of-32 is evidence of scale, not a licence to bind by id. Accepted consequence:
   the board stays 0/32 bound until S3 ships.
2. **S3 — RE-SCOPE REQUIRED (S15).** Fleet measurement across 7 boards: **`bound` ≡ `envelopes`
   EXACTLY** (424 packets, 63 envelopes, 63 bindings = 14.9%). Confirms `Q-07`. **But mstack is 71%
   bound and still dispatches 0** — binding is **NECESSARY BUT NOT SUFFICIENT**. Only **5 of 424**
   packets are dispatchable fleet-wide. **S3 as scoped ("ship the envelope writer") would ship,
   measure green, and change nothing.** It must also cover the post-binding gate. *The second gate is
   NOT yet identified — do not propose a remedy for it before measuring it.*
   glpnet-specific: our 1 envelope names a **foreign repo** (`bound` 1→0 under `--repo`), so glpnet
   has **zero valid envelopes of its own**.
3. **S5** — repo tidy-up: 12 remote heads, 0 open PRs, develop ahead of main. Engineer/peer-gated.
4. **S9** — features stuck at `specified`. **Now unblocked** (S4 was its blocker); the not-closed
   table enumerates 23 (9 specified / 14 promoted, 6 epics).
5. **S8/S11 — RULED (`Q-09`) re-sized saga→mini to bk-flow ADOPTION**; cutover scope DROPPED. There
   is **no marathon→bk-flow cutover**: bk-flow *layers on* marathon and `open` seeds a marathon run.
   Not closed — adoption is partial until S14/S3 lands.

**Sequenced this session** (order keys): S1 `@1.0` → S3 `@2.0` → S14 `@5.0` → S9 `@6.0`.

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
