# Well-Typed Clause

**Paper Reference**: Definition 5.7

## Definition 5.7 (Well-typed, input accepting clause)

> Let C = (H :- B) be a GLP clause and D a GLP type for all its procedures. Then C is **well-typed** by D if:
>
> 1. There is a moded head H' corresponding to H that is well-typed by D.
>
> 2. For each atom A ∈ B, the produced moded term A' corresponding to A is well-typed by D.
>
> 3. For every pair of dual variables X and X? in C:
>    - (a) If both occur in H, or both occur in B, they are assigned dual types by D.
>    - (b) If one occurs in H and the other in B, they are assigned the same type by D.
>
> In addition, C **accepts an input path** x ∈ paths(D) if H' has a path consistent with x.

## Produced Moded Term

A **produced moded term** for a body atom has root mode ↑ (the clause produces these goals). Argument modes follow the declared types as usual.

## Amendment to Definition 5.7 clause 2 — Occurrence-Pair Licensing

**Status**: APPROVED by Gabi 2026-08-12 under DISCIPLINE §1.14 (language authority).
Feature `076-typechecker-body-atom-moding`; proposal text and rationale in
`specs/076-typechecker-body-atom-moding/plan.md` §"§1.14 Semantics Proposal"; acceptance
matrix in `specs/076-typechecker-body-atom-moding/contracts/body-atom-moding-rule.md`
(referenced, not duplicated here).

Clause 2 of Definition 5.7 is refined for variable leaves as follows. Let C = (H :- G | B)
be a clause, X a variable of C, and o a leaf occurrence of X in the produced moded term A'
of some atom A ∈ B, with derived structural mode m(o) — composed along the declared type
path by mode involution, at any nesting depth, exactly as for heads (typed-glp-manual §2A).
One combination is licensed in addition to those already consistent:

> A **writer** occurrence o with m(o) = ↓ is mode-consistent iff the paired **reader**
> occurrence of X appears in H at a position whose flip-derived mode is ↑ — i.e. the head
> occurrence is a *head-flipped reader* (an output hole in the sense of §2A).

Everything else is unchanged:

- reader at ↓, and writer at ↑, remain consistent as before;
- a **reader** occurrence at ↑ in a body atom remains a mode mismatch — the symmetric
  combination is deliberately NOT licensed;
- a writer at ↓ whose pair is absent, occurs in B, or occurs in H at a ↓ position remains a
  mode mismatch (the license requires the head hole);
- clause 3 (pair typing) is unaffected: the licensed pair is a head/body pair and therefore
  still requires the same type.

**Justification.** A head-flipped reader `X?` at a ↑ head position is a hole the clause must
fill through the pair's unique writer X. Passing that writer to a callee at a declared ↓
position delegates the binding to the callee — the same delegation the callee expresses by
capturing a writer with its own flipped head occurrence (e.g. `X? = X.` under
`procedure =(_?, _).`). The caller-side writer and the callee-side flipped-writer capture
are the two ends of one binding channel; before this amendment the definition modelled only
the callee end (Definition 5.5 flip). SRSW guarantees the licensed writer is the *only*
writer of X, so no second producer is admitted; requiring positive evidence (the head hole)
excludes accept-by-default.

**Scope.** Acceptance only — no runtime, compiler, partial-evaluator, or prelude change, and
no new syntax, guard, directive, or primitive type. The rule is uniform across all
procedures; `=` receives no special case.

## Variable Type Rules

The location of variables determines the type relationship required:

| Writer Location | Reader Location | Required Relationship |
|-----------------|-----------------|----------------------|
| Head | Head | Dual types |
| Body | Body | Dual types |
| Head | Body | Same type |
| Body | Head | Same type |

## Error Reporting

Errors are reported as simple strings with sufficient detail to locate the problem. No elaborate error class hierarchy is required. Example error messages:

- "Head not well-typed: path inconsistent at position 2"
- "Body atom 3 not well-typed: variable X has wrong mode"
- "Variable pair (Y, Y?) not dual: head has Stream, body has Integer"
