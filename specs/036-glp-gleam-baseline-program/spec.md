# Feature Specification: GLP → Gleam/AtomVM Baseline — Research, Verification & Reconfiguration Program

**Feature Branch**: `036-glp-gleam-baseline-program`
**Created**: 2026-06-26
**Status**: Draft
**Input**: User description: "Research, verification & reconfiguration program — run under bk-marathon in two macro-phases (build the machinery; run it, synthesise, gated migration) — to compress the three open epics (REPL/engine-separation, marathon, Gleam-AtomVM) into the shortest VERIFIED roadmap to a strong Gleam/AtomVM GLP: one combined front+back GLP instance faithfully replicating Dart/C# single-instance (M1) and, by linking, multi-instance (M2). Deliverables: realignment of all not-completed features; concerns + opportunities registers; a corpus-verified faithfulness spec with constructed proofs (inspect the GLP corpus directly, never summaries; use the in-repo Lean/SPIN/MLIR armoury); an ANTLR4 parser-integration dossier + an intermediate-language-vs-direct-compile decision, each backed by a spike; a Gleam/AtomVM implementation-strategy dossier; a QHSM/YngeniOS(=NGENIUS) integration design grounded in sibling repos; and two new fully-scored, optimally-sequenced, verified epics — 'Optional features' and 'Full Gleam implementation' — plus an advisory migration plan. Read-only on the target roadmap/specs/code and on all sibling repos until the owner approves; epic migration is the marathon discharge gate."

> **Nature of this feature.** This is a *research, verification, and roadmap-reconfiguration*
> feature, not product code. Its "product" is a set of decision-ready, evidence-backed
> artifacts and a reorganised backlog. Technical terms (Gleam, AtomVM, ANTLR, QHSM, BEAM,
> Lean/SPIN) name the **subject matter under investigation**, not an implementation choice for
> this feature. The full durable definition lives at
> `docs/research/glp-gleam-baseline/feature-definition.md` and is the source of truth.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Owner gets a decision-ready, verified two-epic reconfiguration (Priority: P1)

The owner needs the three sprawling open epics (REPL/engine-separation, marathon, Gleam-AtomVM)
collapsed into exactly two epics — **Optional features** (everything not required for a full
Gleam implementation) and **Full Gleam implementation** (the shortest verified critical path to
M1 then M2) — with every not-completed feature given an evidenced disposition, the Full-Gleam
epic fully scored and in optimal dependency order, and an advisory migration plan. The owner
then approves (or amends) before anything moves.

**Why this priority**: This is the headline outcome. Without it the effort produces analysis
but no actionable, reorganised plan, and the owner cannot decide what to build next.

**Independent Test**: Confirm a synthesis artifact exists that (a) dispositions 100% of the
not-completed sep/marathon/gleam features with cited evidence, (b) presents the two target epics,
(c) shows the Full-Gleam epic as a valid topological order with scores, and (d) maps each
existing feature to one target epic with a re-scope note — all without any live-roadmap mutation.

**Acceptance Scenarios**:

1. **Given** the not-completed feature set, **When** the owner opens the synthesis, **Then**
   each feature carries a disposition (aligned / realign / supersede-by-BEAM / fold / cross-runtime
   / drop) with cited evidence and a target-epic assignment.
2. **Given** the Full-Gleam epic, **When** the owner checks ordering, **Then** no feature depends
   on a later one and each is scored and tied to a faithfulness criterion (US2).
3. **Given** the proposal, **When** the owner withholds approval, **Then** the live roadmap is
   unchanged and migration does not occur.

### User Story 2 - Implementers get a corpus-verified faithfulness specification with proofs (Priority: P1)

An implementer of the Gleam baseline needs an exact, testable definition of what "faithfully
replicates Dart/C#" means at single-instance (M1) and linked-multi-instance (M2) level, grounded
in the GLP corpus inspected **directly** (not summarised), with the load-bearing invariants
backed by **constructed proofs**.

**Why this priority**: "Faithful" is the whole point of the baseline. If faithfulness is asserted
rather than verified, every downstream feature inherits an unproven premise.

**Independent Test**: Confirm a faithfulness specification exists where every M1/M2 criterion
cites a primary source (page or `file:line`) and every load-bearing invariant has a recorded
proof outcome (proved / refuted / open) produced with the in-repo Lean/SPIN/MLIR armoury.

**Acceptance Scenarios**:

1. **Given** a faithfulness criterion, **When** a reviewer checks its provenance, **Then** it
   cites the primary corpus source it was derived from, not a secondary summary.
2. **Given** a load-bearing invariant (e.g. SRSW preservation, unification soundness,
   suspension/reactivation, distributed deref), **When** the reviewer checks it, **Then** a proof
   artifact records its outcome; a refuted or unprovable invariant is surfaced as a faithfulness
   risk, never silently dropped.

