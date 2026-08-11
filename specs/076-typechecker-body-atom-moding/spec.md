<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 86d431e3-8849-4b6f-a473-8c268e68529f
-->

# Feature Specification: Type-checker body-atom moding — accept head-flipped readers at declared reader positions (unblock `=/2`)

**Feature Branch**: `076-typechecker-body-atom-moding`
**Created**: 2026-08-11
**Status**: Draft
**Input**: User description: "Type-checker body-atom moding: accept head-flipped readers at declared reader positions (unblock =/2) - roadmap feature under epic issue-backlog-root-cause-closure-sweep-2026-08, absorbs known-issues Issue 4; evidence in .specify/3rtask/runs/20260811T091356Z-a625/curator_report.md"

## Problem Statement

The type checker rejects well-typed clauses in which a variable occurrence in a body atom
stands at a declared reader (consume) position while its effective mode was flipped by the
variable's head occurrence. `docs/known-issues.md` Issue 4 records the canonical case:

```prolog
procedure bind_later(_).
bind_later(Done?) :- wait(1000) | done(Done).
```

When the body instead uses `=` (declared `procedure =(_?, _).` with unit clause `X? = X.`),
the checker reports "Variable mode mismatch: writer requires ↑ (produce), got ↓ (consume)"
for the variable at the `=` call site, even though the clause is well-typed: the head
occurrence sits at a produce position, so per the mode-flip rule (typed-glp-manual §2A) the
body occurrence carries the flipped mode. Programmers must today restructure code to avoid
`=` entirely (the Issue 4 workaround), which blocks the natural assignment idiom.

**Root cause** (curator report, run `20260811T091356Z-a625`, builder-1, both cycles):
body-atom moding derives each leaf's mode purely from the surface annotation on the
variable occurrence — with no head-binding context — so the mode flip that the head
position imposes on the variable pair is never applied when checking body atoms.
Evidence anchors: `glp_runtime/lib/analysis/type_checker/well_typed_clause.dart:538-540`
("no variable flip for body atoms") and the duality mode check at lines 874-879.

## ⛔ Language-Authority Gate (DISCIPLINE §1.14)

This feature touches the GLP type-system semantics. Per `docs/DISCIPLINE.md` §1.14, the
acceptance rule for head-flipped variable occurrences in body atoms MUST be formulated as
an explicit semantics proposal and receive Gabi's express approval **before any
implementation**. The specify/clarify/plan stages of this feature exist to formulate that
proposal; nothing in this document pre-approves it.

## Clarifications

### Session 2026-08-11

- Q: Locus of the fix — checker rule, `=` redeclaration, or both? → A: Fix the checker: body-atom mode derivation incorporates head-binding context (the §2A flip rule applied clause-wide); `=`'s declaration in `programs/self.glp` stays as is.
- Q: Nesting-depth scope — depth-composed or top-level only? → A: Depth-composed: the rule applies at any nesting depth in body-atom arguments, flips composed along the type path exactly as §2A does for heads.
- Q: Where is the §1.14 approval recorded? → A: Proposal as a dedicated section in `plan.md`; Gabi's express approval recorded as a Clarifications entry in `spec.md` plus a marathon trace row on `mrun-d086da8a860f`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Assignment with a head-flipped variable type-checks (Priority: P1)

A GLP programmer writes a clause whose head receives an output position (declared produce
mode) with a reader-form variable (`Done?`, a hole to be filled), and in the body binds
that variable using `=` (or another procedure with a declared reader position). Loading
the file in the REPL succeeds: the full pipeline (SRSW → partial eval → type check →
compile) passes with no mode-mismatch error.

**Why this priority**: This is the exact blocked idiom of Issue 4 — the feature's reason
to exist ("unblock `=/2`"). Without it, every typed program must route around `=` with
ad-hoc helper procedures.

**Independent Test**: Load a minimal program containing the Issue 4 clause shape via
`dart run bin/glp_repl.dart`; the load must succeed and the goal must run.

**Acceptance Scenarios**:

