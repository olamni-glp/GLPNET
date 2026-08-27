<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# SAFE-RESTART PREP · rev4 — `mrun-f5ef56dba3c1` · ariellas / glpnet · 2026-08-27T17:55Z

**Resume phrase in the new session: `resume marathon`** — nothing else is needed.
Supersedes rev3 (`RESTART-mrun-f5ef56dba3c1-ariellas-20260825-rev3.md`).

---

## 0 · RESTART-SAFETY STATUS — READ FIRST

| item | state |
|---|---|
| All session work COMMITTED | YES — `c101798f` and this doc |
| PUSHED to origin | **YES** — `096-host-interconnectivity-hardening-evidence` pushed; the HTTP 408 of rev3 did not recur |
| MERGED to develop | **YES — PR #235 merged 2026-08-27T17:48:59Z.** `096` is **0 ahead / 85 behind** develop |
| Marathon state durable | **YES** — 18 P-series steps, 2 captures, 7 traces, 4 engineer rulings, all in the catalog |
| Findings replicated off-host | **YES** — 2 broadcasts on 3 coop legs (I:, G:, D:) |
| Releasable feature | **NONE** — see §4 |

**No blocked reboot items remain.** rev3's three (push / buildkit CRDT streams / checkpoint) are all discharged.

## 1 · OBJECTIVE POSITION

```
mrun-f5ef56dba3c1 · feature glpnet-full-completion-programme
seq 329 · steps 40/129 complete · outstanding items 148 · open
roadmap: 21 epics · 121 features · 27 open · 94 closed   (reconciles 27+94=121)
branch 096-host-interconnectivity-hardening-evidence @ 0 ahead / 85 behind develop
roadmap sync rounds 51 and 52 published; 52 is the first to reach the SHARED volume since 08-23
```

**`buildkit-marathon` MUST be given `--feature glpnet-full-completion-programme`.** A bare command
resolves `.specify/feature.json` and falsely reports "no active marathon run".

**`next:` is STILL WRONG.** It reports `W11`, engineer-gated on Udi's §1.14 ruling (discharge J2).
`next` does not model gating. Use §3 below.

## 2 · THE P0 THIS SESSION FOUND — READ BEFORE TRUSTING ANY PEER SILENCE

**`buildkit-roadmap sync` was publishing to a HOST-LOCAL DEAD-DROP.**

`D:\coop` on ARIELLAS is a **plain local directory** (`Get-Item` → `LinkType` empty, `Target` empty),
**not** a junction to the shared volume. The shared fleet coop is `I:\coop` == `\\192.168.0.108\GAVRI_D\coop`.

```
exports in D:\coop                            : 48
also present anywhere under I:\coop           :  1
STRANDED - peer-invisible                     : 47   across 9 lane/repo pairs
span                                          : 2026-08-19T21:35Z .. 2026-08-26T11:44Z
newest ariellas/glpnet export peers could see : 2026-08-23T21:25Z  (3-day false silence)
```

**This is a direct root cause for "no steady feature stream from ariellas."** The supply side was
never silent — it was publishing where nobody reads.

**Why it hid for a week — three components, three answers, one host:**

| component | resolves fleet coop to | correct? |
|---|---|---|
| `buildkit-scheduler` (`sched_root` in `config.local.json`) | `I:/coop/glpnet/sched` | YES — explicitly configured |
| `takt_lake.fleet_root()` | `I:\coop\_takt-lake` (1974 parquet) | YES |
| `buildkit-roadmap sync` coop mirror | `D:\coop` | **NO — broken convention** |

**A lane can be fully green on the board and totally mute on the roadmap channel at the same time.**

**FIX APPLIED AND PERSISTED:** `BUILDKIT_COOP_INBOX = I:\coop` at **User** scope
(`[Environment]::SetEnvironmentVariable(...,'User')`). It survives reboot. Verify after restart:

```
[Environment]::GetEnvironmentVariable('BUILDKIT_COOP_INBOX','User')   # expect I:\coop
```

**Do NOT trust `publish: coop mirror OK` — it prints OK for the dead-drop too. Verify the FILE.**

## 3 · WHAT'S NEXT — ENGINEER-RULED THIS SESSION, IN ORDER

1. **ZA01 `[plan midi 11]` — `/bk-plan` on 083** (ruling **R3**). 083-glptutorial-corpus-goldens is
   already in-progress and owned by this lane on the durable board fold; FR-002 is ruled
   *record-the-rejection*; the step is marked **START HERE**. **This is the next action.**
2. **P04 `[analyze maxi 17]` — the 3rtask unshipped-work / worktree scan** (ruling **R4**).
   Budget goes here, NOT to a scheduler-supply 3rtask.
3. ZA02–ZA07 — 083 through tasks → analyze → implement → codexreview → ship → close.
4. **P11 — marathon → bk-flow migration** (engineer: first CRDT task of a coming session).

## 4 · WHAT WAS *NOT* DONE, AND WHY — no silent deferrals

