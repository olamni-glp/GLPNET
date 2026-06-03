# Exercise 2 — REPL trace

This trace is the verbatim output of an actual GLP REPL session run on this Windows host on 2026-04-30. It demonstrates §3.2's defined-guard machinery via `channel/1` + `process/2` (byte-exact from book p 22). The four goals exercise both `process/2` clauses (the defined-guard branch and the `otherwise` fallback) and the `channel/1` unit clause. Use this trace to cross-check your own session line-for-line modulo the REPL banner and build-wallclock lines.

## Phase A — Load ex-02 file

```glp
GLP> ✓ Loaded: olamni/tutorial/ch03/exercise-02/ch-03-ex-02-defined-guards.glp
```

`channel/1` (1 unit clause), `process/2` (2 clauses), and `handle/1` (the local stub from R-008) are now in the REPL's procedure table. The compiler unfolds `channel(X?)` at `process/2`'s clause 1 guard site into a structural pattern match against `ch(_, _)`.

## Phase B — Primary demo goal

```glp
GLP> Status = ok
→ succeeds
```

Goal: `process(ch(a, b), Status).` — the input `ch(a, b)` matches `channel/1`'s unit clause (`channel(ch(_, _)).`), so the defined guard at `process/2`'s clause 1 succeeds. The head's literal `ok` binds Status; the body call `handle(X?)` succeeds via the local handle/1 stub. Locked binding `Status = ok` (per Clarifications Q2) is empirically confirmed.

## Phase C — Inspection goal 1 (non-channel constant)

```glp
GLP> Status = error
→ succeeds
```

Goal: `process(foo, Status).` — `foo` is an atomic constant, not a `ch(_, _)` term. The `channel(X?)` guard on clause 1 fails (no structural match). Clause 2's `otherwise` guard then succeeds (all prior guards failed-not-suspended), binding Status to `error`. The clearest "guard fails, fallback selects" demonstration.

## Phase D — Inspection goal 2 (empty-args channel)

```glp
GLP> Status = ok
→ succeeds
```

Goal: `process(ch([], []), Status).` — the argument is still `ch(_, _)`-shaped (with empty-list args), so `channel/1` succeeds and clause 1 fires. Confirms that the defined guard matches the SHAPE of the term, not its contents.

## Phase E — Inspection goal 3 (list, not channel)

```glp
GLP> Status = error
→ succeeds
```

Goal: `process([1,2,3], Status).` — `[1,2,3]` is a list, not `ch(_, _)`-shaped. `channel/1` fails; `otherwise` fires; Status = error. Reinforces that the defined guard discriminates by shape rather than by some general type-coercion mechanism.

---

Together the four goals exercise both `process/2` clauses (clause 1 fires in Phases B + D when the defined guard succeeds; clause 2 fires in Phases C + E when the fallback `otherwise` selects) and the `channel/1` unit clause (succeeds in B + D; fails in C + E). The §3.2 distinction between built-in guards (used in ex-01: `>`, `ground`) and defined guards (used here: `channel/1`) is now concrete: any unit clause can become a guard predicate, and the compiler unfolds it at guard sites at compile time. ex-03 (next exercise) introduces the third §3.2 guard species — guard negation via the `~(...)` form — restricted to negatable built-in guards (`=?=`, type tests). Defined guards like `channel/1` here are NOT negatable per the SRSW Rules table on book p 24; ex-03's `lookup/3` demonstrates the distinction by negating `=?=` (which IS negatable) rather than `channel/1` (which is not).
