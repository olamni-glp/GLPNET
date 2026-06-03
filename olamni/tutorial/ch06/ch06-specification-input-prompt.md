# Chapter 6 — specification input prompt

Plain-prose input for `/speckit-specify`. **No speckit ceremony**: no Feature Branch, Status, FR-NNN, User Story, Priority, Independent Test, Acceptance Scenarios, Given-When-Then, or Clarifications block. Those are the speckit pipeline's job to produce.

## What the chapter delivers

A self-contained, runnable tutorial for chapter 6 of *The Art of Grassroots Logic Programming* (Shapiro, 2025), titled **"Typed Programming"** (book p 53, PDF p 65).

**The PDF chapter is a stub** — it contains only the chapter title and five section headings, with no body text and no Programs:

> Chapter 6: Typed Programming
> 6.1 Difference Lists
> 6.2 Quicksort
> 6.3 Equators: Emergency Brake
> 6.4 Bidirectional Communication
> 6.5 Buffered Communication

Because the chapter has no native code, the tutorial is **synthesised from chapters 1–5** by selecting, for each section heading, the closest matching Program already established in earlier chapters of the book and re-presenting it under the §6.x banner with type declarations. The synthesis is acknowledged explicitly in every header comment and in the chapter signpost — these are not new Programs invented for ch06; they are typed presentations of Programs that already appeared earlier and are being revisited here under the §6.x heading the author intended.

The chapter is **REPL-only** per charter §1 (chapters 1–6 are REPL-only). No Flutter project, no module structure. Type declarations and `procedure` declarations DO appear (per ch05).

## The five exercises (one per headline)

| # | §6.x | Headline | Source program (from ch01–ch05) | PDF page (origin) |
|---|---|---|---|---|
| ex-01 | §6.1 | Difference Lists | ch04 §4.2.3 + §4.2.4 — naive `reverse/2` + accumulator `reverse_acc/3` re-presented as a difference-list idiom (the accumulator-passing pattern IS the difference-list idiom in spirit). Add a typed `procedure` declaration. | book pp 31–32 |
| ex-02 | §6.2 | Quicksort | ch05 §5.6 — Program 5.6 typed quicksort (`NumList` + `quicksort/2` + `qsort/3` + `partition/4`). Byte-exact re-presentation under the §6.2 banner. | book p 51 |
| ex-03 | §6.3 | Equators: Emergency Brake | ch04 §4.4.4 — control meta-interpreter `run/5` + `suspended_run/4` with control-stream (suspend / resume / abort). The control-stream's abort message IS the "emergency brake" demonstration. Add typed declarations. | book p 42 |
| ex-04 | §6.4 | Bidirectional Communication | ch03 §3.2 — `Channel ::= ch(Stream, Stream?)` + `send/3` + `receive/3` + `new_channel/2` + `relay/3` + `make_pair/2`. Add typed declarations consistent with ch05's mode-checking flow. | book p 23 |
| ex-05 | §6.5 | Buffered Communication | ch04 §4.2.12 + §4.2.13 — `bb/0` sliding-window buffer + `bb_test/0` terminating variant (with their `producer/2` + `consumer/2` helpers as defined in those Programs). Add typed declarations. | book pp 34–35 |

For each exercise, the GLP code body is **byte-exact** from the cited earlier-chapter PDF source, with type declarations and `procedure` declarations added on top per ch05 conventions. Adding type and procedure declarations to byte-exact code is permitted (and required) — the literal-source mandate applies to the *clauses*, not to the surrounding declarations being introduced for the first time at §6.x.

## Files to produce

Under `olamni/tutorial/ch06/`:

For each exercise (NN ∈ 01..05):
- `exercise-NN/ch-06-ex-NN-<short-name>.glp` — single GLP source file; `<short-name>` is hyphenated and descriptive (e.g., `ch-06-ex-01-difference-lists.glp`, `ch-06-ex-02-typed-quicksort.glp`).
- `exercise-NN/ex-NN-tutorial.md` — learner-facing step-through guide.
- `exercise-NN/ex-NN-repl-trace.md` — verbatim REPL session capture.

Plus chapter-level:
- `ch06_tutorial.md` (underscore) — chapter signpost: brief intro to the chapter's stub-source-and-synthesis nature, build instructions, links to the five exercises with one-line summaries, status block, and explicit prose documenting that each exercise's code originates in an earlier chapter and is re-presented under the §6.x heading.

Top-level `olamni/tutorial/tutorial.md` is updated incrementally: ch06's row flips from `planned` to `pending review (YYYY-MM-DD)` once any exercise lands and to `implemented YYYY-MM-DD` once all five are approved.

## Per-exercise format

Each exercise has:
- **One primary demo goal** plus **three inspection goals** chosen during /speckit-plan, collectively exercising every clause.
- **Locked binding** for each goal, empirically verified against the REPL during /speckit-implement.
- **Strict trace byte-equality** modulo REPL banner / build wallclock lines.
- **`%%` paraphrase comment per clause** (charter §1.5).
- **Header comment block** citing both the original earlier-chapter PDF source AND the §6.x heading under which it is re-presented; the synthesis-from-earlier-chapter status is stated explicitly.

## Approval gates

Single linear sequence with pairwise gates between exercises (the chapter is short enough that ch04/ch05's group gates are unnecessary). Each `exercise-NN: approved YYYY-MM-DD` in `ch06_tutorial.md`'s status block must be present before exercise-(NN+1) begins.

## Source provenance

The implementing session re-reads each cited earlier-chapter PDF block byte-exactly during /speckit-implement (per the ch01–ch05 lesson — `chXX-sources.md` files have drifted by single characters). The ch06 PDF page (book p 53) is also re-read byte-exactly to confirm the stub state has not changed between sessions.

## Cross-chapter relationship

Every exercise in ch06 is a typed re-presentation of an earlier chapter's Program. This is documented:
1. In each `.glp` file's header comment block (citing the original chapter, section, page, and Program).
2. In the chapter signpost's plain prose (so a learner who reaches ch06 understands the synthesis).
3. In the top-level tutorial index (a footnote on the ch06 row stating that the chapter's content is synthesised from earlier chapters because the PDF chapter is a stub).

No ambiguity: these are NOT new Programs — they are the named earlier-chapter Programs re-presented under the §6.x headings the author intended.

## REPL infrastructure

Same as ch01–ch05. Build with `dart compile exe glp_runtime/bin/glp_repl.dart --define=GLP_BUILD_COMMIT="$(git log -1 --format='%h %s')" -o glp_runtime/glp_repl.exe`. Dart 3.10.1 at `C:\Users\gavri\dart-sdk\bin\dart.exe`. Implementing session runs the kernel snapshot pattern for batch trace capture.

## Out of scope

- Inventing Programs not present in chapters 1–5.
- Module structure, exported types, type aliases across modules (those start in ch07+).
- The polluted speckit-output `ch06-DEPRECATED-spec.md` (rev-eng input only).
- Any chapter beyond 6.

## What is NOT this file

This file is **not** the speckit feature spec. The spec lives at `specs/007-tutorial-ch06/spec.md` and is produced by `/speckit-specify` from this prompt.
