<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: glptutorial corpus-golden reconciliation (stale goldens + drift-guard vendoring)

**Feature Branch**: `083-glptutorial-corpus-goldens`
**Created**: 2026-08-20
**Status**: Draft
**Input**: User description: "glptutorial corpus-golden reconciliation (stale goldens + drift-guard vendoring)"

**Roadmap**: `glptutorial-corpus-golden-reconciliation-stale-goldens-drift-guard-vendoring` (WSJF 6.50, RICE 1700, rank 3, effort small, risk low)
**Board**: WP `wp-glptutorial-corpus-golden-reconciliation-stale-goldens-drift`, allocated to `ariellas` by op `ariellas:000035`

## Problem

The tutorial corpus is used as a regression oracle: an exercise is run, and its recorded
outcome ("golden") is compared against the live runtime. **Four of the corpus's truth
artefacts no longer tell the truth**, so the oracle currently asserts falsehoods and the
drift guard cannot see part of the corpus at all.

Measured 2026-08-20 by `codeconv tutorials propose` (read-only), which reports **four**
proposals — one more than the roadmap brief records:

| # | Kind | Exercise | Defect as measured |
|---|---|---|---|
| 1 | `layout_normalise` | ch04/07 | Exercise uses multi-clause `natural_number/1` as a guard — **spec-invalid** per the typed-GLP manual §8. The live runtime correctly **rejects** it, but the golden records `✓Loaded`, captured from a stale build. |
| 2 | `stale_artefact` | ch04/08 | `flatten` golden predates the C# `is_list` guard fix: it shows a `[WARN]` and an un-flattened result. The live Dart/C# oracle now yields `F=[5,4,3,2,1]`. |
| 3 | `drift_gap` | ch07 | ch07 runs the live sibling `programs/cssg_modules/`, which is **not vendored**, so `tutorials sync --check` cannot guard it. |
| 4 | `run_manifest` | ch07 | No explicit `exercise-MM → (programs/cssg_modules, fplayMM, :limit)` manifest exists, so the use-case mapping is neither deterministic nor drift-checkable. |

Defect 1 is the sharpest: **a golden that asserts a spec-invalid program loads successfully.**
Anyone trusting the corpus would conclude the runtime accepts something the manual forbids.
This is the same defect class this repo keeps hitting — a check that reports success without
having actually verified anything.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The corpus stops asserting falsehoods (Priority: P1)

An engineer runs the tutorial corpus as a regression oracle after changing the runtime. Every
recorded outcome either matches the live runtime, or is flagged as a real regression. No
recorded outcome disagrees with the live runtime for a reason that is merely historical.

**Why this priority**: Until this holds, every other use of the corpus is unsound. This story
alone is a viable MVP — it restores trust in the oracle without touching the drift guard.

**Independent test**: Run every ch04 exercise against the live runtime and compare to its
golden. Zero unexplained mismatches.

**Acceptance Scenarios**

1. **Given** ch04/07's exercise as it stands, **When** it is loaded by the live runtime,
   **Then** the recorded golden agrees with what the runtime actually does — whether that is
   acceptance of a corrected exercise or a recorded rejection of the current one.
2. **Given** ch04/08's `flatten` exercise, **When** it is run against the live Dart and C#
   oracles, **Then** the golden records `F=[5,4,3,2,1]` with no `[WARN]`.
3. **Given** any repaired golden, **When** `codeconv tutorials propose` is re-run,
   **Then** that exercise no longer appears as a proposal.

### User Story 2 - The drift guard can see the whole corpus (Priority: P2)

An engineer changes `programs/cssg_modules/` and expects the corpus drift guard to notice.

**Why this priority**: Independently valuable and independently testable, but the corpus is
worth guarding only once it is truthful (US1). Delivers no value if US1 is skipped.

**Independent test**: Modify the ch07 substrate, run `tutorials sync --check`, and observe a
non-zero exit naming the drift.

**Acceptance Scenarios**

1. **Given** the ch07 substrate is covered, **When** `tutorials sync --check` runs against an
   unmodified tree, **Then** it exits zero and reports OK.
2. **Given** the ch07 substrate has been modified, **When** `tutorials sync --check` runs,
   **Then** it exits non-zero and names the drifted path.
3. **Given** ch07's exercises, **When** the run mapping is consulted, **Then** each exercise
   resolves deterministically to its program, play and step limit.

### User Story 3 - Repairs are proposed, approved, and recorded (Priority: P3)

Every repair goes through the existing approval-gated flow rather than by hand, so the corpus
carries a record of why each golden changed.

**Why this priority**: Process integrity. The repairs are correct without it, but unattributed.

**Acceptance Scenarios**

1. **Given** a proposal, **When** it is applied, **Then** it required an explicit approval and
   a recorded rationale.
2. **Given** an applied repair, **When** the corpus is inspected later, **Then** the rationale
   is recoverable.

### Edge Cases

- A golden's live outcome is **itself** wrong (a real runtime regression). Re-capturing would
  silently bless a bug. Re-capture must be distinguishable from repair.
