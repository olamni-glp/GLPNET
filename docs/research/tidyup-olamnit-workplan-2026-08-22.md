<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# Olamnit tidy-up CRDT workplan — 2026-08-22

**Marathon**: `mrun-76da6e46bd44` · **Feature**: `tidy-up-branches-worktrees-olamnit` · **Host**: OLAMNIT
**Store**: `deploy-home/targets/fb9d55f94f8b` (this repo = `D:\BSTDEV\research\glp\GLPNET`)
**Sizing**: `default` — nano=1 · micro=3 · mini=7 · midi=11 · maxi=17 · saga=35

> **Why this file exists.** Marathon steps are durable in the catalog, but a step's *content* lives
> only in its name, and `expand --steps` is comma-delimited with no escaping. This file is the
> **authoritative content**; marathon backlog items are the **state machine**. Where a step name
> and this file disagree, **this file wins**. This is Olamnit's own lane — it does **not** touch or
> fork GAVRIELLA's `078` marathon (`mrun-20d9230f767b`, store absent here).

---

## Survey (primary-source git, post-`fetch`, 2026-08-22)

- **Worktrees: ZERO linked** (`git worktree list` → only the main tree). No `wt-*`/`bk-wt*` path on
  `D:` belongs to glpnet — every one resolves to `D:/BSTDEV/research/buildkit/.git` or
  `D:/BSTDEV/research/.git`. **Deleting them destroys another repo's worktrees — DO NOT.**
- **43 local branches are MERGED into `origin/develop`** (0-ahead, tip is an ancestor). Preservation
  under safety-rule C2 is therefore *intrinsic*: the tip is recoverable from `develop` (features/
  chores) or from a version tag (releases). Most are **local-only** (no `origin` head) → pure local
  hygiene, zero remote impact.
- **6 branches are UNMERGED — KEEP/PARK, do not delete**:
  `050-full-gleam-combined` (10 ahead) and `059-full-scope-gleam-glp-implementation` (32 ahead) are
  **complementary subsystems** (059 = compiler/type-checker/bytecode + Lean proof; 050 = QUIC
  transport + link lifecycle) — the survivor question is engineer ruling X10, unresolved;
  `030-phase8-polish` (9 ahead, carries a parked buildkit→bk migration stash);
  `078-olamnit-impl-preserve` (5 ahead, intentional preservation of the local 078 draft);
  `080-occurs-checked-substitution` (2 ahead, §1.14 FR-002 Udi-blocked);
  `upgrade/buildkit-migration-20260627T220138Z` (1 ahead, obsolete-candidate — verify the 1 commit).
- Local `078-verification-receipts` is **stale/merged** (0-ahead of develop), distinct from
  GAVRIELLA's active `origin/078` (84-ahead). **Do not push the local branch.**

---

## Ledger

| ID | Step | Size | Pts | State | Notes |
|---|---|---|---|---|---|
| T01 | Survey branches + worktrees (primary-source git) | nano | 1 | ✅ DONE | this file |
| T02 | Verify preservation of all merged-delete candidates (ancestor-of-develop / tag) | micro | 3 | ▶ NEXT | C2 gate before any delete |
| T03 | Delete 16 merged feature branches (local-only) | mini | 7 | ⛔ CLASSIFIER | needs engineer `!`-run or bypass |
| T04 | Delete 11 merged chore branches (local-only) | micro | 3 | ⛔ CLASSIFIER | |
| T05 | Delete 13 merged `release/*` branches (tags preserve) | micro | 3 | ⛔ CLASSIFIER | |
| T06 | Delete 3 merged misc (`docs/079…`, `fix/064…`, `roadmap-dedup-cleanup`) | nano | 1 | ⛔ CLASSIFIER | |
| T07 | Park + document the 6 unmerged keep-branches with rationale | mini | 7 | ▶ | keep, not delete |
| T08 | Verify + drop stale stashes | nano | 1 | ▶ | inspect first |
| T09 | Commit 2 untracked (bk-onrestart skill + 08-20 workplan doc) | nano | 1 | ▶ | onto this branch |
| T10 | Land this workplan doc + hygiene notes on the tidy-up branch | micro | 3 | ▶ | this branch |
| T11 | Merge tidy-up branch → develop (GitFlow) | mini | 7 | ⏸ | after T02–T10 |
| T12 | Push / create remote tidy-up branch on GitHub | micro | 3 | ⏸ | after commit |
| H01 | Scheduler onboard + poll + COOP ACK (NO 5-min escalation) | micro | 3 | ▶ | timer retired by ruling 20260816T0910Z |
| H02 | Roadmap reconcile/import/dedupe/export/sync + push | mini | 7 | ▶ | |
| H03 | Marathon sitrep + safe-restart prep + memory update | micro | 3 | ⏸ | end of session |

**Engineer-gated (parked — NOT executed unilaterally):**

| ID | Step | Size | Pts | Owner | Notes |
|---|---|---|---|---|---|
| G01 | X07 private-key rotation + history rewrite | maxi | 17 | 🔴 ENGINEER | keys PUBLIC on `origin/main`, 23/65 tags, ~44 days |
| G02 | X10 `050`-vs-`059` survivor ruling | midi | 11 | 🔴 ENGINEER | complementary, not redundant |
| G03 | X11/X12 GitHub Release backfill + `buildkit release` publish gap | mini | 7 | 🔴 ENG/perm | `gh release` classifier-blocked |

**Executable this session: T02, T07–T10, H01–H03 (≈35 pts).**
**Blocked on engineer `!`-run: T03–T06 (14 pts).** **Engineer rulings: G01–G03 (35 pts).**

---

## Binding safety rules (from the fleet tidy-up doctrine, in force)

1. **No deletion claims a reflog recovery window** — treat every delete as class **C2**:
   preserve → verify the preservation → delete. For MERGED branches the verified preservation is
   "tip is an ancestor of `origin/develop`" (checked in T02); for `release/*` it is the version tag.
2. **An archive tag is preservation only when verified** (tag-commit == branch tip at delete time,
   tag on `origin`). Only relevant if an UNMERGED keep-branch is ever chosen for deletion.
3. **A git bundle is NEVER content preservation** — it packs reachable objects, not untracked bytes.
4. **The merge gate is local only** — re-run `bash test/run_all_tests.sh` after any merge to develop.

---

*Authoritative content for marathon `mrun-76da6e46bd44`. Update this file, then reflect state in
the marathon; never the reverse.*
