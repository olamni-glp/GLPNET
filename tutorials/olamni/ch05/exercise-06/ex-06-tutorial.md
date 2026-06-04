# Exercise 6 — Type Error Illustration (§5.7.1)

Welcome to chapter 5, exercise 6. This is one of the chapter's two negative-exercise pairs. **The failing-form file is meant to fail to load** — when you run it, the type-checker rejects it with a documented error message. That rejection IS the demonstration. A companion corrected-form file shows the fix.

§5.7.1 shows the type-checker doing its job: catching a declared-type-vs-actual-value mismatch at load time. The atoms `a, b, c` are not `Number`s, so a clause that puts them into a `NumList` argument doesn't load.

## Before you start

Read book §5.7.1 (Type Errors, p 51). It's a four-line example.

## What's in this folder

Two `.glp` files instead of one:

- `ch-05-ex-06-type-error-failing.glp` — marked `⚠ THIS FILE IS MEANT TO FAIL TO LOAD ⚠`. Contains `procedure foo(NumList).` + `foo([a, b, c]).`. The clause violates the declaration; the type-checker rejects it.
- `ch-05-ex-06-type-error-corrected.glp` — same declaration, with values `[1, 2, 3]` that satisfy the declared `NumList` type. Loads.

Both files duplicate `NumList ::= [] ; [Number | NumList].` inline so each file is self-contained.

## The exercise

### Step 1 — Open the REPL

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Try to load the failing-form file (expect rejection)

At the `GLP>` prompt:

```
olamni/tutorial/ch05/exercise-06/ch-05-ex-06-type-error-failing.glp
```

Expected output (multi-line):

```
Error loading …: Exception: Type checking failed:
  Head of foo is not well-typed:
  Inconsistent path: Number type requires numeric literal
  Path: ([|]/2, 0, output) → (a, 1, output)
  Inconsistent path: Number type requires numeric literal
  Path: ([|]/2, 0, output) → ([|]/2, 2, output) → (b, 1, output)
  Inconsistent path: Number type requires numeric literal
  Path: ([|]/2, 0, output) → ([|]/2, 2, output) → ([|]/2, 2, output) → (c, 1, output) at line 27
```

The type-checker walks each cons-cell of `[a, b, c]` and reports an `Inconsistent path` for each non-Number atom — three errors for `a`, `b`, `c`. The path notation traces the structure: outer cons / element 0 / "output" → atom `a` at element 1 of the inner cons. The final `at line 27` points at `foo([a, b, c]).` in the failing-form file. Cross-check: trace's **Phase A**.

**This is the demonstration — not a bug.** §5.7.1 is teaching you what the type-checker rejects and how it reports the rejection.

### Step 3 — Load the corrected-form file (expect success)

At the `GLP>` prompt:

```
olamni/tutorial/ch05/exercise-06/ch-05-ex-06-type-error-corrected.glp
```

Expected: `✓ Loaded: …` (preceded by a bytecode dump for `foo/1` — the REPL prints that during compilation; it's part of the actual load output and is captured verbatim in the trace). Cross-check: **Phase B**.

### Step 4 — Confirm the corrected form runs

```
foo(L).
```

Expected: `L = [1, 2, 3]` and `→ succeeds`. The single fact `foo([1, 2, 3]).` matches the goal; `L` binds to the list. Cross-check: **Phase C**.

### Step 5 — Cross-check against the trace

Open `ex-06-repl-trace.md` and compare modulo banner.

## What you've learned

By the end of this exercise you have seen:

1. **How the type-checker rejects a value-vs-declaration mismatch** — three `Inconsistent path` errors with explicit structural paths that point at each offending position.
2. **The negative-exercise pattern** — a *failing* file shows what the type-checker rejects; a *corrected* file shows the fix. Together they bracket the boundary between accepted and rejected code.
3. **How to read an `Inconsistent path` message** — the path notation is a breadcrumb trail through the structure: outer cons → inner cons → atom at element-index. Future type errors you encounter will use the same notation.

ex-07 (next exercise) is the second negative-exercise pair. The same shape — failing + corrected — but with a *mode* error instead of a type error. The book itself cites the corrected form on p 52, so the fix is given to you directly.
