# Chapter 4 — specification input prompt

This file is the plain-prose description of what the chapter-4 tutorial must deliver. It is the input you would feed to `/buildkit-specify` (or paraphrase to a human implementer) to drive the production of `specs/005-tutorial-ch04/spec.md`. **It deliberately contains no buildkit ceremony**: no Feature Branch, no Status, no Input header, no Constitution header, no Tutorial Mode header, no Clarifications block, no User Story / Priority / Independent Test / Acceptance Scenarios forms, no FR-NNN forms, no Given-When-Then phrasing. Those are the buildkit pipeline's job to produce; this file's job is to describe what the chapter needs in language a human or an LLM can act on.

## What the chapter delivers

A self-contained, runnable tutorial for chapter 4 of *The Art of Grassroots Logic Programming* (Shapiro, 2025). Chapter 4 is "Basic Concurrent Programming" — book pp 25–43 (PDF pp 37–55). It is the **largest content chapter so far** (ch01 + ch02 each had 1–2 substantial Programs; ch03 had 1 anchor Program plus short §3.2 inline idioms; ch04 has approximately 38 substantial code blocks across four sub-sections):

- §4.1 (book pp 25–30) — Programming with Constants. 6 substantial code blocks: `p(a)` unit-clause demo, `q(b) / q(a)` multi-clause committed-choice demo, logic gates `and/3` + `or/3` + `not/2` + `xor/3`, `nand/3` first-clause-with-body, `half_adder/4` with `ground` guards on multiple readers, `full_adder/5` compound circuit. Includes Formal 4.1 (Produces and Consumes Parameters, p 29).
- §4.2 (book pp 30–37) — Streams. 15 substantial code blocks: `producer/2` + `consumer/3` (already used as a cross-chapter import in ch03 ex-01 — this is their NATIVE home), `reverse/2` naive, `reverse/2` accumulator + `reverse_acc/3`, `merge/3` simple fair (4-clause variant distinct from ch3's Program 3.1), `dmerge/3` + `dmerger/3` dynamic merge, `merge_tree/2` + `merge_layer/2` static balanced tree, `distribute/3` broadcast, `distribute_indexed/3`, `observer/3`, `adder/4` ripple-carry on streams, `bb/0` + sliding-window buffer, `bb_test/0` terminating buffer variant, `counter/1` + `counter_loop/2` object/monitor, `accumulator/1` + `acc_loop/2` + `client1/1` + `client2/1` + `test_acc/0` monitor with multiple clients. Includes Formal 4.2 (SRSW in Continuation Calls, p 31 — already cited in ch03 R-007 + Q4) and Formal 4.3 (Which Guards Enable Multiple Reader Occurrences, pp 35–36).
- §4.3 (book pp 37–41) — Recursive Programming. 12 substantial code blocks: Peano arithmetic (`plus/3`, `times/3`, `lesseq/2`, `natural_number/1`), integer arithmetic (`double/2`, `average/3`, `abs/2`, `max/3`), `factorial/2` (naive + tail-recursive `fact_acc/3`), `fib/2` (naive + linear `fib_linear/2` + `fib_acc/4`), `flatten/2` + `flatten_acc/3`, `tree_sum/2`, `insertion_sort/2` + `insert/3`, `mergesort/2` + `split2/5` + `merge_sorted/3`, `distribute_ng/3` + `copy/3` + `copy_list/3` non-ground distributor (uses `=..` univ operator), `substitute/4` + `replace/4` tree substitution.
- §4.4 (book pp 41–43) — Metaprogramming. 5 substantial code blocks: `reduce/2` programs-as-data encoding, trust-mode `run/2` minimal meta-interpreter, fail-safe `run/4` MI with success-list output, control `run/5` + `suspended_run/4` with control-stream (suspend / resume / abort), tracing `run/3` + indexed `reduce/3` + `replay/3` deterministic-replay MI.

The chapter is **REPL-only** per charter §1 (chapters 1–6 are REPL-only). No Flutter project, no module structure, no type declarations (those start in chapter 5).

## Scope decision: 10–12 exercises grouped by sub-section family

The chapter's volume — approximately 38 substantial code blocks — cannot be presented as one-Program-per-exercise (which would produce 30+ exercises far above the project-owner-mandated maximum of 12). The implementing session MUST aggregate multiple Programs per exercise, grouping by **pedagogical sub-section family**, into a count of **no less than 6 and no more than 12 exercises** (project-owner directive).

The recommended target is **10 exercises** (Option A in the candidate set below). The implementing session is free to propose 8 or 12 if a strong pedagogical reason supports the alternative; the project owner approves the final count during /buildkit-clarify.

### Grouping selection criteria

Every candidate grouping MUST satisfy:

1. **Sub-section coherence** — each exercise covers Programs from ONE book sub-section (§4.1, §4.2, §4.3, or §4.4), never mixing across sub-sections in a single exercise. This preserves the chapter's structural pedagogy.
2. **Self-contained REPL session** — each exercise's `.glp` file(s) MUST be loadable in a fresh REPL session and produce all primary + 3 inspection goals' bindings without depending on other exercises' files. Per ch02 Q2 + ch03 R-009 self-containment precedent: shared procedures (e.g., `producer/2` + `consumer/3` reused across multiple §4.2 exercises) are duplicated inline rather than collected in a shared `useful-techniques.glp` helper file.
3. **Coherent thematic progression within a sub-section** — Programs within an exercise share a pedagogical theme (e.g., "all the logic gates and their compound-circuit variants" or "all the recursive numeric programs"). Within a sub-section, simpler Programs come before their amplifications (e.g., `factorial` before `factorial_tail_recursive`).
4. **Approval gates at sub-section boundaries** — see "Approval gates" section below. The grouping MUST allow gates to land cleanly at §4.1→§4.2, §4.2→§4.3, §4.3→§4.4 transitions; no exercise straddles a sub-section boundary.
5. **Per-exercise file count ≤ 2** — by default ONE `.glp` per exercise; up to TWO when a deliberate contrast or compose-pair (analogous to ch02 ex-01's classical-LP/GLP pair or ch03 ex-01's Program 3.1 + producer/consumer) genuinely needs the second file.

### Candidate set (Q3=B per project owner; /buildkit-clarify locks one)

The implementing session proposes one of the following groupings during /buildkit-plan; the project owner approves the final selection during /buildkit-clarify with the locked exercise list recorded in `spec.md` Clarifications.

**Option A — 10 exercises (recommended)**:

| # | Sub-section | Programs grouped | Theme |
|---|---|---|---|
| ex-01 | §4.1 | 4.1.1 `p(a)` + 4.1.2 `q(b)/q(a)` + 4.1.3 logic gates `and/3` `or/3` `not/2` `xor/3` | constants + multi-clause + logic gates (unit clauses only) |
| ex-02 | §4.1 | 4.1.4 `nand/3` + 4.1.5 `half_adder/4` + 4.1.6 `full_adder/5` | compound circuits with `ground` guards on multiple readers (Formal 4.1) |
| ex-03 | §4.2 | 4.2.1 `producer/2` + 4.2.2 `consumer/3` + 4.2.3 naive `reverse/2` + 4.2.4 `reverse/2` acc + `reverse_acc/3` | producers / consumers / list reversal (the §4.2 entry point — also reclaims producer/consumer from ch03's cross-chapter forward import as their native home) |
| ex-04 | §4.2 | 4.2.5 simple `merge/3` (4-clause) + 4.2.6 `dmerge/3` + `dmerger/3` + 4.2.7 `merge_tree/2` + `merge_layer/2` | merger variants (simple fair, dynamic, static balanced tree) |
| ex-05 | §4.2 | 4.2.8 `distribute/3` + 4.2.9 `distribute_indexed/3` + 4.2.10 `observer/3` + 4.2.11 `adder/4` ripple-carry | stream operators + first compound stream circuit |
| ex-06 | §4.2 | 4.2.12 `bb/0` sliding-window + 4.2.13 `bb_test/0` + 4.2.14 `counter/1` + `counter_loop/2` + 4.2.15 `accumulator/1` + clients | buffered communication + objects/monitors |
| ex-07 | §4.3 | 4.3.1 Peano + 4.3.2 integer arith + 4.3.3 `factorial/2` + 4.3.4 tail `factorial` + 4.3.5 `fib/2` + 4.3.6 `fib_linear/2` | recursive numerics |
| ex-08 | §4.3 | 4.3.7 `flatten/2` + 4.3.8 `tree_sum/2` + 4.3.9 `insertion_sort/2` + 4.3.10 `mergesort/2` + 4.3.11 `distribute_ng/3` + 4.3.12 `substitute/4` | recursive list / tree processing |
| ex-09 | §4.4 | 4.4.1 `reduce/2` programs-as-data + 4.4.2 trust-mode `run/2` | metaprogramming foundations |
| ex-10 | §4.4 | 4.4.3 fail-safe `run/4` + 4.4.4 control `run/5` + `suspended_run/4` + 4.4.5 tracing `run/3` + indexed `reduce/3` + `replay/3` | advanced meta-interpreters |

**Option B — 12 exercises (finer §4.2 split for digestibility)**:

Same as A but split:
- ex-04 → ex-04a (4.2.5 simple `merge/3`) + ex-04b (4.2.6 `dmerge` + 4.2.7 `merge_tree`)
- ex-05 → ex-05a (4.2.8 + 4.2.9 + 4.2.10 distributors / observer) + ex-05b (4.2.11 ripple-carry)
- ex-06 stays as one (buffered + monitors are tightly coupled pedagogically)

Option B trades coverage breadth per exercise for deeper per-Program prose in the trace + tutorial. Recommended only if the project owner judges that ex-04 / ex-05 in Option A would be "too dense" for a single exercise.

**Option C — 8 exercises (compress §4.3 + §4.4)**:

Same as A but compress:
- ex-07 + ex-08 → ex-07 (combine all of §4.3 into one larger exercise)
- ex-09 + ex-10 → ex-08 (combine all of §4.4 into one)

Option C is for the project owner who wants the §4.1 + §4.2 sections to dominate the chapter's pedagogical weight. Recommended only if §4.3 + §4.4 are judged secondary (which they shouldn't be for a chapter titled "Basic Concurrent Programming," but the option is documented for completeness).

The implementing session's recommendation during /buildkit-plan: **Option A (10 exercises)**. /buildkit-clarify locks the chosen option as Q1.

## Approval gates (group-boundary, per project owner Q2=B)

Per the project owner's Q2=B directive, ch04 uses **group-boundary approval gates**, not pairwise gates. Three gates govern the chapter (regardless of which Option A/B/C is chosen):

1. **§4.1 → §4.2 gate** — ALL §4.1 exercises (ex-01 + ex-02 in Option A; ex-01 + ex-02 in Options B + C) MUST be approved before ANY §4.2 exercise begins.
2. **§4.2 → §4.3 gate** — ALL §4.2 exercises (ex-03 through ex-06 in Option A; ex-03 through ex-07 in Option B; ex-03 through ex-06 in Option C) MUST be approved before ANY §4.3 exercise begins.
3. **§4.3 → §4.4 gate** — ALL §4.3 exercises (ex-07 + ex-08 in Option A; ex-08 + ex-09 in Option B; ex-07 in Option C) MUST be approved before ANY §4.4 exercise begins.

Within a group, exercises are implemented in order but DO NOT require pairwise approval gates between them — the implementer may write all §4.1 exercises (ex-01 + ex-02) before pausing for the project owner's group review. The plan-then-act discipline of FR-013 still applies within a group: each exercise's `.glp` shape, primary goal, and inspection-goal selection are presented to the project owner at /buildkit-implement T006/T007-equivalent before the corresponding files are written. What changes from ch01–ch03 is only the formal status-block-flip-then-continue cycle — a status flip per exercise is replaced by a status flip for the entire group at the gate boundary.

The status block in `ch04_tutorial.md` therefore EITHER carries one line per exercise (10–12 lines) WITH explicit notes that within-group exercises don't gate each other, OR carries one line per group (4 group lines). The implementing session decides during /buildkit-plan; the project owner approves the chosen format.

This grouped-gate pattern is **new for ch04**; ch01–ch03's pairwise gates are NOT inherited unchanged. The /buildkit-clarify session may amend this if the project owner prefers strict pairwise gates after seeing the implementation cost; the current spec inherits Q2=B.

## Files to produce

Under `olamni/tutorial/ch04/`:

For each exercise (exercise-NN where NN ∈ 01..10 in Option A):

- `exercise-NN/ch-04-ex-NN-<short-name>.glp` — the GLP source file(s) for this exercise. Filename convention: `ch-04-ex-NN-<short-name>.glp` where `<short-name>` is a hyphenated descriptive label (e.g., `ch-04-ex-01-constants-and-gates.glp`, `ch-04-ex-04-merge-variants.glp`). Most exercises have one `.glp` file; up to two when a deliberate compose-pair / contrast is pedagogically necessary (analogous to ch02 ex-01's two-`.glp` layout).
- `exercise-NN/ex-NN-tutorial.md` — learner-facing step-through guide for this exercise.
- `exercise-NN/ex-NN-repl-trace.md` — verbatim REPL session capture (one fenced ```glp code block per phase, byte-equality contract per FR-014 inherited from ch03).

Plus chapter-level:

- `ch04_tutorial.md` (note **underscore**, per workflow memory file-naming dialect) — chapter signpost. Brief intro to ch04's role in the book, build instructions, links to all 10–12 exercises with one-line summaries each, group structure overview, status block per the chosen format (per-exercise or per-group), and explicit documentation of the cross-chapter inversion (ch03 ex-01 imported §4.2.1 + §4.2.2 producer/consumer; ch04 ex-03 reclaims them as their native home).

Plus, the top-level `olamni/tutorial/tutorial.md` is updated incrementally: chapter 4's row flips from `planned` to `pending review (YYYY-MM-DD)` once any exercise lands and to `implemented YYYY-MM-DD` once all 10–12 exercises are approved. Chapters 5–13 stay marked `planned`.

## Source provenance — what comes from where

The implementing session MUST re-read every PDF code block byte-exactly during /buildkit-implement (per the ch01 / ch02 / ch03 lesson — `chXX-sources.md` files have been observed to drift by single characters from the PDF; the byte-exact re-read catches that drift before it propagates into the `.glp` files). For ch04 specifically, the byte-exact re-reads are extensive given the volume; the implementing session should plan time for this.

| Source | PDF page range | Book page range | Section | Code blocks |
|---|---|---|---|---|
| `GLP_ART.pdf` | 37–42 | 25–30 | §4.1 | 4.1.1 through 4.1.6 (6 blocks; lands in ex-01 + ex-02 per Option A) |
| `GLP_ART.pdf` | 42–48 | 30–36 | §4.2 | 4.2.1 through 4.2.15 (15 blocks; lands in ex-03 through ex-06 per Option A) |
| `GLP_ART.pdf` | 49–53 | 37–41 | §4.3 | 4.3.1 through 4.3.12 (12 blocks; lands in ex-07 + ex-08 per Option A) |
| `GLP_ART.pdf` | 53–55 | 41–43 | §4.4 | 4.4.1 through 4.4.5 (5 blocks; lands in ex-09 + ex-10 per Option A) |
| Formal 4.1 | p 41 | p 29 | §4.1 | Produces and Consumes Parameters table — referenced in ex-02 header comments + tutorial; not encoded as code |
| Formal 4.2 | p 43 | p 31 | §4.2 | SRSW in Continuation Calls — already cited in ch03 R-007 + Q4; referenced in ex-03 header to remind learners; not encoded as code |
| Formal 4.3 | p 47–48 | p 35–36 | §4.2 | Which Guards Enable Multiple Reader Occurrences — referenced in ex-05 / ex-06 trace annotations where multi-reader guards appear; not encoded as code |
| `ch04-sources.md` | — | — | (existing) | The PDF code-block index — committed in `592d89e3`; should be sanity-checked against PDF byte-exact during /buildkit-implement (not authoritative). |

## Cross-chapter inversion — producer/consumer

Chapter 3's exercise-01 uses `producer/2` + `consumer/3` byte-exact from PDF p 31 (§4.2.1 + §4.2.2) as a cross-chapter forward import — composed with Program 3.1 into a producer-merger-consumer pipeline. Per ch03 spec FR-002 + Clarifications Q1, those two procedures' header in `ch-03-ex-01-producer-consumer.glp` carries the canonical R-007 provenance lines naming ch04 §4.2.1 + §4.2.2 as the source.

Chapter 4 reclaims `producer/2` + `consumer/3` as their **native home**. ex-03 in Option A (the §4.2 entry-point exercise) presents them with the natural §4.2.1 + §4.2.2 prose-paraphrase context (the "Producers and Consumers" subsection's prose plus Formal 4.2 SRSW-in-continuation-calls explanation). The byte-exact code corpus is identical between ch03's import and ch04's native presentation; the difference is the surrounding `%%` paraphrase comments + header block, which in ch03 cite the cross-chapter import provenance and in ch04 paraphrase the §4.2 native prose.

The chapter signpost `ch04_tutorial.md` MUST document this inversion in plain prose so a learner who reaches ch04 after working through ch03 understands that they are revisiting `producer/2` + `consumer/3` in their natural context, not seeing them for the first time.

## Per-exercise format expectations

Each exercise has:

- **One primary demo goal** — a top-level GLP goal that exercises the exercise's main Program(s). Empirically verified during /buildkit-implement; mismatch is halt-and-report per ch03 FR-013 (no silent spec rewrite).
- **Three inspection goals** — exercises different clauses or different sub-Programs within the exercise, chosen during /buildkit-plan T006-equivalent with project-owner approval. The four-goal session (primary + three inspection) MUST collectively exercise every clause of every Program in the exercise's `.glp`.
- **Locked binding** for the primary goal AND each inspection goal — proposed during /buildkit-clarify or /buildkit-plan; verified empirically during /buildkit-implement.
- **Strict trace byte-equality contract** modulo REPL banner / build wallclock lines (per ch03 FR-014). No per-run-variation relaxation in ch04 — chapter 4 introduces no new wallclock-derived output (`now/1` and `'_output'/1` are ch02 territory and explicitly NOT exercised in ch04 tutorial code; the Programs that COULD use them in book §4.2 / §4.3 / §4.4 don't, because the book's examples don't need timing or I/O).

## Literal-source mandate

Per the project owner's directive, ch04's source code MUST be transcribed **literally and unsummarised** from the PDF. This means:

1. **Byte-exact code corpus** — every clause of every Program in every exercise's `.glp` file MUST be byte-identical to the corresponding PDF source block. The /buildkit-implement verification subtask compares the file's clause text (after stripping the header comment block and the per-clause `%%` paraphrase comments per `contracts/glp-file-format.md` rule 7) against the byte-exact PDF transcription.
2. **No code summarisation, simplification, or "cleaning up"** — even if a Program's PDF form has unusual whitespace, variable naming, or clause ordering, the `.glp` file matches it exactly. If a PDF transcription appears to have a typo or SRSW issue, the implementing session HALTS per FR-013 and proposes a Clarifications amendment per the ch02 Q3a / ch03 Q4 precedents — never silently corrects.
3. **`%%` paraphrase comments are IN ADDITION to literal code, not REPLACING it** — charter §1.5 mandates one `%%` paraphrase comment per clause. This is the chapter-tutorial standard; the literal-source mandate adds no new constraint here.
4. **Header comment block** — each `.glp` file MUST have a header comment block summarising what the file does, citing the PDF source, and noting any relevant Formal box. Multi-Program exercises (which is most of ch04) carry one header block at the top of the file plus per-Program sub-headers as needed.

The byte-exact rule from ch01 R-001 + ch02 R-001 + ch03 R-001 already establishes per-clause discipline; the literal-source mandate makes it explicit at the chapter scope given ch04's volume.

## REPL infrastructure

Same as chapters 1, 2, and 3. Use the GLP REPL built from `glp_runtime/bin/glp_repl.dart` in this repo, compiled to a host executable via `dart compile exe glp_runtime/bin/glp_repl.dart [--define=GLP_BUILD_COMMIT="$(git log -1 --format='%h %s')"] -o glp_runtime/glp_repl.exe`. The Dart SDK requirement is `^3.9.4`; this Windows host has 3.10.1 at `C:\Users\gavri\dart-sdk\bin\dart.exe`. The compiled binary is gitignored. Building and running the REPL is a one-time setup step the learner does themselves; the tutorial documents it explicitly. The implementing session runs the REPL via the kernel snapshot pattern from the workflow memory (`printf "<path>\n<goal>.\n:quit\n" | dart run glp_runtime/.dart_tool/repl.dill`) for batch trace capture.

The `--define=GLP_BUILD_COMMIT=...` flag is required after the build-provenance fix (branch `claude/fix-misleading-build-line` / tag `v2026.04.29-3` once merged). If that branch is unmerged when ch04 work begins, the implementing session decides whether to merge it first or build without `--define` (the banner shows `Built from: unknown` — clear signal but not blocking). As of ch03 ship (v2026.04.30), this branch was still unmerged.

## Known runtime limitations affecting ch04

Per CLAUDE.md "Known REPL Limitations" section:

1. **Structs in lists in REPL goals** — the REPL parser cannot parse compound terms (structs) inside lists in goal arguments. Example: `distribute_indexed([send(1,a), send(2,b)], Y, Z).` fails with "Unsupported list head type: StructTerm." This affects ex-05 (Option A) which contains §4.2.9 `distribute_indexed/3`. The implementing session has three options at /buildkit-plan T006-equivalent: (a) choose primary + inspection goals that AVOID structs-in-lists (e.g., `distribute_indexed([], Y, Z).` for the empty-list base case + simulate the recursive case via construction goals); (b) document the parser limitation in the trace as a known caveat and use simpler goal shapes; (c) propose a Clarifications amendment to defer §4.2.9 to a later branch when the parser is fixed. Option (a) or (b) preferred; (c) only if (a) and (b) cannot produce a meaningful 4-goal session.
2. **`=..` in clause bodies** — the parser does not yet recognise `=..` (univ operator) as a valid goal in clause BODIES; it only works in clause HEADS. This affects ex-08 (Option A) which contains §4.3.11 `distribute_ng/3` + `copy/3` + `copy_list/3` (which use `=..`). The implementing session checks this empirically during /buildkit-implement; if `=..` is body-rejected, halt and propose either (a) goal shape that exercises only the HEAD-position usage; (b) document the limitation and skip the affected sub-Program; (c) Clarifications amendment to defer.
3. **Other limitations** — the implementing session checks CLAUDE.md "Known REPL Limitations" section freshly at /buildkit-implement T001-equivalent and updates the chapter spec if any new limitations are found that affect ch04's planned goals.

These limitations were not anticipated for ch01 (one Program, none affected), ch02 (append-only, none affected), or ch03 (small §3.2 idioms, none affected). ch04 is the first chapter where they materially matter. The /buildkit-clarify session may probe this as Q-N if the project owner wants a specific resolution locked before /buildkit-plan.

## Charter alignment

Chapter 4 is governed by `olamni/tutorial/charter.md`. The relevant charter clauses:

- §1 (REPL-only for chapters 1–6) — chapter 4 is REPL-only.
- §1.5 (every clause carries a `%%` paraphrase comment of the matching paragraph of the book) — applies to every clause in every `.glp` file in every exercise. Given ch04's volume (~38 substantial code blocks across 10 exercises), this represents approximately 60–80 `%%` comments total. The implementing session budgets time accordingly.
- design-principles 1–2 (section-driven for chapters 1–6; reader on §X.Y loads the matching file) — chapter 4's exercise files are loaded by a reader who has just finished §4.1 / §4.2 / §4.3 / §4.4 and wants to see each sub-section's Programs concretely. The grouping into 10–12 exercises (rather than ~38 one-Program-per-file files) is a deliberate compaction; the chapter signpost's "Sources" section MUST cross-reference each Program back to its book sub-section so a learner who wants to find a specific Program can locate which exercise contains it.

No cross-chapter imports are required for ch04 (it has its own native content). The cross-chapter import patterns from ch02 (forward import of GLP `append/3` from §4.2) and ch03 (forward import of `producer/2` + `consumer/3` from §4.2.1 + §4.2.2) are **NOT extended in ch04** — ch04 is the largest content chapter and has no need to import from elsewhere.

## Out of scope

- End-of-chapter book exercises (ch04 has none explicit per the deprecated spec; if any are present in the PDF, they are out of scope per charter §1).
- Type declarations and module structure — those start in chapter 5; ch04 stays REPL-only.
- Cross-chapter imports beyond the natural inversion (ch04 reclaims `producer/2` + `consumer/3` as native; it does NOT import from chapters 5+ or from elsewhere).
- Body kernels NOT used by the byte-exact PDF Programs in scope — `now/1` and `'_output'/1` (ch02 territory) are NOT used by any §4.1 / §4.2 / §4.3 / §4.4 Program in the PDF, so they remain entirely out of scope for ch04. The `:=` arithmetic operator (also ch02 territory) IS used extensively in §4.2's `producer/2` recursive clause + `consumer/3` recursive clause + §4.3's arithmetic Programs; per ch03 FR-015 amendment precedent, `:=` is permitted in ch04 in any byte-exact PDF clause that uses it.
- Parser-limited goal forms (per "Known runtime limitations" above) — handled via the per-exercise resolution pattern; not entirely excluded but may be partially demonstrated.
- The polluted buildkit-output `ch04-DEPRECATED-spec.md` — used as reverse-engineering INPUT only; its content is superseded by this prompt and by whatever `/buildkit-specify` produces from it.
- Any chapter beyond 4. Chapter 5+ uses types and modes which are NOT introduced in ch04.

## What is NOT this file

This file is **not** the buildkit feature spec. The feature spec lives at `specs/005-tutorial-ch04/spec.md` and is produced by `/buildkit-specify` from this prompt. The two are separate artifacts on purpose: this prompt strips buildkit ceremony so it can be written and read in plain language; the spec is the formalised, FR-numbered, user-story-shaped artifact that the rest of the buildkit pipeline (`/buildkit-clarify`, `/buildkit-plan`, `/buildkit-tasks`, `/buildkit-implement`) consumes.

## Revisions of `ch04-DEPRECATED-spec.md` baked into this prompt

The deprecated spec at `olamni/tutorial/ch04/spec-rev-eng-input/ch04-DEPRECATED-spec.md` is the rev-eng input. Material differences this prompt makes:

- **DEPRECATED**: buildkit-output ceremony (Feature Branch / Created / Status / Input / Constitution / Tutorial Mode headers; Clarifications block; User Story 1..8 with Priority + Independent Test + Acceptance Scenarios; FR-001..FR-007). **THIS PROMPT**: plain prose, no ceremony. Reason: this file is the input to `/buildkit-specify`, not its output. The buildkit-output spec is downstream.
- **DEPRECATED**: "one `.glp` file per substantial Program" (FR-001) implying ~37 files (≤6 §4.1 + ≤15 §4.2 + ≤12 §4.3 + ≤4 §4.4). **THIS PROMPT**: 10–12 exercises with multi-Program-per-exercise grouping by sub-section family per project owner directive. Reason: ~37 files exceeds the 6–12 mandate; multi-Program grouping with sub-section coherence respects both the volume constraint and the chapter's pedagogical structure.
- **DEPRECATED**: shared `useful-techniques.glp` helpers file collecting `producer`, `consumer`, `merge`, `copy` (FR-002). **THIS PROMPT**: per-exercise self-containment per ch02 Q2 + ch03 R-009 precedent. Each exercise duplicates whatever helpers it needs inline; no shared cross-exercise file. Reason: maintains the SRSW-analyser-sees-one-program-at-a-time discipline; each exercise's REPL session is reproducible standalone; consistent with ch01 / ch02 / ch03 patterns; the duplication is small per exercise (typically 2–4 clauses) and pedagogically zero-cost (each exercise's `%%` paraphrase comments contextualise the duplicated procedures in that exercise's theme).
- **DEPRECATED**: pairwise approval gate per User Story (8 stories ⇒ 7 pairwise gates implied by the buildkit-output approval-state convention). **THIS PROMPT**: group-boundary gates per project owner Q2=B directive. 3 gates total (§4.1→§4.2, §4.2→§4.3, §4.3→§4.4). Reason: ch04's pedagogy is breadth-first within a sub-section, not progressive amplification across exercises (which is the ch01–ch03 axis that justified pairwise gates); group-boundary gates accelerate the implement phase without sacrificing the project-owner-approval contract at meaningful boundaries.
- **DEPRECATED**: priorities P1/P2/P3 across User Stories. **THIS PROMPT**: priorities are a `/buildkit-specify` derived value; not pre-encoded here. /buildkit-specify assigns priorities based on group structure (§4.1 + §4.2 likely P1 as the chapter's foundational content; §4.3 + §4.4 likely P2 as amplifications).
- **DEPRECATED**: tutorial-mode `cohesive-synthesis` header. **THIS PROMPT**: tutorial mode is a `/buildkit-specify` derived value, not pre-encoded here.
- **DEPRECATED**: User Story 3 mentions REPL parser limitation as a "REPL-test caveat" but doesn't propose a resolution. **THIS PROMPT**: the parser limitation is documented in "Known runtime limitations" with explicit per-exercise resolution patterns (avoid-the-form / document-and-skip / Clarifications-amend-defer). Reason: ch04 surfaces these limitations materially for the first time; they need spec-level handling, not per-test ad-hoc workarounds.
- **DEPRECATED**: §4.4 metaprogramming included as User Story 8 P3 (low priority, possibly omitted). **THIS PROMPT**: §4.4 is fully in scope per project owner Q1=A directive. ex-09 + ex-10 (Option A) cover §4.4 in two exercises with full byte-exact PDF transcription. Reason: §4.4 has no natural later-chapter home; deferring it would mean never tutorial-ising it; the project owner judged completeness more valuable than scope reduction.
- **DEPRECATED**: explicit User Story acceptance test "load `ch04/ch-04-ex-01-logic-gates.glp`" naming a specific filename. **THIS PROMPT**: filenames follow the ch01–ch03 convention `ch-04-ex-NN-<short-name>.glp` with `<short-name>` chosen during /buildkit-clarify or /buildkit-plan based on the locked grouping; not pre-encoded here.
- **DEPRECATED**: cross-chapter inversion of `producer/2` + `consumer/3` (already imported into ch03) is not mentioned. **THIS PROMPT**: explicitly documents the inversion in its own section. Reason: the implementing session needs to know that these procedures were already used in ch03 ex-01 to correctly write the ch04 ex-03 header comment and the chapter signpost's plain-prose explanation; the byte-exact code is identical between ch03's import and ch04's native presentation but the surrounding paraphrase context is different.
