# Exercise 1 — Type Definitions

Welcome to chapter 5, exercise 1. This is the §5.1 entry-point exercise. Chapter 5 introduces the GLP type system; ex-01 establishes the type-definition syntax via three small recursive types — `Bit`, `Nat`, `NumList` — and gives you minimal recogniser predicates so you can probe each type at the REPL.

## Before you start

Read book §5.1 (Type Definitions, p 47) — the three short demonstrations of `::=` syntax and the recursive-type idea. Skim Formal 5.1 (Type Definition Syntax, p 48) for the four allowed alternative shapes (constant, structure, list cons, type reference).

Chapter 5 is the first chapter where the type-checker stage of the REPL pipeline does meaningful work on tutorial code — every `.glp` in this chapter passes through it.

## What's in this file

`ch-05-ex-01-type-definitions.glp` contains three type definitions byte-exact from book p 47, plus three minimal recognisers so you can run goals:

- `Bit ::= 0 ; 1.` — a `Bit` is the constant `0` or the constant `1`.
- `Nat ::= 0 ; s(Nat).` — Peano naturals. Either `0`, or `s(N)` where `N` is itself a `Nat`. The type refers to itself.
- `NumList ::= [] ; [Number | NumList].` — a typed list cons. Either `[]`, or `[Head | Tail]` where `Head` is a `Number` (built-in) and `Tail` is again a `NumList`.
- `is_bit/1`, `is_nat/1`, `is_numlist/1` — one recogniser per type. Each declared `procedure is_<name>(<Type>?).` then defined by base + recursive clauses. Their job is purely to give you something to call.

## The exercise

### Step 1 — Open the REPL

If you haven't built the REPL yet, see `ch01_tutorial.md` for the one-time setup. Then:

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Load the ex-01 file

At the `GLP>` prompt:

```
olamni/tutorial/ch05/exercise-01/ch-05-ex-01-type-definitions.glp
```

Expected: `✓ Loaded: …`. The REPL ran SRSW analysis, partial evaluation, type checking, and compilation — all in one pipeline — without errors. The three type definitions and three recognisers are now in the procedure table. Cross-check: trace's **Phase A**.

If you see `Error loading: …`, your file differs from the byte-exact version — check it against the one in this folder.

### Step 3 — Run the primary demo goal: probe the `Bit` type

```
is_bit(0).
```

Expected: `→ succeeds`. The first matching clause `is_bit(0).` succeeds. The procedure declaration `procedure is_bit(Bit?).` told the type-checker that arg 1 must be a `Bit`; the constant `0` satisfies that. Cross-check: **Phase B**.

### Step 4 — Inspection 1 — probe the recursive `Nat` type

```
is_nat(s(s(0))).
```

Expected: `→ succeeds`. The recursive clause `is_nat(s(N)) :- is_nat(N?).` peels two `s` constructors then matches the base `is_nat(0).`. The point: types can recurse, and so can their recognisers. Cross-check: **Phase C**.

### Step 5 — Inspection 2 — probe the typed `NumList` with valid contents

```
is_numlist([1,2,3]).
```

Expected: `→ succeeds`. Each element is a `Number`, so the recursive clause `is_numlist([N|Rest]) :- number(N?) | is_numlist(Rest?).` peels each cons, the `number(N?)` guard succeeds, and we recurse to the empty-list base. Cross-check: **Phase D**.

### Step 6 — Inspection 3 — the empty-list base case

```
is_numlist([]).
```

Expected: `→ succeeds`. The base clause `is_numlist([]).` matches directly. Termination case for the recursive recogniser. Cross-check: **Phase E**.

### Step 7 — Cross-check against the trace

Open `ex-01-repl-trace.md` in this directory. Match each phase line-for-line modulo banner.

### Optional — try a value that doesn't match its declared type

After the four steps above, try:

```
is_numlist([a,b,c]).
```

Atoms like `a` are not `Number`s, so this either fails outright or surfaces a guard-failure on `number(a?)`. The point: the recogniser only succeeds when the value satisfies the typed shape it declares. The chapter's negative-exercise pair (ex-06, ex-07) explores type-checker rejections in more depth.

## What you've learned

By the end of this exercise you have seen:

1. **Type-definition syntax** — the `::=` form with alternatives separated by `;`. Three of the four shapes from Formal 5.1 appear here: constant alternation (`Bit`), recursive structure (`Nat`'s `s(Nat)`), and recursive list cons (`NumList`'s `[Number | NumList]`).
2. **Recursive types and recursive recognisers** — `Nat` and `NumList` both refer to themselves. The recogniser predicates mirror that recursion with a base case + a recursive call.
3. **`procedure …(Type?).` declarations** — a procedure declaration tells the type-checker the argument type; the type-checker then validates clause heads and goals against it.
4. **The type-checker as a load-time gate** — chapter 5 turns the type-checker on for tutorial code. From now on a `.glp` won't load if a clause violates a declared type or mode. The next several exercises exercise that machinery in increasing detail.

ex-02 (next exercise) introduces the built-in types `Number`, `Atom`, and `Any`, and defines the universal `List ::= [] ; [Any | List].` — same shape as `NumList` but with the universal element type, accepting atoms, numbers, and anything else.
