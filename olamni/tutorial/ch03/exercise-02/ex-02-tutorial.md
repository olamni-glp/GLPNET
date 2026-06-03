# Exercise 2 — §3.2 defined guards via channel/1 + process/2

Welcome to chapter 3, exercise 2. This exercise introduces the second of book §3.2's three guard species: **defined guards** — user-extensible guard predicates built from unit clauses or short procedures that the compiler unfolds at guard sites. ex-01 demonstrated the first species (built-in guards: `>`, `ground`); ex-02 demonstrates defined guards via the canonical `channel/1` + `process/2` example from book p 22; ex-03 (next) demonstrates the third species — guard negation via the `~(...)` form on a negatable built-in guard.

## Before you start

You should have completed exercise-01 first (the chapter signpost's status block confirms ex-01 is approved before unblocking ex-02). Read book §3.2 specifically, paying attention to the "Defined Guards" subsection (book p 22) which presents `channel/1` + `process/2` as the canonical defined-guard example, and the SRSW Rules for Defined Guards table on book p 24 (which we'll reference in ex-03).

## What's new in ex-02

Compared to ex-01:

- ex-01 used **built-in guards only** — `>` arithmetic comparison and `ground` term-groundedness test. These are runtime-implemented; you can't add new ones from user code.
- ex-02 introduces **defined guards** — any unit clause can become a guard predicate. The compiler unfolds the unit clause at any guard site that calls the predicate, turning it into a structural pattern match. Defined guards extend the built-in guard vocabulary while remaining bound by the same SRSW reader-position rules.

The `channel/1` defined guard is a TYPE TEST: it succeeds when its argument matches the shape `ch(_, _)` (a 2-arg `ch` term). `process/2` then USES it at its first clause's guard site to dispatch between an "ok" path (when input is a channel) and an "error" path (when input is anything else, via the built-in `otherwise` guard).

## The exercise

This exercise is **stand-alone** — you do NOT need to load Program 3.1 or the producer/consumer pair from ex-01. Loading just `ch-03-ex-02-defined-guards.glp` is sufficient for the four-goal trace.

### Step 1 — Open the REPL

If you've already done ex-01 in this session, your REPL might still be open. Otherwise:

```bash
./glp_runtime/glp_repl.exe
```

Or via the kernel snapshot:

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Load the ex-02 file

At the `GLP>` prompt:

```
olamni/tutorial/ch03/exercise-02/ch-03-ex-02-defined-guards.glp
```

You should see `✓ Loaded:` followed by the path. Now `channel/1` (1 unit clause), `process/2` (2 clauses), and `handle/1` (a local stub) are in the REPL's procedure table. Cross-check: trace's **Phase A**.

### Step 3 — Run the primary demo goal

```
process(ch(a, b), Status).
```

Expected response:

```
Status = ok
→ succeeds
```

What happens internally: `process(ch(a, b), Status)` matches `process/2`'s first clause `process(X, ok) :- channel(X?) | handle(X?).` with `X = ch(a, b)`. The guard `channel(X?)` triggers — the compiler has unfolded `channel/1`'s unit clause `channel(ch(_, _)).` at this site, so the guard becomes a structural match against `ch(_, _)`. Since `ch(a, b)` matches that shape, the guard succeeds. The head's literal `ok` binds Status; the body call `handle(X?)` succeeds via the local stub `handle(_).` Cross-check: **Phase B**.

### Step 4 — Run the three inspection goals

#### Inspection 1 — non-channel constant

```
process(foo, Status).
```

Expected: `Status = error` and `→ succeeds`. The constant `foo` doesn't match `ch(_, _)`, so `channel/1` fails — clause 1's guard fails. Clause 2's `otherwise` guard then succeeds (because all prior clauses' guards have failed-not-suspended), binding Status to the literal `error` from clause 2's head. The clearest "guard fails, fallback selects" demonstration. Cross-check: **Phase C**.

#### Inspection 2 — empty-args channel

```
process(ch([], []), Status).
```

Expected: `Status = ok`. Even though the args of `ch` are empty lists, the term still matches `ch(_, _)` — the underscore matches anything. Confirms that the defined guard discriminates by SHAPE, not by what's inside. Cross-check: **Phase D**.

#### Inspection 3 — list (not channel)

```
process([1,2,3], Status).
```

Expected: `Status = error`. A list is not `ch(_, _)`-shaped. Same path as inspection 1 (channel fails, otherwise fires). Reinforces the shape-not-type-coercion semantics. Cross-check: **Phase E**.

### Step 5 — Cross-check against the trace

Open `ex-02-repl-trace.md` in this same directory. Your output for each phase should match line-for-line modulo banner / wallclock.

## What you've learned

By the end of this exercise you have seen:

1. **Defined guards** in action. `channel/1` as a unit clause becomes a guard predicate; the compiler unfolds it at `process/2`'s clause 1 guard site, turning a procedure call into a structural pattern match. This extends the guard vocabulary beyond the built-in set without changing the runtime.
2. **The defined-guard / otherwise dispatch idiom**. Clause 1 with a defined guard + clause 2 with `otherwise` is a canonical §3.2 way to express "if X has property P, do path A; else do path B." It generalizes to any defined predicate.
3. **Defined guards discriminate by SHAPE, not type**. `channel/1`'s unit clause `channel(ch(_, _)).` is a pattern match — anything `ch`-shaped passes; everything else fails. There's no implicit type coercion; defined guards are pure structural tests.
4. **Defined guards have a runtime cost compared to built-in guards.** The compiler unfolds them at compile time (partial evaluation), so the dispatch is statically prepared. But they're still slower per call than a true built-in like `ground/1` because they involve a structural match. For ch3's pedagogy this overhead is invisible; for performance-critical code it matters and the book's later chapters discuss it.

ex-03 (gated behind ex-02 approval) introduces the third §3.2 guard species: **guard negation** via the `~(...)` form. Crucially, the `~(...)` form is restricted to NEGATABLE built-in guards (`=?=` and type tests, per book p 22 + the SRSW Rules table on p 24). Defined guards like the `channel/1` you saw here are NOT negatable — `~(channel(...))` would be ill-formed. ex-03's `lookup/3` demonstrates negation cleanly via `~(=?=)` on the same equality operator that its first clause uses positively.