1. **Given** the Issue 4 example program (`procedure bind_later(_).` with a clause whose
   head has the variable in reader form at the produce-mode position and whose body binds
   it through `=`), **When** the file is loaded in the REPL, **Then** the load succeeds
   with no "Variable mode mismatch" error and the binding executes.
2. **Given** the same program using the current workaround (`done(Done)` helper instead
   of `=`), **When** loaded, **Then** it continues to load and behave identically (no
   regression on the previously-accepted form).

---

### User Story 2 - The rule is general, not an `=`-special-case (Priority: P2)

A programmer calls any procedure whose declaration has a reader (consume) position,
passing a variable occurrence whose effective mode is flipped by its head occurrence.
The checker accepts every such well-typed call — the fix addresses body-atom mode
derivation, not the `=` procedure specifically.

**Why this priority**: The root cause is in the general body-atom moding path; an
`=`-only carve-out would leave the defect class open and violate the approved semantics.

**Independent Test**: Load a test program with a user-defined procedure (not `=`) that
exercises the same head-flipped shape; it must type-check.

**Acceptance Scenarios**:

1. **Given** a user-defined procedure `p(T?)` and a clause whose head holds the variable
   pair's reader form at a produce position while the body passes the flipped occurrence
   to `p` at its declared reader position, **When** loaded, **Then** the clause
   type-checks.
2. **Given** the approved semantics rule, **When** the same variable shape appears nested
   inside a structure argument of a body atom (mode flips composed per the §2A path
   rule), **Then** acceptance follows the same rule at any depth.

---

### User Story 3 - Ill-moded programs are still rejected precisely (Priority: P2)

A programmer writes a genuinely ill-moded body atom (e.g., a plain writer occurrence at a
declared writer position when the pairing demands otherwise, or a mode clash not licensed
by any head flip). The checker still rejects it, and the diagnostic still names the
variable, its position, and the expected vs. actual mode.

**Why this priority**: Over-acceptance would be a soundness regression worse than the
current over-rejection; the negative test suite is the guard.

**Independent Test**: Run the negative type-check section of the unified suite; all
previously-rejected programs must still be rejected for the same reasons.

**Acceptance Scenarios**:

1. **Given** the existing negative type-check test programs, **When** the unified suite
   runs, **Then** every negative test still fails type-checking as before.
2. **Given** a new negative control that resembles the Issue 4 shape but is genuinely
   ill-moded (no licensing head flip), **When** loaded, **Then** the checker rejects it
   with a mode-mismatch diagnostic identifying the variable and position.

---

### Edge Cases

- Head-flipped variable used in a body atom of a **parameterized** procedure (call-site
  instantiation path): the same acceptance rule must apply after type-parameter
  inference; when inference is skipped (no caller types), behaviour must not regress.
- Mode flips **composed through nested type structure** (`?` in the type-definition path,
  §2A): the rule must compose at depth, not only at top-level argument positions.
- **Constant-type SRSW relaxation** (multiple occurrences of a constant-typed variable):
  acceptance must not double-apply or skip the flip for the extra occurrences.
- **Anonymous variables** (`_`-prefixed writers) at flipped positions: unchanged
  behaviour — they have no paired occurrence to flip against.
- A variable whose head occurrence is **not** flipped (plain input/output shape): the
  surface-annotation derivation must give the same verdict as today.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A written semantics proposal for body-atom mode derivation in the presence
  of head-flipped variable pairs MUST be produced and MUST receive Gabi's express
  approval (§1.14) before any implementation work begins. The proposal MUST state the
  rule in terms of the typed-GLP mode system (declared modes, the §2A flip rule, SRSW
  pairing), not in terms of the current implementation. It is authored as a dedicated
  section of `plan.md` (Clarification Q3).
- **FR-002**: Under the approved rule, the type checker MUST accept a body-atom variable
  occurrence at a declared reader (consume) position whenever the occurrence is the SRSW
  pair of a head occurrence whose position flipped the variable's effective mode — i.e.,
  body-atom mode derivation MUST incorporate head-binding context rather than surface
  annotation alone. The rule applies at any nesting depth in body-atom arguments, with
  mode flips composed along the declared type path exactly as §2A composes them for
  heads (Clarification Q2).
