<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Standardised cross-host SITREP + roadmap table format

**Purpose**: every host (`ariellas`, `gavriella`, `olamnit`, `shiras`) and every repo emits the
same two tables, so a reader can diff two hosts without re-deriving either. Adopted 2026-08-23.

🔴 **Every field below must be MEASURED from a durable source. No field may be an estimate.**
Where a value cannot be measured, write `unmeasurable` — never `0`, never a guess.

---

## Table A — SITREP header (one row per repo lane)

| Field | Source of truth (how to measure) |
|---|---|
| `host` | the machine's actor id — `ariellas` \| `gavriella` \| `olamnit` \| `shiras` |
| `repo` | repo directory name |
| `branch` | `git branch --show-current` |
| `run_id` | `buildkit-marathon resume --feature <f>` → `run` |
| `steps` | `<done>/<total>` from the same line |
| `outstanding_items` | same line |
| `board_root` | `buildkit-scheduler root` (must print `exists=True`) |
| `wp_open_here` | count of durable ops under `<root>/ops/<actor>/*.jsonl` where the last `allocate`/`claim` names this actor and last `transition.to_state` ∉ {done} |
| `prs_open` | `gh pr list --state open` |
| `develop_ahead_of_main` | `git rev-list --count origin/main..origin/develop` |
| `blocks_open` | count of engineer-ruling blocks in that repo's `docs/current_plan.md` |

## Table B — roadmap: every epic and feature NOT closed

🔴 **Fold the signed export's `heads` list. Do NOT use `buildkit-roadmap status`** — status is
blind to epic-less features and under-reports (it showed 99 of 115 when the true figure was
different).

```
buildkit-roadmap export
# then fold: heads[] where entity_kind == 'feature' and state != 'closed'
```

Columns, in this order: `# | state | epic | feature slot | spec_path`.
Sort by `state`, then `epic`, then `slot`. Report the state counts above the table.

## Table C — takt (per-phase and per-feature)

`buildkit-marathon takt --feature <f>` — report `n / p50 / p80 / max / band / verdict` per phase,
plus the feature total, plus **`measurable / total` steps and `sources: k/4`**.

🔴 **Unmeasurable steps must be stated as a count, never folded in as zero.**
🔴 **The only permissible durations are the generic takt range or a size-adjusted estimate
computed from ACTUAL measurements. An LLM estimate is never permitted.**

Bands: phase **0.5–3.0 h**; feature (era) **1.5–6.0 h**.
Sizes: `nano 1 · micro 3 · mini 7 · midi 11 · maxi 17 · saga 35`.

## Table D — what's next

`rank | step | size | state | blocked-by`. `state` ∈ {unblocked, held, gated}.
A `held`/`gated` row **must** name the block it waits on. Never list a blocked step as next
without naming its blocker.

---

## Known measurement traps (apply on every host)

1. `marathon resume`'s `next:` field can be **stale** — a `defer`ed item's steps are not removed
   from the `next` computation. Read the live ledger item, not `next`.
2. A **bare feature number is not an identifier** — `065` resolves to two spec dirs that answer
   the stage question differently. Key on `spec_path`.
3. Measure a feature's stage **on the ref that owns its spec dir**. `066`/`067` have no spec dir
   on `develop` or `main`.
4. Test ref containment against **branches AND tags** after `fetch --prune --tags`. A
   `refs/remotes`-only test yields false "uncontained".
5. Read the scheduler's **durable ops**, not `views/` — the allocate view contradicts the durable
   allocate ops and re-proposes from scratch each cycle.
6. Verify a lock-holder PID is **alive** (`Get-Process`, sampling CPU twice) before believing the
   "STUCK lock" message. Git-Bash `ps -p` cannot see native Windows PIDs.

---

# Merged in from the `gavriella` lane, 2026-08-23

*Two lanes authored a cross-host standard within minutes of each other (`ariellas`
`docs/SITREP-FORMAT.md`, `gavriella`
`docs/research/fleet-standard-report-formats-2026-08-23.md`). Two standards is the fork a standard
exists to prevent. **This file is canonical**; the other is now a pointer. Everything above is
ariellas' and is unchanged. Everything below is the gavriella lane's unique content, merged in.*

## 🔴 The ERA definition (ENGINEER RULING, 2026-08-23)

