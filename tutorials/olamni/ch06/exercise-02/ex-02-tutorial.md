# Exercise 2 — §6.2 Quicksort

Welcome to chapter 6, exercise 2.  This is a re-presentation of **ch05 §5.6
typed quicksort** (book p 51) under the §6.2 banner.  ch06 ex-02 is the
only chapter-6 exercise where the type definitions and procedure
declarations are ALSO byte-exact from the source chapter — because ch05
§5.6 was already typed.  Every other ch06 exercise adds fresh declarations
on top of un-typed clauses; ex-02 inherits everything.

## Why ch06 §6.2 = ch05 §5.6 verbatim

The ch06 PDF chapter is a stub.  The author named §6.2 "Quicksort"; the
closest match in chapters 1–5 is ch05 §5.6 "Complete Example: Typed
Quicksort", which is itself the chapter-5 flagship.  Re-presenting it under
§6.2 — including ch05's Q10 dual amendments — is the synthesis-from-
earlier-chapters approach defined in the chapter signpost
(`ch06_tutorial.md`).

If you have already worked through ch05 ex-05, **ex-02 is the same code**.
You can either skip the empirical run (the binding is identical) or use
ex-02 as a recap-with-the-§6.2-framing.

## Before you start

Re-read book §5.6 (Complete Example: Typed Quicksort, p 51).  Same source,
same code, viewed through the §6.2 banner.

## What's in this file

`ch-06-ex-02-typed-quicksort.glp` contains, byte-exact from ch05 §5.6 (with
ch05's Q10 dual amendments):

- `NumList ::= [] ; [Number | NumList].`
- `procedure quicksort(NumList?, NumList).` + 1 clause (entry).
- `procedure qsort(NumList?, NumList, NumList?).` + 2 clauses.
- `procedure partition(NumList?, Number?, NumList, NumList).` + 3 clauses.

## The exercise

### Step 1 — Open the REPL

```bash
./glp_runtime/glp_repl.exe
```

### Step 2 — Load the ex-02 file

```
D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-02/ch-06-ex-02-typed-quicksort.glp
```

Expected: `✓ Loaded: …`.  Cross-check: trace's **Phase A**.

### Step 3 — Primary demo: 8-element sort

```
quicksort([3,1,4,1,5,9,2,6], S).
```

Expected: `S = [1, 1, 2, 3, 4, 5, 6, 9]`.  Cross-check: **Phase B**.

### Step 4 — Inspection 1: empty list

```
quicksort([], S).
```

Expected: `S = []`.  Cross-check: **Phase C**.

### Step 5 — Inspection 2: singleton

```
quicksort([5], S).
```

Expected: `S = [5]`.  Cross-check: **Phase D**.

### Step 6 — Inspection 3: small unsorted list

```
quicksort([3,1,2], S).
```

Expected: `S = [1, 2, 3]`.  Cross-check: **Phase E**.

### Step 7 — Cross-check against the trace

Open `ex-02-repl-trace.md` and confirm.

## What you've learned

1. **§6.2 is ch05 §5.6** — recognising the synthesis-from-earlier-chapters
   contract for ch06.  When the chapter source is a stub and the
   re-presentation is byte-exact (declarations included), the §6.2 framing
   contributes the banner only — no algorithmic content beyond ch05.
2. **ch05 Q10 dual amendments persist** — ex-02 inherits the corrected
   `qsort/3` declaration `(NumList?, NumList, NumList?)` and the
   interleaved declaration-then-clauses layout.

## What ex-03 brings next

Exercise 3 is §6.3 Equators: Emergency Brake — synthesised from ch04 §4.4.4
control meta-interpreter, with abort messages on a control stream
demonstrating the emergency-brake semantics.  Type definitions and
procedure declarations are introduced fresh.
