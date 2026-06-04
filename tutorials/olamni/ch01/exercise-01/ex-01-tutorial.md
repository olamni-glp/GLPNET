# Exercise 01 — Fair Stream Merger

**Source**: *The Art of Grassroots Logic Programming* (Shapiro, 2025), Chapter 1, §1.6, p 5 (Program 1.1).
**Files in this folder**:
- `ch-01-ex-01-fair-stream-merger.glp` — the runnable code (Program 1.1 verbatim from the book).
- `ex-01-tutorial.md` — this file (step-by-step guide).
- `ex-01-repl-trace.md` — known-good capture of the REPL session this tutorial walks through.

## Before you start

Read §1.4 (Concurrent Logic Programming), §1.5 (The Single-Reader/Single-Writer Insight), and §1.6 (A First GLP Program) in the book. The goal of this exercise is to make those three sections concrete by running the merge program and watching it interleave two streams.

## Building the REPL

You only need to do this once per checkout (or after a Dart SDK upgrade):

```bash
dart compile exe glp_runtime/bin/glp_repl.dart -o glp_runtime/glp_repl.exe
```

This produces a single `.exe` (Windows) or unsuffixed binary (Linux/macOS) at `glp_runtime/glp_repl.exe`. The Dart SDK requirement is `^3.9.4` — check with `dart --version` if anything fails.

## The exercise

Open the REPL:

```bash
./glp_runtime/glp_repl.exe
```

You should see a banner ending with `Loaded root self.glp`. Then enter the path of the exercise file:

```
GLP> olamni/tutorial/ch01/exercise-01/ch-01-ex-01-fair-stream-merger.glp
```

Look for `✓ Loaded: …` in the response. If you see that line, the REPL has run SRSW analysis, partial evaluation, type checking, and compilation in one pipeline, and your file passed all four. If you see `Error loading: …` instead, the file has a defect — re-check it byte-for-byte against the book and against the version committed in this repo.

### Step 1 — the canonical fair merge

Type the goal:

```
merge([1,2,3],[a,b],Xs).
```

You should see `Xs = [1, a, 2, b, 3]` followed by `→ succeeds`. Take a moment to look at that binding: notice how the elements alternate. That alternation is the whole point of "fair merge" — both input streams contribute equally as long as both have elements. It's produced by the **argument swap** in each recursive call (clause 1 calls `merge(Ys?, Xs?, Zs)` rather than `merge(Xs?, Ys?, Zs)`) — without the swap, one stream would be consumed entirely before the other was touched.

This binding (`[1, a, 2, b, 3]`) is locked in the spec. If your REPL produces something different, either your `.glp` is corrupted or the runtime is misbehaving — file an issue rather than silently move on.

### Step 2 — try an asymmetric pair

Now run the merge with one stream much longer than the other:

```
merge([1,2,3,4],[a],Xs).
```

You should get `Xs = [1, a, 2, 3, 4]`. The first two elements alternate (`1, a`), but once stream 2 is empty, the rest of stream 1 (`2, 3, 4`) is forwarded linearly. Fairness applies *while both streams have elements*; once one runs dry, the merge effectively becomes "drain the other".

### Step 3 — try an empty first stream

```
merge([],[a,b,c],Xs).
```

You should get `Xs = [a, b, c]`. Here the first clause never matches (it requires `[X|Xs]`), but the second clause does. The result is just stream 2 unchanged. Trace through the clauses by hand to convince yourself why that's the right behaviour.

### Step 4 — the empty/empty base case

```
merge([],[],Xs).
```

You should get `Xs = []`. Only the third clause `merge([],[],[])` matches both lists' shapes. This is the protocol's termination condition — without it, recursion would never bottom out.

### Closing

```
:quit
```

The REPL says `Goodbye!` and exits.

## Cross-check against the captured trace

After you've run all four steps, open `ex-01-repl-trace.md` and compare your terminal output line by line against the captured trace there. They should match modulo the build/timestamp banner. If they don't, write down what's different — divergence is interesting and likely points at either a build issue on your machine or a real change in the runtime since this trace was captured.

## What you've learned

By reading §1.4–§1.6 and running these four goals you've seen:

1. **GLP's REPL is one tool, not many.** Loading a `.glp` triggers SRSW + type-check + compile + run as a single pipeline.
2. **Fairness comes from the argument swap.** Without the swap, the first clause would consume stream 1 entirely before touching stream 2.
3. **SRSW is what makes this work.** Every variable in Program 1.1 occurs exactly once as a writer and once as a reader. That property is what eliminates distributed unification and lets the runtime treat each variable as a point-to-point channel.
4. **The base case matters.** Without `merge([],[],[])`, recursion never terminates. With it, every well-typed merge eventually completes.

The same Program 1.1 reappears in Chapter 3 (`Program 3.1`) with comments and again in Chapter 4 (§4.2 Streams) embedded in larger producer/consumer machinery. Recognising it across those contexts is the start of reading GLP fluently.

## Variants to look for in later exercises

After exercise-01 is approved, a renamed-variable variant lives in `exercise-02/` and another in `exercise-03/`. They run the same algorithm but rename `X, Xs, Y, Ys, Zs` to semantic names (`First, RestFirst, Second, RestSecond, Out`) and then to single-letter mathematical names (`A, As, B, Bs, Cs`). The point: the merge's **structure** (the SRSW reader/writer pairing) is what matters; the *names* are immaterial. Running them yourself and comparing the bindings is the cleanest way to internalise that distinction.
