# Exercise 10 — Advanced Meta-Interpreters (Fail-Safe)

Welcome to chapter 4, exercise 10 — the last exercise in the §4.4 group AND in the chapter. This exercise builds on ex-09's trust-mode meta-interpreter by introducing the fail-safe meta-interpreter `run/4`: a variant that reports failures via a short-circuit list rather than letting them halt execution. §4.4.4 control + §4.4.5 tracing + replay are deferred per Clarifications Q10 to a separate book-wide audit branch.

## Q9 + Q10 amendment notices

- **Q9 (carried forward from ex-09)**: ex-10's fail-safe `run/4` has the same systematic SRSW issues that Q9 fixed for ex-09's trust-mode `run/2` — M unused in halt-style clauses (anonymise to `_`); M? multi-read in fork + reduce-style clauses (add `constant(M?)` guard). Same pattern, applied to the longer 5-clause `run/4` shape.

- **Q10**: book pp 42–43's §4.4.4 control meta-interpreter (`run/5` + `suspended_run/4`) and §4.4.5 tracing meta-interpreter (`run/3` + indexed `reduce/3` + `replay/3`) have the same kinds of SRSW issues plus additional book-internal issues in their longer body chains (control's body chains 6+ goals; tracing's body interleaves the trace-tree-building with the reduction). Amending all three (control + tracing + replay) end-to-end is high-cost; the resulting code would diverge non-trivially from book pp 42–43. Per Q10, ex-10 includes ONLY §4.4.3 fail-safe (with Q9-pattern amendments). The deferred §4.4.4 + §4.4.5 await a separate book-wide SRSW audit branch.

This is now six book-internal SRSW + runtime-defined-guard inconsistencies surfaced during ch01–ch04 implementation (Q3a + Q4 + Q5 + Q7 + Q8 + Q9 + Q10 — more than I expected when ch04 started). The pattern across all six is the same: book-printed clauses violate strict SRSW in small but systematic ways (multi-reader without permissive guard; writer-pass-through instead of reader-pass-through; anonymous-variable convention not applied). A book-wide audit branch (separate from this ch04 tutorial work) would clean these up systematically and let future tutorials proceed without per-exercise amendment overhead.

## Before you start

ex-09 (the §4.4 group entry) must be approved before ex-10 unlocks. Read book §4.4.3 (book pp 41–42) — fail-safe meta-interpreter. Optionally skim §4.4.4 + §4.4.5 (book pp 42–43) for the deferred control + tracing + replay material; that content remains in the book for direct study after a future SRSW audit branch lands.

## What's in the file

`ch-04-ex-10-advanced-meta-interpreters.glp` — 9 clauses byte-exact-ish from book pp 41–42 (with Q9-pattern amendments + a `reduce/2` catch-all clause):

- **reduce/2 encoding** (book p 41): 3 unit clauses encoding the Program-3.1-style merge clauses (duplicated from ex-09 per FR-010 self-containment) + 1 catch-all clause `reduce(A, failed(A?)) :- otherwise | true.` per book p 42's fail-safe-MI requirement.
- **§4.4.3 fail-safe `run/4`** (book pp 41–42): 5 clauses dispatching on goal shape with a failure-list short circuit threaded through every clause:
  - **halt** (`run(_, true, L, L?)`) — close circuit (in-list = out-list)
  - **fork** (`run(M, (A,B), L, R?)` with `constant(M?)` guard) — split chain via a Mid intermediate
  - **cross-module** (`run(_, M1 # G, L, R?)`) — switch modules
  - **report failure** (`run(_, failed(A), L, [failed(A?)|L?])`) — prepend `failed(A?)` to the failure list
  - **reduce** (`run(M, A, L, R?) :- tuple(A?), constant(M?) | M # reduce(A?, B), run(M?, B?, L?, R)`) — look up clause, continue with body, threading the failure list

The fail-safe MI's signature `run(Module, Goal, FailuresIn, FailuresOut)` is the trust-mode MI's signature plus two extra parameters threading the failure short-circuit list through every recursive call.

## The exercise

### Step 1 — Open the REPL

If your REPL session from ex-09 is still open, you can `:quit` it and start fresh. Otherwise:

```bash
"/c/Users/gavri/dart-sdk/bin/dart" run glp_runtime/.dart_tool/repl.dill
```

### Step 2 — Load the ex-10 file

```
olamni/tutorial/ch04/exercise-10/ch-04-ex-10-advanced-meta-interpreters.glp
```

You should see `✓ Loaded:`. The 9 clauses (4 reduce/2 with catch-all + 5 run/4) are now in the procedure table. Cross-check trace **Phase A**.

### Step 3 — Run the primary demo goal: fail-safe halt

```
run(my_module, true, [], R).
```

Expected: `R = []` and `→ succeeds`.

The fail-safe MI's halt clause `run(_, true, L, L?).` matches: M is anonymised (`_`), Goal is `true`, L is the input failure list `[]`. The head's third argument L is the input writer; the head's fourth argument L? is the reader paired with L — meaning the head produces L's value to the caller's R writer. So R = L = [] (no failures accumulated). The trust-mode MI's halt only succeeded; the fail-safe MI's halt also reports an empty failure list. Cross-check trace **Phase B**.

### Step 4 — Run the three inspection goals

#### Inspection 1 — Fail-safe fork

```
run(any_module, (true, true), [], R).
```

Expected: `R = []` and `→ succeeds`.