- **FR-003**: The Issue 4 example MUST type-check, load, and execute via the REPL, using
  `=` directly (no workaround restructuring).
- **FR-004**: The rule MUST apply uniformly to all procedures with declared reader
  positions (system and user-defined alike); no procedure-specific special case.
- **FR-005**: All genuinely ill-moded clauses MUST still be rejected; every existing
  negative type-check test MUST retain its rejecting verdict.
- **FR-006**: Mode-mismatch diagnostics MUST continue to identify the variable, the
  argument position, and the expected vs. actual mode for rejected clauses.
- **FR-007**: Regression tests MUST be added to the unified suite: at least one positive
  test (the Issue 4 shape, plus a non-`=` generalisation) in the positive type-check
  section, and at least one negative control in the negative type-check section, with
  test programs under `programs/tests/typed/` carrying `procedure` declarations.
- **FR-008**: `docs/known-issues.md` Issue 4 MUST be closed with the resolution recorded,
  including correction of its stale prelude claim (the `=` declaration now lives in
  `programs/self.glp`; the built-in type prelude is empty) per the curator report's
  doc-corrections sweep.
- **FR-009**: If, during proposal formulation, the correct semantics turns out to differ
  from the head-flip acceptance sketched here (or Gabi withholds approval), work MUST
  stop at the proposal stage and the feature MUST be re-scoped or closed by decision —
  never implemented on an unapproved rule.

### Key Entities

- **Variable pair**: a clause variable's writer/reader halves under SRSW; exactly one
  occurrence of each per clause (outside declared relaxations).
- **Declared mode / position**: the produce (`T`) or consume (`T?`) mode a procedure
  declaration assigns to each argument position, composed through type structure.
- **Head flip**: the §2A rule by which a `?` in the declared type path inverts the
  effective mode of a variable occurrence in the head, determining which half
  (writer/reader) appears where.
- **Body-atom mode derivation**: the checker's assignment of an effective mode to each
  variable occurrence in a body atom, checked against the callee's declared modes.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The Issue 4 example program loads and runs through the REPL with zero type
  errors, using `=` directly.
- **SC-002**: The unified REPL test suite passes with zero regressions relative to the
  pre-change baseline, including every existing negative type-check test still rejecting.
- **SC-003**: At least 2 new positive and 1 new negative regression tests are in the
  unified suite and green.
- **SC-004**: `docs/known-issues.md` Issue 4 is marked resolved with its stale prelude
  claim corrected; no other open issue references the `=` workaround as required
  practice.
- **SC-005**: A §1.14 approval record exists before any implementation commit, in the
  agreed form (Clarification Q3): the semantics proposal as a dedicated section in
  `plan.md`, Gabi's express approval as a Clarifications entry in `spec.md`, and a
  marathon trace row on `mrun-d086da8a860f`.

## Assumptions

- The correct acceptance rule is expected to be the §2A mode-flip rule extended to
  body-atom checking (head-binding context); the definitive rule is whatever the §1.14
  proposal fixes with Gabi's approval — FR-009 governs divergence.
- Evidence basis is the 3rtask curator report (run `20260811T091356Z-a625`), builder-1,
  re-derived consistently in both cycles; the cited code anchors were re-verified in
  source on 2026-08-11 during specification.
- Scope is this one root-cause cluster only. The sibling clusters from the same sweep
  (madGLP address discipline, front-end term acceptance, PGLite PG17, tutorial goldens,
  crdtmsg post-MVP) are separate roadmap features and out of scope here.
- The feature changes the type checker's acceptance, not the runtime: programs accepted
  under the new rule execute under existing runtime semantics unchanged.
- The fix locus is the checker's body-atom mode derivation only; the `=` declaration
  (`procedure =(_?, _).` / `X? = X.`) and all other prelude declarations in
  `programs/self.glp` are out of scope and remain unchanged (Clarification Q1).
- Baseline discipline applies: full suite green and committed before any change; re-run
  and committed after (CLAUDE.md Test Protocol).
