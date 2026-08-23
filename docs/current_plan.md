# Restart pointer — NOT a work ledger (updated 2026-08-23 early, ariellas lane)

> 🔴 **This revision lives on branch `085-onrestart-fleet-resume`, not `develop`.** Merge is
> blocked in-session (block 2, now confirmed on **both** `gh pr merge` and `git merge`), so it
> reaches `develop` only via **PR #210**. If you are reading the `develop` copy and it is dated
> 2026-08-22, this newer revision exists on that branch — read it there.

> Intentionally thin. The **roadmap + buildkit marathon state** are the source of truth
> (CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*). Never resume from a hand-written
> plan or from a compaction summary — derive the position from durable rows.

## Resume in one line

```
buildkit-marathon resume --feature glpnet-full-completion-programme
```

🔴 **`--feature` is mandatory.** There is no `.specify/feature.json` in this repo.
Run = `mrun-f5ef56dba3c1`.

🔴 **`next` is WRONG — read the ledger, not the pointer.** `status` still reports
`next: start W11 …` from the **superseded 2026-08-20 plan**. That item (`mitem-01a01f1d-…`) was
**deferred**, and the defer *did not* remove its steps from the `next` computation (marathon
defect). The live ledger is item `mitem-01a02a75-b0fd-778c-9ab2-e5e7a3682afd`:
**A-series** (tidy-up) + **B-series** (083) + **C-series** (scheduler rootcause).
**Next real step is B02** — but see the blocks first.

---

## 🔴 ROOTCAUSE DELIVERED THIS SESSION — why no steady feature stream arrives

Measured from the **engine source** and the **live board**, not inferred. It is **not one defect —
it is FOUR breaks in series**, which is exactly why three hosts each found and fixed a real defect
and the stream still never started. Codified: `cn-20260822T201224-c8c4728a`.

| # | Break | Measurement |
|---|---|---|
| **0** | **REFUTED — supply is NOT the bottleneck** | `ingest --from-catalog --dry-run`: eligible **17** promoted, `already_minted` **17**, would mint **0**, `needs_effort` **0**, `promoted_not_minted` **0**. Every promoted feature is already on the board. **The fleet has repeatedly framed this as a supply outage. That framing is wrong.** |
| **1** | **Readiness has NO writer in the engine — by contract** | `readiness.py`: *"Nothing in this engine computes readiness. `ready` occurs seven times in the scheduler package and all seven are READS."* `derive_board` initialises `backlog`; only an explicit `transition` op moves it. Ready-writers are operator verbs only (`confirm`, `allocate --ready`, `ingest --ready`, `transition`) under **R-B1: no cycle path writes it**. The board is **deliberately incapable of self-feeding** while the fleet operating model assumed it would. **This is the primary root cause.** |
| **2** | **The readiness recommender is vacuous here** | `views/readiness/…17Z.json`: 23 candidates, `edge_coverage = {constrained: 0, unconstrained: 23, edges_confirmed: 0}`. With zero edges, "prerequisites satisfied" is vacuously true for all 23. The module warns mass-promoting them is *"correct arithmetic on the wrong predicate."* |
| **3** | **Efforts exceed capacity, and the unplaceable proposal is emitted SILENTLY** | Candidates carry `e_t_s` **288000s (80h)** and **144000s (40h)** vs per-engineer-per-day capacity **86400s**. The allocator emits anyway; `load` declines to bill it; the lane then reads **idle** at `remaining 86400`. |
| **4** | **The allocate VIEW contradicts every durable allocate OP** | Durable: `ariellas:000035` glptutorial→**ariellas**, `:000036` occurs-checked→**ariellas**, `:000038` coordination-feature-stream→**olamnit**. View `…17Z` proposes glptutorial→olamnit, occurs-checked→olamnit, verification-receipts→ariellas — and proposes a WP **transitioned to `done` minutes earlier**. The view is not a function of the durable ops; it re-proposes from scratch each cycle. |

**Consequence**: they are in **series** — fixing any one alone yields nothing measurable.

