# Chapter 3 — specification input prompt

This file is the plain-prose description of what the chapter-3 tutorial must deliver. It is the input you would feed to `/buildkit-specify` (or paraphrase to a human implementer) to drive the production of `specs/004-tutorial-ch03/spec.md`. **It deliberately contains no buildkit ceremony**: no Feature Branch, no Status, no Input header, no Constitution header, no Tutorial Mode header, no Clarifications block, no User Story / Priority / Independent Test / Acceptance Scenarios forms, no FR-NNN forms, no Given-When-Then phrasing. Those are the buildkit pipeline's job to produce; this file's job is to describe what the chapter needs in language a human or an LLM can act on.

## What the chapter delivers

A self-contained, runnable tutorial for chapter 3 of *The Art of Grassroots Logic Programming* (Shapiro, 2025). Chapter 3 is "GLP Core" — the formal presentation of the language. Its content is:

- §3.1 (book p 15) — Reader/Writer pairs, the SO Invariant, SRSW, GLP operational semantics, GLP Safety, Monotonicity. Carries one substantial Program — **Program 3.1: GLP Fair Stream Merger**, p 15, three clauses on `merge/3`.
- §3.2 (book pp 21–24) — Guards. Three guard species are introduced: **built-in guards** (`>`, `ground`, `=?=`, etc.), **defined guards** (unit clauses or short procedures that the compiler unfolds at guard sites), and **guard negation** (the `~(...)` form, restricted to negatable guards like `=?=`). The section presents several short inline GLP blocks: `lookup/3` (guard negation, p 22), `channel/1` defined-guard type test plus `process/2` (p 22), the channel-abstraction primitives `send/3` / `receive/3` / `new_channel/2` (p 23), `relay/3` (p 23), `make_pair/2` (p 23), and `bind_response/3` (p 23).

Chapter 3's executable surface area is therefore one anchor Program plus a constellation of short §3.2 guard idioms — not enough on its own to power three substantial exercises in the chapter-1 / chapter-2 mould. To give the tutorial enough material, this prompt **pairs Program 3.1 with a selected GLP exemplar from chapter 4 (Basic Concurrent Programming)** and uses §3.2's three guard species as the **variation curriculum** that distinguishes the three exercises:

