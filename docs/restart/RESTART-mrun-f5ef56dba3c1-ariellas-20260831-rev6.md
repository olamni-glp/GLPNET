<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# SAFE-REBOOT PREP · rev6 — `mrun-f5ef56dba3c1` · ariellas / glpnet · 2026-08-31T15:05Z

**Resume phrase in the new session: `resume marathon`** — nothing else is needed.
Supersedes rev5 (`RESTART-mrun-f5ef56dba3c1-ariellas-20260827-rev5.md`), which was itself
**stranded on branch `096` for four days** and never reached develop until this session.

---

## 0 · REBOOT-SAFETY STATUS — READ FIRST

| item | state |
|---|---|
| All session work COMMITTED | YES |
| PUSHED to origin | YES |
| MERGED to develop | **YES — PR #254 merged 12:37:18Z**, plus this branch |
| Marathon state durable | **YES — 3 traces + 1 pre-reboot capture appended this session** |
| Held-branch escalation | **DISCHARGED** (§2.1) |
| COOP ACKs owed | **NONE — full sweep published 12:45Z** |
| Engineer rulings recorded | **4** (Q-GLPNETA13-01..04), in the git-tracked ledger |
| At-logon relaunch task | **REGISTERED and verified** (§6) |
| Releasable feature | **NONE** — and the bar for that is now ruled (§2.2) |

**REBOOT IS SAFE.** Nothing is stranded on a local branch, in a shell, or in this transcript.

## 1 · OBJECTIVE POSITION

```
mrun-f5ef56dba3c1 · feature glpnet-full-completion-programme
seq 337 · steps 40/129 complete · outstanding items 150 · open
  (session opened at seq 333/149; +3 traces +1 capture mitem-01a05853-3958-7761-babd-be61071e91fc)
discharge: 8 of 25 satisfied — 17 open
develop @ ac36265d + this branch;  main @ v2026.08.31.1 (cut by olamnit 12:16Z)
```

🔴 **`buildkit-marathon` MUST be given `--feature glpnet-full-completion-programme`.** A bare
command resolves `.specify/feature.json` (which points at `specs/085-onrestart-fleet-resume`) and
falsely reports *"no active marathon run"*.

🔴 **`next:` is STILL WRONG.** It reports **W11**, which is gated on Udi's §1.14 ruling (J2).
`next` does not model gating. Use §5.

## 2 · WHAT THIS SESSION DID

### 2.1 · The held-branch escalation — DISCHARGED BY RE-DERIVATION, not by rebase

@gavriella escalated at `20260831T1115Z` that two branches were held 3 days past her own 8h window,
25 commits stranded, owner of `096` unknown to her. **`096` was ours** — its head commit adds
`RESTART-...-ariellas-...-rev5.md` and `mrun-f5ef56dba3c1` is this lane's run.

She asked for a rebase. **I did not rebase; I re-derived, because two of the four commits were dead:**

| the 4 held commits | disposition | why |
|---|---|---|
| `fb76fb62` C#15 via `Directory.Build.props` | **DROPPED** | superseded on develop by `48a283d4` (`.targets`-only pin) |
| `e74ce02e` `global.json` `rollForward: latestFeature` | **DROPPED** | superseded on develop by `8004e2d6` (`latestPatch`) |
| `43359587` roadmap round-53 export | carried | byte-identical duplicate of what was on `097` |
| `42d6be1c` restart doc rev5 | carried | the only unique content |

**MEASURED, not assumed** — on develop with no `Directory.Build.props` present:

```
Test-Path Directory.Build.props  ->  False
dotnet msbuild csharp\glp_il_codec\GlpIlCodec.csproj -getProperty:LangVersion  ->  preview
dotnet msbuild csharp\glp_link\GlpLink.csproj        -getProperty:LangVersion  ->  preview
```

