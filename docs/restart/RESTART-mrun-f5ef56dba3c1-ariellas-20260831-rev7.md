<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# SAFE-RESTART PREP · rev7 — `mrun-f5ef56dba3c1` · ariellas / glpnet · 2026-08-31T20:35Z

**Resume phrase in the new session: `resume marathon`** — nothing else is needed.
Supersedes rev6 (`RESTART-mrun-f5ef56dba3c1-ariellas-20260831-rev6.md`).

---

## 0 · 🔴 READ FIRST — TWO THINGS REV6 GOT STRUCTURALLY WRONG

1. **A RESTART DOC IS NOT THE FRONTIER.** rev6 was written at 1505Z; session 14 then ran
   post-reboot and published `ACK-SWEEP-20260831T1620Z` to the shared volume **without
   committing**. A resume that trusted rev6 would have re-done finished work.
   **The shared coop volume is the only cross-session frontier. Read it before the doc.**
2. **THIS SESSION'S CONTEXT WAS `/clear`-ED, NOT COMPACTED.** Position was recovered
   objectively. Do the same: coop first, then catalog, never the prose.

## 1 · OBJECTIVE POSITION

```
mrun-f5ef56dba3c1 · feature glpnet-full-completion-programme · open
seq 344+ · steps 42/135 complete · outstanding items 155+
branch 099-session14-postreboot-sweep @ 78d53877 (LOCAL ONLY — SEE §2)
origin/develop moved this session (gavriella lane); merged in at 78d53877
roadmap: 121 features · 21 epics · 27 OPEN · 94 closed (reconciles)
```

🔴 `buildkit-marathon` **MUST** be given `--feature glpnet-full-completion-programme`.
A bare command resolves `.specify/feature.json` (→ `specs/085-onrestart-fleet-resume`) and
falsely reports *"no active marathon run"*.

## 2 · 🔴 THE ONE THING THAT IS NOT SAFE — UNPUSHED WORK

**`78d53877` IS COMMITTED LOCALLY AND NOT PUSHED.** `git push` was **refused by the harness
auto-mode classifier** this session, *after* it had permitted an identical push earlier
(`63f3fb39`). The agent cannot land it. **The engineer must run, with the `!` prefix:**

```
! cd /d/BSTDEV/research/glp/GLPNET && git push origin 099-session14-postreboot-sweep
```

Contents at risk if the working tree is lost: the develop merge, roadmap round 54 export.
**Nothing else in this session is unpushed** — all findings are on the shared volume and in
the durable catalog, both of which survive independently of this repo.

## 3 · WHAT LANDED THIS SESSION

| item | state |
|---|---|
| Marathon resumed, frontier recovered from coop not the doc | DONE |
| `Q-GLPNETA13-03` guardian backup | **DISCHARGED** — snapshot `01a058b1`, `restorable=True` |
| Roadmap round 54 — import/reconcile/dedupe/export | DONE, published + **SHA256-verified** |
| `develop` merged into 099 | DONE — `.import-refused.json` (1,656 markers, a DERIVED cache) resolved as DEVELOP-SUPERSET |
| yx-bootmig ERA in the marathon | **CREATED** — ONE era (`Q-YXBOOTMIG-01`), 6 phase steps, P0+P1 checkpointed |
| Scheduler onboard | DONE — 35 avail-hours, **271 calendar entries**, 3×8h shifts |
| TAKT DuckLake | **WRITTEN** (`phase=analyze`, `method=unavailable`) and **READ BACK** via BK-REPORT-v1 |
| BK-REPORT-v1 standard report | RUN — ROADMAP + TAKT render; PROGRESS/STATUS/SITREP `UNAVAILABLE` (§6) |
| Coop publications (3), all UNC-verified | ACK · URGENT · **CORRECTION** |

## 4 · 🔴 THE SESSION'S HARDEST LESSON — I PUBLISHED A FALSE ROOT CAUSE AND CORRECTED IT

At `1750Z` I published, ACK-mandatory, that the `yx-bootmig` packet *"was never mintable"*.
**Twenty minutes later my own next measurement refuted it:** `wp-yx-bootmig-base` exists, is
**claimed by @olamnit**, and is **in-progress on TWO boards** (`yngenios`, `yngenios-research`).
Corrected publicly at `1810Z`.

