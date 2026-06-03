# Exercise 03 — `timed_append/3` (introducing system time and ground-term I/O)

**Source**: This exercise extends ex-02 by adding `now/1` (system clock, milliseconds since epoch) and `'_output'/1` (ground-term print) on top of the arithmetic introduced in ex-02. Both come from `programs/self.glp`, which dispatches them to the runtime body kernels `_now` and `_output` (defined in `glp_runtime/lib/runtime/body_kernels.dart`).

**Files in this folder**:
- `ch-02-ex-03-timed-append.glp` — duplicated `append/3` (byte-exact from pp 31–32) plus `timed_append/3`, `finalize/4`, `emit_elapsed/1`, and `write_through/2`.
- `ex-03-tutorial.md` — this file (step-by-step guide).
- `ex-03-repl-trace.md` — known-good capture of the REPL session.

## Before you start

You should have completed exercises 01 and 02. Ex-01 demonstrated the LP→GLP transition at the SRSW analyser; ex-02 introduced GLP arithmetic via `:=`. This exercise builds on both — it timing-instruments `append/3` and emits the result.

You may want to peek at the runtime kernel definitions in `glp_runtime/lib/runtime/body_kernels.dart` to see how `_now` and `_output` are implemented (about 30 lines of Dart total). You don't need to understand the Dart — the point is to see that body kernels are wired in once at the runtime and exposed to GLP via `programs/self.glp`.

## The exercise

### Step 1 — Load the file

```
GLP> olamni/tutorial/ch02/exercise-03/ch-02-ex-03-timed-append.glp
```

You should see `✓ Loaded: …`. The file defines five procedures: the duplicated `append/3`, the top-level `timed_append/3`, the gated `finalize/4` and `emit_elapsed/1`, and the helper `write_through/2`. Together they demonstrate that body kernels (timing + I/O) participate in the same SRSW writer/reader bonds you've seen for lists and arithmetic.

### Step 2 — The primary demo goal

```
GLP> timed_append([1,2,3], [a,b,c], Zs).
```

You should see TWO output lines (in this order):

```
elapsed_ms(1)
Zs = [1, 2, 3, a, b, c]
```

followed by `→ succeeds`. The `elapsed_ms(N)` line is emitted by `'_output'/1` from inside `emit_elapsed/1`; the `Zs = …` line is the goal's binding result reported by the REPL.

The integer `N` inside `elapsed_ms(N)` varies per run — it's wallclock-derived. On this host, you'll typically see 0, 1, or 2 milliseconds for this small input. **The SHAPE matters, not the specific number.** Per the spec (FR-014), the trace's byte-equality contract relaxes specifically for this integer: the auditor ignores the value inside `elapsed_ms(...)` while still requiring the surrounding structure to be byte-equal.

`Zs = [1, 2, 3, a, b, c]` is locked — it's the same result you saw in ex-01 for the same `append` inputs.

### Step 3 — Inspection goal 1: degenerate case (both lists empty)

```
GLP> timed_append([], [], Zs).
```

You should get `elapsed_ms(N)` (typically 0) followed by `Zs = []` and `→ succeeds`. The minimal input case still fires `_output`. This confirms that `now/1`, `:=`, and `'_output'/1` work end-to-end even when `append/3` does almost no work.

### Step 4 — Inspection goal 2: larger input

```
GLP> timed_append([1,2,3,4,5,6,7,8,9,10], [a,b,c,d,e,f,g,h,i,j], Zs).
```

You should get `elapsed_ms(N)` (still typically 0–5 ms — `append/3` is fast) followed by `Zs = [1, 2, 3, …, j]` and `→ succeeds`. At this scale the elapsed time is still small; the chapter doesn't pursue benchmarking, but you can see that the timing infrastructure DOES capture real wallclock differences (try running the goal multiple times and noting the variation).

### Step 5 — Inspection goal 3: minimal non-empty

```
GLP> timed_append([1], [a], Zs).
```

You should get `elapsed_ms(N)` followed by `Zs = [1, a]` and `→ succeeds`. Two-element input — exercises the recursive clauses of both `append/3` and `write_through/2` once each.

### Closing

```
:quit
```

## Cross-check against the captured trace

Compare your terminal output against `ex-03-repl-trace.md` line-for-line, modulo the build/timestamp banner AND the elapsed-ms integer. The integer N inside `elapsed_ms(N)` will differ between your run and the captured trace — that's expected and documented. The surrounding structure (the parentheses, the `Zs = …` line, the `→ succeeds`) should match exactly.

## What you've learned

By running these four goals you've seen:

1. **Body kernels participate in SRSW.** `now/1` writes a number into a writer; the corresponding reader is consumed by `:=`. `'_output'/1` reads a ground term. Each kernel has the same writer/reader semantics as `append/3` and `:=`.
2. **`ground/1` guards sequence concurrent body goals.** GLP body goals run concurrently. Without `ground(Elapsed?)`, the `'_output'(elapsed_ms(Elapsed?))` call could fire before `:=` writes `Elapsed`, printing an unbound variable like `elapsed_ms(Var@43)`. The guard suspends the output goal until `Elapsed` is fully ground. This is the canonical idiom for ordering arithmetic + side-effects in GLP.
3. **The `_now` kernel returns ms since epoch.** Two `now/1` calls (one before, one after `append/3`) produce two integers; their difference is the elapsed time. This is the same pattern any system-clock instrumentation uses.
4. **`'_output'/1` prints a ground term.** Passing `elapsed_ms(Elapsed?)` instead of just `Elapsed?` produces the structured output `elapsed_ms(2)` rather than just `2`. The structured form is grep-friendly in traces and self-documenting.

## What ch02 has covered

You've now completed all three exercises of chapter 2:
- **ex-01** — LP → GLP transition (SRSW analyser rejects classical LP append; accepts GLP append).
- **ex-02** — GLP arithmetic via `:=` (`append_and_sum/3`).
- **ex-03** — System time and ground-term I/O (`timed_append/3`).

You've used the runtime's body kernels (`_add`, `_sub`, `_now`, `_output`) via the `programs/self.glp` declarations, with the SRSW invariant intact across lists, numbers, and side-effects. These are the foundational tools you'll see used throughout the rest of the book — chapter 4's stream operations, chapter 5's typed programs, and the multi-actor scenarios in chapters 7–13 all rest on this same `:=` + `now/1` + `'_output'/1` substrate.

The next chapter (chapter 3, "GLP Core") formalises what you've already used: the operational semantics of the GLP transition system, the writer/reader bond, and the suspension mechanism that lets body goals wait for unbound readers.
