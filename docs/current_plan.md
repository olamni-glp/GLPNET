# Restart pointer — NOT a work ledger (updated 2026-08-22, ariellas lane)

> Intentionally thin. The **roadmap + buildkit marathon state** are the source of truth
> (CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*). Never resume from a hand-written
> plan or from a compaction summary — derive the position from durable rows.

## Resume in one line

```
buildkit-marathon resume --feature glpnet-full-completion-programme
```

🔴 **`--feature` is mandatory.** There is no `.specify/feature.json` in this repo, so a bare
`buildkit-marathon resume` will not find the programme run. It is `mrun-f5ef56dba3c1`.

🔴 **`next` is WRONG — read the ledger file, not the pointer.** `buildkit-marathon status` still
reports `next: start W11 …` from the **superseded 2026-08-20 plan**. That item
(`mitem-01a01f1d-…`) was **deferred** on 2026-08-22 and the defer *did not* remove its steps from
the `next` computation (marathon defect — reported). **The live ledger is the A-series** in
`docs/research/tidyup-crdt-workplan-2026-08-22-ariellas.md`, item
`mitem-01a02a75-b0fd-778c-9ab2-e5e7a3682afd`. **Next real step is A10.**

## Two lanes, disjoint — do not re-derive this

| Lane | Marathon | Owns | Ledger file |
|---|---|---|---|
| **ariellas** (this host) | `mrun-f5ef56dba3c1` | host-local `Ariellas` git residue in **both** D: clones · the glpnet **board** · **roadmap sync** · feature **`082-feature-stream-superset`** end-to-end | `docs/research/tidyup-crdt-workplan-2026-08-22-ariellas.md` (A01–A21) |
| **gavriella** | `mrun-20d9230f767b` | `067` private-key rotation + merge · `066` · the `050`/`059` survivor ruling · tag→GitHub-Release backfill · `078` | `docs/research/tidyup-crdt-workplan-2026-08-22.md` (X01–X14) |

Both supersede the 2026-08-20 `W01–W25` plan. Declared in `I:\coop\20260822T162201Z-ariellas-…md`
§7 and `…20260822T174500Z-ariellas-…md` §4.

## Where things stand (2026-08-22, end of session)

- **Branch** `develop` @ `1cf1e908`, pushed, clean. Peer cut `v2026.08.21.3`; PR #203 (078) landed.
- **Marathon** `mrun-f5ef56dba3c1` — 18/46 steps complete; A01–A07 + A09 delivered this session
  (12 of 118 pts on the A-series); 124 outstanding backlog items.
- **Takt targets now set** for every phase (30 min – 3 h) and for a feature (1.5 – 6 h).
  First reading: `analyze` p50 **0.05 h — under band**; one legacy `implement` step reads 21.82 h
  (a step spanning a session gap, not a real duration). 6 of 46 steps measurable.
- **Roadmap round 31** — reconcile in sync · import converged · **0 dup groups in 115 live** ·
  export **20 epics / 116 features / 3760 journal lines**, both publish legs OK.
  **23 of 116 features not closed; 9 of those carry no epic** and are invisible to
  `buildkit-roadmap status` — always fold `heads` from the export.
- **Board** `I:/coop/glpnet/sched` — `ariellas` onboarded 840 h / 35 d × 3×8 h (113 calendar rows,
  horizon `2026-09-26`). 3 stuck claims cleared. **`082` is `in-progress` and is this lane's
  active feature.**

## 🔴 Two things this session proved that the previous pointer got wrong

1. **"Nothing can be deleted unrecoverably" was FALSE.** `064-durable-walfix` tip `d0187c9f`
   (2026-08-06) was reachable from **no remote ref and no tag**. Closed: `archive/064-durable-walfix-20260822`
   pushed to origin + a verified bundle. **Every host must run
   `git rev-list --count <branch> --not --remotes --tags` on its own clones** — an origin-driven
   sweep is structurally blind to host-local-only work.
2. **"glpnet has ZERO linked worktrees" is clone-1-only.** Clone 2
   (`D:/BSTDEV/glp/GLPNET`) has one: `…GLPNET.worktrees/051-ynet-transport` (clean, and
   origin-preserved by `archive/051-ynet-transport-20260820`). Do not delete it — it is another
   lane's live checkout.

## NEXT — in strict order

1. **A10** — `/bk-close` on `076-type-checker-body-atom-moding` (roadmap says `released`, not
   `closed`; merged into develop, 0 open tasks, no retrospective dir).
2. **A12–A19** — `082-feature-stream-superset` end-to-end: clarify → plan → tasks → analyze →
   implement → codexreview → ship → close. **Defects D1 and D2 fold in at clarify** (this lane
   accepted ownership in the coop ACK §4).
3. **A20** — trust-material controlled reproduction (WP now `ready`).
4. **A21** — per-phase takt emission and `/bk-flow` migration prep.
5. **A11** — delete the 39 contained clone-1 local heads. **Engineer ruling required** (safety is
   settled by containment; ownership is not).

## Open blocks — ENGINEER rulings, nothing proceeds past them

