# Exercise 01 — captured REPL trace

This trace shows what you should see when you load `ch-01-ex-01-fair-stream-merger.glp` in the GLP REPL and run the four goals from the tutorial. The point of the trace is to give you a known-good reference: if your own REPL session produces the same bindings, you've reproduced the SRSW-fair-merge behaviour from the book — exactly.

## Phase 1 — Build the REPL and load the file

Building the REPL once (you only need to do this when you check out the repo or pull a Dart upgrade):

```glp
$ dart compile exe glp_runtime/bin/glp_repl.dart -o glp_runtime/glp_repl.exe
Generated: ...\glp_runtime\glp_repl.exe
```

Then run it and load the chapter-1 exercise file:

```glp
$ ./glp_runtime/glp_repl.exe
╔════════════════════════════════════════╗
║  GLP REPL - With Type Checking         ║
╚════════════════════════════════════════╝
Loaded root self.glp

GLP> olamni/tutorial/ch01/exercise-01/ch-01-ex-01-fair-stream-merger.glp
✓ Loaded: olamni/tutorial/ch01/exercise-01/ch-01-ex-01-fair-stream-merger.glp
```

> The `✓ Loaded` line is the key signal. The REPL ran SRSW analysis, partial evaluation, type checking, and compilation — all in one pipeline — without errors. If you see `Error loading: …` instead, your `.glp` doesn't satisfy the GLP language invariants and won't run; cross-check against `ch-01-ex-01-fair-stream-merger.glp` byte-for-byte.

## Phase 2 — Primary goal: a fair merge of two non-empty streams

This is the canonical demonstration: stream 1 has `[1, 2, 3]`, stream 2 has `[a, b]`, and we want `Xs` to be a fair interleaving.

```glp
GLP> merge([1,2,3],[a,b],Xs).
Xs = [1, a, 2, b, 3]
→ succeeds
```

> Notice the alternation: 1 from stream 1, then `a` from stream 2, then 2, then `b`, then 3. Stream 1's surplus element comes last after stream 2 runs out. This alternation is what the **argument swap in the recursive call** produces — the swap is the entire mechanism that makes the merge fair. This is the binding the book promises and the spec locks (`Xs = [1, a, 2, b, 3]`).

## Phase 3 — Inspection goal 1: asymmetric (first stream much longer)

What happens when one stream has surplus items after the other is exhausted?

```glp
GLP> merge([1,2,3,4],[a],Xs).
Xs = [1, a, 2, 3, 4]
→ succeeds
```

> The first two elements alternate (`1`, `a`), then stream 2 is empty so the second clause's base-case path forwards stream 1's tail (`[2, 3, 4]`) straight through. This shows that fairness applies *while both streams have elements*; once one is exhausted, the other is drained linearly.

## Phase 4 — Inspection goal 2: one stream empty from the start

What if you call `merge` with an empty first stream?

```glp
GLP> merge([],[a,b,c],Xs).
Xs = [a, b, c]
→ succeeds
```

> The third clause `merge([],[],[])` doesn't apply (only one list is empty). The first clause doesn't apply (its head requires `[X|Xs]`). The second clause matches: `merge(Xs, [Y|Ys], [Y?|Zs?])` peels the first element of stream 2, recurses with `Xs` (still empty) and `Ys?`, eventually terminating via the third clause. The merge is just stream 2 unchanged.

## Phase 5 — Inspection goal 3: both streams empty (the base case)

The simplest possible call — both inputs empty.

```glp
GLP> merge([],[],Xs).
Xs = []
→ succeeds
```

> Only the third clause `merge([],[],[])` applies. `Xs` is bound to the empty list. This is the merge protocol's termination condition — without it, recursion would never stop.

## Closing

```glp
GLP> :quit
Goodbye!
```

## What this trace demonstrates

The four goals together exercise all three clauses of Program 1.1: phase 2 exercises clause 1 (and, via the swap, clause 2 alternately); phase 3 exercises clause 1 then a clause-2-only suffix; phase 4 exercises clause 2 only; phase 5 exercises clause 3. They also illustrate two facets of fairness: it's a *while-both-have-elements* property (phase 2), and the surplus-stream is forwarded linearly afterwards (phase 3). The fact that every variable in Program 1.1 occurs **exactly once as a writer and once as a reader** is what makes all this work without distributed unification — that's the SRSW discipline §1.5 introduces, and merge is the canonical first example of it.