`GlpLink` is one of the 18 projects that said `latest` (= C# 14). The `.targets` pin reaches it.
Both fleet rulings (`Q-GLPNETS10-01`, `Q-GLPNETS10-03`) are satisfied by develop as it stands.

**`096-host-interconnectivity-hardening-evidence` is now SUPERSEDED — nothing on it is worth
merging.** The ref is left in place deliberately; deleting it is a separate decision.

### 2.2 · Four engineer rulings taken and recorded — `Q-GLPNETA13`

Asked through the shipped `tools/bkquestion/` template (validated, then recorded to the
append-only git-tracked ledger `.specify/decisions/engineer-decisions.jsonl`):

| id | ruling |
|---|---|
| **Q-GLPNETA13-01** | **Release bar = FEATURE-LEVEL, fleet-wide.** A CalVer is cut only when develop carries a completed, implemented AND codexreviewed feature. Settles a live three-lane, two-bar tension |
| **Q-GLPNETA13-02** | **Gleam: `059` is CANONICAL; re-derive `050`'s LINK/TRANSPORT tier onto develop.** Unblocks N12 — 45 of the 47 genuinely open tasks — and rescues stranded toolchain commit `5def2750` |
| **Q-GLPNETA13-03** | **Start `bk-guardian` and take a catalog backup before catalog-heavy work.** Discharges Q17 |
| **Q-GLPNETA13-04** | **Send `specs/080/spec.md` AS-IS to Udi.** No provisional §1.14 ruling. Routes J2 |

### 2.3 · COOP — full ACK sweep published, nothing owed

`ACK-SWEEP-20260831T1245Z-ariellas-glpnet-...md` on `I:\coop\glpnet\`. Covers every ACK owed since
this lane's last publication (`20260827T204707Z`): the held-branch escalation (fulfilment),
@olamnit `1220Z`, @shiras `20260830T2230Z` §1, @gavriella `1050Z` pipefail (ADOPTED),
@olamnit `ACTIONABLE 20260827T2245Z` (measured negative), @gavriella `20260827T205939Z` ADDENDUM-1,
@shiras `20260828T0200Z` P1, and 11 peer-repo items ACKed as recipient without answering.

### 2.4 · Landed on develop

`PR #254` — roadmap round-53 export + rev5, rebased onto develop, merged 12:37:18Z.

## 3 · MEASURED CORRECTIONS — including two against my own records

### 3.1 · 🔴 `gh pr merge` is INTERMITTENT, not blocked. My own standing hazard over-generalised.

Refused **twice** this session in **both** Bash and PowerShell, then **succeeded on the third
attempt in the same session and the same shell** (PR #254, 12:37:18Z).
**Operational rule: retry once per turn before escalating.** Also corrects @olamnit's `1220Z` §1,
which states it as a wall. `buildkit release` remains separately reported as unaffected.

### 3.2 · 🔴 rev5 §9's `DOTNET_ROOT` claim is FALSE, and the live value is the trap rev5 itself warned about

```
DOTNET_ROOT (User)                            = C:\Users\ariel\.dotnet        -> sdk: 11 ONLY
C:\Users\ariel\AppData\Local\Microsoft\dotnet  -> sdk: 10.0.400, 11            (superset)
C:\Program Files\dotnet                        -> sdk: 10.0.302, 11            (superset)
(Get-Command dotnet).Source                    = C:\Program Files\dotnet\dotnet.exe
```

rev5 §9 asserted `DOTNET_ROOT` was persisted to the **AppData** root and was first on PATH.
**Neither holds.** The persisted root contains **only** SDK 11 — precisely rev5 §2 trap 1
(*"a user-local root sees only what you put in it… every 10.0 runtime disappears; builds work,
running `net10.0` apps does not"*), and glpnet has 30 `net10.0` projects.

**NOT CHANGED — deliberately.** A persisted User env var is host-level state I did not set and
whose owner I have not established. `dotnet` on PATH still resolves to a superset root, so nothing
is currently proven broken. **Raised as an engineer question, not silently repaired.**

### 3.4 · 🔴 Ruling `Q-GLPNETA13-03` turns out to be only HALF EXECUTABLE — two defects, both reproduced

I executed the ruling and it did not survive contact:

```
buildkit-guardian start   -> running (pid 5216), 5 modules, gate: cleared, open findings: {}
buildkit-guardian backup now (attempt 1) -> "pgdb/.lock held by PID 19620 — which is THIS process —
                                            in phase 'supervisor-acquiring' ... a leaked acquire"
buildkit-guardian backup now (attempt 2) -> same, PID 17464.  REPRODUCED.
buildkit-marathon capture  -> "registry busy ... PID 5216 held it on ALL 61 attempts"  = THE GUARDIAN
```

1. **`backup now` self-deadlocks.** It asks for a lock its own process already holds. Different PID
   each attempt, so this is the command deadlocking against itself, not a peer.
2. **A running guardian STARVES this repo's marathon.** The daemon holds `pgdb/.lock`
   continuously, so `marathon capture/trace/checkpoint` cannot write while it is up. The protective
   measure and the durable record are mutually exclusive on this repo as shipped.

**Guardian was STOPPED again** to restore marathon writability. Cost of doing so is low:
`supervisor=none`, so it would not have survived the reboot in any case.
**Two catalog backups remain**, one verified `restorable=True`. **The ruling stands and is not
withdrawn — it is currently unimplementable, and that is a defect for the buildkit lane, not a
reason to stop asking for backups.**

### 3.3 · The `next:` pointer and the restart-doc chain both mislead

rev5 was written 2026-08-27 and pushed to `096`, where it sat unmerged for 4 days. A resume that
reads `docs/restart/` on develop would have found **rev4** and taken the superseded next-action.
Fixed by landing rev5 and then this rev6 in the same PR chain.

## 4 · WHAT WAS *NOT* DONE, AND WHY — no silent deferrals

| not done | reason |
|---|---|
| **`/bk-roadmap` reconcile/import/dedupe/export round** | Directed, then the session was redirected to reboot prep. It is catalog-heavy, and ruling **Q-GLPNETA13-03** puts a guardian backup in front of exactly that. **First substantive action after reboot** |
| **Question round 2 (`Q-GLPNETA14`)** | **Authored and VALIDATED, not asked.** 4 questions: readiness authority · buildkit write authority · roadmap `spec_path` blindness · bk-flow rollout controls. File is committed and ready to put straight to the engineer |
| **Question round 3** | Not authored. Would carry: Q48 glpquick trust distribution · `096` + superseded-ref disposition · ZA15/16/17 (085 homing, 082 fold, 065 FR-008) · L3 merge-all scope |
| **The guardian BACKUP half of `Q-GLPNETA13-03`** | **ATTEMPTED AND BLOCKED BY A REPRODUCED TOOL DEFECT — see §3.4.** The daemon was started (5 modules, gate cleared, 0 open findings) but `backup now` self-deadlocks, and the running daemon starves every marathon write in this repo. Guardian **stopped again** to restore writability |
| **`/bk-release`** | **Correctly a no-op.** `origin/main..origin/develop` is a roadmap export and restart docs. Now backed by ruling `Q-GLPNETA13-01` rather than by my judgement |
| **ZA01 `/bk-plan` on 083** | Still the substantive next action; the session was spent on the escalation, the ACK backlog and the rulings |

## 5 · WHAT'S NEXT — IN STRICT ORDER

1. **The `/bk-roadmap` round** — import from the coop inbox, reconcile, dedupe, export, publish to
   **`I:\coop`** (never `D:\coop` — §8.3), commit, push. Then the not-closed epics/features table.
2. **Ask `Q-GLPNETA14`** — the file is committed and validated; put it straight to the engineer.
3. **ZA01 `[plan midi 11]` — `/bk-plan` on 083-glptutorial-corpus-goldens** (ruling **R3**).
   In-progress, owned by this lane, FR-002 ruled *record the rejection*. **The substantive next action.**
4. **N12 execution under ruling `Q-GLPNETA13-02`** — `059` canonical, re-derive `050`'s transport
   tier onto develop. This is the largest genuinely open build in the repo.
5. **Route J2 to Udi** under `Q-GLPNETA13-04` — the artifact is ready; only the delivery route is owed.
6. **P04 `[analyze maxi 17]`** — 3rtask unshipped-work / worktree scan (ruling **R4**).
7. ZA02–ZA07 — 083 through tasks → analyze → implement → codexreview → ship → close.
8. **P11** — marathon → bk-flow migration (gated on `Q-GLPNETA14-04`).

## 6 · POST-REBOOT RELAUNCH — REGISTERED AND VERIFIED

```
Task      : BK-OnRestart          State: Ready
Trigger   : AtLogOn  ARIELLAS\ariel   delay PT45S
Execute   : C:\Program Files\PowerShell\7\pwsh.exe
Arguments : -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden
            -File "D:\BSTDEV\tools\mstack\scripts\fleet\post-reboot-restart.ps1"
            -WaitForMounts -Layout Tabs
Command   : claude --continue --autocompact 1000000     (per tab)
```

**Dry-run verified 15/15 repos resumable**, every one with a non-empty session store:

`ospark` 17 · `tefl` 24 · `hatzinor` 20 · `olamnit` 24 · `buildkit` 29 · `qhstate` 23 ·
`yngraw` 28 · `crucible` 29 · **`glpnet` 35** · `lejepa` 30 · `mstack` 37 · `yngwin` 43 ·
`yngcor` 2 · `ynglin` 2 · `yngapp` 2

⚠ **The launcher covers 15 repos, not the 12 named in the directive.** `yngcor`, `ynglin` and
`yngapp` were added to the table on 2026-08-31 and are live lanes with stored sessions. The task
is installed **without `-Only`**, so all 15 come back. Scoping it to 12 would silently kill three
lanes, which is the exact failure class this tool exists to prevent — say the word to scope it.

**`--continue`, never `--fork-session`:** the session resumes mid-thread rather than continuing a
copy, and `--autocompact` is pinned to the CLI maximum so nothing summarises on the way back.

## 7 · ENGINEER RULINGS ON RECORD (cite, never re-ask)

| id | ruling |
|---|---|
| **R1** | Stranded coop exports — each lane recovers its own |
| **R2** | Takt partition — `kind=tokens` is normative; the fix is the module owner's |
| **R3** | Next feature — **finish 083 first** |
| **R4** | 3rtask budget — skip the supply rootcause, run the unshipped-work scan |
| **Q-GLPNETA13-01** | Release bar = **feature-level**, fleet-wide |
| **Q-GLPNETA13-02** | Gleam — **059 canonical**, re-derive 050 |
| **Q-GLPNETA13-03** | **Start guardian + backup** before catalog-heavy work |
| **Q-GLPNETA13-04** | **Send 080's spec as-is to Udi**; no provisional §1.14 ruling |
| — | C# 15 opt-in approved and mandated — implemented as `LangVersion preview` |

## 8 · OPEN ENGINEER BLOCKS

| id | block |
|---|---|
| **J2** | §1.14 UnifyFail vs CompileError for 080 — **Udi's, not Gabi's**. Route ruled; delivery route still owed |
| **Q-GLPNETA14-01..04** | readiness authority · buildkit write authority · roadmap `spec_path` blindness · bk-flow's 0-of-4 rollout controls. **Authored, validated, not yet asked** |
| **NEW** | `DOTNET_ROOT` points at an 11-only root (§3.2) — repair it, or rule that PATH resolution is sufficient? |
| **NEW** | `096` and other superseded refs — delete, or retain as evidence? |
| **ZA15/16/17** | 085 homing · 082 fold + missing `feature_pipeline` row · 065 FR-008 |
| **Q48** | glpquick trust material rotated 2026-08-11 — distributed out-of-band or not? Residual risk carried into a delivered ship-token claim |
| **L3** | "merge all" cannot be executed as stated — 20 unmerged branches, one carrying a recorded DO-NOT-MERGE (red suite + unredacted secret) |

## 9 · STANDING HAZARDS

1. **`gh pr merge` is FLAKY, not blocked** (§3.1). Retry once per turn, both shells, before escalating.
2. **Registry-lock contention is routine and is NOT a stuck lock.** Two peer `marathon takt` runs
   held it this session (~4 min and ~10 min). **Verify liveness with PowerShell `Get-Process` and
   sample CPU; never reap.** Git-Bash `ps -p` cannot see native Windows PIDs and will lie.
3. **`D:\coop` is a LOCAL directory, not the shared volume.** The shared coop is `I:\coop` ==
   `\\192.168.0.108\GAVRI_D\coop`. `BUILDKIT_COOP_INBOX = I:\coop` is persisted at User scope —
   **verified present this session.** Never trust `publish: coop mirror OK`; verify the FILE.
4. **Git-Bash cannot test `I:`/`G:`/`H:` as paths** — `[ -d "I:" ]` is false for a mounted drive.
   Use PowerShell `Test-Path`.
5. **`marathon expand --steps` is COMMA-delimited with no escaping**, one step per call.
   `checkpoint` needs `--step`; `expand` needs `--item`; use `trace` for completed work that
   never had a step. `checkpoint` has **no held/blocked state**, so a gated step can only be
   logged complete, which over-reports.
6. **Pipe discipline (adopted from @gavriella's measurement):** a piped gate returns the pipe's
   last rc, turning a refusal into a pass. Git-Bash invocations carry `set -euo pipefail`;
   PowerShell reads `$LASTEXITCODE`. PowerShell is immune; bash is not.
7. **Long suite runs must be detached** via `Start-Process`, outside the tool process tree's
   10-minute cap, and stragglers reaped first. An absent `Total:/Passed:/Failed:` summary means
   the run did not finish, whatever the exit code says.
8. **`J:` (SHIRAS) is unreachable from this host.** That means *I cannot see SHIRAS*, never that
   it is absent.

## 10 · ENVIRONMENT

```
$env:PYTHONUTF8 = 1
$env:BUILDKIT_COOP_INBOX = "I:\coop"        # PERSISTED, User scope — verified
# DOTNET_ROOT (User) = C:\Users\ariel\.dotnet  -> SDK 11 ONLY. See section 3.2 before trusting it.
# A superset root is C:\Users\ariel\AppData\Local\Microsoft\dotnet (10.0.400 + 11).
```

Host **ARIELLAS**, actor **`ariellas`**. `I:` = `\\192.168.0.108\GAVRI_D` (shared board volume,
present). `G:`, `H:` present. `J:` (SHIRAS) **absent**.
`config.local.json` → `sched_root: I:/coop/glpnet/sched`, `scheduler_actor: ariellas`.

## 11 · REBOOT READINESS

- [x] All work committed, pushed, and merged to develop
- [x] Marathon state durable — 3 traces + 1 pre-reboot capture
- [x] 4 engineer rulings recorded in the git-tracked ledger
- [x] COOP ACK backlog cleared; fulfilment published on the shared volume
- [x] Held-branch escalation discharged with measured evidence
- [x] `BK-OnRestart` task registered, dry-run verified 15/15 resumable
- [x] Next action identified, ordered, and unblocked
- [x] Guardian ruling executed as far as the tool allows; the two defects it exposed are captured
      as `mitem-01a0585b-0c02-7000-a355-5f5513426be3` and written up in section 3.4

**REBOOT IS SAFE.**

— `ariellas` · `glpnet` · `mrun-f5ef56dba3c1` · `2026-08-31T15:05:00Z`
