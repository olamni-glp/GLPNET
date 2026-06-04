# ch06 ex-01 — §6.1 Difference Lists — REPL trace

This trace captures the verbatim REPL session for ex-01.  Five phases:
A loads the `.glp`; B runs the primary demo goal; C, D, E run three inspection
goals that together cover every clause of `flatten/2` and `flatten_acc/3`.

## Phase A — Build / load

The implementer rebuilds the REPL exe from the current commit and loads the
ex-01 source file.  A clean load means SRSW + partial evaluation + type-check
+ compile all passed; subsequent goals can run.

```glp
GLP> D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-01/ch-06-ex-01-difference-lists.glp
✓ Loaded: D:/bstdev/research/glp/glp/olamni/tutorial/ch06/exercise-01/ch-06-ex-01-difference-lists.glp
```

The `✓ Loaded:` line is the type-checker + compiler success signal.

## Phase B — Primary demo goal

`flatten([[1,2],[3,[4,5]]], Out).` is the canonical demo of the algorithm: a
two-deep nested list with mixed shapes (`[1,2]` flat at depth 1, `[3,[4,5]]`
flat-then-nested at depth 1).  Every clause of `flatten_acc/3` fires at least
once during this evaluation.

```glp
GLP> flatten([[1,2],[3,[4,5]]], Out).
Out = [5, 4, 3, 2, 1]
→ succeeds
```

The result is in REVERSE order — leaves are PRE-pended onto the accumulator
via `[X?|Acc?]` in the otherwise clause.  This is the byte-exact ch04 §4.3.7
behaviour; the §6.1 typed presentation does not change the algorithm.

## Phase C — Inspection 1: empty input

`flatten([], Out).` exercises `flatten_acc/3`'s base case in isolation: empty
input means the (empty) accumulator IS the result.

```glp
GLP> flatten([], Out).
Out = []
→ succeeds
```

The base clause `flatten_acc([], Acc, Acc?).` fires once and returns.

## Phase D — Inspection 2: singleton sub-list

`flatten([[1]], Out).` exercises the recursive list-head clause + the base
case.  The outer cons has a single sub-list `[1]`; recursing into it calls
`flatten_acc([1], [], Acc1)` which then takes the otherwise branch (1 is not
a list), giving `Acc1 = [1]`; the outer call then bottoms out via the base.

```glp
GLP> flatten([[1]], Out).
Out = [1]
→ succeeds
```

Singleton output is identical to the singleton input — the prepend-reverse
property is invisible at length 1, but the recursive list-head clause was
genuinely exercised (the `is_list([1])` guard had to succeed for the outer
cons to recurse into the sub-list).

## Phase E — Inspection 3: flat input

`flatten([1,2,3], Out).` exercises the otherwise clause + the base case
(no recursive list-head clause fires because no head is itself a list).

```glp
GLP> flatten([1,2,3], Out).
Out = [3, 2, 1]
→ succeeds
```

Three otherwise iterations prepend `1`, then `2`, then `3` onto the
accumulator, then the base case fires on the empty tail.  Result is reversed
— same property as Phase B, more visible here.

---

This `flatten/2` + `flatten_acc/3` pair is byte-exact from ch04 §4.3.7
(book pp 38–39); re-presented here under §6.1 with the `NestedList ::= [] ;
[_ | NestedList]` type definition introduced fresh per Q2 deferral.  The
accumulator threading mirrors a difference-list `List \ List?` pattern,
which is the §6.1 banner topic.
