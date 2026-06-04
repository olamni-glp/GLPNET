# Exercise 7 — REPL trace

This trace is the verbatim transcript of an actual GLP REPL session run on this Windows host on 2026-05-01. It demonstrates the §5.7.2 mode-error illustration: a failing-form file rejected with a 2-error mode-mismatch message, and a book-cited corrected-form file that loads cleanly and produces the expected arithmetic answer.

## Phase A — Failing-form load attempt

```glp
GLP> olamni/tutorial/ch05/exercise-07/ch-05-ex-07-mode-error-failing.glp
Error loading olamni/tutorial/ch05/exercise-07/ch-05-ex-07-mode-error-failing.glp: Exception: Type checking failed:
  Head of bar is not well-typed:
  Inconsistent path: Variable mode mismatch: writer requires ↑ (produce), got ↓ (consume)
  Path: (X, 0, input)
  Inconsistent path: Variable mode mismatch: reader requires ↓ (consume), got ↑ (produce)
  Path: (Y?, 0, output) at line 23
```

→ load failed (expected — this is the demonstration). Two errors, one per argument: arg 1 expected a writer (consume position) but got a reader `X?`; arg 2 expected a reader-hole (produce position) but got a writer `Y`. The `↑`/`↓` arrows in the message show the direction. The `at line 23` points at `bar(X?, Y).` in the failing-form `.glp`.

## Phase B — Corrected-form load

```glp
GLP> olamni/tutorial/ch05/exercise-07/ch-05-ex-07-mode-error-corrected.glp
✓ Loaded: olamni/tutorial/ch05/exercise-07/ch-05-ex-07-mode-error-corrected.glp
```

→ load succeeded. The corrected clause `bar(X, Y?) :- Y := X? + 1.` (book p 52, byte-exact) has writer `X` at the consume position, reader hole `Y?` at the produce position; body reads `X?` and writes `Y`. The mode-check passes.

## Phase C — Confirmation goal

```glp
GLP> bar(5,R).
R = 6
→ succeeds
```

The corrected clause computes `5 + 1`; `R` binds to `6`.

## Closing

```glp
GLP> :quit
Goodbye!
```

---

The three phases bracket the boundary between rejected and accepted code in the mode dimension: Phase A shows the type-checker rejecting inverted reader/writer roles with explicit `↑`/`↓` direction; Phase B shows the book-cited corrected form loading cleanly; Phase C shows the corrected form actually running and computing the expected value. Together they are the §5.7.2 mode-error illustration.
