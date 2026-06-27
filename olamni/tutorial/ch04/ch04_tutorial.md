# Chapter 4 — Basic Concurrent Programming

This is the chapter signpost for chapter 4 of *The Art of Grassroots Logic Programming* (Shapiro, 2025). Chapter 4 is the largest content chapter so far — approximately 38 substantial Programs across four sub-sections (§4.1 Programming with Constants, §4.2 Streams, §4.3 Recursive Programming, §4.4 Metaprogramming). The tutorial covers the entire chapter via 10 exercises grouped by sub-section family.

## Chapter scope

- **§4.1 Programming with Constants** (book pp 25–30) — unit clauses, multi-clause committed-choice, logic gates, clauses-with-bodies, multi-reader-ground-guards, compound circuits. Formal 4.1 (Produces/Consumes Parameters) + Formal 4.3 (Multi-Reader Guards).
- **§4.2 Streams** (book pp 30–37) — producers, consumers, list reversal, merge variants (simple fair, dynamic, balanced tree), distributors, observer, ripple-carry adder, buffered communication, objects/monitors. Formal 4.2 (SRSW in Continuation Calls).
- **§4.3 Recursive Programming** (book pp 37–41) — Peano arithmetic, integer arithmetic, factorial, Fibonacci (naive + linear), flatten, tree_sum, sort variants, non-ground distributor, tree substitution.
- **§4.4 Metaprogramming** (book pp 41–43) — programs-as-data, trust-mode + fail-safe + control + tracing meta-interpreters with deterministic replay.

## Cross-chapter inversion

Chapter 3's exercise-01 used `producer/2` + `consumer/3` byte-exact from §4.2.1 + §4.2.2 (book p 31) as a cross-chapter forward import — composed with Program 3.1 into a producer-merger-consumer pipeline. **Chapter 4 reclaims those procedures as their NATIVE home** in exercise-03. The byte-exact code corpus is identical between ch03's import and ch04's native presentation; the difference is the surrounding `%%` paraphrase comments + header block. ch03's header cites the cross-chapter import provenance; ch04 ex-03's header paraphrases the §4.2.1 + §4.2.2 native prose.

This inversion is the only cross-chapter procedure relationship in chapter 4 — ch04 has its own native content for everything else and does NOT import from chapters 5+ or elsewhere.

## Group-boundary approval gates (NEW for ch04)

Chapter 4 uses **group-boundary approval gates** instead of ch01–ch03's pairwise gates. Three gates govern progression:

1. **§4.1 → §4.2 gate** — both ex-01 + ex-02 must be approved before any §4.2 exercise begins.
2. **§4.2 → §4.3 gate** — all four §4.2 exercises (ex-03 through ex-06) must be approved before any §4.3 exercise begins.
3. **§4.3 → §4.4 gate** — both ex-07 + ex-08 must be approved before any §4.4 exercise begins.

Within a group, exercises are implemented sequentially but DO NOT require pairwise approval gates between them. The implementer writes all in-group exercises before pausing for the project owner's group-level review. Approval flips all the group's exercise-NN status lines together (group-atomic flip).

## Exercises

### §4.1 group

- **[exercise-01](exercise-01/ex-01-tutorial.md)** — Programs with Constants + Logic Gates. The §4.1 entry point. Unit clauses for `p/1`, `q/1`, and the four logic gates `and/3`, `or/3`, `not/2`, `xor/3`.
- **[exercise-02](exercise-02/ex-02-tutorial.md)** — Compound Circuits. Clauses with bodies + `ground` guards on multi-reader clauses. `nand/3`, `half_adder/4`, `full_adder/5` composing the ex-01 gates per Formal 4.1 + Formal 4.3.

### §4.2 group (gated behind §4.1 group)

- **exercise-03** — producer + consumer + reverse variants. Reclaims `producer/2` + `consumer/3` as their NATIVE home (cross-chapter inversion).
- **exercise-04** — merge variants (simple fair, dynamic dmerge, static merge_tree).
- **exercise-05** — stream operators (distribute, distribute_indexed, observer, ripple-carry adder).
- **exercise-06** — buffered communication + objects/monitors (counter, accumulator).

### §4.3 group (gated behind §4.2 group)

- **exercise-07** — recursive numerics (Peano + integer arith + factorial variants + Fibonacci variants).
- **exercise-08** — recursive list/tree (flatten, tree_sum, sort variants, distribute_ng with `=..`, substitute).

### §4.4 group (gated behind §4.3 group)

- **exercise-09** — metaprogramming foundations (programs-as-data + trust-mode meta-interpreter).
- **exercise-10** — advanced meta-interpreters (fail-safe + control + tracing with deterministic replay).

## How to work with this chapter's tutorial code

1. Read book §4.1 first (pp 25–30); skim §4.2 + §4.3 + §4.4 to understand the chapter's overall scope.
2. Build the GLP REPL — see `ch01_tutorial.md` for the one-time setup.
3. Open the exercise that matches your progress in the status block below. ex-01 is the entry point; subsequent exercises unlock as their predecessor groups land.
4. Each exercise has its own `ex-NN-tutorial.md` (learner step-through) and `ex-NN-repl-trace.md` (verbatim REPL session captured on this Windows host on the date the exercise was approved). Cross-check your REPL output against the trace.

## Sources

- `ch04-sources.md` — chapter 4 PDF code-block index (committed in `592d89e3`).
- `ch04-specification-input-prompt.md` — plain-prose description of what this tutorial delivers (rev-eng input to `/buildkit-specify`).
- `spec-rev-eng-input/ch04-DEPRECATED-spec.md` — quarantined reverse-engineering input only; superseded by `specs/005-tutorial-ch04/spec.md` + the artefacts in this directory.

## Exercise status

- exercise-01: approved 2026-04-30
- exercise-02: approved 2026-04-30
- exercise-03: approved 2026-04-30
- exercise-04: approved 2026-04-30
- exercise-05: approved 2026-04-30
- exercise-06: pending review (format-quality halt 2026-04-30)
- exercise-07: pending review (format-quality halt 2026-04-30)
- exercise-08: pending review (format-quality halt 2026-04-30)
- exercise-09: pending review (format-quality halt 2026-04-30)
- exercise-10: pending review (format-quality halt 2026-04-30)
