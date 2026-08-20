# Tidy-up CRDT workplan — canonical, durable

**Marathon**: `mrun-20d9230f767b` · **Parent item**: `mitem-01a01997-808b-76b5-a5df-e9d6b5de6444`
**Source survey**: 3rtask run `20260820T072729Z-1de6` — 4 blind builders / 4 pairwise-disjoint
slices / **0 independence violations** / 220 merge rows / Critic 184 CONFIRM · 24 REFUTE · 12 ESCALATE
**Sizing scheme**: `default` (active) — nano=1 · micro=3 · mini=7 · midi=11 · maxi=17 · saga=35

> **Why this file exists.** Marathon steps are durable in the catalog, but a step's *content*
> lives only in its name, and `expand --steps` is comma-delimited with no escaping — which
> truncated two steps and merged two more. This file is the **authoritative content** of the
> workplan; marathon steps are the **state machine**. Where a step name and this file disagree,
> **this file wins**. Both are git-/catalog-durable; neither alone is reliable.

---

## Ledger

| ID | Step | Size | Pts | State | Evidence / blocker |
|---|---|---|---|---|---|
| W01 | `067` private-key rotation ruling | maxi | 17 | 🔴 ENGINEER | archive tag would republish `glpquick.key/.pfx/.pem` reachable at `bc5ea232` |
| W02 | Sync local `develop` → `origin/develop` | nano | 1 | ✅ DONE | `39e886ec`; `rev-list develop...origin/develop` = `0 0` |
| W03 | Merge `082-feature-stream-superset` | nano | 1 | ✅ NO-OP | tip `f5be473a` **is an ancestor** of `origin/develop` — already merged |
| W04 | Merge `065-ynet-consolidation` | micro | 3 | ✅ DONE | `7cca2ae4`; spec-only; landed via PR #186 |
| W05 | Merge `066-wave6-consolidation` | maxi | 17 | ⏸ HELD | 223 303 insertions + CLAUDE.md content conflict — a review, not a tidy-up |
| W06 | Merge `067-qr-link-provisioning` | midi | 11 | 🔴 BLOCKED | gated on W01 |
| W07b | Ship-or-abandon ruling: `049`/`050`/`058`/`059` | maxi | 17 | 🔴 ENGINEER | post-PR commits never proposed in any PR; `059` = 32 commits / 248 files, 6 paths fire TOUCHES-GATE |
| W08 | Recover 5 closed-unmerged roadmap-sync branches | mini | 7 | ✅ DONE | PRs #163/#165/#166/#167/#168 (1/1/1/1/10 ahead) — all 5 now prove `CONTAINED` |
| W09 | Branch protection + `delete_branch_on_merge=true` | micro | 3 | ⛔ BLOCKED | `gh api -X PATCH` refused by permission classifier; **this is the root-cause fix** |
| W10 | Delete provably-contained local branches | mini | 7 | ✅ DONE | **78 → 8 heads**; 70 tip SHAs preserved in `docs/handover/tidyup-deleted-branch-tips-20260820.txt` (`861ba3c5`) **before** deletion |
| W11 | Cut release `develop`→`main`; backfill 56 tags | midi | 11 | 🔴 ENGINEER | `origin/develop` 51 ahead of `main`; 56 of 62 tags have no GitHub Release |
| W12c | Takt metrics contract + `/bk-flow` readiness | maxi | 17 | ✅ DONE | `d9a62b79` → `docs/research/takt-metrics-and-bkflow-migration-2026-08-20.md` |
| W13 | Delete provably-contained **remote** branches | mini | 7 | ⛔ BLOCKED | enumeration + `git push --delete` refused by permission classifier |
| W14 | Roadmap sync round 27 | micro | 3 | ✅ DONE | reconcile + import + dedupe (115 scanned, 0 dup groups) + export; **both publish legs OK** |

**Delivered: 39 pts** (W02·W03·W04·W08·W10·W12c·W14) — **Remaining: 97 pts**
Of the remainder, **62 pts (W01·W07b·W11)** are engineer rulings and **10 pts (W09·W13)** are
permission-blocked. Only **28 pts (W05·W06)** are agent-executable, and W06 is gated on W01.

---

## Binding safety rules (from the frozen method, non-negotiable)

1. **No deletion may claim a reflog recovery window.** 54 of 77 per-branch reflogs are zero-byte
   and retention config is unobservable. Every delete is class **C2**: preserve first, verify the
   preservation, then delete.
2. **A git bundle is NEVER content preservation.** It packs only reachable objects — untracked and
   uncommitted bytes are not in it. For any dirty or non-git target, preservation is a filesystem
   archive **including untracked files** plus a per-file `sha256` manifest, stored outside the
   deletion target and verified by extract-and-rehash **before** the destructive act.
3. **`git merge-tree` clean is textual only.** It is necessary, never sufficient — it cannot see
   semantic conflicts or modify/delete hazards. Never read it as "safe to merge".
4. **The merge gate is local only.** No CI runs `test/run_all_tests.sh`; the remote enforces
   nothing. Measured baseline **2026-08-20: Total 561 | Passed 559 | Failed 2** (the 2 are the
   known 064 Section T drills). Re-verify after every merge.
5. **Never quote an ahead-count without naming the ref measured.** local `develop` 15 ahead of
   local `main`; `origin/develop` was 29 ahead of `origin/main` — reconciled exactly by the
   14-commit local staleness (15 + 14 = 29). Post-tidy-up: **51**.

## Facts that must not be re-derived wrongly

- **glpnet has ZERO linked worktrees.** `.git/worktrees` does not exist. Every `wt-*` / `bk-wt`
  path on D: belongs to **`D:/BSTDEV/research/buildkit`**. Deleting them as glpnet cleanup would
  destroy buildkit worktrees. Their disposition is **out of scope and unanswered**.
- **Only 3 branches carry unmerged work**: `065` (+1, now merged), `066` (+23), `067` (+26).
  `082` was already contained — the survey's "4" was measured against a stale local `develop`.
- **Neither `main` nor `develop` is protected**, and `delete_branch_on_merge` is false. This is
  the mechanical cause of the 145-branch accumulation (W09).
- **`GLPNET-016` loss is UNVERIFIED** — its remaining subtree is permission-denied, so the
  0-file count is a **lower bound**, never proof of emptiness.

## Known board-integrity defect (affects any takt ratio)

`expand --steps` splits only on commas, with no escaping and no warning. Two step texts split on
internal commas; a `;`-separated retry merged W07b and W12b into one step. With 2 probe steps,
**seven malformed steps are permanently on the board** (steps are grow-only; there is no delete
verb). The board total is inflated **13 → 31**, so **`steps done / steps total` is wrong by
construction** and must never be used as a takt or progress signal. Count delivered **points**
from this ledger instead.

---

*Authoritative content for marathon `mrun-20d9230f767b`. Update this file, then reflect state in
the marathon; never the reverse.*
