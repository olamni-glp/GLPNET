# Exercise 3 — REPL trace

This trace is the verbatim transcript of an actual GLP REPL session run on this Windows host on 2026-05-01. It demonstrates the §5.3 procedure declaration and §5.4 typed `merge/3`, exercised via four goals — a primary fair merge plus three inspections covering the both-empty base case and the two asymmetric (full/empty) cases.

## Phase A — Load ex-03 file

```glp
GLP> olamni/tutorial/ch05/exercise-03/ch-05-ex-03-mode-checked-merge.glp
✓ Loaded: olamni/tutorial/ch05/exercise-03/ch-05-ex-03-mode-checked-merge.glp
```

The `List` type, the `procedure merge(List?, List?, List).` declaration, and the three `merge/3` clauses are now in the REPL. The mode-check passed silently for every clause — each variable's writer/reader pair pattern matches the declared modes.

## Phase B — Primary demo goal: a typed fair merge

```glp
GLP> merge([1,3],[2,4],M).
M = [1, 2, 3, 4]
→ succeeds
```

Clause 1 selects `[1|…]` from arg 1, then clause 2 selects `[2|…]` from arg 2, alternating until both inputs are exhausted; clause 3 closes the output with `[]`. The argument swap inside each recursive body is what produces the alternation.

## Phase C — Inspection 1: both-empty base case

```glp
GLP> merge([],[],M).
M = []
→ succeeds
```

Clause 3 `merge([], [], []).` matches directly. Termination case for the recursion.

## Phase D — Inspection 2: atoms in arg 1, empty arg 2

```glp
GLP> merge([a,b],[],M).
M = [a, b]
→ succeeds
```

Clause 1 fires twice (peeling `a` then `b`), then clause 3 terminates. The atoms `a` and `b` pass the type-check because the universal `List`'s element type is `Any`.

## Phase E — Inspection 3: empty arg 1, atoms in arg 2

```glp
GLP> merge([],[c,d],M).
M = [c, d]
→ succeeds
```

Symmetric to Phase D — clause 2 fires twice, then clause 3 terminates.

## Closing

```glp
GLP> :quit
Goodbye!
```

---

The four goals together exercise all three clauses of the typed `merge/3`. Phase B exercises clauses 1 + 2 + 3 in alternation; Phase C exercises clause 3 alone; Phase D exercises clause 1 then clause 3; Phase E exercises clause 2 then clause 3. The mode-check at load time is what distinguishes this from chapter 4's untyped `merge/3` — every variable's writer/reader pattern was validated against the procedure declaration before the goals ran.
