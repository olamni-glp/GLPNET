# Restart pointer — NOT a work ledger (updated 2026-08-21)

> Intentionally thin. The **roadmap + buildkit marathon state** are the source of truth
> (CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*). Never resume from a hand-written plan
> or from a compaction summary — derive the position from durable rows.

## Resume in one line

```
buildkit-marathon resume --feature glpnet-full-completion-programme
```

🔴 **`--feature` is mandatory here.** `.specify/feature.json` points at
`specs/083-glptutorial-corpus-goldens`, so a bare `buildkit-marathon resume` resolves to 083 and
reports *"no active marathon run"* — which is **not** true. The live programme run is
`mrun-f5ef56dba3c1`, feature id `glpnet-full-completion-programme`.

## Where things stand (2026-08-21)

- **Branch**: `084-host-tidy-up-and-merge-closure` (pushed, tracking origin).
- **Marathon** `mrun-f5ef56dba3c1` — **7 of 25 steps recorded; TRUE figure 6 done + 1 held (W07)**; 116 outstanding backlog
  items; discharge gate **8 of 25 satisfied, 17 unsatisfied** (all engineer rulings).
- **Workplan item** `mitem-01a01f1d-c9b4-77af-b9c0-e81d0e47f57c` carries the ordered CRDT workplan
  **W01–W25, 121 points**, sizes `nano 1 / micro 3 / mini 7 / midi 11 / maxi 17 / saga 35`,
  hard cap `mini` per step, phases `analyze | implement | codexreview-ship | close`.
- **Roadmap**: round 29 converged (2026-08-21) — 20 epics / 116 features / 3760 journal lines, 0 duplicate groups,
  both publish legs OK. **23 features not closed** (9 of them carry no epic and are therefore
  invisible to `buildkit-roadmap status` — always fold `heads` from the export instead).

### Done and verified — the preservation phase is complete

W01–W05 plus W06 (23 + 3 = 26 of 121 points). **Nothing in this repo can now be deleted
unrecoverably**:

- 18 full-history `git bundle`s at `D:/BSTDEV/evidence/glpnet-tidyup-20260820/bundles` (1.7 GB),
  every one `git bundle verify`-clean with its restored tip SHA matched against the recorded tip.
  Hard proof: fetch-from-bundle into a fresh empty repo restored `050-full-gleam-combined` at
  `10f02f7d` with 2566 reachable commits.
- 18 annotated `archive/<name>-20260820` tags **pushed to origin** — objects stay reachable
  server-side independent of any local ref.
- The second clone's only at-risk content (2 unpushed commits on `D:/BSTDEV/glp/GLPNET`'s local
  `main`, `57fa2066` / `fd305b5a`) bundled and verified at
  `D:/BSTDEV/evidence/glpnet-tidyup-20260820/clone2`.

**Why this mattered**: `3dca578c` — a fix `CHANGELOG.md` records as merely "stranded" on
`glpnet-lane/toolchain-integrity-fixes` — is **already gone**; `git cat-file -t` reports it is not a
valid object and no such ref exists on origin. A list of tip SHAs preserves nothing.

## NEXT — in strict order

1. **W07 / W08** — merge `origin/081-scheduler-supply-rootcause` (3 ahead) and
   `origin/083-glptutorial-corpus-goldens` (1 ahead). Both re-measured CLEAN against develop
   `2d72c1bd` on 2026-08-21. Preservation gate already satisfied. **Blocked only by the
   `gh pr merge` permission gate** (see below).
2. **W09 — do NOT merge `049-wave1-guard-link-acceptance` on the topology reading.** It measures
   CLEAN and `tasks_open=0`, but its unchanged SHIP-HANDOFF carries **four unchecked hard
   GO-CONDITIONS**. Resolve those first or record a decision to waive them.
3. **W10** — re-measure `merge-tree` across the remaining branches; landings change the base.
   Unmerged remote refs stood at **20** on 2026-08-21 (up from 18: `084` and `078-verification-receipts`
   are new; `083-repo-tidy-up` is 3 ahead / 0 behind).
4. **W11–W18** — the conflicted branches. Every one needs a ruling first (see the open blocks).
5. **W19–W21** — deletions. Now **unblocked by preservation**, but gated on the lane-ownership
   ruling: a second lane is running its own tidy-up ledger on `083-repo-tidy-up` and has already
   staged 124 remote refs for deletion (audited safe — see block 3).
