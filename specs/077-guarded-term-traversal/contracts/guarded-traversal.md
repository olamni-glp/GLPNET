<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Contract: Shared guarded term-traversal utility

The internal interface the ~21 compiler walkers consume. This is an in-process C# contract (compiler back-end), not an external API. Names are indicative; the implementation stage (`/bk-tasks` → `/bk-implement`) fixes exact signatures.

## C1 — Cycle-guarded substitution/resolve family (var-name keyed)

**Consumers**: `ApplySubstitution`, `UnifyTerms`, `ResolveTerm`, `ApplyRenaming`, `CollectVarNames`, the mark/ground walkers in `analyzer.cs`/`partial_evaluator.cs`.

- **Input**: a `Term`, the substitution map (`IReadOnlyDictionary<string,Term>` where relevant), and the behavioural knobs `Func<Term,bool> isAnonymous`, `string freshVarPrefix`.
- **Guard state**: `HashSet<string> visited` on the active resolution path, keyed on `VarTerm.Name`.
- **Cycle rule**: encountering a `VarTerm.Name` already on the active path is a cycle.
- **On cycle**: raise `CompileError` (see C3). *(The existing inner `ResolveTerm` short-circuit that returns the revisited node to terminate a benign self-reference is preserved; the hard-fail is the outer guarantee when the closure is genuinely unbreakable — see research.md Open items.)*
- **On acyclic/DAG/deep**: identical result to pre-feature (FR-005). Sharing a subterm (DAG) is NOT a cycle.

## C2 — Cycle-guarded structural family (fuel / identity keyed)

**Consumers**: the six `codegen.cs` structure/ground walkers, `project_linker.ResolveGoal`.

- **Input**: a `Term`/`Goal` and a traversal budget (`long fuel`) and/or a `HashSet<Term>` under `ReferenceEqualityComparer.Instance`.
- **Cycle rule**: re-encountering the same node object (identity) OR exhausting fuel ⇒ cycle. This covers a programmatically-constructed self-referential AST node that has no var-name to key on.
- **On cycle**: raise `CompileError` (C3).
- **On acyclic**: identical emitted ops / result to pre-feature (FR-005). A deep-but-finite acyclic term MUST complete (fuel sized so no legitimate program trips it; FR-006).

## C3 — Cyclic-term error

- Type: existing `CompileError` (`error.cs:13`).
- Shape: `new CompileError(message, line, column, phase: <"analyzer"|"codegen"|"term_traversal">)`.
- `message`: names it as a cyclic-term compile error and, where available, the offending variable/functor.
- MUST be catchable by the existing compile driver (it already catches `CompileError`); MUST NOT be a `StackOverflowException` and MUST NOT terminate the process (SC-001).

## C4 — Single-implementation invariant

- Exactly ONE implementation of C1's guard and ONE of C2's guard exist in the codebase (SC-003).
- The substitution/resolve/unify primitives exist in exactly ONE shared module consumed by both `analyzer.cs` and `partial_evaluator.cs` (SC-004) — no second copy remains.

## C5 — Behaviour-parity acceptance

- The full REPL suite (`test/run_all_tests.sh`) and the C# engine build/tests produce results identical to the pre-feature baseline (SC-005).
- New tests: (a) each guarded walker on a cyclic `Term` → `CompileError`, bounded, no overflow (SC-001); (b) the F-069-1 repro compiles to a diagnostic, not a crash (SC-002); (c) a deep acyclic term and a DAG-shared term both traverse OK (SC-006).
