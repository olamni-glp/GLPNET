# Chapter 5 — specification input prompt

This file is the plain-prose description of what the chapter-5 tutorial must deliver. It is the input you would feed to `/speckit-specify` (or paraphrase to a human implementer) to drive the production of `specs/006-tutorial-ch05/spec.md`. **It deliberately contains no speckit ceremony**: no Feature Branch, no Status, no Input header, no Constitution header, no Tutorial Mode header, no Clarifications block, no User Story / Priority / Independent Test / Acceptance Scenarios forms, no FR-NNN forms, no Given-When-Then phrasing. Those are the speckit pipeline's job to produce; this file's job is to describe what the chapter needs in language a human or an LLM can act on.

## What the chapter delivers

A self-contained, runnable tutorial for chapter 5 of *The Art of Grassroots Logic Programming* (Shapiro, 2025). Chapter 5 is "Types and Modes" — book pp 47–52 (PDF pp 59–64). It is the chapter that **introduces GLP's type system and mode declarations**, which are activated for the first time in the REPL pipeline (the `SRSW → PE → type-check → compile → execute` pipeline's third stage becomes meaningful from this chapter onward). All earlier chapters' code is implicitly typed `Any`; ch05 is where explicit `procedure …(…?, …)` declarations and `T ::= …` type definitions enter the curriculum.

The chapter has approximately 10 distinct PDF code blocks across seven sub-sections — substantially fewer than ch04's ~38 blocks but pedagogically dense because each block introduces a new concept (type definition syntax, recursive type, list type, built-in `Any`, procedure declaration, mode checking, embedded modes, typed flagship Program, type errors, mode errors):

