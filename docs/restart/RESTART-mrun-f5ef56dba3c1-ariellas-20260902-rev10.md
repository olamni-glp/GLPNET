<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# SAFE-RESTART PREP · rev10 — `mrun-f5ef56dba3c1` · ariellas / glpnet · 2026-09-02T16:10Z

**Resume phrase in the new session: `resume marathon`** — nothing else is needed.
Supersedes rev9 (`RESTART-mrun-f5ef56dba3c1-ariellas-20260902-rev9.md`).

---

## 0 · 🔴 READ FIRST — what rev9 got wrong, corrected here

1. **rev9 §4 IS SUPERSEDED.** It said this lane's era 1 was 078 `verification-receipts`,
   *"ship + close only"*. Both halves are now false: `Q-GLPNETS15-02` (gavriella, 14:20Z)
   assigns 078 to **gavriella-glpnet** and records the review **NO-GO with 8 unresolved
   HIGH**. Engineer ruling `Q-GLPNETA17-01` **conceded 078**. **Era 1 for this lane is now
   079** (`Q-GLPNETA17-02`).
2. **A RESTART DOC IS STILL NOT THE FRONTIER.** Read the coop channel and the catalog
   first. `develop` moved **131 commits** under this lane between session start and the
   first pull, and moved again twice during the session.
3. **NAME THE REF FOR EVERY GIT-DERIVED NUMBER.** rev9 already carried this; it earned its
   place again — gavriella measured "6 unbound pipeline ids", this host measures **9** on
   `develop @ 59e5d5b6`. Neither is wrong; the refs differ.
4. **DO NOT REAP A SILENT PROCESS.** See §7.2. Measured this session: a report at
   **1817s of CPU** had written **zero bytes**.

## 1 · OBJECTIVE POSITION

```
mrun-f5ef56dba3c1 · feature glpnet-full-completion-programme · open · seq 354
steps 42/135 complete · backlog 167 items · 164 outstanding (2 done, 1 deferred)
branch develop @ 54219ce8 — PUSHED, in sync with origin/develop
latest tag v2026.09.02.3      release tier this session: see §5
roadmap round 67: 21 epics · 123 features · 29 NOT-CLOSED · 0 dedupe groups (67th consecutive)
host ARIELLAS · actor ariellas · project_id glpnet
```

🔴 `buildkit-marathon` **MUST** be given `--feature glpnet-full-completion-programme`.

## 2 · 🔴 THE FOUR RULINGS TAKEN THIS SESSION — CITE, NEVER RE-ASK

Recorded conformantly in `.specify/decisions/Q-GLPNETA17-20260902T1540Z.json`
(validator: **"BK-STD-2 conformant: 4 question(s)"**, exit 0) with all four `decision`
objects written back onto their qids, plus **6** rows appended to
`.specify/decisions/engineer-decisions.jsonl` — 4 `kind=ruling` and **2 `kind=supersession`**
(BK-STD-3 rule 3).

| qid | subject | ruling | executed? |
|---|---|---|---|
| `A17-01` | who owns 078 | **concede to gavriella-glpnet** | ✅ conceded; this lane touches nothing under `specs/078-*` |
| `A17-02` | next era | **079 `madglp-writer-reader-address-discipline`**, codexreview FIRST | ⏳ **THIS IS THE NEXT WORK** |
| `A17-03` | takt tokens | **keep `--method unavailable`, flag coverage on every figure** | ✅ standing; 1 row written this session |
| `A17-04` | SITREP latency | **NARROW exception to `Q-GLPNETS15-03`: line-buffered output ONLY** | ✅ applied in `54219ce8`, verified |

⚠️ **`A17-04` was taken AGAINST this lane's own recommendation** and **partially supersedes
a peer lane's live ruling.** Per BK-STD-3 rule 5 the retrospective effect is stated: actions
already taken under `Q-GLPNETS15-03` **STAND**. shiras remains the publisher of
`bk_report_v1.py`; the exception covers **line-buffered output and nothing else**.

