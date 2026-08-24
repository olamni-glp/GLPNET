<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 0 — Research: 083 glptutorial corpus-golden reconciliation

**Date**: 2026-08-24 · **Branch**: `083-glptutorial-corpus-goldens` · **Lane**: `gavriella`

🔴 **Every finding below was re-measured on 2026-08-24, not inherited from the spec.** The spec's
figures were taken 2026-08-20; a measured claim older than the session is a hypothesis until re-run.

---

## R-1 · Baseline proposal set — **CONFIRMED UNCHANGED**

**Decision**: the spec's Problem table is current. Plan against exactly these four.

**Measured** — `codeconv tutorials propose` (read-only), 2026-08-24:

| kind | exercise | id |
|---|---|---|
| `layout_normalise` | ch04/07 | `spec-violation-ch04-ex07` |
| `stale_artefact` | ch04/08 | `stale-golden-ch04-ex08` |
| `drift_gap` | ch07 | `drift-gap-cssg` |
| `run_manifest` | ch07 | `run-manifest-ch07` |

**Rationale**: four proposals, same four ids, four days after the spec's baseline. SC-001's
baseline of 4 is sound.

**Alternatives considered**: treating the spec's count as given — rejected; this repo has been
burned three times by a stale measured claim carried forward as fact.

---

## R-2 · 🔴 THE DRIFT GUARD IS SATURATED — this reshapes US2

**Decision**: FR-004 and SC-003 **cannot be delivered as literally written**, and the plan must
say so rather than quietly redefining them.

**Measured** — `codeconv tutorials sync --check` on an **unmodified** tree, 2026-08-24:

```
exit = 1
67 drift lines, across ALL 13 chapters
```

| chapter | drift lines | | chapter | drift lines |
|---|---:|---|---|---:|
| ch06 | 7 | | ch09 | 2 |
| **ch07** | **5** | | ch10 | 2 |
| **ch04** | **5** | | ch11 | 2 |
| ch05 | 4 | | ch12 | 2 |
| ch03 | 4 | | ch13 | 2 |
| ch02 | 3 | | ch01 | 2 |
| ch08 | 2 | | (root `tutorial.md`) | 1 |

Two distinct drift classes appear: `vendored content differs from **sibling**` and
`vendored content differs from **manifest**`.

### Why this is a defect and not merely inconvenient

FR-004 requires that *"a modification to \[the ch07 substrate] causes `tutorials sync --check` to
fail and name the drifted path."* **It already fails.** A guard that is red before you touch
anything cannot distinguish *"I broke the substrate"* from *"it was already broken"* — its failure
carries **no information**. SC-003's *"an unmodified tree reports OK 100% of the time"* is, today,
**0% true**.

This is the same defect class the sibling feature 078 exists to eliminate: **a check whose result
does not depend on what it is supposed to be checking.** Vendoring `cssg_modules/` into a guard in
this state would produce exactly the outcome the spec's own Edge Case warns about — *"a guard that
passes while guarding nothing"* — except inverted: a guard that **fails while guarding nothing**.

**Rationale for the plan's response**: US2 must first make the guard *informative* for the
chapters in scope, then extend it. Concretely, the ch04 + ch07 drift (10 of 67 lines) must be
driven to zero and the guard must be able to report per-chapter, so that "ch07 is clean" is a
statement that can be made at all. The other 57 lines are **out of this feature's declared scope**
(ch04 and ch07 only) and must be reported, not silently repaired.

**Alternatives considered**:
- *Repair all 67* — rejected: out of scope (spec: "Chapters other than ch04 and ch07"), and it
  would bundle an unrelated 13-chapter sweep into a small feature.