- **exercise-01** — built-in guards only. Program 3.1 plus the ch4 cross-chapter exemplar; uses the built-in guards already implicit in those programs (`>` and `ground` from §4.2).
- **exercise-02** — adds **defined guards** from §3.2. A variation of ex-01 that introduces one of §3.2's defined-guard idioms (e.g., `channel/1` as a defined-guard type test, or `lookup/3`'s first clause guarded by `=?=`).
- **exercise-03** — adds **guard negation** from §3.2. A further variation/amplification of ex-01 + ex-02 that introduces the `~(=?=)` form (typically via `lookup/3`'s second clause), or an analogous negation-using guard composition.

This three-step §3.2 curriculum mirrors the body-kernel curriculum used in chapter 2 (ex-01 lists → ex-02 arithmetic → ex-03 time + I/O) and the variable-naming curriculum used in chapter 1 (ex-01 book names → ex-02 semantic names → ex-03 single-letter names). The rationale is the same: a chapter that introduces a new concept axis (here, guard species) is the right place to thread that axis through three concrete exercises.

Chapter 3 is REPL-only. There is no Flutter project, no module structure, no type declarations (those start in chapter 5).

## Files to produce

Under `olamni/tutorial/ch03/`:

- `exercise-01/ch-03-ex-01-glp-fair-stream-merger.glp` — Program 3.1 byte-exact from PDF p 15. Header comment block paraphrases §3.1's prose on Reader/Writer pairs, SO Invariant, SRSW, and the operational-semantics stepping rules. One `%%` paraphrase comment per clause maps the variables to their reader/writer roles.

- `exercise-01/ch-03-ex-01-<ch4-exemplar>.glp` — the chapter-4 cross-chapter exemplar selected during `/buildkit-plan` (see "Cross-chapter exemplar" section below). Byte-exact from its PDF source. Header comment block names the source (book page + section number), explains why it was imported into ch03, and references Program 3.1 as the host-chapter anchor it pairs with. One `%%` paraphrase comment per clause.

- `exercise-01/ex-01-tutorial.md` — learner-facing step-through guide. Walks through (a) building the REPL, (b) loading both `.glp` files, (c) running the primary demo goal that composes Program 3.1 with the ch4 exemplar into a single pipeline, (d) running three inspection goals exercising different clauses, (e) cross-checking against the captured trace.

- `exercise-01/ex-01-repl-trace.md` — verbatim capture of an actual REPL session run by the implementing Claude on this Windows host. Format follows the chapter-1/chapter-2 trace contract: 1–3 sentence learner-targeted preface; one fenced ```glp code block per phase (load + primary goal + three inspection goals = five phases minimum, more if the load step covers two `.glp` files separately); 1–2 brief annotation lines outside each code block; 1–3 sentence learner-targeted postscript.

- `exercise-02/ch-03-ex-02-<short-name>.glp` — variation on ex-01 that **introduces a defined guard from §3.2** (book pp 22–23). The defined-guard mechanism MUST come from one of the §3.2 inline idioms; specific choice is project-owner-approved during `/buildkit-plan`. Per the chapter-2 precedent, ex-02's `.glp` MAY duplicate the relevant ex-01 procedure(s) inline rather than loading ex-01 as a dependency — each exercise dir is self-contained so each exercise's REPL session is reproducible standalone.

- `exercise-02/ex-02-tutorial.md` and `exercise-02/ex-02-repl-trace.md` — same shape as ex-01's, adjusted for the new defined-guard mechanism. The trace's annotation lines explicitly call out the defined-guard machinery: which guard predicate is being unfolded, where the partial-evaluation note from §3.2 applies, and how the defined guard differs from the built-in guards used in ex-01.

- `exercise-03/ch-03-ex-03-<short-name>.glp` — further variation/amplification of ex-01 + ex-02 that **introduces guard negation from §3.2** (the `~(=?=)` form, p 22). The negation-using idiom MUST come from §3.2; specific choice (typically `lookup/3` or an analogous filter) is project-owner-approved during `/buildkit-plan`. Same self-containment rule as ex-02 — duplicate relevant procedures from ex-01 / ex-02 inline rather than loading them as dependencies.

- `exercise-03/ex-03-tutorial.md` and `exercise-03/ex-03-repl-trace.md` — same shape as ex-02's, adjusted for the negation form. The trace's annotation lines explicitly call out which guards are negatable (`=?=` and friends) versus non-negatable, referring the curious learner to §3.2's "SRSW Rules for Defined Guards" table on p 24.

- `ch03_tutorial.md` (with the underscore between `ch03` and `tutorial` — chapter signpost convention from ch01/ch02). Brief intro to chapter 3's role in the book (formal presentation of GLP, guards, the substrate that chapters 4+ build on), build instructions, links to each exercise, and the date-stamped per-exercise status block. The signpost ALSO documents the cross-chapter import in plain prose so a learner who skips the `.glp` headers still encounters the explanation.

Plus, the top-level `olamni/tutorial/tutorial.md` is updated incrementally: chapter 3's row flips from "planned" to "implemented YYYY-MM-DD" once all three exercises are approved. Chapters 4–13 stay marked "planned".

## Source provenance — what comes from where

| Source | PDF page | Book page | Section | What it provides |
|---|---|---|---|---|
| `GLP_ART.pdf` | 27 | 15 | §3.1 | **Program 3.1: GLP Fair Stream Merger** — three clauses on `merge/3`. Goes verbatim into `ch-03-ex-01-glp-fair-stream-merger.glp`. |
| `GLP_ART.pdf` | 34–35 | 22–23 | §3.2 inline | The defined-guard idioms (`channel/1`, `process/2`, `send/3`, `receive/3`, `new_channel/2`, `relay/3`, `make_pair/2`, `bind_response/3`) and the guard-negation idiom (`lookup/3`). Source for the ex-02 defined-guard variation and the ex-03 negation variation. Specific selection per exercise is decided during `/buildkit-plan` and locked in `research.md` with project-owner approval. |
| `GLP_ART.pdf` | 36 | 24 | §3.2 (end) | The "SRSW Rules for Defined Guards" table — referenced by ex-02 and ex-03 trace annotations and by `ch03_tutorial.md`. Not re-encoded as code. |
| `GLP_ART.pdf` | varies (see below) | varies (book p 25 onward) | ch4 §4.1–§4.3 | The cross-chapter GLP exemplar paired into ex-01. Specific selection from the candidate set listed below; locked during `/buildkit-plan` with project-owner approval before construction. |

The implementing Claude session MUST re-read all PDF locations byte-exactly during `/buildkit-implement` (per the chapter-1 / chapter-2 lesson — `chXX-sources.md` files have been observed to drift by single characters from the PDF; the byte-exact re-read catches that drift before it propagates into the `.glp` files).

## Cross-chapter exemplar — selection criteria and candidate set

Chapter 3's own code is one Program plus a few short §3.2 idioms; without an additional anchor, exercise-01 would have only `merge/3` to demonstrate. The chapter-4 cross-chapter import gives ex-01 a meaningful pipeline.

**Selection criteria** (the chosen ch4 exemplar MUST satisfy all of these):

1. **Composes naturally with `merge/3`.** Either the exemplar produces or consumes a stream that `merge/3` can plug into, OR the exemplar is itself a stream operator that can be chained with `merge/3`.
2. **Uses only built-in guards** (`>`, `ground`, `=?=`, comparison operators, ground-test predicates). Defined guards and guard negation are reserved for ex-02 and ex-03 respectively; ex-01 must demonstrate the SRSW reader/writer discipline against built-in guards alone.
3. **Small enough to read in one sitting.** Two to four clauses total; no nested helpers beyond what already appears verbatim in the chosen ch4 sub-section.
4. **Pre-required for nothing later in the tutorial set.** Forward-imports MUST NOT pull in material that ch4's own tutorial (ch4 = `005-tutorial-ch04`) is going to need as its primary pedagogical content.

**Candidate set** (the implementing session proposes one of these during `/buildkit-plan`; the project owner approves the final selection before construction):

- **§4.2.1 + §4.2.2 — `producer/2` + `consumer/3` pair (book p 31).** Recommended starting candidate: a countdown producer guarded by `>` plus a sum consumer guarded by `ground` form a complete pipeline that `merge/3` can sit between. The composed primary goal is something like `producer(5, A), producer(3, B), merge(A?, B?, M), consumer(M?, 0, Sum).` All three components together exercise SRSW reader/writer pairing across four producer/consumer/merger roles. Built-in guards only (`>` from `producer`, `ground` from `consumer`).
- **§4.2.5 — simple fair `merge/3` (book p 32).** Alternate `merge/3` definition (4 clauses, with explicit empty-stream early-exit cases). Pairs with Program 3.1 as a contrast piece — same predicate name, different fairness/termination behaviour. Could be used as a contrast pair (akin to ch02's LP-vs-GLP contrast) but the contrast is SRSW-compatible vs SRSW-compatible rather than rejected vs accepted.
- **§4.2.8 — `distribute/3` broadcast (book p 33).** Stream operator that broadcasts one input stream to multiple consumers; uses the `ground` guard. Could be chained downstream of `merge/3` to broadcast the merged output.
- **§4.1.3 — logic gates `and/3`, `or/3`, `not/2`, `xor/3` (book p 28).** Unit-clause-only constants programs; built-in guards implicit in the ground-matching. Less natural composition with `merge/3` because they don't operate on streams — would require a stream-of-bits framing.

The **recommended** selection is the producer + consumer pair from §4.2.1 + §4.2.2, because it produces the cleanest composed pipeline with `merge/3` and lets the learner observe SRSW reader/writer pairing from end to end. The implementing session is free to propose a different candidate if there is a stronger pedagogical reason; the project owner approves the choice during `/buildkit-plan`.

The header comment block of the imported ch4 `.glp` MUST contain the canonical provenance lines (mirroring the ch02 ch4-import precedent):

```
%% <Predicate name>/<arity> byte-exact from "The Art of Grassroots Logic Programming" (Shapiro, 2025), §<X.Y>, p <book-page>.
%% Imported into ch03 to <one-line reason: e.g., "compose with Program 3.1 into a producer-merger-consumer pipeline that demonstrates SRSW reader/writer pairing across four roles">.
%% This is the only cross-chapter import permitted in ch03 per the spec's Out-of-Scope section.
```

The `ch03_tutorial.md` signpost ALSO documents this import in plain prose.

## Exercise 01 — Program 3.1 + ch4 exemplar (built-in guards only)

**Variation type**: anchor + cross-chapter pairing. ex-01 establishes the two byte-exact source programs (Program 3.1 from ch3 §3.1 + the selected ch4 exemplar) and exercises them together via a composed primary goal. The pedagogical point is the SRSW reader/writer discipline from §3.1 made concrete in a runnable pipeline.

**Primary demo goal**: a single composed goal that exercises BOTH `.glp` files in the same REPL invocation. The exact shape depends on the chosen ch4 exemplar; with the recommended producer + consumer selection it is roughly `producer(5, A), producer(3, B), merge(A?, B?, M), consumer(M?, 0, Sum).` The locked binding (e.g., `Sum = 21` for that goal — five from `[1,2,3,4,5]` plus three from `[1,2,3]` summed) is verified empirically by running the goal under the actual REPL on this Windows host during `/buildkit-implement`; mismatch is a halt-and-report bug — never a silent rewrite of the spec. The locked binding is finalised during `/buildkit-plan` and recorded in `research.md`.

**Three inspection goals** (proposed; final selection at `/buildkit-plan` T006 with project-owner approval):

1. A goal that exercises the **first recursive clause** of `merge/3` (output from first stream).
2. A goal that exercises the **second recursive clause** of `merge/3` (output from second stream).
3. A goal that exercises the **base clause** of `merge/3` (both streams empty) and/or terminates the ch4 exemplar's recursion cleanly.

These three are chosen so that the four-goal session (primary + three inspections) exercises **all three clauses** of Program 3.1 and at least the base + recursive case of the ch4 exemplar. Same exercise-different-clauses pattern as ch01 and ch02.

**Built-in guards in scope**: any guards already implicit in Program 3.1 itself (none beyond unification matching) and any guards in the chosen ch4 exemplar (with the recommended selection: `>` from `producer/2` and `ground` from `consumer/3`). No defined guards, no guard negation — those are reserved for ex-02 and ex-03.

## Exercise 02 — variation introducing defined guards from §3.2

**Variation type**: amplification of ex-01 that **introduces one of §3.2's defined-guard idioms**. The pedagogical point: defined guards are user-extensible — any unit clause or short procedure can become a guard, and the compiler unfolds them at guard sites. Defined guards extend the built-in guard vocabulary while remaining bound by the same SRSW reader-position rules.

ex-02 MUST introduce a defined guard from §3.2 (book pp 22–23). Acceptable concrete shapes (illustrative, not exhaustive — the implementing session proposes a specific shape during `/buildkit-plan`, and the project owner approves the choice before construction):

- **`channel/1` defined-guard type test** (p 22) — wrap one of ex-01's stream variables in a `ch(_, _)` term and guard a clause by `channel(X?)`. The `channel/1` unit clause unfolds at the guard site to a structural match. Demonstrates the simplest defined-guard form: a unit-clause type predicate.
- **`process/2` two-clause dispatch** (p 22) — first clause guarded by `channel(X?)`, second guarded by the built-in `otherwise` for the fallback. Demonstrates defined-guard branching.
- **`lookup/3` first clause only** (p 22) — the `Key? =?= K? | true` form; uses `=?=` as a built-in negatable guard but in its un-negated (positive) position. Demonstrates the equality-test guard without yet invoking negation.
- **A small custom defined guard** that filters or annotates the merger's output stream (e.g., `even_number/1` as a unit clause matching even-valued streams). MUST be derivable directly from §3.2's pattern; not an arbitrary new abstraction.

The chosen shape MUST exercise at least one defined guard at a guard position in at least one clause of the exercise's GLP procedure. The defined-guard machinery itself (compiler unfolding, partial evaluation) is described in §3.2's prose; the tutorial uses the mechanism via concrete code and refers the curious learner to §3.2 for the underlying explanation.

**Primary demo goal**: chosen during `/buildkit-plan` to match the chosen shape; the locked binding is verified empirically against the REPL during `/buildkit-implement`, exactly as in ex-01.

**Three inspection goals**: chosen during `/buildkit-plan` T006 to exercise the new defined-guard predicate's clauses (e.g., a goal that satisfies the defined guard, a goal that fails the defined guard and falls through to the next clause, and a goal that exercises the base case unchanged from ex-01).

**Approval gate**: ex-02 is implemented only **after** ex-01 has been thoroughly REPL-tested AND approved. See "Approval gates" section below.

## Exercise 03 — further variation/amplification introducing guard negation from §3.2

**Variation type**: further amplification of ex-01 + ex-02 that **introduces guard negation** (the `~(...)` form, p 22). The pedagogical point: not all guards are negatable. The §3.2 prose lists `=?=` and a few others as negatable; defined guards and structural matchers are NOT negatable. ex-03 demonstrates the negation form in action and (via the trace's annotation lines) explains why the §3.2 "SRSW Rules for Defined Guards" table on p 24 distinguishes the two.

ex-03 MUST introduce guard negation via the `~(...)` form on a negatable built-in guard. Acceptable concrete shapes (illustrative, not exhaustive — implementing session proposes, project owner approves):

- **`lookup/3` complete (both clauses)** (p 22) — the canonical §3.2 negation example. First clause: `Key? =?= K? | true`. Second clause: `~(Key? =?= K?) | lookup(Key, Rest?, V)`. Demonstrates the positive/negative guard pair on the same equality test, and shows how the recursion descends only when the negative branch fires. Pair with ex-02's defined-guard work and ex-01's merger pipeline by using `lookup/3` on a key/value list derived from the pipeline's output, OR by filtering the pipeline's stream by a key match.
- **A small custom negation-using guard** that filters the merger's output stream by a non-equality criterion expressed via negated `=?=` (e.g., "drop matching tokens" rather than "keep matching tokens"). MUST use the negation form on `=?=` or another §3.2-listed negatable guard; not an arbitrary new abstraction.

The chosen shape MUST use the `~(...)` form at least once in a guard position, and the trace MUST show at least one inspection goal where the negative branch fires (i.e., the negated guard succeeds and selects that clause).

The defined-guard mechanism from ex-02 SHOULD be reused (typically the same defined-guard procedure carries over so the curriculum compounds rather than resets). At minimum, ex-03's `.glp` MUST be loadable in the REPL alongside Program 3.1 and the ch4 exemplar without procedure-redeclaration conflicts.

**Primary demo goal**: chosen during `/buildkit-plan` to match the chosen shape. The locked binding is verified empirically against the REPL during `/buildkit-implement`, exactly as in ex-01 and ex-02.

**Three inspection goals**: chosen during `/buildkit-plan` T006 to exercise the negation form (e.g., a goal where the positive branch matches, a goal where the positive branch fails and the negative branch fires, and a goal where neither branch matches and the procedure fails or terminates).

**Approval gate**: ex-03 is implemented only **after** ex-02 has been thoroughly REPL-tested AND approved. See "Approval gates" section below.

## Approval gates (procedural, enforced by the implementing session)

Three gates govern the chapter:

1. **ex-02 gate** — `exercise-01: approved YYYY-MM-DD` MUST be present in the `ch03_tutorial.md` status block AND the ex-01 trace MUST cover all the "thoroughly REPL-tested" criteria below. Absent or non-`approved` status blocks ex-02 work.

2. **ex-03 gate** — `exercise-02: approved YYYY-MM-DD` MUST be present in the same status block AND ex-02's trace MUST cover the "thoroughly REPL-tested" criteria. Absent or non-`approved` status blocks ex-03 work.

3. **Variation-shape gates** — the specific concrete shape chosen for ex-02 (which §3.2 defined-guard idiom) and for ex-03 (which negation-using idiom), AND the specific ch4 exemplar selected for ex-01, MUST be project-owner-approved BEFORE the corresponding `.glp` is written, not after. The implementing session proposes during `/buildkit-plan`; approval lives in the plan-phase decision log (`research.md`) so it is greppable post-hoc.

**"Thoroughly REPL-tested"** means:

- All `.glp` files in the exercise dir have been loaded in the REPL (for ex-01 that means BOTH Program 3.1 and the ch4 exemplar).
- The primary demo goal AND all three inspection goals have been run and their bindings captured in `ex-NN-repl-trace.md`.
- Each clause of each exercise's GLP procedure(s) has been exercised by at least one of those four goals — counted across BOTH `.glp` files for ex-01.
- For ex-02, the introduced defined guard has been exercised at a guard position by at least one goal.
- For ex-03, the introduced guard negation form has fired (i.e., its negative branch has selected a clause) in at least one inspection goal.
- The full trace has been reviewed by the project owner and `exercise-NN: approved YYYY-MM-DD` has been written into the `ch03_tutorial.md` status block.

**Status-block format** (same as ch01 / ch02):

```
## Exercise status

- exercise-01: <status> [<date>]
- exercise-02: <status> [<date or empty>]
- exercise-03: <status> [<date or empty>]
```

`<status>` ∈ {`approved YYYY-MM-DD`, `pending exercise-N approval`, `pending review`, `not yet implemented`}.

## REPL infrastructure

Same as chapters 1 and 2. Use the GLP REPL built from `glp_runtime/bin/glp_repl.dart` in this repo, compiled to a host executable via `dart compile exe glp_runtime/bin/glp_repl.dart --define=GLP_BUILD_COMMIT="$(git log -1 --format='%h %s')" -o glp_runtime/glp_repl.exe`. The Dart SDK requirement is `^3.9.4`; this Windows host has 3.10.1 at `C:\Users\gavri\dart-sdk\bin\dart.exe`. The compiled binary is gitignored. Building and running the REPL is a one-time setup step the learner does themselves; the tutorial documents it explicitly. The implementing session runs the REPL via the kernel snapshot pattern from the workflow memory (`printf "<path>\n<goal>.\n:quit\n" | dart run glp_runtime/.dart_tool/repl.dill`) for batch trace capture.

The `--define=GLP_BUILD_COMMIT=...` flag is required after the build-provenance fix (branch `claude/fix-misleading-build-line`, expected tag `v2026.04.29-3` once merged). If that branch is not yet merged when ch03 work begins, the implementing session decides whether to merge it first or build without `--define` (the banner will show `Built from: unknown` — a clear signal but not blocking).

The REPL banner reports the resolved root-`self.glp` path, the baked build commit, the binary mtime, and the current repo HEAD with a stale-binary warning when those diverge — all already in place per the v2026.04.29-2 / v2026.04.29-3 fixes documented in the workflow memory.

## Charter alignment

Chapter 3 is governed by `olamni/tutorial/charter.md`. The relevant charter clauses:

- §1 (REPL-only for chapters 1–6) — chapter 3 is REPL-only.
- §1.5 (every clause carries a `%%` paraphrase comment of the matching paragraph of the book) — applies to Program 3.1 in ex-01, the ch4 exemplar's clauses in ex-01, the new clauses introduced in ex-02 and ex-03, AND the duplicated `merge/3` (or other) clauses if ex-02 / ex-03 inline them per the self-containment rule.
- design-principles 1–2 (section-driven for chs 1–6; reader on §X.Y loads the matching file) — chapter 3's exercise files are loaded by a reader who has just finished §3.1 + §3.2 and wants to see the formal GLP semantics + the three guard species concretely.

The cross-chapter import from chapter 4 is **explicitly allowed** by this prompt: chapter 3's own code is one Program plus a few short §3.2 idioms, so the tutorial pulls forward the smallest GLP exemplar from chapter 4 that lets ex-01 exercise a complete pipeline. The header comment in the imported `.glp` documents this provenance and explicitly notes that ch4's own tutorial (ch4 = `005-tutorial-ch04`) will revisit the exemplar in its native chapter context.

## Out of scope

- Definitions 3.1–3.6 (Writers Assignment, Term Matching, GLP Renaming, Reduction, Transition System, Pure Logic Variant) — formal-track material per the book's "How to Read This Book" guidance.
- Propositions 3.7 (Computations are Deductions), 3.8 (SO Invariant), 3.10 (Monotonicity) and Lemma 3.9 (Reader-Instance) — formal-track material; referenced in `.glp` header comments as motivation but not encoded as code.
- **Formal 3.1: Circular Term Semantics** (p 20) — formal-track material; the `Example 3.1: Circular Term Formation` block on p 20 is NOT a runnable demo per charter §1 (formal-track examples).
- The §3.2 "SRSW Rules for Defined Guards" table on p 24 — referenced by ex-02 / ex-03 trace annotations and by `ch03_tutorial.md`, not re-encoded as code.
- §3.3 Exercises — out of scope per charter §1 (end-of-chapter exercises are book material the learner does themselves; they are NOT pre-canned in the tutorial set).
- Worked Examples 1–4 (Success / Suspension / Failure / Writer-to-Writer Failure, pp 18–19) — narrative semantics walkthroughs, not standalone Programs. Encoded as comments inside `ch-03-ex-01-glp-fair-stream-merger.glp` if at all (the implementing session decides during `/buildkit-plan`); NOT promoted to separate `.glp` files.
- §3.2 idioms NOT selected for ex-02 / ex-03 — the channel-abstraction primitives (`send/3`, `receive/3`, `new_channel/2`, `relay/3`, `make_pair/2`, `bind_response/3`) are reserved for chapter 8 (cold-call protocol) where they appear in their native context. They are listed here as candidates for ex-02 / ex-03's defined-guard variation but only ONE of them is selected; the rest stay reserved for ch8.
- Chapter 4 material BEYOND the single selected cross-chapter exemplar — per the chapter-2 precedent, the cross-chapter scope is tight. No "while we're at it" reuse from elsewhere in chapter 4.
- Any chapter beyond 3 (other than the explicit ch4 exemplar import).
- Body kernels introduced in chapter 2 (`:=`, `now/1`, `'_output'/1`) — available in the runtime but NOT required for ch3; all ex-NN goals stick to list/structure manipulation plus guards. The body-kernel curriculum is ch2's territory.
- Type declarations and module structure — those start in chapter 5; ch3 stays REPL-only.
- The polluted buildkit-output `ch03-DEPRECATED-spec.md` — used as reverse-engineering INPUT only; its content is superseded by this prompt and by whatever `/buildkit-specify` produces from it.

## What is NOT this file

This file is **not** the buildkit feature spec. The feature spec lives at `specs/004-tutorial-ch03/spec.md` and is produced by `/buildkit-specify` from this prompt. The two are separate artifacts on purpose: this prompt strips buildkit ceremony so it can be written and read in plain language; the spec is the formalised, FR-numbered, user-story-shaped artifact that the rest of the buildkit pipeline (`/buildkit-clarify`, `/buildkit-plan`, `/buildkit-tasks`, `/buildkit-implement`) consumes.

## Revisions of `ch03-DEPRECATED-spec.md` baked into this prompt

The deprecated spec at `olamni/tutorial/ch03/spec-rev-eng-input/ch03-DEPRECATED-spec.md` is the rev-eng input. Material differences this prompt makes:

- **DEPRECATED**: buildkit-output ceremony (Feature Branch / Created / Status / Input / Constitution / Tutorial Mode headers; Clarifications block; User Story 1/2/3/4 with Priority + Independent Test + Acceptance Scenarios; FR-001..FR-007). **THIS PROMPT**: plain prose, no ceremony. Reason: this file is the input to `/buildkit-specify`, not its output. The buildkit-output spec is downstream.
- **DEPRECATED**: four exercises (ex-01 through ex-04), one per §3.2 idiom (fair-stream-merger, channel-primitives, bind-response, lookup-with-negation). **THIS PROMPT**: three exercises with the ch01/ch02 variation/amplification structure (ex-01 anchor + ch4 exemplar; ex-02 defined-guard variation; ex-03 negation amplification). Reason: scattering §3.2 idioms across separate exercises produces four parallel disconnected mini-demos, each with its own approval gate; the variation/amplification structure produces a curriculum where each exercise builds on the prior, mirroring ch01 and ch02's pedagogy and ch3's own §3.2 progression (built-in → defined → negation).
- **DEPRECATED**: no cross-chapter import; ex-01 is Program 3.1 alone. **THIS PROMPT**: mandates a chapter-4 cross-chapter exemplar paired into ex-01 with the same provenance-header rule used in ch02. Reason: chapter 3's own code is one substantial Program plus short §3.2 inline idioms — too thin for a meaningful runnable pipeline; ch4 §4.2 (or §4.1) supplies the missing producer / consumer / distributor that lets `merge/3` participate in a composed goal.
- **DEPRECATED**: P1 / P2 / P3 priority labels and FR-001..FR-007 numbering. **THIS PROMPT**: priorities and FR numbers are downstream — `/buildkit-specify` assigns them based on the project owner's plan; not pre-encoded here.
- **DEPRECATED**: Worked Examples 1–4 mentioned as "encoded as comments inside ex-01" in FR-004. **THIS PROMPT**: optional — the implementing session decides during `/buildkit-plan` whether the worked examples appear as comments inside ex-01's `.glp` or are documented only in the `ex-01-tutorial.md` prose. Either is acceptable; the choice is project-owner-approved.
- **DEPRECATED**: §3.2 idioms scoped as separate exercises (`channel-primitives.glp`, `bind-response.glp`, `lookup-with-negation.glp`). **THIS PROMPT**: §3.2 idioms scoped as the variation curriculum across ex-02 and ex-03. The channel-abstraction primitives (`send/3`, `receive/3`, `new_channel/2`, `relay/3`, `make_pair/2`, `bind_response/3`) are explicitly REARRANGED from "ex-02 and ex-03 of ch03" to "primary content of ch08 (cold-call protocol)" per their native-chapter context. Reason: those primitives' real pedagogical home is the ch8 cold-call protocol that USES them in a complete agent-to-agent flow; pulling them forward into ch3 as standalone exercises duplicates ch8's eventual content.
- **DEPRECATED**: buildkit-tutorial-mode `cohesive-synthesis` header. **THIS PROMPT**: tutorial mode is a `/buildkit-specify` derived value, not pre-encoded here.
