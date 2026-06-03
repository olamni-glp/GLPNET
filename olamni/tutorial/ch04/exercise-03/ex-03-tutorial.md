# Exercise 3 — Producer + Consumer + List Reversal (with cross-chapter inversion)

Welcome to chapter 4, exercise 3. This is the §4.2 entry point — the chapter's centerpiece sub-section on streams. ex-03 introduces three Program families: producers + consumers (the canonical SRSW dataflow pair), and TWO list-reversal implementations (naive O(n²) and accumulator-based O(n)).

## Cross-chapter inversion

`producer/2` + `consumer/3` are byte-exact from book p 31, §4.2.1 + §4.2.2. **You have seen them before**: they appear in chapter 3's exercise-01 as a cross-chapter forward import, composed with Program 3.1's GLP Fair Stream Merger into a four-role producer-merger-consumer pipeline. Here in chapter 4, they appear in their NATIVE home — with the §4.2.1 + §4.2.2 prose-paraphrase context that ch03's cross-chapter-import header could only hint at.

The byte-exact code corpus is identical between ch03's import and ch04's native presentation. The difference is the surrounding `%%` paraphrase comments + header block. ch03's header cites the cross-chapter import provenance ("imported into ch03 to compose with Program 3.1..."); ch04 ex-03's header paraphrases the §4.2.1 + §4.2.2 native prose ("a producer that counts down from N", Formal 4.2 SRSW-in-continuation-calls, etc.).

This is the only such cross-chapter inversion in the entire tutorial set. ch04 has its own native content for everything else.

## Two-file structure (Clarifications Q4 amendment)

Per Clarifications Q4 spec amendment (recorded during /speckit-implement), this exercise contains TWO `.glp` files instead of one:

- `ch-04-ex-03-producer-consumer-naive-reverse.glp` — producer + consumer + naive reverse + append
- `ch-04-ex-03-producer-consumer-acc-reverse.glp` — producer + consumer (duplicated per FR-010) + accumulator reverse + reverse_acc

The split is forced by GLP's non-contiguous-clauses constraint: both book p 31's naive `reverse/2` and book p 32's accumulator `reverse/2` define the SAME predicate name. GLP requires all clauses for a predicate to be contiguous in a single source file. If both implementations are loaded together, only the first one (committed-choice) ever runs; the second becomes dead code. The two-file split preserves both implementations byte-exact + executable.

Pedagogically this matches the book: §4.2.3 presents naive reverse (with append) as a first-attempt O(n²); §4.2.4 presents accumulator reverse as the linear-time replacement. Reading naive THEN accumulator is the book's progression; loading them as separate files matches that progression.

## Before you start

You should have completed the §4.1 group (ex-01 + ex-02) — both must be approved before §4.2 unlocks. Read book §4.2.1 + §4.2.2 + §4.2.3 + §4.2.4 (book pp 30–32). Re-read **Formal 4.2** (SRSW in Continuation Calls, p 31) — the recursive calls in producer/consumer pass readers, not writers; this is the canonical SRSW pattern.

## What's in the two files

### File 1: naive-reverse

`ch-04-ex-03-producer-consumer-naive-reverse.glp` (~9 clauses):

- `producer/2` (2 clauses) — countdown from N
- `consumer/3` (2 clauses) — sums stream elements
- naive `reverse/2` (2 clauses) — recursively reverse + append
- `append/3` (2 clauses) — list concatenation

### File 2: accumulator-reverse

`ch-04-ex-03-producer-consumer-acc-reverse.glp` (~7 clauses):

- `producer/2` (2 clauses) — duplicated byte-exact per FR-010
- `consumer/3` (2 clauses) — duplicated byte-exact per FR-010
- accumulator `reverse/2` (1 clause) — entry point, calls reverse_acc
- `reverse_acc/3` (2 clauses) — does the actual work, accumulator-style

## The exercise

You'll do TWO REPL sessions, one per file.

