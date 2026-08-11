<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
buildkit-file-id: 57a8b9fc-2569-4312-9cb0-17b9be75ff62
-->

# Implementation Plan: Type-checker body-atom moding — accept head-flipped readers at declared reader positions

**Branch**: `076-typechecker-body-atom-moding` | **Date**: 2026-08-11 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/076-typechecker-body-atom-moding/spec.md`

## Summary

The type checker rejects well-typed clauses in which a body-atom occurrence of a
variable sits at a declared reader (consume) position while its SRSW pair is a
head-flipped reader (an output hole) — canonically blocking `=/2` (known-issues
Issue 4). The fix: body-atom mode derivation gains **occurrence-pair licensing** —
a surface writer at a derived ↓ position is accepted iff its paired reader occurs in
the clause head at a flip-derived ↑ position — depth-composed per the §2A mode
involution, uniformly for all procedures, with no change to `programs/self.glp`.
The rule is a GLP type-system semantics change and is **hard-gated on Gabi's §1.14
approval** of the proposal below before any implementation.

## §1.14 Semantics Proposal (AWAITING GABI'S EXPRESS APPROVAL — HARD GATE)

*Stated against the type system, not the implementation (spec FR-001). Approval is
recorded per Clarification Q3: a Clarifications entry in spec.md + a marathon trace row
on `mrun-d086da8a860f`. Until then: no implementation.*

**Current rule** (`docs/type system/well-typed-clause.md`, Definition 5.7 clause 2):
for each body atom A, the produced moded term A' (root mode ↑, argument modes derived
from the declared types by mode involution, variables **not** flipped) must be
well-typed. At a variable leaf this requires: reader at ↓, or writer at ↑; every other
combination is a mode mismatch.

**Proposed amendment (occurrence-pair licensing).** Let C = (H :- G | B) be a clause,
X a variable of C, and o a leaf occurrence of X in a produced moded term A' for some
A ∈ B, with derived structural mode m(o) (composed along the declared type path by mode
involution, at any depth). Add one licensed combination:

> A **writer** occurrence o with m(o) = ↓ is mode-consistent iff the paired reader
> occurrence of X appears in H at a position whose flip-derived mode is ↑ — i.e. the
> head occurrence is a *head-flipped reader* (an output hole in the sense of manual
> §2A: reader `X?` written at a produce-mode head position).

Everything else is unchanged. In particular:

- Reader at ↓ and writer at ↑ remain consistent as today.
- A **reader** occurrence at ↑ in a body atom remains a mode mismatch — the symmetric
  combination is deliberately NOT licensed by this proposal (no evidenced program
  shape; smallest sound extension).
- A writer at ↓ whose pair does not exist, occurs in the body, or occurs in the head
  at a ↓ position remains a mode mismatch (the license requires the head hole).
- Definition 5.7 clause 3 (pair typing: dual types within one part, same type across
  head/body) is unchanged; the licensed pair is a head/body pair and thus continues to
  require the same base type.

**Why this is correct.** The head-flipped reader `X?` at a ↑ head position is a hole
the clause must fill through the pair's unique writer X. Passing that writer to a
callee at a declared ↓ position delegates the binding to the callee — exactly the
delegation the callee's own definition expresses by capturing a writer with its
flipped head occurrence (e.g. `X? = X.` under `procedure =(_?, _).`: arg 1's moded
head is the flipped writer that the unit clause binds). The caller-side writer and the
callee-side flipped-writer capture are the two ends of one binding channel; today the
checker models the callee end (Definition 5.5 flip) but not the caller end. SRSW
guarantees the licensed writer is the *only* writer of X, so acceptance introduces no
second producer and no unsoundness; the license's positive evidence (the head hole)
excludes accept-by-default.

**Scope**: type-checker acceptance only. No runtime, compiler, PE, or prelude change;
no new syntax, guard, directive, or primitive. `procedure =(_?, _).` and its unit
clause stay exactly as they are (Clarification Q1).

**Example (Issue 4) under the amendment**:

```prolog
procedure bind_later(_).
bind_later(Done?) :- wait(1000) | Done = done.
```

Head: `Done?` at ↑ (declared `_`) — a head-flipped reader → the hole exists.
Body: `=(Done, done)` derives arg 1 mode ↓ (declared `_?`); writer `Done` at ↓ is
licensed by the head hole → clause well-typed. The equivalent
`bind_later(Done?) :- wait(1000) | done(Done).` (writer at a declared ↑ position)
stays accepted exactly as today.

## Technical Context

**Language/Version**: Dart (SDK at `D:\BSTDEV\tools\dart-sdk`, project `glp_runtime`)
**Primary Dependencies**: none new — `glp_runtime/lib/analysis/type_checker/` only
**Storage**: N/A
**Testing**: unified REPL suite `bash test/run_all_tests.sh` (host: `DART=/d/BSTDEV/tools/dart-sdk/bin/dart.exe`); Dart unit tests `cd glp_runtime && dart test`
**Target Platform**: Windows host (this repo); checker is platform-independent Dart
**Project Type**: compiler/analysis component of an existing runtime
**Performance Goals**: no measurable type-check slowdown (licensing is an O(1) map
lookup per conflicting leaf against already-computed head variable info)
**Constraints**: §1.14 hard gate before implementation; no modification of core-GLP
files outside `lib/analysis/type_checker/`; `programs/self.glp` untouched; maGLP
constraint not applicable (no `lib/multiagent/` involvement)
**Scale/Scope**: 2 checker source files (`moded_head.dart` call path — untouched;
`well_typed_clause.dart` + `program_dfa.dart` leaf check), 1 spec doc amendment,
3 new test programs, Dart unit tests

## Constitution Check

*GATE: evaluated pre-Phase 0 and re-evaluated post-design — PASS (no violations, no
Complexity Tracking entries).*

- **I. Spec-First**: PASS with explicit sequencing — the authoritative subsystem spec
  `docs/type system/well-typed-clause.md` is amended (post-approval) BEFORE the checker
  change; implementation matches the amended spec exactly. This plan quotes the spec
  verbatim above.
- **II. Bug-Protocol / No-Workarounds**: PASS — Issue 4 was reported and is being fixed
  at root cause; the existing `done(Done)` workaround is retired, not entrenched. No
  robustness masking: ill-moded clauses keep failing.
- **III. SRSW inviolable**: PASS — the rule *relies on* SRSW (uniqueness of the licensed
  writer); no SRSW-escape option is proposed or used anywhere in these artifacts.
- **IV-a. Language Authority**: PASS by construction — the §1.14 proposal above is the
  gate; implementation is blocked until Gabi's express approval is recorded (spec
  FR-001/FR-009/SC-005, marathon discharge item `1.14-ruling`).
- **IV-b. Preserve Working Internals**: PASS — no removal of `_ClauseVar`,
  `_TentativeStruct`, or fallback branches; the change is additive licensing in the
  checker; `modedHead`'s unconditional flip is untouched.
- **V. Claude-Only LM**: PASS — no LM in the delivered artifact at all.
- **VI-a/VI-b. Persistence**: PASS — no migrations, no clusters touched.
- **VII. Test-Gated, Commit-Scoped**: PASS — baseline → change → re-test protocol in
  quickstart.md; commits stage files by name; ship via buildkit GitFlow.
- **VIII. Single Source of Truth**: PASS — one authoritative spec amended; plan/spec
  reference it rather than duplicate it (the proposal quotes it once, as required by I).

## Project Structure

### Documentation (this feature)

```text
specs/076-typechecker-body-atom-moding/
├── spec.md              # Feature spec (clarified)
├── plan.md              # This file — includes the §1.14 Semantics Proposal
├── research.md          # Phase 0 — root cause, rule candidates, rejected alternatives
├── data-model.md        # Phase 1 — checker entities & licensing relation
├── quickstart.md        # Phase 1 — repro, baseline, verify procedure
├── contracts/
│   └── body-atom-moding-rule.md   # Acceptance matrix + diagnostics contract
├── baseline.md          # Implement stage (T001/T014) — recorded suite counts
└── tasks.md             # Phase 2 (/bk-tasks — NOT created by /bk-plan)
```

### Source Code (repository root)

```text
glp_runtime/
├── lib/analysis/type_checker/
│   ├── well_typed_clause.dart   # body-atom check: thread head-context licensing
│   ├── program_dfa.dart         # leaf consistency: licensed-combination acceptance
│   ├── moded_head.dart          # READ-ONLY reference (Definition 5.5 flip) — unchanged
│   └── moded_term.dart          # READ-ONLY (ModedVariable carries surface+structural mode)
└── test/                        # new unit tests beside existing type-checker tests

