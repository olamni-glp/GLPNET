# Exercise 9 — REPL trace

Verbatim REPL session 2026-04-30. Demonstrates §4.4.1 reduce/2 programs-as-data + §4.4.2 trust-mode meta-interpreter run/2. Q9 amendment: run/2 has anonymous M handling (`_` for clauses where M is unused) + constant(M?) guard for clauses where M is multi-read.

## Phase A — Load ex-09 file

```glp
GLP> ✓ Loaded: olamni/tutorial/ch04/exercise-09/ch-04-ex-09-metaprogramming-foundations.glp
```

7 clauses (3 reduce/2 + 4 run/2 with Q9 amendments) loaded.

## Phase B — Primary: reduce direct call

```glp
GLP> Z = [1 | X12]
Body = merge([2], [3, 4], X12)
→ succeeds
```

`reduce(merge([1,2], [3,4], Z), Body).` — matches reduce clause 1 (output from first stream). The encoding's reader/writer pairs are exposed at the REPL: Z is bound to a partial cons cell `[1 | X12]` (head 1 from input arg-1, tail X12 to be filled by body's residue); Body shape is `merge([2], [3, 4], X12)` (the recursion's continuation).

## Phase C — Inspection 1: reduce base case

```glp
GLP> Body = true
→ succeeds
```

`reduce(merge([], [], []), Body).` — matches reduce clause 3 (empty-empty base). Body = true (terminate).

## Phase D — Inspection 2: run halt

```glp
GLP> → succeeds
```

`run(my_module, true).` — matches run's halt clause (the first `run(_, true).`). M is anonymous (`_`); body is empty; succeeds immediately.

## Phase E — Inspection 3: run fork

```glp
GLP> → succeeds
```

`run(any_module, (true, true)).` — matches run's fork clause. The conjunction `(true, true)` spawns two sub-runs, each matching halt. Both succeed concurrently → fork clause body succeeds.

---

The four goals exercise: reduce clause 1 (Phase B), reduce clause 3 (Phase C), run halt clause (Phase D), run fork clause (Phase E). reduce clause 2 + run cross-module + run reduce-clause aren't exercised in the locked 4-goal session — they require cross-module setup or mid-stream reduce dispatch which is more complex; learners can construct such goals manually.
