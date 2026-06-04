# Exercise 1 — REPL trace

This trace is the verbatim output of an actual GLP REPL session run on this Windows host on 2026-04-30. It demonstrates how Program 3.1 (the chapter-3 anchor) composes with the chapter-4 `producer/2` + `consumer/3` pair into a producer-merger-consumer pipeline that exercises SRSW reader/writer pairing across four roles in a single goal. Use it to cross-check your own session: the four goals' bindings should match line-for-line modulo the REPL banner and build-wallclock lines at the top.

## Phase A — Load Program 3.1 (the GLP fair stream merger)

```glp
GLP> ✓ Loaded: olamni/tutorial/ch03/exercise-01/ch-03-ex-01-glp-fair-stream-merger.glp
```

The merger's three clauses (`merge/3`) are now in the REPL's procedure table. No SRSW errors, no type errors — Program 3.1 is well-formed by construction.

## Phase B — Load the cross-chapter producer/consumer pair

```glp
GLP> ✓ Loaded: olamni/tutorial/ch03/exercise-01/ch-03-ex-01-producer-consumer.glp
```

`producer/2` (2 clauses) and `consumer/3` (2 clauses) coexist with `merge/3` from Phase A — no procedure-redeclaration conflict because the imported procedures don't redefine `merge/3`. The composed primary goal in Phase C will reference procedures from BOTH `.glp` files; SRSW reader/writer pairing connects four roles (producer A, producer B, merger, consumer).

## Phase C — Composed primary demo goal

```glp
GLP> A = [5, 4, 3, 2, 1]
B = [3, 2, 1]
M = [5, 3, 4, 2, 3, 1, 2, 1]
Sum = 21
→ succeeds
```

`producer(A, 5)` produces the countdown stream `[5,4,3,2,1]` summing to 15; `producer(B, 3)` produces `[3,2,1]` summing to 6; `merge(A?, B?, M)` interleaves the two streams fairly via Program 3.1's argument-swap recursion; `consumer(M?, 0, Sum)` accumulates the merged stream's elements, producing `Sum = 15 + 6 = 21`. The locked binding from Clarifications Q1 is empirically confirmed.

## Phase D — Inspection goal 1 (both producers empty from start)

```glp
GLP> A = []
B = []
M = []
Sum = 0
→ succeeds
```

`producer(A, 0)` immediately hits its base clause (`producer([], 0).`) producing `A = []`; same for `B`. `merge([], [], [])` matches Program 3.1's clause 3 (the empty-streams base case) and binds `M = []`. `consumer([], 0, Sum)` matches `consumer/3`'s base clause (`consumer([], Sum, Sum?).`) and forwards the accumulator `0` to `Sum`. The minimal-pipeline trace.

## Phase E — Inspection goal 2 (first producer empty, second non-empty)

```glp
GLP> A = []
B = [3, 2, 1]
M = [3, 2, 1]
Sum = 6
→ succeeds
```

`producer(A, 0)` produces empty; `producer(B, 3)` produces `[3,2,1]`. `merge([], [3,2,1], M)` cannot match Program 3.1's clause 1 (head requires `[X|Xs]` for the first arg) so clause 2 fires, forwarding `B`'s elements one at a time; eventually clause 3 terminates. `Sum = 3+2+1 = 6`. The fair-merger gracefully handles a stream that is empty from the start by funneling everything through clause 2.

## Phase F — Inspection goal 3 (single element from each producer)

```glp
GLP> A = [1]
B = [1]
M = [1, 1]
Sum = 2
→ succeeds
```

The smallest non-trivial composed goal. `producer(A, 1)` produces `[1]`; `producer(B, 1)` produces `[1]`. `merge([1], [1], M)` fires clause 1 (output 1 from first stream, swap args), then clause 2 on the residue (output 1 from second stream, after swap), then clause 3. `consumer/3` accumulates `1 + 1 = 2`. The smallest goal that exercises ALL THREE merge clauses + both producer clauses + both consumer clauses.

---

Together the four goals exercise: all three `merge/3` clauses (clause 1 produces from first stream, clause 2 produces from second stream, clause 3 terminates on empty pair), both `producer/2` clauses (recursive while N>0; base when N=0), both `consumer/3` clauses (recursive while stream non-empty; base when stream empty). The SRSW reader/writer discipline connects the four procedures into a single composed goal — this is the chapter-3 lesson made concrete: §3.1's formal semantics in action across multiple producer/consumer roles using only built-in guards (`>` from `producer/2`, `ground` from `consumer/3`).