### User Story 3 - The parser & intermediate-language strategy is decided with evidence (Priority: P2)

A planner needs a verified answer to two coupled questions: which ANTLR4 integration option is
viable for parsing GLP into the Gleam/BEAM toolchain, and whether the Gleam engine needs an
intermediate language/bytecode or can compile clauses directly — each backed by a working spike,
not a survey.

**Why this priority**: These two decisions size and shape the Full-Gleam epic's front end and
gate several features; deciding them with evidence prevents mis-scoping the critical path.

**Independent Test**: Confirm (a) an ANTLR options dossier with at least one option actually
built/run, and (b) an IL-vs-direct decision supported by a runnable spike for each side.

**Acceptance Scenarios**:

1. **Given** the ANTLR dossier, **When** a reviewer checks the recommended option, **Then** it is
   backed by a built/run artifact, not description alone.
2. **Given** the IL-vs-direct decision, **When** a reviewer checks it, **Then** both a
   direct-compile and an intermediate-language spike exist and the decision cites their results.

### User Story 4 - Implementation & OS-integration strategy is grounded in real sources (Priority: P2)

A planner needs (a) a Gleam/AtomVM implementation-strategy dossier (concurrency mapping of GLP
suspension/reactivation to BEAM processes/messages, heap model, persistence, AtomVM constraints)
and (b) a QHSM/YngeniOS(=NGENIUS) integration design showing how the combined Gleam instance is
packaged as a QHSM and integrated into the YngeniOS microkernel — both grounded in the located
sibling repos, inspected read-only.

**Why this priority**: The baseline must run on AtomVM and ultimately inside YngeniOS; these
designs de-risk the target environment before the Full-Gleam epic is committed.

**Independent Test**: Confirm both dossiers exist, cite the located repos (`qhstate`,
`qhstate-Yngenios`, `MSTACK/docs/diana`, `olamnit`) and the AtomVM/Gleam constraints, and give a
concrete packaging design — with zero writes to any sibling repo.

**Acceptance Scenarios**:

1. **Given** the impl-strategy dossier, **When** a reviewer checks a claim about AtomVM limits or
   the concurrency mapping, **Then** it is grounded in a cited source (on-disk or re-grounded web).
2. **Given** the integration design, **When** a reviewer checks it, **Then** it names how the
   front+back instance becomes a QHSM and how it slots into YngeniOS, citing the sibling repos.

### User Story 5 - Concerns and opportunities are exhaustively surfaced (Priority: P3)

A reviewer needs a concerns register (risks, blockers, faithfulness gaps, AtomVM limits, scope
traps) and an opportunities register (what becomes possible/cheaper on BEAM/AtomVM), each driven
to exhaustion rather than a first-pass list.

**Why this priority**: These registers feed the dispositions and ordering; they raise quality but
rank below the core synthesis and verification.

**Independent Test**: Confirm both registers exist, each item carries evidence, and the discovery
ran loop-until-dry (consecutive empty rounds recorded).

**Acceptance Scenarios**:

1. **Given** the concerns register, **When** a reviewer samples an item, **Then** it has evidence
   and a severity, and links to any feature it affects.
2. **Given** the opportunities register, **When** a reviewer samples an item, **Then** it names
   the BEAM/AtomVM capability and what it lets the design delete or simplify.

### Edge Cases

- **Corpus contradicts an existing dossier/claim.** The primary source wins; the corrected
  reality is recorded and the dependent disposition is revised.
- **A feature resists clean disposition.** It is recorded as "needs owner decision" with the
  competing options and consequences — never force-fit.
- **A load-bearing invariant is refuted or unprovable.** It is surfaced as a first-class
  faithfulness risk affecting the relevant feature, not omitted.
- **ANTLR has no viable Gleam/BEAM target, or AtomVM cannot support a required mechanism.** The
  blocker is recorded with evidence and the affected feature is re-scoped or moved to Optional.
- **A sibling repo is inaccessible or ambiguous.** The gap is reported; the affected design is
  marked provisional rather than fabricated.
- **The owner rejects or amends the reconfiguration.** No migration occurs; the proposal is
  revised. Migration is strictly the post-approval discharge action.
- **Interruption / compaction / crash mid-run.** Work resumes from the last durable checkpoint
  with no lost or duplicated pipeline output.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The program MUST produce an evidenced **disposition** for every not-completed
  feature across the three epics (REPL/engine-separation, marathon, Gleam-AtomVM), classifying
  each as aligned / realign / supersede-by-BEAM / fold-into-Gleam / keep-cross-runtime / drop.
