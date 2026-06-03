# ch06 ex-05 — §6.5 Buffered Communication — REPL trace

This trace captures the verbatim REPL session for ex-05.  Five phases: A
loads the `.glp`; B is the canonical bounded-buffer demo (`bb_test`); C, D,
E run three inspection goals.

## Phase A — Build / load

```glp
GLP> D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-05/ch-06-ex-05-buffered-communication.glp
✓ Loaded: D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-05/ch-06-ex-05-buffered-communication.glp
```

## Phase B — Primary demo: bb_test (10-element bounded buffer)

`bb_test.` runs the §4.2.13 terminating buffer.  The consumer carries a
countdown of 10; after consuming 10 elements the consumer/2 base case
fires and the consumer terminates.  The producer continues writing values
1, 2, …, 12 but eventually suspends because the consumer is no longer
extending the shared stream.  The result is `→ suspended`: bb_test
itself has run to completion (consumer's countdown reached 0), but the
producer process is still waiting for a stream slot to fill.

```glp
GLP> bb_test.
→ suspended
```

This is the canonical bounded-buffer outcome documented in book p 35.

## Phase C — Inspection 1: consumer/2 base case

`consumer(anything, 0).` directly exercises the consumer/2 base clause —
the countdown is 0, so the base clause `consumer(_, 0).` matches and the
consumer terminates without consuming anything from the stream.

```glp
GLP> consumer(anything, 0).
→ succeeds
```

## Phase D — Inspection 2: consumer/2 recursive

`consumer([1, 2, 3, 4, 5], 3).` exercises the consumer/2 recursive clause.
The consumer takes a fully-bound list and a countdown of 3: each iteration
matches the 3-element-prefix pattern, decrements the countdown, recurses
on the tail.  After 3 iterations the countdown reaches 0 and the base
clause fires.

```glp
GLP> consumer([1, 2, 3, 4, 5], 3).
→ succeeds
```

## Phase E — Inspection 3: bb (infinite bounded-buffer)

`bb.` runs the §4.2.12 infinite-stream variant.  The consumer/1 has no
countdown so it consumes indefinitely; the producer also runs indefinitely.
At the default reduction limit the REPL terminates and reports `→
succeeds` (because no clause failed; the goals are still alive but the
limit was reached).

```glp
GLP> bb.
→ succeeds
```

This goal exercises bb/0's clause + consumer/1's clause + producer/2's
clause — three clauses not exercised by Phases B+C+D's bb_test variant.

## Coverage

The four goals collectively exercise all 6 clauses in this exercise:

| Clause | Phase that fires it |
|---|---|
| bb/0 | E |
| consumer/1 | E (via bb) |
| producer/2 | B (via bb_test), E (via bb) |
| bb_test/0 | B |
| consumer/2 recursive | B (via bb_test, until countdown reaches 0), D (direct) |
| consumer/2 base | B (when countdown reaches 0), C (direct) |

---

These bounded-buffer Programs are byte-exact from ch04 §4.2.12 + §4.2.13
(book pp 34–35), re-presented here under §6.5.  No procedure declarations
were added — the byte-exact source's stream-mode pattern (writers and
readers exchanged across head and body for the same variable Xs) does not
satisfy the implementation's strict reader-at-↓ / writer-at-↑ convention,
and adding `_?` typed declarations did not bypass the strict mode-check.
The `.glp` file omits all five procedure declarations (`bb`, `bb_test`,
`consumer/1`, `consumer/2`, `producer/2`) for this reason — the same
strategy `olamni/tutorial/ch04/exercise-06/ch-04-ex-06-buffered-and-
monitors.glp` uses.  The `NumStream` type is declared as the §6.5 typed
alphabet but is not bound to any procedure declaration in this file.
