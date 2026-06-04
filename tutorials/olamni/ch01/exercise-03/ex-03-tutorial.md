# Exercise 03 — Fair Stream Merger (mathematical variable names)

**Source**: same as exercises 01 and 02 — *The Art of Grassroots Logic Programming* (Shapiro, 2025), Chapter 1, §1.6, Program 1.1, p 5.
**Difference from ex-01 and ex-02**: variables renamed to **single-letter mathematical identifiers**:

| ex-01 (book) | ex-02 (semantic) | ex-03 (mathematical) |
|---|---|---|
| `X` | `First` | `A` |
| `Xs` | `RestFirst` | `As` |
| `Y` | `Second` | `B` |
| `Ys` | `RestSecond` | `Bs` |
| `Zs` | `Out` | `Cs` |

Same algorithm, same clauses, same semantics. Only the names differ.

## Why this exercise exists

Exercise 02 made the names-don't-matter lesson concrete by renaming to readable semantic names. Exercise 03 takes the opposite path: renaming to terse single-letter mathematical names, the kind you'd see in a textbook proof or a paper. If you accept that ex-02's bindings are identical to ex-01's, and ex-03's bindings are identical to both, then the lesson is undeniable: **GLP semantics depend on the SRSW reader/writer pairing, not on identifier text.**

## Run the exercise

Prerequisite: REPL already built (see chapter signpost). Open the REPL and load:

```
GLP> olamni/tutorial/ch01/exercise-03/ch-01-ex-03-fair-stream-merger.glp
```

Look for `✓ Loaded: …`.

### Step 1 — primary goal

```
merge([1,2,3],[a,b],Xs).
```

Expect `Xs = [1, a, 2, b, 3]`. Identical to ex-01 and ex-02.

### Step 2 — asymmetric

```
merge([1,2,3,4],[a],Xs).
```

Expect `Xs = [1, a, 2, 3, 4]`.

### Step 3 — empty first stream

```
merge([],[a,b,c],Xs).
```

Expect `Xs = [a, b, c]`.

### Step 4 — base case

```
merge([],[],Xs).
```

Expect `Xs = []`.

### Closing

```
:quit
```

## Cross-check

Open all three trace files side by side: `../exercise-01/ex-01-repl-trace.md`, `../exercise-02/ex-02-repl-trace.md`, `ex-03-repl-trace.md`. Every binding for every goal should match across all three. The only differences are: the file path in the `Loaded:` line, and the variable names inside the source file's `%%` comments and clauses.

## What you've learned (chapter 1 wrap-up)

Three exercises, three naming conventions, one algorithm:

1. **ex-01**: book-original names — what the book teaches you to read.
2. **ex-02**: semantic names — what you'd write for production code that humans maintain.
3. **ex-03**: single-letter mathematical names — what you'd read in a proof or formal specification.

Same SRSW analysis passes in all three. Same primary binding `[1, a, 2, b, 3]` returns from the REPL in all three. The runtime is name-blind; the SRSW invariant is what it actually checks. Internalising this distinction unlocks reading GLP code written by anyone, in any conventional naming style — including mixing styles within a single program.

The next chapters build on Program 1.1 in two ways: chapter 3 gives a more formal account of GLP's term matching and SRSW (with `Program 3.1` being the same merge with comments), and chapter 4 §4.2 embeds merge in larger producer/consumer pipelines. You'll recognise it everywhere.