6. **W22–W25** — PR hygiene (draft PR #111), roadmap reconcile, codexreview + ship 084, takt emission.

## Open blocks — ENGINEER rulings, nothing proceeds past them

| # | Block | Why it blocks |
|---|---|---|
| 1 | `gh pr merge` refused by the auto-mode permission classifier | W07–W09 and the W24 ship. PR creation, commits, tag pushes are all permitted; only the merge verb is gated. `.claude/settings.local.json` already allows it — the **classifier** is the gate, not settings. |
| 2 | Gleam cluster `050` (48 ahead / 64 conflicts) vs `059` (32 ahead / 89 conflicts) | 96 of the 220 unmerged commits. Marathon holds two **contradictory** recorded reads: item N12 "independent colliding implementations", item C1 "complementary tiers". Both cannot be true. |
| 3 | Lane collision | Two concurrent tidy-up workplans on one repo: this marathon's W01–W25 (121 pt) and the peer's 14-step ledger on `083-repo-tidy-up` (136 pt). Neither references the other; both claim ref-deletion scope. **Audited 2026-08-21: the peer's W13 list of 124 remote branches was checked against `origin/develop` 2d72c1bd — all 124 are true ancestors, so their deletion is SAFE (containment IS the preservation for contained refs; the bundle/tag gate binds only the non-contained refs, which this lane has covered).** The open question is ownership, not safety. |
| 4 | `080-occurs-checked-substitution` | Only 2 conflicting paths, but gated on the **§1.14 language-authority ruling that is Udi's, not Gabi's** (UnifyFail vs CompileError). |
| 5 | `067` vs `067b` survivor | `067`: 10 open tasks / 8 conflicts. `067b`: 0 open tasks / 12 conflicts. The Critic REFUTED the `duplicate-implementation` tag on `067` — a zero-task count is not a record of supersession. |
| 6 | `needs-rebase` vs `needs-completion` exclusivity | 10 of 16 merge conflicts are this taxonomy artefact, not disagreement. A branch can be behind develop *and* incomplete. |
| 7 | 5 `chore/roadmap-sync-*` refs | Tagged `abandon` (closed-unmerged PRs) **and** `already-contained` (`ahead=0`). Both true at different times — confirm they are now contained and deletable. |

## Provenance of the plan

3rtask run `20260820T115931Z-dddd` — 3 blind builders over file-disjoint / subject-overlapping
slices, cross-provider codex Critic, **0 independence violations**, 753 claims → 596 (subject,tag)
pairs, **157 corroborated**, 16 conflicts; adjudication 42 CONFIRM / 12 REFUTE / 35 ESCALATE.
Report: `docs/research/host-d-git-asset-survey-and-tidyup-workplan-2026-08-20.md`.

**Tool defect found, reported not worked around**: `buildkit-3rtask merge` returned 2 corroborated of
820 claims because `threerole/concept.py` `_category_in_key()` drops the subject from the merge key
for `task_type` in {code, plan, strategy}. The E07 merge was computed by a deterministic set-ops
script instead (`.specify/3rtask/e07_merge.py`).

## Takt

Scheme confirmed active: `nano 1 · micro 3 · mini 7 · midi 11 · maxi 17 · saga 35`.
Targets: a phase 30 min – 3 h, a feature 1.5 – 6 h. Correction to the peer's takt doc: **`bk-flow`
IS on PATH** (`poll · claim · open · report · version`) — that prerequisite is met; the remaining
`/bk-flow` migration blockers are the takt projection, step-board integrity and Critic determinism.

## Environment gotchas (still current)

```
$env:PATH = "C:\Program Files\nodejs;C:\Program Files\Git\cmd;C:\Program Files\GitHub CLI;$env:PATH"
$env:PYTHONUTF8 = 1
```

- `node`, `git`, `gh` are not on PATH by default; PGlite commands exit 2 without node.
- Scheduler board: **always** pass `--root I:/coop/glpnet/sched`; there is no default and an
  unconfigured host reports an empty board at exit 0.
- Roadmap sync is **two-legged**: import with
  `--in-dir I:/coop/glpnet/roadmap-sync/inbox` and export to both the local `exports/` and that inbox.
- `marathon expand --steps` is **comma-delimited with no escaping** — never put a comma in a step text.
- `marathon checkpoint --paths` refuses out-of-repo paths; omit `--paths` for evidence held outside
  the repo, and do not suppress its output or the failure is silent.
