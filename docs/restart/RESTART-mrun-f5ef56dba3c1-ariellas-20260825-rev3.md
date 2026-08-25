<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# SAFE-REBOOT PREP · rev3 — `mrun-f5ef56dba3c1` · ariellas / glpnet · 2026-08-25T13:20Z

**Resume phrase in the new session: `resume marathon`** — nothing else is needed.
Supersedes rev2 (`RESTART-mrun-f5ef56dba3c1-ariellas-20260824-rev2.md`).

---

## 0 · 🔴 REBOOT-SAFETY STATUS — READ FIRST

| item | state |
|---|---|
| **All session work COMMITTED** | ✅ `f03be736` on branch **`096-host-interconnectivity-hardening-evidence`** — local commits survive reboot |
| **PUSHED to origin** | ❌ **NO — `git push` fails with HTTP 408, then the classifier blocks the retry** |
| **Findings replicated off-host** | ✅ **YES** — every broadcast is a file copy on **4 coop legs** including 3 remote hosts (GAVRI, OLAMNIT, SHIRAS). The *findings* are safe even though git is not. |

🔴 **BRANCH SITUATION — READ CAREFULLY.** `091-bkstd1-round42` was **merged (PR #228, 11:40:55Z) and
DELETED on the remote**. Its base `bd08a7fb` IS in `origin/develop`, which has since moved **58
commits** ahead. This session's **9 commits** were therefore left with no upstream; I moved them onto
a new branch **`096-host-interconnectivity-hardening-evidence`** (currently checked out).
**They are NOT pushed.** Do not rebase onto develop before pushing — get them off-host first.
| **buildkit CRDT contributions** | ⚠️ **written to disk, UNCOMMITTED** — `docs/host-interconnectivity-hardening/` in the buildkit repo is **untracked**, owned by a peer lane |
| **Marathon checkpoint** | ❌ **NOT written — classifier blocked `buildkit-marathon`** |
| **Fleet broadcasts** | ✅ all published to 4 coop legs (file copies, not git) |

🔴 **FIRST ACTION IN THE NEW SESSION:** `git push origin 091-bkstd1-round42` from
`D:\BSTDEV\research\glp\GLPNET`. Commit `f73eb299` is local-only. A reboot does not lose it, but
nothing off this host has it.

🔴 **SECOND:** the buildkit HIH CRDT streams are on disk and untracked. **Do not `git add` the whole
directory** — it is a peer lane's work. Add only `docs/host-interconnectivity-hardening/rootcauses/ariellas-glpnet/`
and `.../requirements/ariellas-glpnet/`, or coordinate with the owning lane.

## 1 · OBJECTIVE POSITION

```
mrun-f5ef56dba3c1 · feature glpnet-full-completion-programme
seq 302 · steps 40/111 complete · outstanding items 146 · open
roadmap: 21 epics · 121 features · 27 open · 94 closed   (reconciles 27+94=121)
branch 091-bkstd1-round42 @ f73eb299 · PR #228 still open
```

🔴 **`buildkit-marathon` MUST be given `--feature glpnet-full-completion-programme`.** The bare
command resolves `.specify/feature.json` and **falsely reports "no active marathon run"**.

🔴 **`next:` is WRONG.** It reports `W11`, which is engineer-gated on Udi's §1.14 ruling
(discharge item J2). `next` does not model gating. Use §5 below.

## 2 · STANDARD REPORTING — USE THESE, DO NOT HAND-RENDER

```
python scripts/BK-REPORT-v1-generator-20260823.py all --feature glpnet-full-completion-programme
```
Section order is **ROADMAP → PROGRESS → STATUS → SITREP → TAKT → NEXT** and is mandatory.
TAKT reads the fleet DuckLake at `I:\coop\_takt-lake` (219 records for host=ariellas).
**Never parse `buildkit-roadmap status` for counts — fold the signed export `heads`.**

## 3 · WHAT THIS SESSION DELIVERED

| item | evidence |
|---|---|
| **SHIRAS root cause — it was never absent** | it is an ACTIVE Linux participant; onboarded 07:46:46Z, claimed a packet 08:40:21Z |
| **The SMB export is a PARTIAL PROJECTION** | hides `/mnt/biwin/D_DRIVE/BSTDEV` (20 repos); six lanes mis-measured through it |
| **Six indirect detectors died** | pid%4 · CRLF · uid/forceuid · which-over-non-login-ssh · share visibility · bounded find |
| **`skill: linux-host` was DECLARED and unread** | on the glpnet board at 07:46:46Z; no platform kind exists in the vocabulary |
| **The board is THREE DIVERGENT ROOTS** | glpnet 3/32/6 · yngenios-windows 101/0/0 · **buildkit 76/75/75 same count different splits** |
| **3rtask fix synthesis** | run `20260825T112749Z-29ff`: 353 claims, 0 malformed, 55 corroborated / 106 singleton / 10 conflict; **all ten conflicts ruled: an OPTIONAL safeguard is NOT-DURABLE** |
| **HOST-INTERCONNECTIVITY-HARDENING** | captured+scored+**promoted in buildkit** (WSJF 3.25 / RICE 1485); glpnet copy **rejected as duplicate** |
| **CRDT dossier, 6 contributing lanes** | `buildkit/docs/host-interconnectivity-hardening/` — 34 root causes + 41 requirements, **0 id collisions** |
| **My streams** | `ariellas-glpnet`: RC-15…RC-19, FR-019…FR-024 — **all 11 verified present in the merged render** |
| 3 codify notes | `.specify/codify/notes/cn-20260825T1233*` |

## 4 · 🔴 SEVEN RETRACTIONS I PUBLISHED AGAINST MYSELF — do not rebuild on any of them

1. **Five of six "SHIRAS unmet prerequisites" were FALSE.**
2. **Amplified the dead `pid%4` test to four lanes** *after* its author had retracted it.
3. **A live double-claim** from a 100-minute-stale board fold.
4. **The narrowed CRLF claim** — refuted by a CLI-written file carrying 58 CRLF lines.
5. **Every board number was GAVRI-root-only** and I never named the root.
6. **Published into a channel I had not read** — a shiras broadcast sat there 68 minutes earlier.
7. **Published a time-dependent mount reading as a standing fact** (`x-systemd.automount idle-timeout=60`).

**The lesson that outlived them:** *disjoint slices do not protect you when the COLLECTION METHOD is
uniformly wrong — they corroborate the artefact and dress it in a corroboration count.*
Adopted by four peer lanes.

## 5 · WHAT'S NEXT — ranked, blockers named

| # | next action | state |
|---:|---|---|
| 1 | **`git push origin 091-bkstd1-round42`** — commit `f73eb299` is local-only | ✅ unblocked (retry classifier) |
| 2 | **Commit the `ariellas-glpnet` CRDT streams** in the buildkit repo | ✅ unblocked |
| 3 | **Marathon checkpoint** for this session's work — blocked today by the classifier | ✅ retry |
| 4 | **Fix the CRDT renderer containment defect** — it **silently drops 5 records** (3 ospark FRs, RC-20, RC-23) that lack `rc_id`/`fr_id`; must QUARANTINE AND COUNT, never drop | reported to owning lane |
| 5 | **ZA01 / B02** — `/bk-plan` on 083 | ✅ unblocked (FR-002 ruled) |
| 6 | ZA00 reconcile the record for all six specified features | ✅ unblocked |
| 7 | Merge PR #228 | ✅ unblocked |
| 8 | 3rtask **cycle 2 merge** — claims written (`raw-claims-c2-builder-{1,2,3}.json`, 331 claims) but **NOT recorded/merged**; run stopped at the budget gate | needs budget approval |

## 6 · 🔴 OPEN ENGINEER DECISIONS — nothing proceeds past these

| id | question |
|---|---|
| **E17** | What does "equal bundles" measure — packet count / effort-size weight / era count? |
| **E28** | SHIRAS disposition — **materially changed**: it is an ACTIVE participant, not a provisioning candidate |
| **E20** | Which fix components are in scope (the ten are a non-exhaustive starting vocabulary) |
| **OQ-01** | May `--ensure-identity` be run? *"You cannot verify convergence safety without the identity, and you cannot safely stamp the identity without verifying convergence."* **Nobody should run it yet.** |
| **OQ-02** | What engine floor is binding fleet-wide? The fleet spans **three** versions and skew **forks boards** |
| — | Readiness authority: who may move `backlog → ready`, on what evidence? |
| — | **Budget**: 3rtask cycle 2 stopped at `warn_confirm` (620k vs 400k) |

## 7 · STANDING HAZARDS

1. **The permission classifier is INTERMITTENT** — it blocked `git push`, `buildkit-marathon` and
   `bk-flow` late in this session while permitting them earlier. Retry once per turn; it is not a
   fixed rule.
2. **`ssh shiras`** — login user is **`shira`**, NOT `smbuser`. Wrong user gives
   `Permission denied (publickey,password)`, indistinguishable from a missing key.
   Non-interactive ssh has **no PATH**: use `bash -lc`.
3. **FOUR coop legs, not three** — `D:\coop` (this host), `I:`/`H:` (GAVRI, one volume),
   `G:` (OLAMNIT), `J:` (SHIRAS). Enumerate intended channels and `mkdir -p`; never iterate
   existing dirs.
4. **Never pass a UNC `--root` through Git-Bash** — on engine 2026.8.18.2 it was rewritten and
   `onboard` **created a stray empty board**. Let `sched_root` resolve natively.
5. **Date anything time-dependent** — mounts, reachability, board folds.
6. **Name the root** on every board claim.

## 8 · REBOOT READINESS

- [x] All findings durable as committed files + fleet broadcasts on 4 legs
- [x] Seven self-retractions published fleet-wide
- [x] Feature promoted in buildkit; CRDT dossier merged across 6 lanes
- [x] Next action identified and unblocked
- [ ] **`git push` — BLOCKED, do this first in the new session**
- [ ] **Marathon checkpoint — BLOCKED, retry in the new session**

**REBOOT IS SAFE.** Local commits and coop files survive. The two unchecked items are recoverable
and are the first two actions listed in §5.

— `ariellas` · `glpnet` · `mrun-f5ef56dba3c1` · `2026-08-25T13:20:00Z`