> **An ERA is a synonym for a FEATURE:**
> `/bk-specify` → `/bk-clarify` → `/bk-plan` → `/bk-tasks` → `/bk-analyze` → `/bk-implement` →
> `/bk-codexreview` → `/bk-ship` → `/bk-close`
> **Opens at `/bk-specify`. Closes at `/bk-close`, after the feature has shipped.**

Not a marathon run, step, step aggregate, phase, session or wave. **Era ≡ feature, whole.**
**An era must not be decomposed into summarised atoms that lose the feature's functional identity.**
The nine stages above are the canonical `step-start --phase` vocabulary.
Full text: `docs/research/ENGINEER-RULING-era-is-a-feature-2026-08-23.md`.

## Three rules that make any two lanes' figures comparable

1. **Name the ref or the root with every count.** "18 branches" is meaningless; "18 `origin` heads"
   and "5 local heads" are different numbers about different objects. Same for boards: name the
   `sched_root`.
2. **Date-stamp every measured claim.** A claim older than the current session is a **hypothesis**
   until re-run. Mark re-used figures with the time they were taken.
3. **State coverage as a fraction, never "all"** — `54 of 54 op-logs`, `7 of 52 engine files`.

## Table B addendum — *why* the export fold, and when it must be taken

Table B already mandates the export fold. **Take it AFTER the `sync` import leg**, for a second and
stronger reason than the epic-less one:

**Measured 2026-08-23:** a peer pushed *"round 40 — 6 features linked to spec dirs"*. `reconcile`
was run **twice** and said **"already in sync with pipeline (no changes)"** both times; the export at
that moment folded to `promoted 21 / specified 3 / analyzed 1`. After `sync --round 41` (whose
**import** leg applies peer state) the next fold gave `promoted 15 / specified 8 / analyzed 1 /`
**`implemented 1`** — **six features had moved**. `reconcile` compares only against the **local**
pipeline; peer state arrives **only** via the import leg. A green `reconcile` is **not** evidence of
currency, and this cost a wrong table: membership was right, **six states were wrong**.

*(The epic-less rationale did not reproduce here — `status` and the export fold both returned 25,
with 12 of the 25 being epic-less. The import-currency reason is the load-bearing one.)*

## Fold rule — how two lanes' rows are matched

Two rows from different hosts are the same feature **iff `feature_id` matches exactly**. **Never
join on the numeric prefix** — this repo has carried duplicate feature numbers, and trap 2 above
records that a bare number resolves to two spec dirs.

## Blocked-owner vocabulary (fixed set, so blocked points fold)

| Owner | Means | Who clears it |
|---|---|---|
| `ENGINEER` | needs a human ruling | Gabi |
| `PEER` | another lane owns the artifact | that lane |
| `PERMISSION` | refused by the environment/classifier | Gabi, by running it or granting the rule |
| `TOOL` | a tool defect blocks the path | whoever owns the tool repo |
| `SELF` | this lane can do it and has not yet | this lane |

Anything outside this set is a reporting bug, not a new category.

## Two SITREP sections that must never be omitted

- **CORRECTIONS** — anything the session **withdrew or re-measured**, with *both* readings and their
  timestamps. An empty CORRECTIONS block in a long session is suspicious; write "none" explicitly.
- **EVIDENCE CAVEATS** — any figure whose basis is not fully trusted, and why.

## Report POINTS, never steps-done/steps-total

Step boards are **grow-only with no delete verb**, so any ratio derived from them is wrong by
construction wherever a mis-minted step exists. Measured here: 4 completed steps could not be
checkpointed at all because **no verb lists steps** and the run mirror was ~17 h stale, so the board
under-reports permanently. Sizes are the canonical scale (already given above).

## Two more measurement traps

7. **Quote the conflict count, never the files-differing count.** `filesdiff` measures how far the
   integration branch has moved since the branch was cut, not how hard the merge is. Measured: two
   branches recorded as *"3786 and 4007 file conflicts"* actually had **6 and 6**. And a low conflict
   count says a merge is **easy**, never that it is **desirable** — **read the branch's commit
   subjects first**; two such branches would have regressed the toolchain by six weeks.
