<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Phase 1 Data Model: Guarded term-traversal utilities

This feature adds no persisted data. The "entities" are compiler in-memory structures.

## Entity: Term graph (existing — `ast.cs`)

- `abstract Term : AstNode` (`ast.cs:49`); reference type, default identity, no `Equals`/`GetHashCode` override, no id field.
- Shape-bearing subclasses: `VarTerm{ string Name, bool IsReader }` (199), `StructTerm{ string Functor, IReadOnlyList<Term> Args }` (219), `ListTerm{ Term? Head, Term? Tail }` (240), `ConstTerm{ object? Value }` (268), `UnderscoreTerm{ bool IsReader }` (299).
- **Shapes**:
  - *Acyclic tree* — the normal case (the AST as parsed).
  - *DAG* — shared subterms via the substitution map; still acyclic; MUST traverse successfully (FR-006 edge case).
  - *Cyclic* — pathological: a substitution `name → Term(...name...)` (F-069-1) OR a programmatically-constructed self-referential node. MUST be caught (FR-004).
- **No change** to `Term` in this feature (read-only).

## Entity: Shared traversal guard (NEW — `term_traversal.cs`)

The single home of the cycle guard (FR-001, SC-003). Two keying strategies (Research Decision 2):

| Field / concept | Purpose |
|---|---|
| `visited: HashSet<string>` (var-name keyed) | For the substitution/resolve family — detects substitution-closure cycles (the `ResolveTerm` pattern, generalised). A name re-encountered on the active resolution path ⇒ cycle. |
| `fuel: long` / `identitySeen: HashSet<Term>` (ref-identity keyed) | For the structural family (codegen/linker) — bounds traversal of a programmatically-cyclic AST node with no var-name to key on. |
| cycle-detection predicate | Declares a cycle ⇒ raises `CompileError` (see below). Distinguishes *revisit* (cycle) from *deep/DAG* (allowed) — FR-006. |

- **Invariants**: exactly one implementation of each guard entry point; every one of the ~21 walkers (research.md) obtains its guard here; the guard adds no observable behaviour on acyclic input (FR-005 parity).

## Entity: Consolidated substitution/resolve/unify module (NEW — `term_traversal.cs`)

The single merged home of the primitives previously duplicated in `analyzer.cs` (`DefinedGuardEvaluator`, `_`-prefixed) and `partial_evaluator.cs` (`PartialEvaluator`).

- **Moved-in primitives** (one copy): `GlpUnifyForPE`, `UnifyTerms`, `SubstSet`, `CheckCompatible`, `ResolveSubstitution`, `ResolveTerm`, `ApplySubstitution`, `ApplySubstitutionToAtom/Guard/Goal`, `RenameUnitClauseVars`, `CollectVarNames`, `ApplyRenaming`, anonymous-predicate.
- **Parameterised behavioural knobs** (preserve both callers' semantics — Research Decision 3):
  - `Func<Term,bool> isAnonymous` — analyzer passes `StartsWith('_')`, PE passes `== "_"` (divergences #1, #3).
  - `string freshVarPrefix` — analyzer `"PE_"`, PE `"PE"` (divergence #2); counter unified to `long`.
- **Left in owning classes**: `TransformClause`/`TransformDefinedGuards` (divergence #4, orchestration), PE-only Stage-2 (`UnfoldReduceCalls`, etc.).
- **Shared signature normalisation**: `ResolveSubstitution` returns `IReadOnlyDictionary<string,Term>` (divergence #5).

## Entity: Cyclic-term signal (existing type — `error.cs`)

- Reuse `CompileError(message, line, column, source?, phase?)` (`error.cs:13`). Raised by the guard on a declared cycle (FR-004).
- `phase`: reuse caller's (`"analyzer"`/`"codegen"`); optional new `"term_traversal"` mapping in `CategoryFromPhase` (`error.cs:33`).
- NOT a new `UnifyResult` subtype (most walkers don't return `UnifyResult`; an exception is the uniform signal across all ~21 sites).

## State transitions (traversal outcome)

```
enter walker → traverse under guard
   ├─ acyclic / DAG / deep  → complete normally, result identical to pre-feature (FR-005)
   └─ revisit / fuel-exhausted → raise CompileError (FR-004) — bounded time, catchable (SC-001)
```
