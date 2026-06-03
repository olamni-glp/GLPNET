# Exercise 3 — Mode-Checked Typed Merge

Welcome to chapter 5, exercise 3. ex-03 introduces the `procedure` keyword's *mode marks* (§5.3) and walks through the chapter's worked example of mode checking on a typed `merge/3` (§5.4). It is your first exercise where the type-checker does more than accept type definitions — it validates clause heads and bodies against the declared modes.

A related un-typed `merge/3` appears in chapter 4 ex-04 (book §4.2.5). Same procedure name, different signature: ch04's version has no procedure declaration and four clauses; this typed version has a procedure declaration and three clauses. Different presentations of stream merging, with chapter 5's centred on the mode-check itself.

## Before you start

Read book §5.3 (Moded Procedure Declarations, p 48) and §5.4 (Mode Checking, p 49). Skim Formal 5.2 (Mode Semantics, p 49) for the consume/produce data-flow table.

## What's in this file

`ch-05-ex-03-mode-checked-merge.glp` contains, byte-exact from book pp 48–49:

- `List ::= [] ; [Any | List].` — universal list, duplicated inline from ex-02.
- `procedure merge(List?, List?, List).` — moded declaration. Arg 1 is `List?` (consume), arg 2 is `List?` (consume), arg 3 is `List` (produce — no `?`).
- Three `merge/3` clauses: interleave-from-first-stream, interleave-from-second-stream, both-empty base case.

The `%%` annotations on each clause walk through the head-mode reasoning §5.4 prose describes.

## The exercise

### Step 1 — Open the REPL

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Load the ex-03 file

At the `GLP>` prompt:

```
olamni/tutorial/ch05/exercise-03/ch-05-ex-03-mode-checked-merge.glp
```

Expected: `✓ Loaded: …`. The mode-check passed silently for all three clauses. Cross-check: trace's **Phase A**.

### Step 3 — Run the primary demo goal: a typed fair merge

```
merge([1,3],[2,4],M).
```

Expected: `M = [1, 2, 3, 4]` and `→ succeeds`. Clause 1 selects `[1|...]` from arg 1, alternating with clause 2 selecting `[2|...]` from arg 2. Clause 3 closes the output with `[]`. Same fair-merge mechanism as ch04's untyped version — the difference is the mode-check at load time. Cross-check: **Phase B**.

### Step 4 — Inspection 1 — the both-empty base case

```
merge([],[],M).
```

Expected: `M = []`. Matches clause 3 directly. Cross-check: **Phase C**.

### Step 5 — Inspection 2 — atoms in arg 1, empty arg 2

```
merge([a,b],[],M).
```

Expected: `M = [a, b]`. Exercises clause 1 twice (recursing through `[a|…]` then `[b|…]`) before clause 3 terminates. Note: `a` and `b` are atoms — the universal `List` type accepts them because its element type is `Any` (compare with the typed `NumList` from ex-01, which would have rejected them). Cross-check: **Phase D**.

### Step 6 — Inspection 3 — empty arg 1, atoms in arg 2

```
merge([],[c,d],M).
```

Expected: `M = [c, d]`. Symmetric to Step 5 — exercises clause 2 (instead of clause 1), then clause 3. Cross-check: **Phase E**.

### Step 7 — Cross-check against the trace

Open `ex-03-repl-trace.md` and confirm.

## What you've learned

By the end of this exercise you have seen:

1. **Procedure declarations with mode marks** — `procedure merge(List?, List?, List).` says arg 1 + arg 2 are inputs (consume) and arg 3 is an output (produce). The `?` distinguishes them.
2. **Clause body `?` annotations** — every variable that reads in the body is marked `?`; every variable that writes is unmarked. Pair the head writers with body readers (and vice versa) to satisfy SRSW.
3. **Mode checking at load time** — the type-checker walks each clause and verifies the writer/reader pair pattern against the declaration. The chapter's first demonstration of mode-check actually doing work on a clause body.
4. **Cross-chapter relationship** — same `merge/3` name as ch04 ex-04; different signature (typed) and different clause set (three vs four). Read the two side-by-side to see the typing add value.

ex-04 (next exercise) introduces *embedded modes* — types whose alternatives carry `?` annotations inside structures, used for response-slot patterns. Formal 5.3 (Mode Involution) does real work there.
