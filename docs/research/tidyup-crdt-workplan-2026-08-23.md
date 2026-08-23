# Tidy-up CRDT workplan — 2026-08-23 (Y-series)

**Marathon**: `mrun-20d9230f767b` · **Feature**: `078-verification-receipts` · **Host**: GAVRIELLA
**Item**: `mitem-01a01997-808b-76b5-a5df-e9d6b5de6444` (consolidated verified CRDT plan)
**Sizing**: `default` — nano=1 · micro=3 · mini=7 · midi=11 · maxi=17 · saga=35

> **Why this file exists.** Marathon steps are the **state machine**; a step's *content* lives only
> in its name, and `expand --steps` is **comma-delimited with no escaping** and **grow-only with no
> void verb**. (buildkit PR #618, merged today, now *documents* the comma rule and refuses a `|`
> — but the delimiter and the grow-only property are unchanged.) This file is the **authoritative
> content**. **Where a step name and this file disagree, this file wins.** Step names below are
> deliberately comma-free.

Supersedes the **X-series** in `tidyup-crdt-workplan-2026-08-22.md` for branch state only; that
file's X07/X10/X11/X12 engineer items and its binding safety rules remain in force unchanged.
Numbered **Y** precisely so it cannot collide with the X-series on a grow-only board.

---

## How this was measured

`git fetch --prune` at 2026-08-23T15:3xZ, then for every origin head:
`git merge-tree --write-tree origin/develop origin/<branch>` — a **non-destructive** probe that
computes the merge without touching the working tree or any ref. Conflict counts below are
**measured, not estimated**. `filesdiff` is `git diff --name-only origin/develop..origin/<branch>`
and counts **both** directions, so a large number means "the branch is old", not "the branch is big".

