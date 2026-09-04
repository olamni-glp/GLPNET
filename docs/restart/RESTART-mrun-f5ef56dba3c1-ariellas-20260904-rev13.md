<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# RESTART — ariellas · glpnet · 2026-09-04 · rev13

**Supersedes rev12 (same day, 08:00Z).** Resume with: **`resume marathon`**

```
HOST     ARIELLAS 192.168.0.142   LANE  glpnet   REPO  D:/BSTDEV/research/glp/GLPNET
BRANCH   develop (pushed, clean) @ d6aaa9b8
MARATHON mrun-f5ef56dba3c1  feature glpnet-full-completion-programme
         seq 382 · steps 50/135 (was 42) · outstanding 167 · next W18 (ESCALATED, but see §3)
ROADMAP  21 epics · 124 features · 30 NOT-CLOSED · export 20260904T153315Z (round 71)
BOARD    \\192.168.0.108\GAVRI_D\coop\glpnet\sched  — 25 op-logs, ZERO term ops
```

🔴 **`--feature` is MANDATORY.** A bare `resume` reads `.specify/feature.json` (→ `specs/085-…`) and
falsely prints *"no active marathon run"*.

## 1 · THE ONE-LINER

```
buildkit-marathon resume --feature glpnet-full-completion-programme
```

---

## 2 · 🔴 THE ONE LESSON THAT CHANGED THIS SESSION — READ IT BEFORE ANYTHING ELSE

**Three marathon steps recorded as blocked or contested were not blocked at all. The branches had
landed underneath them.** One command settles it:

```
git merge-base --is-ancestor origin/<branch> origin/develop
```

- **W11** was recorded **BLOCKED ON AN ENGINEER RULING (J2, §1.14, reserved to Udi) for eleven
  days.** `origin/080-occurs-checked-substitution` is an **ancestor of develop**. There was no merge
  and no conflict, so the ruling never gated the step.
- **W12** (choose the 067 vs 067b survivor) was **moot** — both are ancestors of develop.
- **W13/W22** (land 051, resolve "open" draft PR 111) — **PR 111 is `MERGED`** and there are **zero
  open PRs** on this repo.

🔴 **Before escalating any merge/consolidation step to an engineer ruling, run the containment test.**
The roadmap said `specified`, the marathon said `blocked`, and git said `landed`. **Three systems,
three answers, and only one of them was measured.**

---

## 3 · WHAT THIS SESSION DID (2026-09-04, session 19)

| # | outcome |
|---|---|
| 1 | **Marathon 42/135 → 50/135.** W11, W12, W13, W14, W15, W16, W17, W22 all discharged **by measurement**, each recorded as a durable trace |
| 2 | **W18's eleven-day contradiction RESOLVED by measurement** (§4) — N12 and C1 are *both* partly wrong |
| 3 | 🔴 **A preservation gap found and closed** — 2 commits existed in **no** origin ref and **no** archive tag (§5) |
| 4 | **Pulled 39 commits** of peer work incl. the QUIC fallback chain and four engineer rulings |
| 5 | **Mandatory ACK published** to git *and* the shared coop board — stop order **HELD** |
| 6 | Roadmap reconcile → dedupe → export round 71; BK-STD-1 not-closed table (**30**) |
| 7 | **Six landed-but-not-advanced roadmap features measured** — W23's input, pre-computed |

Commits: `7aa465d0` (ACK), `d6aaa9b8` (roadmap round 71). Both pushed.

---

## 4 · W18 — THE GLEAM ESCALATION IS NOW A CHERRY-PICK

Measured `develop` vs `origin/059` over the `.gleam` trees:

```
059 = 189 files · develop = 178 · shared paths = 148
   of the 148 shared:  104 BYTE-IDENTICAL   44 differing
   only on 059: 41      only on develop: 30
```

- **N12** (*independent colliding implementations*) is **too pessimistic** — 104 identical files
  prove common ancestry; only **44 of 248** changed paths collide.
- **C1** (*complementary tiers*) is **too optimistic** — the 44-file collision is real and sits in
  the shared core (`link/primitives` 9, `engine` 5+4, `link/reliability` 4, `analysis` 4+3+1,
  `repl` 3+2, `compiler` 2+1).
- **Truth: ONE LINEAGE THAT FORKED.** 059's unique 41 are overwhelmingly **additive link-layer
  tests** (`test/link/reliability` 8, `test/link/primitives` 8, `test/engine` 7) plus a `mad`
  module develop lacks. develop's unique 30 include `ring`, `split`, `contract` — which 059 never had.

**The question shrinks from "merge or abandon 248 files" to "cherry-pick 41 additive files and rule
on a 44-file core collision".** develop is **1194 commits ahead** of 059, so develop's core wins by
default — leaving a decision surface of roughly **41 mostly-test files**.

---

