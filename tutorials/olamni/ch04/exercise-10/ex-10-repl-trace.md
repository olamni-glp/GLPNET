# Exercise 10 — REPL trace

Verbatim REPL session 2026-04-30. Demonstrates §4.4.3 fail-safe meta-interpreter run/4. §4.4.4 control + §4.4.5 tracing + replay deferred per Clarifications Q10. Q9-style amendments applied: `_` for unused M; `constant(M?)` for multi-read M; `reduce/2` catch-all clause for unmatched goals.

## Phase A — Load ex-10 file

```glp
GLP> ✓ Loaded: olamni/tutorial/ch04/exercise-10/ch-04-ex-10-advanced-meta-interpreters.glp
```

9 clauses (4 reduce/2 with catch-all + 5 run/4) loaded.

## Phase B — Primary: fail-safe halt

```glp
GLP> R = []
→ succeeds
```

`run(my_module, true, [], R).` — fail-safe halt clause; R = L = [] (no failures accumulated).

## Phase C — Inspection 1: fail-safe fork

```glp
GLP> R = []
→ succeeds
```

`run(any_module, (true, true), [], R).` — fork spawns two run/4 subprocesses, each reaching halt; failure list threads through Mid: L = [] → run A → Mid = [] → run B → R = []. No failures reported.

## Phase D — Inspection 2: failure reporting

```glp
GLP> R = [failed(broken_goal)]
→ succeeds
```

`run(any_mod, failed(broken_goal), [], R).` — fail-safe failure-report clause prepends `failed(broken_goal)` to the input failure list `[]`, producing R = `[failed(broken_goal)]`. Demonstrates the short-circuit failure mechanism — failures don't stop execution; they accumulate in the output list.

## Phase E — Inspection 3: reduce direct

```glp
GLP> Body = true
→ succeeds
```

`reduce(merge([], [], []), Body).` — duplicated reduce/2 clause 3 (base) returns Body = true. The fail-safe MI's reduce-clause body would invoke this when stepping through merge program execution.

---

The four goals exercise: run halt clause (Phase B), run fork clause (Phase C), run failure-report clause (Phase D), reduce/2 base (Phase E). run cross-module + run reduce-clause aren't exercised in the locked 4-goal session — they require cross-module setup or full merge-execution trace which is more complex; learners can construct such goals manually.