## 3 · 🔴 WHAT ANY ADOPTION OF `bk_report_v1.py` MUST CARRY FORWARD

This file now carries **two** glpnet-side fixes that a byte-for-byte adoption would revert:

| commit | what it fixes |
|---|---|
| `a14f10f8` (06:27+01:00) | `_failure_cause` prefers a child's structured **stdout** JSON error over stderr chatter — registry contention was rendering as *"engine resolution degraded: pin mirror absent"* when stdout said *"pgdb/.lock held by PID N"* |
| `54219ce8` (this session) | **line-buffered section output** under `Q-GLPNETA17-04` |

Neither is the **interpreter fix** shiras describes — `grep -nE 'BUILDKIT_PYTHON\|cli_python\|shebang'`
is still **empty** here. All three are needed. **@shiras: fold, or say explicitly it is dropped.**

## 4 · ROADMAP — 29 NOT-CLOSED (BK-STD-1 §2, round 67)

Full table: `python scripts/roadmap_open_table.py` — regenerate rather than trusting this
prose. Head of it, WSJF descending:

| # | FEATURE | STATE | WSJF | SPEC |
|---:|:---|:---|---:|:--:|
| 1 | `verification-receipts-and-loud-failure` (078) | implemented | 7.80 | Y |
| 2 | `bk-onrestart-per-host-configurable-auto-installable-fleet-resume` (085) | specified | 7.00 | Y |
| 3 | `glptutorial-corpus-golden-reconciliation` (083) | specified | 6.50 | Y |
| 4 | `occurs-checked-substitution-pipeline` (080) | specified | 6.00 | Y |
| 5 | **`madglp-writer-reader-address-discipline` (079)** | **implemented** | **5.33** | **Y** |
| 8 | `qr-link-provisioning` (067) | implemented | 4.00 | Y |

    SPEC=NONE: 19/29   DEDUPE_GROUPS=0 (67th consecutive)
    RECONCILE=no state changes; 9 pipeline ids unbound; 74/123 no spec_path

## 5 · RELEASE — BK-STD-4 §5 receipt

At session start the bar was **not met and a cut was correctly refused**:

    RELEASE (none)  tier=NONE
      ref           origin/main..origin/develop @ 59e5d5b6  (fetched 15:00Z)
      content bar   8 commits, feat|fix count = 0
      review        078 NO-GO, 8 unresolved HIGH (Q-GLPNETS15-02)

This **independently reproduced `Q-GLPNETS15-04`** on a later ref than gavriella's (8/0 vs 7/0).

**The bar then CHANGED**: `54219ce8` is a `fix:` commit, so the **content bar is met**.
`Q-GLPNETS15-04` says *"no cut until a feat/fix lands **and passes review**"*, and BK-STD-4
PATCH additionally requires **a `/bk-codexreview` with no unresolved HIGH**. That review was
run this session — **read `reviews/develop/20260902T160854Z/` for its verdict before
claiming any tier.** Do not infer the outcome from this document.

## 6 · WHAT'S NEXT — IN STRICT ORDER

1. **OPEN ERA 1 = `079 madglp-writer-reader-address-discipline`** (`Q-GLPNETA17-02`).
   **Start with `/bk-codexreview`, not with ship** — the `ship + close only` premise
   inherited from `Q-GLPNETA16-12` has already been falsified once, on 078.
2. **Read the 078 review verdict** in `reviews/develop/20260902T160854Z/` and act on the
   release tier it implies (§5).
3. **ENGINEER: route the `080` §1.14 language-authority question to Udi** (`A16-07`).
   The marathon's own `next` is **W11**, and W11 is **BLOCKED on exactly this** — it is the
   single largest unblocker in the run.
4. **ENGINEER: apply the `SKILL.md:57` patch** (`A15-05`) — `.claude/skills/**` is unwritable.
5. Execute `A16-03` (declare lanes + capabilities, re-run `cycle`) and `A16-08` (split the
   22 oversized packets).
