# Feature Specification: codeconv-builder — Unified Conversion Workbench

**Feature Branch**: `018-codeconv-builder`
**Created**: 2026-05-17
**Status**: Draft
**Input**: User description: "consolidate 015, 016 and 017 and existing codebase into a uniform whole with a /codeconv-builder skill and tool and /codeconv-init /codeconv-scaffold /codeconv-discover /codeconv-convspec (analyse the Dart source and create a well-researched detailed spec for converting a Dart source file into a C# code-unit file through careful source-code analysis and by building a database of conversion idioms for this codebase) etc. codeconv-builder and its child tools need full DBOS integration for conversion workflow continuity. The already-spec'd-and-implemented topological sort is the backbone of the workflows from discover onwards. Deep code analysis from spec to implementation is critical, as is thorough web-based research on appropriate Dart↔C# code-conversion patterns for nuances and detail."

## Clarifications

### Session 2026-05-17

- Q: What mechanism performs convspec's deep analysis + web research? → A: Agent/LLM-driven deep analysis + web research, orchestrated per-file as durable DBOS steps (feature-017 planagents-style).
- Q: What form is the per-file conversion spec? → A: A structured machine-consumable artifact WITH embedded human-readable rationale/provenance (both).
- Q: What is the DBOS durable unit of work? → A: Per-(file, stage) — each stage of each file is a durable step, resume at the interrupted stage; a cycle group is one unit.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - One durable command drives the whole conversion pipeline (Priority: P1)

The operator runs a single `codeconv-builder` action and the system executes
the full conversion pipeline — inventory the Dart source, compute the
dependency-ordered (topologically sorted, cycle-group-aware) work list, and
process files in that order through each downstream stage — as one **durable**
workflow. If the run is interrupted (crash, reboot, bridge restart, operator
cancel), re-running the same command **resumes from the last completed unit of
work**; completed files are not redone and partially-done files are recovered
to a consistent state.

**Why this priority**: This is the core value of consolidation. Today the
operator manually chains discover → depgraph → init/scaffold → planagents
across fragmented surfaces (015/016/017) with no continuity guarantee; a long
run that dies midway loses progress or risks corrupt state. A single resumable
command over a large codebase is the MVP and delivers value even before
`convspec` exists.

**Independent Test**: Start a builder run over a multi-file Dart subtree, kill
it partway, re-run the same command; confirm (a) no completed file is
reprocessed, (b) remaining files complete, (c) final state equals an
uninterrupted run.

**Acceptance Scenarios**:

1. **Given** an initialised workspace with N inventoried Dart files, **When**
   the operator runs the builder end-to-end, **Then** every file is processed
   exactly once in dependency order with a complete summary.
2. **Given** a run interrupted after K of N files, **When** the operator
   re-runs the same command, **Then** it continues from file K+1 in
   dependency order and does not reprocess files 1..K.
3. **Given** a file whose dependencies are not yet converted, **When** the
   pipeline reaches it, **Then** it is not processed until all dependencies
   (or its entire cycle group) are complete.

---

### User Story 2 - convspec deeply analyses each file and researches Dart↔C# nuances to produce a researched conversion spec (Priority: P1)

For each Dart source file, `codeconv-convspec` performs a **deep source-code
analysis** (semantics, types, async/stream constructs, language features, not a
mechanical rename) and conducts **thorough web-based research** on the
appropriate Dart→C#/.NET conversion patterns for the specific constructs it
finds — capturing nuances and detail where the two languages differ — and from
both produces a detailed, well-researched per-file conversion specification for
the corresponding C# code-unit. It records and reuses **conversion idioms**
specific to this codebase so later files are converted consistently and spec
quality compounds as more of the codebase is analysed.

**Why this priority**: This is the capability that makes the conversion
*correct and faithful to language nuance*, not just scaffolded. The operator
explicitly designates deep analysis + web research as critical; a fast but
shallow conversion is not acceptable. It is co-critical with US1 (the durable
pipeline is the vehicle; the researched per-file spec is the payload).

**Independent Test**: Run `convspec` on a Dart file using a non-trivial
construct (e.g. a stream/async or a Dart-specific idiom) with no prior idioms;
confirm the output spec cites the analysed source facts and the
researched Dart→C# pattern, and records new idioms. Run it on a second file
reusing that construct; confirm the recorded idiom is applied, not
re-researched into a divergent decision.

**Acceptance Scenarios**:

1. **Given** a Dart source file, **When** `convspec` analyses it, **Then** a
   per-file conversion spec is persisted, linked to that file's tombstone, and
   grounded in both the deep source analysis and the researched conversion
   pattern for each non-trivial construct.
2. **Given** a construct already recorded as an idiom, **When** a later file
   uses it, **Then** the spec references the existing idiom rather than
   producing a divergent decision.
