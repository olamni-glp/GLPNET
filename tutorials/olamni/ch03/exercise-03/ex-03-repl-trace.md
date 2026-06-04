# Exercise 3 — REPL trace

This trace is the verbatim output of an actual GLP REPL session run on this Windows host on 2026-04-30. It demonstrates §3.2's guard-negation form `~(...)` applied to the negatable built-in guard `=?=` via the `lookup/3` idiom from book p 22 (with the Q4 amendment described below). The four goals exercise both clauses (positive `=?=` in clause 1, negated `~(=?=)` in clause 2) plus the no-match termination case. Use this trace to cross-check your own session line-for-line modulo the REPL banner and build-wallclock lines.

**Q4 amendment notice**: the `.glp` file in this exercise differs from book p 22's byte-exact form by a single character — clause 2's body recursive call uses `Key?` (reader) rather than `Key` (writer per PDF). The amendment is justified by book Formal 4.2 (p 31) "SRSW in Continuation Calls — the recursive call passes readers, not writers" and was forced by an empirical halt at first-implementation: the byte-exact form failed under strict SRSW with `WARNING: PutVariable got unexpected value: Const(<key>) (isReader=false)` on any goal requiring clause-2 negated-recursion descent. The amendment preserves the locked primary goal binding `V = 2` and the pedagogical content (positive vs negated `=?=` on the same operator) unchanged. See `specs/004-tutorial-ch03/spec.md` Clarifications Q4 for full reasoning.

## Phase A — Load ex-03 file

```glp
GLP> ✓ Loaded: olamni/tutorial/ch03/exercise-03/ch-03-ex-03-guard-negation.glp
```

`lookup/3` (2 clauses) is now in the REPL's procedure table. Clause 1 uses `=?=` in positive form; clause 2 uses `~(=?=)` in negated form on the same operator. The `~(...)` form is restricted to negatable built-in guards per the SRSW Rules table on book p 24 — `=?=` is on that list; defined guards (e.g., ex-02's `channel/1`) and arithmetic comparisons (`<`, `>`, `=:=`, etc.) and `otherwise` are NOT.

## Phase B — Primary demo goal

```glp
GLP> V = 2
→ succeeds
```

Goal: `lookup(b, [(a,1),(b,2),(c,3)], V).` — recursion descends in two steps. Step 1: clause 1's guard `b =?= a` fails (b ≠ a); clause 2's guard `~(b =?= a)` succeeds (negation of fail = success), recursive call advances to `lookup(b, [(b,2),(c,3)], V)`. Step 2: clause 1's guard `b =?= b` succeeds, head pattern matches `(b,2)`, V binds to 2 via writer/reader pair `V/V?`, body `true` succeeds. Locked binding `V = 2` (per Clarifications Q3) is empirically confirmed.

## Phase C — Inspection goal 1 (positive branch only)

```glp
GLP> V = 1
→ succeeds
```

Goal: `lookup(a, [(a,1),(b,2),(c,3)], V).` — clause 1 fires immediately on the first element. `a =?= a` succeeds; head pattern matches `(a,1)`; V = 1. The recursive descent via clause 2 is NOT exercised in this goal — `~(=?=)` never fires. This isolates the positive-branch behaviour.

## Phase D — Inspection goal 2 (deepest descent)

```glp
GLP> V = 3
→ succeeds
```

Goal: `lookup(c, [(a,1),(b,2),(c,3)], V).` — recursion descends in three steps. Step 1: `~(c =?= a)` succeeds, advance past `(a,1)`. Step 2: `~(c =?= b)` succeeds, advance past `(b,2)`. Step 3: `c =?= c` succeeds, head matches `(c,3)`, V = 3. The longest descent in this trace; emphasises that the negated branch's recursion is the engine of search.

## Phase E — Inspection goal 3 (no-match termination)

```glp
GLP> V = <unbound>
→ failed
```

Goal: `lookup(z, [(a,1),(b,2),(c,3)], V).` — recursion descends three times via clause 2 (`~(z =?= a)`, `~(z =?= b)`, `~(z =?= c)` all succeed), eventually arriving at `lookup(z, [], V)`. Both clause heads require a non-empty list (`[(K,V)|_]` and `[(K,_)|Rest]`), neither matches `[]`, so the procedure deterministically fails. The input list was fully ground at the call site so the empty residue is also ground; suspension is impossible. V remains `<unbound>` since no clause matched. The deterministic-fails contract from spec FR-014 + Edge Case + R-004 is empirically confirmed: a fresh runtime that produced `→ suspended` instead would have been a Principle II halt-and-report bug. See `specs/004-tutorial-ch03/spec.md` Edge Case bullet on "Goal that suspends rather than succeeds" (M1-amended) and the §3.2 SRSW Rules for Defined Guards table on book p 24 for the full guard-negatability list.

---

Together the four goals exercise both `lookup/3` clauses (clause 1 alone in Phase C; clause 2 once then clause 1 in Phase B; clause 2 twice then clause 1 in Phase D; clause 2 three times then no-match in Phase E) and demonstrate the `~(=?=)` negation form on the same operator that clause 1 uses positively. The §3.2 curriculum (built-in → defined → negation) is now complete across the three exercises: ex-01 used `>`/`ground` (built-in), ex-02 used `channel/1` (defined), ex-03 used `~(=?=)` (guard negation on a built-in negatable guard). Defined guards like ex-02's `channel/1` are NOT negatable per the SRSW Rules table on book p 24 — `~(channel(...))` would be ill-formed — so the negation form's restriction to negatable built-in guards is preserved across the curriculum.