- **FR-002**: The program MUST produce a **concerns register** and an **opportunities register**,
  each item carrying cited evidence, driven loop-until-dry.
- **FR-003**: The program MUST produce a **faithfulness specification** for M1 (single combined
  instance parity) and M2 (linked multi-instance parity), every criterion derived from **direct
  inspection** of the GLP corpus (citing page or `file:line`) — corpus material MUST NOT be
  replaced by a memorised or second-hand summary as the basis of a criterion.
- **FR-004**: The program MUST **construct proofs** for the load-bearing faithfulness invariants
  using the in-repo Lean/SPIN/MLIR armoury, and MUST record each invariant's outcome (proved /
  refuted / open); a refuted or unprovable invariant MUST be surfaced as a faithfulness risk.
- **FR-005**: The program MUST deliver an **ANTLR4 integration options dossier** with at least one
  option **verified by being built/run**, and a decision on **intermediate-language vs
  direct-compile** supported by a **runnable spike for each side**.
- **FR-006**: The program MUST deliver a **Gleam/AtomVM implementation-strategy dossier** covering
  the GLP→BEAM concurrency mapping (suspension/reactivation), heap model, persistence, and AtomVM
  constraints (incl. absence of `gleam_otp` on AtomVM), each material claim cited.
- **FR-007**: The program MUST deliver a **QHSM/YngeniOS(=NGENIUS) integration design** showing how
  the combined Gleam front+back instance is packaged as a QHSM and integrated into the YngeniOS
  microkernel, grounded in the located sibling repos.
- **FR-008**: The program MUST synthesise the above into **two target epics** — *Optional features*
  and *Full Gleam implementation* — where the Full-Gleam epic is a **valid topological order**
  (no forward dependency), **fully scored**, and each feature is **tied to ≥1 faithfulness
  criterion**.
- **FR-009**: The program MUST produce an **advisory migration plan** mapping each existing
  not-completed feature to exactly one target epic, with a re-scope note where applicable.
- **FR-010**: The program MUST be **read-only** with respect to the target roadmap, specs, and
  code, and **read-only** with respect to all sibling repos (`GLP`, `qhstate`, `qhstate-Yngenios`,
  `MSTACK`, `olamnit`), until the owner approves the synthesised plan.
- **FR-011**: The actual **epic migration** (creating the two epics and moving features) MUST occur
  only **after explicit owner approval**, as the marathon **discharge gate** — never as part of
  normal execution.
- **FR-012**: The program MUST run under **bk-marathon** in two macro-phases — **Phase A: build the
  machinery** (stand up the research pipelines as resumable units + shared corpus index + proof
  harness) and **Phase B: run the machinery** (execute, accumulate verified artifacts, synthesise)
  — and MUST be **durable and restart-safe** (resume from the last checkpoint after interruption).
- **FR-013**: The program MUST use the **multi-agent, multi-stage method** (several independent
  design approaches → adversarial review → deep sub-agent analysis → synthesis) with adversarial
  verification of findings.
- **FR-014**: The program MAY use **web research** where on-disk material is insufficient, and
  MUST re-ground any web finding against a primary source before it informs a decision.
- **FR-015**: Every analytical claim and every recommendation MUST carry **cited evidence**; every
  Full-Gleam feature MUST carry a **score** and a **sequence position**.
- **FR-016**: All produced artifacts MUST be written under `docs/research/glp-gleam-baseline/` so
  the synthesis is locatable and reviewable as a unit.

### Key Entities

- **Feature Disposition**: a verdict per existing feature (classification, target epic,
  contribution, improvement, evidence, sequence position).
- **Concern / Opportunity**: a risk/blocker/gap, or a BEAM/AtomVM-enabled simplification, each with
  evidence, severity/value, and affected features.
- **Faithfulness Criterion**: a testable M1/M2 parity requirement with a primary-source citation.
- **Proof Artifact**: a Lean/SPIN/MLIR (or equivalent) construction recording an invariant's
  outcome (proved / refuted / open).
- **ANTLR Option / IL-vs-Direct Decision**: parser-integration choices, each with a built/run spike.
- **Implementation-Strategy Dossier / Integration Design**: the Gleam-AtomVM and QHSM/YngeniOS
  designs, grounded in cited sources.
- **Target Epic**: *Optional features* or *Full Gleam implementation* (the latter scored + ordered).
- **Migration Mapping**: existing-feature → target-epic assignment with a re-scope note.
- **Research Pipeline / Marathon Run**: the durable machinery and its restart-safe run state.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of not-completed features across the three epics have an evidenced disposition
  and a target-epic assignment.
- **SC-002**: The Full-Gleam epic is a valid topological order (zero forward dependencies), 100% of
  its features are scored, and 100% are linked to at least one faithfulness criterion.
