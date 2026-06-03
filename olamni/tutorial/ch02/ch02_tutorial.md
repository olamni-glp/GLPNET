# Chapter 2 — Logic Programs and Linear Logic

Companion tutorial for Chapter 2 of *The Art of Grassroots Logic Programming* (Shapiro, 2025). Chapter 2 is mostly theoretical — transition systems, LP syntax, MGU, linear logic, and the GLP-as-linear-logic-programming correspondence (Formal 2.1, p 14). The only executable code in chapter 2 itself is **Example 2.1 (Append)** on p 10, and that example is **classical Logic Programs**, NOT GLP — it is presented in the book to set up the SRSW contrast that the rest of the book then develops.

Because chapter 2 alone is too thin to anchor a meaningful learner exercise, this tutorial **pairs the chapter-2 classical LP append with the chapter-4 GLP append from §4.2 (book pp 31–32)** as a contrast piece. The pedagogical point is concretely visible: same predicate name, same recursion shape, but classical LP allows variable contraction (rejected by the SRSW analyser at load time) while GLP forbids it via the `?` reader annotations. Watching the rejection happen on a real file, then watching the GLP version load and run, is the most direct way to make §2.2 observable.

This is a **REPL-only chapter** (no Flutter project, no module structure). Build the GLP REPL once:

```bash
dart compile exe glp_runtime/bin/glp_repl.dart -o glp_runtime/glp_repl.exe
```

Then load each exercise's `.glp` file and follow the corresponding `ex-NN-tutorial.md` step-through.

## How to work with this chapter's tutorial code

1. Read sections §2.1 (Logic Programs — Definitions 2.1–2.10, pp 9–12) and §2.2 (Linear Logic — Definitions 2.11–2.12 and Formal 2.1, pp 12–14) in the book.
2. Pick an exercise from the status block below and open its folder.
3. Read the exercise's `ex-NN-tutorial.md` — it walks you through what to load, what to run, and what to see.
4. Cross-check your REPL output against `ex-NN-repl-trace.md` (the verbatim known-good trace).
5. The exercises form a body-kernel curriculum: ex-01 introduces the LP→GLP contrast (no math, no I/O); ex-02 adds GLP arithmetic via `:=`; ex-03 adds system time (`now/1`) and ground-term output (`'_output'/1`). Each later exercise builds on the prior; approval gates between them ensure the foundation is solid before extension.

## The cross-chapter import (why ch 4 §4.2 appears in ch 2)

Chapter 2's Example 2.1 is classical LP only; the book does not show a GLP version of `append/3` until chapter 4 §4.2 ("List Reversal — Naive Reverse"). To make the LP→GLP transition observable in chapter 2, this tutorial pulls forward the GLP `append/3` definition from pp 31–32 byte-exactly. Each exercise's `.glp` file documents this provenance in its header comment block. This is the only cross-chapter import in chapter 2; ex-02 and ex-03 build on the same import without adding new ones.

## Exercises

- [`exercise-01/`](exercise-01/) — LP/GLP append contrast (the chapter's pedagogical core). Contains TWO `.glp` files: classical LP append (intentionally rejected by the SRSW analyser) and GLP append (accepted, runs the primary demo goal `append([1,2,3], [a,b,c], Zs).`). Entry point: [`exercise-01/ex-01-tutorial.md`](exercise-01/ex-01-tutorial.md).
- [`exercise-02/`](exercise-02/) — variation on ex-01 introducing GLP arithmetic via `:=`. Defines `append_and_sum/3` which appends two number lists locally and exposes the sum. Locked primary goal: `append_and_sum([1,2,3], [4,5,6], Sum).` → `Sum = 21`. Entry point: [`exercise-02/ex-02-tutorial.md`](exercise-02/ex-02-tutorial.md).
- [`exercise-03/`](exercise-03/) — amplification introducing system time (`now/1`) and ground-term I/O (`'_output'/1`). Defines `timed_append/3` which captures start, runs append, captures end, computes elapsed via `:=` subtraction, and emits `'_output'(elapsed_ms(N))`. Locked primary goal: `timed_append([1,2,3], [a,b,c], Zs).` → `Zs = [1, 2, 3, a, b, c]` plus a per-run-varying `elapsed_ms(N)` line. Entry point: [`exercise-03/ex-03-tutorial.md`](exercise-03/ex-03-tutorial.md).

## Exercise status

- exercise-01: approved 2026-04-29
- exercise-02: approved 2026-04-29
- exercise-03: approved 2026-04-29

## Sources

- `ch02-sources.md` — PDF code-block index for chapter 2 (verified against `GLP_ART.pdf` p 10).
- `ch02-specification-input-prompt.md` — plain-prose input that drove the spec at `specs/003-tutorial-ch02/spec.md`.
- `spec-rev-eng-input/ch02-DEPRECATED-spec.md` — historical reference (rev-eng input only; do NOT use as authoritative).
- `../charter.md` — the full Olamni Tutorial design charter; Principle VI of the project Constitution requires tutorial work to comply with it.
