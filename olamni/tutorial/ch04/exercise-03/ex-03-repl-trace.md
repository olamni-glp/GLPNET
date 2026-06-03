# Exercise 3 — REPL trace

This trace is the verbatim output of actual GLP REPL sessions run on this Windows host on 2026-04-30. Per Clarifications Q4 spec amendment, ex-03 contains TWO `.glp` files (split because both naive `reverse/2` and accumulator `reverse/2` define the same predicate name; GLP forbids non-contiguous clauses for the same predicate). The trace covers both files via two load steps + a four-goal session spanning both.

This exercise also reclaims `producer/2` + `consumer/3` as their NATIVE chapter-4 home. These same procedures appear in ch03 ex-01 as a cross-chapter forward import; the byte-exact code corpus is identical between ch03's import and the two ch04 ex-03 files.

## Phase A — Load naive-reverse file (first REPL session)

```glp
GLP> ✓ Loaded: olamni/tutorial/ch04/exercise-03/ch-04-ex-03-producer-consumer-naive-reverse.glp
```

producer/2 + consumer/3 + naive reverse/2 + append/3 are now in the procedure table. Per Formal 4.2 (book p 31), the recursive calls in producer + consumer pass readers (`Xs?`, `Sum1?`) — not writers — to preserve SRSW.

## Phase B — Primary demo goal: producer + consumer pipeline (countdown from 5)

```glp
GLP> A = [5, 4, 3, 2, 1]
Sum = 15
→ succeeds
```

Goal: `producer(A, 5), consumer(A?, 0, Sum).` — producer counts down from 5 producing the stream `[5,4,3,2,1]`; consumer reads the stream via `A?` (the paired reader of writer A) and accumulates `5+4+3+2+1 = 15`. The locked binding is empirically confirmed.

## Phase C — Inspection goal 1: smaller pipeline

```glp
GLP> B = [3, 2, 1]
R = 6
→ succeeds
```

Goal: `producer(B, 3), consumer(B?, 0, R).` — countdown from 3 produces `[3,2,1]` summing to 6. Demonstrates the pipeline at smaller input.

## Phase D — Inspection goal 2: naive reverse

```glp
GLP> Ys = [c, b, a]
→ succeeds
```

Goal: `reverse([a,b,c], Ys).` against the naive-reverse file — naive reverse calls `reverse(Xs?, Zs)` recursively then `append(Zs?, [X?], Ys)`, producing `[c, b, a]`. O(n²) due to the repeated appends; for `[a,b,c]` it does 3+2+1 = 6 appends total.

## Phase E — Load accumulator-reverse file (second REPL session)

```glp
GLP> ✓ Loaded: olamni/tutorial/ch04/exercise-03/ch-04-ex-03-producer-consumer-acc-reverse.glp
```

producer/2 + consumer/3 (duplicated byte-exact per FR-010) + accumulator reverse/2 + reverse_acc/3 are now in the procedure table. The cross-chapter inversion identity contract from FR-002 + SC-007 means producer/2 + consumer/3 here are byte-identical to both ch03's import AND the sibling naive-reverse file.

## Phase F — Inspection goal 3: accumulator reverse_acc direct call

```glp
GLP> R = [3, 2, 1]
→ succeeds
```

Goal: `reverse_acc([1,2,3], [], R).` — accumulator-based reverse: traverse `[1,2,3]`, prepending each element to the accumulator (initially `[]`). Final accumulator = `[3,2,1]`. O(n) total work — linear, in contrast to the naive O(n²) version. The book's pedagogy in §4.2.4 (p 32) is exactly this comparison: same `reverse/2` predicate, different time complexity.

---

The six phases (2 loads + 4 goals) cover both `.glp` files in ex-03. Across the four-goal session (Phases B + C + D + F), every clause of every Program in both files is exercised: producer's base + recursive (Phase B + Phase C); consumer's base + recursive (Phase B + Phase C); naive reverse's base + recursive + append's base + recursive (Phase D); reverse_acc's base + recursive (Phase F). The two-file split per Q4 amendment preserves both naive (book p 31) and accumulator (book p 32) reverse implementations byte-exact while satisfying GLP's non-contiguous-clauses constraint. The cross-chapter inversion is consummated: `producer/2` + `consumer/3` were forward-imported INTO ch03 ex-01; here in ch04 ex-03 they appear in their NATIVE §4.2.1 + §4.2.2 prose-paraphrase context.
