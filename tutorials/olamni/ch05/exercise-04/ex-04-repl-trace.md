# Exercise 4 — REPL trace

This trace is the verbatim transcript of an actual GLP REPL session run on this Windows host on 2026-05-01. It demonstrates the §5.5 typed counter response-slot, exercised via four goals — a primary single-show plus three inspections covering the empty-stream stub, a clear-then-show sequence, and two consecutive show requests.

## Phase A — Load ex-04 file

```glp
GLP> olamni/tutorial/ch05/exercise-04/ch-05-ex-04-counter-response-slot.glp
✓ Loaded: olamni/tutorial/ch05/exercise-04/ch-05-ex-04-counter-response-slot.glp
```

The `CounterMsg` and `CounterStream` types, the `procedure counter(CounterStream?, Number?).` declaration, the byte-exact `show` response-slot clause, and the four coverage stubs are now in the REPL.

## Phase B — Primary demo goal: single response slot

```glp
GLP> counter([show(R)],42).
R = 42
→ succeeds
```

The response-slot clause `counter([show(State?)|S], State) :- number(State?) | counter(S?, State?).` matches: `R` unifies with the embedded `State?` reader, `42` with the head's `State` writer, the `number/1` guard succeeds, the recursive body falls into the empty-stream stub. The response slot is bound to `42`.

## Phase C — Inspection 1: empty-stream coverage stub

```glp
GLP> counter([],99).
→ succeeds
```

Matches `counter([], _).` directly — the state `99` is passed in but discarded.

## Phase D — Inspection 2: clear-then-show

```glp
GLP> counter([clear,show(R)],7).
R = 7
→ succeeds
```

The `clear` coverage stub fires first (forwards the recursion unchanged), then the byte-exact response-slot clause binds `R` to `7`.

## Phase E — Inspection 3: two response slots

```glp
GLP> counter([show(R1),show(R2)],3).
R1 = 3
R2 = 3
→ succeeds
```

The response-slot clause fires twice in succession; the state `3` flows through both `show` requests unchanged.

## Closing

```glp
GLP> :quit
Goodbye!
```

---

The four goals exercise the byte-exact PDF response-slot clause plus two of the four coverage stubs (`[]` and `clear`). Mode involution (Formal 5.3) is what makes the response-slot work: the embedded `Number?` inside `show(...)` of a consume-mode `CounterStream?` flips to produce, and the clause's writer `State` binds it.