- §5.1 (book p 47) — Type Definitions. 3 code blocks: `Bit ::= 0 ; 1.` (constant enum), `Nat ::= 0 ; s(Nat).` (recursive Peano), `NumList ::= [] ; [Number | NumList].` (typed list cons). Includes Formal 5.1 (Type Definition Syntax — alternation forms: constant, structure, list cons, type ref).
- §5.2 (book p 48) — Built-in Types. 1 code block: `List ::= [] ; [Any | List].` (universal list via the built-in `Any` type) plus prose introducing `Number`, `Any`, `Atom`.
- §5.3 (book p 48) — Moded Procedure Declarations. 1 code block: `procedure merge(List?, List?, List).` (the `?` reader-mark syntax demo, on the universal-`List` form).
- §5.4 (book p 49) — Mode Checking. 1 worked example: typed `merge/3` with `NumList` type + `procedure merge(NumList?, NumList?, NumList).` + 3 clauses (the chapter's worked walk-through of how the mode checker proves head-mode correctness and propagates modes through clause bodies). Includes Formal 5.2 (Mode Semantics — consume vs produce data-flow table).
- §5.5 (book p 50) — Embedded Modes: Response Slots. 1 code block: `CounterMsg ::= … ; show(Number?)` + `CounterStream` + `procedure counter(CounterStream?, Number).` + the `counter([show(State?)|S], State)` clause (introduces consume-mode-inside-produce-mode response-slot construct). Includes Formal 5.3 (Mode Involution — consume × consume = produce).
- §5.6 (book p 51) — Complete Example: Typed Quicksort (the chapter's flagship). 1 large code block: `NumList` type + 3 procedure declarations (`quicksort/2`, `qsort/3`, `partition/4`) + 6 clauses spanning the full sort algorithm.
- §5.7 (book pp 51–52) — Type Errors and Mode Errors. 2 code blocks (negative examples): `foo/1` with type-error trigger (`'a' is not a Number`), `bar/2` with mode-error trigger plus the corrected form `bar(X, Y?) :- Y := X? + 1.`

§5.8 (book p 52) — Summary + Exercises — is **out of scope** per charter §1 (end-of-chapter book exercises are not encoded in tutorial content for chapters 1–6).

The chapter is **REPL-only** per charter §1 (chapters 1–6 are REPL-only). No Flutter project, no module structure. **Type declarations and `procedure` declarations DO appear** for the first time — this is the chapter that introduces them — but they remain confined to per-`.glp`-file declarations consumed by the REPL pipeline; no separate type-system tooling, no module-scoped types, no exported types (those start later).

## Scope decision: 6–12 exercises grouped by sub-section family

The chapter's volume — approximately 10 distinct PDF code blocks across seven sub-sections — is small enough that one-block-per-exercise would land near the lower bound of the project-owner mandate (6–12 exercises). The implementing session MUST aggregate or split blocks as needed to land in the **no less than 6 and no more than 12** range, grouping by **pedagogical sub-section family** so each exercise covers Programs from ONE book sub-section.

The recommended target is **8 exercises** (Option A in the candidate set below). The implementing session is free to propose 6 or 10 if a strong pedagogical reason supports the alternative; the project owner approves the final count during /speckit-clarify.

### Grouping selection criteria

Every candidate grouping MUST satisfy:

1. **Sub-section coherence** — each exercise covers Programs from ONE book sub-section (§5.1, §5.2, §5.3, §5.4, §5.5, §5.6, or §5.7), never mixing across sub-sections in a single exercise. This preserves the chapter's structural pedagogy.
2. **Self-contained REPL session** — each exercise's `.glp` file(s) MUST be loadable in a fresh REPL session and produce the documented primary outcome (a successful binding for positive exercises, a specific error for negative §5.7 exercises) without depending on other exercises' files. Per ch02 Q2 + ch03 R-009 + ch04 self-containment precedent: shared type definitions (e.g., `NumList` reused in §5.4 worked merge AND §5.6 quicksort) are duplicated inline rather than collected in a shared `types.glp` helper file.
3. **Coherent thematic progression within a sub-section** — Programs within an exercise share a pedagogical theme (e.g., "all the type-definition forms in §5.1" or "the corrected-vs-failing mode form in §5.7.2"). Within a sub-section, the simpler block comes before its amplification.
4. **Approval gates at sub-section-group boundaries** — see "Approval gates" section below. The grouping MUST allow gates to land cleanly at meaningful conceptual transitions; no exercise straddles a sub-section boundary.
5. **Per-exercise file count ≤ 2** — by default ONE `.glp` per exercise; up to TWO when a deliberate contrast (analogous to ch02 ex-01's classical-LP-rejected / GLP-accepted pair, or ch05's expected mode-error / corrected-form pair) genuinely needs the second file.
6. **Negative exercises are split per project-owner Q2=B directive** — §5.7's two error categories (type error in §5.7.1, mode error in §5.7.2) MUST land in TWO distinct exercises, not folded into one. This preserves the conceptual distinction between type errors (a value's type does not match the declared type) and mode errors (a clause's reader/writer roles violate the procedure declaration).

### Candidate set (Q1 in /speckit-clarify locks one)

The implementing session proposes one of the following groupings during /speckit-plan; the project owner approves the final selection during /speckit-clarify with the locked exercise list recorded in `spec.md` Clarifications.

**Option A — 8 exercises (recommended)**:

| # | Sub-section | Programs grouped | Theme |
|---|---|---|---|
| ex-01 | §5.1 | 5.1.1 `Bit ::= 0 ; 1.` + 5.1.2 `Nat ::= 0 ; s(Nat).` + 5.1.3 `NumList ::= [] ; [Number \| NumList].` | type definition forms (constant alternation, recursive, list cons) — Formal 5.1 referenced |
| ex-02 | §5.2 | 5.2.1 `List ::= [] ; [Any \| List].` (with `Number` / `Any` / `Atom` prose) | built-in types and the universal `Any` type |
| ex-03 | §5.3 | 5.3.1 `procedure merge(List?, List?, List).` | moded procedure declaration syntax (the `?` reader-mark demo, on universal `List`) |
| ex-04 | §5.4 | 5.4.1 typed `merge/3` worked example | mode-checking flow on the typed `merge/3` — `NumList` type + procedure decl + 3 clauses, with `%%` annotations walking through the head/body mode-check steps; Formal 5.2 referenced |
| ex-05 | §5.5 | 5.5.1 `CounterMsg` + `CounterStream` + typed `counter/2` show clause | embedded modes / response slots — consume-inside-produce; Formal 5.3 referenced |
| ex-06 | §5.6 | Program 5.6 typed quicksort (flagship) | full typed sort algorithm — `NumList` type + `quicksort/2` + `qsort/3` + `partition/4` (3 procedure decls + 6 clauses) |
| ex-07 | §5.7.1 | 5.7.1 `foo/1` type-error illustration | type-error demonstration — load MUST FAIL with `'a' is not a Number` (or equivalent type-mismatch message); two-`.glp` recommended (failing form + corrected form to show what the fix looks like) |
| ex-08 | §5.7.2 | 5.7.2 `bar/2` mode-error illustration + corrected form | mode-error demonstration — load MUST FAIL with mode-mismatch error; two-`.glp` recommended (failing form `bar(X?, Y)` + corrected `bar(X, Y?) :- Y := X? + 1.`) |

**Option B — 10 exercises (split §5.1 type defs each)**:

Same as A but expand:
- ex-01 in Option A → ex-01 (`Bit`) + ex-02 (`Nat`) + ex-03 (`NumList`) — three separate single-type-def exercises so each type form has its own focused trace
- Subsequent exercises shift up: ex-02→ex-04 (`List`/`Any`), ex-03→ex-05 (`procedure` decl), ex-04→ex-06 (worked merge), ex-05→ex-07 (counter), ex-06→ex-08 (quicksort), ex-07→ex-09 (type error), ex-08→ex-10 (mode error)

Option B trades cohesion in §5.1 for deeper per-type-def prose in the trace + tutorial. Recommended only if the project owner judges that ex-01 in Option A would be "too much in one trace" (three type defs is plausibly fine in one trace).

**Option C — 6 exercises (compress foundations)**:

Same as A but compress:
- ex-01 + ex-02 + ex-03 → ex-01 (combine all of §5.1 + §5.2 + §5.3 type-system foundations into one larger exercise — three type defs + universal `List` + procedure decl syntax in a single `.glp`)
- Subsequent exercises shift down: ex-04→ex-02 (worked merge), ex-05→ex-03 (counter), ex-06→ex-04 (quicksort), ex-07→ex-05 (type error), ex-08→ex-06 (mode error)

Option C is for the project owner who wants the §5.4-§5.7 substantive Programs to dominate the chapter's pedagogical weight. Recommended only if §5.1-§5.3 are judged background syntax (which they shouldn't be for a chapter introducing the type system, but the option is documented for completeness).

The implementing session's recommendation during /speckit-plan: **Option A (8 exercises)**. /speckit-clarify locks the chosen option as Q1.

## Approval gates (group-boundary)

Per the project-owner-approved approach inherited from ch04 (Q2=B group-boundary gates rather than ch01–ch03's pairwise gates), ch05 uses **group-boundary approval gates**. The natural sub-section groups for ch05 are coarser than ch04's because ch05 has fewer exercises:

1. **Foundations gate** — ALL exercises covering §5.1 + §5.2 + §5.3 (type-system foundations + procedure-declaration syntax: ex-01 through ex-03 in Option A; ex-01 through ex-05 in Option B; ex-01 only in Option C) MUST be approved before ANY §5.4-onward exercise begins.
2. **Mode-checking-flow gate** — ALL exercises covering §5.4 + §5.5 (worked-example mode checking + embedded modes: ex-04 + ex-05 in Option A; ex-06 + ex-07 in Option B; ex-02 + ex-03 in Option C) MUST be approved before the §5.6 flagship exercise begins.
3. **Flagship gate** — The §5.6 typed-quicksort flagship exercise (ex-06 in Option A; ex-08 in Option B; ex-04 in Option C) MUST be approved before ANY §5.7 negative exercise begins.
4. **Negatives gate (internal)** — The two §5.7 negative exercises (ex-07 + ex-08 in Option A; ex-09 + ex-10 in Option B; ex-05 + ex-06 in Option C) MAY be implemented together within their group; the implementer pauses for the project-owner's group review after both negative exercises are written.

Within a group, exercises are implemented in order but DO NOT require pairwise approval gates between them — the implementer may write all foundations exercises before pausing for the project owner's group review. The plan-then-act discipline of FR-013 (inherited from ch03) still applies within a group: each exercise's `.glp` shape, primary outcome (success binding OR specific error), and inspection-goal selection (where applicable — see "Negative-load-test handling" below) are presented to the project owner at /speckit-implement T006/T007-equivalent before the corresponding files are written.

The status block in `ch05_tutorial.md` therefore EITHER carries one line per exercise (6–10 lines) WITH explicit notes that within-group exercises don't gate each other, OR carries one line per group (4 group lines). The implementing session decides during /speckit-plan; the project owner approves the chosen format.

This grouped-gate pattern is **inherited from ch04**; ch01–ch03's strict pairwise gates are NOT reverted. The /speckit-clarify session may amend this if the project owner prefers strict pairwise gates after seeing the implementation cost; the current spec inherits the ch04 pattern.

## Files to produce

Under `olamni/tutorial/ch05/`:

For each exercise (exercise-NN where NN ∈ 01..08 in Option A):

- `exercise-NN/ch-05-ex-NN-<short-name>.glp` — the GLP source file(s) for this exercise. Filename convention: `ch-05-ex-NN-<short-name>.glp` where `<short-name>` is a hyphenated descriptive label (e.g., `ch-05-ex-01-type-definitions.glp`, `ch-05-ex-06-typed-quicksort.glp`, `ch-05-ex-07-type-error.glp`, `ch-05-ex-08-mode-error.glp`). Most exercises have one `.glp` file; up to two when the §5.7 failing-form / corrected-form contrast is presented.
- `exercise-NN/ex-NN-tutorial.md` — learner-facing step-through guide for this exercise.
- `exercise-NN/ex-NN-repl-trace.md` — verbatim REPL session capture (one fenced ```glp code block per phase, byte-equality contract per FR-014 inherited from ch03; for negative exercises, the captured error message is byte-equality-required modulo wallclock-derived elements).

Plus chapter-level:

- `ch05_tutorial.md` (note **underscore**, per workflow memory file-naming dialect) — chapter signpost. Brief intro to ch05's role in the book (the type-system introduction), build instructions, links to all 6–10 exercises with one-line summaries each, group structure overview, status block per the chosen format (per-exercise or per-group), and explicit documentation of the cross-chapter relationships (ch04 had un-typed `merge/3` + `counter/1`; ch05 introduces typed variants that share the procedure name but differ in arity / signature / mode shape — see "Cross-chapter relationships" below).

Plus, the top-level `olamni/tutorial/tutorial.md` is updated incrementally: chapter 5's row flips from `planned` to `pending review (YYYY-MM-DD)` once any exercise lands and to `implemented YYYY-MM-DD` once all 6–10 exercises are approved. Chapters 6–13 stay marked `planned`.

## Source provenance — what comes from where

The implementing session MUST re-read every PDF code block byte-exactly during /speckit-implement (per the ch01 / ch02 / ch03 / ch04 lesson — `chXX-sources.md` files have been observed to drift by single characters from the PDF; the byte-exact re-read catches that drift before it propagates into the `.glp` files). For ch05 specifically, the byte-exact re-reads are smaller in volume than ch04 (~10 blocks vs ch04's ~38) but each block introduces a NEW concept whose syntax matters precisely (type-def `::=` form, procedure-decl `?`-mark placement, response-slot embedded mode); the implementing session pays close attention to `?` marks, `;` alternation separators, and `|` list-cons separators specifically.

| Source | PDF page range | Book page range | Section | Code blocks |
|---|---|---|---|---|
| `GLP_ART.pdf` | p 59 | p 47 | §5.1 | 5.1.1, 5.1.2, 5.1.3 (3 type defs; lands in ex-01 per Option A) |
| `GLP_ART.pdf` | p 60 | p 48 | §5.2 | 5.2.1 (1 built-in-types block; lands in ex-02 per Option A) |
| `GLP_ART.pdf` | p 60 | p 48 | §5.3 | 5.3.1 (1 procedure-decl syntax block; lands in ex-03 per Option A) |
| `GLP_ART.pdf` | p 61 | p 49 | §5.4 | 5.4.1 typed-merge worked example (NumList + procedure decl + 3 clauses; lands in ex-04 per Option A) |
| `GLP_ART.pdf` | p 62 | p 50 | §5.5 | 5.5.1 counter response-slot (CounterMsg + CounterStream + procedure decl + 1 clause; lands in ex-05 per Option A) |
| `GLP_ART.pdf` | p 63 | p 51 | §5.6 | Program 5.6 typed quicksort (NumList + 3 procedure decls + 6 clauses; lands in ex-06 per Option A) |
| `GLP_ART.pdf` | p 63 | p 51 | §5.7.1 | foo/1 type-error illustration (lands in ex-07 per Option A) |
| `GLP_ART.pdf` | pp 63–64 | pp 51–52 | §5.7.2 | bar/2 mode-error illustration + corrected form (lands in ex-08 per Option A) |
| Formal 5.1 | p 60 | p 48 | §5.1 | Type Definition Syntax — referenced in ex-01 header comments + tutorial; not encoded as code |
| Formal 5.2 | p 61 | p 49 | §5.4 | Mode Semantics — referenced in ex-04 header + tutorial; not encoded as code |
| Formal 5.3 | p 62 | p 50 | §5.5 | Mode Involution — referenced in ex-05 header + tutorial; not encoded as code |
| `ch05-sources.md` | — | — | (existing) | The PDF code-block index — committed in `592d89e3`; should be sanity-checked against PDF byte-exact during /speckit-implement (not authoritative). |

## Cross-chapter relationships — typed variants of earlier procedures

Two procedures from ch04 reappear in ch05 with type declarations:

1. **`merge/3`** — ch04 ex-04 (per ch04 spec) presented an un-typed simple fair `merge/3` (4-clause variant from book §4.2.5, p 32). ch05 §5.4 (ex-04 in Option A) presents a TYPED `merge/3` with `procedure merge(NumList?, NumList?, NumList).` and 3 clauses (the worked-example mode-checking flow). The procedure name is the same; the signature, mode declaration, and clause set differ. The two are NOT byte-identical — they are pedagogically distinct presentations.
2. **`counter/2`** — ch04 ex-06 (per ch04 spec) presented un-typed `counter/1` + `counter_loop/2` (book §4.2.14). ch05 §5.5 (ex-05 in Option A) presents a TYPED `counter/2` with response-slot embedded mode (book §5.5). Different arity, different shape, different pedagogical focus (objects/monitors in ch04 vs response slots / embedded modes in ch05).

The chapter signpost `ch05_tutorial.md` MUST document these relationships in plain prose so a learner who reaches ch05 after working through ch04 understands they are seeing typed variants of familiar procedures, not the same code re-presented. The relevant ch05 exercises' `.glp` headers MUST cross-reference their ch04 untyped predecessors with the canonical provenance line shape established in ch03 R-007.

No cross-chapter import is required for ch05 (its content is self-contained). The cross-chapter import patterns from ch02 (forward import of GLP `append/3` from §4.2) and ch03 (forward import of `producer/2` + `consumer/3` from §4.2.1 + §4.2.2) are **NOT extended in ch05** — ch05 stands on its own type-system content.

## Per-exercise format expectations

Each positive exercise (ex-01 through ex-06 in Option A — the §5.1 through §5.6 exercises) has:

- **One primary demo goal** — a top-level GLP goal that exercises the exercise's main Program(s). Empirically verified during /speckit-implement; mismatch is halt-and-report per ch03 FR-013 (no silent spec rewrite).
- **Three inspection goals** — exercises different clauses or different sub-Programs within the exercise, chosen during /speckit-plan T006-equivalent with project-owner approval. The four-goal session (primary + three inspection) MUST collectively exercise every clause of every Program in the exercise's `.glp` (where applicable — see note below for type-only exercises).
- **Locked binding** for the primary goal AND each inspection goal — proposed during /speckit-clarify or /speckit-plan; verified empirically during /speckit-implement.
- **Strict trace byte-equality contract** modulo REPL banner / build wallclock lines (per ch03 FR-014). No per-run-variation relaxation in ch05 — chapter 5 introduces no new wallclock-derived output (`now/1` and `'_output'/1` from ch02 are explicitly NOT exercised in ch05 tutorial code).

For type-definition-only exercises (ex-01, ex-02 in Option A — §5.1, §5.2): a type definition alone is not a runnable goal. The "primary demo goal" for these exercises is the **load itself** (the file loads successfully and the type definition is registered with the type system). The three inspection goals exercise the type by constructing or matching values against it (e.g., for `Bit`: `bit_test(0).`, `bit_test(1).`, `bit_test(2).` where the third fails the type check — but the failing case is captured as part of the trace, not as an error condition that aborts the session). The implementer proposes a small unit-clause or type-test predicate per exercise to make the type interactively exercisable; the project owner approves during /speckit-plan T006-equivalent.

For the §5.3 procedure-declaration exercise (ex-03 in Option A): the procedure declaration alone is also not a runnable goal. The exercise's `.glp` includes the procedure declaration plus a minimal 1–2-clause body that exercises the declared mode shape (e.g., a stub `merge(L?, R?, M)` clause that demonstrates the `?` reader marks at work without re-implementing the full §5.4 worked merge). The implementer proposes the stub shape during /speckit-plan T006-equivalent.

### Negative-load-test handling (ex-07, ex-08 in Option A — §5.7.1, §5.7.2)

Negative exercises have a different acceptance contract than positive exercises. The key shape:

- **Primary outcome is a specific load-time error**, not a successful binding. The `.glp` file MUST FAIL TO LOAD with a documented type-error or mode-error message. The trace captures the error message as the demonstrated outcome.
- **Two-`.glp`-file pattern recommended** — analogous to ch02 ex-01's classical-LP-rejected / GLP-accepted pair: one `.glp` contains the failing form (the bad code from the PDF), the other contains a corrected form that demonstrates the fix. For §5.7.1 (type error), the corrected form might be `foo/1` re-typed to accept the offending value; for §5.7.2 (mode error), the corrected form is the explicitly cited `bar(X, Y?) :- Y := X? + 1.` from p 51–52. The implementer proposes the exact corrected forms during /speckit-clarify or /speckit-plan.
- **Inspection goals** for negative exercises are minimal or absent — there is no successful binding to inspect. The trace structure for negative exercises MAY have only two phases (load-attempt-of-failing-form + load-of-corrected-form), versus the standard 4–5 phases for positive exercises. The implementer proposes the trace structure during /speckit-plan; the project owner approves.
- **Error-message byte-equality** — the captured error message in the trace MUST be byte-equal to the actual REPL output, modulo any wallclock or per-run-varying segments. If the type-checker produces an error message containing a memory address or a tuple-id, the implementer halts at /speckit-implement T026/T037-equivalent and proposes a per-run-variation relaxation analogous to ch02's FR-014 amendment (`varies per run; the SHAPE matters, not the specific number`). Otherwise full byte-equality holds.
- **Charter §1.5 paraphrase comments** still apply to negative exercises — every clause (even ill-typed ones) carries a `%%` paraphrase comment of the matching paragraph of the book.
- **Documentation in the chapter signpost** — `ch05_tutorial.md` MUST document the negative-exercise contract in plain prose so a learner does not interpret the load failure as a tutorial bug. The signpost explicitly states that ex-07 and ex-08 are *meant* to fail to load with the specified error.

## Literal-source mandate

Per the project owner's directive (inherited from ch04), ch05's source code MUST be transcribed **literally and unsummarised** from the PDF. This means:

1. **Byte-exact code corpus** — every clause of every Program in every exercise's `.glp` file MUST be byte-identical to the corresponding PDF source block. The /speckit-implement verification subtask compares the file's clause text (after stripping the header comment block and the per-clause `%%` paraphrase comments per `contracts/glp-file-format.md` rule 7) against the byte-exact PDF transcription.
2. **No code summarisation, simplification, or "cleaning up"** — even if a Program's PDF form has unusual whitespace, variable naming, or clause ordering, the `.glp` file matches it exactly. This explicitly includes type definitions and procedure declarations: `NumList ::= [] ; [Number | NumList].` is byte-exact from the PDF; the implementer does NOT rearrange the alternation, normalise whitespace around `;` or `|`, or reorder the alternatives. If a PDF transcription appears to have a typo or violation of any analyser rule, the implementing session HALTS per FR-013 and proposes a Clarifications amendment per the ch02 Q3a / ch03 Q4 precedents — never silently corrects.
3. **`%%` paraphrase comments are IN ADDITION to literal code, not REPLACING it** — charter §1.5 mandates one `%%` paraphrase comment per clause. This is the chapter-tutorial standard; the literal-source mandate adds no new constraint here. For the §5.4 worked-example exercise (ex-04 in Option A), the `%%` annotations additionally walk through the head/body mode-check steps from §5.4 prose; this is in ADDITION to the per-clause paraphrase, not in place of it.
4. **Header comment block** — each `.glp` file MUST have a header comment block summarising what the file does, citing the PDF source, and noting any relevant Formal box (Formal 5.1 for ex-01, Formal 5.2 for ex-04, Formal 5.3 for ex-05, per Option A). Multi-Program exercises (ex-04 worked merge, ex-05 counter, ex-06 typed quicksort) carry one header block at the top of the file plus per-Program sub-headers as needed.

The byte-exact rule from ch01 R-001 + ch02 R-001 + ch03 R-001 + ch04's literal-source mandate already establishes per-clause discipline; ch05 inherits this without modification. The reason it is restated here is because ch05 introduces type definitions and procedure declarations whose syntax learners may be tempted to "tidy up" — the literal-source mandate makes the no-tidying rule explicit at the chapter scope.

## REPL infrastructure

Same as chapters 1, 2, 3, and 4. Use the GLP REPL built from `glp_runtime/bin/glp_repl.dart` in this repo, compiled to a host executable via `dart compile exe glp_runtime/bin/glp_repl.dart [--define=GLP_BUILD_COMMIT="$(git log -1 --format='%h %s')"] -o glp_runtime/glp_repl.exe`. The Dart SDK requirement is `^3.9.4`; this Windows host has 3.10.1 at `C:\Users\gavri\dart-sdk\bin\dart.exe`. The compiled binary is gitignored. Building and running the REPL is a one-time setup step the learner does themselves; the tutorial documents it explicitly. The implementing session runs the REPL via the kernel snapshot pattern from the workflow memory (`printf "<path>\n<goal>.\n:quit\n" | dart run glp_runtime/.dart_tool/repl.dill`) for batch trace capture.

The `--define=GLP_BUILD_COMMIT=...` flag is required after the build-provenance fix (branch `claude/fix-misleading-build-line` / tag `v2026.04.29-3` once merged). If that branch is unmerged when ch05 work begins, the implementing session decides whether to merge it first or build without `--define` (the banner shows `Built from: unknown` — clear signal but not blocking).

**Type-checking is now in the live pipeline.** ch05 is the first chapter where the third stage of the REPL pipeline (`type-check`) does meaningful work on the chapter's tutorial code: previous chapters' code was implicitly `Any`-typed and passed type-check trivially; ch05's code carries explicit `T ::= …` definitions and `procedure` declarations that the type-checker validates. The implementing session verifies during /speckit-implement T001-equivalent that the REPL build's type-checker is operational and correctly rejects the §5.7 negative examples before proceeding to ex-07 and ex-08. If the type-checker is in a broken state (which would be a regression — ch05 spec REQUIRES a working type-checker), the implementer halts and reports per FR-013; ch05 work does not proceed against a broken type-checker.

## Known runtime limitations affecting ch05

Per CLAUDE.md "Known REPL Limitations" section:

1. **Structs in lists in REPL goals** — does not affect ch05 directly; the chapter's primary goals (sorting numeric lists, type tests, mode-check walk-throughs) do not use compound terms inside lists in goal arguments. The implementer verifies during /speckit-plan T006-equivalent that the proposed primary + 3 inspection goals do not trip this limitation.
2. **`=..` in clause bodies** — does not affect ch05; no chapter-5 code uses `=..`.
3. **Type-checker-specific limitations** — the implementing session checks CLAUDE.md "Known REPL Limitations" section freshly at /speckit-implement T001-equivalent for any type-checker-specific limitations and updates the chapter spec if any new limitations are found that affect ch05's planned exercises. Specifically: if the type-checker rejects code that the PDF presents as valid (false positive), or accepts code that the §5.7 negative examples require be rejected (false negative), the implementer halts and reports per FR-013.
4. **Negative-example error-message stability** — the type-checker's error messages may evolve across REPL versions. The implementer captures error messages exactly as the current build emits them, locks them into the trace at /speckit-implement T026/T037-equivalent, and notes in the trace's annotation that the message format depends on the REPL build version. If a future REPL build changes the message, the trace becomes inconsistent — the workflow memory should track this as a known maintenance item.

## Charter alignment

Chapter 5 is governed by `olamni/tutorial/charter.md`. The relevant charter clauses:

- §1 (REPL-only for chapters 1–6) — chapter 5 is REPL-only. Type declarations and `procedure` declarations DO appear (this is the chapter that introduces them) but they remain confined to per-`.glp`-file declarations consumed by the REPL pipeline; no Flutter project, no module structure, no separately tooled type system.
- §1.5 (every clause carries a `%%` paraphrase comment of the matching paragraph of the book) — applies to every clause in every `.glp` file in every exercise. Given ch05's smaller volume (~10 substantial blocks across 8 exercises), this represents approximately 20–30 `%%` comments total. The implementing session budgets time accordingly.
- design-principles 1–2 (section-driven for chapters 1–6; reader on §X.Y loads the matching file) — chapter 5's exercise files are loaded by a reader who has just finished §5.1 / §5.2 / … / §5.7 and wants to see each sub-section's Programs concretely. The grouping into 6–10 exercises (rather than separate files for every type definition) is a deliberate compaction; the chapter signpost's "Sources" section MUST cross-reference each Program back to its book sub-section so a learner who wants to find a specific Program can locate which exercise contains it.

## Out of scope

- §5.8 Summary + Exercises — book exercises are out of scope per charter §1 (chapters 1–6 do not encode end-of-chapter book exercises into tutorial content).
- Module structure, exported types, type aliases across modules — those start in chapter 6+; ch05 stays REPL-only with per-`.glp`-file declarations.
- Cross-chapter imports — none required for ch05 (the chapter is self-contained with its own type-system content). The cross-chapter relationship between ch05's typed `merge/3` + `counter/2` and ch04's untyped predecessors is documented as a *relationship* (cross-reference in headers + signpost) but NOT as a code import — each ch05 exercise carries its own byte-exact PDF code, not a copy of ch04's code.
- Body kernels NOT used by the byte-exact PDF Programs in scope — `now/1` and `'_output'/1` (ch02 territory) are NOT used by any §5.1–§5.7 Program. The `:=` arithmetic operator (ch02 territory) IS used in the §5.7.2 corrected `bar/2` form (`Y := X? + 1.`); per ch03 FR-015 amendment precedent, `:=` is permitted in any byte-exact PDF clause that uses it.
- Parser-limited goal forms — handled per the ch04 pattern; not entirely excluded but the implementer chooses goal shapes that avoid known limitations.
- The polluted speckit-output `ch05-DEPRECATED-spec.md` — used as reverse-engineering INPUT only; its content is superseded by this prompt and by whatever `/speckit-specify` produces from it.
- The companion `programs/typed_book/` reference Programs (e.g., `programs/typed_book/recursive/list_processing/quicksort.glp`) — these may be useful as a sanity check but are NOT authoritative; the PDF is the only source of truth. The ch05 `.glp` files are produced from the PDF via byte-exact transcription, not copied from `programs/typed_book/`.
- Any chapter beyond 5. Chapter 6+ extends the type system with module-scoped types and exported types; those are NOT introduced in ch05.

## What is NOT this file

This file is **not** the speckit feature spec. The feature spec lives at `specs/006-tutorial-ch05/spec.md` and is produced by `/speckit-specify` from this prompt. The two are separate artifacts on purpose: this prompt strips speckit ceremony so it can be written and read in plain language; the spec is the formalised, FR-numbered, user-story-shaped artifact that the rest of the speckit pipeline (`/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`, `/speckit-implement`) consumes.

## Revisions of `ch05-DEPRECATED-spec.md` baked into this prompt

The deprecated spec at `olamni/tutorial/ch05/spec-rev-eng-input/ch05-DEPRECATED-spec.md` is the rev-eng input. Material differences this prompt makes:

- **DEPRECATED**: speckit-output ceremony (Feature Branch / Created / Status / Input / Constitution / Tutorial Mode headers; Clarifications block; User Story 1..4 with Priority + Independent Test + Acceptance Scenarios; FR-001..FR-009). **THIS PROMPT**: plain prose, no ceremony. Reason: this file is the input to `/speckit-specify`, not its output.
- **DEPRECATED**: 4 user stories implying 4 exercises (typed quicksort, mode-checked merge, counter response-slot, type-and-mode errors as one combined User Story). **THIS PROMPT**: 6–12 exercises with multi-block-per-exercise grouping by sub-section family per project owner directive. The deprecated spec's 4-exercise count violates the 6-12 mandate; this prompt resolves by splitting the type-system foundations (§5.1–§5.3) into separate exercises and splitting §5.7 into two exercises per project-owner Q2=B directive (one per error kind).
- **DEPRECATED**: User Story 4 folds type errors AND mode errors into one exercise. **THIS PROMPT**: per project-owner Q2=B directive, §5.7.1 (type error) and §5.7.2 (mode error) are TWO distinct exercises (ex-07 and ex-08 in Option A). Reason: the conceptual distinction between type errors (value-vs-type mismatch) and mode errors (reader/writer-role-vs-mode-declaration mismatch) is pedagogically important; folding them obscures the distinction.
- **DEPRECATED**: pairwise approval gate per User Story (4 stories ⇒ 3 pairwise gates implied by the speckit-output approval-state convention). **THIS PROMPT**: group-boundary gates inherited from ch04. 4 gates total (Foundations / Mode-checking-flow / Flagship / Negatives-internal). Reason: ch05's pedagogy is breadth-first within a sub-section group, not progressive amplification across exercises (which is the ch01–ch03 axis that justified pairwise gates); group-boundary gates accelerate the implement phase without sacrificing the project-owner-approval contract at meaningful boundaries.
- **DEPRECATED**: priorities P1/P2/P3 across User Stories. **THIS PROMPT**: priorities are a `/speckit-specify` derived value; not pre-encoded here. /speckit-specify assigns priorities based on group structure (§5.1–§5.3 foundations + §5.4–§5.6 substantive Programs likely P1; §5.7 negatives likely P2 as illustrative).
- **DEPRECATED**: tutorial-mode `cohesive-synthesis` header. **THIS PROMPT**: tutorial mode is a `/speckit-specify` derived value, not pre-encoded here.
- **DEPRECATED**: explicit User Story acceptance test "load `ch05/ch-05-ex-01-typed-quicksort.glp`" naming a specific filename. **THIS PROMPT**: filenames follow the ch01–ch04 convention `ch-05-ex-NN-<short-name>.glp` with `<short-name>` chosen during /speckit-clarify or /speckit-plan based on the locked grouping; not pre-encoded here.
- **DEPRECATED**: implicit reliance on `programs/typed_book/recursive/list_processing/quicksort.glp` as a verification source. **THIS PROMPT**: the PDF is the only authoritative source; `programs/typed_book/` files may be used as sanity checks but are NOT the source of truth. Each ch05 exercise `.glp` is produced from the PDF via byte-exact transcription.
- **DEPRECATED**: silence on cross-chapter relationships. **THIS PROMPT**: explicitly documents `merge/3` (ch04 untyped → ch05 typed) and `counter/2` (ch04 untyped `counter/1` → ch05 typed `counter/2` with response slot) as cross-chapter relationships requiring header cross-references and signpost prose. Reason: a learner who reaches ch05 after ch04 should understand they are seeing typed variants of familiar procedures, not the same code.
- **DEPRECATED**: silence on negative-load-test handling specifics (just states "load MUST fail with the specific errors"). **THIS PROMPT**: documents the negative-load-test contract in detail — two-`.glp`-file pattern recommended, error-message byte-equality, minimal trace structure, charter §1.5 paraphrase comments still apply, signpost documentation. Reason: ch05 surfaces this acceptance shape for the first time in the tutorial set; it needs spec-level handling, not per-test ad-hoc treatment.
- **DEPRECATED**: silence on type-only and procedure-decl-only exercises (the deprecated spec assumes every exercise has a runnable Program). **THIS PROMPT**: documents that ex-01, ex-02, ex-03 in Option A are type-definition-only or procedure-declaration-only exercises whose "primary demo goal" is the load itself, with inspection goals exercising the type or mode shape via small unit-clause helpers proposed during /speckit-plan T006-equivalent. Reason: §5.1, §5.2, §5.3 don't have full Programs in the PDF; the implementer must propose a minimal helper to make each exercise interactively exercisable, and the spec must explicitly authorise this so the implementer doesn't treat the absent goal as a halt-and-report condition.
