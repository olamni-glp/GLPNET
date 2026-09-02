<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# RESTART PREP — shiras / glpnet · run `mrun-f77f62158255`

    written:  2026-09-02T09:20Z
    host:     SHIRAS (Linux)   repo: olamni-glp/GLPNET
    branch:   100-cpm-central-package-management   commit 9feb6840
    run:      mrun-f77f62158255 [open]  seq 110  feature=glpnet-shiras-tidyup-and-scheduler-rootcause
    resume:   type exactly  →  resume marathon
    status:   ✅ SAFE TO RESTART.   ✅ SAFE TO REBOOT (gate exits 0 — §5).

> **This file is a POINTER, not a work ledger.** The roadmap and the buildkit pipeline state
> are the source of truth. Re-locate objectively with the Restart-Resume order in CLAUDE.md:
> `buildkit-roadmap next` → in-progress? → pipeline/WIP position. Never resume from a summary.

---

## 1 · What "resume marathon" must do first

```bash
buildkit-marathon status          # run mrun-f77f62158255, seq, outstanding items
buildkit-marathon backlog         # 65 items
/home/shira/.local/share/bkvenv/bin/python .specify/standards/bk_report_v1.py all \
    --feature glpnet-shiras-tidyup-and-scheduler-rootcause
```

🔴 **Run BK-REPORT with the bkvenv python, NOT `python3`.** The generator shells out with
`sys.executable`; under `/usr/bin/python3` every section returns
`ModuleNotFoundError: No module named 'buildkit_cli'` and the report is six UNAVAILABLE blocks.

🔴 **The report order is FIXED and MANDATORY:** ROADMAP → PROGRESS → STATUS → SITREP → TAKT →
NEXT. It is not configurable and must not be re-ordered or hand-written. Takt is READ FROM THE
TAKT DUCKLAKE; per-phase token use is RECORDED there (`buildkit-scheduler takt-tokens`) and
RETRIEVED from there — never recomputed in-process.

## 2 · Next action, derived from recorded state

| source | next |
|---|---|
| `buildkit-marathon status` | sequence or resolve **S1 — scheduler dispatch leaks at all three transitions** (midi) |
| `buildkit-roadmap next` | **front-end-goal-term-acceptance-completeness-parser-repl-goal-builders-cross-runtime** (rank 21) |
| engineer ruling **Q-19** | **hand-instrument the next era**, stamping every stage, to find why six era stages beyond the Q-08 defect never persist |

**Standing engineer directive on era shape:** all future eras are **SINGLE-FEATURE eras**, to burn
the roadmap backlog down one feature at a time. After each successful `/bk-ship` + `/bk-close`,
**tidy the repo of leftover branches and worktrees before starting the next era.**

## 3 · Open blocks carried forward — DECIDED, not open questions

Cite these; do **not** re-ask them. All in `.specify/decisions/`.

| qid | ruling | what this lane must do |
|---|---|---|
| Q-09 | occurs-check = **UnifyFail** | spec 080 unblocked; FR-002 unconditional; `/bk-plan` may run |
| Q-10 | widen permission rule to **merges** | not yet in settings — merges still blocked here |
| Q-11 | fix **envelope supply** before any CPM/PERT reallocation | board still reports NOTHING DISPATCHABLE |
| Q-12 | close T8, land 083, leave 059 | T8 done; 083/059 still unmerged heads |
| Q-13 | **ariellas publishes its 3 signing keys** | 19 coop docs still refused until then |
| Q-14 | portable lake root **by configuration** | ✅ DONE — lake reachable, 738 records for shiras |
| Q-15 | **single-feature eras** | governs every future era |
| Q-16 | extend the takt lake layout; **contract, not a new schema** | superseded by the converged fleet CPM CRDT v0.4 |
| Q-17 | lamport step 2 **re-addressed to the buildkit lane** | this install has **no `lclock` code at all** |
| Q-18 | registry lock wait to become **configurable** | buildkit-lane change |
| Q-19 | **hand-instrument the next era** | ⬅ **the next real action** |
| Q-20 | untrack the refusal ledger | ✅ DONE — `git rm --cached`, annotated in `.gitignore` |

## 4 · Live blockers a resuming session will hit

1. **Merging is blocked** by the Claude Code permission classifier (`gh pr merge` and `git merge`).
   Ruling Q-10 says widen the rule; it is **not yet applied**. Until then this lane's contract ends
   at a green PR and a peer merges. PRs #259, #264, #267, #274 all landed this way. **#279 is open.**
2. **Registry contention is sustained.** `_LOCK_WAIT_SECONDS = 30.0` is hard-coded with no override
   (ruling Q-18). 20 full-report attempts over ~10 min never found a clear window. **Sections
   succeed individually** — run `roadmap`, `progress`, `status`, `sitrep`, `takt`, `next` separately
   if `all` degrades.
3. **Era takt is unmeasurable.** glpnet: 38 eras across 4 hosts, **zero measurable**. Fleet: 13 of
   843 (1.5%). Token coverage 9%. Ruling Q-19 is the diagnostic path.
4. **`.import-refused.json` is now untracked** — do not re-add it (ruling Q-20).

## 5 · REBOOT READINESS — measured 2026-09-02T09:20Z

```
bash ~/.local/share/buildkit/deploy-home/onrestart/bk-onrestart.sh launch --dry-run
  → all 15 lane(s) already up - nothing to do.        EXIT 0
```

| check | result |
|---|---|
| lanes registered | **15 / 15**, all names DISTINCT, every path resolves |
| session store per lane | **15 / 15 present**, all touched 2026-09-02 (guard 1 satisfied — `claude --continue` will resume, not start empty) |
| `bk-onrestart.service` | `enabled`, `[Install] WantedBy=default.target` present |
| `bk-onrestart.timer` | `enabled`, `WantedBy=timers.target` |
| autostart `.desktop` | `X-GNOME-Autostart-enabled=true` |
| launch delay | 45 s (`ExecStartPre=/bin/sleep 45`) |
| mount guard | waits for `/mnt/biwin/D_DRIVE` |
| resume args | `claude --continue --autocompact 1000000` — **never summarising** |
| takt lake env | launcher exports `BUILDKIT_TAKT_LAKE` + `BUILDKIT_TAKT_LAKE_FLEET` (the Q-14 fix propagated) |
| duplicate guard | guard 6 — will not double-launch a lane already running |

**The 15 lanes, in launch order:** ospark · tefl · ulpanit (`lang/hatzinor`) · olamnit · buildkit ·
qhstate · crucible · glpnet · lejepa · mstack · yngraw (`research/yngenios`) · yngwin · ynglin ·
yngapp · yngcor.

⚠ **`Linger=no`.** The user session is not lingering, so resume is tied to **login**, not to boot.
A reboot that stops at a login screen relaunches nothing until someone logs in. That is the one
residual reboot risk and it is a property of the host, not of the config.

## 6 · Repo state at write time

```
branch  100-cpm-central-package-management @ 9feb6840   (pushed)
PRs     #259 #264 #267 #274 MERGED   ·   #279 OPEN (rulings + round 63 + untrack)
develop 31/31 net11.0 · Directory.Packages.props present · CPM adopted
tags    v2026.09.02.1 latest
tree    clean except regenerable artefacts
```

**Do NOT** re-run `/bk-roadmap` round 63 — it is complete (export
`shiras__glpnet__20260902T072858Z.json`, 21 epics / 123 features / 3902 journal lines). Only
`sync --round 63` publish is outstanding, and it is retried opportunistically.

---

*Written by shiras/glpnet for its own successor session. Resume with: `resume marathon`.*
