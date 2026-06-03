# Exercise 4 — §6.4 Bidirectional Communication

Welcome to chapter 6, exercise 4.  This is a re-presentation of ch03 §3.2
(channel operations, book p 23) under the §6.4 banner.

## §6.4's banner: "Bidirectional Communication"

ch03 §3.2 introduces the channel-as-cross-linked-pair pattern: `new_channel`
allocates a pair of channels where what is written to one is read from the
other.  This is bidirectional communication at its most primitive — a
shared variable accessed from two opposite views.

The five unit clauses of ch03 §3.2 (`send/3`, `receive/3`, `new_channel/2`,
`make_pair/2` + the three-clause `relay/3`) are the primitives that
higher-level GLP communication patterns are built from.  Most are
single-unit-clause defined guards: the partial evaluator unfolds them at
compile time so they have zero runtime overhead.

## What's in this file

`ch-06-ex-04-bidirectional-communication.glp` contains:

- `Stream ::= [] ; [_ | Stream].` — local non-parameterised stream type
  (`_` element per typed-glp-manual.md §18.3).
- `Channel ::= ch(Stream, Stream?).` — ch05 §5.5 channel form.
- `procedure send(_?, Channel?, Channel).` + 1 unit clause.
- `procedure receive(_, Channel?, Channel).` + 1 unit clause.
- `procedure new_channel(Channel, Channel).` + 1 unit clause.
- `procedure relay(Stream?, Stream, Channel?).` + 3 clauses.
- `procedure make_pair(Channel, Channel).` + 1 clause.

## One amendment to ch03 §3.2

`relay/3`'s clause 2 has a mode mix-up under tight typing: the byte-exact
source uses `In?` (reader) at head arg 1 + `In` (writer) at body arg 1.
This is a head-reader / body-writer SRSW pair pattern that means "this
relay clause produces the input stream from its recursive call" — which
contradicts the procedure declaration's arg 1 consume mode.

Amendment: swap to `In` (writer in head) + `In?` (reader in body) — the
same mode profile as clause 1.  Algorithmic effect: In is a pass-through
writer threaded to the recursive call; clause 2 still uses `receive/3` to
extract from the channel into the output stream `[X?|Out?]`.

## Three runtime fixes inherited from earlier exercises

ex-04 builds on the three earlier exercises' runtime fixes:

1. `is_list/1` recognised by the type-checker prelude + runner.dart guard
   dispatch (added during ex-01).
2. `tuple/1` recognised the same way (added during ex-03).
3. `procedure is_list(_?).` and `procedure tuple(_?).` added to
   `programs/self.glp`.

ex-04 itself does NOT require a runtime fix.

## The exercise

### Step 1 — Open the REPL

```bash
./glp_runtime/glp_repl.exe
```

### Step 2 — Load the ex-04 file

```
D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-04/ch-06-ex-04-bidirectional-communication.glp
```

Expected: `✓ Loaded: …`.  Cross-check: trace's **Phase A**.

### Step 3 — Primary demo: new_channel cross-linking

```
new_channel(C1, C2).
```

Expected: `C1 = ch(Xs?, Ys), C2 = ch(Ys?, Xs)` shape — the actual variable
numbers are runtime-allocated and vary per session, but the shape (cross-
linked slot positions) is the invariant.  Cross-check: **Phase B**.

### Step 4 — Inspection 1: make_pair returns inverted-reader pair

```
make_pair(P1, P2).
```

Expected: same cross-linked shape as Phase B.  Cross-check: **Phase C**.

### Step 5 — Inspection 2: send appends to output stream

```
send(hello, ch([], Out), Result).
```

Expected: `Out = [hello | <fresh tail>]` and `Result = ch([], <fresh tail>)`.
Cross-check: **Phase D**.

### Step 6 — Inspection 3: receive extracts from input stream

```
receive(X, ch([world], R), Result).
```

Expected: `X = world` (head of input stream) and `R = <unbound>`.
Cross-check: **Phase E**.

### Step 7 — Cross-check against the trace

Open `ex-04-repl-trace.md` and confirm each goal's shape (variable numbers
will differ but the cross-linking + value extraction shapes match).

## Coverage

The four goals collectively exercise: `new_channel/2` (Phases B + C), 
`make_pair/2` (Phase C — also indirectly exercises `new_channel/2` via
its guard), `send/3` (Phase D), and `receive/3` (Phase E).  The three
`relay/3` clauses are NOT exercised by the four goals — relay requires a
running concurrent process to feed it streams, which is out of scope for
a single-shot REPL goal.  Per the same FR-006 relaxation applied to ex-03,
ex-04 covers 4 of 7 clauses (5 procedures, 7 clauses).  The trace's
"Coverage notes" section documents this.

## What you've learned

By the end of this exercise you have seen:

1. **Channels as cross-linked pairs** — `new_channel` allocates two
   channel-views that share the same underlying stream variables but
   in opposite slot positions.  This is bidirectional communication at
   its most primitive.
2. **send/receive as unit-clause defined guards** — both are single
   unit clauses that the partial evaluator unfolds at compile time.
   At runtime they are just structural unifications; no extra
   procedure-call overhead.
3. **The relay/3 mode amendment** — the §6.4 typing required swapping
   one clause's mode profile to match the procedure declaration.  This
   is the third ch06 exercise to require an amendment to byte-exact
   earlier-chapter source (after ex-01's `_`-element NestedList and
   ex-03's three SRSW-related clause amendments).

## What ex-05 brings next

Exercise 5 is §6.5 Buffered Communication — synthesised from ch04 §4.2.12
+ §4.2.13 (sliding-window buffer + terminating bb_test variant).
