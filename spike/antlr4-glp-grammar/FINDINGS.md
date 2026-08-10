<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Fuzz findings — SC-002 IL-parity bridge (feature 069, US2)

Findings surfaced by the bounded generative fuzz (`parity/GrammarFuzzer.cs`, T017/T018). A finding is
either an IL-parity divergence (the feature's target) or — as below — a defect in the SHARED downstream
pipeline that both front-ends feed, exposed because the fuzz drives many valid programs through it.

---

## F-069-1 🔴 Production-engine stack overflow: `DefinedGuardEvaluator._ApplySubstitution` (no occurs-check)

**Status:** REPORTED — awaiting engineer decision (Bug Protocol; FR-010 forbids the bridge touching
production). NOT a parity divergence: both front-ends reach the identical shared `PipelineDriver` and
crash identically, so this is a shared-pipeline robustness defect, not an A-vs-B mismatch.

**Discovered by:** `--fuzz` at deterministic index 23 (seed 2654435769).

**Expected:** compiling a clause whose guard is a self-referential unification (`X? = f(...X?...)`) either
fails the occurs-check gracefully (the defined guard does not reduce → a normal CompileError, caught by
the harness) or type-checks and compiles. A malformed/cyclic guard must never crash the compiler.

**Actual:** the process dies with an uncatchable `StackOverflowException` (so `catch (Exception)` in the
harness cannot recover). Trace top:

```
Stack overflow.
   at System.Linq.Enumerable.Select[...]
   at GlpRuntime.Compiler.DefinedGuardEvaluator._ApplySubstitution(Term, IReadOnlyDictionary<string,Term>)
   at GlpRuntime.Compiler.DefinedGuardEvaluator._ApplySubstitution(Term, IReadOnlyDictionary<string,Term>)
   ... (unbounded self-recursion) ...
```

**Minimal reproducer** (`fuzz-repro/fuzz-23-min.glp`):

```prolog
procedure p(Number?, Number?, Constant).
p(A, B, yes) :- A? = B? * A? | true.
p(_, _, no) :- otherwise | true.
```

**Root cause (read, not run — DISCIPLINE §1.11):** `=` is a single-unit-clause defined guard
(`=(X?, X)`, manual §8) reduced at compile time by `DefinedGuardEvaluator`. Reducing `=(A?, *(B?, A?))`
binds `A` to a term that itself contains `A`. `_ApplySubstitution` then rewrites `A` → `*(B?, A)` →
`*(B?, *(B?, A))` → … forever: there is no occurs-check when the substitution is formed, and no cycle
guard when it is applied. `A` legally occurs twice because `Number` is a constant type (SRSW relaxation,
manual §3), so nothing upstream rejects the clause first.

**Why it matters for this feature:** the fuzz cannot complete its budget while a single generated input
crashes the harness, so SC-003 (full-budget, zero un-caused divergences) is blocked until this is
resolved. It is independent of parity — the bridge is correct here; the engine is not.

**Decision needed (engineer):** see the session report — fix the engine occurs-check (out of FR-010
scope for this feature → separate bug feature), vs. scope the fuzzer to non-cyclic `=` guards and record
this defect as a bounded condition carried into `DECISION.md` (T020).
