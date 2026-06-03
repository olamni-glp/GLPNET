# Exercise 7 — REPL trace

Verbatim REPL session 2026-04-30. Demonstrates §4.3.1 Peano arithmetic + §4.3.2 integer arithmetic + §4.3.3 naive factorial + §4.3.5 naive Fibonacci + §4.3.6 linear Fibonacci. §4.3.4 tail-recursive factorial deferred per Clarifications Q6 (would conflict with naive factorial via GLP non-contiguous-clauses). fib_acc has a Q7 multi-reader guard amendment.

## Phase A — Load ex-07 file

```glp
GLP> ✓ Loaded: olamni/tutorial/ch04/exercise-07/ch-04-ex-07-recursive-numerics.glp
```

23 clauses (Peano 8 + integer arith 6 + factorial 3 + fib 3 + fib_linear/fib_acc 3) loaded.

## Phase B — Primary: naive factorial

```glp
GLP> F = 5040
→ succeeds
```

`factorial(7, F).` → 7! = 5040. The recursive case spawns: `factorial(6, F1)` then multiplies by 7. Concurrent processes for each level of recursion.

## Phase C — Inspection 1: linear Fibonacci

```glp
GLP> G = 6765
→ succeeds
```

`fib_linear(20, G).` → Fib(20) = 6765. fib_acc threads two accumulators A, B (last two Fibs); each step produces the next Fib in O(N) total work, vs. naive fib's O(2^N).

## Phase D — Inspection 2: Peano addition

```glp
GLP> R = s(s(s(s(s(0)))))
→ succeeds
```

`plus(s(s(0)), s(s(s(0))), R).` → 2+3 = 5 in Peano successor notation. Demonstrates structural recursion on the natural-number representation.

## Phase E — Inspection 3: max

```glp
GLP> M = 7
→ succeeds
```

`max(7, 3, M).` → committed-choice on `X >= Y` succeeds (7 >= 3), so first clause fires: M = 7.

---

The four goals exercise: factorial's three clauses (Phase B fires recursive case which fires base via factorial(1) → 1 sub-call); fib_linear's entry + fib_acc's recursive clause (Phase C); Peano plus's two clauses (Phase D fires recursive case twice then base); max's first clause (Phase E). Peano times + lesseq + natural_number + integer arith's double + average + abs + naive fib are NOT exercised in the locked 4-goal session — learners can call them directly.