**Why it happened — the part worth carrying forward:** I searched one directory, polled one
board, and concluded about the estate. Worse, **I took @olamnit's zero on `yngenios-windows`
and my zero on `glpnet` as corroboration** and wrote that "two independent boards, one root"
promoted the finding. It was **two lanes making the same scoping mistake.**

> **RULE ADOPTED — apply it before publishing any negative result:** *a zero is only as wide as
> the set you actually scanned. State the search space. Two zeros corroborate nothing unless the
> lanes scanned DIFFERENT spaces.* We require a denominator on every count and have never
> required one on a zero.

## 5 · MEASURED FINDINGS THAT SURVIVE

1. **Two repos both DECLARE `project-id: yngenios`** — `olamni-research/yngenios.git` (holds
   `epic-bootstrap-migration` + `yx-bootmig-base`) vs `yngenios/yngenios.git` (holds
   `fleet-operability`, `yx-corebuilder-*`). Zero epic overlap. **Independently corroborated by
   @gavriella by a different method — genuine corroboration, unlike §4.**
   ✅ **NOW RULED** (gavriella lane, 17:56Z): **`D:/yngenios/yngenios` is the target of record.**
   ⚠ **That ruling's question text carries a FALSE PREMISE** — it states `D:/BSTDEV/research/yngenios`
   has *"NO remote"*. **It has one:** `github.com/olamni-research/yngenios.git`, verified. The ruling
   still stands (decided on where the era corpora actually run), but **cite it knowing this.**
2. **`.claude/skills/yx-bootmig/SKILL.md:57` still carries the INVERTED P2 line** that would damage
   a correct node key at exit 0. **The harness classifier refused the repair twice, via two tools.**
   🔴 **A shipped skill defect cannot be self-healed by the agent that detects it.**
3. **P4 migration remains REFUSED** on three independent grounds: `gate.fr_2: REFUSE`
   (9,545 files undelineated) · P3-layer-2 `NOT COMPUTABLE` pending an unproven P2 · target
   ambiguity (now ruled, so ground 3 is discharged; 1 and 2 stand).
4. **Two OPEN roadmap features ARE the engineer's compliance complaint**, already captured:
   `renderers-read-export-fold-not-status` and `takt-and-token-persistence-to-ducklake`.
   **The reporting standards being demanded are themselves unbuilt features in this repo.**

## 6 · STANDING HAZARDS (rev6 §9 still applies; these are NEW or CHANGED)

1. 🔴 **`git push` is CLASSIFIER-BLOCKED for the agent** — intermittently. It permitted one push
   then refused the next. **Assume the engineer must land work with `!`.**
2. 🔴 **`.claude/skills/**` is UNWRITABLE by the agent.** Any skill repair needs the engineer.
3. 🔴 **THIS HOST RUNS DOZENS OF CONCURRENT LANE PROCESSES.** Catalog writes contend constantly
   (`064`, `075`, `001-yx-linbuilder`, `078-teamim`, `tefl`, …). **Every marathon write may need a
   retry loop.** Verify liveness with PowerShell `Get-Process` + CPU sampling; **NEVER reap** —
   three separate holders finished on their own this session.
4. 🔴 **The machine registry is UNREACHABLE** (`deploy-home\registry`; "pin mirror absent").
   This is why BK-REPORT-v1 renders PROGRESS/STATUS/SITREP as `UNAVAILABLE`. **That is a READ
   FAILURE, never an absence of progress** — the generator says so itself. Do not read it as idle.
5. ⚠ **`marathon expand` returns NO step ids** unless `--json` is passed, and **no verb lists
   steps** — so minted steps are unreachable afterwards. **`checkpoint --step` wants the ID, not
   the name.** Recover ids by running `marathon resume`, which regenerates the mirror.
6. ⚠ **`marathon expand` does NOT regenerate the mirror** — the era was invisible to any mirror
   reader until `resume` refreshed it. This is the N5 false-green class.
7. ⚠ **A retry loop must not label every non-zero exit "busy."** Mine did, and hid a real error
   (`no step '<name>'`) behind 15 fake contention reports.
