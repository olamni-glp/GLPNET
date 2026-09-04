<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — shiras / glpnet · run `mrun-f77f62158255`

    written:  2026-09-04T05:50Z   (REWRITTEN WHOLE — supersedes the 2026-09-03T16:10Z revision,
                                   which codex found internally inconsistent in three places)
    host:     SHIRAS (Linux)   repo: olamni-glp/GLPNET
    branch:   100-cpm-central-package-management
    run:      mrun-f77f62158255 [open]   era S1 CLOSED 9/9
    resume:   type exactly  →  resume marathon
    status:   ✅ SAFE TO RESTART.   ⚠️ REBOOT: SAFE ONLY IF YOU LOG BACK IN — and read §6 FIRST,
                                     one lane is ALREADY DOWN before any reboot.

> **POINTER, not a ledger.** The roadmap + buildkit pipeline state are the source of truth.
> Re-locate objectively. **Never resume from a summary.**
>
> 🔴 **DO NOT TRUST A COMMIT HASH WRITTEN IN THIS FILE.** Its header named a stale commit once
> already. Read the tip with `git log --oneline -1`; that is the only in-sync claim allowed here.

---

## 1 · First three commands on resume

```bash
bk-heavy-lock --timeout 3600 -- buildkit-marathon status --feature glpnet-shiras-tidyup-and-scheduler-rootcause
bk-heavy-lock --timeout 3600 -- buildkit-marathon backlog --feature glpnet-shiras-tidyup-and-scheduler-rootcause
bk-heavy-lock --timeout 3600 -- /home/shira/.local/share/bkvenv/bin/python \
    .specify/standards/bk_report_v1.py all --feature glpnet-shiras-tidyup-and-scheduler-rootcause
```

🔴 Four rules, each learned by breaking it:
1. **Wrap every heavy buildkit call in `bk-heavy-lock`.** Waits measured this session: 5s, 26s,
   40s, 51s, 59s, 65s, 353s, **471s**. Four other lanes contend for one registry. It queues; it is
   not stuck. Never kill a holder.
2. **BK-REPORT needs the bkvenv python, NOT `python3`.**
3. **Report order is FIXED:** ROADMAP → PROGRESS → STATUS → SITREP → TAKT → NEXT.
4. **`step-start` / `checkpoint` take the `mstep-…` ID, NOT the stage name.** `--step clarify`
   fails with `no step 'clarify' in run`. Get IDs from the run mirror at
   `~/.local/share/buildkit/deploy-home/targets/b0ada634764e/marathon-mrun-f77f62158255.md`.

## 2 · WHERE THE ERA STANDS — **S1 IS CLOSED. 9/9. FULLY MEASURED.**

```
takt: 9/9 steps measurable (9 declared phase, 0 derived)
specify 0.03h · clarify 25.60h · plan 8.85h · tasks 0.20h · analyze 0.03h
implement 0.02h · codexreview 0.85h · ship 0.97h · close 0.15h
ERA ELAPSED 35.69h (band 1.5-6.0h -> over)
```

**This is the first fully-measured era for glpnet on any host** (the fleet report had glpnet at 0%).
⚠ **Read the two big numbers honestly:** `clarify` 25.60h and `plan` 8.85h are **overnight
wall-clock**, not effort — a checkpoint stamps the next step's start, so an idle night lands inside
the next phase. The `over` verdict on the era is an artefact of that, **not** slow work.

**Both root causes were PROVED and must NOT be re-derived:**
- **Q-19** — era stages were never **MINTED**, not lost. `expand --item --steps` is the only
  minting path. Remedy already applied here.
- **S1** — transition writers omit `phase`; `board_phase_seconds` (`marathon/takt.py:747`) skips a
  phase-less op **by design**, and its docstring says so. **The reader was never the defect.**

**CORRECTED DURING THE ERA — the finding grew:** S1 said **three** phase-omitting writers. **There
are FIVE.** `flow/__main__.py:1109` (`→done`) and `:1446` (generic verb) were missed. Patching only
three leaves the interval uncloseable.

## 3 · 🔴 THE ONE THING BLOCKING THIS LANE — a single permission

**Measured 2026-09-04 on CI that was 5/5 GREEN at the exact tip:**

| operation | result |
|---|---|
| `git pull --ff-only` | ✅ **WORKS** — the old "denied" record was **STALE** |
| `git push` | ✅ **WORKS** — five pushes this session |
| `gh pr merge 279 --merge` | ❌ **STILL REFUSED** by the Claude Code auto mode classifier |

**The lane did NOT route around it** by merging locally and pushing `develop` — that accomplishes
the denied action under another name.

**Consequences, both of them real:**
1. **PR #279 cannot land**, so the committed `[SUPERSEDED]` yx-bootmig correction stays invisible to
   every peer: `git show origin/develop:.claude/skills/yx-bootmig/SKILL.md | grep -c SUPERSEDED` → **0**.
2. **`buildkit release` cannot run either** — it merges a PR to `main`. So the Q-34 decision to
   supersede the S6 release hold **cannot be executed**, even though it is decided.

> ### ⏩ THE ONE ENGINEER ACTION THAT UNBLOCKS BOTH
> ```
> /permissions   → add:   Bash(gh pr merge:*)
> ```
> Recorded as ruling **`Q-glpnetshiras-31`** and backlog item **S31**.

## 4 · DECIDED THIS SESSION — cite, never re-ask

`.specify/questions/Q-glpnetshiras-20260904T0500Z.json` — **BK-STD-2 conformant, 4/4 decided.**

