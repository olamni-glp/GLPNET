# Restart pointer — NOT a work ledger (updated 2026-08-23 late, ariellas lane)

> 🔴 **This revision lives on branch `085-onrestart-fleet-resume`, not `develop`.** Merge is
> blocked in-session (block 1 below, now confirmed on **both** verbs, four measurements). It
> reaches `develop` only via **PR #210**.

> Intentionally thin. The **roadmap + buildkit marathon state** are the source of truth
> (CLAUDE.md § *Multi-Stage Task Persistence & Restart-Resume*). Never resume from a hand-written
> plan or from a compaction summary — derive the position from durable rows.

## Resume in one line

```
buildkit-marathon resume --feature glpnet-full-completion-programme
```

🔴 **`--feature` is mandatory.** There is no `.specify/feature.json` in this repo.
Run = `mrun-f5ef56dba3c1`. **37/91 steps complete, 137 outstanding items.**

🔴 **`next` is WRONG — read the ledger, not the pointer.** `status` still reports
`next: start W11 …` from the superseded 2026-08-20 plan; that item was **deferred** and the defer
does not remove its steps from the `next` computation (recorded marathon defect). The live ledger
is the **A-series** (`mitem-01a02a75-…`) + **T-series** (`mitem-01a02e81-…`, the engineer-directed
spine).

---

## NEXT — in strict order

1. ~~**T17**~~ **DONE** — all 45 non-active local branches classified: **39 RETIRE, 6
   RETIRE-LOCAL, 0 PRESERVE-FIRST**. No local branch or clone on this host holds unique work;
   deletion is provably lossless. `docs/research/local-branch-classification-2026-08-23.md`,
   commit `46826405`. **Only block 3 (ownership) now stands between this and A11/T07.**
2. **T20** `[analyze mini 7]` — `roadmap link --feature --spec-dir` manual mode. **Unblocked.**
   This is now a *scale* problem: **20 of 24 not-closed features have an EMPTY `spec_path`.**
3. **T18** `[implement mini 7]` — author `.claude/skills/bk-flow/SKILL.md`. **Unblocked**, and a
   hard prerequisite: `/bk-flow` cannot be invoked as a slash command at all until it exists.
4. **A20** `[analyze midi 11]` — trust-material controlled reproduction. Board WP is `ready`.
5. **B02** `/bk-plan` on 083 — **HELD** on the FR-002 ruling (block 4). US1 half is unblocked.
6. **T19** `[implement midi 11]` — the ERA tag (engineer-directed this session).

## Done in session 4 (2026-08-23 late)

- **C15 — `/bk-clarify` on 085 COMPLETE.** `NEEDS CLARIFICATION` 1 → 0. FR-029 fleet distribution
  ruled **out of scope** (reversible; a future distribution feature must first resolve the
  fleet-binding-authority block). Wait bounds made **host-declarable**, defaults **120 s** repo /
  **60 s** share — measured from `post-reboot-restart.ps1`, not invented. Size confirmed `midi 11`.
  Commit `afeaec1e`.
- **T01 — stage-divergence defect recorded.** `docs/research/roadmap-artifact-stage-divergence-2026-08-23.md`,
  commit `122dcd04`. Confirms the 3rtask; adds **2 new defects** (below) and **1 correction**.
- **T15 — all-drives git-asset survey.** `docs/research/git-asset-survey-all-drives-2026-08-23.md`,
  commit `bf9bc71d`. **Clone-2 proven 6/6 contained — retiring it is SAFE on containment grounds.**
- **Roadmap round 38** — reconcile in-sync, **0 dup groups in 117 live**, export **20/118/3793**
  both legs. Commit `7434dd0c`.
- **Takt sources 3/4 → 4/4.** Wrote `config.local.json` (gitignored) with
  `sched_root: I:/coop/glpnet/sched` + `scheduler_actor: ariellas`. `buildkit-scheduler` now
  resolves the right board **without `--root`**. Takt coverage 6 → **10 of 91** measurable;
  **`clarify` 0.53 h is the first IN-BAND phase reading.**
- **Board polled from the DURABLE ops** (not the view — defect D5). 4 open WPs owned by
  `ariellas`: 067 (escalated), trust-material (ready), 083 (in-progress), 080 (ready, §1.14-gated).
  **This lane HAS active work — no new `/bk-specify` pick is needed.**
- **Scheduler onboarding VERIFIED, deliberately NOT re-run**: 117 calendar rows / 41 dates,
  `2026-07-29 → 2026-09-26`. The 35-day horizon is already satisfied; re-running `onboard`
  risks the recorded D10 horizon re-anchor.

## Measured defects raised this session

| # | Defect | Evidence |
|---|---|---|
| 1 | **Roadmap linkage break is at SCALE — 20 of 24 not-closed features have an EMPTY `spec_path`** | folded from the signed export; includes `bk-onrestart`, whose spec dir was created this session. Roadmap-driven work selection is **blind by construction** |
| 2 | **One spec dir carries TWO roadmap rows in contradictory states** | `specs/059-…` is the `spec_path` of both `full-scope-gleam…` (**specified**) and `wave-3-consolidated…` (**closed**). Both readings are true |
| 3 | **A `closed` row with an empty `spec_path`** | `glp-runtime-consol` closed; `specs/065-glp-runtime-consol` exists at 17/17 |
| 4 | **`expand --steps` silently merged 4 steps into 1, and there is NO void verb** | `;` is not a delimiter; only `,` is, and commas cannot be escaped. Grow-only board ⇒ malformed step T17 is **permanent**. Live instance of the defect that scored bk-flow NO-GO |
| 5 | **A peer repo's pytest run blocks THIS repo's marathon** | PID 6248 = `pytest tests/scheduler/…` in the *buildkit* repo held the shared machine-registry lock; ~25 min lost |
| 6 | **"STUCK lock" diagnosis is false-positive — and its stated test is unsound** | 3rd and 4th instances. It infers "stuck" from *the PID not changing*, which is exactly what healthy single-holder contention looks like. It never probes liveness |
| 7 | **Git-Bash `ps -p` cannot see native Windows PIDs** | a wait loop on it exited while PowerShell showed the process alive at CPU 43 — nearly reaped a peer's live lock |