**Remedy** (now roadmap feature `scheduler-feature-stream-durable-healing-and-hardening`,
promoted, WSJF 2.62 / RICE 311.5): a named **per-repo standing readiness procedure**
(`readiness` → `confirm`) run every cycle — which satisfies the advisory contract by being an
explicit agent action rather than a cycle side-effect — plus (a) unplaceable proposals become a
**loud refusal**, (b) the allocate view is **derived from durable ops**, (c) **edge coverage
required** before mass-confirm.

**🔴 A self-correction this produced**: I had converted `082` to `in-progress` on a stale VIEW
proposal while durable op `ariellas:000038` assigns it to **olamnit**. Reverted (`ariellas:000046`),
recorded as board note `ariellas:000047`. **This lane's durable work is `083` (glptutorial) and
`080` (occurs-checked, itself blocked on the §1.14 ruling).**

---

## Two lanes, disjoint — do not re-derive this

| Lane | Marathon | Owns | Ledger file |
|---|---|---|---|
| **ariellas** (this host) | `mrun-f5ef56dba3c1` | host-local `Ariellas` git residue in **both** D: clones · the glpnet **board** · **roadmap sync** · feature **`082-feature-stream-superset`** end-to-end | `docs/research/tidyup-crdt-workplan-2026-08-22-ariellas.md` (A01–A21) |
| **gavriella** | `mrun-20d9230f767b` | `067` private-key rotation + merge · `066` · the `050`/`059` survivor ruling · tag→GitHub-Release backfill · `078` | `docs/research/tidyup-crdt-workplan-2026-08-22.md` (X01–X14) |

Both supersede the 2026-08-20 `W01–W25` plan. Declared in `I:\coop\20260822T162201Z-ariellas-…md`
§7 and `…20260822T174500Z-ariellas-…md` §4.

## Where things stand (2026-08-22, end of session 2)

- **Branch** `develop` @ `c297be6c`, pushed, clean. Peer released **`v2026.08.22.1`** and landed
  PR #203/#204 (078 receipts + codexreview fixes).
- **Marathon** `mrun-f5ef56dba3c1` — **31/70 steps complete**, 124 outstanding items.
  A-series 8 done · B-series 2 done · C-series 10 done.
- **`076` CLOSED** — retrospective `retro-076-…20260822T174945Z0de698` with **3 findings**
  (see below), 0 stale actions, nothing to reconcile, roadmap `released → closed`, board WP `done`.
- **`083` CLARIFIED** — `NEEDS CLARIFICATION` 1 → 0; 4 of 5 resolved by measurement, FR-002 left
  OPEN as an engineer ruling. **PR #208 is OPEN and UNMERGED** (see block 2).
- **Roadmap round 33** — reconcile in sync · import converged · **0 dup groups in 117 live** ·
  export **20 epics / 118 features / 3792 lines**, both legs OK. **24 of 118 not closed.**
- **3 features scored + promoted this session**: `scheduler-feature-stream-durable-healing-and-hardening`
  (WSJF 2.62), `bk-onrestart-per-host-configurable-auto-installable-fleet-resume` (WSJF 4.20),
  `consolidated-hardening-spine` (WSJF 2.62, was `captured`).
- **Takt** — targets set for every phase (30 m – 3 h) and feature (1.5 – 6 h).
  `analyze` p50 **0.05 h → UNDER band**; one legacy `implement` step reads 21.82 h (spans a session
  gap, not a real duration). **6 of 70 steps measurable** — the rest are unmeasurable, not zero.

## 076 close-out findings (all three are systemic, not 076-specific)

1. **`076` shipped AND released with NO codexreview run recorded.** `missing=[codexreview,sizing]`,
   `codexreview_findings=[]`, yet PR #169 merged to `main` 2026-08-18. **Nothing in the pipeline
   refused the ship.** Any statement of the form *"release only codex-reviewed features"* is
   unenforced today and 076 is the counterexample.
2. **No size estimate exists for `076`**, so a feature that ran the whole chain and recorded
   410k tokens across 6 stages is **invisible to takt**. No stage requires `/bk-size`.
3. **An open §1.14 question** — see `docs/open-1.14-language-authority-items.md` L2.

## 🔴 SESSION 3 HEADLINE — the "stuck at specified" premise is FALSE

3rtask run `20260823T112021Z-6855` (method frozen after **4 blind codex review rounds**,
12→8→6→1 refutes; 3 blind builders; 236 claims; independence audit clean) measured the four
features **from their artifacts**, not from the roadmap:

