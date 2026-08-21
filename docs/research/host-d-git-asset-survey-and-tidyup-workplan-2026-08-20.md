# Curator report — host D: GLPNET git-asset survey and tidy-up workplan

**Run** `20260820T115931Z-dddd` · task-type `plan` · 3 blind builders · Critic = codex (cross-provider, no independence warning) · host ARIELLAS · 2026-08-20

## 1. What was surveyed

| Asset class | Count | Source |
|---|---|---|
| origin refs | 147 (146 heads + HEAD) | S1 |
| origin refs with unmerged work | 18 (220 commits ahead of develop) | S1 |
| origin refs provably contained in develop `201b97a0` | 129 | S1 |
| local branches, active clone `D:/BSTDEV/research/glp/GLPNET` | 42 | S1 |
| **second clone** `D:/BSTDEV/glp/GLPNET` | 6 local branches, **2 unpushed commits on its local `main`** | S1 |
| linked worktree `D:/BSTDEV/glp/GLPNET.worktrees/051-ynet-transport` | 1 (clean) | S1 |
| open PRs | 1 (draft #111, 051-ynet-transport) | S3 |
| closed-unmerged PRs | 5 (#163,165,166,167,168) | S3 |

Only **two** git clones of `olamni-glp/GLPNET` exist anywhere on D:. Every other `GLPNET`/`glpnet`
directory found by the filesystem sweep is a buildkit `beacon/glpnet` demo dir or a yngenios L0
dir with no `.git` — not a clone and not in scope.

## 2. Merge result (mechanical set-ops on `(subject, tag)`)

753 non-abstaining claims → **596 distinct (subject, tag) pairs** over **327 subjects**:
**157 corroborated** (≥2 builders), 439 singletons, **16 conflicts**.

Critic adjudication over the 89 decision-relevant rows: **42 CONFIRM · 12 REFUTE · 35 ESCALATE**,
plus 16 conflict escalations.

## 3. The decisive measurement — merge-tree against current develop

Curator evidence (`curator-mergetree.txt`), not a builder slice. Only **4 of the 18** unmerged
branches merge into current `develop` without conflict:

| CLEAN | conflicting paths |
|---|---|
| `origin/083-repo-tidy-up` (2 ahead) | 0 |
| `origin/081-scheduler-supply-rootcause` (3 ahead) | 0 |
| `origin/083-glptutorial-corpus-goldens` (1 ahead) | 0 |
| `origin/049-wave1-guard-link-acceptance` (3 ahead) | 0 |

The other 14 conflict from 2 paths (`080-occurs-checked-substitution`) to **89**
(`059-full-scope-gleam-glp-implementation`) and 64 (`050-full-gleam-combined`).

## 4. Findings that changed the plan

**F1 — a SHA list preserves nothing.** The blind planning Critic refuted the draft's preservation
rule: once the last ref to a commit is deleted the objects become unreachable and `git gc` may
collect them, so a text file of tip SHAs is an index, never a preservation artifact. This was then
**empirically confirmed** by builder-1: commit `3dca578c`, which `CHANGELOG.md` records as a real
fix "stranded on `glpnet-lane/toolchain-integrity-fixes`", is **not present in this clone at all** —
`git cat-file -t 3dca578c` → *Not a valid object name*, and no such ref exists on origin. Work
recorded as merely "stranded" is in fact **already lost**. Every destructive step in the workplan is
therefore gated on a verified full-history `git bundle` **or** a pushed annotated archive tag, each
**restore-verified** before the deletion runs.

**F2 — "0 open tasks" is not "ready to merge".** `049-wave1-guard-link-acceptance` measures
`tasks_open=0` and merges CLEAN, yet builder-2 found four unchecked hard GO-CONDITIONS in its
unchanged SHIP-HANDOFF. Topology alone would have landed it.

**F3 — the peer lane already did part of this work.** `083-repo-tidy-up` (PR #186, merged into
develop 10:23Z today) recovered 5 closed-unmerged roadmap-sync branches and authored a takt-metrics
doc. Its two newest commits — the 70-tip index and the takt doc — are still unmerged. This run was
scoped to what that lane could not see: the **host-local** residue (second clone, its worktree, its
unpushed `main` commits, 42 local branches).

**F4 — `bk-flow` is now on PATH.** The peer's takt doc records "`bk-flow.exe` … absent from PATH"
as a blocking `/bk-flow` migration prerequisite. Measured today: `bk-flow` resolves and
`bk-flow --help` runs (verbs `poll · claim · open · report · version`). That prerequisite is met;
the remaining blockers are the takt projection, step-board integrity and Critic determinism.

## 5. Tool defect found (reported, not worked around)

`buildkit-3rtask merge` returned **2 corroborated of 820 claims**. Root cause, read from
`threerole/concept.py`: for `task_type` in {code, plan, strategy} `_category_in_key()` returns
False, so `concept_key` is `("", method_family, property_family)` over a fixed software vocabulary —
the claim's **`category`/`subject` is dropped from the key entirely**. A per-asset disposition method
therefore cannot corroborate through it: every claim either collapses onto a shared family key or
falls to the prose-identity floor. Re-keying cannot fix it from the caller side, because the
vocabulary labels are closed and branch names collide with them (`049-wave1-guard-link-acceptance`
matches the `gate-spec` keyword `guard`). The E07 merge was therefore computed by a deterministic
set-ops script (`e07_merge.py`, committed as a run artifact) — code, not judgment — and the CLI
shortfall is recorded here rather than silently absorbed.

**A second method defect, surfaced by the escalations:** E03 declares `needs-rebase` and
`needs-completion` mutually exclusive. They are not. A branch can be simultaneously behind develop
(topology) and carry open tasks (spec state) — indeed 10 of the 16 conflicts are exactly this pair,
raised because two disjoint slices each reported a true fact. The exclusivity family needs splitting
into an *integration* axis and a *completion* axis before the next run.

## 6. Open ESCALATEs — the ENGINEER's to resolve, not mine

1. **Gleam cluster** `050-full-gleam-combined` (48 ahead / 597 behind / 64 conflicts) and
   `059-full-scope-gleam-glp-implementation` (32 ahead / 548 behind / 89 conflicts). The marathon
   holds two **contradictory recorded reads**: item N12 says they are independent implementations of
   overlapping scope that collide, item C1 says they are complementary tiers (link/transport vs
   language). Both cannot be true. No merge until ruled.
2. **`080-occurs-checked-substitution`** — only 2 conflicting paths, but blocked on the **§1.14
   language-authority ruling that is Udi's, not Gabi's** (UnifyFail vs CompileError).
3. **The 10 needs-rebase / needs-completion pairs** — a method-taxonomy artefact (see §5), not a
   real disagreement; confirm the axis split rather than adjudicating branch by branch.
4. **The 5 `chore/roadmap-sync-*` refs** — tagged `abandon` from their closed-unmerged PRs and
   `already-contained` from `ahead=0`. Both are true at different times: the PRs were closed
   unmerged, then the peer lane merged the branches into `083-repo-tidy-up`. Confirm they are now
   contained and deletable.
5. **067 vs 067b** — `067` has 10 open tasks / 8 conflicts, `067b` has 0 open tasks / 12 conflicts.
   The Critic REFUTED the `duplicate-implementation` tag on `067` (a zero-open-task count is not a
   record that another branch supersedes it), so the survivor is an engineer choice.

## 7. Singletons kept visible (not averaged away)

The single-slice claims that carry the most weight are precisely the ones only one slice *can*
ground: S3 alone sees recorded decisions (the two CONFIRMed `duplicate-implementation` tags on
`origin/037-virtual-3270-term` "superseded via 040" and `origin/066-abandon-stub-cleanup` "now
redundant (065 shipped the same removal)"), and S2 alone sees open-task counts. The Critic REFUTED
5 `abandon` claims on PRs #163–168 because the ledger excerpt cited showed only number/branch/title,
not the closed-unmerged state — a correct rejection of an under-cited claim even though the
underlying fact is true elsewhere in the same file.

## 8. Cycle 2 not run

`min_cycles` was 2; one cycle ran. Coverage was already complete (three full universe enumerations,
327 subjects, no coverage gap either builder could close), and every remaining question is an
engineer ruling that a second blind pass over unchanged evidence cannot resolve. Recorded as a
scope stop, not as convergence. Budget at stop: ~643k of 1.2M tokens.

## 9. Deliverable

The ordered workplan **W01–W25** (121 points; sizes from nano=1/micro=3/mini=7/midi=11/maxi=17/saga=35
with a hard max of `mini` per step; phases analyze/implement/codexreview-ship/close) is registered
durably in marathon run `mrun-f5ef56dba3c1` under item
`mitem-01a01f1d-c9b4-77af-b9c0-e81d0e47f57c`. Preservation (W02–W05) precedes every merge, and every
deletion (W19–W21) lists a restore-verified preservation step as a prerequisite.