8. **A corroboration count is not evidence unless authorship was verified.** Measured 2026-08-23:
   3rtask's default `key_mode="concept"` union-finds related claims and **unions the builder lists**,
   so a row renders one author's text beside the whole cluster's authorship. Checked by exact-text
   membership, **12 of 12 "corroborated" rows carried a builder who did not write that text**. Until
   fixed, mark corroboration counts `unverified-authorship` or omit them.

9. **`git pull --rebase` silently drops an unpushed MERGE commit** — no warning, exit 0. Measured
   here: a merge reported DONE was rewritten out of existence and its receipt sha went dead; the
   content survived only because a peer landed it independently. Push a merge before pulling, or use
   `--rebase-merges`. **Verify a receipt by content, never by the sha you were handed.**

---

# Merged in from the `gavriella` lane / `olamnit-assistant` repo, 2026-08-23T20:4xZ

Four items measured on host GAVRIELLA while driving `mrun-eae934194c04`. Added here rather than in a
third file, per the unification rule.

## ⚠ Trap 10 — a takt `verdict` is recomputed AT READ TIME, so takt history is not a record

`verdict` is evaluated against whatever `takt-target` bands are current when you *read* it. It is
**never stamped at completion**. Measured on the same 21 steps, one day, **no step worked between
any of the three reads**:

| Read | `under` | `in-band` | what changed |
|---|---:|---:|---|
| A | **20** | **1** | baseline, no targets set |
| B | **10** | **11** | bands set |
| C | **20** | **1** | bands realigned to the ERA targets (1.5–6 h feature / 30 min–3 h phase) |

**Consequences.** Changing a band silently rewrites the verdict of every step ever completed,
including other sessions' work. No takt count is comparable across lanes unless both quote their
bands. A lane reporting *"11 in-band"* is not lying — the number is simply not about the work.

**Rule: quote the bands with every takt figure, or omit the figure.** Stamping at completion has
been requested of the buildkit lane; until it lands this trap is permanent.

## ⚠ Trap 11 — `BUILDKIT_ENGINE_OVERRIDE=ambient` degrades SILENTLY under a registry lock

Measured: the identical `buildkit-roadmap --json reconcile` returned the full `pipeline_binding`
report at 16:4xZ and a bare `{"reconciled": []}` at 17:3xZ — **no error, still exit 0** — while a
live peer held the deploy-home registry. The override simply stopped applying.

**Rule: check for the expected KEY in the output, never the exit code.** The proven route that works
even while the registry is locked is to bypass engine resolution entirely:

```python
import sys; sys.path.insert(0, r"<buildkit>/src")
from buildkit_cli.roadmap import store; store.pipeline_binding_report()
```

*(This is the environment half of your trap 6. Independently reproduced here on **PID 24936** — the
same PID your unification note cites — alive, CPU climbing 60 s → 81 s, exited on its own.)*

## ⚠ Trap 12 — "the catalog" is not one thing: the roadmap half converges, the pipeline half never does

`pgdb/` is in-repo and git-ignored, so **every clone and worktree carries its own catalog**. But the
two halves behave differently, and conflating them produces false disagreements between lanes:

| Half | Replicated by | Comparable across lanes? |
|---|---|---|
| `roadmap_*` | export / import / sync | **yes** |
| `pipeline_stage` | **nothing** — written only by the stage skills of the lane that ran them | **no** |

Measured: two lanes reported `13` and `19` distinct pipeline ids (both correct, different checkouts)
while **both** reported `roadmap_without_spec_path: 43` — identical, because that half syncs.

**Rule: a roadmap count may be quoted fleet-wide; a PIPELINE-derived count may not** — including
`bound N of M`, since `reconcile` joins across both halves. Name the checkout for pipeline figures.

## ⚠ Trap 13 — restart-document version suffixes are PER-LANE, not a fleet sequence

`RESTART-PREP-v3-olamnit-lane.md` is **not** newer than `RESTART-PREP-v2-resume-marathon.md` — they
are different runs on different hosts (`mrun-d7d0c6d4758f` vs `mrun-eae934194c04`). One repo held
**twelve** restart/sitrep documents from **three** lanes.

**A session told to "read the newest restart prep" resumes another host's work.** This nearly
happened here.

**Rule: every restart document names its `run id`, `lane`, `host` and `repo` in its first table, and
no reader ever selects by filename.** This is `filename-is-not-content` with a version suffix
attached.
