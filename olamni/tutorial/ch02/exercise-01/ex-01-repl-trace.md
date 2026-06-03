# Exercise 01 — REPL trace (LP/GLP append contrast)

This trace is the verbatim record of a REPL session that demonstrates chapter 2's pedagogical core: the LP→GLP transition. Phase A loads the classical LP append (PDF p 10) — the SRSW analyser rejects it. Phase B loads the GLP append (PDF pp 31–32, imported from chapter 4 §4.2) — it loads cleanly and runs the primary demo goal plus three inspection goals. The contrast is the chapter's punchline: same predicate, same recursion, but the `?` reader annotations turn it from rejected to runnable.

## Phase A — Attempt to load the classical LP file

Entering the path of the LP-only file at the REPL prompt should produce an `Error loading: …` SRSW-violation message. No `✓ Loaded` line appears; the analyser stops the load before any goal can run.

```glp
GLP> D:/bstdev/research/glp/glp/olamni/tutorial/ch02/exercise-01/ch-02-ex-01-classical-append-LP-only.glp
Error loading D:/bstdev/research/glp/glp/olamni/tutorial/ch02/exercise-01/ch-02-ex-01-classical-append-LP-only.glp: SRSW violations found:
  • append/3: Line 15: Writer variable "X" occurs 2 times without ground guard or constant type
  • append/3: Line 15: Variable "X" has no reader (must have exactly one)
  • append/3: Line 15: Writer variable "Xs" occurs 2 times without ground guard or constant type
  • append/3: Line 15: Variable "Xs" has no reader (must have exactly one)
  • append/3: Line 15: Writer variable "Ys" occurs 2 times without ground guard or constant type
  • append/3: Line 15: Variable "Ys" has no reader (must have exactly one)
  • append/3: Line 15: Writer variable "Zs" occurs 2 times without ground guard or constant type
  • append/3: Line 15: Variable "Zs" has no reader (must have exactly one)
  • append/3: Line 16: Writer variable "Ys" occurs 2 times without ground guard or constant type
  • append/3: Line 16: Variable "Ys" has no reader (must have exactly one) at Line 0, Column 0
```

You are watching the SRSW analyser do its job. This is the runtime version of what Formal 2.1 (p 14) calls "No contraction" — every variable in classical LP can be used multiple times as both producer and consumer, but GLP forbids that and the analyser catches it at load time. The list of violations names every variable that breaks the rule. Phase B loads the same predicate written GLP-style and shows it accepted.

## Phase B — Load the GLP append file (cross-chapter import from ch 4 §4.2)

The GLP file contains the same predicate but with `?` reader annotations on the right-hand side of `:-` and on every continuation. The analyser accepts this version cleanly.

```glp
GLP> D:/bstdev/research/glp/glp/olamni/tutorial/ch02/exercise-01/ch-02-ex-01-glp-append.glp
✓ Loaded: D:/bstdev/research/glp/glp/olamni/tutorial/ch02/exercise-01/ch-02-ex-01-glp-append.glp
```

The `✓` says SRSW analysis, partial evaluation, type checking, and compilation all passed. The two clauses of `append/3` are now ready to run.

## Phase C — Primary demo goal

The primary goal exercises both clauses: the recursive case walks down the first list, the base case forwards the second list when the first runs dry.

```glp
GLP> append([1,2,3], [a,b,c], Zs).
Zs = [1, 2, 3, a, b, c]
→ succeeds
```

The locked binding `Zs = [1, 2, 3, a, b, c]` is the chapter's empirical anchor — first-list elements precede second-list elements in the output, and the SRSW writer/reader pairing makes the construction safe.

## Phase D — Inspection goal 1: empty first list

The base clause `append([], Ys, Ys?)` fires immediately; the second list is forwarded verbatim through the writer/reader pair `Ys` / `Ys?`.

```glp
GLP> append([], [a,b,c], Zs).
Zs = [a, b, c]
→ succeeds
```

`Zs = [a, b, c]` is just the second list copied through. No recursion happens — the base clause matches on the first call.

## Phase E — Inspection goal 2: empty second list

The recursive clause walks `[1,2,3]` down to `[]` then forwards an empty `Ys` through the base clause. Because `Ys` is empty, the result is just the original first list.

```glp
GLP> append([1,2,3], [], Zs).
Zs = [1, 2, 3]
→ succeeds
```

`Zs = [1, 2, 3]` confirms recursion bottoms out cleanly when the first list runs dry, even with an empty second list.

## Phase F — Inspection goal 3: both lists empty

The base clause matches immediately; both writers point to `[]`.

```glp
GLP> append([], [], Zs).
Zs = []
→ succeeds
```

`Zs = []` is the minimal termination behaviour — without the base clause, the recursion would never bottom out.

## What this trace proves

Phases A and B are the chapter's contrast pair: same predicate name, same recursion shape, opposite outcomes. The GLP version satisfies SRSW (Formal 2.1's "No contraction") and runs; the classical version does not and is rejected. Phases C–F then demonstrate the GLP version actually computing the canonical append, exercising both clauses across four goal shapes. Together, the six phases make §2.2's abstract LP→GLP transition observable at the REPL — which is the whole point of chapter 2.