**Baseline after this session's pushes:** 20 origin heads (down from 21 — `chore/tidy-up-branches-
worktrees-20260822-olamnit` was merged as PR #209 and auto-deleted, proving `delete_branch_on_merge`
works). **16 unmerged into `origin/develop`.** `origin/develop` is 21 ahead of `origin/main`.
Working tree clean. **Linked worktrees: 0.**

**Merge gate re-verified this session: `Total 561 | Passed 559 | Failed 2 | Skipped 0 |
Unsearchable 0`** — the 2 are the known-real 064 Section T drills. **Zero regression.**

---

## Ledger

| ID | Item | Merge probe | Size | Pts | Owner |
|---|---|---|---|---:|---|
| **Y01** | Merge `083-glptutorial-corpus-goldens` | **CLEAN** · 2 commits · 39 files | micro | 3 | agent |
| **Y02** | Merge `085-onrestart-fleet-resume` | **CLEAN** · 9 commits · 23 files | micro | 3 | **PEER — live** |
| **Y03** | Merge `078-verification-receipts` | **CLEAN** · 1 commit · 192 files | micro | 3 | agent |
| **Y04** | Merge `080-occurs-checked-substitution` | 1 conflict (`.specify/feature.json` modify/delete) | mini | 7 | agent |
| **Y05** | Merge `066-wave6-consolidation` | 4 conflicts · 23 ahead | maxi | 17 | agent (review) |
| **Y06** | `067-qr-link-provisioning` | 3 conflicts · 26 ahead | midi | 11 | **ENGINEER-ruled → graduate** |
| **Y07** | `067b-qr-link-continuation` | 4 conflicts · 27 ahead | midi | 11 | follows Y06 |
| **Y08** | `051-ynet-transport` | 2 conflicts · 26 ahead | midi | 11 | **ENGINEER — unruled** |
| **Y09** | `050` vs `059` survivor ruling | 21 and 30 conflicts | midi | 11 | **ENGINEER (X10)** |
| **Y10** | `030-phase8-polish` + `backup/030-phase8-polish` | 8 and 9 conflicts · distinct SHAs | mini | 7 | agent |
| **Y11** | `backup/078-olamnit-impl-preserve` | 5 conflicts · preserve-only | micro | 3 | agent |
| **Y12** | `backup/upgrade/buildkit-migration-20260627T220138Z` | rename/rename · 2431 files | micro | 3 | agent |
| **Y13** | `016-codeconv-init-scaffold-langpair` + `017-conversion-plan-agents` | 3786 and 4007 file conflicts | mini | 7 | agent |
| **Y14** | Class-C2 remote cleanup of whatever ends CONTAINED | — | mini | 7 | agent |
| **Y15** | Author `.claude/skills/bk-flow/SKILL.md` | — | mini | 7 | agent |
| **Y16** | `era` metric in marathon — opens at `/bk-specify`, closes at `/bk-close` after ship | — | midi | 11 | agent |
| **Y17** | Unique allocation: one feature → one repo → one host, across all boards | — | maxi | 17 | agent |
| **Y18** | Takt-only duration rule — generic range or measured size-adjusted; refuse invented numbers | — | midi | 11 | agent |

**Total 150 pts.** Engineer-gated: **Y06 · Y08 · Y09 = 33 pts.** Peer-owned: **Y02 = 3 pts.**
Agent-executable: **114 pts.**

### 🔴 150 points does not fit in one session — stated, not hidden

The directive asks that the tidy-up branch be "fully complete and fully merged" by the end of this
marathon session. **On measured evidence that is not achievable**, and saying otherwise would be
exactly the false-green this feature exists to eliminate. Fourteen of the sixteen unmerged branches
carry real merge conflicts, four of them carry 8–30 conflicts each, and three are behind engineer
rulings that have been open for days. The honest plan is the ordering below: bank the three CLEAN
merges now, take the single-conflict one next, and leave the conflict-heavy and engineer-gated ones
sequenced and durable for the next session. That is what the Y-series is ordered for.

---

## Execution order (dependency-respecting)

1. **Y01 → Y03 → Y04** — the cheap, safe, verifiable wins. Merge gate re-run after each.
2. **Y11 → Y12 → Y13 → Y10** — the stale/backup lines. Each is class **C2**: archive-tag, verify the
   tag is on `origin` **and** equals the branch tip *at delete time*, then delete.
3. **Y15** — unblocks bk-flow adoption; additive and safe; no shared-registry mutation.
4. **Y05** — 066 is a review, not a tidy-up. Budget it as such.
5. **Y16 → Y18 → Y17** — the scheduler/marathon hardening trio.
6. **Y14** — last, because it can only act on what the earlier steps made CONTAINED.
7. **Y06 · Y07 · Y08 · Y09** — blocked on engineer rulings; do not start on assumption.

## Notes that must not be re-derived wrongly

- **`030-phase8-polish` and `backup/030-phase8-polish` are NOT the same commit** —
  `ebc9da07` vs `363fba46`. The backup is 1 commit ahead. Treating them as duplicates and dropping
  one loses a commit.
- **`051-ynet-transport` is newly surfaced.** 26 commits ahead, untouched since 2026-07-16, and it
  appears in **no** prior ledger (not X01–X17, not the 08-20 plan, not olamnit's tidy-up). It has
  never been triaged. Only 2 conflicts — cheaper than its age suggests.
- **`085-onrestart-fleet-resume` is another lane's live branch.** It moved three times during this
  session (`afeaec1e → 122dcd04 → bf9bc71d`). It merges CLEAN today, but merging a branch someone
  else is actively pushing to is a coordination act, not a tidy-up. **Leave to the owning lane.**
- **The 078 branch commit is `315e3be5`** — *"add tidy-up survey evidence manifest — 4 pairwise-
  disjoint slices"*. It is evidence, not implementation; merging it does not advance or disturb the
  078 pipeline stage.
- **`016` and `017` conflict on 3786 / 4007 files.** That is not a merge, it is an archaeology
  project. Treat as archive-and-drop candidates unless a ruling says otherwise.

## Binding safety rules — carried forward from 08-20/08-22, still in force

1. **No deletion may claim a reflog recovery window** — every delete is class **C2**.
2. **An archive tag is preservation only when verified**: tag-commit == branch tip **at delete
   time**, and the tag is on `origin`.
3. **A git bundle is NEVER content preservation** — it packs reachable objects, not untracked bytes.
4. **The merge gate is local only.** CI exists (5 CodeQL jobs) but **no CI runs
   `test/run_all_tests.sh`**. Re-run it after every merge.
5. **Never quote a branch count, an ahead-count or a board number without naming the ref or root.**

---

*Authoritative content for the `synthesise-verified-consolidated-crdt-plan` step of marathon
`mrun-20d9230f767b`. Update this file, then reflect state in the marathon; never the reverse.*
