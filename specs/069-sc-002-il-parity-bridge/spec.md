<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature Specification: SC-002 IL-parity bridge — shared-grammar parse tree → engine representation lowering + adoption decision

**Feature Branch**: `069-sc-002-il-parity-bridge`  
**Created**: 2026-08-06  
**Status**: Draft  
**Input**: User description: "SC-002 IL-parity bridge: ANTLR parse-tree -> engine-AST lowering + adoption decision"

## Context

The feature-065 shared-grammar spike (`spike/antlr4-glp-grammar/REPORT.md`) proved that a single,
faithful grammar can describe the GLP surface syntax and that the grammar-generated parser reproduces
the production hand-written parser's accept/reject decision on 7/7 of a representative corpus (the
spike's success criterion **SC-001**, coverage parity, 100%). The spike's verdict was
**GO-WITH-CONDITIONS**. The one load-bearing open condition is the spike's success criterion
**SC-002**: demonstrating that both front-ends produce *byte-identical compiled intermediate language
(IL)*, not merely that they accept the same language. That was left unproven because the
grammar-generated parser emits a parse tree, while the shared downstream pipeline
(SRSW → partial-eval → type-check → compile → compiled IL) consumes the engine's own internal
representation. Closing the gap requires a **lowering bridge** from the grammar parse tree to the
engine's internal representation, after which both front-ends feed the identical unchanged downstream
pipeline and their compiled-IL output is compared. This feature builds that bridge, closes the
spike's SC-002 with evidence over an expanded corpus, and delivers a production-adoption decision.

Throughout this document, "SC-002" in the feature title/context refers to the **spike's** success
criterion. This spec's own measurable outcomes are numbered `SC-001…` in the Success Criteria
section below and are independent of that naming.

## Clarifications

### Session 2026-08-06

- Q: What is the concrete coverage floor for the "expanded corpus" (FR-005/SC-002), given "exhaustive" is unmeasurable? → A: The set of accepted `.glp` programs drawn from across the `programs/` tree (e.g. `typed_book/`, `lib/`, `plays/`, `tests/typed/`) that both front-ends already accept, PLUS ≥1 dedicated program per distinct guard, operator, and type-alternative construct enumerated from the shared grammar; coverage is complete when every such construct appears in ≥1 corpus program. (Note: there is no `programs/book/` dir — the book corpus lives under `typed_book/`/`book 2/`.)
- Q: For the `mod`-as-functor tokenization divergence (FR-007), resolve in the grammar or accept as bounded? → A: Attempt the lexer-predicate/island fix first so `mod(...)` call forms tokenize as a functor; only record it as an explicit bounded non-adoption condition if that fix proves infeasible within this feature's scope (production adoption requires `mod(...)` call forms to work).
- Q: What is the stop criterion for the "adversarial fuzzing" (FR-006/SC-003)? → A: A bounded, grammar-driven generative fuzz with a fixed iteration budget (default 10,000 generated inputs) over the prediction-sensitive corners; the gate is zero unexplained IL divergences across the budget, and any divergence halts the run for diagnosis.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Prove IL parity on the representative corpus (Priority: P1)

A GLP compiler maintainer wants objective proof that the shared grammar's front-end and the
production front-end compile the *same* representative program to the *same* intermediate language,
so that the shared grammar can be trusted to preserve behaviour, not just accepted syntax. The
maintainer runs the parity comparison over the 7-file spike corpus and sees, for each file, that the
compiled IL produced via the lowering bridge is byte-identical to the IL produced by the production
front-end.

**Why this priority**: This is the minimum viable proof of the spike's open condition. Without a
lowering bridge that yields identical IL on even the representative corpus, no shared-grammar
adoption can be justified. Delivering only this story already converts the spike's GO-WITH-CONDITIONS
into an evidence-backed result on the corpus that motivated it.

**Independent Test**: Fully testable by lowering each of the 7 spike-corpus files through the bridge,
compiling both front-ends' output through the identical shared pipeline, and comparing the serialized
IL for byte-equality — delivering a per-file MATCH/DIVERGE verdict without any production parser
change.

**Acceptance Scenarios**:

1. **Given** a representative-corpus file that both front-ends accept, **When** it is compiled via the
   lowering bridge and via the production front-end through the identical downstream pipeline, **Then**
   the two serialized IL outputs are byte-identical (MATCH).
2. **Given** a file whose lowered IL diverges from the production IL, **When** the comparison runs,
   **Then** the divergence is reported with enough detail (file + first differing instruction) to
   diagnose it, and it is treated as a defect to resolve — never silently accepted.
3. **Given** the full 7-file spike corpus, **When** the parity run completes, **Then** a reviewable
   per-file result table is produced that can be read without re-running the harness.

---

### User Story 2 - Establish confidence with an expanded corpus and adversarial fuzzing (Priority: P2)

