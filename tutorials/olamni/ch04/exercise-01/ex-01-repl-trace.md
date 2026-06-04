# Exercise 1 — REPL trace

This trace is the verbatim output of an actual GLP REPL session run on this Windows host on 2026-04-30. It demonstrates the §4.1 unit-clause Programs (`p(a)`, `q(b)/q(a)`, and the four logic gates `and/3`, `or/3`, `not/2`, `xor/3`) loaded into a single file and exercised via four goals — primary plus three inspections covering the three other gates not used in the primary.

## Phase A — Load ex-01 file

```glp
GLP> ✓ Loaded: olamni/tutorial/ch04/exercise-01/ch-04-ex-01-constants-and-gates.glp
```

The 17 unit clauses (1 for `p/1` + 2 for `q/1` + 4 each for `and/3`, `or/3`, `xor/3` + 2 for `not/2`) are now in the REPL's procedure table. No SRSW errors, no type errors — all clauses are well-formed unit clauses with constant arguments.

## Phase B — Primary demo goal: AND

```glp
GLP> R = 1
→ succeeds
```

Goal: `and(1, 1, R).` — committed choice picks the first matching clause `and(1,1,1).`; the writer `R` in the goal's third position consumes the constant `1` from the head, producing `R = 1`. Locked binding empirically confirmed.

## Phase C — Inspection goal 1: OR

```glp
GLP> X = 1
→ succeeds
```

Goal: `or(1, 0, X).` — matches the second `or` clause `or(1,0,1).`; binds `X = 1`.

## Phase D — Inspection goal 2: NOT

```glp
GLP> N = 0
→ succeeds
```

Goal: `not(1, N).` — matches the first `not` clause `not(1,0).`; binds `N = 0`.

## Phase E — Inspection goal 3: XOR

```glp
GLP> Y = 0
→ succeeds
```

Goal: `xor(0, 0, Y).` — matches the fourth `xor` clause `xor(0,0,0).`; binds `Y = 0`.

---

The four goals exercise four of the four gate predicates (`and/3`, `or/3`, `not/2`, `xor/3`); together they show all six elementary dataflow operations the chapter introduces. The pedagogical point: every clause in this file is a unit clause with constant arguments; each REPL goal triggers committed-choice clause selection on the first matching head, binds writers to constants, and succeeds. ex-02 (next exercise) introduces clauses with bodies and `ground` guards on multiple readers to compose these gates into a half-adder and full-adder.
