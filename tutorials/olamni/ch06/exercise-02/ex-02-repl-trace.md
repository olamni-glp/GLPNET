# ch06 ex-02 — §6.2 Quicksort — REPL trace

This trace captures the verbatim REPL session for ex-02.  Five phases: A
loads the `.glp`; B runs the canonical 8-element sort; C, D, E run three
inspection goals.

## Phase A — Build / load

```glp
GLP> D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-02/ch-06-ex-02-typed-quicksort.glp
✓ Loaded: D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-02/ch-06-ex-02-typed-quicksort.glp
```

## Phase B — Primary demo goal: 8-element sort

```glp
GLP> quicksort([3,1,4,1,5,9,2,6], S).
S = [1, 1, 2, 3, 4, 5, 6, 9]
→ succeeds
```

The canonical 8-element sort from ch05 §5.6.  The duplicate `1` is correctly
preserved.  Exercises `quicksort/2`'s entry, `qsort/3`'s recursive case,
both `partition/4` comparison clauses, the `partition/4` base case at each
leaf, and the `qsort/3` base case at each empty-input recursion.

## Phase C — Inspection 1: empty list

```glp
GLP> quicksort([], S).
S = []
→ succeeds
```

`quicksort` calls `qsort([], Sorted, [])` which matches `qsort([], Rest?,
Rest).` directly: `Sorted = []`.  Exercises only the qsort base.

## Phase D — Inspection 2: singleton

```glp
GLP> quicksort([5], S).
S = [5]
→ succeeds
```

`qsort` pivots on `5`, calls `partition([], 5, Smaller, Larger)` → both
`[]`, then recurses through the qsort base case to build `[5]`.

## Phase E — Inspection 3: small unsorted list

```glp
GLP> quicksort([3,1,2], S).
S = [1, 2, 3]
→ succeeds
```

A 3-element input that exercises both partition comparison clauses without
the volume of Phase B.

---

This typed quicksort is byte-exact from ch05 §5.6 (book p 51), including
the ch05 Q10 dual amendments (corrected qsort declaration `(NumList?,
NumList, NumList?)` + interleaved layout).  ex-02 is the only ch06 exercise
where the type definitions and procedure declarations are ALSO byte-exact
from the source chapter — the other four ch06 exercises introduce
declarations fresh under §6.x.
