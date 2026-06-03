# ch06 ex-03 — §6.3 Equators: Emergency Brake — REPL trace

This trace captures the verbatim REPL session for ex-03.  Five phases: A
loads the `.glp`; B is the canonical emergency-brake demo; C, D, E run
three inspection goals.

## Phase A — Build / load

```glp
GLP> D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-03/ch-06-ex-03-equators-emergency-brake.glp
✓ Loaded: D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-03/ch-06-ex-03-equators-emergency-brake.glp
```

## Phase B — Primary demo: emergency brake

`run(my_module, my_goal, [suspend, abort], [], R).` is the canonical
emergency-brake demonstration.  The control stream `[suspend, abort]`
suspends the running goal first (handing control to `suspended_run/4`),
then aborts (causing the abort+dump clause to prepend the goal onto the
dump list).

```glp
GLP> run(my_module, my_goal, [suspend, abort], [], R).
R = [my_goal]
→ succeeds
```

Trace:
- `run/5` matches `[suspend|Cs]` clause (Cs = `[abort]`).
- `suspended_run/4` matches `[abort|_]` clause → `R = [my_goal | []]`.

The §6.3 banner — "Equators: Emergency Brake" — is realised by the abort
control message: a running goal is dumped from execution and accumulated
on the dump list, just as an emergency brake stops a running train and
records the event in the log.

## Phase C — Inspection 1: halt with pre-existing dump

`run(my_module, true, [], [done], R).` exercises `run/5`'s halt clause in
isolation.  The goal `true` matches the halt clause, and the dump-list-out
is identical to the dump-list-in.

```glp
GLP> run(my_module, true, [], [done], R).
R = [done]
→ succeeds
```

Demonstrates: the halt clause is the natural termination point.  Pre-
existing dump entries (here `[done]`) are preserved.

## Phase D — Inspection 2: full suspend-resume-suspend-abort cycle

`run(my_module, my_goal, [suspend, resume, suspend, abort], [], R).`
exercises the resume clause (which transitions back to `run/5` from
`suspended_run/4`).

```glp
GLP> run(my_module, my_goal, [suspend, resume, suspend, abort], [], R).
R = [my_goal]
→ succeeds
```

Trace:
- `run/5` suspend → `suspended_run/4` with `[resume, suspend, abort]`.
- `suspended_run/4` resume → back to `run/5` with `[suspend, abort]`.
- `run/5` suspend → `suspended_run/4` with `[abort]`.
- `suspended_run/4` abort+dump → `R = [my_goal]`.

Demonstrates: resume is the inverse of suspend; the goal can transition
between the two states multiple times before being aborted.

## Phase E — Inspection 3: cross-module + fork

`run(my_module, sub # (g1, g2), [suspend, abort], [], R).` exercises the
cross-module clause (which transitions to running the goal under a
different module) AND the fork clause (which runs a conjunction sequentially
through the dump list).

```glp
GLP> run(my_module, sub # (g1, g2), [suspend, abort], [], R).
R = [g2, g1]
→ succeeds
```

Trace:
- `run/5` cross-module → `run(sub, (g1, g2), [suspend, abort], [], R)`.
- `run/5` fork (with `ground(M?)`, `ground(Cs?)` guards both succeeding) →
  recurses on `g1` and `g2` sequentially.
- Each branch hits suspend → abort+dump → dumps `g1` and `g2` onto the
  shared dump list.  Result is `[g2, g1]` because the fork runs A first
  then B and each branch prepends its goal.

Demonstrates: cross-module call chains through to the fork clause; fork's
sequential threading + the per-branch suspend+abort produces a deterministic
dump order.

## Coverage notes

The four goals collectively exercise 6 of the 7 clauses in this exercise:
halt, fork, cross-module, suspend, resume, abort+dump.  The seventh clause
— `run/5`'s reduce clause — requires an actual object-program module
providing `reduce/2` clauses, which is out of scope for a single-`.glp`
file (per FR-009).  This is documented in `ex-03-tutorial.md` and recorded
as the ex-03 FR-006 relaxation per spec FR-013.

---

This control meta-interpreter is byte-exact from ch04 §4.4.4 (book p 42),
re-presented here under §6.3 with type definitions + procedure declarations
introduced fresh.  Three SRSW-related amendments (rename `M` → `_` in two
clauses where M is unused; add `ground(M?)` to two guards where M? appears
twice in the body) are documented in the `.glp` header and are necessary
because the byte-exact ch04 source does not satisfy SRSW under tight
typing.  See `ex-03-tutorial.md` for the full amendment table.