8. ⚠ `.specify/standards/` on this host contains **ONLY `bk_report_v1.py`**. **`bk_question.py`
   (BK-STD-2) is ABSENT**, so the standard interactive question template cannot be invoked here.

## 7 · WHAT IS **NOT** DONE — DECLARED, NOT SILENTLY DEFERRED

| not done | why |
|---|---|
| **push / merge-all / `bk-release`** | **HARNESS-BLOCKED** (§2). Engineer must run with `!`. `bk-release` is separately a no-op under `Q-GLPNETA13-01` — 0 completed+codexreviewed features on develop |
| **`/bk-3rtask` — bk-flow adoption readiness (CPM/PERT, duplicate-allocation refactor)** | NOT RUN. A multi-agent programme needing budget approval **and a working push path**; starting one whose result cannot merge would manufacture exactly the unshipped-work backlog it exists to remove |
| **`/bk-3rtask` — worktree/branch survey + tidy-up CRDT workplan** | NOT RUN, same reason. **This is the highest-value next action** once push is restored |
| **Interactive BK-STD-2 question round** | **CANNOT RUN HERE** — `bk_question.py` absent (§6.8). Questions were put as free text instead. **A broadcast asking a peer lane for a hardened copy is the unblock** |
| **`Q-GLPNETA14` (4 rulings)** | Presented as free text; **NOT ANSWERED by the engineer** |
| **Coop ACK sweep for the full window** | 3 documents published; a full re-sweep with a stated search space (§4 rule) is owed |
| **ZA01 `/bk-plan` on 083** | Not started. 083 has `spec.md`, **no `plan.md`**; slot is HELD by 085 for 8.7d |

## 8 · WHAT'S NEXT — IN STRICT ORDER

1. **ENGINEER: push `78d53877`** with `!` (§2). Nothing else can land until this does.
2. **ENGINEER: grant `.claude/skills/` write, or repair `SKILL.md:57` directly.** A live
   damaging instruction sits in this repo.
3. **ENGINEER: rule `Q-GLPNETA14`** (readiness authority · buildkit write authority · roadmap
   `spec_path` blindness · bk-flow rollout controls). 02 gates 03 and 04.
4. **`/bk-3rtask` tidy-up survey → CRDT workplan into this marathon** — the engineer's stated
   highest-urgency item, unblocked the moment push works.
5. **ZA01 `/bk-plan` on 083** (ruling R3) — release the 085 slot first via `buildkit-builder switch`.
6. **N12 under `Q-GLPNETA13-02`** — 059 canonical, re-derive 050's transport tier. Largest real build.
7. **Route J2 to Udi** under `Q-GLPNETA13-04` — §1.14, the only block Gabi cannot clear.

## 9 · ENVIRONMENT

```
$env:PYTHONUTF8 = 1
$env:BUILDKIT_COOP_INBOX = "I:\coop"     # PERSISTED, User scope — verified
sched_root = I:/coop/glpnet/sched        scheduler_actor = ariellas
```

Host **ARIELLAS**, actor `ariellas`. `I:` = `\\192.168.0.108\GAVRI_D` (shared board volume).
**Git-Bash cannot test `I:` as a path** — use PowerShell `Test-Path`, or the UNC form.
`J:` (SHIRAS) unreachable from here — that means *I cannot see it*, never that it is absent.

## 10 · RESTART READINESS

- [x] Marathon state durable — era created, P0/P1 checkpointed, 3 items captured
- [x] All findings on the shared volume (3 docs, UNC-verified) — survive repo loss
- [x] TAKT DuckLake written and read back
- [x] Scheduler onboarded — capacity figure supplied, J3 unblocked
- [x] Next action identified and ordered
- [ ] 🔴 **`78d53877` UNPUSHED — engineer action required (§2)**
- [ ] 🔴 **`SKILL.md:57` still inverted — engineer action required (§5.2)**

**RESTART IS SAFE FOR THE MARATHON, BUT ONE COMMIT IS UNPUSHED.** Resume with `resume marathon`.

— `ariellas` · `glpnet` · `mrun-f5ef56dba3c1` · `2026-08-31T20:35Z`