### Session 1 — Naive reverse + producer-consumer

#### Step 1.1 — Open the REPL

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

#### Step 1.2 — Load the naive-reverse file

```
olamni/tutorial/ch04/exercise-03/ch-04-ex-03-producer-consumer-naive-reverse.glp
```

Expected: `✓ Loaded:`. Cross-check trace **Phase A**.

#### Step 1.3 — Run the primary goal: countdown from 5 + sum

```
producer(A, 5), consumer(A?, 0, Sum).
```

Expected: `A = [5, 4, 3, 2, 1]` and `Sum = 15`. The producer emits the countdown stream; the consumer reads via `A?` and accumulates. Cross-check trace **Phase B**.

#### Step 1.4 — Run inspection 1: smaller countdown

```
producer(B, 3), consumer(B?, 0, R).
```

Expected: `B = [3, 2, 1]` and `R = 6`. Cross-check trace **Phase C**.

#### Step 1.5 — Run inspection 2: naive reverse

```
reverse([a,b,c], Ys).
```

Expected: `Ys = [c, b, a]`. Naive reverse: O(n²). For 3 elements, the recursion does 3+2+1 = 6 append operations. Cross-check trace **Phase D**.

### Session 2 — Accumulator reverse

#### Step 2.1 — Open a fresh REPL

(Type `:quit` in Session 1 first, OR open a new terminal.)

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

#### Step 2.2 — Load the accumulator-reverse file

```
olamni/tutorial/ch04/exercise-03/ch-04-ex-03-producer-consumer-acc-reverse.glp
```

Expected: `✓ Loaded:`. Cross-check trace **Phase E**.

#### Step 2.3 — Run inspection 3: reverse_acc direct call

```
reverse_acc([1,2,3], [], R).
```

Expected: `R = [3, 2, 1]`. Accumulator-based reverse: O(n). Linear traversal, prepending each element to the accumulator. Cross-check trace **Phase F**.

You can also try `reverse([a,b,c], Ys).` in Session 2 — it produces the same output (`[c, b, a]`) as Session 1's naive reverse, but via the linear path. The semantic equivalence is the book's pedagogical point: same predicate name, same input-output relation, different time complexity.

### Step 3 — Cross-check against the trace

Open `ex-03-repl-trace.md`. Match each phase line-for-line modulo banner / wallclock. Two REPL sessions = two banner blocks; both are stripped during the byte-equality check.

## What you've learned

By the end of this exercise you have seen:

1. **Producers and consumers** — the canonical SRSW dataflow pair. The producer is a writer-side procedure that emits a stream; the consumer is a reader-side procedure that traverses the stream. They share a stream variable via a writer/reader pair (`A` writer / `A?` reader). The pipeline runs concurrently — the consumer can read elements as the producer emits them.
2. **Formal 4.2 in action** — continuation calls pass readers, not writers. Look at producer's recursive call: `producer(Xs, N1?)` — `Xs` is the writer (the same writer that became `Xs?` in the head pattern); `N1?` is the reader paired with the freshly-allocated writer `N1`. Continuation calls preserve SRSW by following this convention.
3. **Two list-reversal implementations** — naive (O(n²)) and accumulator (O(n)). Same predicate name, same input-output relation, different complexity. The book's progression from naive to accumulator is a classic functional-programming pattern.
4. **Cross-chapter inversion** — the producer/consumer pair you saw in ch03 as a cross-chapter import is here in its native home. Same byte-exact code; different surrounding pedagogical context.
5. **Two-file exercise dirs** — when two byte-exact Programs share a predicate name, splitting into two files preserves both. Same pattern as ch02 ex-01 (LP/GLP append) and ch03 ex-01 (Program 3.1 + producer-consumer).

ex-04 (next exercise in the §4.2 group) introduces merge variants — simple fair `merge/3`, dynamic `dmerge/3`, and static `merge_tree/2`. The producer-consumer pipeline you've just exercised becomes the substrate for routing streams through mergers.
