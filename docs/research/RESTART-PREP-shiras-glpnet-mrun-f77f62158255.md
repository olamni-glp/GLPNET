<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — `shiras` / `glpnet` / `mrun-f77f62158255`

    LANE     shiras-glpnet          HOST shiras (Linux)
    RUN      mrun-f77f62158255      FEATURE glpnet-shiras-tidyup-and-scheduler-rootcause
    UPDATED  2026-08-31T11:50Z   ·  restart-safe: YES   ·  🔴 REBOOT-SAFE: **NO** (gate still exits 1 - see §0)
    BRANCH   097-shiras-restart-prep-frontend-handoff   (095 is MERGED via PR #249)

> **RESUME WITH EXACTLY: `resume marathon`** — nothing else is needed. The pointer is durable.

---

## 0 · 🔴 REBOOT — **THIS SECTION'S OLD CLAIM WAS FALSE. CORRECTED 2026-08-28T02:00Z.**

> **The previous version of this file said "REBOOT-SAFE: YES (bk-onrestart preflight exit 0)".
> Re-measured bare on 2026-08-28T02:00Z: the gate exits `1`.** Do not reboot until §0.1 is fixed.

```
$ ~/.local/share/buildkit/deploy-home/onrestart/bk-onrestart.sh preflight ; echo $?
  FAIL systemd trigger is WantedBy=, which is '' - it will NOT fire
  FAIL triggers disagree: '/mnt/biwin/D_DRIVE/YNGENIOS/yngenios/scripts/fleet/post-reboot-restart.sh'
                     vs 'bash' - two canonical roots
  NOT SAFE TO REBOOT - 1 blocking check(s) above
  1                                       <-- REAL exit code
```

### 0.1 · What must be fixed before a reboot

1. **`WantedBy=` on the systemd user unit is EMPTY** — "enabled" but it will never fire.
   Set `WantedBy=default.target`.
2. **Two canonical roots** — the XDG autostart fallback and the systemd unit invoke
   *different* launchers. Collapse to one. (This is @ariellas' `20260828T0005Z`
   "two launchers" finding, reproduced here — 2-of-2 hosts, not host-specific.)
3. Re-run `preflight` **bare** and read `$?`.

### 0.2 · 🔴 NEVER PIPE THE GATE

`preflight 2>&1 | tail` prints every FAIL and still returns **0** — that is `tail`'s status,
not the gate's. **Piping turns a refusal into a pass.** Run it bare.

### 0.3 · The lane count is 18, not 15 — and 2 have no session

`bk-onrestart list` → **18 lanes**, and preflight says "0 will be skipped" — but `claudesat`
and `game_dev_demo` show `SESSION = NO`. They will start a *fresh* `claude` that looks alive
and has lost its thread. **"0 skipped" ≠ "18 will resume with their thread."**
**Verify against `list`, never against a number written in a document — including this one.**

---

## 0b · REBOOT MECHANICS — **superseded by §0; the numbers below are the 2026-08-27 snapshot**

> 🔴 **DO NOT read "Just reboot" as current.** §0 supersedes it: the gate now exits 1.

**When §0.1 is fixed**, `bk-onrestart` fires at logon and restores the fleet.

    layout        1 WINDOW  (shiras uses ONE window for ALL lane tabs; ariellas uses TWO)
    tabs          18        window1=18, window2=0  (was 15 on 2026-08-27; VERIFY WITH `list`)
    command       claude --continue --autocompact 1000000   (resumes MID-THREAD, never summarises)
    triggers      🔴 BROKEN - systemd unit WantedBy= is EMPTY; autostart names a DIFFERENT script (§0.1)
    terminal      xfce4-terminal
    preflight     🔴 exit 1  ->  NOT SAFE TO REBOOT  (re-measured 2026-08-28T02:00Z)

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
~/.local/share/buildkit/deploy-home/onrestart/bk-onrestart.sh verify    # compare to `list`, NOT to a number here
```

**Before rebooting again:** `bk-onrestart.sh preflight` — an executable gate (exit 0 = safe), not a
sentence typed optimistically. 🔴 **Run it BARE — a pipe returns `tail`'s 0 and hides every FAIL (§0.2).** If `/mnt/gavri/d` is absent afterwards that means
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

## 5 · 🔴 WHAT'S NEXT — **ENGINEER-RULED. Start with the FRONT-END feature, not the scheduler.**

> 🔴 **RULING `Q-glpnetshiras-04` (2026-08-30) SUPERSEDES THE SCHEDULER SEQUENCE BELOW.**
> **NEXT WORK = `front-end-goal-term-acceptance-completeness-parser-repl-goal-builders-cross-runtime`**
> (roadmap, WSJF 3.60 / RICE 3000, state `promoted`). Hand off with:
>
> ```
> /bk-specify "Front-end goal-term acceptance completeness (parser + REPL goal builders, cross-runtime)"
> ```
>
> It covers three located product defects: `=..` rejected in clause bodies; structs-in-lists in REPL
> goals (**recorded location is STALE — re-verify in `GlpEngine` first, it may already be fixed**);
> and the C# REPL `_SetupArgument` throwing on `UnderscoreTerm`.
> Ledger: `.specify/decisions/engineer-decisions.jsonl`. **Do NOT resume at S14/S1 below** — that
> sequence is retained for context and is NOT the instruction.

**Retained for context only (superseded): the scheduler analysis.** S14 was the live root cause
before the ruling.

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

## 5a · 🔴 SESSION 2026-08-31 — READ THIS FIRST. **THE CODEX GATE IS DISCHARGED; ONLY THE RELEASE IS LEFT.**

### The one thing blocking completion

**`buildkit release` was NOT run — it was BLOCKED BY THE PERMISSION CLASSIFIER, not by a gate.**
Everything the engineer's ruling `Q-glpnetshiras-01` required *before* it is DONE. On restart, the
release needs the engineer to approve the command, then:

```bash
git checkout develop && git pull && buildkit release
```

`develop` is **39 commits ahead of `main`**; last tag `v2026.08.28.1`.

### What was delivered (all PUSHED; PR #249 MERGED into develop)

| | |
|---|---|
| **codex gate** | **CONVERGED — 7 cycles, 12 findings, ALL FIXED, cycle 7 CLEAN.** Runs `20260831T094700Z..112539Z` under `reviews/095-.../`. |
| **PR #249** | MERGED into `develop`; **CodeQL 5/5 pass**. Conflict on `engineer-decisions.jsonl` resolved by **UNION** per ruling `Q-tidyup-20260827T214708Z` (42+50 → 59, 0 dropped). |
| **rulings** | 4 recorded: `Q-glpnetshiras-01..04`. `bkquestion record`/`decisions` now enforce supersession. |
| **roadmap** | trust 6→21 keys, own key published; import/reconcile×2/dedupe×2 (**0 groups**)/export **20/118/3806**, published to the coop inbox. |
| **coop** | ACK sweep + a **CORRECTION withdrawing my own "two canonical roots" claim** (it was a gate artefact). |

### 🔴 THE FOUR ENGINEER RULINGS — three DONE, one OPEN

1. `Q-…-01` **Codexreview first, then ship** → gate CONVERGED, PR merged. **RELEASE still owed (blocked above).**
2. `Q-…-02` **Revert the C#15 fork** → DONE. `Directory.Build.props` deleted, 3 generated `out/csharp` csproj restored.
   **Proven after the merge:** `dotnet msbuild -getProperty:LangVersion` → `preview` on both trees,
   and develop's `net11.0` TFM fix survives alongside it.
3. `Q-…-03` **Do not reboot; fix trigger** → unit fixed (`enabled`, `WantedBy=default.target`, 0 parse errors).
   **The gate still exits 1 on a FALSE POSITIVE** (§0.2) — do not read that as a new host defect.
4. `Q-…-04` **NEXT WORK = front-end goal-term acceptance** → see §5, not started.

### New marathon items this session

- **S17** codex CLI IS present (`codex-cli 0.149.1`) — S7's dischargeable-by-absence is RETIRED.
- **S18** the C#15 fork — **RESOLVED** by ruling 2.
- **S19** trust store 6→21; **19 docs still REFUSED** on 3 unpublished `ariellas` keys (publisher-side only).
- **S20** codex gate converged (detail above).
- **S21** 🔴 **TAKT RETRIEVAL GAP, NOT ROOT-CAUSED.** `emit_tokens` wrote `phase='resume-marathon'`
  to both lake roots and duckdb reads the row back directly (`total_tokens=276158`) — but
  `phase_token_rollup()` does **not** surface that key, while siblings like `resume-marathon-ops` are
  present. So a lane can write the mandated per-phase token data, verify it on disk, and still have it
  invisible to the mandated retrieval path. **This is NOT the `kind=token`/`kind=tokens` split — I write
  the plural.** Reported, not resolved.

---

## 5b · WHAT THE 2026-08-28T01-02Z SESSION DELIVERED (supersedes §4/§5 where they disagree)

- **Trust store 6 -> 21 keys.** `buildkit-roadmap trust import --from /mnt/gavri/d/coop/trust-exchange`
  (15 keys) + own key **published** as `shiras__4bc1a1a78cf43b17.pub`. **19 documents STILL REFUSED**
  on three ariellas keys never published: `66c9f04e045be536` (buildkit, 7), `810f0bcaa9133135`
  (yngenios-windows, 7), `8422afd5f6778bbd` (olamnit-assistant, 5). **Publisher-side remedy only.**
- **Roadmap cycle:** import (correct `--in-dir`) -> 2 new files, **139 `crucible-xyz` entities correctly
  refused as foreign**, 76 inbox files missing a `.license` sidecar; reconcile x2 = **in sync**;
  dedupe x2 = **0 groups (2nd consecutive)**; export **20 epics / 118 features / 3806 lines** ->
  `shiras__glpnet__20260828T014423Z.json`, published to the coop inbox.
- **BK-REPORT-v1 ran end to end** under `/home/shira/.local/share/bkvenv/bin/python` with the §2 env.
  **Takt read FROM the DuckLake: 105,045,962 tokens over 74 phases, coverage 232/2789 rows (8%).**
- **Commits `8351b635` + `dece1ac4` PUSHED**; branch 0 ahead / 0 behind origin; **no phantom `D:`** this session.
- **Marathon items captured: S17, S18, S19** (below).
- **Engineer question set** `.specify/decisions/Q-glpnetshiras-20260828T0200Z.json` — 4 questions,
  validated by `tools/bkquestion/bkquestion.py`.
- **Coop:** `ACK-SWEEP-20260828T0200Z-shiras-glpnet-...` published to BOTH channels.

### New marathon items

- **S17 — `codex-cli 0.149.1` IS INSTALLED HERE.** `/home/shira/.local/bin/codex`, and a peer lane ran
  `buildkit-codexreview codex-pass --cycle 5` on this host during the session. **S7's premise
  ("codex CLI absent here") is STALE — the gate is runnable and must not be discharged by absence.**
- **S18 — this branch FORKS develop's deliberate `.targets` decision:** a redundant root
  `Directory.Build.props` whose comment is provably false, plus `LangVersion` edits inside
  **codeconv-GENERATED** `out/csharp/*.csproj`. **Not reverted** — engineer question `Q-glpnetshiras-02`.
- **S19 — trust/refusal state** as recorded above.

### 🔴 The release directive could not be satisfied as worded

`/bk-release any completed fully implemented and codex reviewed features or patches`: **nothing in
this repo meets that bar.** develop is 2 ahead of main and both are docs/merge commits; this branch is
docs + roadmap-sync artifacts; the only roadmap feature at state `implemented`
(`qr-link-provisioning`) **has no spec dir on develop** (recorded as F067); no codexreview has run
against this branch; **PR #246 from another lane is open against develop**; and S6 records a standing
peer RELEASE HELD. **Nothing was cut.** Engineer question `Q-glpnetshiras-01`.

---

## 6 · 🔴 OPEN / BLOCKED — carry these forward

- ~~Commit not pushed~~ **DONE** — `08a02c5c..90aeb5bf` pushed to origin (engineer granted).
- **`buildkit release` NOT run.** RELEASE HELD stands (ariellas, 2026-08-24, pending quiescence);
  nothing was cut.
- 🔴 **CODEX GATE IS LIVE — S7 IS SUPERSEDED. DO NOT DISCHARGE IT BY ABSENCE.**
  S7 said *"ruled dischargeable by Claude reviewers; codex CLI absent here"*. **That premise is
  dead: `codex-cli 0.149.1` IS installed at `/home/shira/.local/bin/codex` (S17).** Ruling
  `Q-glpnetshiras-01` requires **codexreview FIRST, then ship**. A release without a real codex
  pass is a gate bypass.
  🔴 **AND `0 findings` FROM A TIMED-OUT PASS IS NOT A CLEAN REVIEW** — the first attempt
  printed `review (base): 0 finding(s)` after `codex produced no stdout` + `TIMED_OUT after 200s`.
  **Check `codex.md` for `TIMED_OUT` and `run.json` for `converged` before believing a green.**
  A real pass needs `--max-seconds 1200`; it then returned **3 P1 findings** (run `20260831T095127Z`).
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
