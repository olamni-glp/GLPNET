# Chapter 6 — Typed Programming

This is the chapter signpost for chapter 6 of *The Art of Grassroots Logic
Programming* (Shapiro, 2025).

## ⚠ The chapter 6 PDF is a stub

Book p 53 of `GLP_ART.pdf` contains only the chapter title, a one-line intro
sentence ("This chapter presents advanced GLP programming techniques that
build on the moded type system introduced in Chapter 5."), and the five §6.x
section headings.  No body text and no native Programs.

Per `/speckit-clarify` Q1 (recorded in `specs/007-tutorial-ch06/spec.md`),
the chapter's tutorial content is **synthesised from chapters 1–5**: each
§6.x section is matched to the closest related Program already established
in earlier chapters of the book and re-presented under the §6.x banner with
typed declarations.  The synthesis approach is acknowledged in three places
per FR-014: (1) every `.glp` header comment, (2) this signpost, (3) the
top-level `tutorial.md` row footnote.

This is **not** new code invented for ch06; it is a typed presentation of
code that already appeared earlier in the book and is being revisited under
the §6.x heading the author intended.  Clause text is byte-exact from the
cited earlier-chapter PDF source; only the surrounding type / `procedure`
declarations (and, for ex-03's control-MI, the small `Goal` type) are
authored fresh under §6.x — these were absent from the un-typed sources.

## Chapter scope and per-section synthesis sources

| Exercise | §6.x heading | Synthesised from | Source page |
|---|---|---|---|
| ex-01 | §6.1 Difference Lists | ch04 §4.3.7 `flatten/2` + `flatten_acc/3` | book pp 38–39 |
| ex-02 | §6.2 Quicksort | ch05 §5.6 typed quicksort | book p 51 |
| ex-03 | §6.3 Equators: Emergency Brake | ch04 §4.4.4 control MI `run/5` + `suspended_run/4` | book p 42 |
| ex-04 | §6.4 Bidirectional Communication | ch03 §3.2 channel ops `send/3` + `receive/3` + `new_channel/2` + `relay/3` + `make_pair/2` | book p 23 |
| ex-05 | §6.5 Buffered Communication | ch04 §4.2.12 + §4.2.13 `bb/0` sliding-window + `bb_test/0` terminating variant | book pp 34–35 |

## Cross-chapter relationship contract

ch06's relationship to ch01–ch05 is a NEW relationship type for the tutorial:
**synthesis-from-earlier-chapters**.  Distinct from:

- ch04's *cross-chapter inversion* (ch03 imported `producer`+`consumer` from
  ch04 §4.2.1+§4.2.2; ch04 reclaimed them as native — *same code, two
  homes*).
- ch05's *typed↔untyped relationship* (ch05 §5.4 typed `merge/3` cross-
  references ch04 §4.2.5 untyped `merge/3` — *same procedure name,
  different signature/clauses*).
- ch02's *cross-chapter forward import* (ch02 imports ch04 §4.2 GLP
  `append/3` byte-exact — *same code as a forward reference*).

The synthesis-from-earlier-chapters contract documents that ch06 is
ENTIRELY synthesised because the chapter's PDF source is a stub; every
exercise has a different earlier-chapter source.

## Pairwise approval gates

Chapter 6 uses the **pairwise** approval-gate model inherited from ch01–
ch03 (NOT ch04/ch05's group-boundary model).  Five exercises with four gates
in series:

- ex-01 → ex-02 gate
- ex-02 → ex-03 gate
- ex-03 → ex-04 gate
- ex-04 → ex-05 gate

Each gate is satisfied by an `exercise-0NN: approved YYYY-MM-DD` line in
the status block below.  ex-(N+1) work begins only when ex-N is approved.

The pairwise model fits ch06 because each exercise synthesises from a
*different* earlier-chapter source — there is no natural grouping.  Each
gate is a meaningful independent decision.

## How to work with this chapter's tutorial code

1. Read book p 53 first to confirm the stub state for yourself; then read
   the earlier-chapter source for whichever §6.x exercise you're working
   on (sources cited in the table above).
2. Build the GLP REPL — see `ch01_tutorial.md` for the one-time setup.
3. Open the exercise that matches your progress in the status block below.
   ex-01 is the entry point; subsequent exercises unlock as their
   predecessor lands.
4. Each exercise has its own `ex-NN-tutorial.md` (learner step-through with
   explicit goals to type into the REPL) and `ex-NN-repl-trace.md`
   (verbatim REPL session captured on this Windows host on 2026-05-01).
   Cross-check your REPL output against the trace.

## Sources

- `ch06-sources.md` — chapter 6 PDF code-block index (currently empty
  because the chapter is a stub; documents the stub state).
- `ch06-specification-input-prompt.md` — plain-prose description of the
  synthesis approach (rev-eng input to `/speckit-specify`).
- `spec-rev-eng-input/ch06-DEPRECATED-spec.md` — quarantined reverse-
  engineering input only; superseded by `specs/007-tutorial-ch06/spec.md`
  + the artefacts in this directory.

## Exercise status

- exercise-01: approved 2026-05-01
- exercise-02: approved 2026-05-01
- exercise-03: approved 2026-05-01
- exercise-04: approved 2026-05-01
- exercise-05: approved 2026-05-01