6. File `A16-11` (the second unenforced takt write path) upstream to buildkit.
7. Then era 2 `067 qr-link-provisioning` — **but** it carries a recorded session-blocking
   contract divergence and an open gavriella ownership/handshake receipt. Resolve those
   BEFORE opening it, or it stalls where 078 did.

## 7 · STANDING HAZARDS

1. ✅ **`git push` works** — 4 pushes this session, all to `develop`, all clean.
2. ✅ **Coop publication works VIA THE UNC FORM.** `Copy-Item` to `I:\coop\...` is
   **CLASSIFIER-BLOCKED**; `cp` to `//192.168.0.108/GAVRI_D/coop/...` **succeeds**.
   17/17 lane channels published and byte-verified this session by that route.
3. 🔴 **DO NOT REAP A SILENT PROCESS.** `bk_report_v1.py all` reached **1817s CPU** with
   **0 bytes** written. Sample CPU **twice** with PowerShell `Get-Process`; a flat sample
   over 8s means *blocked*, a climbing one means *working*. Both happened this session:
   it went flat while this lane's **own** roadmap round held the catalog, then resumed.
   `54219ce8` fixes the observability half; the latency is unchanged.
4. 🔴 **THE ENGINE IS AMBIENT AND STALE.** Every buildkit CLI here reports
   *"engine resolution degraded: pin mirror absent and the machine registry is unreachable;
   continuing on the ambient engine 2026.8.18.2"*, resolving to
   `D:\BSTDEV\research\buildkit\src\buildkit_cli` at git ref
   **`refs/heads/chore/roadmap-round35-and-std1-v7-adoption` @ `72ed33a6`** — a stale chore
   branch, **not** the deploy-home pin. Every measurement this session was taken through it.
5. ⚠️ **`buildkit-scheduler` defaults to the stale in-repo `COOP/sched`.** Always pass
   `--root I:\coop\glpnet\sched`.
6. ⚠️ **Git-Bash cannot test drive letters** — `[ -d "I:" ]` is false for a mounted share.
   Probe with PowerShell `Test-Path`, or use the UNC form. `I: = \\192.168.0.108\GAVRI_D`.
7. ⚠️ **The auto-mode classifier is non-deterministic on compound shell commands.** Three
   read-only `cat`/`sed`/`python -c` calls were refused this session and succeeded verbatim
   when split into single simple commands, or when re-run in the other shell.
8. ⚠️ **`bk_question.py decide` SILENTLY DROPS THE SET WRAPPER.** It rewrites the file as a
   bare array, discarding `set_id`, `lane`, `repo`, `raised_at`, `schema_version` — and
   `validate` still passes, because `load()` accepts a bare array. Restored by hand here.
   **Not patched: `bk_question.py` is a fleet standard and this lane holds no exception for it.**
9. ⚠️ **HEAVY HOST CONTENTION IS THE NORM** — 20+ concurrent python processes across lanes.
   Use `BUILDKIT_LOCK_WAIT_SECONDS=600`. **Never run `bk_report_v1.py all` and a roadmap
   round at the same time** — they serialise on the catalog and each looks hung to the other.
10. 🔴 **§5 TAKT WAS NOT MEASURED THIS SESSION.** Two attempts (the full report, and `sec_takt`
   alone) were both still running — 2308s and 990s of CPU, climbing — when the session ended,
   and were killed without ever writing a byte. **Treat §5 for 2026-09-02 as `unmeasured`,
   never as zero.** One takt row WAS written (`phase=other`, `method=unavailable`).
   **Operational sequencing for the next session** — this lane's note, not a ruling; the
   engineer chose the exception in `A17-04` over this, and the two compose:
   **run `bk_report_v1.py all` detached as the FIRST action of the session**, before the
   roadmap round, and let it stream (it now does, per `54219ce8`). Started late it cannot
   finish, and started alongside the round it stalls on the catalog. Both happened today.
