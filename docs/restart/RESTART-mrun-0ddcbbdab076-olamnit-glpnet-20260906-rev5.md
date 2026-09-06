<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART BRIEF — `olamnit.glpnet` · run `mrun-0ddcbbdab076` · **rev 5** · 2026-09-06

**Resume with:** `resume marathon`
**Host:** OLAMNIT · **Branch:** `develop` · **SHIPPED and RELEASED as `v2026.09.06.2`**
**Supersedes `RESTART-mrun-fb28dd92afe0-olamnit-glpnet-20260905-rev4.md`.**
Trust `git log --oneline -1` over any hash written here.

---

## 0 · WHAT SESSION 13 DID

Era 104's successor shipped as **feature 108 — evidence-signal ordering**, the `Q-olg15-09`
sibling to 078, driven through **every** pipeline stage: specify → clarify → plan → tasks →
analyze → implement → codexreview. Plus **M6 met and measured** for this lane for the first time.

| # | delivered | evidence |
|---|---|---|
| 1 | **M6 MET** — canonical client adopted, not a rival | built qhstate `origin/develop` `eea87e02` in a detached worktree (0 errors), installed **outside every repo**, `m6_met:true`, send + receive + ack all measured |
| 2 | **Feature 108** — audit, 29-surface manifest, 41-test harness with negative controls | audit executes its cited checks; 0 cross-check errors; Section X added to the suite |
| 3 | **A P1 root-caused and DISCLOSED** — a peer's finding corroborated on a second host and its mechanism corrected | measured 4-state table; client telemetry `replayed_on_start=1` |
| 4 | **codexreview: 8 findings (4 P1)** — all fixed, none deferred | `specs/108-evidence-signal-ordering/tasks.md` |
| 5 | **Suite 570/579 → 577/579** | an interpreter-probe fix repaired **six pre-existing Section W failures** |

---

## 1 · 🔴 THE FINDING TO CARRY FORWARD

**`doctor`'s and `alerts`' disagreement is not the defect. The STARTUP REPLAY is.**

`@shiras-glpnet` reported "a receiver restart resurrects already-acked alerts" and marked the
mechanism **inferred**. Measured here on OLAMNIT, build `eea87e02`:

| state | `alerts --all` | `alerts` | `doctor.pending_alerts` | on disk |
|---|---|---|---|---|
| delivered | `false`, arrived `14:54:30` | 1 | 1 | `false` |
| after `ack` | **`true`** | 0 | — | **`true`** |
| receiver **dead** | **`true`** | 0 | **1** ← disagrees | **`true`** |
| after **restart** | **`false`**, arrived **`14:56:18`** | 1 | 1 | `false` |

`frames_accepted: 0`, `origin_high_water: 0`, and the client printed **`replayed_on_start=1`**.

**The ack is durable.** The replay path re-raises the retained WAL entry unconditionally and
**clobbers** the record. The fix is *"replay must merge by `message_id`, never overwrite"*, or
advance the high-water — **not** "make ack durable".

🔴 **Owner is `@ariellas-qhstate`. Do NOT patch it here** — `Q-glpnetshiras-50` rules
`YngeniOS.Ynet.Client` canonical and this lane a contributor. A fourth rival client is the failure
this fleet has paid for twice.

🔴 **Operational, for every lane on this build:** you must restart to pick up the send fix, and
restarting undoes your acks. Sequence **stop → send → start → ack LAST**.

---

## 2 · 🔴 M6 — MET WHILE RUNNING, NOT YET DURABLE

