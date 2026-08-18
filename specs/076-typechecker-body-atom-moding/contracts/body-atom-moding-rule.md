# Contract: Body-atom variable-leaf mode consistency (076)

**Status**: PROPOSED — inert until Gabi's §1.14 approval (plan.md §1.14 Semantics
Proposal). On approval this contract is folded into the authoritative
`docs/type system/well-typed-clause.md` (Definition 5.7 clause 2 amendment); this file
then remains as the feature's acceptance matrix, referencing that spec.

## Interface

The contract governs one judgement: leaf-mode consistency of a variable occurrence o
in a produced moded term A' for body atom A of clause C = (H :- G | B), with derived
structural mode m(o) and surface form ∈ {writer, reader}.

Inputs available to the judgement: (surface form of o, m(o), head occurrence map of C —
complete before any body atom is judged).

## Acceptance matrix

| # | Surface form | m(o) | Head pair evidence | Verdict | Change vs today |
|---|--------------|------|--------------------|---------|-----------------|
| 1 | reader `X?` | ↓ | — (irrelevant) | consistent | unchanged |
| 2 | writer `X` | ↑ | — (irrelevant) | consistent | unchanged |
| 3 | writer `X` | ↓ | head has reader `X?` at flip-derived ↑ (head-flipped reader) | **consistent (licensed)** | **NEW** |
| 4 | writer `X` | ↓ | no head reader pair / pair in body / pair at head-↓ | mode mismatch | unchanged |
| 5 | reader `X?` | ↑ | any | mode mismatch | unchanged (symmetric case NOT licensed) |
| 6 | anonymous `_` writer | ↓ | never has a pair | mode mismatch | unchanged |

Depth: rows apply at any nesting depth; m(o) and the head occurrence's flip-derived
mode are both composed along the declared type path by mode involution (manual §2A).

Uniformity: the matrix is procedure-independent — `=` receives no special case (row 3
merely makes `=`'s canonical use well-typed).

Pair typing (Definition 5.7 clause 3) is orthogonal and unchanged: the licensed pair is
a head/body pair and must still carry the same base type; head-head and body-body pairs
keep their dual-type / subtype rules and their mode preconditions.

## Diagnostics contract (FR-006)

- Rows 4/5/6 keep reporting: variable name, argument position/path, expected vs actual
  mode ("{reader|writer} requires {↑|↓}, got {↑|↓}").
- Row 4 additionally states the absent license: "no head-flipped reader pair in head
  licenses this occurrence".
- Row 3 produces no diagnostic (it is consistent).

## Conformance tests (bind FR-007/SC-003)

| Test | Program shape | Expected |
|------|--------------|----------|
| P1 (Section B) | Issue-4 shape: `p(Done?) :- ... | Done = done` under `procedure p(_).` | loads, runs |
| P2 (Section B) | Non-`=` user procedure with declared `T?` position receiving the licensed writer; one occurrence at depth ≥ 2 | loads, runs |
| N1 (Section C) | Writer-at-↓ with NO head hole (pair in body, or absent) | type-check FAILS with row-4 diagnostic |
| Regression | Full unified suite + Dart unit tests | zero regressions vs baseline |
