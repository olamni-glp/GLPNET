# Exercise 8 — Recursive List/Tree

Welcome to chapter 4, exercise 8 — last in the §4.3 group. This exercise covers book §4.3's second half: recursive programming on list/tree data structures. The structural-recursion patterns ex-07 introduced for Peano successors generalise naturally to list cons cells and tree nodes.

## Q8 amendment + is_list runtime quirk

Two implementation notes apply to this exercise:

- **Q8** (deferred §4.3.11 distribute_ng + copy + copy_list): book p 40 has multiple SRSW violations in these Programs — output writers Y/Z appear in BOTH head and body across all three `copy/3` clauses; reader F? appears twice in clause 3. Each clause needs the systematic `?`-amendment treatment that Q5 + Q9 applied to other Programs, but the cumulative cost across distribute_ng + copy + copy_list is high and the resulting code would diverge non-trivially from book p 40. Per Q8, ex-08 includes 5 of 6 §4.3 list/tree Programs (flatten, tree_sum, insertion_sort, mergesort, substitute); §4.3.11 is deferred to a separate book-wide audit branch.

- **`is_list/1` runtime gap**: book p 38's `flatten_acc` clause uses `is_list/1` as a defined guard:
  ```
  flatten_acc([X|Xs], Acc, Ys?) :- ground(X?), is_list(X?) | ...
  ```
  But the GLP runtime emits `[WARN] Unknown guard predicate: is_list` and falls through to the second clause's `otherwise` guard. The result: the locked flatten goal produces a top-level reverse (not full nested-list flatten). This is an upstream gap in `programs/self.glp` — `is_list/1` is not declared as a defined-guard predicate. Learners who want full flatten semantics can manually define `is_list([]).` + `is_list([_|_]).` and add the guard to self.glp. Reporting upstream is recommended; out of scope for this branch.

This is now five book-internal SRSW + runtime-defined-guard inconsistencies surfaced during ch01–ch04 (Q3a + Q4 + Q5 + Q7 + Q8 — Q9 + Q10 not yet counted at this point in the chapter implementation).

## Before you start

ex-07 (the §4.3 group entry) must be approved. Read book §4.3's "Flattening Nested Lists" + "Binary Trees" + "Insertion Sort" + "Merge Sort" + "Tree Substitution" subsections (book pp 38–40). The §4.3.11 "Non-Ground Stream Distributor" subsection (book p 40) is in scope of book reading but deferred from `.glp` implementation per Q8.

## What's in the file

`ch-04-ex-08-recursive-list-tree.glp` — ~26 clauses byte-exact from book pp 38–40 (with §4.3.11 omitted per Q8):

- **§4.3.7 Flatten** (book p 38): `flatten/2` + `flatten_acc/3` (4 clauses; flatten_acc has 3 clauses — base + ground+is_list recursive + otherwise recursive)
- **§4.3.8 Binary tree sum** (book p 39): `tree_sum/2` (2 clauses; recursive case spawns two concurrent recursive calls per Formal 4.2 SRSW-in-continuation)
- **§4.3.9 Insertion sort** (book p 39): `insertion_sort/2` + `insert/3` (5 clauses)
- **§4.3.10 Merge sort** (book pp 39–40): `mergesort/2` + `split2/5` + `merge_sorted/3` (~10 clauses; mergesort has 3 clauses, split2 has 3, merge_sorted has 4)
- **§4.3.12 Tree substitution** (book p 40): `substitute/4` + `replace/4` (4 clauses)

Out of scope for this file (per Q8): §4.3.11 `distribute_ng/3` + `copy/3` + `copy_list/3` (book p 40).

## The exercise

### Step 1 — Open the REPL

If your REPL session from ex-07 is still open, you can `:quit` it and start fresh. Otherwise:

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Load the ex-08 file

```
olamni/tutorial/ch04/exercise-08/ch-04-ex-08-recursive-list-tree.glp
```

You should see `✓ Loaded:`. All ~26 clauses are now in the procedure table. Cross-check trace **Phase A**.

### Step 3 — Run the primary demo goal: mergesort

```
mergesort([3,1,4,1,5,9,2,6], S).
```

Expected: `S = [1, 1, 2, 3, 4, 5, 6, 9]` and `→ succeeds`.

What happens internally: mergesort's recursive clause matches the 8-element input. `split2(3, 1, [4,1,5,9,2,6], Left, Right)` alternates elements between two output lists: Left gets odd-position elements (3, 4, 5, 2 plus the leading 3), Right gets even-position (1, 1, 9, 6 plus the leading 1). Then mergesort recurses on Left and Right concurrently (two spawned processes). At the bottom of each recursion the singleton or empty base cases fire. Finally `merge_sorted/3` combines the two sorted halves using `=<` / `>` guards on numeric heads. Total work: O(N log N). Cross-check trace **Phase B**.

### Step 4 — Run the three inspection goals

#### Inspection 1 — Flatten (with is_list runtime quirk)

