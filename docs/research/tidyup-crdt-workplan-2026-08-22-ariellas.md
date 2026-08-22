<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->
# Tidy-up + completion CRDT workplan — **ariellas lane** — 2026-08-22

**Marathon**: `mrun-f5ef56dba3c1` · **Feature**: `glpnet-full-completion-programme` · **Host**: `Ariellas`
**Sizing**: `nano 1 · micro 3 · mini 7 · midi 11 · maxi 17 · saga 35` · hard cap `mini` per step
**Phases**: `analyze | implement | codexreview-ship | close`

> **Why this file exists.** Marathon steps are durable in the catalog, but a step's *content*
> lives only in its name, and `expand --steps` is comma-delimited with no escaping. This file is
> the **authoritative content**; marathon steps are the **state machine**. Where a step name and
> this file disagree, **this file wins**. Step names below are deliberately comma-free.

## Lane split — this file does NOT supersede the peer's

There are two live tidy-up marathons on this repo and they are now **explicitly disjoint**:

| Lane | Marathon | Owns |
|---|---|---|
| **GAVRIELLA** | `mrun-20d9230f767b` (`docs/research/tidyup-crdt-workplan-2026-08-22.md`, X01–X14) | `067` key rotation + merge · `066` · the `050`/`059` survivor ruling · tag→GitHub-Release backfill · `078` |
| **ariellas** (this file) | `mrun-f5ef56dba3c1`, A01–A21 | **host-local `Ariellas` git residue** (both D: clones) · the **scheduler board** for glpnet · **roadmap sync** · **feature `082-feature-stream-superset`** end-to-end |

Declared to the peers in `I:\coop\20260822T162201Z-ariellas-glpnet-ACK-x4-…md` §7. The
**2026-08-20** plan (`W01–W25`) is superseded by **both** files; do not resume from it.

---

## 🔴 Correction owed to the peer's authoritative doc

The peer's file lists under *"Facts that must not be re-derived wrongly"*:

> **glpnet has ZERO linked worktrees.** Re-verified today: `git worktree list` returns only the
> main tree and `.git/worktrees` does not exist.

**That is true of clone 1 only. Measured on `Ariellas` 2026-08-22, fleet-wide it is false.**

```
CLONE 1  D:/BSTDEV/research/glp/GLPNET
         git worktree list -> 1 entry (the main tree). .git/worktrees ABSENT.   <- peer is right here

CLONE 2  D:/BSTDEV/glp/GLPNET
         git worktree list -> 2 entries:
           D:/BSTDEV/glp/GLPNET                              d45c40fa [058-s4-policy-service]
           D:/BSTDEV/glp/GLPNET.worktrees/051-ynet-transport b3b6c2bf [051-ynet-transport]
         worktree tree is CLEAN (git status --porcelain empty)
         b3b6c2bf is reachable from archive/051-ynet-transport-20260820, and that tag IS on
         origin (ls-remote: a5117d67…^{} -> b3b6c2bf). So it is PRESERVED-ON-ORIGIN.
```

The peer's *safety* conclusion still holds — this worktree must **not** be deleted as glpnet
cleanup, because it is a live checkout of a preserved branch in another lane's tree. Only the
*count* was wrong, and the count is what a later session would act on.

---

## 🔴 A preservation gap the 2026-08-20 sweep MISSED — found and closed today

The 08-20 restart pointer asserts *"Nothing in this repo can now be deleted unrecoverably"*.
**That was not true.** Measured this morning:

```
git rev-list --count 064-durable-walfix --not --remotes --tags   ->  1
```

`d0187c9f` — *"fix(064): replay merges both WAL backends; drill states SC-002 and coverage
verdicts"*, vonwenm, 2026-08-06 — was reachable from **no remote ref and no tag**. Its origin
branch (`origin/064-durable-listener-service-box`) is gone and the 08-20 sweep cut **18** archive
tags, none of them for `064-durable-walfix`. One `git branch -D` would have destroyed it, exactly
as `3dca578c` was already destroyed in this repo.