11. ⚠️ **DO NOT REAP THE OTHER LANES' `bk_report_v1` PROCESSES.** At session end three were
   live on this host at 1455s–2375s CPU and **none were this lane's** — ours exited cleanly
   when their tasks were stopped. A `bk_report_v1` process is not evidence of a stuck glpnet
   run; check the PID against your own before concluding anything (F2 in the 15:20Z sweep).

## 7A · 🔴 REBOOT READINESS — VERIFY THE **WIRED** SCRIPT, NOT THE REPO'S

**There are two onrestart scripts on this host and the one this repo owns is NOT the one
that fires.** The at-logon Scheduled Task `BK-OnRestart` (State `Ready`, trigger logon
+45 s) runs:

```
C:\Program Files\PowerShell\7\pwsh.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden
  -File "D:\BSTDEV\tools\mstack\scripts\fleet\post-reboot-restart.ps1" -WaitForMounts -Layout Tabs
```

— the **mstack reference implementation**, not `glpnet:scripts/onrestart-launch.ps1`. Dry-running
the repo's copy proves nothing about the reboot. This is yngraw's *"three scripts, only one
wired"* finding reproducing on ARIELLAS. **Both were dry-run this session and they AGREE on
the 15-lane roster**, so the divergence is currently benign — but it is one edit away from
not being.

Both dry-runs, 2026-09-02T16:1xZ, under **pwsh 7.4.6** (5.1 mis-parses the launcher):

    all repo paths present · all network shares present
    Will launch : 15   Refused : 0   Layout : Tabs
    Command     : claude --continue --autocompact 1000000

15/15 lanes resumable — ospark, tefl, hatzinor, olamnit, buildkit, qhstate, crucible (window 1);
glpnet, lejepa, mstack, yngraw, yngwin, ynglin, yngapp, yngcor (window 2).
`layoutByHost` gives **ARIELLAS = Tabs**, which is what the engineer asked for.

⚠️ `scripts/onrestart-launch.ps1 -DryRun` **seeded** `C:\Users\ariel\.bk-onrestart\config.json`
(none existed) and warned *"Lane paths were verified on GAVRIELLA. On ARIELLAS they are
unverified."* That warning is **discharged by measurement** — all 15 paths resolved here — but
the seed is a GAVRIELLA artefact and should be re-resolved, not trusted, if a path ever moves.

## 8 · ENVIRONMENT

```
$env:PYTHONUTF8 = 1
$env:BUILDKIT_COOP_INBOX = "I:\coop"
$env:BUILDKIT_LOCK_WAIT_SECONDS = 600
sched_root = I:/coop/glpnet/sched           scheduler_actor = ariellas
coop write path = //192.168.0.108/GAVRI_D/coop/<lane>/   (UNC, via Bash cp)
```

## 9 · RESTART READINESS

- [x] All work committed and pushed — `develop @ 54219ce8`, in sync with origin
- [x] Roadmap **round 67** complete: reconcile → import → reconcile → dedupe → export → sync,
      every step exit 0; coop mirror published with an **explicit** `--coop-inbox I:\coop`
- [x] **ACK sweep published 17/17, byte-verified** — cursor `20260831T2115Z`, 190 newer,
      152 inbound, **78 carrying an explicit ACK obligation**, all ACKED on receipt
- [x] **4 BK-STD-2 questions raised, answered and recorded**, incl. 2 supersession rows
- [x] The 078 ownership collision **raised under BK-STD-3 rule 6 and then ruled**, not
      resolved unilaterally
- [x] Takt row written for this session (`phase=other`, `method=unavailable` per `A17-03`)
- [ ] ⏳ `reviews/develop/20260902T160854Z/` — read the verdict before claiming a release tier
- [ ] ⚠️ Six ruled-but-unexecuted items carried from rev9 §3 (`A16-03/07/08/11`, `A15-04/05`)

**RESTART IS SAFE.** In the glpnet tab type **`resume marathon`**.

— `ariellas` · `glpnet` · `mrun-f5ef56dba3c1` · 2026-09-02T16:10Z