A maintainer weighing production adoption needs assurance that IL parity holds beyond the 7
distinctive constructs the spike exercised. The maintainer expands the parity corpus to cover
book/lib/plays programs and every guard, operator, and type-alternative corner of the language, and
runs adversarial fuzzing focused on the corners the spike flagged as ALL(*)-prediction-dependent
(variable-versus-comparison dispatch and deep type-alternative nesting). Every program either matches
byte-for-byte or has its divergence explained and traced.

**Why this priority**: Corpus breadth is a stated residual risk; 7 files are not exhaustive. Adoption
confidence scales with corpus coverage and with fuzzing the prediction-sensitive corners. This story
is independent of US1's mechanism — it reuses the same parity comparison over a larger input set.

**Independent Test**: Testable by assembling the expanded corpus and fuzz inputs, running the same
per-file parity comparison, and confirming zero unexplained divergences across the whole set.

**Acceptance Scenarios**:

1. **Given** the expanded corpus (accepted `programs/` files across `typed_book/`/`lib/`/`plays/`/
   `tests/typed/` + all guard/operator/type-alt corners), **When** the parity comparison runs, **Then**
   every file yields a MATCH or a divergence traced to a documented, bounded cause.
2. **Given** adversarial fuzz inputs targeting variable-versus-comparison dispatch and deep
   type-alternative nesting, **When** they are compiled through both front-ends, **Then** no
   unexplained IL divergence is produced.
3. **Given** the `mod`-as-functor lexer divergence flagged by the spike, **When** the expanded corpus
   includes `mod(...)` call forms, **Then** the divergence is either resolved in the grammar's
   tokenization or recorded as an explicit, bounded non-adoption condition.

---

### User Story 3 - Deliver an evidence-based production-adoption decision (Priority: P3)

A maintainer (and the language-authority owners) need a single written recommendation on whether to
adopt the shared grammar in production, grounded in the parity evidence and stating every remaining
boundary condition. The decision states adopt / adopt-with-conditions / do-not-adopt, cites the
parity results, and explicitly bounds the claim by the known limits (Dart-target maturity; the shared
grammar not covering the Gleam runtime directly).

**Why this priority**: The decision is the feature's terminal deliverable and depends on the evidence
from US1 and US2. It converts measurements into an actionable, reviewable choice for the language
authority to ratify.

**Independent Test**: Testable by confirming a written decision document exists, cites the parity
results, states a clear verdict, and enumerates the residual boundary conditions — reviewable without
re-running any harness.

**Acceptance Scenarios**:

1. **Given** completed parity evidence from US1 and US2, **When** the decision is authored, **Then** it
   states one of adopt / adopt-with-conditions / do-not-adopt and cites the specific parity results
   supporting it.
2. **Given** the residual risks (Dart-target maturity, Gleam not covered by the shared grammar,
   `mod`-functor tokenization), **When** the decision is authored, **Then** each is explicitly
   addressed as resolved, accepted-as-bounded, or blocking.

---

### Edge Cases

- **Semantic-but-not-syntactic badness**: a program that parses on both front-ends but is rejected
  downstream (e.g. an SRSW / reader-mode violation) must still compare identically up to the point of
  rejection; parity is about the shared pipeline's behaviour, and both front-ends must reach the same
  rejection, not diverge.
- **Tokenization divergence** (`mod` as functor vs. operator): must be surfaced as a first-class,
  bounded finding rather than masked by a corpus that never exercises the call form.
- **Prediction-sensitive corners**: variable-versus-comparison dispatch and deeply nested
  type-alternatives must be fuzzed, not assumed correct from the small corpus.
- **First-divergence reporting**: when IL differs, the comparison must localize the first differing
  instruction rather than only reporting a boolean mismatch.
- **Non-ANTLR runtime**: the Gleam runtime is out of the shared grammar's reach; the decision must not
  overclaim "one grammar, every runtime".

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST provide a lowering bridge that maps every rule of the shared grammar to
  the corresponding node of the engine's internal representation, such that a grammar parse tree is
  transformed into an engine representation the existing downstream pipeline can consume.
- **FR-002**: Both front-ends — the shared-grammar front-end (via the bridge) and the production
  front-end — MUST feed the identical, unchanged downstream pipeline (SRSW → partial-eval →
  type-check → compile). No new downstream capability may be introduced.
- **FR-003**: IL parity MUST be verified example-by-example by comparing the two front-ends' compiled
  IL for byte-identity, using the delivered deterministic IL-serialization as the equality oracle.
- **FR-004**: Each parity comparison MUST yield a per-input verdict of MATCH or DIVERGE, and MUST
  localize any divergence to at least the offending input and its first differing instruction.
- **FR-005**: The parity corpus MUST be expanded beyond the 7 spike files to include accepted `.glp`
  programs drawn from across the `programs/` tree (e.g. `typed_book/`, `lib/`, `plays/`, `tests/typed/`)
  that both front-ends accept, PLUS at least one dedicated program per distinct guard, operator, and
  type-alternative construct enumerated from the shared grammar. Corpus coverage is complete only when
  every such construct appears in at least one corpus program.
