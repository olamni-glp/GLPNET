# Exercise 7 — Recursive Numerics

Welcome to chapter 4, exercise 7 — the §4.3 group entry point. This exercise covers book §4.3's first half: Peano arithmetic (the inductive definition of natural numbers via `0` + successor `s/1`), integer arithmetic via the `:=` body kernel, and recursive numeric functions — naive factorial + naive Fibonacci + linear Fibonacci with accumulator. The §4.3 group establishes recursive programming on numeric data; ex-08 (next) extends to recursive programming on list/tree data structures.

## Q6 + Q7 amendment notices

Two Clarifications-level amendments apply to this exercise (recorded in `specs/005-tutorial-ch04/spec.md`):

- **Q6** (deferred §4.3.4 tail-recursive factorial): book p 38 presents both a naive `factorial/2` (book p 37–38) and a tail-recursive `factorial/2` + `fact_acc/3` (book p 38). Both define the same predicate name `factorial/2`. GLP requires all clauses for a predicate to be contiguous in a single source file; the two implementations cannot coexist in the same file (loading both makes the second one's entry clause dead code under committed-choice). Per Q6, ex-07's `.glp` includes the naive version (3 clauses); the tail-recursive version is documented here for learners who want to exercise it (comment out the naive clauses in the `.glp` file and add `factorial(N, F?) :- fact_acc(N?, 1, F).` plus the two `fact_acc/3` clauses from book p 38).

- **Q7** (added `number(B?)` guard to fib_acc): book p 38's `fib_acc/4` body has `B?` appearing twice — once in `AB := A? + B?`, again in the recursive call `fib_acc(N1?, B?, AB?, F)`. The book's clause guard is only `N? > 0` — no multi-reader-permissive guard for B. Strict SRSW analyser flags this. Per Formal 4.3 (book p 36), `number/1` is multi-reader-permissive (numbers cannot contain unbound writers); the guard is extended from `N? > 0` to `N? > 0, number(B?)`. Pedagogical content (linear Fibonacci with two-accumulator threading) preserved unchanged. Same precedent as ch04 Q5 (distribute_indexed Out2/Out1 fix).

This is the fourth book-internal SRSW inconsistency surfaced during ch01–ch04 implementation (after ch02 Q3a `append_and_sum/4`→`/3`, ch03 Q4 `lookup/3` Key→Key?, ch04 Q5 `distribute_indexed` Out1/Out2). A separate book-wide SRSW audit branch is recommended.

## Before you start

The §4.2 group (ex-03 through ex-06) must be approved before ex-07 unlocks. Read book §4.3 (book pp 37–38), specifically the "Peano Arithmetic" + "Integer Arithmetic" + "Recursive Numeric Functions" subsections. Re-read Formal 4.3 (book p 36) for the multi-reader-permissive guard list (relevant for Q7).

## What's in the file

`ch-04-ex-07-recursive-numerics.glp` — 23 clauses byte-exact from book pp 37–38 (with Q7 amendment to fib_acc):

- **§4.3.1 Peano arithmetic** (book p 37): `plus/3` + `times/3` + `lesseq/2` + `natural_number/1` (8 clauses)
- **§4.3.2 Integer arithmetic** (book p 37): `double/2` + `average/3` + `abs/2` + `max/3` (6 clauses)
- **§4.3.3 Naive factorial** (book pp 37–38): `factorial/2` (3 clauses)
- **§4.3.5 Naive Fibonacci** (book p 38): `fib/2` (3 clauses; spawns O(2^N) processes)
- **§4.3.6 Linear Fibonacci with accumulator** (book p 38): `fib_linear/2` + `fib_acc/4` (3 clauses, Q7-amended)

Out of scope for this file (per Q6): §4.3.4 tail-recursive factorial — would conflict with §4.3.3's `factorial/2` predicate name. Documented in the Q6 notice above for learners who want to swap implementations.

## The exercise

### Step 1 — Open the REPL

If your REPL session from earlier exercises is still open, you can `:quit` it and start fresh. Otherwise:

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

You'll see the GLP REPL banner and a `GLP>` prompt.

### Step 2 — Load the ex-07 file

```
olamni/tutorial/ch04/exercise-07/ch-04-ex-07-recursive-numerics.glp
```

You should see `✓ Loaded:`. All 23 clauses are now in the REPL's procedure table. Cross-check trace **Phase A**.

### Step 3 — Run the primary demo goal: naive factorial

```
factorial(7, F).
```

Expected: `F = 5040` and `→ succeeds`.

What happens internally: `factorial/2`'s third clause (the recursive case) matches `factorial(7, F)` with `N? > 1` guard succeeding. The body computes `N1 := N? - 1` (binds N1 = 6), spawns `factorial(N1?, F1)` (a new process for factorial(6)), then computes `F := N? * F1?` once F1 is bound. The recursion descends 6 → 5 → 4 → 3 → 2 levels; at level 1 the second clause `factorial(1, 1).` fires. Each recursion level spawns a new process, so 7! computes via 6 nested factorial calls + the base. Cross-check trace **Phase B**.

### Step 4 — Run the three inspection goals

#### Inspection 1 — Linear Fibonacci

```
fib_linear(20, G).
```

Expected: `G = 6765` and `→ succeeds`.

Linear Fibonacci uses a 2-accumulator pattern (A, B) representing the last two Fib numbers. `fib_linear/2`'s entry clause `fib_linear(N, F?) :- fib_acc(N?, 0, 1, F)` initialises the accumulators to Fib(0)=0 and Fib(1)=1. Each `fib_acc/4` recursive step computes `AB := A? + B?` (next Fib), then recurses with `(B, AB)` as the new accumulators. After N steps, the accumulator A holds Fib(N). Total work: O(N) — vs. naive `fib/2`'s O(2^N) which would take prohibitively long for N=20. The Q7 amendment's `number(B?)` guard permits B's two reader occurrences in the body. Cross-check trace **Phase C**.

#### Inspection 2 — Peano addition

```
plus(s(s(0)), s(s(s(0))), R).
```

Expected: `R = s(s(s(s(s(0)))))` and `→ succeeds`.

Peano arithmetic represents naturals via `0` (zero) and `s/1` (successor). `s(s(0))` is 2; `s(s(s(0)))` is 3. The `plus/3` recursion peels successors off the first argument and pushes them onto the third: clause 2 fires for `plus(s(X), Y, s(Z?))`, recursing with `plus(X?, Y?, Z)`. After 2 recursive steps, the base case `plus(0, Y, Y?)` fires and `Y` (which is 3 = `s(s(s(0)))`) flows back through the writer/reader pairs, getting wrapped in two more `s/1` constructors. Final result: `s(s(s(s(s(0)))))` = 5. Demonstrates structural recursion on the inductive natural-number representation. Cross-check trace **Phase D**.

#### Inspection 3 — max

```
max(7, 3, M).
```

Expected: `M = 7` and `→ succeeds`.

`max/3` has two clauses — one for `X >= Y` (returns X) and one for `X < Y` (returns Y). Committed choice: the first clause's guard `X? >= Y?` is checked; with X=7, Y=3, the guard succeeds and the clause's head writer/reader pair `M` ↔ `X?` binds M = 7. The second clause is never tried. Demonstrates committed-choice on built-in numeric comparison guards. Cross-check trace **Phase E**.

### Step 5 — Cross-check against the captured trace

Open `ex-07-repl-trace.md` in this same directory. Match each phase line-for-line modulo banner / wallclock. If `F = 5040` doesn't appear for the factorial primary, or if the linear-Fibonacci inspection produces a different G, that's a halt-and-report situation.

### Optional explorations

- **Naive Fibonacci** (slow):
  ```
  fib(15, F).
  ```
  Expected: `F = 610`. Naive fib spawns O(2^N) processes; tractable for N ≤ ~25 but exponentially worse than fib_linear. Try it for N = 20 and you'll feel the pause.

- **Peano times** (multiplication):
  ```
  times(s(s(0)), s(s(s(0))), R).
  ```
  Expected: `R = s(s(s(s(s(s(0))))))` (2 × 3 = 6 in Peano successor form). Note times's recursive clause uses a `tuple(Y?)` guard to permit Y's multi-reader occurrences (Y? appears in the recursive `times` call AND the `plus` call).

- **Integer abs** (negative input):
  ```
  abs(-7, A).
  ```
  Expected: `A = 7`. Demonstrates abs's two-clause committed-choice on the sign of X.

- **Tail-recursive factorial** (per Q6 swap): comment out lines for `factorial(0, 1)` + `factorial(1, 1)` + `factorial(N, F?) :- ...` (the 3 naive clauses) AND add the tail-recursive version from book p 38. Re-load and re-run `factorial(7, F).` — same `F = 5040` but via 1 process instead of 7.

## What you've learned

By the end of this exercise you have seen:

1. **Peano arithmetic** — natural numbers represented inductively as `0` + `s(X)` successors; arithmetic operations are structural recursion on this representation. `plus/3` recurses by peeling successors off arg-1 and pushing them onto arg-3; `times/3` recurses similarly with `tuple(Y?)` guard for Y multi-reader. The Peano representation is foundational for the GLP semantics — it shows that arithmetic is just a special case of recursive term manipulation, not a primitive.
2. **Integer arithmetic via `:=`** — the `:=` body kernel evaluates a numeric expression and binds the LHS writer. One-clause definitions for `double/2`, `average/3`, `abs/2`, `max/3`. Each uses guards (`>=`, `<`) to dispatch on numeric properties of the input. Demonstrates how concurrent committed-choice handles arithmetic via the body-kernel mechanism (introduced in ch02's tutorial).
3. **Naive vs accumulator-based recursion** — naive factorial (3 clauses) spawns a new process per recursion level; tail-recursive factorial (Q6-deferred) uses a single accumulator-threaded process. Same input/output relation, different process count and memory profile. The same pattern in Fibonacci: naive `fib/2` spawns O(2^N); `fib_linear/2` + `fib_acc/4` runs in O(N).
4. **Multi-reader guards in fib_acc** — Q7's `number(B?)` guard demonstrates the same multi-reader-permissive pattern Formal 4.3 introduces. When a reader variable must appear twice in a clause body, a type-test guard (number, ground, constant, integer) permits the replication.
5. **The §4.3 entry point** — recursive programming on numeric data is the substrate for ex-08's recursive programming on list/tree data. The structural-recursion patterns generalise: instead of recursing on `s(X)` you recurse on `[H|T]`; instead of `0` you have `[]`; the SRSW reader/writer threading is the same.

ex-08 (next, last in §4.3 group) covers flatten + tree_sum + insertion_sort + mergesort + tree substitution. §4.3.11 distribute_ng + copy + copy_list will be deferred per Clarifications Q8 (multiple book-internal SRSW issues; book-wide audit needed).