- *Declare SC-003 met by scoping "the tree" to ch07* — rejected as written; that is redefining a
  success criterion to fit what the tooling can do, which is the error pattern this lane has
  already had to withdraw once. It is offered instead as an **explicit spec amendment** for the
  engineer (see the plan's Complexity Tracking).

---

## R-3 · ch07 substrate identity — **CONFIRMED `programs/cssg_modules/`**

**Decision**: vendor `programs/cssg_modules/` (5 files), not `programs/cssg_modules_v2/` (6 files).

**Measured**: both siblings exist. The spec's C1 resolved this by three independent corpus
references (`ch07-sources.md:43`, `ch07-sources.md:25`, `ch07_tutorial.md:5`), none of which name
`_v2`. The spec's Assumption *"to be confirmed at plan stage"* is hereby **confirmed**: file counts
differ (5 vs 6), so the two are not interchangeable and a wrong choice would be silently wrong.

**Alternatives considered**: vendoring both — rejected; it would make the guard ambiguous about
which substrate ch07 actually runs, defeating FR-005's determinism requirement.

---

## R-4 · The `propose` remedy text contradicts the spec — spec wins

**Measured**, verbatim from today's `propose` output for `drift-gap-cssg`:

> "Vendor cssg_modules/ **or** record a run-manifest."

The spec's C2 rules this "or" **wrong**: vendoring answers *"has the substrate changed?"*, the
manifest answers *"which program, play and limit does exercise MM resolve to?"*. FR-004 and FR-005
are **both** MUSTs.

**Decision**: implement both. **Additionally**, the tool's own remedy string is a live defect —
it advertises an either/or where the requirement is a conjunction. Correcting it is in scope under
FR-010 (documentation that conflates scope), and is cheap.

---

## R-5 · 🔴 SC-007's test baseline is STALE — do not gate on it as written

**Decision**: SC-007 must be re-based before it can gate anything.

| source | figure |
|---|---|
| spec SC-007 (dated) | 546 pass / 0 fail / 1 skip |
| **measured 2026-08-24** (`bash test/run_all_tests.sh`) | **561 total / 559 pass / 2 fail / 0 skip** |

The two failures are the known pre-existing `Section T` 064 service-box drills (`T-1` US1 resume,
`T-2` US2 history); they survive a rebuild and are **real**, not a stale binary. The suite has
grown by 15 tests since the spec was written and **no longer has a skip**.

**Rationale**: SC-007 says "remains green across the change (baseline: 546/0/1)". Taken literally
the criterion can never be met, because the suite is not at that baseline and is not green. The
honest form is *"introduces no new failure against the measured 561/559/2/0 baseline"*.

**Alternatives considered**: fixing the two Section T failures inside this feature — rejected;
they belong to 064 and are out of scope.

---

## R-6 · Delivery mechanism — the existing approval-gated flow

**Decision**: use `codeconv tutorials propose --apply --approve --rationale "<why>"`. No new
mechanism (spec Assumption, unchanged).

**Measured**: `tutorials` exposes `list · sync · preview · run · explain · propose`. `propose` is
read-only by default and states `--apply requires --approve + --rationale`, which is precisely
FR-006's "explicit approval and a recorded rationale". `sync` is the drift verb (FR-004).

**Open for Phase 1**: whether the rationale is stored where FR-006's *"recoverable from the corpus
afterwards"* holds, and whether FR-008's stale-vs-regression discriminator (C4: a re-capture must
cite the specific runtime change) has anywhere to live in the record. **Both are contract
questions, resolved in `contracts/`.**

---

## R-7 · FR-009 representation — rejection as a first-class outcome

**Decision**: the golden format must gain an explicit outcome kind for *"correctly refused"*.

**Rationale**: FR-002 was ruled **(b) record the rejection**, which makes FR-009 load-bearing:
ch04/07's golden must record a rejection, and a corpus that can only express `✓Loaded` has no way
to say that. Per the ruling the exercise source stays **byte-exact from book §4.3.1 p 37**.

**Alternatives considered**: encoding the rejection as free text in the existing golden —
rejected: FR-001 requires the comparison to be mechanical, and a free-text rejection cannot be
compared to a live outcome without parsing prose.

**Downstream, non-negotiable**: the ruling also confirms **B10** — report to Udi that a byte-exact
transcription of book §4.3.1 `lesseq` is **rejected** by the typed-GLP guard rules, because
`natural_number/1` is a two-clause procedure while manual §8 requires a defined guard to be a
single-unit-clause procedure. Per the Bug Protocol this is **reported, not silently fixed**.

---

## Unknowns remaining after Phase 0

**None blocking.** FR-002 is ruled, FR-009's condition is discharged, C1 is confirmed by
measurement. Two items are raised to the engineer as **spec amendments**, not as blockers — they
are carried in the plan's Complexity Tracking and do not stop tasks:

1. **SC-003 / FR-004** cannot mean "unmodified tree exits zero" while 57 out-of-scope drift lines
   exist (R-2).
2. **SC-007**'s baseline is stale and must be re-based to 561/559/2/0 (R-5).
