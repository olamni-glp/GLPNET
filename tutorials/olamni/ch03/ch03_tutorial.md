# Chapter 3 — GLP Core

This is the chapter signpost for chapter 3 of *The Art of Grassroots Logic Programming* (Shapiro, 2025). Chapter 3 is "GLP Core" — the formal presentation of the language. It carries one substantial Program (Program 3.1: GLP Fair Stream Merger, p 15) plus several short §3.2 inline guard idioms. The chapter is mostly theoretical; this tutorial bridges it to runnable code via three exercises that together teach the §3.2 guard species curriculum.

## Cross-chapter import

Chapter 3's own executable surface area is one Program plus a few short §3.2 idioms — too thin alone to power three substantial exercises. exercise-01 therefore imports a small chapter-4 exemplar (`producer/2` + `consumer/3` from book p 31, §4.2.1 + §4.2.2) byte-exact, and composes it with Program 3.1 to form a producer-merger-consumer pipeline that exercises SRSW reader/writer pairing across four roles in a single demo goal. This is the only cross-chapter import permitted in chapter 3 per the spec's Out-of-Scope section.

The `:=` body kernel inside the inherited `producer/2` and `consumer/3` clauses is a chapter-2 feature that this single import carries forward byte-exact. No ch3-introduced procedure (Program 3.1's `merge/3`, ex-02's `channel/1` + `process/2`, ex-03's `lookup/3`) uses `:=` or any other body kernel; chapter 3 stays in pure list / structure / guard territory for everything it newly introduces.

## §3.2 Guard Curriculum

The three exercises form a progressive curriculum through book §3.2's three guard species:

- **exercise-01 (built-in guards)** — Program 3.1 + the ch4 `producer/2` + `consumer/3` exemplar. Uses only built-in guards: `>` from `producer/2`'s recursive clause, `ground` from `consumer/3`'s recursive clause, plus the implicit head-pattern matching of `merge/3`'s three clauses. Establishes the SRSW reader/writer discipline in a multi-procedure composed goal.
- **exercise-02 (defined guards)** — `channel/1` + `process/2` byte-exact from book p 22. Introduces §3.2's defined-guard machinery: a unit clause (`channel/1`) becomes a guard predicate that the compiler unfolds at the `channel(X?)` guard site of `process/2`'s first clause. Defined guards extend the built-in guard vocabulary while remaining bound by the same SRSW reader-position rules.
- **exercise-03 (guard negation)** — `lookup/3` complete (both clauses) byte-exact from book p 22. Introduces §3.2's guard-negation form `~(...)`: clause 1 uses `=?=` in positive form; clause 2 uses `~(=?=)` on the same operator. Demonstrates that the negation form is restricted to negatable built-in guards (`=?=`, type tests, etc.); defined guards (e.g., ex-02's `channel/1`) and arithmetic comparisons / `otherwise` are NOT negatable, per the SRSW Rules table on book p 24.

## How to work with this chapter's tutorial code

1. Read book §3.1 first (Reader/Writer pairs, the SO Invariant, GLP operational semantics — pp 15–17). Skim §3.2 (pp 21–24) so you know the three guard species ahead.
2. Build the GLP REPL — see `exercise-01/ex-01-tutorial.md` "Building the REPL" for the one-time setup. Subsequent sessions reuse the binary.
3. Open the exercise that matches your progress in this status block. exercise-01 is the entry point; exercise-02 / exercise-03 unlock as you work through their predecessors.
4. Each exercise has its own `ex-NN-tutorial.md` (the learner step-through guide) and `ex-NN-repl-trace.md` (a verbatim REPL session captured on this Windows host on the date the exercise was approved). Cross-check your REPL output against the trace, line-for-line modulo the REPL banner.

## Sources

- `ch03-sources.md` — chapter 3 PDF code-block index (committed in `592d89e3`).
- `ch04-sources.md` (in `../ch04/`) — chapter 4 index; consulted by ex-01 for the `producer/2` + `consumer/3` cross-chapter import.
- `ch03-specification-input-prompt.md` — plain-prose description of what this tutorial delivers (pre-existing input to `/speckit-specify`).
- `spec-rev-eng-input/ch03-DEPRECATED-spec.md` — quarantined reverse-engineering input only; superseded by `specs/004-tutorial-ch03/spec.md` and the artefacts in this directory.

## Exercise status

- exercise-01: approved 2026-04-30
- exercise-02: approved 2026-04-30
- exercise-03: approved 2026-04-30
