# Ch 2 Sources — Logic Programs and Linear Logic

**PDF**: `GLP_ART.pdf`, book pp 9–14 (PDF pp 21–26).

## Sections (verified)
- 2.1 Logic Programs — p 9 (Transition Systems, Logic Programs Syntax, Operational Semantics)
- 2.2 Linear Logic — p 12 (Definitions, GLP as Linear Logic Programming, Formal 2.1)

## Code-block index
| Block | Title | Page | Body | Mode hint |
|---|---|---|---|---|
| **Example 2.1** | Append (classical LP, **not GLP**) | p 10 | 2 clauses (`append/3` recursive + base) | LP-only — illustrates contraction; included for contrast |

### Example 2.1 — verbatim (p 10)
```
Example 2.1 (Append).  append([X|Xs], Ys, [X|Zs]) :- append(Xs, Ys, Zs).
                       append([], Ys, Ys).
```
NOTE: This is classical LP (no readers/writers, no SRSW). It is presented as a baseline against which the GLP version in Ch 3 is contrasted.

## Definitions / Propositions (theoretical, no code)
- Def 2.1 Transition System; Def 2.2 Terms; Def 2.3 Logic Programs; Def 2.4 Auxiliary Notation; Def 2.5 Substitution/Instance/Unifier/MGU; Remark 2.1; Def 2.6 Renaming; Def 2.7 LP Goal/Clause Reduction; Def 2.8 LP Transition System; Def 2.9 Proper Run and Outcome; Proposition 2.10 (LP Computation is Deduction).
- Def 2.11 Structural Rules; Def 2.12 Linear Logic; Example 2.2 (Resource Interpretation — coffee/dollar, narrative).
- **Formal 2.1 Linear Equality Assertions** — p 14, mapping table Linear Logic ↔ GLP.

## Tutorial mode
cohesive-synthesis — but chapter is mostly theoretical, only one tiny code fragment. The "tutorial file" for this chapter would be a single short narrative .glp showing the Append contrast (LP → GLP via SRSW).

## Companion repo references
- `programs/typed_book/recursive/list_processing/` (for the Ch 3+ GLP append, by way of contrast).
- `../charter.md`
