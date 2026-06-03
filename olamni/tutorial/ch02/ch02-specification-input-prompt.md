# Chapter 2 — specification input prompt

This file is the plain-prose description of what the chapter-2 tutorial must deliver. It is the input you would feed to `/speckit-specify` (or paraphrase to a human implementer) to drive the production of `specs/003-tutorial-ch02/spec.md`. **It deliberately contains no speckit ceremony**: no Feature Branch, no Status, no Constitution headers, no FR-NNN forms, no User Story / Given-When-Then forms. Those are the speckit tool's job to produce; this file's job is to describe what the chapter needs in language a human or an LLM can act on.

## What the chapter delivers

A self-contained, runnable tutorial for chapter 2 of *The Art of Grassroots Logic Programming* (Shapiro, 2025). Chapter 2 is mostly theoretical — transition systems, LP syntax, MGU, linear logic, and the GLP-as-linear-logic-programming correspondence (Formal 2.1, p 14). The only executable code in chapter 2 itself is **Example 2.1 (Append)** on p 10, and that example is **classical Logic Programs**, NOT GLP — it is presented in the book to set up the SRSW contrast that the rest of the book then develops.

Because chapter 2 alone is too thin to anchor a meaningful learner exercise, the tutorial **pairs the chapter-2 classical LP append with the chapter-4 GLP append from §4.2 (book pp 31–32)** as a contrast piece. The pedagogical point of chapter 2 is the LP→GLP transition: same predicate (`append/3`), same structural recursion, but classical LP allows the same variable to occur multiple times (contraction) while GLP forbids it (SRSW, Formal 2.1's "No contraction" row). Showing the two definitions side by side — one that the GLP REPL **rejects** at load time (classical), one that it **accepts and runs** (GLP) — is the most direct way to make the abstract material in §2.1 and §2.2 concrete.

The tutorial also progressively introduces the runtime's **math, system-time, and I/O body kernels** across the three exercises so that, by the time the learner finishes chapter 2, they have seen GLP arithmetic (`:=`), the system clock (`now/1`), and ground-term output (`_output/1`) in addition to the SRSW reader/writer discipline. These kernels are wired up in `glp_runtime/lib/runtime/body_kernels.dart` and exposed as GLP-level procedures by `programs/self.glp`; the tutorial does NOT reimplement them, only USES them.

Chapter 2 is REPL-only. There is no Flutter project, no module structure, no type declarations (those start in chapter 5).

## Files to produce

Under `olamni/tutorial/ch02/`:

- `exercise-01/ch-02-ex-01-classical-append-LP-only.glp` — the two `append/3` clauses **byte-exact from PDF p 10** (Example 2.1). Header comment block paraphrases §2.1 (Logic Programs syntax + operational semantics) and §2.2 (Linear Logic, Formal 2.1) and explicitly flags this file as `% INTENTIONALLY ILL-FORMED FOR GLP — illustrates classical LP contraction`. The GLP REPL is expected to **reject this file at load** with an SRSW-violation message; that rejection IS the demonstration.

- `exercise-01/ch-02-ex-01-glp-append.glp` — the two `append/3` clauses **byte-exact from PDF pp 31–32** (chapter 4, §4.2 "List Reversal — Naive Reverse"), with `?` reader annotations and SRSW-compliant variable pairings. Header comment block names the source (book pp 31–32, ch 4 §4.2) and paraphrases the prose explaining how the `?` annotations turn each variable into a paired writer/reader. One `%%` paraphrase comment per clause maps the variables to their writer/reader roles.

- `exercise-01/ex-01-tutorial.md` — learner-facing step-through guide. Walks through (a) building the REPL, (b) attempting to load the LP-only file and observing the SRSW rejection, (c) loading the GLP file successfully, (d) running the primary demo goal, (e) running three inspection goals exercising different clauses, (f) cross-checking against the captured trace.

- `exercise-01/ex-01-repl-trace.md` — verbatim capture of an actual REPL session run by the implementing Claude on this Windows host. Shows BOTH the failed load of the LP-only file and the successful load + four goals on the GLP file. Format follows the chapter-1 trace contract: 1–3 sentence learner-targeted preface; one fenced ```glp code block per phase; 1–2 brief annotation lines outside each code block; 1–3 sentence learner-targeted postscript.

- `exercise-02/` — variation on ex-01 that **introduces GLP arithmetic** (`:=` and the math body kernels). Files mirror ex-01: one `.glp` plus `ex-02-tutorial.md` plus `ex-02-repl-trace.md`. See "Exercise 02" section below.

- `exercise-03/` — amplification of ex-01 + ex-02 that **introduces system time (`now/1`) and ground-term I/O (`_output/1`)**. Files mirror ex-01: one `.glp` plus `ex-03-tutorial.md` plus `ex-03-repl-trace.md`. See "Exercise 03" section below.

- `ch02_tutorial.md` — chapter signpost. Brief intro to chapter 2's theoretical content and how the tutorial bridges to runnable code via the ch-4 GLP-append import, build instructions, links to each exercise, and the date-stamped per-exercise status block.

Plus, the top-level `olamni/tutorial/tutorial.md` is updated incrementally: chapter 2's row flips from "planned" to "implemented YYYY-MM-DD" once all three exercises are approved. Chapters 3–13 stay marked "planned".

## Source provenance — what comes from where

| Source | PDF page | Book page | Section | What it provides |
|---|---|---|---|---|
| `GLP_ART.pdf` | 22 | 10 | §2.1 (end) | **Example 2.1 (Append)** — classical LP, 2 clauses. Goes verbatim into `ch-02-ex-01-classical-append-LP-only.glp`. |
| `GLP_ART.pdf` | 43–44 | 31–32 | §4.2 "List Reversal — Naive Reverse" | **GLP `append/3`** — 2 clauses, the SRSW-compliant version. Goes verbatim into `ch-02-ex-01-glp-append.glp`. |
| `GLP_ART.pdf` | 26 | 14 | Formal 2.1 | The Linear-Logic ↔ GLP correspondence table. Referenced by both `.glp` files' header comments to motivate the contrast. |
| `glp_runtime/lib/runtime/body_kernels.dart` | — | — | (Dart source) | Authoritative list of arithmetic / math / time / I/O body kernels. ex-02 and ex-03 USE these via the GLP-level procedures defined in `programs/self.glp`; they do NOT reimplement them. |
| `programs/self.glp` | — | — | (root prelude) | GLP-level procedures `:=/2`, `now/1`, `'_output'/1` and the comparison guards. Loaded automatically by the REPL; ex-02 and ex-03 may rely on it being present. |

The implementing Claude session MUST re-read both PDF locations byte-exactly during /speckit-implement (per ch01's "predict-and-verify" lesson — `ch01-sources.md` had a single-character transcription drift that the byte-exact re-read caught).

## Exercise 01 — LP / GLP append contrast (no math / no I/O)

**Variation type**: structural contrast. ex-01 is purely list-shaped — no arithmetic, no system time, no I/O. The point is to make the SRSW reader/writer discipline observable on its own before any other runtime feature is layered on.

**Primary demo goal** for `ch-02-ex-01-glp-append.glp`: `append([1,2,3], [a,b,c], Zs).` The locked binding is `Zs = [1, 2, 3, a, b, c]` (recursive descent on stream 1, base-case forwards stream 2, stream 1 elements precede stream 2 elements in the output). The implementation step empirically verifies this binding by running the goal under the actual REPL on this Windows host; mismatch is a halt-and-report bug — never a silent rewrite of the spec.

**Three inspection goals** (proposed; final selection at /speckit-plan T006 with project-owner approval):

1. `append([], [a,b,c], Zs).` — first list empty. Result: `Zs = [a, b, c]`. Exercises the **base clause** `append([], Ys, Ys?)` — shows that the second argument is forwarded directly via the writer/reader pair `Ys`/`Ys?`.
2. `append([1,2,3], [], Zs).` — second list empty. Result: `Zs = [1, 2, 3]`. Exercises the **recursive clause** terminating into the base — shows the recursion bottoms out cleanly when the first list runs dry.
3. `append([], [], Zs).` — both empty. Result: `Zs = []`. Exercises the **base case alone** — minimal termination behaviour.

These three are chosen so that the four-goal session (primary + three inspections) exercises **both clauses** of the GLP append.

**Expected SRSW rejection trace** for the LP-only file: when the learner attempts to load `ch-02-ex-01-classical-append-LP-only.glp`, the GLP REPL is expected to produce an `Error loading: …` message naming the SRSW violation. The exact error wording is captured verbatim in `ex-01-repl-trace.md`; it MUST NOT be hand-constructed. This rejection is **not a bug** in the tutorial — it is the demonstration. The trace's annotation lines explicitly tell the learner: "you are watching the SRSW analyser do its job; this is the runtime version of what Formal 2.1's 'No contraction' row says about classical LP."

## Exercise 02 — variation introducing GLP arithmetic (`:=`)

**Variation type**: amplification of ex-01 that **adds the GLP arithmetic body kernels** to the same append-shaped problem. The pedagogical point: SRSW lets a downstream consumer compute on a stream concurrently while the producer is still constructing it — the producer/consumer pairing is the same one the learner saw in ex-01, just with arithmetic instead of pure list-shape forwarding.

ex-02 MUST use `:=` arithmetic from `programs/self.glp` (e.g., `Total := Subtotal? + X?`). Acceptable concrete shapes (illustrative, not exhaustive — the implementing session proposes a specific shape during /speckit-plan, and the project owner approves the choice before construction):

- `append_and_sum/4`: append two lists of numbers and concurrently sum the result. The summer reads from the appended stream as it is built; this is the SRSW promise made concrete with numbers.
- `append_with_running_total/4`: each cons cell of the output carries a running total computed via `:=`, demonstrating how arithmetic guards interact with stream construction.
- `length_via_append/3`: defines `length` in terms of `append` plus an arithmetic counter, weaving the new operator into a structural-recursion pattern.

The chosen shape MUST exercise at least one of `+`, `-`, `*`, `/`, `//`, `mod`, or `abs` via the `:=` operator. The math kernels themselves (`'_add'`, `'_sub'`, `_mul`, …) are runtime-private and MUST NOT be called directly from learner-facing code; the tutorial uses `:=` and refers the curious learner to `body_kernels.dart` for the underlying mechanism.

**Primary demo goal**: chosen during /speckit-plan to match the chosen shape; the locked binding is verified empirically against the REPL during /speckit-implement, exactly as in ex-01.

**Three inspection goals**: chosen during /speckit-plan T006 to exercise the new arithmetic predicate's clauses (e.g., empty-list base case, single-element case, and a multi-element case that produces a non-trivial sum).

**Approval gate**: ex-02 is implemented only **after** ex-01 has been thoroughly REPL-tested AND approved. See "Approval gates" section below.

## Exercise 03 — amplification introducing system time (`now/1`) and I/O (`'_output'/1`)

**Variation type**: further amplification of ex-01 + ex-02 that **adds the system clock and ground-term output** on top of the arithmetic introduced in ex-02. The pedagogical point: the same SRSW discipline that governs lists and numbers also governs side-effecting kernels — the timing and printing kernels are body kernels just like `:=`, and they obey the same writer/reader rules.

ex-03 MUST use **both** `now/1` (declared in `self.glp`, returns `Integer` ms-since-epoch via the `_now` body kernel) **and** `'_output'/1` (declared in `self.glp`, prints a ground term via the `_output` body kernel). Acceptable concrete shapes (illustrative, not exhaustive — implementing session proposes, project owner approves):

- `timed_append/3`: capture `now(Start)`, run `append`, capture `now(End)`, compute `Elapsed := End? - Start?` via the arithmetic from ex-02, and emit `'_output'(elapsed_ms(Elapsed?))`. Demonstrates all three new kernels working together.
- `traced_append/3`: as `append` runs, emit `'_output'(saw(X))` for each element of the first list as it is taken, plus a final `'_output'(elapsed_ms(N))`. Demonstrates intra-recursion output.
- `bench_append_and_sum/4`: composes ex-02's `append_and_sum` with timing and output, building a small benchmark.

The chosen shape MUST call `now/1` at least twice (start + end) and MUST call `'_output'/1` at least once with a ground term. The math from ex-02 MUST be reused (typically the elapsed-time subtraction). The body kernels themselves (`_now`, `_output`) are runtime-private and MUST NOT be called directly except via the `self.glp`-level procedures.

**Primary demo goal**: chosen during /speckit-plan to match the chosen shape. Because ex-03 produces side-effects (printed lines), the trace contract is slightly extended: `ex-03-repl-trace.md` MUST show the `_output`-printed lines exactly as they appeared in the REPL, and the elapsed-time value (which is non-deterministic) is documented in the annotation as "varies per run; the SHAPE matters, not the specific number". This is a deliberate exception to the "byte-equality modulo timestamps" rule from ch01 — the elapsed-ms VALUE is itself wallclock-derived and behaves like a timestamp for the purposes of the auditor's reproducibility check.

**Three inspection goals**: chosen during /speckit-plan T006 to exercise the timing + output behaviour (e.g., empty input → near-zero elapsed; large input → larger elapsed; degenerate input → check that `_output` still fires).

**Approval gate**: ex-03 is implemented only **after** ex-02 has been thoroughly REPL-tested AND approved. See "Approval gates" section below.

## Approval gates (procedural, enforced by the implementing session)

Three gates govern the chapter:

1. **ex-02 gate** — `exercise-01: approved YYYY-MM-DD` MUST be present in the `ch02_tutorial.md` status block AND the ex-01 trace MUST cover all the "thoroughly REPL-tested" criteria below. Absent or non-`approved` status blocks ex-02 work.

2. **ex-03 gate** — `exercise-02: approved YYYY-MM-DD` MUST be present in the same status block AND ex-02's trace MUST cover the "thoroughly REPL-tested" criteria. Absent or non-`approved` status blocks ex-03 work.

3. **Variation-shape gates** — the specific concrete shape chosen for ex-02 (which arithmetic predicate) and for ex-03 (which timing + output predicate) MUST be project-owner-approved BEFORE the corresponding `.glp` is written, not after. The implementing session proposes during /speckit-plan; approval lives in the plan-phase decision log (`research.md`) so it is greppable post-hoc.

**"Thoroughly REPL-tested"** means:

- Both `.glp` files in the exercise dir have been loaded in the REPL (or, for the LP-only file in ex-01, the rejection has been captured verbatim).
- The primary demo goal AND all three inspection goals have been run and their bindings captured in `ex-NN-repl-trace.md`.
- Each clause of the exercise's GLP procedure(s) has been exercised by at least one of those four goals.
- For ex-02 and ex-03, every newly-introduced body-kernel-backed operator (`:=`, `now/1`, `'_output'/1`) has been exercised by at least one goal.
- The full trace has been reviewed by the project owner and `exercise-NN: approved YYYY-MM-DD` has been written into the `ch02_tutorial.md` status block.

**Status-block format** (same as ch01):

```
## Exercise status

- exercise-01: <status> [<date>]
- exercise-02: <status> [<date or empty>]
- exercise-03: <status> [<date or empty>]
```

`<status>` ∈ {`approved YYYY-MM-DD`, `pending exercise-N approval`, `not yet implemented`}.

## REPL infrastructure

Same as chapter 1. Use the GLP REPL built from `glp_runtime/bin/glp_repl.dart` in this repo, compiled to a host executable via `dart compile exe ... -o glp_runtime/glp_repl.exe`. The Dart SDK requirement is `^3.9.4`. The compiled binary is gitignored. Building and running the REPL is a one-time setup step the learner does themselves; the tutorial documents it explicitly. The implementing session runs the REPL via the kernel snapshot pattern from the workflow memory (`printf "<path>\n<goal>.\n:quit\n" | dart run glp_runtime/.dart_tool/repl.dill`) for batch trace capture.

## Charter alignment

Chapter 2 is governed by `olamni/tutorial/charter.md`. The relevant charter clauses:

- §1 (REPL-only for chapters 1–6) — chapter 2 is REPL-only.
- §1.5 (every clause carries a `%%` paraphrase comment of the matching paragraph of the book) — applies to BOTH the LP-only and the GLP files in ex-01, and to the new clauses introduced in ex-02 and ex-03.
- design-principles 1–2 (section-driven for chs 1–6; reader on §X.Y loads the matching file) — chapter 2's exercise files are loaded by a reader who has just finished §2.1 + §2.2 and wants to see the LP→GLP transition concretely.

The cross-chapter import from ch 4 §4.2 is **explicitly allowed** by this prompt: chapter 2's own code is too thin, so the tutorial pulls forward the smallest GLP exemplar from ch 4 that makes the contrast direct. The header comment in `ch-02-ex-01-glp-append.glp` documents this provenance ("byte-exact from book pp 31–32, used here in chapter 2 to illustrate the SRSW transition described in §2.2").

The use of math / time / I/O body kernels in ex-02 and ex-03 is also a deliberate forward import — these kernels are first formally introduced later in the book, but the runtime makes them available at any point and they pair naturally with the chapter-2 SRSW theme.

## Out of scope

- Definitions 2.1–2.10 (transition systems, terms, MGU, runs, deductions) — formal-track material per the book's "How to Read This Book" guidance.
- Definitions 2.11–2.12 (structural rules, linear logic) — referenced in `.glp` header comments via Formal 2.1 but not encoded as code.
- Example 2.2 (Resource Interpretation — coffee/dollar) — narrative-only, no code.
- The chapter-4 `reverse/2`, `reverse_acc/3`, and stream-merging definitions BEYOND the GLP `append/3` import — they are not pre-required for ch 2 and stay reserved for ch 4's own tutorial.
- Trigonometric, logarithmic, and exponential body kernels (`sin`, `cos`, `tan`, `asin`, `acos`, `atan`, `exp`, `ln`, `log`, `sqrt`, `pow`, `abs`) — available in the runtime but NOT required for ch 2; ex-02 and ex-03 stick to the four-function arithmetic + comparison guards. The advanced math kernels are reserved for later chapters.
- Time guards `wait/1` and `wait_until/1` — available in `self.glp` but NOT required for ch 2; only `now/1` is in scope.
- Mutual-reference / multi-way-merge kernels (`_allocate_mutual_reference`, `_stream_append`, `_close_mutual_reference`) and madGLP `_send` — out of scope.
- Any chapter beyond 2 (other than the explicit ch-4 GLP-append import).
- The polluted speckit-output `ch02-DEPRECATED-spec.md` — used as reverse-engineering INPUT only; its content is superseded by this prompt and by whatever `/speckit-specify` produces from it.

## What is NOT this file

This file is **not** the speckit feature spec. The feature spec lives at `specs/003-tutorial-ch02/spec.md` and is produced by `/speckit-specify` from this prompt. The two are separate artifacts on purpose: this prompt strips speckit ceremony so it can be written and read in plain language; the spec is the formalised, FR-numbered, user-story-shaped artifact that the rest of the speckit pipeline (`/speckit-clarify`, `/speckit-plan`, `/speckit-tasks`, `/speckit-implement`) consumes.

## Revisions of `ch02-DEPRECATED-spec.md` baked into this prompt

The deprecated spec at `olamni/tutorial/ch02/spec-rev-eng-input/ch02-DEPRECATED-spec.md` is the rev-eng input. Material differences this prompt makes:

- **DEPRECATED**: single file, classical-LP only, no GLP companion. **THIS PROMPT**: two `.glp` files in ex-01 — classical-LP + GLP-from-ch4. Reason: a contrast pair is required for the LP→GLP transition to be observable, not just stated.
- **DEPRECATED**: no exercise-02 or exercise-03; "P2 because the chapter is theoretical". **THIS PROMPT**: three exercises with explicit approval-gate progression and a deliberate body-kernel curriculum (ex-01 lists → ex-02 arithmetic → ex-03 time + I/O). Reason: chapter 2 is the right place to start introducing the math, system-time, and I/O body kernels because the LP→GLP transition theme provides a clean substrate to layer them onto, and three exercises matches the chapter-1 model.
- **DEPRECATED**: speckit-output ceremony (Feature Branch, FR-NNN, User Story, Acceptance Scenarios, Priority headers). **THIS PROMPT**: plain prose, no ceremony. Reason: this file is the input to /speckit-specify, not the output. The speckit-output spec is downstream.
- **DEPRECATED**: "P2" priority and FR-001..FR-006 numbering. **THIS PROMPT**: priorities and FR numbers are downstream — /speckit-specify assigns them based on the project owner's plan; not pre-encoded here.
- **DEPRECATED**: documents only the SRSW rejection as the success criterion. **THIS PROMPT**: documents BOTH the SRSW rejection (LP-only file) AND the success path (GLP file with primary + 3 inspection goals), plus ex-02's arithmetic and ex-03's timing + output as additional success criteria. Reason: a tutorial that only demonstrates failure leaves the learner without a working mental model of GLP — the contrast pair is the minimum unit of pedagogy.
