# Exercise 6 — REPL trace

This trace is the verbatim output of an actual GLP REPL session run on this Windows host on 2026-04-30. It demonstrates §4.2's buffered communication (sliding-window buffer) + objects/monitors (counter + accumulator). Note: the book's printed counter goal `counter([add, add, add, read(X), clear, add, read(Y), []])` includes a trailing `[]` element that fails (no clause matches `[]` as an in-stream value); the locked primary goal here uses the same message sequence WITHOUT the trailing `[]`, which lets counter_loop's empty-list base clause match cleanly.

## Phase A — Load ex-06 file

```glp
GLP> ✓ Loaded: olamni/tutorial/ch04/exercise-06/ch-04-ex-06-buffered-and-monitors.glp
```

22 clauses (3 bb + 3 bb_test + 5 counter + 7 accumulator/clients + 4 merge duplicated per FR-010) loaded.

## Phase B — Primary demo goal: counter monitor

```glp
GLP> X = 3
Y = 1
→ succeeds
```

Goal: `counter([add, add, add, read(X), clear, add, read(Y)]).` — sends 7 messages: 3 adds (count → 3), read(X) (X = 3), clear (count → 0), 1 add (count → 1), read(Y) (Y = 1). counter_loop's multi-clause dispatch handles each message type via committed choice. The `number(C?)` guard on the read clause permits multi-reader replication of C? per Formal 4.3.

## Phase C — Inspection goal 1: accumulator direct call

```glp
GLP> S = 15
→ succeeds
```

Goal: `accumulator([add(5), add(10), read(S)]).` — accumulator's add messages carry numeric arguments; read(S) exposes the running sum via writer/reader pair. 5+10 = 15.

## Phase D — Inspection goal 2: bb_test (suspends per book p 35)

```glp
GLP> → suspended
```

Goal: `bb_test.` — the sliding-window buffer terminates the consumer side after 10 elements, but the producer keeps trying to produce element 13 onto the buffer's still-allocated future slots (no consumer demand). Per book p 35 trace: "the producer suspends—no demand for element 13." The REPL's `→ suspended` outcome matches: the test goal cannot fully reduce because the producer is still alive but waiting.

## Phase E — Inspection goal 3: counter on a smaller message list

```glp
GLP> X = 0
→ succeeds
```

Goal: `counter([read(X), clear]).` — fresh counter (count = 0); read(X) → X = 0; clear (count → 0; trivial). counter_loop's read clause + clear clause + base clause all exercise.

---

The four goals exercise: counter_loop's add + read + clear + base clauses (Phase B); acc_loop's add + read + base clauses (Phase C); bb's consumer/1 + producer/2 + bb_test's consumer/2 + base clauses (Phase D — albeit suspending, all clauses fire during the partial computation); counter_loop's read + clear + base again (Phase E). The objects/monitors pedagogy (state-in-recursive-parameters) is now concrete; ex-07 (next, gated behind §4.2 group approval) introduces recursive numerics from §4.3.
