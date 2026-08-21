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

- **Branch**: `develop`. **RELEASED `v2026.08.21.1` then `v2026.08.21.2`** (PRs #194/#197 → main,
  both tagged; back-merges #195/#198 → develop). develop/main in normal GitFlow steady state.
- **Repo is TIDY**: origin heads **146 → 19**. The peer lane deleted 127 refs; audited with one
  batch `git rev-list --no-walk <127 tips> --not origin/develop` → **zero unreachable**. All 18
  archive tags still resolve (14 branches alive, 4 landed).
- **15 unmerged refs remain and ZERO merge clean** — smallest `080` at 2 conflicting paths,
  largest `059` at 89. Nothing further lands without a per-branch ruling.
- **Green baseline**: `test/run_all_tests.sh` on merged develop = **559/559, 0 failed, all 21
  sections A–U**, summary block present, exit 0. Log:
  `D:/BSTDEV/evidence/glpnet-tidyup-20260820/suite-20260821-clean.log`.
- **Marathon** `mrun-f5ef56dba3c1` — **10 of 25 steps complete (44 of 121 pt)**; 120 outstanding
  backlog items; discharge gate **8 of 25 satisfied, 17 unsatisfied** (all engineer rulings).
- **Workplan item** `mitem-01a01f1d-c9b4-77af-b9c0-e81d0e47f57c` carries the ordered CRDT workplan
  **W01–W25, 121 points**, sizes `nano 1 / micro 3 / mini 7 / midi 11 / maxi 17 / saga 35`,
  hard cap `mini` per step, phases `analyze | implement | codexreview-ship | close`.
- **Roadmap**: round 30 converged (2026-08-21) — 20 epics / 116 features / 3760 journal lines, 0 duplicate groups,
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

### W06–W10 DONE 2026-08-21 — six branches landed

PRs **#188** `083-repo-tidy-up` · **#189** `081-scheduler-supply-rootcause` · **#190**
`083-glptutorial-corpus-goldens` · **#191** `049-wave1-guard-link-acceptance` · **#192**
`078-verification-receipts` · **#193** `084-host-tidy-up-and-merge-closure`. (**#187** landed earlier.)

🔴 **Concern carried forward on 049**: it reported `tasks_open=0` and merged clean, but its unchanged
SHIP-HANDOFF holds **four unchecked hard GO-CONDITIONS**. Raised, reaffirmed by the engineer, landed
on that instruction. Not resolved — still owed.

## NEXT — in strict order

1. **W10 re-measure result: ZERO clean branches remain.** All 14 still-unmerged refs conflict —
   `080` (2 paths) · `backup/upgrade-buildkit-migration` (6) · `067` (8) · `066-wave6` (9) ·
   `058-s4` (11) · `067b` (12) · `backup/078-olamnit` (15) · `016` (19) · `017` (19) ·
   `030-phase8-polish` (24) · `backup/030` (27) · `051-ynet` (26 ahead) · `050-full-gleam` (64) ·
   `059-full-scope-gleam` (89). **Nothing further can land without a per-branch ruling.**
2. **W11–W18** — the conflicted branches, each behind a ruling (see the open blocks).
3. **W19–W21** — deletions. Preservation-unblocked; gated on the lane-ownership ruling. The peer has
   already staged 124 remote refs for deletion (audited safe — see block 3).
4. **W22–W25** — PR hygiene (draft PR #111), roadmap reconcile, codexreview, takt emission.
5. ~~Release~~ **DONE** — `v2026.08.21.1` cut on the verified-green baseline.

## Open blocks — ENGINEER rulings, nothing proceeds past them

| # | Block | Why it blocks |
|---|---|---|
| 1 | ~~`gh pr merge` permission~~ **RESOLVED 2026-08-21** | The verb now works for single invocations. It still trips the classifier when wrapped in a shell `for` loop — issue one PR merge per command. |
| 2 | Gleam cluster `050` vs `059` — **MEASURED 2026-08-21, N12's premise is false** | File-overlap: 050 changes **1152** files, 059 changes **248**, shared = **25** (2% / 10%), and 10 of those 25 are noise (.gitignore, COOP handoffs, roadmap exports). Real collision surface = **15 Gleam files**: `engine.gleam`, `engine/{kernels,runner,scheduler}.gleam`, `analysis/prelude.gleam`, and 10 `link/primitives/*.gleam`. This supports **C1 (complementary tiers)**, not N12 (rival implementations). **Recommendation: land 050 first, rebase 059 onto it, resolve only the 15-file seam. Discard nothing.** Engineer ruling still required to proceed. |
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
- 🔴 **A killed roadmap command leaves `pgdb/.lock` behind** and every later catalog command dies with
  *"catalog unavailable: pgdb/.lock held by PID N"*. Check the PID is actually dead
  (`Get-Process -Id N`) and only then `rm -rf pgdb/.lock`. Hit on 2026-08-21 with dead PID 16432.
- 🔴 **Git-Bash mangles `rev:path` arguments** — `git show origin/branch:file` becomes
  `originranch;file`. Prefix with `MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*'`.
- `marathon expand --steps` is **comma-delimited with no escaping** — never put a comma in a step text.
- `marathon checkpoint --paths` refuses out-of-repo paths; omit `--paths` for evidence held outside
  the repo, and do not suppress its output or the failure is silent. `checkpoint` has **no
  held/blocked state** — a gated step can only be logged `complete`, which over-reports.
- 🔴 **Running the suite — three false greens were hit on 2026-08-21, all with the same tell.**
  (1) `nohup … &` combined with the harness `run_in_background` flag → the *launcher* exited 0 after
  only Section A. (2) Two orphaned suite trees running concurrently → a phantom
  `FAIL: bidirectional [C→G]` in **Section I**, which passes cleanly once the orphans are reaped
  (`link_both_ways` PASS=4 FAIL=0). (3) A detached wrapper with a stripped PATH → `SUITE_EXIT=127`
  and 215 phantom Section-A failures. **Detection rule: the script always prints a
  `Total: … Passed: … Failed: …` summary block last — an absent summary means the run did not
  finish, whatever the exit code says.**
  **Correct way to run it:** write a wrapper that exports the FULL inherited PATH (dart *and*
  dotnet *and* node), launch it with PowerShell `Start-Process` so it sits outside the tool process
  tree (the 10-minute Bash cap otherwise orphan-kills it), and watch the log with `Monitor`. Reap
  stragglers first — `history_drill.sh` respawns, so killing needs a repeat loop.
- `gh pr merge` inside a `for` loop is refused by the permission classifier; run it one PR at a time.