3. **Given** a construct whose correct Dart→C# translation cannot be
   established from analysis, recorded idioms, or research, **When** `convspec`
   runs, **Then** it raises an escalation rather than guessing silently.
4. **Given** a construct with a well-known Dart→C# nuance (e.g. value vs.
   reference semantics, `Stream` vs. `IAsyncEnumerable`, null-safety mapping),
   **When** the spec is produced, **Then** the nuance is explicitly addressed,
   not glossed over.

---

### User Story 3 - One coherent surface replaces the fragmented 015/016/017 tools (Priority: P2)

The operator interacts with one consistent set of skills/commands
(`codeconv-builder` plus child tools `codeconv-init`, `codeconv-discover`,
`codeconv-scaffold`, `codeconv-convspec`, and the existing depgraph/mirror/
planagents capabilities) sharing one workspace model, one database schema with
a single linear migration chain, and one status/reporting convention — instead
of three independently-specified features with overlapping concepts and a
broken migration graph.

**Why this priority**: Consolidation removes current defects (duplicate Alembic
`0003` / dual heads that block migration; divergent CLI conventions;
per-feature workspace assumptions) and makes the system maintainable, enabling
US1/US2.

**Independent Test**: From a fresh PG17 cluster, run the unified migration to a
single head with no duplicate-revision error; run each child tool through the
unified surface and confirm consistent workspace/status behaviour.

**Acceptance Scenarios**:

1. **Given** a fresh database, **When** migrations are applied, **Then** they
   reach a single head with no duplicate-revision or multiple-heads error.
2. **Given** the unified surface, **When** the operator inspects status,
   **Then** every stage reports through one consistent status/escalation model.

---

### User Story 4 - Progress, escalations, and recovery are observable per file (Priority: P3)

At any time the operator can ask the builder for conversion state — which files
are done, in progress, blocked on dependencies, or escalated — and can resume,
retry a single file, or re-drive the frontier without corrupting state.

**Why this priority**: Long multi-hour conversions over a large codebase need
visibility and safe single-file retry; valuable but dependent on US1.

**Independent Test**: Mid-run, query status and confirm counts reconcile with
tombstones/DB; force one file to escalate, confirm it is reported while the
rest of the frontier still progresses.

**Acceptance Scenarios**:

1. **Given** an in-flight run, **When** the operator queries status, **Then**
   per-file states and aggregate counts reconcile with durable state.
2. **Given** an escalated file, **When** the operator resolves it and re-runs,
   **Then** only that file (and now-unblocked dependents) progresses.

### Edge Cases

- **Circular imports (SCC)**: a dependency cycle is one indivisible work unit —
  the whole cycle group is analysed/specced/scaffolded together, consistent
  with the already-implemented topological/SCC handling.
- **Crash mid-file**: a file interrupted between stages is recovered to a
  consistent state (fully rolled back or resumable at the interrupted stage),
  never left as a corrupt "done".
- **Web research unavailable / inconclusive**: if research cannot be performed
  or yields no authoritative pattern for a construct, the system escalates
  (does not silently fall back to a naive translation).
- **Conflicting research vs. recorded idiom**: when newly-researched guidance
  contradicts an established codebase idiom, the conflict is escalated, not
  silently overridden in either direction.
- **Durable-workflow code changes mid-run**: resume vs. restart of affected
  units is deterministic and well-defined, not undefined behaviour.
- **Tombstone ↔ database divergence**: detected; the system refuses to proceed
  silently on stale state.
- **Idiom conflict**: two recorded idioms dictating contradictory translations
  for the same construct → escalation, not arbitrary precedence.
- **Missing/empty source subtree**: a run with nothing to do exits cleanly with
  an explicit "nothing to convert".
- **Dependency not yet converted**: a file is never processed ahead of its
  unconverted dependencies (topo invariant) unless they form one cycle group
  processed together.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a single top-level `codeconv-builder`
  entry point (skill + tool) orchestrating the end-to-end conversion pipeline
  over an initialised workspace.
- **FR-002**: The builder MUST drive its stages in dependency order using the
  already-specified-and-implemented topological sort (with cycle-group/SCC
  handling) as the backbone for all work from discovery onwards; no file may be
  processed before its dependencies (or whole cycle group) are complete.
