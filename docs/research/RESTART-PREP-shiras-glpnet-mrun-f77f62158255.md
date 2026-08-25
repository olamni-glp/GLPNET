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
| **PR** | ✅ **#230 OPEN** — https://github.com/olamni-glp/GLPNET/pull/230 |
| marathon | `seq=10`, **0/0 steps**, **10 outstanding items**, all `parked` |
| develop vs main | **94 ahead**, 0 open PRs |
| release | **HELD by engineer ruling** — dry-run green (`v2026.08.24.1`), deliberately not cut |
| scheduler | shiras onboarded: **18 caps · 105 calendar windows · 1 claimed WP** |
| board fold | 32 WPs — backlog 23 · ready 3 · in-progress 4 · done 1 · escalated 1 |
| roadmap | 🔴 **catalog will not project here (OOM).** Read from signed exports only. |
| roadmap (from export) | **25 not-closed = 1 implemented · 3 analyzed · 6 specified · 15 promoted, across 8 epics** |
| engineer rulings | **7 recorded** in `.specify/decisions/engineer-decisions.jsonl` |

## 🔴 If a `gh` command misbehaves — check the default repo FIRST

This checkout has **two remotes** (`origin` → `GLPNET`, `upstream` → `GLP`), and `gh` had defaulted
to the **sibling** `olamni-glp/GLP`. Every `gh pr create` went to the wrong repo and failed with
`"No commits between develop and <branch>"` — a message that was **literally true, about a repo I
was not in**. Fixed this session via `gh repo set-default olamni-glp/GLPNET`.

```bash
gh repo set-default --view     # FIRST check, always
gh api rate_limit --jq '.resources.core'   # only a second check
```

## 🔴 Corrections carried forward (do not re-derive)

1. **The "classifier blocks commit on 2nd host" hypothesis is RE-OPENED, not withdrawn.** I
   withdrew it mid-session blaming the GitHub rate limit, then **retracted that withdrawal** when
   the identical failure reproduced on a fully restored budget. The real cause of *this* session's
   PR failure was `gh`'s default repo (see above). The classifier report from
   `ACK-20260824T112831Z` stands **unexplained** — and may be the same wrong-default-repo defect.
   Board record: `RETRACTION-20260825T084334Z-shiras-glpnet-...`.
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


## 🔴 SPECIFIED-FEATURE AUDIT — done this session, do not redo

Audited every non-closed feature against artefacts actually on `develop` (roadmap state from the
**signed export**, per ruling `Q-GLPNETSHIRAS2-03`).

| feature | roadmap state | on develop | tasks | true next phase |
|---|---|---|--:|---|
| **078** verification-receipts | analyzed | full set | 4✓ / **62 open** | `/bk-implement` |
| **059** full-scope-gleam | analyzed | full set | 75✓ / **23 open** | `/bk-implement` |
| **066** wave6-consolidation | analyzed | full set | 12✓ / **18 open** | `/bk-implement` |
| **067** qr-link-provisioning | **implemented** | 🔴 **no spec dir** | — | **contradiction** |
| **083** glptutorial-goldens | specified | spec only | 0 | `/bk-plan` |
| **080** occurs-checked-subst | specified | spec only | 0 | `/bk-plan` |
| **079** madglp-writer-reader | **specified** | 🔴 **full set** | **20 open** | **stale state** |
| **082** feature-stream-superset | specified | spec only | 0 | `/bk-plan` ← **claimed by shiras** |
| **085** onrestart-fleet-resume | specified | spec only | 0 | `/bk-plan` |
| **065** ynet-consolidation | specified | spec only | 0 | `/bk-plan` |

**Two contradictions, both other lanes' rows — do not touch, they are broadcast and ACK-requested:**

1. **067 is `implemented` with NO spec dir on develop.** Spec/plan/27 tasks exist only on
   `origin/067-qr-link-provisioning` and `origin/067b-qr-link-continuation`. And `implemented` is a
   state the renderer whitelist **drops** — so the row is *invisible in the table AND stranded off
   trunk*. The reporting defect and the merge defect conceal each other.
2. **079 is `specified` but fully tasked** (plan + tasks + 20 open). Any lane picking by state will
   send `/bk-plan` at a feature that already has one. Remedy: `buildkit-roadmap reconcile` on the
   owning host.

## ✅ WORKTREE SURVEY — shiras is CLEAN, do not redo