- **FR-006**: Variable-versus-comparison dispatch and deep type-alternative nesting MUST be exercised
  by a bounded, grammar-driven generative fuzz (default budget 10,000 generated inputs), not only by
  curated corpus files; any IL divergence MUST halt the fuzz run for diagnosis.
- **FR-007**: The `mod`-as-functor tokenization divergence MUST first be addressed by a
  lexer-predicate/island fix so `mod(...)` call forms tokenize as a functor; only if that fix is
  infeasible within this feature's scope may it be recorded as an explicit, bounded condition that
  constrains adoption.
- **FR-008**: Any IL divergence discovered MUST be diagnosed to root cause and either fixed in the
  bridge or reported as a bounded finding; no divergence may be silently accepted.
- **FR-009**: Every parity result MUST be recorded in a form reviewable without re-running the harness
  (a committed results table/report, mirroring the spike's SC-003 reviewability standard).
- **FR-010**: Production parsers and the accepted GLP surface syntax MUST remain untouched for the
  duration of the feature; the feature decides adoption but does not itself change any production
  front-end. (Carried verbatim from the spike's FR-010.)
- **FR-011**: The feature MUST deliver a written production-adoption decision that states adopt /
  adopt-with-conditions / do-not-adopt, cites the parity evidence, and enumerates the residual
  boundary conditions (Dart-target maturity; Gleam not covered by the shared grammar;
  `mod`-functor tokenization).

### Key Entities *(include if feature involves data)*

- **Grammar parse tree**: the tree the shared-grammar front-end produces from source text; the bridge's
  input.
- **Engine internal representation**: the term/clause representation the production front-end produces
  and the downstream pipeline consumes; the bridge's output.
- **Compiled IL**: the intermediate language emitted by the shared downstream pipeline; the artifact
  whose byte-identity across front-ends defines parity.
- **IL-equality oracle**: the deterministic IL serialization used to compare two compiled-IL outputs
  for byte-identity.
- **Parity corpus**: the set of programs (representative + expanded + fuzz inputs) over which parity is
  measured.
- **Adoption decision**: the terminal recommendation document with verdict, evidence citations, and
  bounded conditions.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the 7-file representative corpus produces byte-identical compiled IL between the
  two front-ends (closes the spike's SC-002 on the corpus that motivated it).
- **SC-002**: 100% of the expanded corpus — accepted `.glp` programs drawn from across `programs/`
  (`typed_book/`, `lib/`, `plays/`, `tests/typed/`, …) plus ≥1 program per distinct
  guard/operator/type-alternative construct of the grammar — produces either byte-identical IL or a
  divergence traced to a documented, bounded cause; zero unexplained divergences.
- **SC-003**: The bounded generative fuzz (default 10,000 inputs) over the prediction-sensitive
  corners (variable-versus-comparison, deep type-alternatives) completes with zero unexplained IL
  divergences.
- **SC-004**: A production-adoption decision exists, states a single verdict, cites the parity
  results, and enumerates every residual boundary condition — reviewable without re-running any
  harness.
- **SC-005**: Zero changes to the accepted GLP surface syntax and zero modifications to any production
  parser land during the feature.
- **SC-006**: Every parity comparison outcome is recorded in a committed, human-readable results
  artifact (per-input MATCH/DIVERGE), so the whole result set is auditable from artifacts alone.

## Assumptions

- The deterministic IL serialization (il-codec) and compiled-IL-on-the-wire capabilities are already
  delivered and are used as the equality oracle and IL source respectively; this feature adds no new
  downstream/engine capability (grounded in REPORT §3, §5).
- "Byte-identical IL" is the parity standard; semantically-equivalent-but-differently-serialized IL
  counts as a divergence to diagnose, not a pass.
- The shared grammar targets the ANTLR-supported languages; the Gleam runtime is explicitly outside
  the "one grammar" claim and is treated as a bounding condition, not a gap to close here.
- The expanded corpus is drawn from the repository's existing `programs/` tree (e.g. `typed_book/`,
  `lib/`, `plays/`, `tests/typed/`; there is no `programs/book/` dir) plus small purpose-built files
  for any guard/operator/type-alt corner not otherwise exercised; complete construct coverage is the
  target, not a fixed file count.
- The production-adoption decision is a recommendation for the language authority (Gabi + Udi) to
  ratify per DISCIPLINE §1.14; this feature produces the evidence and the recommendation, not a
  unilateral production cut-over.
- The shared-grammar front-end and lowering bridge live alongside the spike artifacts and the C#
  engine; C# is the demonstrated target (REPORT §4). Other ANTLR targets remain deferred.

## Dependencies

- Spike artifacts and findings: `spike/antlr4-glp-grammar/` (grammar `Glp.g4`, generated parser
  `gen/`, coverage harness `harness/`, `REPORT.md` §3/§7, `PROPOSAL-1.14.md`).
- Delivered `il-codec` (deterministic IL (de)serialization = equality oracle).
- Delivered compiled-IL-on-the-wire (wave-4 / feature 062; produces/transports compiled IL).
- The unchanged shared downstream pipeline (SRSW → partial-eval → type-check → compile) in the C#
  engine.
