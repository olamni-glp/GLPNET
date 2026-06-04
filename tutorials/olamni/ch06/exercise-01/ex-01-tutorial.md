# Exercise 1 — §6.1 Difference Lists

Welcome to chapter 6, exercise 1.  This is the first of five exercises that
synthesise chapter 6's content from earlier chapters of the book — chapter 6
of `GLP_ART.pdf` is a stub (book p 53 contains only the chapter title, a
one-line intro sentence, and the five §6.x section headings; no body text and
no native Programs).

## Why a synthesis?

Per `/speckit-clarify` Q1 (option B), the §6.1 "Difference Lists" exercise is
synthesised from **ch04 §4.3.7** (`flatten/2` + `flatten_acc/3`, book pp 38–
39).  The flatten-with-accumulator pattern threads a partial-result list
through the recursion in the exact shape of a difference-list `List \ List?`
idiom — the §6.1 banner topic.  This is the closest match in chapters 1–5 to
the §6.1 heading.

The clause text in `ch-06-ex-01-difference-lists.glp` is **byte-exact from
ch04 §4.3.7**.  Only the type definition (`NestedList`) and the two
`procedure` declarations (`flatten/2` and `flatten_acc/3`) are introduced
fresh under §6.1 — these were absent from the un-typed ch04 source.

## Before you start

Read book §6.1 (Difference Lists) — the heading is there but no body, so the
PDF tells you almost nothing.  Then read book §4.3.7 (book pp 38–39): that
is the actual source of every clause in this exercise.  The §6.1 framing is
"this is a difference-list-shaped algorithm"; the §4.3.7 framing is
"flatten-with-accumulator".  They are the same code, different angle.

## What's in this file

`ch-06-ex-01-difference-lists.glp` contains:

- `NestedList ::= [] ; [_ | NestedList].` — the input/output type.  The
  element is `_` (any term) per `typed-glp-manual.md` §18.3 exception:
  flatten consumes a heterogeneous tree of atoms and sub-lists, and the
  tight-typing discipline cannot express the element type without a wrapper
  that would violate the byte-exact source mandate.  The is_list/1 +
  ground/1 guards in the recursive list-head clause discriminate atom-vs-
  sublist at run time.
- `procedure flatten(NestedList?, NestedList).` + 1 clause (entry — hands
  input to flatten_acc with an empty initial accumulator).
- `procedure flatten_acc(NestedList?, NestedList?, NestedList).` + 3 clauses
  (base case; recursive list-head case guarded by `ground` + `is_list`;
  recursive atom-head case via `otherwise`).

## The exercise

### Step 1 — Open the REPL

```bash
./glp_runtime/glp_repl.exe
```

### Step 2 — Load the ex-01 file

At the `GLP>` prompt:

```
D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-01/ch-06-ex-01-difference-lists.glp
```

Expected: `✓ Loaded: …`.  The mode-check passes for all four clauses across
two procedures.  Cross-check: trace's **Phase A**.

### Step 3 — Run the primary demo goal: a two-deep nested list

```
flatten([[1,2],[3,[4,5]]], Out).
```

Expected: `Out = [5, 4, 3, 2, 1]`.  The result is in REVERSE order — leaves
are PRE-pended onto the accumulator via `[X?|Acc?]` in the otherwise clause.
This is the byte-exact ch04 §4.3.7 behaviour; the §6.1 typed presentation
does not change the algorithm.  Every clause of `flatten_acc/3` fires at
least once during this evaluation.  Cross-check: **Phase B**.

### Step 4 — Inspection 1 — empty input

```
flatten([], Out).
```

Expected: `Out = []`.  The base clause `flatten_acc([], Acc, Acc?).` fires
once and returns.  Cross-check: **Phase C**.

### Step 5 — Inspection 2 — singleton sub-list

```
flatten([[1]], Out).
```

Expected: `Out = [1]`.  Outer cons has one sub-list `[1]`; recursing into it
calls `flatten_acc([1], [], Acc1)` which takes the otherwise branch (1 is
not a list), giving `Acc1 = [1]`; the outer call then bottoms out via the
base case.  Cross-check: **Phase D**.

### Step 6 — Inspection 3 — flat input (no nesting)

```
flatten([1,2,3], Out).
```

Expected: `Out = [3, 2, 1]`.  Three otherwise iterations prepend `1`, then
`2`, then `3` onto the accumulator, then the base case fires on the empty
tail.  Result is reversed — the same property as Phase B but more visible at
this length.  Cross-check: **Phase E**.

### Step 7 — Cross-check against the trace

Open `ex-01-repl-trace.md` and confirm each goal's output.

## What you've learned

By the end of this exercise you have seen:

1. **A typed difference-list-shaped algorithm** — `flatten_acc/3`'s
   accumulator threading is the same shape as a difference-list `List \
   List?` pair: hand each recursion the partial-result so far, get back the
   extended partial-result.  The §6.1 banner names the technique; the ch04
   §4.3.7 source is one canonical instance.
2. **The byte-exact source mandate in action** — every clause in this file
   came from ch04 §4.3.7 character-for-character.  Only the surrounding
   type and procedure declarations were authored fresh at §6.1.
3. **A `_`-element exception to tight typing** — `NestedList ::= [] ; [_ |
   NestedList]` is the smallest typing that admits the heterogeneous input.
   `typed-glp-manual.md` §18.3 documents this exception class.

## What ex-02 brings next

Exercise 2 is §6.2 Quicksort — synthesised from ch05 §5.6, byte-exact down to
the type definitions and procedure declarations because that source was
already typed.  ex-02 is the only ch06 exercise where the declarations are
ALSO byte-exact (not just the clauses).