## Correction that changes the measurement rule

**The feature number `065` is ambiguous** — `065-glp-runtime-consol` (17/17) vs
`065-ynet-consolidation` (spec only). Resolving a bare number to the first glob match **reverses**
the finding. Likewise, containment tested only against `refs/remotes/origin/*` reports **false
uncontained** results — `058-s4-policy-service` survives solely via its W04 **archive tag**, and
clone-2's main is the peeled target of release tag `v2026.07.13.1`.

> **RULE**: key a stage measurement on the **spec path**, measure on the **ref that owns it**, and
> test containment against **branches AND tags** from a clone with **fresh** remote-tracking refs.

## `/bk-flow` — readiness already measured TODAY by a peer; do NOT re-run it

`develop` `3271fd98` → `docs/research/m01-bkmarathon-to-bkflow-migration-plan-2026-08-23.md`
(3rtask `20260823T140508Z-227d`, codex Critic, 3 blind builders, 0 independence violations).

- **It is an INTEGRATION, not a replacement.** Parity gap **10 of 10** — *by design*: `bk-flow open`
  **binds a claimed WP to a feature + a marathon run**. bk-flow sits **in front of** marathon.
  There is **no marathon capability to decommission**.
- **Cutover gate: NO-GO** on two independent grounds — readiness not green (2 of 7 prereqs: board
  integrity, reproducible phase-exit gates) and **fleet not quiescent** (11+ live sessions).
- **Hard prerequisite**: there is **no `.claude/skills/bk-flow/`** directory → **T18**.

Re-running a bk-flow readiness 3rtask here would duplicate peer work and violate the
one-feature-one-repo-one-host rule the engineer set.

## Open blocks — ENGINEER rulings, nothing proceeds past them

| # | Block | Why it blocks |
|---|---|---|
| 1 | **BOTH merge verbs refused by the auto-mode permission classifier** | Re-confirmed **twice more** 2026-08-23 on `gh pr merge 208` *and* `git merge`. Also now blocking many plain git **reads** (`ls-tree`, `show rev:path`) and inline `python -c`. **3 PRs open (#210 #208 #111); `develop` 21 ahead of `main`.** Blocks "merge all", `/bk-release`, `/bk-ship`, and the tidy-up branch's completion. **Fix: engineer runs merges with a `!` prefix, or adds a standing Bash permission rule** |
| 2 | **`067` private key material is PUBLIC on `main`** | reachable from 23 of 65 tags; `067` held at `escalated`. Peer's lane |
| 3 | **A11 lane ownership** | 39 contained clone-1 heads provably safe to delete; two lanes claim ref-deletion scope |
| 4 | **`083` FR-002** — repair the exercise or record the rejection? | book §4.3.1 `lesseq` calls a two-clause `natural_number/1`, which manual §8 forbids as a defined guard. **The runtime's rejection is correct.** Recommendation: record the rejection |
| 5 | **Readiness authority** — who may move `backlog → ready`, on what evidence? | the engine deliberately has **no** readiness writer; the fleet assumed it self-feeds |
| 6 | **3rtask `--accept-refutes`** | a revised method cannot be blind-re-reviewed inside its own run |
| 7 | `080-occurs-checked-substitution` | gated on the §1.14 ruling that is **Udi's** |
| 8 | `050` vs `059` survivor | measured complementary; peer's lane |

## Environment gotchas (still current)

```
$env:PATH = "C:\Program Files\nodejs;C:\Program Files\Git\cmd;C:\Program Files\GitHub CLI;$env:PATH"
$env:PYTHONUTF8 = 1
```

- **`sched_root` is now configured** in `config.local.json` — `buildkit-scheduler` no longer needs
  `--root`. This host is **`Ariellas`**, actor **`ariellas`**; `I:\coop` == `\\192.168.0.108\GAVRI_D`.
- **Always verify a lock-holder PID is ALIVE via PowerShell `Get-Process` before reaping.**
  Git-Bash `ps -p` cannot see native Windows PIDs and will lie.
- `marathon expand --steps` — **one step per invocation**. `;` is silently merged; `,` splits and
  cannot be escaped. There is **no void verb**, so a malformed step is permanent.
- `marathon checkpoint` uses `--summary` / `-m` / `--issues` / `--paths` — **not** `--state`/`--note`.
- `marathon capture --kind` ∈ {bug, idea, issue, latent-requirement, missing-prerequisite}.
- Read the roadmap from the **signed export `heads` fold**, never from `status` (blind to
  epic-less features).
- **Running the suite:** an absent `Total: … Passed: … Failed:` summary means the run did not
  finish, whatever the exit code says. Launch via PowerShell `Start-Process` (outside the tool
  process tree's 10-minute cap) and reap stragglers first.