- **SC-003**: 100% of M1/M2 faithfulness criteria cite a primary corpus source (page or
  `file:line`); none rests solely on a summary.
- **SC-004**: 100% of load-bearing faithfulness invariants have a recorded proof outcome
  (proved / refuted / open); none is silently skipped.
- **SC-005**: At least one ANTLR integration option is verified by a built/run artifact, and the
  IL-vs-direct decision cites results from a runnable spike on each side.
- **SC-006**: The Gleam/AtomVM impl-strategy dossier and the QHSM/YngeniOS integration design each
  cite the located sources and contain a concrete design (no fabricated claims).
- **SC-007**: Zero mutations to the target roadmap, specs, or code, and zero writes to any sibling
  repo, occur before owner approval.
- **SC-008**: After an induced interruption, the program resumes from the last durable checkpoint
  with no lost or duplicated pipeline output.
- **SC-009**: The owner can reach an approve/amend decision on the reconfiguration from the
  synthesis artifacts alone, without re-deriving the analysis from source.

## Assumptions

- Multi-agent orchestration and the bk-marathon harness are available, and the owner has approved
  using them for this program.
- The GLP corpus and sibling repos exist at the paths located 2026-06-26 (recorded in
  `docs/research/glp-gleam-baseline/feature-definition.md`).
- "Faithful" means identical observable execution semantics (single instance) and identical
  linked-distribution semantics (multi-instance) versus the Dart and C# implementations.
- The unit of delivery the roadmap targets is **one combined front+back GLP instance** with
  **link capability**, matching how Dart/C# are deployed today.
- The existing engine-separation dossier, link-layer corpus, and Gleam-AtomVM dossier are valid
  inputs, re-verified against primary sources where load-bearing.
- "YngeniOS", "NGENIUS", and "Yngenios" denote the same target operating system.
- Owner approval is required before any reconfiguration is applied to the live roadmap.

## Established Decisions (program output so far — owner-ratified, spike-verified)

These resolve FR-005's IL/parser fork and the front/back-separation question, and are fixed
inputs to the synthesis (FR-008) and the future *Full Gleam implementation* epic. Full record +
evidence: `docs/research/glp-gleam-baseline/pipelines/P5-il-machine-language/{DOSSIER.md,DECISIONS.md}`.

- **ED-1 — Front/back seam (ratified 2026-06-26).** The seam = **serialized v2.16.3 bytecode
  (compiled-ML-on-wire) + a server-resolved, heap-independent result envelope**, *identical
  in-process and over-the-wire*. "Combined instance" and "split front/back" are two bindings of
  the one contract — this is the clean separation. The **maGLP agent-link (M2) is a separate
  term-level seam**, not bytecode.
- **ED-2 — Machine language (Fork A=a1).** Keep + freeze/version the **v2.16.3 bytecode ISA** (the
  GLP-correct heir of Shapiro's FCP abstract machine; its two-cell writer/reader = the writer-MGU
  extension that keeps M1 faithful). Do not adopt the FCP machine verbatim.
- **ED-3 — Intermediate language (Fork B=b2, lightweight).** A **dependency-free in-language IR**
  on four primitives (`head_unify`/`guard_test`/`body_spawn`/`suspend_reactivate`) lives
  **front-end-internal** as a codegen + verification aid (SRSW / phase-order / writer-MGU checkable
  before emission); the IL never crosses — only bytecode does. Real-MLIR deferred (revisit only for
  a future LLVM/C++ backend).
- **ED-4 — Compiler placement (Fork C=c1).** The compiler relocates to the **front-end**; pipeline:
  **ANTLR-defined grammar** (parser generated in C#/Dart — ANTLR has no BEAM target; engine stays
  pure-Gleam) → AST → partial-eval → type-check → analyze → 4-primitive IL (+verifiers) →
  v2.16.3 bytecode → codec → engine.
- **ED-5 — Verification (spike PASS, 2026-06-27).** `spike/p5-il-merge/` proved the IL lowering of
  `merge/3` is **byte-identical** to stock `CodeGenerator` and **execution-equivalent** on the real
  runner (Suspend-not-Fail then reactivate+commit), with the verifiers catching real SRSW +
  phase-order violations; ANTLR phase reproduced it. Production tree untouched.
- **ED-6 — Open obligations for the Full-Gleam epic (not yet built):** the Section-15 bytecode
  binary codec (de-embed `Object?`/`StructTerm` operands; Dart↔C#↔Gleam byte-parity) **+ an AtomVM
  bit-syntax decode spike**; the IL op-verifiers as production code; freeze/version the ISA (resolve
  v1/v2). **M2 parity ≠ ISA-identity** (it is the term protocol + byte-identical codec).