- ch04/07's live outcome is a **rejection**. A corpus that records only successful loads has no
  representation for "correctly refused" and would need one.
- The ch07 substrate has a `programs/cssg_modules_v2/` sibling; vendoring the wrong one would
  produce a guard that passes while guarding nothing.
- Re-running `propose` after a partial repair must show exactly the unrepaired remainder —
  never a clean report.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST report every corpus artefact whose recorded outcome disagrees
  with the live runtime, and MUST NOT report a clean corpus while any such artefact exists.
- **FR-002**: The ch04/07 spec-violation MUST be resolved such that the recorded outcome and
  the live runtime agree. [NEEDS CLARIFICATION: repair the exercise to a single-unit-clause
  guard, or keep the exercise and record its rejection as the golden? The exercise is a
  `.glp` file in the tutorial corpus; per CLAUDE.md, `.glp` files authored by the engineer
  must not be modified without express approval.]
- **FR-003**: The ch04/08 `flatten` golden MUST record the live oracle's result
  (`F=[5,4,3,2,1]`, no `[WARN]`) for both the Dart and C# backends.
- **FR-004**: The ch07 substrate MUST be covered by the drift guard, such that a modification
  to it causes `tutorials sync --check` to fail and name the drifted path.
- **FR-005**: Each ch07 exercise MUST resolve deterministically to its program, play and step
  limit through a recorded manifest.
- **FR-006**: Applying any repair MUST require an explicit approval and a recorded rationale,
  and MUST leave that rationale recoverable from the corpus afterwards.
- **FR-007**: Re-running the proposal report after a repair MUST show exactly the unrepaired
  remainder.
- **FR-008**: Where a golden is re-captured rather than repaired, the record MUST distinguish
  "the golden was stale" from "the runtime changed behaviour", so that re-capture can never
  silently bless a regression.
- **FR-009**: The corpus MUST be able to represent an exercise whose correct outcome is
  rejection, not only successful loading.
- **FR-010**: The Issue-10 headline documentation, which conflates approved scope with pending
  repairs, MUST be corrected to match the delivered scope.

### Key Entities

- **Exercise** — a tutorial unit: source program, tutorial text, and recorded outcome.
- **Golden** — the recorded outcome of an exercise, used as the comparison oracle.
- **Proposal** — a detected divergence between corpus and live truth, with a kind
  (`layout_normalise`, `stale_artefact`, `drift_gap`, `run_manifest`) and a suggested repair.
- **Run manifest** — the deterministic mapping from an exercise to the program, play and
  step limit needed to reproduce it.
- **Vendored substrate** — the corpus-local copy of a live sibling tree, plus the manifest the
  drift guard recomputes against.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The proposal report returns **zero** outstanding proposals for ch04 and ch07
  (baseline measured 2026-08-20: **4**).
- **SC-002**: Zero corpus goldens disagree with the live runtime for a purely historical reason.
- **SC-003**: A deliberate modification to the ch07 substrate is detected by the drift guard
  100% of the time; an unmodified tree reports OK 100% of the time.
- **SC-004**: Every ch07 exercise resolves to exactly one program/play/limit triple, with no
  ambiguous or missing mappings.
- **SC-005**: Every applied repair carries a recoverable approval and rationale — 100%, no
  exceptions.
- **SC-006**: A reviewer can determine, for each changed golden, whether it was stale or the
  runtime changed, without reading the implementation.
- **SC-007**: The existing REPL test suite remains green across the change (baseline: 546 pass
  / 0 fail / 1 skip).

## Assumptions

- The four proposals reported on 2026-08-20 are the complete current divergence set for this
  feature's scope; new divergences arising later are out of scope for this feature.
- `programs/cssg_modules/` (not `_v2`) is the ch07 substrate, per the proposal text. To be
  confirmed at plan stage against the ch07 exercises.
- The live Dart/C# oracle result for ch04/08 (`F=[5,4,3,2,1]`) is correct, i.e. the `is_list`
  guard fix was right. This feature repairs the golden; it does not re-litigate that fix.
- The existing approval-gated propose/apply flow is the delivery mechanism; this feature does
  not introduce a new one.
- The corpus is the single source of truth for recorded outcomes; no parallel copy exists.

## Out of Scope

- Re-litigating the C# `is_list` guard fix.
- Any change to the GLP language definition or its type system (CLAUDE.md Language Authority).
- Chapters other than ch04 and ch07.
- Building a general corpus-refresh automation beyond the four measured defects.

## Open Escalations

- **E2 (from 3rtask a625, still open)**: stale-golden repair (defects 1, 2) and substrate
  vendoring (defects 3, 4) are **distinct mechanisms sharing one workflow**. The engineer may
  wish to split this into two features. This specification keeps them together, structured as
  two independently-deliverable user stories (US1, US2) so that a split remains cheap: US1 and
  US2 have no ordering dependency on each other beyond value sequencing.

## Dependencies

- The live Dart and C# runtimes must be runnable on this host to re-capture goldens
  (the toolchain-path defect recorded as F1/F3 exemplar #5 must not silently substitute an
  absent toolchain).
