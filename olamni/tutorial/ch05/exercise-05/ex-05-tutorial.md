# Exercise 5 — Typed Quicksort (Chapter Flagship)

Welcome to chapter 5, exercise 5. This is the chapter's flagship — the §5.6 typed quicksort. It composes everything from §5.1–§5.5 into one program: a type definition, three procedure declarations with mode marks, six clauses, recursion across two of the predicates, guards on every recursive partition step.

Two small amendments versus the printed PDF are explained inline in the `.glp`'s header (a typo in one of the procedure declarations + a layout reorganisation the REPL parser requires). Clause text is byte-exact PDF; only the qsort signature and the section layout are amended.

## Before you start

Read book §5.6 (Complete Example: Typed Quicksort, p 51). It's a one-page program-walk that ties §5.1 + §5.3 + §5.4 together. Have ex-01..ex-04 in your head when you read it.

## What's in this file

`ch-05-ex-05-typed-quicksort.glp` contains, byte-exact from book p 51 (with the two amendments noted):

- `NumList ::= [] ; [Number | NumList].` — duplicated inline from ex-01.
- `procedure quicksort(NumList?, NumList).` + 1 clause (entry — hands input to qsort with empty accumulator).
- `procedure qsort(NumList?, NumList, NumList?).` + 2 clauses (recursive split-merge; base case).
- `procedure partition(NumList?, Number?, NumList, NumList).` + 3 clauses (less-than, greater-or-equal, base).

The accumulator-tail trick lets the program avoid `append`: `Sorted1` is the head of the sorted larger-half, and `[X|Sorted1]` is wedged between the sorted smaller-half and `Sorted1`.

## The exercise

### Step 1 — Open the REPL

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Load the ex-05 file

At the `GLP>` prompt:

```
olamni/tutorial/ch05/exercise-05/ch-05-ex-05-typed-quicksort.glp
```

Expected: `✓ Loaded: …`. The mode-check passes for all six clauses across three procedures. Cross-check: trace's **Phase A**.

### Step 3 — Run the primary demo goal: an 8-element sort

```
quicksort([3,1,4,1,5,9,2,6],S).
```

Expected: `S = [1, 1, 2, 3, 4, 5, 6, 9]`. Quicksort sorts the canonical 8-element demo input from §5.6. Note the duplicate `1` is correctly preserved. The goal exercises `quicksort/2`'s entry, `qsort/3`'s recursive case, both `partition/4` comparison clauses, the `partition/4` base case at each leaf, and the `qsort/3` base case at each empty-input recursion. Cross-check: **Phase B**.

### Step 4 — Inspection 1 — empty list

```
quicksort([],S).
```

Expected: `S = []`. `quicksort` calls `qsort([], Sorted, [])` which matches `qsort([], Rest?, Rest).` directly: `Sorted = []`. Exercises the qsort base alone. Cross-check: **Phase C**.

### Step 5 — Inspection 2 — single element

```
quicksort([7],S).
```

Expected: `S = [7]`. `qsort` pivots on `7`, calls `partition([], 7, Smaller, Larger)` → both `[]`, then recurses through the qsort base case to build `[7]`. Cross-check: **Phase D**.

### Step 6 — Inspection 3 — reverse-sorted (worst case)

```
quicksort([5,4,3,2,1],S).
```

Expected: `S = [1, 2, 3, 4, 5]`. Reverse-sorted input is quicksort's worst case — every pivot puts all subsequent elements into Smaller, never into Larger. This goal exercises `partition/4`'s `A? >= X?` clause heavily and never the `A? < X?` clause. Confirms the partition's two-clause split correctly routes by comparison. Cross-check: **Phase E**.

### Step 7 — Cross-check against the trace

Open `ex-05-repl-trace.md` and confirm.

## What you've learned

By the end of this exercise you have seen:

1. **A complete typed program** — type definition, three procedure declarations with mode marks, recursion across multiple typed predicates, guards on every comparison clause.
2. **Accumulator-tail composition without `append`** — `qsort` builds the sorted output by handing each recursion an accumulator-tail to wedge content into. Read the recursive qsort clause carefully — it's the algorithmic heart of the program.
3. **Multi-procedure mode-checking** — the type-checker validates not just `merge/3` (one procedure with three clauses) but the entire `quicksort + qsort + partition` cluster. Every body call's mode must match its target's declaration; the file loads only when every clause-and-call pair is consistent.

ex-06 + ex-07 are the chapter's negative-exercise pair. Both are **meant to fail to load** with documented error messages from the type-checker. Each has a companion corrected file showing the fix.
