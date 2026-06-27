# Chapter 5 — Types and Modes

This is the chapter signpost for chapter 5 of *The Art of Grassroots Logic Programming* (Shapiro, 2025). Chapter 5 is the GLP type system in seven exercises: type definitions (§5.1), built-in types (§5.2), procedure declarations and the worked-example mode-checking flow (§5.3+§5.4), embedded modes / response slots (§5.5), the typed-quicksort flagship (§5.6), and the type-checker's two rejection categories — type errors and mode errors (§5.7). It is the first chapter where the type-checker stage of the REPL pipeline does meaningful work on tutorial code.

## Chapter scope

- **§5.1 Type Definitions** (book p 47) — `::=` syntax, recursive types. Three short examples: `Bit`, `Nat`, `NumList`. Formal 5.1 (Type Definition Syntax, p 48).
- **§5.2 Built-in Types** (book p 48) — `Number`, `Atom`, `Any` introduced as primitives; the universal `List ::= [] ; [Any | List].` defined.
- **§5.3 Moded Procedure Declarations** (book p 48) + **§5.4 Mode Checking** (book p 49) — the `procedure …(Type?, …)` form with mode marks; the worked example walks the mode-check on typed `merge/3`. Formal 5.2 (Mode Semantics, p 49).
- **§5.5 Embedded Modes: Response Slots** (book p 50) — `?` annotations inside structures (e.g., `show(Number?)`), used as response slots. Formal 5.3 (Mode Involution, p 50).
- **§5.6 Complete Example: Typed Quicksort** (book p 51) — chapter flagship; composes §5.1–§5.5 into one program.
- **§5.7 Type Errors and Mode Errors** (book pp 51–52) — what the type-checker rejects and how it reports the rejection. Two illustrations (`foo/1` type error + `bar/2` mode error), each as a failing-form + corrected-form pair.

## Cross-chapter relationships

Two ch05 exercises carry header notes pointing back to un-typed predecessors in chapter 4:

- **ex-03 typed `merge/3`** ↔ **ch04 ex-04 untyped `merge/3`** (book §4.2.5, p 32). Same procedure name; ch05 has a `procedure` declaration with mode marks (using the universal `List` from §5.2), ch04 has none. Different clause set (3 typed vs 4 untyped), different pedagogical focus (mode checking vs stream-merge variants).
- **ex-04 typed `counter/2`** ↔ **ch04 ex-06 untyped `counter/1` + `counter_loop/2`** (book §4.2.14). Different arity, different shape (no response slot in ch04 vs response slot in ch05), different focus (objects/monitors vs embedded modes).

These are documentation-only cross-references — the ch05 clauses are byte-exact from §5.4/§5.5 PDF, not copies of ch04's.

## Negative-exercise contract

ex-06 and ex-07 are the chapter's two negative-exercise pairs. Each consists of two `.glp` files:

- A *failing-form* file marked `⚠ THIS FILE IS MEANT TO FAIL TO LOAD ⚠`. Loading it produces a documented type-checker rejection (with explicit error message and structural path). **The rejection IS the demonstration.**
- A *corrected-form* file that loads cleanly. ex-06's corrected form is implementer-chosen (re-typed values matching the declaration); ex-07's is **book-cited** — §5.7.2 itself prints the fix.

Read each pair's `ex-NN-tutorial.md` for the expected error messages and the comparison.

## Group-boundary approval gates

Chapter 5 inherits chapter 4's group-boundary approval gate model. Three gates govern progression:

1. **Foundations group (ex-01 + ex-02)** must be approved before any Mode-checking-flow exercise.
2. **Mode-checking-flow group (ex-03 + ex-04)** must be approved before the Flagship.
3. **Flagship group (ex-05)** must be approved before any Negative exercise.

Within a group, exercises are implemented sequentially. Group approval flips all the group's exercise-NN status lines together.

## Exercises

### Foundations group (§5.1 + §5.2)

- **[exercise-01](exercise-01/ex-01-tutorial.md)** — Type Definitions. The §5.1 entry. `Bit`, `Nat`, `NumList` declarations plus three minimal recogniser predicates (`is_bit`, `is_nat`, `is_numlist`) so the learner can probe each type.
- **[exercise-02](exercise-02/ex-02-tutorial.md)** — Built-in Types. Universal `List` definition + `is_list/1` recogniser; contrasts with `is_numlist/1` to show how `Any` differs from `Number`.

### Mode-checking-flow group (§5.3+§5.4 + §5.5; gated behind Foundations)

- **[exercise-03](exercise-03/ex-03-tutorial.md)** — Mode-Checked Typed Merge. The procedure declaration syntax + the §5.4 worked example on typed `merge/3`. Cross-chapter relationship to ch04 ex-04.
- **[exercise-04](exercise-04/ex-04-tutorial.md)** — Counter Response-Slot. The §5.5 typed counter with embedded `?` and Mode Involution. Cross-chapter relationship to ch04 ex-06.

### Flagship group (§5.6; gated behind Mode-checking-flow)

- **[exercise-05](exercise-05/ex-05-tutorial.md)** — Typed Quicksort. The §5.6 flagship: type def, three procedure declarations, six clauses, recursion across two predicates.

### Negatives group (§5.7; gated behind Flagship)

- **[exercise-06](exercise-06/ex-06-tutorial.md)** — Type Error Illustration. §5.7.1 — failing form rejected with three `Inconsistent path` errors; corrected form runs.
- **[exercise-07](exercise-07/ex-07-tutorial.md)** — Mode Error Illustration. §5.7.2 — failing form rejected with two mode-mismatch errors; book-cited corrected form runs.

## How to work with this chapter's tutorial code

1. Read book §5.1 first (p 47); skim §5.2–§5.7 to understand the chapter's overall scope.
2. Build the GLP REPL — see `ch01_tutorial.md` for the one-time setup.
3. Open the exercise that matches your progress in the status block below. ex-01 is the entry point; subsequent exercises unlock as their predecessor groups land.
4. Each exercise has its own `ex-NN-tutorial.md` (learner step-through with explicit goals to type into the REPL) and `ex-NN-repl-trace.md` (verbatim REPL session captured on this Windows host on 2026-05-01). Cross-check your REPL output against the trace.

## Sources

- `ch05-sources.md` — chapter 5 PDF code-block index.
- `ch05-specification-input-prompt.md` — plain-prose description of what this tutorial delivers (rev-eng input to `/buildkit-specify`).
- `spec-rev-eng-input/ch05-DEPRECATED-spec.md` — quarantined reverse-engineering input only; superseded by `specs/006-tutorial-ch05/spec.md` + the artefacts in this directory.

## Exercise status

- exercise-01: approved 2026-05-01
- exercise-02: approved 2026-05-01
- exercise-03: approved 2026-05-01
- exercise-04: approved 2026-05-01
- exercise-05: approved 2026-05-01
- exercise-06: approved 2026-05-01
- exercise-07: approved 2026-05-01
