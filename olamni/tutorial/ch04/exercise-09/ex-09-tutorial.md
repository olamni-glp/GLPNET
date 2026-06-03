# Exercise 9 — Metaprogramming Foundations

Welcome to chapter 4, exercise 9 — the §4.4 group entry point. This exercise introduces metaprogramming: programs that manipulate other programs as data. Two foundational pieces: the `reduce/2` programs-as-data encoding (representing a GLP program's clauses as data terms) + the trust-mode meta-interpreter `run/2` (a tiny GLP program that interprets other GLP programs by stepping through their reduce/2 encoded clauses). ex-10 (next, last in §4.4 group + chapter) extends to fail-safe meta-interpretation.

## Q9 amendment notice

Book p 41's `run/2` has multiple SRSW issues that strict GLP analysis surfaces:

- **Halt clause** `run(M, true).` and **cross-module clause** `run(M, M1 # G) :- run(M1?, G?).` — M is in head but unused in body. Per strict SRSW, every variable must have exactly one writer + one reader; an unused head writer with no body reader is a violation. Per GLP convention, anonymous variables (`_`) are exempt — replacing M with `_` in these two clauses satisfies SRSW.

- **Fork clause** `run(M, (A,B)) :- run(M?, A?), run(M?, B?).` and **reduce clause** `run(M, A) :- tuple(A?) | M # reduce(A?, B), run(M?, B?).` — M? appears multiple times in the body. Per Formal 4.3 (book p 36), unbound multi-reader occurrences violate SRSW; a multi-reader-permissive guard is required. Module names are atoms (constants) — `constant(M?)` is on the multi-reader-permissive list. Adding `constant(M?)` to these clauses' guards satisfies SRSW.

Total: 4 small `?`/`_` adjustments per Q9 amendment. Pedagogical content (trust-mode MI dispatch on halt/fork/cross-module/reduce) preserved unchanged. The fifth book-internal SRSW inconsistency surfaced during ch01–ch04 implementation (after Q3a, Q4, Q5, Q7).

## Before you start

The §4.3 group (ex-07 + ex-08) must be approved before ex-09 unlocks. Read book §4.4's "Programs as Data" + "Trust Mode Meta-Interpreter" subsections (book p 41). The reduce/2 encoding and the run/2 dispatch are the foundational concepts; ex-10 extends them to fail-safe + control + tracing variants.

## What's in the file

`ch-04-ex-09-metaprogramming-foundations.glp` — 7 clauses byte-exact-ish from book p 41 (with Q9 amendments to run/2):

- **§4.4.1 reduce/2 encoding** (book p 41): 3 unit clauses encoding the §3.1 fair-merge program (Program 3.1 — the 3-clause merge from ch03). Each reduce/2 fact is `reduce(<head-pattern>, <body-pattern>).` — a tuple naming the clause's head and body.
- **§4.4.2 trust-mode `run/2`** (book p 41): 4 clauses dispatching on the goal's shape — halt (`run(_, true)`), fork (`run(M, (A,B))`), cross-module (`run(_, M1 # G)`), reduce (`run(M, A) :- tuple(A?) | ...`).

## The exercise

### Step 1 — Open the REPL

If your REPL session from ex-08 is still open, you can `:quit` it and start fresh. Otherwise:

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Load the ex-09 file

```
olamni/tutorial/ch04/exercise-09/ch-04-ex-09-metaprogramming-foundations.glp
```

You should see `✓ Loaded:`. The 7 clauses (3 reduce/2 + 4 run/2) are now in the procedure table. Cross-check trace **Phase A**.

### Step 3 — Run the primary demo goal: reduce direct call (clause-1 match)

```
reduce(merge([1,2], [3,4], Z), Body).
```

Expected: `Z = [1 | X12]` and `Body = merge([2], [3, 4], X12)` and `→ succeeds`.

What happens: `reduce/2`'s first clause `reduce(merge([X|Xs], Ys, [X?|Zs?]), merge(Xs?, Ys?, Zs)).` matches the goal's first argument `merge([1,2], [3,4], Z)`. The head's pattern `merge([X|Xs], Ys, [X?|Zs?])` decomposes the input: X=1, Xs=[2], Ys=[3,4]; the `[X?|Zs?]` reader-pattern produces `[1|X12]` (where X12 is a fresh writer/reader pair for the cons tail) into the goal's `Z` writer. The head's second argument `merge(Xs?, Ys?, Zs)` produces `merge([2], [3, 4], X12)` into the goal's Body writer (Zs's writer is paired with Z's tail X12 reader). The result: Body holds the recursive-call body shape that the meta-interpreter would dispatch next.

This is the heart of programs-as-data: a clause's structure (head + body) is encoded as a 2-arg `reduce/2` fact, and pattern-matching on the head decomposes the goal-term and produces the body-term. Cross-check trace **Phase B**.

### Step 4 — Run the three inspection goals

#### Inspection 1 — reduce direct call (base-case match)

```
reduce(merge([], [], []), Body).
```

Expected: `Body = true` and `→ succeeds`.

Goal's first argument `merge([], [], [])` matches reduce/2's third clause (the base of the encoded merge program: `reduce(merge([], [], []), true).`). Body becomes the literal `true` — meaning "this clause has no body, it just succeeds." Cross-check trace **Phase C**.

#### Inspection 2 — run trust-mode halt

```
run(my_module, true).
```

Expected: `→ succeeds`.

`run/2`'s halt clause `run(_, true).` matches the goal. M is `my_module` but the head uses `_` (anonymous) per Q9 — the meta-interpreter doesn't care about the module name when interpreting `true`. Body is empty; goal succeeds immediately. Cross-check trace **Phase D**.

#### Inspection 3 — run trust-mode fork

```
run(any_module, (true, true)).
```

Expected: `→ succeeds`.

`run/2`'s fork clause `run(M, (A,B)) :- constant(M?) | run(M?, A?), run(M?, B?).` matches: A = true, B = true, M = `any_module`. The `constant(M?)` guard (Q9-added) succeeds because `any_module` is an atom (a constant). The body spawns two concurrent sub-runs: `run(any_module, true)` and `run(any_module, true)`. Each sub-run matches the halt clause and succeeds. Both succeed → fork clause body succeeds → top-level goal succeeds. Demonstrates concurrent meta-interpretation of conjunctive goals. Cross-check trace **Phase E**.

### Step 5 — Cross-check against the captured trace

Open `ex-09-repl-trace.md` in this same directory. Match each phase line-for-line modulo banner. The reduce/2 inspection's binding (`Z = [1 | X12]`, `Body = merge([2], [3, 4], X12)`) should match exactly — the variable name `X12` may differ across runs (it's an internal-allocator-generated name), but the structure should match.

