# Chapter 1 — specification input prompt

This file is the plain-prose description of what the chapter-1 tutorial must deliver. It is the input you would feed to `/speckit-specify` (or paraphrase to a human implementer) to drive the production of `specs/002-tutorial-ch01/spec.md`. **It deliberately contains no speckit ceremony**: no Feature Branch, no Status, no Constitution headers, no FR-NNN forms, no User Story / Given-When-Then forms. Those are the speckit tool's job to produce; this file's job is to describe what the chapter needs in language a human or an LLM can act on.

## What the chapter delivers

A self-contained, runnable tutorial for chapter 1 of *The Art of Grassroots Logic Programming* (Shapiro, 2025). The book introduces SRSW (Single-Reader/Single-Writer) discipline and presents one canonical program — `Program 1.1: Fair Stream Merger`, on page 5 — that demonstrates the discipline in action. The tutorial reproduces that program as a runnable `.glp` file, walks the learner through loading it in the GLP REPL, runs it against a primary demo goal and three exploratory inspection goals, and captures the verbatim REPL session as a known-good trace the learner can compare their own session against.

Chapter 1 is REPL-only. There is no Flutter project, no module structure, no type declarations (those start in chapter 5).

## Files to produce

Under `olamni/tutorial/ch01/`:

- `exercise-01/ch-01-ex-01-fair-stream-merger.glp` — the three-clause merge program from PDF p 5, byte-exact (header block plus one paraphrase comment per clause; comments paraphrase the surrounding prose from §1.4–§1.6).
- `exercise-01/ex-01-tutorial.md` — the learner-facing step-through guide. Walks through the build step, the load step, the four goals, and the cross-check against the captured trace.
- `exercise-01/ex-01-repl-trace.md` — verbatim capture of an actual REPL session run on this repo's REPL build. Five fenced code blocks (one per phase: load, primary goal, three inspection goals), with brief learner-targeted preface, brief annotations between code blocks, and a brief postscript explaining what the trace demonstrates. Code-block contents are byte-verbatim from the REPL; annotations are commentary outside the blocks.
- `ch01_tutorial.md` — chapter signpost. Brief intro to the chapter, build instructions, links to each exercise, and a date-stamped per-exercise status block (`exercise-01: approved YYYY-MM-DD` / `pending …` / `not yet implemented`). The status block is the single source of truth for which exercises a downstream session may safely build on.

Plus, the top-level `olamni/tutorial/tutorial.md` is updated to add a row for chapter 1 in the chapter-status table; rows for chapters 2–13 stay marked "planned".

## Variable-naming variants (gated)

The chapter has three exercises in total. Exercise 01 is the canonical version with the original variable names from the book (`X`, `Xs`, `Y`, `Ys`, `Zs`). Exercise 02 renames them to semantic identifiers (`First`, `RestFirst`, `Second`, `RestSecond`, `Out`). Exercise 03 renames them to single-letter mathematical identifiers (`A`, `As`, `B`, `Bs`, `Cs`). The structural shape of every clause is identical across all three; only the names differ. The pedagogical point is that GLP's semantics depend on the SRSW reader/writer pairing, not on identifier text.

Exercise 02 is implemented only after Exercise 01 is approved by the project owner. Exercise 03 is implemented only after Exercise 02 is approved. Approval is signalled by editing the `chXX_tutorial.md` status block from `pending …` to `approved YYYY-MM-DD`.

## The primary demo goal and its locked binding

The primary goal is `merge([1,2,3],[a,b],Xs).` The locked binding is `Xs = [1, a, 2, b, 3]`. This is what the book's prose on p 5–6 implies (alternation produced by the argument swap in the recursive call) and what the REPL produces. The implementation step empirically verifies this binding by running the goal under the actual REPL; mismatch is a halt-and-report bug — never a silent rewrite of the spec.

## The three inspection goals

After the primary goal, the tutorial walks the learner through three exploratory inspection goals, each exercising a different clause of Program 1.1:

1. `merge([1,2,3,4], [a], Xs).` — asymmetric pair; first stream much longer than the second. Result: `[1, a, 2, 3, 4]`. Shows that fairness applies *while both streams have elements*; surplus is forwarded linearly.
2. `merge([], [a, b, c], Xs).` — first stream empty. Result: `[a, b, c]`. Shows that the second clause's path forwards stream 2 unchanged when stream 1 is empty.
3. `merge([], [], Xs).` — both streams empty. Result: `[]`. Shows the base case; without it, recursion would never bottom out.

These three are chosen specifically so that the four-goal session (primary + three inspections) exercises *all three clauses* of Program 1.1.

## REPL infrastructure

Use the GLP REPL built from `glp_runtime/bin/glp_repl.dart` in this repo, compiled to a host executable via `dart compile exe ... -o glp_runtime/glp_repl.exe`. The Dart SDK requirement is `^3.9.4`. The compiled binary is gitignored. Building and running the REPL is a one-time setup step the learner does themselves; the tutorial documents it explicitly.

## Charter alignment

Chapter 1 is governed by `olamni/tutorial/charter.md`. The relevant charter clauses for this chapter:

- §1 (REPL-only for chapters 1–6).
- §1.5 (every clause carries a `%%` comment paraphrasing the matching paragraph of the book).
- design-principles 1–2 (section-driven for chs 1–6; reader on §X.Y loads the matching file).

## Out of scope

End-of-chapter exercises from the book (chapter 1 has none). The Formal 1.1 box on p 6 (formal-track material per the book's "How to Read This Book" guidance). The Security and Book Overview sections (§1.7, §1.8 — these are prose-only, no code). Any chapter beyond 1.

## What is NOT this file

This file is **not** the speckit feature spec. The feature spec lives at `specs/002-tutorial-ch01/spec.md` and is produced by `/speckit-specify` from this prompt. The two are separate artifacts on purpose: this prompt strips speckit ceremony so it can be written and read in plain language; the spec is the formalised, FR-numbered, user-story-shaped artifact that the rest of the speckit pipeline (`/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`, `/speckit-implement`) consumes.
