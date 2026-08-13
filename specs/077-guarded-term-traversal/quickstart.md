<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Quickstart: verifying feature 077 (guarded term-traversal)

Prerequisites: this host's dart SDK for the REPL suite; .NET SDK for the C# engine (`out/csharp`).

## 1. Baseline (before any change)

```
cd D:/bstdev/research/glp/glpnet
DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart.exe bash test/run_all_tests.sh   # expect 547/547
cd out/csharp && dotnet build && dotnet test                                     # expect 0 errors, green
```
Commit the green baseline before changing code (DISCIPLINE §2.2).

## 2. Reproduce the crash class (pre-fix — expected to StackOverflow)

The F-069-1 shape: a defined guard `p(X, s(X))` called as `p(Y, Y)` forms a self-referential substitution `Y → s(Y)`; `ApplySubstitution`'s transitive closure recurses without bound.

- Confirm the pre-fix behaviour is an uncatchable `StackOverflowException` during compile (documented in feature 069's SC-003 notes; 069 used a non-cyclic-scoping workaround to avoid it).

## 3. After the fix — the acceptance checks

1. **Cyclic term → CompileError, not crash** (SC-001): feed a cyclic `Term` to each guarded walker (unit tests) and to the full compile via the F-069-1 repro; assert a `CompileError` is raised, caught, and surfaced as a diagnostic — zero `StackOverflowException`, process survives.
2. **069 fuzz without the workaround** (SC-002): the feature-069 SC-003 fuzz corpus runs cyclic-`=` inputs directly (no non-cyclic-scoping workaround) and terminates with diagnostics.
3. **One guard, one module** (SC-003/SC-004): inspect — exactly one shared guard implementation; the unify/substitution/resolve primitives live in one shared module used by both `analyzer.cs` and `partial_evaluator.cs`; no second copy.
4. **Acyclic parity** (SC-005): `test/run_all_tests.sh` = 547/547 identical to baseline; `dotnet test` green; cross_runtime parity banners unchanged (modulo build-hash/cwd-casing).
5. **Deep acyclic + DAG not falsely rejected** (SC-006): a large deep acyclic term and a DAG-shared term both compile normally.

## 4. Divergence watch (from research.md Decision 3)

- Confirm the anonymous-var semantics divergence (#1/#3) is preserved per-caller via the `isAnonymous` parameter — analyzer still treats `_Foo` as anonymous, PE still treats only `_` as anonymous. A change here would show up as REPL suite diffs; if the suite moves, STOP (it means the dedup silently changed unification semantics — the §II trap).
