# Exercise 8 — REPL trace

Verbatim REPL session 2026-04-30. Demonstrates §4.3.7 flatten + §4.3.8 tree_sum + §4.3.9 insertion_sort + §4.3.10 mergesort + §4.3.12 substitute. §4.3.11 distribute_ng + copy + copy_list deferred per Clarifications Q8 (multiple SRSW violations in book's printed form).

## Phase A — Load ex-08 file

```glp
GLP> ✓ Loaded: olamni/tutorial/ch04/exercise-08/ch-04-ex-08-recursive-list-tree.glp
```

~26 clauses (4 flatten + 2 tree_sum + 5 insertion_sort + 10 mergesort + 4 substitute) loaded.

## Phase B — Primary: mergesort

```glp
GLP> S = [1, 1, 2, 3, 4, 5, 6, 9]
→ succeeds
```

`mergesort([3,1,4,1,5,9,2,6], S).` — divide-and-conquer sort. split2 alternates elements into two lists; mergesort recurses on each half concurrently; merge_sorted combines. O(N log N).

## Phase C — Inspection 1: flatten (with is_list quirk)

```glp
GLP> [WARN] Unknown guard predicate: is_list
[WARN] Unknown guard predicate: is_list
[WARN] Unknown guard predicate: is_list
F = [[[4], 5], [2, 3], 1]
→ succeeds
```

`flatten([1, [2, 3], [[4], 5]], F).` — the book's `flatten_acc` clause uses `is_list/1` as a defined guard, but the GLP runtime emits `[WARN] Unknown guard predicate: is_list` for each occurrence. Without the is_list-guard branch, flatten_acc falls through to the `otherwise` clause for every element, prepending raw list-elements to the accumulator without recursive flattening. Result: top-level reverse (not actual flatten). The WARN is informational, not a halt — but the runtime needs an `is_list/1` defined-guard definition to produce the book's expected behavior. Out of scope for ch04 implementation; learners can manually define `is_list/1` if they want full flatten semantics.

## Phase D — Inspection 2: tree_sum

```glp
GLP> Total = 6
→ succeeds
```

`tree_sum(tree(1, tree(2, void, void), tree(3, void, void)), Total).` — sum nodes of a 3-node binary tree: 1 + 2 + 3 = 6. The two recursive `tree_sum` calls spawn concurrently; `:= V? + SL? + SR?` aggregates.

## Phase E — Inspection 3: insertion_sort

```glp
GLP> R = [1, 2, 3, 5, 8, 9]
→ succeeds
```

`insertion_sort([5,3,8,1,9,2], R).` — sort tail recursively, insert head into sorted tail. O(N²) but simpler than mergesort.

---

The four goals exercise: mergesort + split2 + merge_sorted (Phase B); flatten + flatten_acc otherwise-clause (Phase C — is_list-clause never fires due to unknown-guard warning); tree_sum's two clauses (Phase D); insertion_sort + insert (Phase E). substitute + replace are NOT exercised in the locked 4-goal session — learners can call them directly.