docs/type system/
└── well-typed-clause.md         # authoritative spec — amended post-approval

programs/tests/typed/            # 2 positive + 1 negative regression programs
test/run_all_tests.sh            # Section B/C wiring for the new programs
```

**Structure Decision**: existing single-project layout; the change is confined to
`glp_runtime/lib/analysis/type_checker/` plus spec/tests — consistent with the
maGLP-style constraint that core runtime files stay untouched.

## Design sketch (implements the proposal, pending approval)

1. **Thread head context**: `checkClause` already computes head `VariableTypeInfo`
   before body atoms and passes `callerVarTypes` into `_checkBodyAtomWithTerm`
   (well_typed_clause.dart:266-269). Extend the body-atom path so leaf consistency
   checking can consult it (today `_checkModedTermPerArg` → `program_dfa.dart`
   leaf check has no head context).
2. **Licensing predicate**: a writer leaf at ↓ consults the head map for key `X?` with
   `structuralMode == produce` recorded from the *surface* head term (the head-flipped
   reader). Exactly this combination flips the verdict to consistent; the recorded
   `VariableTypeInfo` for the body occurrence keeps its actual surface form and mode so
   Definition 5.7 clause 3 bookkeeping (same-type across head/body) runs unchanged.
3. **Head-head path untouched**: `_areDualTypesWithReason` preconditions
   (well_typed_clause.dart:874-879) apply to head-head pairs (bind pattern) — licensing
   never rewrites head-occurrence records, so that path is unaffected by construction.
4. **Diagnostics**: the mismatch message stays for unlicensed conflicts and gains the
   license context when relevant ("writer requires ↑ (produce), got ↓ (consume); no
   head-flipped reader pair in head licenses this occurrence") — satisfying FR-006.
5. **Parameterized procedures**: the licensing point sits after call-site instantiation
   (Case B), so inferred concrete decls get the same rule; the existing
   inference-failure skip paths (return success, well_typed_clause.dart:525-535) are
   unchanged.

## Phase 1 artifacts

Generated alongside this plan: [data-model.md](data-model.md),
[contracts/body-atom-moding-rule.md](contracts/body-atom-moding-rule.md),
[quickstart.md](quickstart.md). Agent context (`CLAUDE.md` BUILDKIT block) updated to
point at this plan.

## Complexity Tracking

No Constitution violations — table intentionally empty.
