# Exercise 02 — REPL trace (`append_and_sum/3`)

This trace is the verbatim record of a REPL session that demonstrates the body-kernel curriculum's first step: introducing GLP arithmetic via the `:=` operator. The exercise composes `append/3` (duplicated byte-exact from ex-01) with a locally-defined `sum/2` to build `append_and_sum/3` — the canonical SRSW producer-consumer idiom from book p 31, where the intermediate appended list is local to the clause body and only the sum is exposed to the caller.

## Phase A — Load the ex-02 file

```glp
GLP> D:/bstdev/research/glp/glp/olamni/tutorial/ch02/exercise-02/ch-02-ex-02-append-and-sum.glp
✓ Loaded: D:/bstdev/research/glp/glp/olamni/tutorial/ch02/exercise-02/ch-02-ex-02-append-and-sum.glp
```

The file passes SRSW, type checking, and compilation. The duplicated `append/3` is byte-identical to ex-01's GLP version; the new `sum/2` and `append_and_sum/3` procedures introduce arithmetic via `:=` from `programs/self.glp`.

## Phase B — Primary demo goal

```glp
GLP> append_and_sum([1,2,3], [4,5,6], Sum).
Sum = 21
→ succeeds
```

The locked binding `Sum = 21` is the chapter's empirical anchor for arithmetic: 1+2+3+4+5+6. Inside the clause body, `append/3` writes the local `Zs = [1,2,3,4,5,6]` while `sum/2` reads `Zs?` to compute the result. The intermediate list is invisible to the caller — we only see the `Sum`.

## Phase C — Inspection goal 1: empty first list

```glp
GLP> append_and_sum([], [4,5,6], Sum).
Sum = 15
→ succeeds
```

`Sum = 15` is 4+5+6. The base clause of `append/3` fires immediately (forwarding the second list as the local `Zs`); `sum/2` then walks the resulting list. Both clauses of `sum/2` are exercised: the recursive clause for each cons cell, the base clause when the list runs out.

## Phase D — Inspection goal 2: empty second list

```glp
GLP> append_and_sum([1,2,3], [], Sum).
Sum = 6
→ succeeds
```

`Sum = 6` is 1+2+3. The recursive clause of `append/3` walks `[1,2,3]` while the base case forwards the empty `Ys`; `sum/2` walks the resulting list `[1,2,3]`.

## Phase E — Inspection goal 3: both lists empty

```glp
GLP> append_and_sum([], [], Sum).
Sum = 0
→ succeeds
```

`Sum = 0` confirms the base clause of `sum/2` fires when the appended list is empty. This is the minimal-input case — it exercises `sum([], 0).` directly.

## What this trace proves

The four goals together exercise both clauses of `append/3` AND both clauses of `sum/2`. The `:=` operator is exercised four times (once per primary/inspection goal — every non-empty input contributes at least one `+` operation). The chapter's claim that "SRSW lets a downstream consumer compute on a stream from a producer" is now empirically observable: the `append_and_sum/3` clause body has `Zs` as an internal pipeline — single writer at the `append` sub-call, single reader at the `sum` sub-call — and the runtime evaluates them as a producer-consumer pair without any guard relaxation. This is the textbook GLP idiom from book p 31 made concrete with arithmetic.