`git worktree list` → **1** (primary only) · `.git/worktrees/` → **does not exist** · local branches
→ **3** · other GLPNET checkouts → **none**. Other `GLPNET`-named paths are vendored subdirs inside
other repos. **The C:-drive scratchpad check does not apply — shiras is Linux.**
**shiras's tidy-up burden is zero**; the 12 remote heads belong to other lanes' hosts.

## 🔴 The deploy-home registry is HOST-WIDE — and its error names the WRONG repo

**This is the single biggest operational trap on this host.** `~/.local/share/buildkit/deploy-home/registry`
is **one per user, shared by every repo**. shiras runs concurrent Claude sessions across `glpnet`,
`crucible`, `qhstate`, `yngenios` and `LeJEPA` — **they all serialise on it.**

The lock error says *"Another buildkit session is using **this repo's** pgdb/"* — **that clause is
false.** Measured: PID 157933 was a `buildkit-roadmap import` in the **crucible** repo, holding the
host-wide registry while `glpnet` captures failed. Verify the real holder with:

```bash
ps -o pid,etime,cmd -p <PID>;  ls -l /proc/<PID>/cwd
```

🔴 **It cost 11 marathon captures**, which gave up after 30s and were lost. **Before any buildkit
command here: `pgrep -af 'buildkit-'`.** Run them **SERIALLY**, and never kill the holder — it is
another lane's live work.

Board record: `ACK-20260825T090133Z-shiras-FLEET-DEFECT-the-deploy-home-registry-is-HOST-WIDE-…`.


## 🟢 `/bk-codexreview` WORKS HERE — and the fleet blocker is a BASE defect

**codex IS installed and working on shiras** (`codex-cli 0.149.1`, `CODEX_OK`). My earlier
"codex absent" reading was true at ~07:56Z and is now STALE — **re-probe before quoting it.**

🔴 **@gavriella's "codexreview is undischargeable" is retired.** The cause is the base default:

```
preflight --help:  --base BASE   base ref for diff scope (default origin/HEAD->main)

git diff --shortstat main...HEAD           ->  161 files, 1,176,483 insertions  <-- the DEFAULT
git diff --shortstat origin/develop...HEAD ->   15 files,     2,169 insertions  <-- the real work
```

**This is GitFlow**: features cut from `develop`, which is 94 ahead of `main`. The default base makes
every branch inherit the whole unreleased delta. **ALWAYS pass the base:**

```bash
buildkit-codexreview preflight  --base origin/develop
buildkit-codexreview codex-pass --cycle 1 --base origin/develop --review-only
```

Proven run `20260825T115835Z`: brief **1921 prompt bytes**, exit 0, **6 findings**, 420s, no overflow.

🔴 **Engineer ruling `Q-GLPNETSHIRAS-02` (Claude-reviewers-only discharges the gate) rests on a
premise I have since DISPROVED.** Re-raise it — a real cross-provider review is available now.

## 🔴 FIVE DEFECTS IN THE SHIPPED `bkquestion` (found by codex, in the FLEET tool)

`bkquestion.py:151` AttributeError instead of INVALID on `"questions":[null]` · `:79` `bool` passes
the `number` check (renders `True h`) · `:198` six-word labels accepted vs 1-5 · **`:413` `--answer
Q-ID=` appends an empty decision row reported as recorded** · `schema:21` `format: date-time` never
enforced, contradicting the README's central safety claim. **Not patched — they are @olamnit-ospark's.**

## 🔴 HEARTBEAT — carry the NARROWED claim ONLY

An earlier peer claim that `write_heartbeat()` fires on read-only paths was **RETRACTED IN FULL**
after a natural experiment refuted it. **The tested claim:** `onboard` (a WRITE) emits a heartbeat
even when it lands 0 caps and 0 ops; `loop` beats per cycle; **read-only `board`/`status`/`replicas`
do NOT.** So `ops/<actor>/` holding only `heartbeat.json` means **a prior onboard that landed
nothing** — not manufactured-by-looking. Also: `dispatch.py`/`board.py`/`plan.py` carry **zero**
heartbeat references, so the allocator never reads it either way.

## 🔴 The standard renderer reports FALSE-EMPTY here, at exit 0

`python scripts/roadmap_open_table.py` → **`0 not-closed, across 0 epics`**, `--check exit=0`.
That is D1 (catalog OOM) surfacing as a clean pass. **Render from the signed export instead**
(ruling `Q-GLPNETSHIRAS2-03`): `25 not-closed = 1 implemented · 3 analyzed · 6 specified · 15 promoted, 8 epics`.


## 🔴 CROSS-REPO WORK IN FLIGHT — buildkit `feat/host-interconnectivity-hardening`

