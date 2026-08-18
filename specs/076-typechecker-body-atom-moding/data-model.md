# Data Model: Type-checker body-atom moding (076)

**Date**: 2026-08-11 | **Plan**: [plan.md](plan.md)

Conceptual entities of the licensing rule. No persistence — everything is in-memory
per-clause checker state.

## Entities

### Variable occurrence
- **Fields**: base name; surface form (writer `X` / reader `X?`); location (head /
  body atom *i* / guard atom); derived structural mode m(o) ∈ {↑ produce, ↓ consume}
  (composed along the declared type path by mode involution, any depth).
- **Identity**: (base name, surface form) is unique per clause under SRSW — at most one
  writer occurrence and one reader occurrence per variable (constant-type/ground-guard
  relaxations aside; anonymous `_` writers are fresh per occurrence and pair with
  nothing).
- **Implementation carrier**: `ModedVariable(name, isReader, structuralMode)` leaves in
  `ModedTerm`; per-clause aggregation in `Map<String, VariableTypeInfo>` keyed
  `X` / `X?` with `variableLocations` alongside.

### Occurrence pair
- **Fields**: writer occurrence; reader occurrence (same base name).
- **Relationships**: partitions into head-head, body-body, head-body — driving
  Definition 5.7 clause 3 (dual types within one part; same base type across parts).
  Unchanged by this feature.

### Head-flipped reader (the licensing evidence)
- **Definition**: a reader occurrence `X?` in the clause head at a position whose
  flip-derived mode is ↑ — an output hole (manual §2A).
- **Recognition at check time**: head `VariableTypeInfo[X?]` exists with structural
  mode produce, recorded from the surface head term before body atoms are checked.

### License (the new relation)
- **Definition**: License(o) holds for a body-atom **writer** occurrence o with
  m(o) = ↓ iff the same clause's head contains the head-flipped reader of the same
  base name.
- **Effect**: flips the leaf-consistency verdict for exactly that combination from
  mode-mismatch to consistent. No other verdict changes; no recorded mode is rewritten.
- **State transitions**: none — the license is a pure predicate over already-computed
  clause state (no ordering effects: head info is complete before any body atom is
  checked).

## Validation rules (from spec requirements)

| # | Rule | Source |
|---|------|--------|
| 1 | License requires the head hole: writer-at-↓ with pair absent, in body, or at head-↓ stays rejected | FR-002, FR-005 |
| 2 | License applies at any nesting depth (m(o) via involution both sides) | FR-002 / Clarification Q2 |
| 3 | Reader-at-↑ in body atoms stays rejected (symmetric case not licensed) | plan §1.14 proposal scope |
| 4 | Procedure-uniform: no special case per functor (`=` included) | FR-004 |
| 5 | Head-occurrence records never rewritten (head-head duality path byte-identical) | plan design sketch #3 |
| 6 | Diagnostics keep variable + position + expected/actual mode; add absent-license context | FR-006 |