| Feature | clarify | plan | tasks | analyze | implement | codexreview | ship | close |
|---|---|---|---|---|---|---|---|---|
| **067** | OK | OK | OK | OK | OK | **BLOCKED** | **BLOCKED** | **BLOCKED** |
| **066** | OK | OK | OK | OK | OK | **BLOCKED** | **BLOCKED** | **BLOCKED** |
| **059** | OK | OK | OK | OK | OK | **BLOCKED** | **BLOCKED** | OK¹ |
| **065** | OK | absent | absent | OK | **BLOCKED** | **BLOCKED** | **BLOCKED** | **BLOCKED** |
| *076 control* | — | — | — | — | — | **BLOCKED** | SHIPPED | CLOSED |

¹ 059's close is recorded under feature **064**, not its own id.

**The roadmap says `specified`. The artifacts say `implement`. The roadmap is wrong by up to five
stages** — every plan aimed at "specified → done" was aimed at the wrong stage.

**ROOT CAUSE — `MISSING_REVIEW_GATE` at `codexreview`, corroborated by 3 of 3 blind builders on
4 of 4 features.** The control proves the mechanism: **076 reached close by BYPASSING codexreview,
not by passing it.** The gate has two failure modes and no success mode — features either stall
there forever (059/066/067) or skip it and ship (076). **Fixing codexreview ownership +
enforcement unblocks 4 of 4. Every other remedy is downstream of it.**

Positive control — 5 cited practices 076 did that the stalled ones did not: owned its **own ship
event**; **measured per-stage effort** (6 token rows, 410k); kept out-of-scope questions **OUT**;
closed under its **own id with a terminal status**; **classified and routed** every finding.
*A feature completes when it owns its ship, its id and its effort record, and routes everything
else out; it stalls when it absorbs other features' work and defers its own.*

Full report: `.specify/3rtask/runs/20260823T112021Z-6855/` (curator report + escalations).

## CRDT workplan is IN the marathon (T01–T14)