```
flatten([1, [2, 3], [[4], 5]], F).
```

Expected: `F = [[[4], 5], [2, 3], 1]` and `[WARN] Unknown guard predicate: is_list` (3 occurrences).

The book's intended behavior: full nested-list flatten producing `F = [5, 4, 3, 2, 1]` (or some permutation). The actual GLP-runtime behavior with the `is_list/1` gap: `flatten_acc`'s second clause (`ground(X?), is_list(X?) | ...`) NEVER fires because `is_list/1` isn't recognised; the third clause (`otherwise | ...`) fires for every input element instead, prepending raw list-elements to the accumulator without recursive flattening. Result: top-level reverse of the input, not full flatten.

The WARN is informational, not a halt. The 4-goal session continues. Per Q8 + the is_list note above, fixing this requires upstream changes to `programs/self.glp`. Cross-check trace **Phase C**.

#### Inspection 2 — Binary tree sum

```
tree_sum(tree(1, tree(2, void, void), tree(3, void, void)), Total).
```

Expected: `Total = 6` and `→ succeeds`.

A 3-node tree: root value 1, left subtree is leaf 2, right subtree is leaf 3. tree_sum's recursive clause matches the root, spawning TWO concurrent `tree_sum` calls (one for each subtree) per Formal 4.2 (the recursive calls pass readers L?, R?, not writers). At each subtree the recursion descends to `void` (the base case `tree_sum(void, 0).`). The aggregate `S := V? + SL? + SR?` collects all subtree sums plus the root value: 1 + 2 + 3 = 6. Cross-check trace **Phase D**.

#### Inspection 3 — Insertion sort

```
insertion_sort([5,3,8,1,9,2], R).
```

Expected: `R = [1, 2, 3, 5, 8, 9]` and `→ succeeds`.

Insertion sort recursively sorts the tail then inserts the head into the sorted tail. `insertion_sort([X|Xs], Sorted?)` body: `insertion_sort(Xs?, SortedTail)` produces SortedTail; `insert(X?, SortedTail?, Sorted)` places X in its sorted position. The `insert/3` procedure has three clauses — base (insert into empty), `X < Y` (prepend X), `X >= Y` (recurse past Y). Total work: O(N²). Simpler structure than mergesort but slower for large inputs. Cross-check trace **Phase E**.

### Step 5 — Cross-check against the captured trace

Open `ex-08-repl-trace.md` in this same directory. Match each phase line-for-line modulo banner. The flatten WARN messages (3 occurrences) are part of the verbatim trace; do NOT remove them when reproducing.

### Optional explorations

- **Tree substitution**:
  ```
  substitute(a, x, tree(a, tree(b, void, void), tree(a, void, void)), R).
  ```
  Expected: `R = tree(x, tree(b, void, void), tree(x, void, void))` — substitute replaces every `a` leaf with `x` recursively. Demonstrates the `replace/4` two-clause dispatch on `=?=` and `~(=?=)` (similar to ch03 `lookup/3`).

- **Custom is_list workaround**: define `is_list([]).` and `is_list([_|_]).` at the top of the file (as a unit-clause defined guard), reload, and re-run flatten. The is_list-clause should now fire and produce full flatten semantics.

- **Mergesort with duplicates**:
  ```
  mergesort([5, 2, 5, 1, 5, 2], S).
  ```
  Expected: `S = [1, 2, 2, 5, 5, 5]` — demonstrates merge_sorted's `=<` / `>` dispatch handling equal-key elements (it preserves them via `=<` taking the first when equal).

## What you've learned

By the end of this exercise (and the §4.3 group) you have seen:

1. **Divide-and-conquer mergesort with concurrent recursion** — mergesort's two recursive calls execute in parallel; split2's pairwise alternation halves the input each step. O(N log N) total work distributed across O(log N) concurrent recursion levels. Demonstrates the GLP-natural pattern for divide-and-conquer.
2. **In-place insertion sort** — O(N²) but structurally simpler. Useful for small inputs or near-sorted data.
3. **Tree recursion with concurrent subtree spawning** — `tree_sum/2`'s two recursive calls execute in parallel per Formal 4.2; subtree sums aggregate via `:=` in the parent. The same pattern extends to any binary-tree fold.
4. **Accumulator-based flatten** — the structure of accumulator-threaded recursion is observable even with the is_list runtime quirk. The Q8 deferral of §4.3.11 distribute_ng is a strategic compromise; book-wide SRSW audit will eventually re-include it.
5. **§3.2 guard negation in tree substitution** — `replace/4` uses `~(X? =?= Z?)` for the no-match clause, the same negation idiom from ch03 ex-03. Cross-chapter pattern: §3.2's guard species (built-in, defined, negation) appear throughout the rest of the book.

The §4.3 group is now complete (subject to project owner approval). ex-09 (the §4.4 group entry, gated behind §4.3 approval) introduces metaprogramming foundations: programs-as-data + trust-mode meta-interpreter.
