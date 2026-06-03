# Exercise 7 — Mode Error Illustration (§5.7.2)

Welcome to chapter 5, exercise 7. This is the chapter's second negative-exercise pair. **The failing-form file is meant to fail to load** — when you run it, the type-checker rejects it with a documented mode-error message. That rejection IS the demonstration. The companion corrected-form file is **book-cited** — §5.7.2 itself prints the correct fix on p 52, so you don't have to choose; the book gives you the answer.

Where ex-06 was about *type* mismatches (atoms vs Numbers), ex-07 is about *mode* mismatches: the procedure declaration's reader/writer roles are inverted in the clause head.

## Before you start

Read book §5.7.2 (Mode Errors, pp 51–52). Two short examples — the failing form and the corrected form — both quoted in the book.

## What's in this folder

Two `.glp` files:

- `ch-05-ex-07-mode-error-failing.glp` — marked `⚠ THIS FILE IS MEANT TO FAIL TO LOAD ⚠`. Contains:
  ```glp
  procedure bar(Number?, Number).
  bar(X?, Y).
  ```
  Arg 1 is declared *consume* (`Number?`) but the clause head has `X?` (a reader); arg 2 is declared *produce* (`Number`) but the clause head has `Y` (a writer). Both inverted. The type-checker rejects.
- `ch-05-ex-07-mode-error-corrected.glp` — the book-cited fix:
  ```glp
  procedure bar(Number?, Number).
  bar(X, Y?) :- Y := X? + 1.
  ```
  Arg 1 has `X` (a writer at consume — captures input); arg 2 has `Y?` (a reader at produce — hole for the body to fill); body reads `X?`, writes `Y`, computes `Y = X + 1`.

## The exercise

### Step 1 — Open the REPL

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Try to load the failing-form file (expect rejection)

At the `GLP>` prompt:

```
olamni/tutorial/ch05/exercise-07/ch-05-ex-07-mode-error-failing.glp
```

Expected output:

```
Error loading …: Exception: Type checking failed:
  Head of bar is not well-typed:
  Inconsistent path: Variable mode mismatch: writer requires ↑ (produce), got ↓ (consume)
  Path: (X, 0, input)
  Inconsistent path: Variable mode mismatch: reader requires ↓ (consume), got ↑ (produce)
  Path: (Y?, 0, output) at line 23
```

Two errors, one per argument. Arg 1 (declared consume ↓) appears as a reader, so the type-checker reports "writer requires produce, got consume" (i.e., to be a reader at this position you would need it to be at produce mode, but it's at consume). Arg 2 (declared produce ↑) appears as a writer, so the symmetric error. Cross-check: trace's **Phase A**.

**This is the demonstration — not a bug.** §5.7.2 is teaching you what a mode mismatch looks like in the type-checker's output.

### Step 3 — Load the corrected-form file (expect success)

At the `GLP>` prompt:

```
olamni/tutorial/ch05/exercise-07/ch-05-ex-07-mode-error-corrected.glp
```

Expected: `✓ Loaded: …`. The book-cited corrected clause has each variable's role aligned with the declaration. Cross-check: **Phase B**.

### Step 4 — Confirm the corrected form runs

```
bar(5,R).
```

Expected: `R = 6` and `→ succeeds`. The corrected clause computes `5 + 1 = 6`. Cross-check: **Phase C**.

### Step 5 — Cross-check against the trace

Open `ex-07-repl-trace.md` and confirm.

## What you've learned

By the end of this exercise you have seen:

1. **Mode error vs type error** — the type-checker reports both kinds of inconsistency. Type errors (ex-06) are about *what value* is in a position; mode errors (ex-07) are about *whether the variable's role* matches the declaration.
2. **Reading mode-mismatch messages** — `writer requires ↑ (produce), got ↓ (consume)` means "this position is at produce mode in the declaration but a reader appeared here; readers go at produce positions, but the slot you put it in is consume". The `↑`/`↓` arrows make the direction visible.
3. **Why each variable's role must match its position** — `procedure bar(Number?, Number).` declares a function from one input to one output. A clause has to *implement* that function: its head receives the input as a writer and produces the output as a reader-hole; its body fills the hole.

## Chapter wrap-up

ex-07 is the last exercise of chapter 5. You have seen:

- §5.1 type definitions (ex-01) and §5.2 built-in types (ex-02).
- §5.3 procedure declarations and §5.4 the worked-example mode-check on `merge/3` (ex-03).
- §5.5 embedded modes / response slots on the typed counter (ex-04).
- §5.6 the chapter flagship — typed quicksort composing everything (ex-05).
- §5.7 the type-checker's two rejection categories — type errors (ex-06) and mode errors (ex-07).

The chapter signpost ([`ch05_tutorial.md`](../ch05_tutorial.md)) lists the seven exercises and tracks their approval status. Chapter 6 (Typed Programming) is currently a stub in the PDF and requires source content before its tutorial can be authored.
