# Exercise 4 — Counter Response-Slot

Welcome to chapter 5, exercise 4. ex-04 introduces *embedded modes* — the response-slot pattern from §5.5. The typed counter consumes a stream of `CounterMsg`s, one of which is `show(Number?)` — a constructor whose argument carries an embedded reader. When the counter consumes the message, Formal 5.3 (Mode Involution) flips that embedded reader into a writer position, and the counter binds it to its current state. The caller reads the bound state through its reader half.

A related un-typed counter appears in chapter 4 ex-06 (book §4.2.14). Different arity, different shape, different pedagogical focus — that one was about objects/monitors; this one is about embedded modes.

## Before you start

Read book §5.5 (Embedded Modes: Response Slots, p 50) and Formal 5.3 (Mode Involution, p 50). Mode involution is the rule **consume × consume = produce** — when a `?` annotation appears inside a structure that is itself at consume mode, the inner `?` flips to produce.

## What's in this file

`ch-05-ex-04-counter-response-slot.glp` contains the §5.5 byte-exact PDF code plus four coverage stubs:

- `CounterMsg ::= clear ; up ; down ; show(Number?).` — note the `?` inside `show`. That is the embedded reader.
- `CounterStream ::= [] ; [CounterMsg | CounterStream].` — typed list of messages.
- `procedure counter(CounterStream?, Number?).` — both args are consume mode.
- The byte-exact response-slot clause: `counter([show(State?)|S], State) :- number(State?) | counter(S?, State?).`
- Four coverage stubs for the other `CounterMsg` alternatives + the empty-stream case (book p 50 omits them; the type-checker requires exhaustive coverage).

## The exercise

### Step 1 — Open the REPL

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Load the ex-04 file

At the `GLP>` prompt:

```
olamni/tutorial/ch05/exercise-04/ch-05-ex-04-counter-response-slot.glp
```

Expected: `✓ Loaded: …`. Cross-check: trace's **Phase A**.

### Step 3 — Run the primary demo goal: the response slot

```
counter([show(R)],42).
```

Expected: `R = 42` and `→ succeeds`. The single-element stream `[show(R)]` matches the byte-exact response-slot clause with `R` unifying with the embedded `State?` slot and `42` with the head's `State` writer. The guard `number(42?)` succeeds; the recursion falls into the empty-stream stub and terminates. The response-slot wrote `42` into `R`. Cross-check: **Phase B**.

### Step 4 — Inspection 1 — empty stream

```
counter([],99).
```

Expected: `→ succeeds`. Exercises the empty-stream coverage stub `counter([], _).` directly. The state `99` is passed in but discarded (anonymous `_`); no binding is produced. Cross-check: **Phase C**.

### Step 5 — Inspection 2 — clear-then-show

```
counter([clear,show(R)],7).
```

Expected: `R = 7`. Exercises the `clear` stub first (forwards unchanged to the recursion), then the byte-exact `show` response-slot clause writes `7` into `R`. Demonstrates that the coverage stubs interact correctly with the response-slot. Cross-check: **Phase D**.

### Step 6 — Inspection 3 — two response slots in succession

```
counter([show(R1),show(R2)],3).
```

Expected: `R1 = 3` and `R2 = 3`. The response-slot clause fires twice in succession. Each `show(...)` reads the current state (which never changes — the show clause forwards `State?` unchanged into the recursion), so both `R1` and `R2` see `3`. Cross-check: **Phase E**.

### Step 7 — Cross-check against the trace

Open `ex-04-repl-trace.md` and confirm.

## What you've learned

By the end of this exercise you have seen:

1. **Embedded modes** — a `?` inside a structure (like `show(Number?)`) annotates the data shape, not the call site. When the structure is consumed, mode involution flips the embedded `?`.
2. **Mode involution (Formal 5.3)** — consume × consume = produce. The `Number?` inside `show(...)` of a consume-mode `CounterStream?` becomes a writer slot.
3. **The response-slot pattern** — the caller hands the counter an unbound variable inside `show(...)`; the counter binds it; the caller reads the result. This is one of GLP's core idioms for request/response over a stream.
4. **Exhaustiveness in the type-checker** — when a procedure is declared on a typed-union argument, every alternative needs a clause (or the type-checker rejects the file).

ex-05 (next exercise) is the chapter's flagship — typed quicksort, composing everything from §5.1–§5.5 into a complete typed program with three procedure declarations and six clauses.