| qid | ruling |
|---|---|
| **Q-31** | **Test the merge gate.** Tested: pull/push work, `gh pr merge` refused → escalated (§3) |
| **Q-32** | **NEXT ERA = a P3-completion era on SHIRAS** to unblock yx-bootmig P4; agree manifest scope with `@olamnit` by coop **before** opening it (removes the Q-MARATHON-02 duplication risk) |
| **Q-33** | **S3 PARKED** pending `@buildkit`'s ACK of the filing — **not** discharged, because the code is still unfixed fleet-wide |
| **Q-34** | **The S6 release hold is SUPERSEDED** by the engineer's newer instruction; S6 discharged. Execution blocked by §3 |

Carried and still valid: Q-09 · Q-11..Q-18 · Q-20 ✅ · Q-22 ✅ · Q-23 · Q-25 · Q-26 · Q-27 · Q-28 ·
Q-29 ✅ **EXECUTED** · Q-30.

## 5 · WHAT THIS ERA ACTUALLY DELIVERED (all published, all peer-reachable)

- `coop/FILING-20260903T1954Z-shiras-buildkit-…` — five phase-omitting sites, commit-pinned lines,
  the two-sink near-miss, three asks with one deliberately left open for the owning lane.
- `coop/ACK-SWEEP-20260904T0445Z-shiras-glpnet-…` — **first sweep in nine days**, 20 documents,
  `@buildkit`'s `line-57` question answered by measurement (**glpnet's copy is TRACKED**, so their
  published "zero repo-fixable rows" is **one**).
- **Two codex passes, 13 findings, all remediated** (`b9929b23`, `db4ce9a1`). Pass 2 independently
  corroborated the false-filing record found in `clarify`.
- **Roadmap round 66**: reconcile/import/reconcile/dedupe/export/sync all `rc=0` — 21 epics /
  122 features / 4030 journal lines, **0 refused, no OOM**.

**🔴 THE CORRECTION THIS ERA EXISTS TO CARRY:** ruling Q-29 and the previous revision of THIS FILE
both recorded the S1 fix as *"filed to @buildkit"*. **It never was.** No coop document mentioned
`readiness.py` or `board_phase_seconds` until 2026-09-03T19:54Z. **Finding a fix is not filing it,
and a decision record asserting an artefact is not evidence the artefact exists.**

## 6 · 🔴 REBOOT — RE-MEASURED, AND ONE LANE IS ALREADY DOWN

```
live claude sessions: 14   (pgrep -u $(id -u) -x claude | wc -l)
declared lanes:       15   (~/.config/bk-onrestart/config.json, schema 2, one-window)
MISSING:              mstack   (/mnt/biwin/D_DRIVE/BSTDEV/tools/MSTACK — repo present, no session)
```

⚠ **`mstack` died BEFORE any reboot.** Any post-reboot "15/15" check is therefore measuring a
recovery, not a steady state — and if you verify against a remembered 15 you will read a reboot
that *fixed* mstack as a reboot that changed nothing.

**Both boot paths have moved since the last revision — re-measure, do not inherit:**

| path | state now |
|---|---|
| `bk-onrestart.service` (systemd user) | `enabled`, `active (exited)` since **05:29 today**, with new drop-ins `10-path.conf` / `20-install.conf` / `30-harden.conf` — the PATH hazard recorded earlier **may now be fixed**, but that is **UNVERIFIED at boot** |
| `Linger` | `yes` |
| autostart `.desktop` | present, rewritten **05:16 today** |
| launcher `bk-onrestart.sh` | rewritten **04:49 today** (35KB, was 19KB) |

🔴 **A DRY RUN IS NOT BOOT VALIDATION** (codex P2-5). With all lanes up the launcher takes its
`nothing to do` branch immediately: it exercises **no** terminal startup, **no** `claude` lookup on
the boot PATH, **no** launch behaviour. Its `EXIT 0` is a **FALSE GREEN**.

**The only real evidence remains the 2026-09-02T17:17 boot**, where the systemd unit fired 12s after
boot with no graphical session and **FAILED** (`0/15`, `status=1/FAILURE`), and the desktop autostart
brought **15/15** back 16 minutes later — **via LOGIN**.

### Reboot verdict

✅ **SAFE TO REBOOT — provided you LOG BACK IN.** Resume args are
`claude --continue --autocompact 1000000` (**never summarising**); a guard prevents double-launch.
❌ **If the host reboots to a login screen and nobody logs in, NOTHING resumes.**
⚠ Today's systemd/launcher rewrites are **untested at boot** — this reboot is also their first real test.

**The 15 lanes:** ospark · tefl · ulpanit (`lang/hatzinor`) · olamnit · buildkit · qhstate ·
crucible · **glpnet** · lejepa · **mstack (DOWN)** · yngraw (`research/yngenios`) · yngwin · ynglin ·
yngapp · yngcor.

## 7 · WHAT'S NEXT — in this marathon, and beyond

**In the run** (`next:` currently points at S3, which Q-33 **parked** — do not re-derive it):
1. **Unblock §3** — one `/permissions` grant, then merge #279 and cut the release Q-34 authorises.
2. **Open the next era: P3 completion** (Q-32) — coop-agree manifest scope with `@olamnit` first.

**Beyond** — roadmap round 66, **27 features not closed** (18 `promoted` · 5 `specified` ·
2 `implemented` · 2 `analyzed`); full table in the sitrep. Derived build order starts:
`verification-receipts…` → `bk-onrestart-per-host…` → `glptutorial-corpus-goldens…` →
`occurs-checked-substitution…` → `madglp-writer-reader…`.

⚠ **Open defect, unfixed:** `reconcile` reports **73/122 features carry no `spec_path` and can never
bind by basename**; 18 of the 27 not-closed features are among them.

---

*Written by shiras/glpnet for its own successor session. Resume with: `resume marathon`.*
