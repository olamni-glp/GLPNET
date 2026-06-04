# Exercise 02 — `append_and_sum/3` (introducing GLP arithmetic via `:=`)

**Source**: This exercise extends ex-01's GLP `append/3` (cross-chapter import from book pp 31–32) with a locally-defined `sum/2` and the top-level `append_and_sum/3`. The arithmetic operator `:=` comes from `programs/self.glp` (which dispatches to the runtime's `_add` body kernel). The pattern follows book p 31's producer-consumer call (`producer(H, 5), consumer(H?, 0, R).`) where the intermediate stream is local and only the final result is exposed.

**Files in this folder**:
- `ch-02-ex-02-append-and-sum.glp` — duplicated `append/3` (byte-exact from pp 31–32) plus `sum/2` plus `append_and_sum/3`.
- `ex-02-tutorial.md` — this file (step-by-step guide).
- `ex-02-repl-trace.md` — known-good capture of the REPL session.

## Before you start

You should have completed exercise-01 already; ex-01 demonstrated the LP→GLP transition at the SRSW analyser. This exercise builds on that foundation by introducing GLP **arithmetic** — the `:=` operator and the underlying math body kernel `_add`.

For background, optionally peek at book §4.2 ("Streams", pp 30–32) — the producer-consumer pattern there is the model `append_and_sum/3` follows. You don't need to read all of §4.2 yet; just the producer/consumer code blocks on p 31 give the gist. (Chapter 4's full treatment lands in chapter 4's own tutorial later.)

## The exercise

### Step 1 — Load the file

```
GLP> olamni/tutorial/ch02/exercise-02/ch-02-ex-02-append-and-sum.glp
```

You should see `✓ Loaded: …`. If you see `Error loading: …` instead, halt and check that you copied the file correctly — the head pattern of `sum/2`'s recursive clause must be `sum([X|Xs], Total?)` (writers in head). A common slip is writing `[X|Xs?]` (with `?`), which gives `Xs?` two reader occurrences and trips SRSW.

### Step 2 — The primary demo goal

```
GLP> append_and_sum([1,2,3], [4,5,6], Sum).
```

You should see `Sum = 21` followed by `→ succeeds`. That's 1+2+3+4+5+6. Look at the `.glp` and trace through what happens:

```glp
append_and_sum(A, B, Sum?) :-
    append(A?, B?, Zs),   % local Zs is a writer here
    sum(Zs?, Sum).        % local Zs? is a reader here
```

The local variable `Zs` has exactly one writer (in the `append` sub-call) and one reader (in the `sum` sub-call). That's the canonical SRSW producer-consumer pattern: `append/3` produces the stream into `Zs`; `sum/2` consumes it via `Zs?`. Neither the caller nor the rest of the body sees `Zs` — it's a private channel inside the clause body.

This binding (`Sum = 21`) is locked in the spec. If your REPL produces something different, file an issue rather than silently move on.

### Step 3 — Inspection goal 1: empty first list

```
GLP> append_and_sum([], [4,5,6], Sum).
```

You should get `Sum = 15`. `append/3`'s base clause fires immediately (forwarding `[4,5,6]` into `Zs`); `sum/2` then walks `[4,5,6]`, accumulating 4+5+6 = 15. Notice this exercises `append/3`'s base clause + both of `sum/2`'s clauses.

### Step 4 — Inspection goal 2: empty second list

```
GLP> append_and_sum([1,2,3], [], Sum).
```

You should get `Sum = 6` (1+2+3). `append/3`'s recursive clause walks `[1,2,3]`, with the base clause terminating into an empty `Ys`. `sum/2` walks `[1,2,3]`. Notice this exercises `append/3`'s recursive clause + base clause + `sum/2`'s recursive + base clauses.

### Step 5 — Inspection goal 3: both lists empty

```
GLP> append_and_sum([], [], Sum).
```

You should get `Sum = 0`. `append/3`'s base clause fires immediately with an empty `Ys`; `sum/2`'s base clause then sees an empty list and binds `Sum = 0`. Minimal case.

### Closing

```
:quit
```

## Cross-check against the captured trace

Compare your terminal output against `ex-02-repl-trace.md` line-for-line, modulo the build/timestamp banner. The bindings should match exactly.

## What you've learned

By running these four goals you've seen:

1. **GLP arithmetic happens via `:=`.** Inside `sum/2`'s recursive clause, `Total := Subtotal? + X?` reads the recursive subtotal and the head element, then writes the total. The `:=` operator is defined in `programs/self.glp` and dispatches to the runtime's `_add` body kernel — you don't call `_add` directly.
2. **The `:=` operator is the writer-side of arithmetic.** `Total := Subtotal? + X?` reads `Subtotal?` and `X?` and binds the writer `Total`. This mirrors the writer/reader idiom you saw with lists in ex-01: arithmetic results flow through paired writer/reader bonds just as list elements do.
3. **Producer-consumer is local.** The intermediate `Zs` in `append_and_sum/3`'s body is a local channel between `append` (producer) and `sum` (consumer). The caller never sees it. This is the canonical SRSW idiom from book p 31 — same pattern as `producer(H, 5), consumer(H?, 0, R).` in chapter 4.
4. **One writer, one reader is enough.** `append_and_sum/3` works without any guard relaxation (no `ground/1`, no `is_mutual_ref/1`, no MWM kernels). That's because the SRSW invariant is satisfied directly: each variable in each clause has exactly one writer and exactly one reader.

## Next: exercise-03

After ex-02 is approved, exercise-03 amplifies further by adding the system clock (`now/1`) and ground-term output (`'_output'/1`) on top of the arithmetic. The procedure `timed_append/3` will capture the time before and after `append/3`, compute the elapsed milliseconds via `:=` subtraction (reusing what you learned here), and emit the result via `'_output'`. That demonstrates that the same SRSW discipline that governs lists and numbers also governs side-effecting body kernels.
