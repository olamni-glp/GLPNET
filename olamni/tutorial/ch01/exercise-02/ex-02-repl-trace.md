# Exercise 02 — captured REPL trace

This trace shows what you should see when you load `ch-01-ex-02-fair-stream-merger.glp` in the GLP REPL. The `.glp` is the same merge program as exercise-01, with variables renamed to **semantic identifiers** (`First`, `RestFirst`, `Second`, `RestSecond`, `Out`). The point: the bindings are byte-identical to exercise-01's bindings, demonstrating that GLP semantics depend on the SRSW reader/writer pairing, not on identifier text.

## Phase 1 — Load the file

```glp
GLP> olamni/tutorial/ch01/exercise-02/ch-01-ex-02-fair-stream-merger.glp
✓ Loaded: olamni/tutorial/ch01/exercise-02/ch-01-ex-02-fair-stream-merger.glp
```

> The file loads cleanly — same SRSW analysis, partial evaluation, type checking, and compilation pipeline as ex-01. The variable renaming is irrelevant to the language invariants.

## Phase 2 — Primary goal

```glp
GLP> merge([1,2,3],[a,b],Xs).
Xs = [1, a, 2, b, 3]
→ succeeds
```

> Same locked binding as ex-01: `Xs = [1, a, 2, b, 3]`. The query variable here is still `Xs` because that's what you typed in the REPL — but inside the program, the corresponding writer is named `Out`. Names you choose at the call site bind by position to the names the program uses internally.

## Phase 3 — Asymmetric (first stream longer)

```glp
GLP> merge([1,2,3,4],[a],Xs).
Xs = [1, a, 2, 3, 4]
→ succeeds
```

> Identical to ex-01 phase 3. Stream-1 surplus is forwarded linearly after `RestSecond` runs empty. The semantic name `RestSecond` makes the dataflow easier to read than `Ys` did, but the runtime behaviour is unchanged.

## Phase 4 — Empty first stream

```glp
GLP> merge([],[a,b,c],Xs).
Xs = [a, b, c]
→ succeeds
```

> Second clause's path: `RestFirst` is `[]`, the head's first arg matches via the empty list, the recursive call peels the second stream until both lists empty. Identical binding to ex-01.

## Phase 5 — Both empty (base case)

```glp
GLP> merge([],[],Xs).
Xs = []
→ succeeds
```

> Third clause `merge([],[],[])` matches; identical binding to ex-01.

## Closing

```glp
GLP> :quit
Goodbye!
```

## What this trace demonstrates

All four bindings are **byte-identical** to the ex-01 trace. Renaming `X, Xs, Y, Ys, Zs` to `First, RestFirst, Second, RestSecond, Out` changes the source-text of the program but not its semantics — because GLP, like all logic programming, is referentially transparent in its variable names. The reader/writer **pairing** under SRSW is what determines behaviour; the identifiers are immaterial.
