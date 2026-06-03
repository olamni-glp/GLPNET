# Exercise 1 — REPL trace

This trace is the verbatim transcript of an actual GLP REPL session run on this Windows host on 2026-05-01. It demonstrates the §5.1 type definitions (`Bit`, `Nat`, `NumList`) loaded into a single file along with three minimal recogniser predicates (`is_bit/1`, `is_nat/1`, `is_numlist/1`), exercised via four goals — a primary plus three inspections covering Bit, Nat (recursive case), and NumList (typed-cons case + base case).

## Phase A — Load ex-01 file

```glp
GLP> olamni/tutorial/ch05/exercise-01/ch-05-ex-01-type-definitions.glp
✓ Loaded: olamni/tutorial/ch05/exercise-01/ch-05-ex-01-type-definitions.glp
```

The three type definitions and three recogniser procedures are now in the REPL. No SRSW errors, no type errors — the type-checker accepts the recursive `Nat` and `NumList` shapes and the matching recogniser clauses.

## Phase B — Primary demo goal: probe `Bit`

```glp
GLP> is_bit(0).
→ succeeds
```

Committed choice picks the first matching clause `is_bit(0).`; the constant `0` satisfies the declared `Bit?` argument; the unit clause has no body so the goal succeeds. Locked outcome: `→ succeeds`.

## Phase C — Inspection 1: probe the recursive `Nat` type

```glp
GLP> is_nat(s(s(0))).
→ succeeds
```

The recursive clause `is_nat(s(N)) :- is_nat(N?).` peels the outer `s`, recurses on `s(0)`, peels again, recurses on `0`, and the base clause `is_nat(0).` succeeds. Termination via the base case.

## Phase D — Inspection 2: typed `NumList` with valid contents

```glp
GLP> is_numlist([1,2,3]).
→ succeeds
```

The recursive clause `is_numlist([N|Rest]) :- number(N?) | is_numlist(Rest?).` walks the cons cells; each `number/1` guard succeeds because the elements are integers; recursion bottoms out at `is_numlist([]).`.

## Phase E — Inspection 3: empty-list base case

```glp
GLP> is_numlist([]).
→ succeeds
```

Matches the base clause `is_numlist([]).` directly. Termination case for the recursive recogniser.

## Closing

```glp
GLP> :quit
Goodbye!
```

---

The four goals exercise all three type-recogniser predicates. Phase B exercises `is_bit/1`'s base; phase C exercises `is_nat/1`'s recursion plus its base; phase D exercises `is_numlist/1`'s recursive cons clause plus the multi-reader-permissive `number/1` guard; phase E exercises `is_numlist/1`'s base. Together they show that type definitions are loaded into the type-checker's environment and that procedure declarations of the form `procedure is_<name>(<Type>?).` route arguments through the declared type at goal-evaluation time. ex-02 (next exercise) adds the universal `List` type to the picture and contrasts its acceptance with `is_numlist`'s.
