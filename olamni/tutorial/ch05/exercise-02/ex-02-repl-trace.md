# Exercise 2 — REPL trace

This trace is the verbatim output of an actual GLP REPL session run on this Windows host on 2026-05-01. It demonstrates that the §5.2 universal `List` type loads cleanly and that the GLP type-checker recognises the built-in `Any` as its element type. Like ex-01, there are no goals to run — the load is the entire demonstration.

## Phase A — Load ex-02 file

```glp
GLP> ✓ Loaded: olamni/tutorial/ch05/exercise-02/ch-05-ex-02-built-in-types.glp
```

The universal `List` type is now in the type-checker's environment. The cons-cell shape `[Any | List]` references the built-in `Any` (any term) and the type itself recursively — both accepted.

---

This single phase is the whole trace. Together with ex-01, this completes the §5.1 + §5.2 type-system foundation. ex-03 turns to procedure declarations and the mode-checking flow.
