# Exercise 6 — Buffered Communication + Objects/Monitors

Welcome to chapter 4, exercise 6 — the last exercise in the §4.2 group. This exercise covers two §4.2 themes: buffered communication via the sliding-window buffer pattern (`bb` for indefinite streams + `bb_test` for the terminating variant), and the objects/monitors pattern where a process holds state in recursive parameters (`counter/1` + `accumulator/1`, with `accumulator/1` accessed by multiple clients via merge).

## Notes on book trace divergences

Two minor adjustments from book p 35's printed traces apply here:

1. **Counter goal trailing `[]`**: book p 35 shows `counter([add, add, add, read(X), clear, add, read(Y), []]).` with a literal `[]` at position 8. This trailing element causes the goal to fail because no `counter_loop` clause matches `[]` as an in-stream message — only the base clause `counter_loop([], _).` matches an empty input list, NOT a list whose element-at-index-7 is the empty list. The locked primary goal here omits the trailing `[]`; the same `X = 3, Y = 1` binding the book describes is produced.

2. **bb_test suspension**: book p 35's trace ends with `→ succeeds` but the same trace earlier shows `producer(13, X43?) -> suspended`. The actual GLP REPL output for `bb_test.` is `→ suspended` — the REPL's strict reduction semantics report any sub-process that hasn't fully reduced as a goal-level suspension. The book's "→ succeeds" annotation appears to refer to the consumer-side termination only; the producer's suspended state means the overall goal is suspended.

## Before you start