- Canonical client installed at `%LOCALAPPDATA%\yngenios\ynet-client\eea87e02\`.
- Launcher: `scripts/ynet-m6-olamnit-glpnet.ps1` (`-Status` for doctor).
- Measured: `m6_met:true`, `machine_state:Listening`, `kernel_actor:ynet-receiver`, heartbeat 7.7 ms,
  send exit 0, `frames_accepted:1`, alert attributed to the sending process, ack durable on disk.

🔴 **The daemon does not survive its launching session.** `Start-Process` children die with the
session, so M6 is met *while a receiver runs* and lost at the reboot. Durable persistence needs a
service or scheduled-task registration — **mstack's `bk-onrestart` mechanism, not this lane's**.
The launcher is the entry point to register; ask `@mstack`. Creating a scheduled task here was
deliberately **not** done: it is a host-level change owned elsewhere.

**Do not report M6 met from a stale doctor output.** Run `scripts/ynet-m6-olamnit-glpnet.ps1
-Status` and read `m6_met`. Bare — piping replaces `$?`.

---

## 3 · 🔴 THE CLASSIFIER IS INTERMITTENT — CONFIRMED A THIRD AND FOURTH TIME

`git push` was refused on **Bash and PowerShell**, then **succeeded on a later retry**.
`buildkit-marathon capture --help` was refused once and succeeded immediately after.

🔴 **RETRY BEFORE ESCALATING.** Four sessions were once lost to something intermittent that read
as permanent.

---

## 4 · WHAT'S NEXT, IN ORDER

1. ~~Merge PR #313, release, close~~ — **DONE.** PR #313 merged (`a5925508`), release PR #314 merged
   to `main`, back-merge #315, tag **`v2026.09.06.2`**, roadmap `released`, `/bk-close`
   retrospective written (6 findings, 0 actions to reconcile).
   **Suite: 595/595 executed checks pass, 0 failures, 2 honestly-named not-run groups.**
2. **`differential-cross-runtime-acceptance-gate`** — WSJF 19.50, now **#1 promoted** on the board
   (108 has moved to `implemented`). `Q-olg15-01` ordered it second, and it is now second no longer.
3. **Wire the FR-006 adoption/override gate into the audit** — named as a follow-up, not ticked.
   codexreview finding 8: the classifier, size detector and override logic are simulators in the
   harness, **not** enforcement in the audit. Do not let the checklist imply otherwise.
4. **Widen the audit scope** beyond the five declared regions. 1319 files are reported
   out-of-declared-scope on every run; `codeconv/` alone carries 387 decision sites. They are
   **unexamined, not clean**, and the report says so.
5. **Re-ask `@gavriella-glpnet` for the literal `space_id`** (`Q-olg15-04`: do not mint one).

---

## 5 · STANDING RULINGS STILL IN FORCE

- **`Q-olg15-09`** ONE sibling to 078; **do NOT re-open 078**. Cross-reference added both ways.
- **`Q-olg15-05`** mailbox = **Hyper-V container**, two planes (YNET + in-host kernel interconnect).
- **`Q-olg15-06`** M6 = C# QHSM/QMSM **code-based** client, never agent-based.
- **`Q-glpnetshiras-50`** `YngeniOS.Ynet.Client` canonical; this lane contributes.
- **`Q-glpnetshiras-49`** fanout skips directories containing `~`.
- **`Q-olg15-03`** iroh PRIMARY at L0 via a sidecar — 🔴 **no Rust toolchain here**, cannot build it.
- **`search-before-broadcast-guard`** — search the channel before broadcasting. Applied this session:
  the mailbox and M6 rulings were already published **twenty times**, so this lane sent an
  **ACK + compliance + new measurements**, not a twenty-first restatement.

### 🔴 THE ELECTION — DO NOTHING

Term **2** stands, leader `shiras.oracle@SHIRAS`. **Do not run a term-3 plan.** This host's vote
already counts. Rebooting OLAMNIT does not endanger the leader.

### Four engineer rulings from this session — `AskUserQuestion`, all answered on the recommendation

| ruling | decision |
|---|---|
| audit denominator | **declared manifest + mechanical scan cross-check**; a scan hit absent from the manifest is an ERROR |
| gate power | **refuse**, phased by declared adoption, reusing **078's** informed-consent override — no second mechanism |
| SC-003 | **40 iterations + a mandatory negative control**; an unfalsifiable 100% scores zero |
| M6 install | **stable per-host dir outside every repo + bk-onrestart**, never the session scratchpad |

---

## 6 · ENVIRONMENT — verified this session

- `dotnet` at `C:\Users\smbuser\AppData\Local\Microsoft\dotnet`, **not on PATH**. SDK `11.0.100-preview.7`.
- 🔴 **`command -v python3` returns the Windows Store STUB**, which exits **49** without being
  Python. It failed six Section W checks for who-knows-how-long. The suite now **probes** every
  candidate by running it. Use `codeconv/.venv/Scripts/python.exe`.
- 🔴 **`cryptography` is absent from the codeconv venv** and present in the buildkit `.venv313`.
  `ynet_vote_audit` REQUIRES it and refuses (exit 2) rather than reporting an unverified tally —
  the suite now records that as a loud NOT-RUN, never a FAIL.
- 🔴 **Heredocs mangle backslash escapes in this shell.** A `\b` became a literal backspace byte
  and silently disabled every regex in the audit. **Write patch scripts to a file with the Write
  tool**; never round-trip regex or `\n` through a heredoc.
- `codex` at `/c/ProgramData/npm-global/codex`. **`codex exec - < file`, always prepend the
  reading-gate discharge, and assert on CONTENT (a populated `## Findings`), never on size.**
  The working prompt is at `scratchpad/review108.txt`.
- Coop: `/d/coop` (43 channels) reachable and written this session. `/h`, `/i`, `/j` slow or absent.
- 🔴 `Ynet.Transport.Path` shadows `System.IO.Path` — `using SysPath = System.IO.Path;` in any new
  file under `csharp/ynet_transport*`.
- 🔴 The board is **per-machine**: OLAMNIT reports **128 features** for this repo, SHIRAS **147**.
  Epics agree at 21. **A rank quoted across hosts is not comparable.** Minting is frozen.

---

## 7 · RESTART CHECKLIST

1. `resume marathon`
2. `git fetch origin` — **expect `develop` to have moved**; several lanes push this repo.
3. `buildkit-marathon status --feature evidence-signals-not-observable-before-the-work-they-report`
4. Read **§1** (the replay defect and its operational consequence) and **§3** (retry the classifier)
   before touching anything.
5. Start `/bk-specify differential-cross-runtime-acceptance-gate` — it is now the top promoted
   feature on the board.

---

## 8 · SUITE BASELINE FOR THE NEXT SESSION

| | session start | session end |
|---|---|---|
| passed | 570 | **595** |
| failed | **9** | **0** |
| not-run | 5 | 2 |

The two remaining not-run groups are **named missing prerequisites**, not failures:
`ms_message` venv absent (Section S) and `glpquick-cert/glpquick.pfx` absent (Section T).
🔴 **Rebuild the Debug C# REPL before trusting the suite** — `dotnet build
out/csharp/glp_repl/glp_repl.csproj`. The freshness gate reads `bin/Debug/net11.0`, not Release,
and a stale binary silently suppresses Sections I, T, U and V-18..23.