- **FR-003**: The builder and every child tool MUST execute as durable
  workflows such that an interrupted run resumes from the last completed unit
  of work without reprocessing completed files and without leaving corrupt
  partial state (continuity via the project's existing DBOS integration). The
  **durable unit of work is the (file, stage) pair**: each stage of each file
  is a resumable/idempotent durable step, so an interruption resumes at the
  interrupted stage of the interrupted file (not from that file's first
  stage). A dependency cycle group is treated as one indivisible unit across
  its stages.
- **FR-004**: Re-running the builder MUST be idempotent: completed work is not
  repeated; the final state of an interrupted-then-resumed run MUST equal that
  of an equivalent uninterrupted run.
- **FR-005**: The system MUST expose child tools `codeconv-init`,
  `codeconv-discover`, `codeconv-scaffold`, `codeconv-convspec` (and retain the
  existing depgraph, mirror, planagents capabilities) under one consistent
  surface, workspace model, and status/escalation convention.
- **FR-006**: `codeconv-init` MUST configure one unified conversion workspace
  (Dart→C#/.NET pair, source/target/mirror paths, exclusions, phase tracking)
  shared by all other tools.
- **FR-007**: `codeconv-discover` MUST inventory the in-scope source files into
  shared workspace state and per-file tombstones as the entry stage of the
  topologically-ordered workflow.
- **FR-008**: `codeconv-scaffold` MUST produce the target C# code-unit tree for
  in-scope files and record each produced target path into per-file
  conversion-tracking state.
- **FR-009**: `codeconv-convspec` MUST perform a **deep source-code analysis**
  of each Dart file — semantics, types, null-safety, async/stream/isolate
  constructs, language-specific features — sufficient to drive a faithful
  (not mechanical) conversion from spec through to implementation. The
  analysis + research is **agent/LLM-driven**, orchestrated per-file as
  durable DBOS steps in the feature-017 planagents style (each step
  resumable/idempotent).
- **FR-010**: `codeconv-convspec` MUST perform **thorough web-based research**
  on the appropriate Dart→C#/.NET conversion pattern for each non-trivial
  construct it identifies, explicitly capturing nuances where the languages
  differ, and MUST cite/record the basis of each conversion decision in the
  per-file spec.
- **FR-011**: `codeconv-convspec` MUST produce a detailed, well-researched
  per-file conversion specification for the C# code-unit, grounded in both the
  deep analysis (FR-009) and the research (FR-010), and link it to that file's
  durable conversion record/tombstone. The spec MUST be a **structured,
  machine-consumable artifact** (schema'd fields the later code-gen stage can
  parse) that ALSO carries **embedded human-readable rationale and research
  provenance** for each non-trivial decision, so it is both deterministically
  consumable and reviewable.
- **FR-012**: The system MUST maintain a persistent, codebase-scoped
  **conversion-idiom knowledge base**: recurring source→target decisions are
  recorded once and reused on later files for cross-file consistency; research
  is not redundantly repeated for an already-decided construct.
- **FR-013**: When a construct's correct translation cannot be established from
  analysis, recorded idioms, or research (incl. research unavailable or
  inconclusive), the system MUST raise an escalation for human decision rather
  than guessing silently; resolved decisions feed back into the idiom base.
- **FR-014**: When researched guidance conflicts with an established codebase
  idiom (or two idioms conflict), the system MUST escalate the conflict rather
  than silently overriding either.
- **FR-015**: The system MUST consolidate the database schema into a **single
  linear migration chain with one head**, eliminating the duplicate revision
  `0003` / multiple-heads defect introduced by independently merging features
  016 and 017, so a fresh cluster migrates cleanly to head.
- **FR-016**: The consolidation MUST preserve every capability delivered by
  features 015 (dependency graph + readiness oracle), 016 (init/scaffold/mirror
  Dart→C# pipeline + language-pair registry), and 017 (per-tombstone
  conversion-plan generation) — no implemented capability is lost; overlapping
  concepts are unified, not duplicated.
- **FR-017**: The operator MUST be able to query conversion status at any time
  and see per-file state (not started / blocked-on-deps / analysed / specced /
  scaffolded / converted / escalated / complete) and aggregate counts that
  reconcile with durable state, within an interactive response time.
- **FR-018**: The operator MUST be able to retry or re-drive a single file (or
  cycle group) and resume the frontier without corrupting other files' state.
- **FR-019**: The system MUST detect divergence between durable database state
  and checked-in tombstones and refuse to proceed silently on stale state.
- **FR-020**: A builder run with no in-scope work remaining MUST exit cleanly
  with an explicit "nothing to convert" outcome, not an error.
- **FR-021**: All pipeline data (inventory, depgraph, specs, idioms, plans,
  target stubs) MUST be reproducible from the durable source of truth,
  consistent with the project decision that all conversion data is recreatable
  afresh (no live-data migration dependency).
- **FR-022**: The consolidation MUST take the **refactor** form: the
  capabilities of features 015/016/017 are re-architected behind one unified
  internal model and one builder surface, with behaviour preserved (no
  capability regression). The three features' overlapping concepts (workspace,
  tombstone/conversion record, status/escalation, migration chain) MUST be
  unified into single shared definitions; the unified model becomes
  authoritative going forward while the original per-feature specs remain as
  historical lineage (they are not deleted, but the unified spec governs).
- **FR-023**: `codeconv-convspec` MUST produce **only** the per-file
  conversion specification and the recorded conversion idioms. It MUST NOT
  itself emit compilable C#; actual Dart→C# code generation is performed by a
  later stage (scaffold / a subsequent human-or-agent step) consuming the
  spec. This keeps the researched conversion reviewable before any code is
  written.
- **FR-024**: Web-based research (FR-010) MUST treat **official Dart and
  .NET/C# documentation as authoritative**; broader web sources MAY be used
  only as corroboration, never as the sole basis for a decision. Every
  research-grounded decision MUST record its provenance (source + what was
  concluded) in the per-file spec, and MUST be cached per construct in the
  idiom knowledge base so the same construct is not re-researched and the
  decision is reproducible offline after first research.

### Key Entities *(include if feature involves data)*

- **Conversion Workspace**: the single shared configuration binding the
  language pair (Dart→C#/.NET), source/target/mirror roots, exclusions, phases.
- **Source File**: an in-scope Dart file; unit of inventory and node in the
  dependency graph.
- **Dependency Graph / Topological Order**: the already-implemented ordering
  (with cycle groups/SCCs) sequencing all work from discover onwards.
- **Target Code Unit**: the C# artifact corresponding to a source file.
- **Source Analysis Result**: the deep per-file analysis facts that ground the
  conversion spec.
- **Conversion Spec**: the per-file researched description of how a source file
  becomes its target code unit; linked to the tombstone.
- **Conversion Idiom**: a recorded, reusable, codebase-scoped source→target
  translation decision (with its research/analysis provenance).
- **Idiom Knowledge Base**: the persistent, growing idiom collection enforcing
  cross-file consistency and avoiding redundant research.
- **Research Finding**: recorded provenance of an external Dart→C# pattern used
  to justify a conversion decision.
- **Conversion Workflow Run**: a durable execution with resumable, idempotent
  units of work.
- **Tombstone / Conversion Record**: durable per-file state; the checked-in
  round-trip source of truth.
- **Escalation**: a flagged decision point requiring human resolution, fed back
  into the idiom base.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The entire pipeline for an in-scope subtree is driven by **one**
  operator command (no manual hand-off between stages).
- **SC-002**: After an interruption at any point, re-running reprocesses **0**
  already-completed files and the final state is identical to an uninterrupted
  run.
- **SC-003**: **100%** of processed files are handled in dependency order — no
  file specced/scaffolded/converted before its dependencies (or cycle group) —
  verifiable from the durable record.
- **SC-004**: A fresh database reaches a **single migration head with zero**
  duplicate-revision / multiple-heads errors.
- **SC-005**: **Zero** capability regressions: every capability available from
  015/016/017 before consolidation is still available after.
- **SC-006**: Every per-file conversion spec for a file containing ≥1
  non-trivial construct records both a deep-analysis basis and a researched
  conversion-pattern basis for each such construct (no unjustified decisions).
- **SC-007**: For a construct recurring across files, the conversion decision
  is **consistent** at every occurrence — ≥95% of recurring constructs resolved
  via a recorded idiom rather than re-derived/re-researched.
- **SC-008**: Every undecidable conversion point is surfaced as an escalation
  (**0** silent guesses); once resolved it does not recur for the same
  construct.
- **SC-009**: Operator obtains an accurate per-file status snapshot in under
  **5 seconds** that reconciles exactly with durable state.

## Assumptions

- The already-specified-and-implemented topological sort + conversion-readiness
  oracle (feature 015) is reused unchanged as the workflow backbone; this
  feature does not redesign the ordering algorithm.
- Durable workflow continuity is the project's existing DBOS integration over
  the unified PGLite/PG17 bridge; "DBOS" means durable/resumable/idempotent
  workflow execution, not a new engine.
- The language pair is Dart→C#/.NET via the existing language-pair registry
  (feature 016); other pairs are out of scope.
- All conversion data is recreatable afresh; no dependency on migrating
  historical live data (per the 2026-05-17 project decision + fresh PG17
  cluster rebuild).
- Tombstones under `.codeconv/tombstones/` remain the durable, checked-in
  round-trip source of truth; the database is the working/runtime store.
- Resolving the duplicate Alembic `0003` collision (016 vs 017) into one linear
  chain is in scope as part of "uniform whole".
- The operator is a single technical user driving conversions locally; no
  multi-tenant/concurrent-operator requirement.
- "Deep code analysis from spec to implementation" and "thorough web-based
  research on Dart↔C# conversion nuances" are explicit, non-negotiable quality
  bars for `convspec`; a fast-but-shallow conversion is not acceptable.
- Existing per-feature specs (015/016/017) remain as historical lineage; per
  FR-022 the unified model is authoritative going forward (refactor, not
  delete). convspec is spec-only (FR-023); code generation is a later stage.
  Web research is official-docs-authoritative with recorded, cached provenance
  (FR-024).