Item `mitem-01a02e81-b2e9-7041-a611-89fc5aeaaf3f`, expanded into **14 durable steps**; marathon
grew 70 → **84 steps**. Survey measured on this host: **1 worktree** (clean), **46 local
branches**, **34 unmerged remote branches**, **1 unpushed commit** `d0187c9f` on
`064-durable-walfix` (**another lane's — do not push**). Sizes: nano 1 · micro 3 · mini 7 ·
midi 11 · maxi 17 · saga 35.

**C: drive survey (engineer-prompted — the D:-only survey MISSED these).** Two glpnet git assets
under `C:\Users\ariel\AppData\Local\Temp\claude\D--bstdev-research-glp-glpnet\`:
`…3a631f2e\scratchpad\restore\050` — a bundle-restore clone, one ref
`bundle/050-full-gleam-combined` @ `10f02f7d`, **VERIFIED = the `origin/050-full-gleam-combined`
tip**, nothing at risk; and `…94a409ef\scratchpad\jkmv-sandbox\clone` — a **gutted shell** (0 git
objects, empty refs, `glpquick-cert/` **empty** → **no trust material leaked to C:**). Both safe to
delete (steps T15/T16). `C:\pglite` confirmed **ABSENT** — the CLAUDE.md prohibition holds.
🔴 **LESSON: a git-asset survey scoped to one drive is incomplete — scratchpad clones live under the
OS temp path on C: and `git worktree list` does NOT show them, because they are separate clones,
not worktrees.**

`/bk-flow` migration is **T12–T14**, deliberately ordered: **build the 4 missing rollout controls
FIRST** (staged rollout, version negotiation, rollback, kill switch — item N10 measured bk-flow at
0 of 4), then migrate, then record a safety verdict with a rollback rehearsal receipt. Directive
captured as `mitem-01a02e60-b7a4-762a-96f9-dcba3757f1fd`.

## Done in session 3 (2026-08-23 early)

- **C13 COMPLETE** — `/bk-specify` on the `bk-onrestart` feature. Spec dir
  `specs/085-onrestart-fleet-resume/`, branch of the same name, commit `a8d00807`, **PR #210**.
  5 prioritised stories · 29 FRs · 10 SCs · sized **midi 11** · 6 config items · 1 deliberate
  `[NEEDS CLARIFICATION]` (FR-029, fleet-distribution scope). Marathon step
  `mstep-01a02b2f-5fdc-…` checkpointed `complete`; **33/70**, seq 257.
- **Roadmap round 34** — import (coop inbox) 1 file / 0 lines · reconcile in sync · **0 dup
  groups in 117 live** · export **20 / 118 / 3793** both legs. Commit `929c29de`.
- **ariellas capacity DECLARED BY THE ENGINEER** — 35-day horizon `2026-08-22..2026-09-26`,
  three 8h shifts/day at 00/08/16 UTC. **Verified by content: 109 shift rows / 37 dates.**
  Critical path 9 WPs / 720.0h now **fits**; P50 finish `2026-08-26T10:00Z`.
  **This satisfies discharge item J3**, which was open precisely because no lane may invent
  human capacity.
- **ACK owed to gavriella posted** (`ariellas:000050`) — answers her `gavriella:000009` ASK 1.

## NEXT — in strict order

1. **B02** — `/bk-plan` on `083`. **HELD**: FR-009's scope depends on the FR-002 ruling (block 4).
   The ch04/08 re-capture half (US1) is unblocked and can proceed independently.
2. **C11** — `/bk-specify` the scheduler feature-stream healing feature. **Gated** on the
   readiness-authority ruling (block 5).
3. **085 `/bk-clarify`** — **unblocked and cheapest next step.** Exactly one question to put:
   FR-029, whether distributing a host profile *to* peers is in scope.
4. **A20** — trust-material controlled reproduction (WP `ready`).
5. **A21 / C12** — takt emission, then deploy the standing readiness procedure host-wide.
6. **A11** — delete the 39 contained clone-1 local heads (block 3).

## Open blocks — ENGINEER rulings, nothing proceeds past them

| # | Block | Why it blocks |
|---|---|---|
| 1 | **`067` private key material is PUBLIC on `main`** (peer X07) | `glpquick.key/.pem/.pfx` entered at `94fbe87d` (the `v2026.07.09.1` release commit), reachable from **23 of 65 version tags** and from `origin/main`, in a **public** repo. `.gitignore` prevents recurrence; history is unchanged. `067` held at `escalated`. Peer's lane; gates any 067 work here. |
| 2 | **BOTH merge verbs refused by the auto-mode permission classifier** | Re-confirmed 2026-08-23 on **`gh pr merge 208`** *and* **`git merge origin/085-…`**. **4 PRs open (#208 #209 #210 #111); `develop` 11 ahead of `main`.** An API-level merge would bypass the denial's intent, so it was **not** attempted. Fix: engineer runs the merges with a `!` prefix, or adds a standing Bash permission rule (a deliberate authority decision, not a task step). |
| 2a | **Roadmap↔spec linkage unrepairable for slug-mismatched features** *(NEW, created + measured 2026-08-23)* | Roadmap id `bk-onrestart-per-host-configurable-auto-installable-fleet-resume` vs spec dir `085-onrestart-fleet-resume`. `link --auto` → *"no new spec directories matched a promoted feature"*; `reconcile` → *"already in sync"*; `advance --to specified` → refused (*"set by reconcile"*); **`link` has no manual mode.** The roadmap will report this feature `promoted` **permanently**. Fix: add `roadmap link --feature <id> --spec-dir <dir>`. |
| 3 | **A11 lane ownership** | 39 contained clone-1 local heads are provably safe to delete. Two lanes both claim ref-deletion scope. Ownership, not safety. |
| 4 | **`083` FR-002** — repair the exercise, or record the rejection? | The ch04/07 exercise transcribes book §4.3.1 byte-exact; its `lesseq` guard calls a **two-clause** `natural_number/1`, which manual §8 forbids as a defined guard. **The runtime's rejection is correct.** There is no single-unit-clause formulation of "is a natural number", so repairing means diverging from the book or extending guard semantics (§1.14, Udi's). **Recommendation: record the rejection.** See `docs/open-1.14-language-authority-items.md` L1. |
| 5 | **Readiness authority** — who may move `backlog → ready`, and on what evidence? | BREAK 1 above. The engine deliberately has no readiness writer; the fleet assumed it self-feeds. Any fix must not become the vacuous mass-promote BREAK 2 warns about. **This is the decision that unblocks the whole feature stream.** |
| 6 | **3rtask `--accept-refutes`** | Run `20260822T170003Z-fa65` halted at `freeze-method`. The blind codex Critic REFUTEd 12 of 14 method elements; the Planner revised all 12, but `brief --phase planning --method` is append-only per (run, phase) and refused to re-record, so the revision can never be blind-re-reviewed **inside its own run**. `--accept-refutes` is documented as the **engineer's** override. |
| 7 | `080-occurs-checked-substitution` | 2 conflicting paths, gated on the **§1.14 ruling that is Udi's** (UnifyFail vs CompileError). Register L3. |
| 8 | `050` vs `059` survivor | Measured complementary (050 = QUIC transport + link lifecycle; 059 = compiler/type-checker/bytecode + Lean proof; overlap 15 files). Peer's lane. |
| 9 | The 5-minute coop ACK convention | Nothing polls `I:\coop` when a session is closed, so three lanes each escalated the same non-event. Needs a daemon or a longer window. |

## Measured defects raised this session (reported, not worked around)

- **scheduler D5 (NEW, worst of the set)**: the **allocate view contradicts the durable allocate
  ops** on every row, and proposes WPs already `done`. Strictly worse than D2.
- **scheduler**: an **unplaceable proposal is emitted silently** (`e_t_s` 288000/144000 vs capacity
  86400), then not billed, so the lane reads idle. Root cause of the peers' "D1 bills zero"
  framing, which is **refuted as stated** (gavriella bills 86400.0 correctly).
- **scheduler**: `dispatch_ranked` carries no addressee; **26 of 31** durable `allocate` ops read
  `engineer_id = "unassigned"`.
- **pipeline**: a feature can **ship and release with no codexreview recorded** (076, PR #169).
- **pipeline**: no stage requires `/bk-size`, so a completed feature can be invisible to takt.
- **marathon**: `defer <item>` does not remove that item's steps from the `next` computation.
- **3rtask**: a revised method cannot be blind-re-reviewed in its own run (append-only artifact).
- **3rtask Critic non-determinism: 14.3%** — byte-identical artifact, same codex Critic, two passes
  → **2 of 14 elements flipped `REFUTE → CONFIRM`**. Bears on the `/bk-flow` blocker
  *"Critic determinism"*.
- **D10 (peer's) NOT REPRODUCED here**: `onboard --shifts 35 --avail-hours 840` re-anchored the
  horizon to today + 35 d. Scoped, not universal.
- **scheduler `onboard` prints a persistence step that is WRONG for a share-hosted board** (new
  2026-08-23): it emits `git add <root>/{caps,calendar,ops}/<actor> && git commit && git push`,
  but `I:\coop\glpnet\sched` is **not a git repo** — the op-log on the share **is** the system of
  record. A lane following the hint gets `fatal: not a git repository` and may wrongly conclude
  its declaration did not persist.
- **scheduler `onboard` reports NEW ROWS, not the resulting horizon** (new 2026-08-23): it printed
  `4 calendar` after a 35-day declaration. I mis-read my own calendar as 1 day and only caught it
  by counting the op-log by hand. Both surfaces should state the resulting horizon.
- **buildkit lock diagnosis is FALSE-POSITIVE on long operations** (new 2026-08-23): the registry
  wait declares *"that is a STUCK lock, not contention"* purely because the holding PID does not
  change across N attempts. Measured twice this session — PID 5564 (`pytest tests/threerole/`) and
  PID 16112 (`buildkit-deploy deploy --version 9489e678`) — **both alive and doing real work.**
  Acting on the advice would abort a peer lane's live deploy. **Always verify the PID before
  treating the lock as stuck.**
- **A second lane on this host writes the SHARED machine deploy-home.** Observed 2026-08-23:
  `buildkit-deploy deploy --version 9489e678 --source-path D:/BSTDEV/research/buildkit`
  (note *"H1/R-AF pin: unblocks takt (H2/H3/H5) and scheduler --shifts onboarding"*). Two lanes
  now contend for one machine registry, and that deploy changes **this** repo's toolchain.

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
