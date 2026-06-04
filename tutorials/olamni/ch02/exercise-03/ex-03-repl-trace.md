# Exercise 03 — REPL trace (`timed_append/3`)

This trace is the verbatim record of a REPL session that demonstrates the body-kernel curriculum's second step: introducing system time via `now/1` and ground-term I/O via `'_output'/1` on top of the arithmetic introduced in ex-02. The procedure `timed_append/3` captures the wallclock at start, runs `append/3`, captures the wallclock at end, computes the elapsed milliseconds via `:=` subtraction, and emits the result via `'_output'(elapsed_ms(N))`. The integer `N` varies per run (it is wallclock-derived); the SHAPE matters, not the specific number.

## Phase A — Load the ex-03 file

```glp
GLP> D:/bstdev/research/glp/glp/olamni/tutorial/ch02/exercise-03/ch-02-ex-03-timed-append.glp
✓ Loaded: D:/bstdev/research/glp/glp/olamni/tutorial/ch02/exercise-03/ch-02-ex-03-timed-append.glp
```

The file passes SRSW, type checking, and compilation. The duplicated `append/3` is byte-identical to ex-01's GLP version; the new `timed_append/3`, `finalize/4`, `emit_elapsed/1`, and `write_through/2` procedures introduce the timing and I/O pattern.

## Phase B — Primary demo goal

```glp
GLP> timed_append([1,2,3], [a,b,c], Zs).
elapsed_ms(1)
Zs = [1, 2, 3, a, b, c]
→ succeeds
```

The locked binding `Zs = [1, 2, 3, a, b, c]` matches the ex-01 result for the same input — the `append/3` semantics are unchanged. The new piece is the `elapsed_ms(1)` line emitted via `'_output'/1` BEFORE the binding is reported; this line varies per run (the SHAPE matters, not the specific number — the elapsed-ms value is wallclock-derived and reflects whatever the host happened to take, typically 0–5 ms for small inputs on this host).

## Phase C — Inspection goal 1: degenerate case (both lists empty)

```glp
GLP> timed_append([], [], Zs).
elapsed_ms(0)
Zs = []
→ succeeds
```

The minimal input takes 0 ms on this host. The `_output` line still fires — confirming that `now/1` + `:=` + `'_output'/1` work even when `append/3` does almost no work. `Zs = []` matches the locked base-case binding from ex-01.

## Phase D — Inspection goal 2: larger input

```glp
GLP> timed_append([1,2,3,4,5,6,7,8,9,10], [a,b,c,d,e,f,g,h,i,j], Zs).
elapsed_ms(1)
Zs = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, a, b, c, d, e, f, g, h, i, j]
→ succeeds
```

Twenty elements total. The elapsed time is still typically 1 ms or less on this host — `append/3` is linear and very fast. The pedagogical point is not "timing is meaningful at this scale" but "the timing infrastructure works": `now/1` captures, `:=` subtracts, `'_output'/1` emits. At larger scales (which the chapter doesn't pursue) you would see the elapsed-ms grow.

## Phase E — Inspection goal 3: minimal non-empty

```glp
GLP> timed_append([1], [a], Zs).
elapsed_ms(0)
Zs = [1, a]
→ succeeds
```

Two elements total. Elapsed ≈ 0 ms. `_output` still fires. This case smokes the recursive clause of `append/3` plus the recursive clause of `write_through/2` plus the `:=` arithmetic plus `'_output'/1` — all the procedures in the file are exercised.

## What this trace proves

The four goals together exercise:
- Both clauses of `append/3` (recursive on non-empty first list, base on empty first list).
- Both clauses of `write_through/2` (recursive on non-empty, base on empty).
- The `now/1` body kernel (twice per goal — once before, once after `append/3`).
- The `:=` arithmetic body kernel `_sub` (via `End? - Start?`).
- The `'_output'/1` body kernel (once per goal, emitting `elapsed_ms(N)`).
- The `ground/1` guard (twice — once on `Zs1?` to gate `finalize/4`, once on `Elapsed?` to gate `emit_elapsed/1`).

The chapter's claim that "the same SRSW discipline that governs lists and numbers also governs side-effecting kernels" is now empirically observable. `now/1` and `'_output'/1` participate in writer/reader bonds exactly like list elements and arithmetic results: `now(Start)` writes `Start`, `Start?` reads it; `Elapsed := End? - Start?` writes `Elapsed`, `'_output'(elapsed_ms(Elapsed?))` reads it (gated by `ground(Elapsed?)` to ensure ordering against the concurrent `:=`). The pattern scales from list manipulation through arithmetic to I/O without changing the underlying SRSW invariant.

The elapsed-ms VALUE is wallclock-derived and varies per run — re-running the same goal will produce different integers. The trace's byte-equality contract (per spec FR-014) ignores the integer inside `elapsed_ms(N)` while still requiring the surrounding STRUCTURE (`elapsed_ms(`, `)`, `Zs = …`, `→ succeeds`) to be byte-equal.
