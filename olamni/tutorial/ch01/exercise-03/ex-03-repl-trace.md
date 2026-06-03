# Exercise 03 — captured REPL trace

This trace shows what you should see when you load `ch-01-ex-03-fair-stream-merger.glp` in the GLP REPL. The `.glp` is the same merge program as exercises 01 and 02, with variables renamed to **single-letter mathematical identifiers** (`A`, `As`, `B`, `Bs`, `Cs`). The bindings are byte-identical to ex-01's and ex-02's — three pieces of evidence that GLP semantics are insensitive to variable-name choice.

## Phase 1 — Load the file

```glp
GLP> olamni/tutorial/ch01/exercise-03/ch-01-ex-03-fair-stream-merger.glp
✓ Loaded: olamni/tutorial/ch01/exercise-03/ch-01-ex-03-fair-stream-merger.glp
```

## Phase 2 — Primary goal

```glp
GLP> merge([1,2,3],[a,b],Xs).
Xs = [1, a, 2, b, 3]
→ succeeds
```

> Same locked binding as ex-01 and ex-02. The internal program now uses `A`, `As`, `Cs`; you queried with `Xs`. The runtime maps query variables positionally into the program's variables and reports back using your query name.

## Phase 3 — Asymmetric

```glp
GLP> merge([1,2,3,4],[a],Xs).
Xs = [1, a, 2, 3, 4]
→ succeeds
```

## Phase 4 — Empty first stream

```glp
GLP> merge([],[a,b,c],Xs).
Xs = [a, b, c]
→ succeeds
```

## Phase 5 — Both empty

```glp
GLP> merge([],[],Xs).
Xs = []
→ succeeds
```

## Closing

```glp
GLP> :quit
Goodbye!
```

## What this trace demonstrates

Three exercises, three different variable-naming schemes, **identical bindings** for every goal. The merge program's behaviour is fully determined by its structure under SRSW, not by what you call the variables. With this exercise, the names-don't-matter lesson is no longer a claim — it's been demonstrated three times in a row.