The engineer ruled the **HOST-INTERCONNECTIVITY-HARDENING** feature must live in **buildkit**.
**It already exists there**: roadmap state **promoted**, WSJF 2.00, RICE 2795.29 — do **not** mint it
again. Two multi-contributor CRDT docs are live at
`.specify/crdt/host-interconnectivity-hardening/{ROOTCAUSES,REQUIREMENTS}.crdt.md`
(six shiras lanes contributing: glpnet, tefl, crucible, qhstate, yngraw, hatzinor, buildkit).

**glpnet's contribution:** `RC-glpnet-01` (unmeasured-gate null), `RC-glpnet-02` (codexreview
mis-based on GitFlow), `RC-glpnet-03` (replication-as-compensation), each with a matching
`FR-glpnet-0n` carrying a MEASURED acceptance test.

🔴 **UNFINISHED — FIRST ACTION IN THAT REPO NEXT SESSION.** Commit `a0b1d14c` (restoring my three
RC blocks after a full-file overwrite dropped them) is **committed locally and NOT PUSHED**. The
remote moved twice and a peer holds an **unstaged** edit to `REQUIREMENTS.crdt.md`; I refused to
rebase or stash over another lane's in-flight work.

```bash
cd /mnt/biwin/D_DRIVE/BSTDEV/research/buildkit
git status                      # wait for the peer's unstaged edit to be committed
git pull --rebase && git push -u origin feat/host-interconnectivity-hardening
grep -c '^## RC-glpnet' .specify/crdt/host-interconnectivity-hardening/ROOTCAUSES.crdt.md   # expect 3
```

## 🔴 bk-onrestart — ALREADY ON THE BUILDKIT ROADMAP, DO NOT MINT A NEW ONE

The engineer asked to codify the 1-or-2-window multi-tab resume launch. **Buildkit's roadmap already
carries it, promoted and scored:**

| feature | state | WSJF | RICE |
|---|---|---:|---:|
| `bk-onrestart-per-host-window-layout-config-1-or-2-window-configurable-lanes-register-unregister-capture` | promoted | **6.67** | 560 |
| `onrestart-window-group-layout-policy` | promoted | 6.00 | 1440 |
| `onrestart-host-agnostic-auto-install` | promoted | — | — |
| `bk-onrestart-per-host-configurable-fleet-resume` | promoted | — | — |

The first row **is** the engineer's request, already scored above every other onrestart row.
`scripts/bk_onrestart_config.py` and `scripts/bk-onrestart.sh` exist on the branch. **A fifth row
would be the duplicate allocation this fleet keeps paying for.**

## What's next, in order

| # | step | size | state | blocked-by |
|--:|:---|:---|:---|:---|
| 1 | Open the blocked PR (above) | nano | **unblocked** | rate limit reset only |
| 2 | S3 — design the three transition writers via `/bk-3rtask` | saga | **gated** | **PEER** — board is ariellas-owned |
| 3 | **`/bk-plan 082`** — the claimed feature-stream superset (spec exists, plan+tasks do not). **This is the durable remedy for the three-transition leak.** | saga | **unblocked** | permission recorded `Q-GLPNETSHIRAS2-01` |
| 4 | S4 — file the roadmap OOM as a buildkit defect | mini | **unblocked** | — |
| 5 | ~~S9 — enumerate features stuck at `specified`~~ | maxi | ✅ **DONE this session** — see audit above |
| 6 | S8 — marathon → `/bk-flow` migration readiness | saga | **unblocked** | engineer marked critical |
| 7 | S5 — tidy-up: 12 remote heads | midi | **gated** | **ENGINEER/PEER** per lane affinity |
| 8 | S6 — release | nano | **HELD** | **PEER** — gavriella has not answered ariellas' 06:55Z ask |

**Engineer directive: the FIRST task of the next session is the `/bk-marathon` → `/bk-flow` migration**
(automatic upgrade + verification that every `/bk-*` tool still builds and works; plan and execute via
`/bk-3rtask`, evaluate safety and idempotency before any cutover). **Then `/bk-plan 082`.**

## Evidence caveats

- **This lane contributes ZERO takt rows.** 0 phases measured, no takt capability installed here.
  Do **not** fold shiras into any fleet takt figure — quoting an absent lane as 0 is the takt-lake
  silent-loss defect in a new costume.
- All roadmap figures here are from a **signed peer export**, not a local fold. Stated so no one
  mistakes provenance.
- Marathon shows **0/0 steps with 10 outstanding items**: the items are `parked` intake, not
  sequenced steps. **Report items, not a completion ratio.**
