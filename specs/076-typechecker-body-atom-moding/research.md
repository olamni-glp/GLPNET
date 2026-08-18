# Phase 0 Research: Type-checker body-atom moding (076)

**Date**: 2026-08-11 | **Spec**: [spec.md](spec.md)

All Technical Context unknowns resolved. Evidence anchors re-verified in source this
session (not taken on faith from the 3rtask curator report).

## R1. Root-cause confirmation (mechanism, verified in source)

**Decision**: The defect is confirmed to live in body-atom mode derivation, exactly as
the curator report attributes it, across two cooperating layers:

1. **Moded-term construction**: `modedHead` (`glp_runtime/lib/analysis/type_checker/moded_head.dart:60-75`)
   implements Definition 5.5 — builds the I/O-moded term, then **unconditionally flips
   every variable** (`_ensureVariablesMatchModes`, lines 415-431). `producedTerm`
   (lines 94-106) builds body atoms with root mode ↑ and **no variable flip**
   ("Variables are NOT flipped" — the caller's perspective). Body-atom checking calls
   `producedTerm` at `well_typed_clause.dart:538-540` ("no variable flip for body atoms").
2. **Leaf-mode consistency**: `program_dfa.dart:568-580` accepts exactly two leaf
   combinations — reader at ↓, writer at ↑ — and rejects the rest with "Variable mode
   mismatch: {reader|writer} requires X, got Y".

Consequence for Issue 4: in `bind_later(Done?) :- ... | Done = done`, the moded head
flips `Done?`→writer at ↑ (consistent), but the body atom `=(Done, done)` derives arg 1
mode ↓ (declared `_?`) with unflipped surface writer `Done` → writer-at-↓ → rejected.
The clause-wide information that `Done`'s pair is a head hole (flipped reader at ↑) is
available at the call site (`checkClause` populates `allVariableTypes` from the head
**before** body atoms are checked and already passes it as `callerVarTypes`,
`well_typed_clause.dart:266-269`) but is never consulted for mode licensing.

**Duality-layer interplay** (curator's second anchor, 874-879): `_checkClauseDuality`
(lines 749-846) applies `_areDualTypesWithReason` — whose preconditions hard-require
writer=produce / reader=consume (lines 874-879) — only to **head-head** pairs; head/body
split pairs use `_areSameTypeWithReason` (same base type, no mode precondition). So for
Issue 4 (head reader + body writer) the duality layer is not the rejecting layer, but any
fix that records *effective* (licensed) modes in `VariableTypeInfo` must keep the
head-head path (the bind pattern: both occurrences in head) byte-for-byte unaffected.

**Rationale**: read-first protocol (DISCIPLINE §1.11); all three anchors read directly.
**Alternatives considered**: trusting the curator citations unverified — rejected
(DISCIPLINE §1.5, verify before acting).

## R2. Locus of the fix

**Decision** (fixed by Clarification Q1): amend the checker's body-atom mode
derivation; `programs/self.glp` declarations (`=` included) unchanged.

**Rationale**: the defect class is general (any procedure with a reader-position
declaration receiving a head-flipped pair half), and redeclaring `=` would itself be a
§1.14 language-surface change that fixes one symptom.
**Alternatives considered and rejected**:
- *Redeclare `=` as `=(_, _?)`* — inverts the documented assignment convention
  (manual §8.1), touches every existing use, leaves the class open.
- *Special-case `=/2` in the checker* — violates FR-004 (no procedure-specific case);
  the same shape recurs with user procedures.
- *PE-unfold all single-unit-clause procedures in bodies before checking* — changes
  the compilation pipeline's phase contract for a type-checking concern; body atoms
  that are not unit-clause procedures still exhibit the class.

## R3. The acceptance rule (candidate for the §1.14 proposal)

**Decision**: propose **occurrence-pair licensing**, the narrow form (see plan.md
"§1.14 Semantics Proposal"): a surface **writer** occurrence at a derived ↓ position in
a body atom is mode-consistent iff its SRSW-paired **reader** occurrence appears in the
clause head at a flip-derived ↑ position (a "head-flipped reader", i.e. an output hole).
Depth-composed on both sides (Clarification Q2). The symmetric combination (body reader
at ↑) remains rejected — deliberately NOT licensed by this feature.

**Rationale**: this is precisely Issue 4's shape and the feature's roadmap title; the
plan-guidance rule says prefer the simplest design satisfying the spec. The head hole
is filled by the pair's unique body writer; passing that writer into a callee's consume
position delegates the binding to the callee — the same delegation the callee's own
clause expresses with its flipped-writer head capture (`X? = X.` captures the writer to
bind it). No over-acceptance: the license requires the head hole to exist, so a writer
with no head-reader pair (or whose pair is elsewhere in the body) stays rejected.
**Alternatives considered and rejected**:
- *Full symmetric duality (also license reader-at-↑ in bodies)* — no evidenced program
  shape needs it; manual §7 warns the stored-reader idiom is a semantic trap; wider
  acceptance surface = higher soundness risk (spec US3). Can be proposed separately if
  a legitimate shape appears.
- *Flip all body-atom variables like heads* — wrong: it would invert the verdict on
  every currently-correct body atom (readers passed at ↓, writers at ↑) and break the
  caller-perspective semantics of Definition 5.7 clause 2.
- *Suppress the leaf check when callerVarTypes lacks the variable* — accept-by-default
  is unsound; the license must be positive evidence of the head hole.

## R4. Where the authoritative spec lives, and what must change with it

**Decision**: `docs/type system/well-typed-clause.md` (Definition 5.7) is the
authoritative well-typed-clause spec; the approved rule amends its clause 2 (produced
moded term) with the licensing condition. `docs/type system/moded-head.md` (Definition
5.5) is referenced, not changed. The spec amendment lands **before** implementation
(Constitution I, spec-first), in the same commit as or ahead of the checker change, and
only after the §1.14 approval.

**Rationale**: single source of truth (Constitution VIII); the checker cites these docs
as its specification.
**Alternatives considered**: spec-only-in-plan.md — rejected; plan.md is a feature
artifact, not the subsystem's authoritative spec.

## R5. Test vehicle & baseline

**Decision**: REPL-suite-first per CLAUDE.md: baseline `bash test/run_all_tests.sh`
(with `DART=/d/BSTDEV/tools/dart-sdk/bin/dart.exe` on this host), commit checkpoint,
change, re-run. New regression programs go under `programs/tests/typed/` with
`procedure` declarations: 2 positive (Issue-4 `=` shape; non-`=` user-procedure shape,
one with nesting depth ≥2) and 1 negative control (writer-at-↓ with NO licensing head
hole — pair absent or in body) expected to keep failing. Wire into `run_all_tests.sh`
Section B (positive) / C (negative). Dart unit tests for the licensing function join
`glp_runtime/test/` beside the existing type-checker tests.

**Rationale**: CLAUDE.md Test Protocol; spec FR-007/SC-003.
**Alternatives considered**: unit-tests only — rejected; the REPL pipeline is the one
integration truth (loading = full pipeline).

## R6. Windows/host specifics

**Decision**: run everything from repo root; `PYTHONUTF8=1` for buildkit/codeconv CLIs;
delete stale `glp_runtime/.dart_tool/repl.dill` if unified tests fail unexpectedly.
No Gleam/WSL involvement — this feature is Dart-checker-only, so the Section I /
`glp_gleam` build-ownership rules are untouched.