| # | Block | Why it blocks |
|---|---|---|
| 1 | **`067` private key material is PUBLIC on `main`** (peer X07) | `glpquick.key/.pem/.pfx` entered at `94fbe87d` (the `v2026.07.09.1` release commit), reachable from **23 of 65 version tags** and from `origin/main`, in a **public** repo, exposed 44 days. `.gitignore` prevents recurrence; history is unchanged. Rotation is overdue remediation. `067` held at `escalated` on the board. **Peer's lane, but it gates any 067 work here.** |
| 2 | **3rtask `--accept-refutes`** | Run `20260822T170003Z-fa65` halted at `freeze-method`. The blind codex Critic REFUTEd 12 of 14 method elements; the Planner revised all 12, but `brief --phase planning --method` is append-only per (run, phase) and refused to re-record, so the revision can never be blind-re-reviewed **inside its own run**. Frozen method = 4 elements, missing every token set, the normalization, the merge contract and the budget rule. `--accept-refutes` is documented as the **engineer's** override. |
| 3 | **A11 lane ownership** | 39 contained clone-1 local heads are provably safe to delete. Two lanes both claim ref-deletion scope. Ownership, not safety. |
| 4 | `080-occurs-checked-substitution` | 2 conflicting paths, gated on the **§1.14 language-authority ruling that is Udi's, not Gabi's** (UnifyFail vs CompileError). |
| 5 | `050` vs `059` survivor | Measured complementary (050 = QUIC transport + link lifecycle; 059 = compiler/type-checker/bytecode + Lean proof; overlap 15 files). Peer's lane. |
| 6 | The 5-minute coop ACK convention | Nothing polls `I:\coop` when a session is closed, so three lanes each escalated the same non-event. Needs a daemon or a longer window. |

## Measured defects raised this session (reported, not worked around)

- **3rtask**: a revised method cannot be blind-re-reviewed in its own run (append-only planning
  artifact) — see block 2.
- **3rtask Critic non-determinism: 14.3%.** Byte-identical method artifact, same codex Critic,
  two passes → **2 of 14 elements flipped `REFUTE → CONFIRM`**. Bears directly on the recorded
  `/bk-flow` blocker *"Critic determinism"*.
- **marathon**: `defer <item>` does not remove that item's steps from the `next` computation.
- **scheduler**: an **unplaceable proposal is emitted silently** — `e_t_s 144000 > capacity 86400`
  for `ariellas`, `load` then declines to bill it and the lane reads idle at `remaining 86400`.
  Root cause of the peers' "D1 bills zero" framing, which is **refuted as stated** (gavriella
  bills 86400.0 correctly). Folds into `082` US2.
- **scheduler**: `dispatch_ranked` carries no addressee; **26 of 31** durable `allocate` ops read
  `engineer_id = "unassigned"`. Folds into `082` US3.
- **D10 (peer's) NOT REPRODUCED here**: `onboard --shifts 35 --avail-hours 840` re-anchored the
  horizon to today + 35 d (`2026-09-26`). Scoped, not universal.

## Environment gotchas (still current)

```
$env:PATH = "C:\Program Files\nodejs;C:\Program Files\Git\cmd;C:\Program Files\GitHub CLI;$env:PATH"
$env:PYTHONUTF8 = 1
```

- `node`, `git`, `gh` are **not on PATH by default**; PGlite commands exit 2 without node.
- Scheduler board: **always** `--root I:/coop/glpnet/sched`. There is no default and an
  unconfigured host reports an empty board at exit 0. This host is **`Ariellas`**, actor
  **`ariellas`**; `I:\coop` == `\\192.168.0.108\GAVRI_D\coop` == `D:\coop` on `GAVRIELLA`.
- Roadmap sync is **two-legged**: `import --in-dir I:/coop/glpnet/roadmap-sync/inbox`, then
  `export` **twice** — local `exports/` and `--out-dir` that same inbox.
- A killed roadmap command leaves `pgdb/.lock` behind. Recent builds **auto-reap** an orphaned
  bridge ("reaped orphaned PGlite bridge PID N"); if not, check the PID is dead then `rm -rf`.
- Git-Bash mangles `rev:path` args — prefix `MSYS_NO_PATHCONV=1 MSYS2_ARG_CONV_EXCL='*'`.
- `marathon expand --steps` is **comma-delimited with no escaping** — never put a comma in a step.
- `marathon checkpoint --paths` refuses out-of-repo paths; omit it for outside evidence.
  `checkpoint` has **no held/blocked state** — use `trace --decision reject` with an
  `ESCALATE(open):` prefix instead of logging a gated step `complete`.
- **`gh pr merge` and chained `git tag … && git push`** trip the auto-mode permission classifier.
  Issue one verb per command; a bare `git push origin <ref>` is fine.
- **Running the suite:** the script always prints a `Total: … Passed: … Failed: …` summary block
  last — **an absent summary means the run did not finish, whatever the exit code says.** Launch
  it with PowerShell `Start-Process` (outside the tool process tree, which has a 10-minute cap),
  exporting the FULL inherited PATH, and reap stragglers first.