**Closed 2026-08-22** (A01): annotated tag `archive/064-durable-walfix-20260822` created and
**pushed to origin**, plus a verified bundle at
`D:/BSTDEV/evidence/glpnet-tidyup-20260822/bundles/064-durable-walfix-20260822.bundle`
(`git bundle verify` → *okay*, *records a complete history*, tip `d0187c9f`). Re-measured after:
`--not --remotes --tags` → **0**.

**Lesson, and it generalises**: a preservation sweep driven from *origin's* ref list cannot see a
branch that exists only locally on one host. **Every host must run the
`rev-list --not --remotes --tags` check on its own clones.** The peer's X01 caught the same class
on `078` (`315e3be5`); this is the second instance in three days.

---

## Measured inventory — host `Ariellas`, 2026-08-22

### Clone 1 — `D:/BSTDEV/research/glp/GLPNET` (this repo, `develop`)

- **0 linked worktrees.**
- **44 local heads.** **39 are ancestors of `develop`** — containment *is* preservation, so all 39
  are safe to delete (`nano`) once the lane-ownership block clears. They include 14 landed feature
  branches and 14 `release/v*` branches.
- **5 heads ahead of `develop`:**

| Local branch | ahead | unpreserved commits | status |
|---|---:|---:|---|
| `064-durable-walfix` | 1 | **1 → 0** | 🔴 **was at risk — preserved today (A01)** |
| `upgrade/buildkit-migration-20260627T220138Z` | 1 | 0 | preserved (`archive/backup__upgrade__…-20260820`) |
| `030-phase8-polish` | 9 | 0 | preserved (`archive/030-phase8-polish-20260820`, on origin) |
| `067b-qr-link-continuation` | 27 | 0 | preserved (origin ref + archive tag) |
| `050-full-gleam-combined` | 48 | 0 | preserved (origin ref + archive tag) |

### Clone 2 — `D:/BSTDEV/glp/GLPNET`

- 6 heads, **every one `unpreserved = 0`** after `fetch --prune`. `main` 2 ahead of
  `origin/develop` (the two commits bundled on 08-21). `058-s4-policy-service` tip `d45c40fa`
  survives its origin-branch deletion via the archive tag.
- **1 linked worktree** — see the correction above.

### Roadmap (round 31, this session)

`reconcile` in sync · `import` 1 file / **0 new lines** (converged) · `dedupe` **0 duplicate
groups in 115 live** · `export` **20 epics / 116 features / 3760 journal lines**, both publish
legs OK. **23 of 116 features not closed**; **9 of those carry no epic** and are therefore
invisible to `buildkit-roadmap status` — always fold `heads` from the export instead.

### Scheduler board `I:/coop/glpnet/sched`

32 WPs. `ariellas` onboarded 840 h / 35 d × 3×8 h (calendar 113 rows, horizon `2026-09-26`).
3 stuck `ariellas` claims cleared. `082` converted to `in-progress`.

---

## Ledger

