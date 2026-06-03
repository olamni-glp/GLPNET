# Exercise 4 — REPL trace

This trace is the verbatim output of an actual GLP REPL session run on this Windows host on 2026-04-30. It demonstrates §4.2's three merge variants — simple fair `merge/3` (4 clauses with early-exit empty-stream cases), dynamic `dmerge/3` + `dmerger/3` (handles stream-of-streams via `merge()` messages), and static balanced `merge_tree/2` + `merge_layer/2` (pairwise reduction).

## Phase A — Load ex-04 file

```glp
GLP> ✓ Loaded: olamni/tutorial/ch04/exercise-04/ch-04-ex-04-merge-variants.glp
```

The 17 clauses (4 simple merge + 7 dmerge + 1 dmerger + 2 merge_tree + 3 merge_layer) are now in the procedure table. Note: this `merge/3` is the §4.2.5 four-clause version (with explicit empty-stream early-exits), DIFFERENT from chapter 3's Program 3.1 three-clause version.

## Phase B — Primary demo goal: simple fair merge

```glp
GLP> Xs = [1, a, 2, b, 3]
→ succeeds
```

Goal: `merge([1,2,3], [a,b], Xs).` — fair-merge alternation produces `[1, a, 2, b, 3]`. Same fair-merge result you would expect from Program 3.1 (clauses 1 + 2 alternate via the argument-swap trick). When the second stream `[a,b]` exhausts mid-merge, clause 4 (`merge(Xs, [], Xs?).`) fires to copy the remaining first stream `[3]` directly to output — the §4.2.5 early-exit optimization vs. Program 3.1's element-by-element termination via the empty-empty base.

## Phase C — Inspection goal 1: balanced merge tree

```glp
GLP> Out = [1, 5, 3, 6, 2, 4]
→ succeeds
```

Goal: `merge_tree([[1,2],[3,4],[5,6]], Out).` — reduces three input streams pairwise. `merge_layer/2` first merges streams 1 + 2 → `[1, 3, 2, 4]` (fair merge of `[1,2]` + `[3,4]`); the third stream `[5,6]` passes through unchanged via merge_layer's singleton clause. The reduced layer `[[1,3,2,4], [5,6]]` is then merge_tree-reduced, merging those two → `[1, 5, 3, 6, 2, 4]`. The fair-merge alternation produces the observed interleaving; `merge_tree`'s recursive structure halves the layer count each step.

## Phase D — Inspection goal 2: empty first stream

```glp
GLP> R = [a, b, c]
→ succeeds
```

Goal: `merge([], [a,b,c], R).` — clause 3 (`merge([], Ys, Ys?).`) fires immediately, copying the second stream `[a,b,c]` directly to output via writer/reader pair `Ys`/`Ys?`. The early-exit avoids element-by-element traversal — the entire second stream is forwarded in a single SRSW pair-link.

## Phase E — Inspection goal 3: empty second stream

```glp
GLP> R = [1, 2]
→ succeeds
```

Goal: `merge([1,2], [], R).` — symmetric to Phase D. Clause 4 (`merge(Xs, [], Xs?).`) fires immediately, copying the first stream `[1,2]` directly to output. Demonstrates the second early-exit clause that distinguishes §4.2.5's four-clause merger from Program 3.1's three-clause version.

---

The four goals exercise: simple merge's clauses 1 + 2 (Phase B alternation), clause 3 (Phase D), clause 4 (Phase E); merge_tree's recursive case + merge_layer's three clauses (Phase C exercises pairwise merge twice + singleton-stream pass-through). dmerge + dmerger are NOT exercised in this 4-goal session — their dynamic-merge-message dispatch requires struct-tagged stream input which is more complex to construct as a primary goal. A learner who wants to exercise dmerge can call `dmerger(Ws, Xs, Out).` with a stream containing tagged messages; the implementation is byte-exact from book p 33 and ready for use.
