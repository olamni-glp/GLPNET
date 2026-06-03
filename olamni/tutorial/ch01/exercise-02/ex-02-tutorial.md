# Exercise 02 — Fair Stream Merger (semantic variable names)

**Source**: same as exercise-01 — *The Art of Grassroots Logic Programming* (Shapiro, 2025), Chapter 1, §1.6, Program 1.1, p 5.
**Difference from exercise-01**: variables renamed to **semantic identifiers**:

| ex-01 (book original) | ex-02 (semantic) |
|---|---|
| `X` | `First` |
| `Xs` | `RestFirst` |
| `Y` | `Second` |
| `Ys` | `RestSecond` |
| `Zs` | `Out` |

The structural shape of every clause is identical. Only the names differ.

## Why this exercise exists

GLP, like all logic programming, is referentially transparent in its variable names. What governs runtime behaviour is the **SRSW reader/writer pairing** — every variable occurs exactly once as a writer and exactly once as a reader. The names are just labels for the programmer.

Running this exercise after exercise-01 makes that abstract claim concrete: the bindings produced by the four goals are byte-identical to ex-01's. If you weren't told the variables were renamed, you couldn't tell from the REPL output.

## Run the exercise

Prerequisite: REPL already built per the chapter signpost (`ch01_tutorial.md`). Open the REPL and load the file:

```
GLP> olamni/tutorial/ch01/exercise-02/ch-01-ex-02-fair-stream-merger.glp
```

Look for `✓ Loaded: …`. The file passes the same SRSW analysis, type checking, and compilation pipeline as ex-01.

### Step 1 — primary goal

```
merge([1,2,3],[a,b],Xs).
```

Expect `Xs = [1, a, 2, b, 3]` and `→ succeeds`. **Identical to ex-01.**

### Step 2 — asymmetric

```
merge([1,2,3,4],[a],Xs).
```

Expect `Xs = [1, a, 2, 3, 4]`. Identical to ex-01.

### Step 3 — empty first stream

```
merge([],[a,b,c],Xs).
```

Expect `Xs = [a, b, c]`. Identical to ex-01.

### Step 4 — base case

```
merge([],[],Xs).
```

Expect `Xs = []`. Identical to ex-01.

### Closing

```
:quit
```

## Cross-check

Compare your REPL output line-by-line against `ex-02-repl-trace.md`. They should match modulo the build banner. **Then also compare against `../exercise-01/ex-01-repl-trace.md`** — the four bindings should be identical there too.

## What you've learned

Reading the same `merge/3` algorithm with semantic variable names is markedly easier than reading it with the book's terse `X, Xs, Y, Ys, Zs`. Yet the runtime sees no difference. This is the core lesson:

- **Names are for humans.** Use the most readable names for your audience.
- **Pairing is for the runtime.** SRSW counts each variable's occurrences, not its name.
- **Goal-side query variable** (`Xs` here) is bound positionally to whatever name the program uses internally (`Out` here). You can use any name in your goal; the program doesn't see it.

After approving this exercise, [`exercise-03/`](../exercise-03/) renames the same merge once more — to single-letter mathematical names (`A, As, B, Bs, Cs`). Running all three side-by-side and confirming identical bindings is the cleanest way to internalise the names-don't-matter lesson.
