# Fleet standard report formats — roadmap tables and marathon sitrep

**Status:** proposed standard, authored on GAVRIELLA 2026-08-23 · **Scope:** every host
(`ariellas`, `gavriella`, `olamnit`, `shiras`) and every repo that runs buildkit.
**Why:** the directive asks for the roadmap listing and the marathon sitrep *"in standardised form
across all hosts and repos."* Today each lane emits its own shape, so figures cannot be compared or
folded without re-deriving them. This file is the shape.

> **Adoption is advisory.** Nothing here changes a tool. It is a reporting contract a lane can
> follow today by hand and a tool can emit later.

---

## Rule 0 — the three rules that make any of these comparable

These bind every table and every sitrep below, and they are the ones this fleet has repeatedly
broken:

1. **Name the ref or the root with every count.** "18 branches" is meaningless; "18 `origin` heads"
   and "5 local heads" are different numbers about different objects. Same for boards: name the
   `sched_root`, never "the board".
2. **Date-stamp every measured claim.** A claim older than the current session is a *hypothesis*
   until re-run. Mark re-used figures with the timestamp they were taken at.
3. **State coverage as a fraction, never as "all".** `54 of 54 op-logs`, `7 of 52 engine files`.
   A bare "all" is unverifiable and usually false.

Two derived rules that have each cost this fleet a wrong decision:

4. **Quote the conflict count, never the files-differing count.** `filesdiff` measures how far the
   integration branch has moved since the branch was cut, not how hard the merge is.
5. **A corroboration count is not evidence unless authorship was verified.** See the note under the
   sitrep's Evidence block.

---

## 1. Roadmap — features not closed

One row per feature. Sorted by `wsjf` descending, then `feature_id`.

| Column | Meaning | Rule |
|---|---|---|
| `repo` | repo short name | required — makes rows foldable across lanes |
| `host` | lane that owns the row today | `unowned` if none; never blank |
| `#` | rank in this table | positional only, not an id |
| `feature_id` | roadmap slug | the join key **across hosts**; never the spec-dir number |
| `state` | `captured`/`refined`/`promoted`/`specified`/`implemented`/`reviewed`/`shipped`/`released` | roadmap state, not pipeline state |
| `wsjf` / `rice` | scores | `—` when unscored; never 0 |
| `epic` | parent epic name | `(standalone)` when none |
| `spec_path` | `specs/<NNN>-<slug>` | `—` when absent — **and an absent spec on a `specified` row is a defect, report it** |
| `blocked_by` | feature_ids | comma-separated; `—` when none |
| `stalled_days` | days since the row last changed state | flags parked work that reads active |

**Fold rule.** Two rows from different hosts are the same feature iff `feature_id` matches exactly.
Never join on the numeric prefix — this repo has carried duplicate feature numbers and a
slug-vs-spec-dir mismatch.

## 2. Roadmap — epics

| Column | Meaning |
|---|---|
| `repo` · `epic_id` · `name` | identity |
| `features_total` | features under the epic |
| `features_not_closed` | the number that matters |
| `oldest_open_days` | age of the oldest non-closed child |

Epics with `features_not_closed = 0` collapse to a single summary line; do not list them.

## 3. Marathon sitrep

Fixed section order, so a reader diffing two lanes' sitreps compares like with like.

```
## HEADER
repo · host · run_id · feature_id · seq · steps done/total · points delivered/remaining
plan file (authoritative content) · working tree clean? · unpushed count · branch @ sha

## GATE
suite result as Total/Passed/Failed/Skipped/Unsearchable + the date it was RUN
known-failure list, each marked REAL or STALE-ARTIFACT with how that was established

## DELIVERED THIS SESSION
one line per item: ID · what · SIZE(points) · receipt (sha / tag / url)
"receipt" is mandatory — an item with no receipt is not delivered

## BLOCKED
one line per block: ID · owner (ENGINEER|PEER|PERMISSION|TOOL) · points · one-line why
sub-total blocked points by owner

## NEXT
the next 3-5 step ids in dependency order, with sizes

## CORRECTIONS
anything this session WITHDREW or re-measured, with both readings and their timestamps
an empty Corrections block is suspicious in a long session; say "none" explicitly

## EVIDENCE CAVEATS
any figure whose basis is not fully trusted, and why
```

**Sizes are the canonical scale everywhere:**
`nano 1 · micro 3 · mini 7 · midi 11 · maxi 17 · saga 35`.
Report **points**, never steps-done/steps-total — step boards are grow-only with no delete verb, so
a ratio derived from them is wrong by construction wherever a mis-minted step exists.

**On corroboration counts in the Evidence block.** Measured on this host 2026-08-23: the 3rtask
merge's default `key_mode="concept"` union-finds related claims and **unions the builder lists**, so
a row renders one author's text beside the whole cluster's authorship. Verified by exact-text
membership, **12 of 12 "corroborated" rows carried a builder who did not write that text**. Until
that is fixed, a sitrep must either omit corroboration counts or mark them
`unverified-authorship`.

## 4. Blocked-owner vocabulary

Fixed set, so blocked points fold across lanes:

| Owner | Means | Who can clear it |
|---|---|---|
| `ENGINEER` | needs a human ruling | Gabi |
| `PEER` | another lane owns the artifact | that lane |
| `PERMISSION` | refused by the environment/classifier | Gabi, by running it or granting the rule |
| `TOOL` | a tool defect blocks the path | whoever owns the tool repo |
| `SELF` | this lane can do it and has not yet | this lane |

Anything not in this set is a reporting bug, not a new category.

---

*Proposed by the `gavriella` lane, glpnet, marathon `mrun-20d9230f767b`. Adopt, amend or reject —
but if a lane amends it, amend this file rather than diverging silently, or the fold breaks again.*