You should have completed ex-03 + ex-04 + ex-05 (the §4.2 group's earlier exercises). Read book §4.2's "Buffered Communication" subsection (book pp 34–35) and "Objects and Monitors" subsection (book pp 35–36). Re-read **Formal 4.3** (book pp 35–36) — the table listing which guards permit multiple reader occurrences. Counter's `read` clause uses `number(C?)` because `C?` appears twice in the body; per Formal 4.3, `number/1` is multi-reader-permissive (numbers cannot contain unbound variables, so replicating them is safe).

## What's in the file

`ch-04-ex-06-buffered-and-monitors.glp` — 22 clauses byte-exact from book pp 34–36:

- **§4.2.12 sliding-window buffer** (book p 34): `bb/0` + `consumer/1` + `producer/2` (3 clauses)
- **§4.2.13 terminating buffer test** (book pp 34–35): `bb_test/0` + `consumer/2` (2 clauses); `producer/2` is the same as bb's so doesn't redeclare (3 clauses total counting bb_test/0 + 2 consumer/2)
- **§4.2.14 counter monitor** (book p 35): `counter/1` + `counter_loop/2` (5 clauses; counter_loop's multi-clause dispatch on `clear` / `add` / `read(C?)` / `[]` messages)
- **§4.2.15 accumulator monitor** (book p 36): `accumulator/1` + `acc_loop/2` (4 clauses) + `test_acc/0` + `client1/1` + `client2/1` (3 clauses) — accumulator with multiple clients via merge
- **Simple merge/3** (book p 32) duplicated inline from ex-04 per FR-010 self-containment, because `test_acc` calls `merge/3` (4 clauses)

## The exercise

### Step 1 — Open the REPL

If your REPL session from earlier exercises is still open, you can `:quit` it and start fresh, OR keep it open if no procedure-redeclaration conflicts arise. Otherwise:

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

You'll see the GLP REPL banner and a `GLP>` prompt.

### Step 2 — Load the ex-06 file

At the `GLP>` prompt, enter:

```
olamni/tutorial/ch04/exercise-06/ch-04-ex-06-buffered-and-monitors.glp
```

You should see `✓ Loaded:` followed by the same path. All 22 clauses are now in the REPL's procedure table. Cross-check trace **Phase A**.

### Step 3 — Run the primary demo goal: counter monitor

```
counter([add, add, add, read(X), clear, add, read(Y)]).
```

Expected: `X = 3` and `Y = 1` and `→ succeeds`.

What happens internally: `counter/1`'s entry clause initialises `counter_loop` with count `0`. Then counter_loop's multi-clause dispatch processes the message stream:
- `add` → `C1 := C? + 1` increments to 1
- `add` → 2
- `add` → 3
- `read(X)` → number(C?)|... binds the message's `X` writer to the current count `3`
- `clear` → reset to 0
- `add` → 1
- `read(Y)` → binds `Y` writer to current count `1`
- `[]` → base clause matches, terminates

The `number(C?)` guard on the read clause permits `C?` to appear twice in the body (`number(C?)` itself + `counter_loop(In?, C?)`) per Formal 4.3. Cross-check trace **Phase B**.

### Step 4 — Run the three inspection goals

#### Inspection 1 — accumulator direct call

```
accumulator([add(5), add(10), read(S)]).
```

Expected: `S = 15` and `→ succeeds`.

Accumulator differs from counter in that its `add` messages carry numeric arguments (`add(N)`) rather than being a fixed-step increment. `acc_loop`'s `add(N)` clause uses `Sum1 := Sum? + N?` to update the running sum; `read(Sum?)` exposes the current sum to the client's writer. 5 + 10 = 15. Cross-check trace **Phase C**.

#### Inspection 2 — bb_test (suspends)

```
bb_test.
```

Expected: `→ suspended`.

The sliding-window buffer's `consumer/2` terminates after consuming 10 elements, but the producer keeps trying to write to its still-allocated future slots (no consumer demand). Per book p 35: "the producer suspends—no demand for element 13." The REPL's `→ suspended` matches this — the test goal cannot fully reduce because the producer is alive but has no continuation. This is a pedagogically intentional outcome demonstrating indefinite-stream semantics, not a bug. Cross-check trace **Phase D**.

#### Inspection 3 — counter on a smaller message list

```
counter([read(X), clear]).
```

Expected: `X = 0` and `→ succeeds`.

Fresh counter starts at count `0`. The first message `read(X)` immediately binds X to 0. The second message `clear` resets count to 0 (no observable effect since it was already 0). The empty residual list matches `counter_loop([], _).` base, terminating. Demonstrates that the read clause works on a fresh counter without prior `add` messages. Cross-check trace **Phase E**.

### Step 5 — Cross-check against the captured trace

Open `ex-06-repl-trace.md` in this same directory. Your REPL's output for each phase should match the trace's code blocks line-for-line, modulo the REPL banner and the `Build:` / `Compiled:` wallclock lines at the top of the session. If `S = 15` doesn't appear for the accumulator goal, or if the counter primary doesn't produce X=3 + Y=1, that's a halt-and-report situation — either you typed the goal wrong, the runtime has changed, or the byte-exact transcription has drifted.

### Optional — exercising test_acc

```
test_acc.
```

Expected: likely `→ suspended` (advanced concurrent-monitor demo).

`test_acc` spawns four concurrent processes via its body: `merge(Client1?, Client2?, In)`, `accumulator(In?)`, `client1(Client1)`, `client2(Client2)`. Each client closes its own stream after sending messages (via the `[... | []]` head pattern); merge combines them into a single stream `In`; accumulator processes the merged stream. The full reduction may suspend if the dataflow doesn't fully resolve all client read-bindings before some sub-process closes — a subtle ordering issue.

This is an optional advanced demo, NOT part of the locked 4-goal session. Try it; document whatever outcome the REPL produces in your own notes.

## What you've learned

By the end of this exercise (and the §4.2 group) you have seen:

1. **Buffered communication via sliding-window** — the consumer's pattern `[X1, X2, X3 | Xs?]` pre-allocates 2 slots ahead of the current head; the producer can race ahead by 2 elements. This decouples producer-consumer pace and is the foundational technique for backpressure-free streaming pipelines.
2. **Object/monitor pattern** — state held in recursive parameters of a tail-recursive process; messages dispatched via committed-choice on head patterns. `counter/1` + `counter_loop/2` is the canonical minimal example; `accumulator/1` + `acc_loop/2` is the same pattern with a parameterised numeric accumulator instead of a fixed-step counter.
3. **Multi-reader guards (Formal 4.3) in counter** — the `read(C?)` clause uses `number(C?) |` because `C?` appears twice in the body (once in the guard, once as the recursive call's pass-through accumulator). `number/1` is one of the multi-reader-permissive guards alongside `ground/1` + `constant/1` + `integer/1`. Type-test guards permit replicating readers because the values they admit (numbers, ground terms, constants) cannot contain unbound writers.
4. **Multi-client monitor via merge** — `test_acc` demonstrates the canonical multi-client pattern where individual client streams are merged into a single input stream that the monitor processes. This is how shared resources are accessed concurrently in GLP without locking — the merge serialises client requests into a deterministic stream, and the monitor processes them one at a time.

The §4.2 group is now complete (subject to project owner approval). Once approved, the §4.3 group opens. ex-07 covers recursive numerics (Peano + integer arithmetic + factorial variants + Fibonacci variants); ex-08 covers recursive list/tree (flatten + tree_sum + sort variants + non-ground distributor + tree substitution).