### Optional explorations

- **Run via reduce dispatch** — to see the meta-interpreter step through the reduce-encoded merge program, you'd need to set up a cross-module call: `M # reduce(A?, B)` requires `M` to be a module name with a reduce/2 procedure. The current ex-09 file has reduce/2 as a top-level procedure; for full meta-interpretation you'd need to wrap the file in a module. This is advanced; book p 41 mentions it implicitly in the reduce dispatch clause.

- **Partial reduction** — try running just the reduce dispatch directly:
  ```
  reduce(merge([5], [a,b], R), Body).
  ```
  Expected: matches clause 1; produces `R = [5 | X22]`, `Body = merge([], [a,b], X22)`. The meta-interpreter would then recurse on Body, eventually hitting the empty-empty base.

## What you've learned

By the end of this exercise you have seen:

1. **Programs as data** — a GLP clause `<head> :- <body>` becomes a `reduce/2` fact `reduce(<head>, <body>).` The clause's structural pattern-matching is preserved at the data level: pattern-matching on the reduce/2 fact's first argument decomposes a goal term into its constituent variables; the second argument produces the corresponding body term with the right writer/reader pairs.
2. **Trust-mode meta-interpretation** — `run/2` is a 4-clause dispatcher: halt (`true` succeeds immediately), fork (conjunction `(A,B)` spawns two concurrent sub-runs), cross-module (`M1 # G` switches modules), reduce (`tuple(A?)`-guarded clause-lookup-then-body-recurse). The whole meta-interpreter is 4 GLP clauses — small enough to fit on a slide, complete enough to interpret ANY GLP program (with reduce/2 encoding) trusting that programs don't fail.
3. **Anonymous variables for unused head positions** — Q9's anonymous-variable amendment (`_` instead of `M` in halt + cross-module clauses) follows GLP's SRSW convention: every named variable must have exactly one writer + one reader. Anonymous variables are exempt. When a head variable isn't used in body, anonymise it.
4. **Multi-reader guards for module-passing** — Q9's `constant(M?)` guards in fork + reduce clauses follow Formal 4.3: type-test guards (constant, ground, number, integer) permit multi-reader occurrences. Module names are atoms = constants, so `constant(M?)` is the natural guard for "this M? appears twice in the body, but it's safe because M is always an atom."
5. **The metaprogramming substrate** — programs-as-data + a small dispatcher is the foundation for everything in §4.4. ex-10 extends it to fail-safe (failure-tolerant), control (suspend/resume/abort), tracing (deterministic replay) variants. Each variant adds parameters to the dispatcher's signature; the underlying reduce/2 encoding stays the same.

ex-10 (next, last in chapter) introduces fail-safe meta-interpretation. §4.4.4 control + §4.4.5 tracing + replay deferred per Clarifications Q10 (similar systematic SRSW issues + book-wide audit needed).
