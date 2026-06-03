# Exercise 1 — Program 3.1 + ch4 producer/consumer composed pipeline

Welcome to chapter 3, exercise 1. This exercise pairs the chapter's anchor program (Program 3.1: GLP Fair Stream Merger, book p 15) with a small chapter-4 exemplar (`producer/2` + `consumer/3`, book p 31) to give you a complete producer-merger-consumer pipeline that exercises SRSW reader/writer pairing across four roles in a single composed goal.

## Before you start

Read book §3.1 (Reader/Writer pairs, the SO Invariant, SRSW, GLP operational semantics — pp 15–17). You don't need to read §3.2 yet; ex-02 and ex-03 will introduce the §3.2 guard species. ex-01 uses only built-in guards (`>` from `producer/2`, `ground` from `consumer/3`) and the implicit head-pattern matching of `merge/3`'s three clauses.

If you are arriving at chapter 3 fresh, also read the parent-directory `ch03_tutorial.md` for the chapter overview, the cross-chapter import explanation, and the §3.2 guard curriculum that ex-02 + ex-03 build on top of this exercise.

## Building the REPL

This is a one-time step the first time you work through a chapter. If you've already built the REPL for ch01 / ch02, you can skip ahead. Otherwise:

```bash
DART="/c/Users/gavri/dart-sdk/bin/dart"   # or wherever your Dart 3.9.4+ lives
"$DART" compile exe glp_runtime/bin/glp_repl.dart -o glp_runtime/glp_repl.exe
```

The compiled binary is gitignored. Subsequent sessions reuse it as long as `glp_runtime/bin/glp_repl.dart` hasn't changed.

## The exercise

Load both `.glp` files in the same REPL session, then run the composed primary goal followed by three inspection goals. Cross-check your output against `ex-01-repl-trace.md` at each step.

### Step 1 — Open the REPL

Either run the compiled exe directly:

```bash
./glp_runtime/glp_repl.exe
```

Or use the kernel snapshot (which the test harness compiles as a side effect):

```bash
"$DART" run glp_runtime/.dart_tool/repl.dill
```

Either way you'll see the GLP REPL banner and a `GLP>` prompt.

### Step 2 — Load Program 3.1

At the `GLP>` prompt, enter:

```
olamni/tutorial/ch03/exercise-01/ch-03-ex-01-glp-fair-stream-merger.glp
```

You should see `✓ Loaded:` followed by the same path. The three `merge/3` clauses are now in the REPL's procedure table. Cross-check: the trace's **Phase A** shows this exact response.

### Step 3 — Load the producer/consumer pair

At the next `GLP>` prompt:

```
olamni/tutorial/ch03/exercise-01/ch-03-ex-01-producer-consumer.glp
```

Again `✓ Loaded:`. Now `producer/2`, `consumer/3`, AND `merge/3` are all defined in this session. Cross-check: the trace's **Phase B**.

### Step 4 — Run the composed primary demo goal

```
producer(A, 5), producer(B, 3), merge(A?, B?, M), consumer(M?, 0, Sum).
```

The expected response, byte-for-byte modulo banner / wallclock:

```
A = [5, 4, 3, 2, 1]
B = [3, 2, 1]
M = [5, 3, 4, 2, 3, 1, 2, 1]
Sum = 21
→ succeeds
```

What you're seeing: `A` and `B` are stream writers populated by the two producers; `A?` and `B?` are the matching readers passed to `merge/3` (one writer + one reader = SRSW); `M` is the merger's output writer; `M?` is the reader passed to the consumer; `Sum` is the consumer's output writer. The composed goal threads SRSW pairs through four procedures in one shot. Cross-check: **Phase C**.

The merged stream `M = [5, 3, 4, 2, 3, 1, 2, 1]` shows fair-merge alternation — Program 3.1's clause 1 swaps stream order on each recursive call, which produces the visible 5/3/4/2/3/1/2/1 interleaving. The consumer accumulates `5+3+4+2+3+1+2+1 = 21`.

### Step 5 — Run the three inspection goals

#### Inspection 1 — both producers empty

```
producer(A, 0), producer(B, 0), merge(A?, B?, M), consumer(M?, 0, Sum).
```

Expected: `A = []`, `B = []`, `M = []`, `Sum = 0`. Both producer base clauses fire immediately (`producer([], 0).`); merger clause 3 (the `merge([], [], [])` base) fires once; consumer base fires immediately. The minimal-pipeline trace. Cross-check: **Phase D**.

#### Inspection 2 — first producer empty, second populated

```
producer(A, 0), producer(B, 3), merge(A?, B?, M), consumer(M?, 0, Sum).
```

Expected: `A = []`, `B = [3, 2, 1]`, `M = [3, 2, 1]`, `Sum = 6`. Because `A` is empty from the start, `merge([], [3,2,1], M)` cannot match Program 3.1's clause 1 (head requires the first arg to be `[X|Xs]` cons) — clause 2 fires instead, forwarding B's elements. Eventually clause 3 terminates. `Sum = 6`. Cross-check: **Phase E**.

#### Inspection 3 — one element each

```
producer(A, 1), producer(B, 1), merge(A?, B?, M), consumer(M?, 0, Sum).
```

Expected: `A = [1]`, `B = [1]`, `M = [1, 1]`, `Sum = 2`. The smallest goal that exercises BOTH producer clauses (recursive once + base) AND both consumer clauses AND all three merge clauses. Cross-check: **Phase F**.

### Step 6 — Cross-check against the captured trace

Open `ex-01-repl-trace.md` in this same directory. Your REPL's output for each of the six phases should match the trace's code blocks line-for-line, modulo the REPL banner and the `Build:` / `Compiled:` wallclock lines at the top of the session. If you see a different `Sum` value or a `→ fails` / `→ suspended` where the trace shows `→ succeeds`, that's a bug worth investigating — either you typed the goal wrong, the runtime has changed, or the byte-exact transcription of one of the procedures has drifted.

## What you've learned

By the end of this exercise you have seen:

1. **SRSW reader/writer pairs in action across four roles**. Each variable in the composed goal — `A`, `B`, `M`, `Sum` — is paired exactly once: one writer, one reader. The composed goal threads those pairs through producer / merger / consumer procedures without any contraction (no variable read by two procedures or written by two).
2. **Built-in guards** as the foundation. `producer/2`'s recursive clause uses the `>` arithmetic guard ("count is positive"); `consumer/3`'s recursive clause uses the `ground` guard ("the head element is fully bound, safe to consume"). These are the simplest §3.2 species — guards baked into the language.
3. **Cross-chapter composition**. Program 3.1 from ch3 §3.1 + `producer/2` + `consumer/3` from ch4 §4.2 work together because they share the same SRSW discipline. The `:=` arithmetic operator inside `producer/2` and `consumer/3` is body-kernel territory that ch2 introduced; ch3 inherits its use byte-exact in this single cross-chapter import without expanding the chapter's own scope.
4. **Fair-merge alternation via argument-swap recursion**. Program 3.1's clause 1 recurses with `Ys?, Xs?` (swapped); clause 2 recurses with `Xs?, Ys?` (preserved). Together this rotates the "preferred" stream on each step — the fairness mechanism that distinguishes Program 3.1 from a simple-priority merge.

ex-02 (gated behind ex-01 approval) introduces §3.2 defined guards via `channel/1` + `process/2`. ex-03 introduces §3.2 guard negation via `lookup/3`'s `~(=?=)` form. Together the three exercises form the §3.2 guard curriculum: built-in (here) → defined (ex-02) → negation (ex-03).
