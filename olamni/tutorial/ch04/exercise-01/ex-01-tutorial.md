# Exercise 1 — Programs with Constants + Logic Gates

Welcome to chapter 4, exercise 1. This is the §4.1 entry-point exercise. It establishes the simplest GLP programming pattern: unit clauses with constant arguments. The four logic gates defined here (`and/3`, `or/3`, `not/2`, `xor/3`) are the building blocks for ex-02's compound circuits.

## Before you start

Read book §4.1 (Programming with Constants, pp 25–30) — especially the "Unit Clauses" + "Multiple Clauses" + "Logic Gates" subsections at the start. You don't need to read §4.1's compound-circuits material yet (Clauses with Bodies + Guards for Multiple Reader Occurrences) — that's ex-02's territory.

## What's in this file

`ch-04-ex-01-constants-and-gates.glp` contains 17 unit clauses (clauses with no body) byte-exact from book pp 25 + 27 + 28:

- 1 clause for `p/1`: `p(a).` — the simplest GLP program (book p 25).
- 2 clauses for `q/1`: `q(b).` + `q(a).` — multi-clause committed-choice (book p 27). The first applicable clause is selected.
- 4 clauses for `and/3` — boolean AND truth table (book p 28).
- 4 clauses for `or/3` — boolean OR truth table.
- 2 clauses for `not/2` — boolean NOT.
- 4 clauses for `xor/3` — boolean XOR.

**Out of scope for this file** (mentioned for completeness):

- §4.1 Binary Unit Clauses (book p 27) — `p(a,b).` defines p/2 (different arity from p/1 above). The book demonstrates this as a separate program.
- §4.1 Shared Variables in Goals (book p 28) — `p(a,a).` is yet another p/2 demonstration with both arguments matching. Also a separate program in the book.

These two variants conflict with the `p(a).` p/1 clause if combined in one file (different arities don't conflict, but the demonstrations are pedagogically distinct from this exercise's theme). They are excluded here per the literal-source mandate's "no Programs that conflict with each other in the same file" interpretation. Read book pp 27–28 for the standalone demonstrations.

## The exercise

### Step 1 — Open the REPL

If you haven't built the REPL yet, see `ch01_tutorial.md` for the one-time setup. Then:

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Load the ex-01 file

At the `GLP>` prompt:

```
olamni/tutorial/ch04/exercise-01/ch-04-ex-01-constants-and-gates.glp
```

You should see `✓ Loaded:`. All 17 unit clauses are now in the REPL's procedure table. Cross-check: trace's **Phase A**.

### Step 3 — Run the primary demo goal: AND

```
and(1, 1, R).
```

Expected: `R = 1` and `→ succeeds`. The first matching clause `and(1,1,1).` succeeds; the goal's writer `R` consumes the constant `1` from the head's third position. Cross-check: **Phase B**.

### Step 4 — Run the three inspection goals

#### Inspection 1 — OR

```
or(1, 0, X).
```

Expected: `X = 1`. Matches `or(1,0,1).`. Cross-check: **Phase C**.

#### Inspection 2 — NOT

```
not(1, N).
```

Expected: `N = 0`. Matches `not(1,0).`. Cross-check: **Phase D**.

#### Inspection 3 — XOR

```
xor(0, 0, Y).
```

Expected: `Y = 0`. Matches `xor(0,0,0).`. Cross-check: **Phase E**.

### Step 5 — Cross-check against the trace

Open `ex-01-repl-trace.md` in this directory. Match each phase line-for-line modulo banner / wallclock.

### Optional — try the book's other demos

The book's "Conjunctive Goals" subsection (p 26) shows goals like `p(X), p(X?)` (writer then reader) and `p(X?), p(X)` (reader then writer with suspension). Try them:

```
p(X), p(X?).
```

Expected: `X = a` and `→ succeeds`. The first goal `p(X)` binds `X = a`; the second goal `p(X?)` reads the bound writer.

```
p(X?), p(X).
```

Expected: `X = a` and `→ succeeds`. The first goal `p(X?)` initially suspends (no writer); the second goal `p(X)` binds `X = a`; the first goal reactivates and succeeds.

These conjunctive-goal demos use only `p/1` (already loaded) and demonstrate the suspension/reactivation mechanism §4.1 introduces.

## What you've learned

By the end of this exercise you have seen:

1. **Unit clauses in action** — the simplest GLP programming form. Each clause is a single fact with constant arguments and no body. The REPL's clause table holds them; goals trigger committed-choice clause selection.
2. **Multi-clause predicates** — `q/1`'s two clauses demonstrate that the first applicable clause is selected. Order matters in committed-choice.
3. **Truth tables as Programs** — the four logic gates are pure dataflow: each truth-table row is a unit clause; each gate has 2–4 clauses covering all input combinations. No bodies, no guards, no recursion.
4. **Writer/reader argument modes** — when the goal's argument is a writer (e.g., `R` in `and(1, 1, R)`), the head's constant binds the writer. When the goal's argument is a reader (e.g., `X?` in `p(X?)`), the procedure suspends until a writer-side goal binds the value elsewhere.

ex-02 (next exercise) introduces clauses with bodies + `ground` guards on multiple readers, composing the gates from this file into compound circuits (`nand/3`, `half_adder/4`, `full_adder/5`) per Formal 4.1 + Formal 4.3.