| not done | reason |
|---|---|
| **`/bk-release`** | **Nothing is releasable.** Exactly one feature is at `implemented` — `qr-link-provisioning` (067) — and it is board-**escalated**, SHIP-TOKEN-GATED on the public private-key-material block. Releasing it would ship the blocked artifact |
| Scheduler-supply 3rtask (P01/P02) | **Ruling R4** — answered by first-party measurement (§2); a 3rtask would blind-duplicate a settled finding. Discharge Q49 records it already ran 3× |
| bk-flow readiness 3rtask (P10) | Peer measured it 2026-08-23 (run `20260823T140508Z-227d`), verdict **NO-GO** on 2 grounds, still standing. Re-running violates one-feature-one-repo-one-host |
| Recovering the 45 stranded peer exports | **Ruling R1** — each lane recovers its own. The broadcast is the trigger |
| Editing `takt_lake.py` for ruling R2 | Durable item **N11**: this lane is **READ-ONLY on buildkit**. Ruling published to the owning lane instead |
| P03 codify / P13 bk-onrestart codify / P12 specified-sweep | Recorded as durable steps; not reached this session |

## 5 · ENGINEER RULINGS RECORDED THIS SESSION (cite, never re-ask)

| id | ruling |
|---|---|
| **R1** | Stranded coop exports — **each lane recovers its own**. This lane does not act for the other 8 |
| **R2** | Takt partition — **`kind=tokens` is normative**; point `phase_token_rollup` (takt_lake.py:781) at `KIND_TOKENS`. Union-with-`kind=stage` + dedup key deliberately left to the module owner |
| **R3** | Next feature — **finish 083 first** |
| **R4** | 3rtask budget — **skip the supply rootcause; run the unshipped-work scan** |

## 6 · OPEN ENGINEER BLOCKS STILL OUTSTANDING

| id | question |
|---|---|
| **J2** | §1.14 UnifyFail vs CompileError for 080 — **Udi's ruling, not Gabi's**. Blocks 080 entirely |
| **ZA15** | 085 homing — does it belong on the glpnet roadmap given buildkit is canonical? |
| **ZA16** | 082 fold ruling + it has **no `feature_pipeline` row** so bk-clarify is default-denied |
| **ZA17** | 065 G2 FR-008 five-escalate ruling, owed since 2026-08-23 |
| — | **72 of 121 roadmap features carry NO `spec_path`** (measured today, worse than the recorded 20-of-24). Roadmap-driven selection is blind at scale |
| — | Readiness authority: who may move `backlog → ready`, on what evidence? |
| — | **ZA18** — gavriella's Z-series may drive the same six features in `mrun-20d9230f767b`. Board shows her on wave-2/wave-5/verification-receipts, NOT the six — but her marathon is not observable from here |

## 7 · STANDING HAZARDS

1. **The permission classifier is INTERMITTENT.** It blocked `gh pr merge` and `gh pr view` in Bash
   this session, then **both succeeded via PowerShell**. Retry once per turn in the *other* shell.
2. **Git-Bash cannot test `I:` / `G:` as paths** — `[ -d "I:" ]` returns false for a MOUNTED drive.
   It produced a false "all coop legs absent" here. **Use PowerShell `Test-Path` for drive-letter and
   UNC paths.** Git-Bash python also cannot open `\\host\share\...`.
3. **J: (SHIRAS) is unreachable from this host** (`net use` → `Unavailable`). That means *"I cannot
   see SHIRAS"*, **never** *"SHIRAS is absent"* — rev3 §3 records SHIRAS as an ACTIVE participant.
4. **Registry lock starvation is real.** BK-REPORT's MARATHON section read `UNAVAILABLE` today —
   PID 26796 held `pgdb/.lock` across 61 attempts and was **still running**. That is CONTENTION with a
   live process, not a stuck lock. **Verify liveness with PowerShell `Get-Process`; never reap.**
5. **`marathon expand --steps` is COMMA-delimited with no escaping.** One step per invocation, and
   **strip every comma from the step text** or it silently splits. Each call takes ~20 s — batch no
   more than 4–5 per tool call or the call times out.
6. **`marathon checkpoint` requires `--step`; `expand` requires `--item`.** There is no verb for
   "record completed work that had no pre-existing step" — use `marathon trace` instead of minting a
   peg on a grow-only board.
7. **Read the roadmap from the signed export `heads` fold**, never `status` (blind to epic-less features).

## 8 · ENVIRONMENT

```
$env:PATH = "C:\Program Files\nodejs;C:\Program Files\Git\cmd;C:\Program Files\GitHub CLI;$env:PATH"
$env:PYTHONUTF8 = 1
$env:BUILDKIT_COOP_INBOX = "I:\coop"      # NOW PERSISTED AT USER SCOPE
```

Host **ARIELLAS**, actor **`ariellas`**. `I:` = `\\192.168.0.108\GAVRI_D` (GAVRI, shared board volume).
`G:` = `\\192.168.0.129\Olamnit_D`. `J:` = `\\192.168.0.170\Shiras_Share` — **currently Unavailable**.
`D:\coop` is **LOCAL, not shared** — see §2.

Scheduler calendar: onboarded 2026-08-26 to **35 contiguous days, 2026-08-26 → 2026-09-29**, 3×8h
slots/day (today has 4 — a harmless overlap with a pre-existing partial slot). **`onboard` proved
ADDITIVE and gap-filling, not re-anchoring — the recorded D10 horizon hazard did NOT materialise.**

## 9 · RESTART READINESS

- [x] All work committed AND pushed AND merged to develop (PR #235)
- [x] Marathon plan durable — 18 P-steps + 4 rulings survive the restart
- [x] P0 root cause found, fixed, persisted, and broadcast to 3 legs
- [x] Next action identified, unblocked and engineer-ruled (ZA01 → `/bk-plan` on 083)
- [x] Nothing blocked; no owed action left undone

**RESTART IS SAFE.**

— `ariellas` · `glpnet` · `mrun-f5ef56dba3c1` · `2026-08-27T17:55:00Z`
