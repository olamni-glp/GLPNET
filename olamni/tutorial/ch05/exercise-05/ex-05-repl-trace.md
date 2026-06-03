# Exercise 5 — REPL trace

This trace is the verbatim transcript of an actual GLP REPL session run on this Windows host on 2026-05-01. It demonstrates the §5.6 typed quicksort, exercised via four goals — the canonical 8-element sort from the book plus three inspections covering empty input, single element, and reverse-sorted (worst-case) input.

## Phase A — Load ex-05 file

```glp
GLP> olamni/tutorial/ch05/exercise-05/ch-05-ex-05-typed-quicksort.glp
✓ Loaded: olamni/tutorial/ch05/exercise-05/ch-05-ex-05-typed-quicksort.glp
```

The `NumList` type, three procedure declarations, and six clauses are loaded. The mode-check passes for every clause across all three procedures.

## Phase B — Primary demo goal: the canonical 8-element sort

```glp
GLP> quicksort([3,1,4,1,5,9,2,6],S).
S = [1, 1, 2, 3, 4, 5, 6, 9]
→ succeeds
```

The 8-element input sorts correctly with the duplicate `1` preserved. Exercises every clause of every procedure: `quicksort/2`'s entry, `qsort/3`'s recursive + base, both `partition/4` comparison clauses + base.

## Phase C — Inspection 1: empty input

```glp
GLP> quicksort([],S).
S = []
→ succeeds
```

`quicksort` immediately delegates to `qsort([], Sorted, [])`, which matches the qsort base directly.

## Phase D — Inspection 2: single element

```glp
GLP> quicksort([7],S).
S = [7]
→ succeeds
```

`qsort` pivots on `7`, calls `partition([], 7, Smaller, Larger)` (matching the partition base), then recurses through the qsort base to build `[7]`.

## Phase E — Inspection 3: reverse-sorted (worst case)

```glp
GLP> quicksort([5,4,3,2,1],S).
S = [1, 2, 3, 4, 5]
→ succeeds
```

Reverse-sorted input is quicksort's worst case. Every pivot puts the rest of the list into Smaller; the `partition/4` clause `A? >= X?` fires repeatedly, the `A? < X?` clause never fires. The partition correctly routes elements by comparison even when one branch carries everything.

## Closing

```glp
GLP> :quit
Goodbye!
```

---

The four goals exercise all 6 clauses of typed quicksort plus all 3 procedure declarations. Phase B hits both partition comparison clauses and recursion through both `qsort` clauses; Phase C hits the qsort base alone; Phase D hits the qsort recursion + partition base; Phase E hits the partition `A? >= X?` clause throughout and demonstrates that the worst-case input still produces a correct result.
