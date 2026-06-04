# Chapter 1 — Introduction

Companion tutorial for Chapter 1 of *The Art of Grassroots Logic Programming* (Shapiro, 2025). The chapter introduces the Grassroots vision, the SRSW (Single-Reader/Single-Writer) discipline, and a first GLP program — the fair stream merger.

This is a **REPL-only chapter** (no Flutter project, no module structure). Build the GLP REPL once:

```bash
dart compile exe glp_runtime/bin/glp_repl.dart -o glp_runtime/glp_repl.exe
```

Then load the per-exercise `.glp` file and follow each `ex-NN-tutorial.md` step-through.

## How to work with this chapter's tutorial code

1. Read sections §1.4 (Concurrent Logic Programming), §1.5 (Single-Reader/Single-Writer Insight), and §1.6 (A First GLP Program) in the book — they're 3 short pages.
2. Pick an exercise from the status block below and open its folder.
3. Read the exercise's `ex-NN-tutorial.md` — it walks you through what to load, what to run, and what to see.
4. Cross-check your REPL output against `ex-NN-repl-trace.md` (the verbatim known-good trace).
5. The exercises differ only in **variable names**: `exercise-01` is the original from the book; `exercise-02` and `exercise-03` are renamed-variable variants that show GLP's semantics depend on reader/writer pairing, not on identifier text.

## Exercises

- [`exercise-01/`](exercise-01/) — Program 1.1 (Fair Stream Merger) with the original variable names from the book (`X, Xs, Y, Ys, Zs`). Entry point: [`exercise-01/ex-01-tutorial.md`](exercise-01/ex-01-tutorial.md).
- [`exercise-02/`](exercise-02/) — same merge with semantic variable names (`First, RestFirst, Second, RestSecond, Out`). Entry point: [`exercise-02/ex-02-tutorial.md`](exercise-02/ex-02-tutorial.md).
- [`exercise-03/`](exercise-03/) — same merge with single-letter mathematical names (`A, As, B, Bs, Cs`). Entry point: [`exercise-03/ex-03-tutorial.md`](exercise-03/ex-03-tutorial.md).

## Exercise status

- exercise-01: approved 2026-04-28
- exercise-02: approved 2026-04-28
- exercise-03: approved 2026-04-28

## Sources

- `ch01-sources.md` — PDF code-block index for chapter 1 (verified against `GLP_ART.pdf` p 5).
- `spec-rev-eng-input/ch01-DEPRECATED-spec.md` — historical reference (rev-eng input only; do NOT use as authoritative).
- `../charter.md` — the full Olamni Tutorial design charter; Principle VI of the project Constitution requires tutorial work to comply with it.