| ID | Step | Phase | Size | Pts | State | Evidence / blocker |
|---|---|---|---|---|---|---|
| A01 | Preserve the unpreserved `064-durable-walfix` commit | analyze | nano | 1 | ✅ DONE | tag on origin + verified bundle; re-measure `--not --remotes --tags` → 0 |
| A02 | Re-measure preservation of every clone-1 head | analyze | nano | 1 | ✅ DONE | 39 contained · 5 ahead · 1 gap found (A01) |
| A03 | Audit clone 2 and its linked worktree | analyze | micro | 3 | ✅ DONE | 6 heads all preserved · `051` worktree clean and origin-preserved |
| A04 | Onboard `ariellas` on the glpnet board | analyze | nano | 1 | ✅ DONE | 113 calendar rows · horizon re-anchored to `2026-09-26` |
| A05 | Unstick the three `ariellas` claimed-never-ready WPs | analyze | micro | 3 | ✅ DONE | ops `ariellas:000040/41/42` |
| A06 | Roadmap round 31 both publish legs | analyze | micro | 3 | ✅ DONE | 20/116/3760 · 0 dup groups |
| A07 | Coop ACK ×4 with a scanned-paths + cursor receipt | analyze | micro | 3 | ✅ DONE | `I:\coop\20260822T162201Z-ariellas-…md` |
| A08 | 3rtask adjudicated disposition table for every ref and worktree | analyze | mini | 7 | ▶ RUNNING | run `20260822T170003Z-fa65` · codex Critic · 3 blind builders |
| A09 | Post the clone-2 worktree correction and the A01 preservation gap to the peers | analyze | nano | 1 | ▶ NEXT | owed — both found AFTER the A07 ACK went out |
| A10 | Close `076-type-checker-body-atom-moding` — roadmap says `released` not `closed` | close | mini | 7 | ▶ NEXT | merged into `develop`; 0 open tasks; no retrospective dir |
| A11 | Delete the 39 contained clone-1 local heads | close | micro | 3 | 🔴 ENGINEER | lane-ownership block 3 — safety is settled, ownership is not |
| A12 | `082` `/bk-clarify` — fold in defects D1 and D2 | analyze | mini | 7 | ⏭ QUEUED | D1/D2 ownership accepted in the A07 ACK §4 |
| A13 | `082` `/bk-plan` | analyze | mini | 7 | ⏭ QUEUED | |
| A14 | `082` `/bk-tasks` | analyze | mini | 7 | ⏭ QUEUED | |
| A15 | `082` `/bk-analyze` | analyze | mini | 7 | ⏭ QUEUED | |
| A16 | `082` `/bk-implement` | implement | maxi | 17 | ⏭ QUEUED | exceeds the `mini` per-step cap — must be split at `/bk-tasks` |
| A17 | `082` `/bk-codexreview` | codexreview-ship | midi | 11 | ⏭ QUEUED | |
| A18 | `082` `/bk-ship` | codexreview-ship | mini | 7 | ⏭ QUEUED | |
| A19 | `082` `/bk-close` | close | micro | 3 | ⏭ QUEUED | |
| A20 | Trust-material controlled reproduction on this clean control host | analyze | midi | 11 | ⏭ QUEUED | WP now `ready`; reproduces the peers' 65-refusal import gate |
| A21 | Emit per-phase takt metrics into the marathon and prep `/bk-flow` | close | mini | 7 | ⏭ QUEUED | targets: phase 30 min – 3 h · feature 1.5 – 6 h |

**Delivered: 12 pts** (A01–A07) · **Remaining: 106 pts**, of which **3 are an engineer ruling**
(A11), **7 in flight** (A08) and **96 agent-executable**.

## Binding safety rules (inherited, still in force)

1. **No deletion may claim a reflog recovery window** — 54 of 77 per-branch reflogs measured
   zero-byte on 08-20. Every delete is class **C2**: preserve → verify the preservation → delete.
2. **An archive tag is preservation only when it is verified AND on origin**, and only when the
   tag commit equals the branch tip **at delete time**.
3. **A git bundle is never *content* preservation** — it packs reachable objects, not untracked
   bytes.
4. **The merge gate is local only.** CI runs 5 CodeQL `Analyze` jobs and **no CI runs
   `test/run_all_tests.sh`**. Re-verify the suite locally after every merge, and treat an absent
   `Total: … Passed: … Failed: …` summary block as *the run did not finish*, whatever the exit
   code says.
5. **`ready` on the board means executable now.** A gated WP goes to `escalated`, never `ready`.

---

*Authoritative content for marathon `mrun-f5ef56dba3c1`, ariellas lane. Update this file, then
reflect state in the marathon; never the reverse.*
