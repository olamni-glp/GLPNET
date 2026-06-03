# Exercise 4 — Merge Variants

Welcome to chapter 4, exercise 4. This exercise covers three merge variants from book §4.2:

- **Simple fair merge** (book p 32, 4 clauses) — like Program 3.1 from chapter 3 but with explicit empty-stream early-exit clauses.
- **Dynamic merge** dmerge/3 + dmerger/3 (book p 33, 8 clauses) — handles stream-of-streams via `merge()` messages.
- **Static balanced merge tree** merge_tree/2 + merge_layer/2 (book p 33, 5 clauses) — pairwise reduction of N streams.

## Before you start

You should have completed ex-03 (the §4.2 entry point). Read book §4.2's "Stream Merging" subsections (book pp 32–33). Note the contrast between this chapter's simple merge (4 clauses) and chapter 3's Program 3.1 (3 clauses) — same predicate name, slightly different implementation. The book's pedagogy here is the fair-merge progression from naive (Program 3.1) → fair-with-early-exits (§4.2.5) → dynamic-merge-messages (§4.2.6) → balanced-tree (§4.2.7).

## What's new in ex-04

- **Simple fair merge with early-exits**: clauses 3 + 4 (`merge([], Ys, Ys?).` and `merge(Xs, [], Xs?).`) avoid element-by-element termination by copying the remaining stream directly to output. More efficient than Program 3.1's empty-empty base.
- **Dynamic merge**: dmerge interprets `merge(Ws)` tags inside its input streams as instructions to spawn a sub-merger. This is the substrate for stream-multiplexing networks where new sources can dynamically join.
- **Balanced merge tree**: merge_tree reduces a list of N streams to a single output stream by pairwise merging adjacent streams in O(log N) layers. merge_layer does one pairwise pass.

## What's in the file

`ch-04-ex-04-merge-variants.glp` — 17 clauses byte-exact from book pp 32–33:

- 4 simple `merge/3` clauses
- 7 `dmerge/3` clauses + 1 `dmerger/3` clause
- 2 `merge_tree/2` clauses + 3 `merge_layer/2` clauses

No cross-chapter inversion or dependencies on other ch04 files; this exercise is self-contained per FR-010.

## The exercise

### Step 1 — Open the REPL + load

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

```
olamni/tutorial/ch04/exercise-04/ch-04-ex-04-merge-variants.glp
```

Expected: `✓ Loaded:`. Cross-check trace **Phase A**.

### Step 2 — Run the primary goal: fair merge with early-exit

```
merge([1,2,3], [a,b], Xs).
```

Expected: `Xs = [1, a, 2, b, 3]`. Same fair-merge alternation as Program 3.1, but when the second stream `[a,b]` exhausts mid-merge, clause 4 fires to copy the remaining first stream `[3]` to output. Cross-check trace **Phase B**.

### Step 3 — Run the three inspection goals

#### Inspection 1 — balanced merge tree

```
merge_tree([[1,2],[3,4],[5,6]], Out).
```

Expected: `Out = [1, 5, 3, 6, 2, 4]`. Three input streams reduce pairwise: layer 1 merges `[1,2]` + `[3,4]` → `[1,3,2,4]` and passes `[5,6]` through unchanged; layer 2 merges those two → `[1, 5, 3, 6, 2, 4]`. Cross-check trace **Phase C**.

#### Inspection 2 — empty first stream

```
merge([], [a,b,c], R).
```

Expected: `R = [a, b, c]`. Clause 3 fires immediately (empty first stream → copy second stream verbatim). Cross-check trace **Phase D**.

#### Inspection 3 — empty second stream

```
merge([1,2], [], R).
```

Expected: `R = [1, 2]`. Clause 4 fires immediately (empty second stream → copy first stream). Cross-check trace **Phase E**.

### Step 4 — Cross-check against the trace

Open `ex-04-repl-trace.md`. Match line-for-line modulo banner.

### Optional — exercising dynamic merge

`dmerge/3` requires a stream containing `merge(Ws)` struct messages. To experiment:

```
dmerger([1, 2, 3], [a, b, c], Out).
```

Expected: `Out` produces the fair-merged stream of `[1,2,3]` + `[a,b,c]` (no merge() messages = same as simple merge under the constant + tuple guards). The dispatch logic only branches when a head element matches the `merge(_)` shape; with plain constants like `1` or `a`, dmerge falls through to clauses 4 / 6 (constant guard) and behaves like simple fair merge.

To trigger the dynamic-merge dispatch, you'd construct a stream like `[1, merge([x,y]), 2]` — when dmerge encounters `merge([x,y])`, it spawns a sub-dmerger to merge `[x,y]` into the residue. This is advanced and not part of the primary 4-goal session.

## What you've learned

By the end of this exercise you have seen:

1. **Simple fair merge with early-exits** — the §4.2.5 4-clause merger improves on Program 3.1 by avoiding element-by-element termination when one stream exhausts.
2. **Dynamic merge** — dmerge interprets struct-tagged messages in its input streams to spawn sub-mergers; the substrate for stream-multiplexing networks.
3. **Balanced merge tree** — O(log N) pairwise reduction of N streams. The classic divide-and-conquer pattern applied to streams.
4. **Type-guard dispatch** — dmerge uses `tuple/1` + `constant/1` + `~(=?=)` to dispatch on head-element type, demonstrating the §3.2 guard-species curriculum from chapter 3 in active use.
5. **Stream-merger composition** — simple merge is a building block; dmerge composes mergers dynamically; merge_tree composes them statically. Three perspectives on the same fair-merge primitive.

ex-05 (next exercise in the §4.2 group) introduces stream operators: `distribute/3` (broadcast), `distribute_indexed/3` (tagged routing), `observer/3` (non-consuming spy), and `adder/4` (ripple-carry on streams).