The fork clause `run(M, (A,B), L, R?) :- constant(M?) | run(M?, A?, L?, Mid), run(M?, B?, Mid?, R).` matches: A=true, B=true, M=any_module (a constant atom — `constant(M?)` guard succeeds). The body spawns TWO concurrent sub-runs threading the failure list through `Mid`:
- First sub-run: `run(any_module, true, [], Mid)` — halt clause matches, Mid = [].
- Second sub-run: `run(any_module, true, Mid?, R)` — halt clause matches, R = Mid = [].

Both succeed concurrently → fork clause body succeeds. The output list R = [] reflects "no failures occurred during either sub-run." Demonstrates how the failure list threads through concurrent computation: each sub-run sees the failures accumulated by ALL prior sub-runs. Cross-check trace **Phase C**.

#### Inspection 2 — Fail-safe failure reporting

```
run(any_mod, failed(broken_goal), [], R).
```

Expected: `R = [failed(broken_goal)]` and `→ succeeds`.

The fail-safe MI's failure-report clause `run(_, failed(A), L, [failed(A?)|L?]).` matches: Goal = `failed(broken_goal)`, A = `broken_goal`. The head's fourth argument's pattern `[failed(A?)|L?]` produces a cons cell to the caller's R writer: head is `failed(A?)` (which reads A and wraps as `failed(broken_goal)`); tail is L? (the input list passes through as the rest of the output). R = `[failed(broken_goal) | []]` = `[failed(broken_goal)]`.

This is the heart of fail-safe MI's pedagogy: a failure doesn't halt the meta-interpretation; it's recorded in the failure list and execution continues. The catch-all reduce clause (`reduce(A, failed(A?)) :- otherwise | true.`) ensures that when a goal can't be found in the program's reduce/2 encoding, the MI synthesises a `failed(A?)` body that the failure-report clause then captures. Cross-check trace **Phase D**.

#### Inspection 3 — Reduce direct (encoded base case)

```
reduce(merge([], [], []), Body).
```

Expected: `Body = true` and `→ succeeds`.

The duplicated-from-ex-09 reduce/2 clause 3 matches: `reduce(merge([], [], []), true).`. Body becomes the literal `true`. Demonstrates that the reduce/2 encoding is independent of the meta-interpreter that consumes it — you can call reduce/2 directly to inspect the encoding, and the same encoding works with trust-mode (ex-09) or fail-safe (ex-10) MIs. Cross-check trace **Phase E**.

### Step 5 — Cross-check against the captured trace

Open `ex-10-repl-trace.md` in this same directory. Match each phase line-for-line modulo banner. The fail-safe MI's failure-list semantics are the key pedagogical point — confirm that `R = [failed(broken_goal)]` appears for the failure-report inspection.

### Optional explorations

- **Anonymous module in failure-report**: try `run(_, failed(broken_goal), [], R).` directly. Expected: error `Unsupported argument type: UnderscoreTerm` because the runtime doesn't accept `_` as a top-level goal-argument literal. Use a real atom (e.g., `any_mod`) to invoke the failure-report clause. The file's clause CAN have `_` in the head pattern (anonymous head writer), but the GOAL invocation needs an actual term.

- **Multi-failure accumulation**: there's no easy single-goal demo that triggers multiple `failed(A)` reports in sequence; it would require setting up a goal that the reduce/2 encoding doesn't cover, which then dispatches via the catch-all clause. With the current file's 3-clause merge encoding (covering all merge-program clauses), every merge goal succeeds; failure only arises from the catch-all when an unsupported goal is encountered.

- **Comparison with trust-mode (ex-09)**: load ex-09's file in a separate REPL session and run `run(my_module, true).` (3-arity instead of 4-arity). The trust-mode MI succeeds without a failure list; the fail-safe MI succeeds AND reports an empty failure list. Same semantic outcome on the success path; different observable structure.

## What you've learned

By the end of this exercise (and the chapter) you have seen:

1. **Fail-safe meta-interpretation** — a 5-clause variant of trust-mode MI where every clause threads a "failures so far" list through its body. Halt closes the circuit (in-list passes through as out-list); fork splits the circuit (Mid threading); reduce continues recursively after lookup; failed prepends a `failed(A?)` marker to the list. Failures don't halt execution; they accumulate.
2. **Catch-all reduce clause** — `reduce(A, failed(A?)) :- otherwise | true.` is the defensive fallback the fail-safe MI requires. When a goal isn't covered by the program's reduce/2 encoding, the catch-all synthesises a `failed(A?)` body that the MI's failure-report clause then captures into the list.
3. **Failure-list dataflow as a programming technique** — the same SRSW reader/writer threading you've seen for stream construction (ch03 + ch04) extends naturally to "list of accumulated events." The Mid intermediate in the fork clause is just an SRSW-paired intermediate writer/reader, identical in shape to the merger pipelines you exercised in ex-04.
4. **Q9 + Q10 amendment patterns are systematic** — the anonymous-variable-for-unused-head-writer convention + the multi-reader-permissive-guard-for-multi-read-body-occurrence convention are the two systematic SRSW fixes that apply throughout. Once you internalise them, you can read book code and predict where amendments will be needed.
5. **Chapter complete** — you've worked through §4.1 (constants + compound circuits) + §4.2 (streams + buffered communication + monitors) + §4.3 (recursive numerics + recursive list/tree) + §4.4 (programs-as-data + trust-mode + fail-safe). The §4.4 control + tracing + replay variants remain in book pp 42–43 for direct study after a future book-wide SRSW audit branch lands.

The chapter is now complete (subject to project owner approval of the §4.4 group). ch05 (Types and Modes) is the next chapter in the tutorial set; it introduces type declarations and module structure, ending the REPL-only-for-chs-1-6 era.
