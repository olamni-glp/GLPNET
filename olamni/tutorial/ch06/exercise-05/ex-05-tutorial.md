# Exercise 5 — §6.5 Buffered Communication

Welcome to chapter 6, exercise 5 — the final exercise of chapter 6.  This
is a re-presentation of ch04 §4.2.12 + §4.2.13 (sliding-window buffer +
terminating bb_test variant, book pp 34–35) under the §6.5 banner.

## §6.5's banner: "Buffered Communication"

The §6.5 heading describes a producer/consumer pair sharing a bounded
buffer.  The producer writes values into pre-allocated slots; the consumer
reads from those slots, sliding a window forward as it consumes.  When
the producer has filled its current slots, it allocates a fresh slot for
the consumer to fill via writer/reader pairing.  The bounded-buffer
discipline is achieved without explicit synchronisation primitives — the
SRSW-pair semantics IS the synchronisation.

## What's in this file

`ch-06-ex-05-buffered-communication.glp` contains:

- `NumStream ::= [] ; [Number | NumStream].` — the buffer alphabet (typed).
- `bb/0` + 1 clause — §4.2.12 infinite sliding-window top-level.
- `consumer/1` + 1 clause — §4.2.12 sliding-window consumer.
- `producer/2` + 1 clause — §4.2.12 + §4.2.13 shared producer.
- `bb_test/0` + 1 clause — §4.2.13 terminating top-level.
- `consumer/2` + 2 clauses — §4.2.13 terminating consumer.

Total: 6 clauses across 5 procedures.

## A significant amendment to ch04 §4.2.12 + §4.2.13: NO procedure declarations

The byte-exact ch04 §4.2.12 + §4.2.13 source uses a stream-mode pattern
that exchanges reader/writer roles between head and body for the same
variable `Xs`.  Specifically:

- `consumer/1`: head `[X1, X2, X3 | Xs?]` — Xs? reader at the tail; body
  `consumer([X2?, X3? | Xs])` — Xs writer at the tail.
- `producer/2`: head `[N? | Xs]` — Xs writer at the tail; body
  `producer(N1?, Xs?)` — Xs? reader (which becomes arg 2's value).

These mode patterns do NOT satisfy the type-checker's strict reader-at-↓
/ writer-at-↑ convention (where ↓ = `Type?` and ↑ = `Type`).  Attempting
typed declarations (concrete `NumStream?` or relaxed `_?`) results in
"Variable mode mismatch" errors.

The amendment: omit `procedure` declarations entirely for `bb`, `bb_test`,
`consumer/1`, `consumer/2`, `producer/2`.  This is the same strategy used
by `olamni/tutorial/ch04/exercise-06/ch-04-ex-06-buffered-and-monitors.glp`
(which has the SAME byte-exact clauses and loads cleanly because it has
no procedure declarations).

The `NumStream` type is declared at the top of the file as the §6.5 typed
alphabet, but is not bound to any procedure declaration in this file.
This is the smallest amendment that lets the byte-exact source load.

## The exercise

### Step 1 — Open the REPL

```bash
./glp_runtime/glp_repl.exe
```

### Step 2 — Load the ex-05 file

```
D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-05/ch-06-ex-05-buffered-communication.glp
```

Expected: `✓ Loaded: …`.  Cross-check: trace's **Phase A**.

### Step 3 — Primary demo: bb_test (10-element bounded buffer)

```
bb_test.
```

Expected: `→ suspended`.  Consumer's countdown reaches 0; producer
continues writing but eventually suspends because the consumer is no
longer extending the shared stream.  This IS the §4.2.13 trace from
book p 35.  Cross-check: **Phase B**.

### Step 4 — Inspection 1: consumer/2 base case

```
consumer(anything, 0).
```

Expected: `→ succeeds`.  Direct firing of the consumer/2 base clause.
Cross-check: **Phase C**.

### Step 5 — Inspection 2: consumer/2 recursive

```
consumer([1, 2, 3, 4, 5], 3).
```

Expected: `→ succeeds`.  Three recursive iterations + base case.
Cross-check: **Phase D**.

### Step 6 — Inspection 3: bb (infinite bounded-buffer)

```
bb.
```

Expected: `→ succeeds`.  Runs to the default reduction limit; consumer/1
+ producer/2 + bb/0 clauses all fire.  Cross-check: **Phase E**.

### Step 7 — Cross-check against the trace

Open `ex-05-repl-trace.md` and confirm.

## What you've learned

By the end of this exercise you have seen:

1. **A bounded-buffer producer/consumer pair** — the §4.2.12 + §4.2.13
   sliding-window buffer demonstrates SRSW-pair synchronisation:
   producer's writers and consumer's readers are matched 1-to-1, and
   the producer suspends when there's no consumer to read from.
2. **The byte-exact source mandate has limits — even bigger than ex-03's
   and ex-04's** — ex-05's byte-exact source does NOT satisfy strict
   typed mode-checking; the amendment is to omit `procedure`
   declarations entirely.  This is a real cost of the byte-exact mandate
   under tight typing: not every un-typed Program can be lifted into a
   typed presentation without clause modifications.
3. **Chapter 6 conclusion** — the §6.x banners (Difference Lists,
   Quicksort, Equators: Emergency Brake, Bidirectional Communication,
   Buffered Communication) collectively re-present chapters 1–5's most
   pedagogically interesting Programs under a "typed programming"
   framing.  Some adapt cleanly; some require amendments.  All five
   are loaded and exercised on this Windows host.

## What's next

ch06 is now complete.  Future chapters of the tutorial pick up at ch07
(Module System) — see `olamni/tutorial/ch07/ch07-sources.md`.

The two runtime fixes applied during ch06 (adding `is_list/1` + `tuple/1`
to the type-checker prelude + runner.dart guard dispatch) carry forward
to ch07+ and any future typed work.
