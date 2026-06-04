# Exercise 2 — Built-in Types

Welcome to chapter 5, exercise 2. ex-02 introduces three built-in types — `Number`, `Atom`, `Any` — and uses `Any` to define the universal `List` type. To give you something to run, the file also adds a minimal `is_list/1` recogniser. Compare its acceptance against ex-01's `is_numlist/1` to see how `Any` (universal) differs from `Number` (typed).

## Before you start

Read book §5.2 (Built-in Types, p 48). It's short — half a page — and introduces three names you'll see used as building blocks for the rest of the chapter.

## What's in this file

`ch-05-ex-02-built-in-types.glp` contains one type definition byte-exact from book p 48, plus one recogniser:

- `List ::= [] ; [Any | List].` — the universal list. Same recursive shape as `NumList` from ex-01, but `Number` is replaced with the built-in `Any`.
- `is_list/1` — declared `procedure is_list(List?).`, two clauses (`[]` base + `[_|Rest]` recursion). The head element is anonymous `_` because `Any` accepts any term; the recogniser walks the spine.

The §5.2 prose introduces three built-ins by name — you don't declare them, the GLP type-checker recognises them directly:

| Built-in | What it accepts | Example values |
|---|---|---|
| `Number` | numeric literals | `0`, `1`, `42`, `3.14` |
| `Atom` | constant atoms | `foo`, `red`, `up`, `clear` |
| `Any` | any term | numbers, atoms, structures, lists |

`Number` is what `NumList` uses; `Any` is what makes `List` universal. `Atom` will appear in later chapters.

## The exercise

### Step 1 — Open the REPL

If you haven't built the REPL yet, see `ch01_tutorial.md`. Then:

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Load the ex-02 file

At the `GLP>` prompt:

```
olamni/tutorial/ch05/exercise-02/ch-05-ex-02-built-in-types.glp
```

Expected: `✓ Loaded: …`. The `List` type and `is_list/1` recogniser are now in the procedure table. Cross-check: trace's **Phase A**.

### Step 3 — Run the primary demo goal: a mixed list

```
is_list([1,foo,42]).
```

Expected: `→ succeeds`. The element `1` is a `Number`, `foo` is an atom, `42` is again a `Number` — all are accepted because the element type is `Any`. The recursive clause walks the cons cells; each succeeds. Cross-check: **Phase B**.

### Step 4 — Inspection 1 — empty-list base case

```
is_list([]).
```

Expected: `→ succeeds`. Base clause `is_list([]).` matches directly. Cross-check: **Phase C**.

### Step 5 — Inspection 2 — list of lists

```
is_list([[a,b],[1,2]]).
```

Expected: `→ succeeds`. Each element is itself a list (`[a,b]`, `[1,2]`); they're terms, so `Any` accepts them. The recogniser doesn't recurse INTO each element, only along the outer cons spine, so the inner lists are treated as opaque `Any` values. Cross-check: **Phase D**.

### Step 6 — Inspection 3 — a list of just numbers

```
is_list([1,2,3]).
```

Expected: `→ succeeds`. `Any` is a superset of `Number`, so a `NumList` is also a `List`. This is the contrast with ex-01: the *same* numeric input is accepted by both `is_list/1` and `is_numlist/1`, but `is_list/1` would also have accepted `[1,foo,42]` from Step 3, while `is_numlist/1` would not have (the `number/1` guard would have failed on `foo`). Cross-check: **Phase E**.

### Step 7 — Cross-check against the trace

Open `ex-02-repl-trace.md` and confirm.

## What you've learned

By the end of this exercise you have seen:

1. **Built-in vs user-defined types** — `Number`, `Atom`, `Any` are pre-existing primitives recognised by the type-checker; you write `::=` declarations only for new types you define yourself.
2. **The `Any` element type** — using `Any` in a list cons gives you a universal list that accepts any term: numbers, atoms, structures, nested lists.
3. **`Number ⊆ Any`** — every `NumList` is also a valid `List`; the reverse is not true. This is the chapter's first hint at type relaxation.

ex-03 (next exercise) is your first runnable typed-program exercise that touches the chapter's main pedagogical point — *mode checking*. It introduces the `procedure` keyword's mode marks and walks through a worked example of mode checking on a typed `merge/3` (§5.4).
