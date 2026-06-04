# Exercise 6 — REPL trace

This trace is the verbatim transcript of an actual GLP REPL session run on this Windows host on 2026-05-01. It demonstrates the §5.7.1 type-error illustration: a failing-form file that the type-checker rejects (with three `Inconsistent path` errors) and a corrected-form file that loads cleanly and runs.

## Phase A — Failing-form load attempt

```glp
GLP> olamni/tutorial/ch05/exercise-06/ch-05-ex-06-type-error-failing.glp
Error loading olamni/tutorial/ch05/exercise-06/ch-05-ex-06-type-error-failing.glp: Exception: Type checking failed:
  Head of foo is not well-typed:
  Inconsistent path: Number type requires numeric literal
  Path: ([|]/2, 0, output) → (a, 1, output)
  Inconsistent path: Number type requires numeric literal
  Path: ([|]/2, 0, output) → ([|]/2, 2, output) → (b, 1, output)
  Inconsistent path: Number type requires numeric literal
  Path: ([|]/2, 0, output) → ([|]/2, 2, output) → ([|]/2, 2, output) → (c, 1, output) at line 27
```

→ load failed (expected — this is the demonstration). The type-checker walked the cons-cells of `[a, b, c]` and rejected each non-Number atom. The `at line 27` points at `foo([a, b, c]).` in the failing-form `.glp`.

## Phase B — Corrected-form load

```glp
GLP> olamni/tutorial/ch05/exercise-06/ch-05-ex-06-type-error-corrected.glp

=== BYTECODE FOR foo/1 ===
  0: Label
  1: ClauseTry
  2: HeadStructure HeadStructure(".", 2, argSlot: 0)
  3: UnifyConstant UnifyConstant(1)
  4: Push
  5: UnifyStructure
  6: UnifyConstant UnifyConstant(2)
  7: Push
  8: UnifyStructure
  9: UnifyConstant UnifyConstant(3)
  10: UnifyConstant UnifyConstant(nil)
  11: Pop
  12: UnifyVariable UnifyVariable(reg=11, reader=false)
  13: Pop
  14: UnifyVariable UnifyVariable(reg=10, reader=false)
  15: Commit
  16: Proceed
  17: Label
  18: NoMoreClauses
=== END BYTECODE ===

✓ Loaded: olamni/tutorial/ch05/exercise-06/ch-05-ex-06-type-error-corrected.glp
```

→ load succeeded. The REPL prints a bytecode dump for `foo/1` during compilation; this is part of the actual load output and is captured verbatim. The corrected clause `foo([1, 2, 3]).` provides Number values that satisfy the declared NumList type.

## Phase C — Confirmation goal

```glp
GLP> foo(L).
L = [1, 2, 3]
→ succeeds
```

The fact `foo([1, 2, 3]).` matches the goal; `L` binds to the list of numbers.

## Closing

```glp
GLP> :quit
Goodbye!
```

---

The three phases bracket the boundary between rejected and accepted code: Phase A shows the type-checker rejecting a declared-type-vs-value mismatch with a structural error message; Phase B shows the corrected form loading cleanly (with the bytecode dump being the REPL's normal compilation output); Phase C shows the corrected form actually running. Together they are the §5.7.1 type-error illustration.