## 5 · 🔴 PRESERVATION — WHAT ALMOST WENT, AND WHAT SAVED IT

Commits `57fa2066` + `fd305b5a` on the **second clone's** local `main`
(`D:\BSTDEV\glp\GLPNET`) were reachable from **no origin ref and no archive tag**. The workplan
schedules that clone for retirement (W21). Closed **both** ways:

1. verified `git bundle` → `…/deploy-home/targets/fb9d55f94f8b/preservation/glpnet-secondclone-main-20260904.bundle`
   (*"the bundle records a complete history"*)
2. annotated tag **`archive/secondclone-main-20260904` PUSHED TO ORIGIN** = `1865b2f7`

**A near-miss I caused, disclosed:** I ran `git fetch --prune` in the second clone, deleting its
remote-tracking refs for five branches already gone from origin. **Nothing was lost only because
W04's 20 `archive/*` tags are on ORIGIN, not merely local.** A SHA list would have lost five branches.

---

## 6 · CURRENT GIT REALITY — THE 129-REF PREMISE IS STALE BY ~120

origin carries **15 heads**.

| status | refs |
|---|---|
| **CONTAINED in develop** (deletable, 9) | 065 · 066 · 067 · 067b · 078 · 080 · 082 · 099 · 101-gleam-capability-delivery |
| **NOT contained — do NOT delete** (4) | `059` (W18) · `083-glptutorial-corpus-goldens` (3 ahead) · `101-goal-term-acceptance` (6 ahead) · `102-quic-federation-transport` (4 ahead) |

**Nothing was deleted.** Ref-deletion ownership is unruled (`Q-GLPNETA18-02`) and now blocks
**W19 → W20 → W21 → W23**, all of whose measurement work is complete.

---

## 7 · HOST FACTS FOR ARIELLAS (measured this session)

```
IPv4        192.168.0.142  (Ethernet /24)     — matches gavriella's fleet probe
SAC         VerifiedAndReputablePolicyState = 0   (Smart App Control OFF)
            CodeIntegrityPolicyEnforcementStatus = 2   (policy ENFORCING)
            -> UNDETERMINED for unsigned C# daemon hosting. NOT green. Load test not run.
shares      ariellas_D -> D:\ , C_DRIVE -> C:\    (NO self-loopback on this host)
mounts      G: \\192.168.0.129\Olamnit_D    H: \\192.168.0.108\GAVRI_D
            I: \\192.168.0.108\GAVRI_D      J: \\192.168.0.170\Shiras_Share
```

🔴 **`H:` and `I:` are the SAME UNC** → drive-letter peer enumeration counts **GAVRIELLA twice from
ARIELLAS**. This is *not* the same defect as gavriella's own `I:` loopback; **fixing theirs does not
fix this one.**

🔴 **My Olamnit mount uses `.129`; gavriella's probe found Olamnit on `.136` AND `.129`.** Two peer
tables, both correct, disagreeing — independent corroboration that peer/pin tables must be keyed by
**Ed25519 `nodeId = SHA-256(SPKI)`, never by address**.

---

## 8 · WHAT'S NEXT, IN ORDER

1. **W18** — the cherry-pick in §4. Needs the engineer to rule only on the 44-file core collision.
2. **W19 → W20 → W21 → W23** — measurement complete, all four **gated on the ref-deletion ownership
   ruling** (`Q-GLPNETA18-02`).
3. **W24/W25** — codexreview + ship of `084-host-tidy-up-and-merge-closure`, then the takt projection.
4. **Federation UDP port** — I asked gavriella to publish it; I will not guess a firewall port.
   On receipt: one inbound UDP rule, Private + `192.168.0.0/24` only, then ACK with the rule name.
5. **J2 / `Q-GLPNETA19-01`** — the §1.14 occurs-check *semantic* question is **still open and still
   reserved to Udi**. It no longer blocks W11, and I have **not** claimed it answered.

---

## 9 · REBOOT

`BK-OnRestart` fires **mstack's** launcher, not glpnet's copy:

```
pwsh -File "D:\BSTDEV\tools\mstack\scripts\fleet\post-reboot-restart.ps1" -WaitForMounts -Layout Tabs
```

Verify a fix **only** with `-DryRun -WaitForMounts -AllowUnconfirmedResume` — a plain `-DryRun`
omits `-WaitForMounts` and never exercises the path that failed on 2026-08-28 (`LastTaskResult=6`,
zero lanes launched). **The argument set is part of the failing condition.**

Lanes: `ospark · tefl · ulpanit(hatzinor) · olamnit · buildkit · qhstate · crucible · glpnet ·
lejepa · mstack · yngraw · yngwin · ynglin · yngapp · yngcor`.
⚠ **Never register a yngenios lane without `-Name`** — the leaf default collides and silently drops a lane.

---

**rev13 · `ariellas.glpnet` · 2026-09-04T17:00Z · resume with `resume marathon`**
