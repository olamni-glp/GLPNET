# Tasks: Higher-Level XML-Schema-Style Schema Language over the Functor Registry

**Input**: Design documents from `/specs/043-xsd-schema-language/`
**Prerequisites**: plan.md, spec.md, research.md (R1–R12), data-model.md, contracts/ (5 files), quickstart.md

**Tests**: INCLUDED — the spec quantifies every success criterion as a test (SC-001..SC-006) and
pins "verified by tests, not delegated to model judgement" (spec Assumptions). Tests are written
first per story and must fail before implementation.

**Organization**: grouped by user story; each story is an independently testable increment.
Substrate law for every task: nothing under `csharp/glp_wire_registry/` or `csharp/glp_crdtmsg/`
is modified (FR-012); substrate suites stay green.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 Create production project `csharp/glp_schema_lang/GlpSchemaLang.csproj` — net10.0, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`, `RootNamespace GlpRuntime.SchemaLang`, `AssemblyName glp_schema_lang`, ProjectReference `..\glp_wire_registry\GlpWireRegistry.csproj` ONLY, `InternalsVisibleTo glp_schema_lang.tests`, clobber-safe comment block (plan Structure Decision); create empty dirs ast/ parser/ pattern/ validate/ lower/ lift/ evolve/ registry/
- [X] T002 Create test project `csharp/glp_schema_lang.tests/GlpSchemaLang.Tests.csproj` — xunit 2.9.3, Microsoft.NET.Test.Sdk 17.14.1, xunit.runner.visualstudio 3.1.4, coverlet.collector; ProjectReferences: glp_schema_lang, glp_wire_registry, glp_crdtmsg; `golden/` content dir copied to output (depends T001)
- [X] T003 Baseline checkpoint: run `dotnet test csharp/glp_wire_registry.tests` and `dotnet test csharp/glp_crdtmsg.tests` green, `dotnet build` both new projects, commit "Checkpoint: 043 project skeleton, substrate baseline green" (Test Protocol)

## Phase 2: Foundational (blocking prerequisites for ALL user stories)

**⚠️ No user story work until this phase completes.**

- [X] T004 [P] AST records in `csharp/glp_schema_lang/ast/SchemaAst.cs` — SchemaDocument, NamedType/SimpleType/ComplexType, Composition, ElementDecl, TypeRef, Occurs, PrimitiveKind, Facet family, MessageDecl, SourceLocation (data-model §1)
- [X] T005 [P] Verdict + error records in `csharp/glp_schema_lang/validate/Verdicts.cs` — InstanceValue union, ValidationVerdict/Violation, SchemaValidationError, LoweringError, NoSchemaRegisteredError, LiftError, NoCompatModeDeclaredError (data-model §4/§7, research R12)
- [X] T006 [P] Restricted-regex NFA engine in `csharp/glp_schema_lang/pattern/PatternNfa.cs` — subset parser (schema-dsl.md §Restricted pattern subset), NFA construction, emptiness (accept-reachability), linear-time match, language-inclusion check for compat (research R6)
- [X] T007 [P] NFA unit tests in `csharp/glp_schema_lang.tests/PatternNfaTests.cs` — subset acceptance/rejection (unsupported construct named), emptiness detection, anchored match semantics, inclusion cases; write first, watch fail, then T006 makes green (pair with T006)
- [X] T008 DSL lexer + recursive-descent parser in `csharp/glp_schema_lang/parser/SchemaDslParser.cs` — full grammar of contracts/schema-dsl.md incl. `?` sugar, `[T]`, `occurs a..b|*`, `//` comments, line:col tracking (depends T004)
- [X] T009 SchemaValidator in `csharp/glp_schema_lang/validate/SchemaValidator.cs` — all 6 well-formedness rule groups: uniqueness, reference resolution, facet consistency (incl. pattern subset+emptiness via T006), DAG check with full cycle-path error, occurs bounds, composition arity; reports ALL errors in one pass with construct+location (FR-002; depends T004–T008)
- [X] T010 Parser + validator unit tests in `csharp/glp_schema_lang.tests/SchemaValidatorTests.cs` — parse round-trip of the quickstart `chat` schema, each well-formedness rule positive+negative, cycle error names full path `A → B → A` (clarification 2)
- [X] T011 Seeded overlay registry skeleton in `csharp/glp_schema_lang/registry/SchemaLangRegistry.cs` — instance class seeded from `WireRegistry.All` + `SchemaRegistry` forms; RegistryRecord, VersionChain; lookup by functor/payload-type over seed ∪ overlay; loud-fail lookups (data-model §2, research R2; depends T005)

**Checkpoint**: parse+validate+registry-read work; foundation ready.

## Phase 3: User Story 1 — Author a rich schema and land it in the registry (P1) 🎯 MVP

**Goal**: valid schema documents lower deterministically to canonical CDDL + functor
registrations recorded in the overlay side by side with the seeded 041 entries; invalid
documents and collisions reject loudly with nothing written.

**Independent Test**: author one schema exercising facets, sequence, choice, optionality,
repetition, and type reuse; lower + register; confirm registry holds CDDL artifact + functor
registration(s) accepted by existing registry conventions (US1 acceptance scenarios 1–3).

### Tests for User Story 1 (write first — must fail)

- [X] T012 [P] [US1] Golden lowering tests in `csharp/glp_schema_lang.tests/LoweringTests.cs` + `golden/chat.cddl` — quickstart `chat` schema lowers to byte-identical golden CDDL; double-run self-identity (FR-005); every contracts/lowering.md mapping row exercised; payload-type allocation 0x13-onwards in declaration order (R3); unlowerable-construct error lists every construct
- [X] T013 [P] [US1] Registration law tests in `csharp/glp_schema_lang.tests/RegistrationTests.cs` — success records {qmedit, cddl, xsd_source verbatim, sha256s} retrievable together (FR-004); functor collision vs seeded `crdt_message` and vs overlay entry → explicit LoweringError, nothing written (US1 AS-3); all-or-nothing on multi-message document with one colliding kind; mandatory CompatMode at registration
- [X] T014 [P] [US1] SC-002 seeded-defect suite in `csharp/glp_schema_lang.tests/SeededDefectSuiteTests.cs` — ≥20 invalid documents as `[Theory]` data covering unresolved references, facet contradictions (min>max, empty enum, empty-language pattern), name collisions, cycles (incl. self-reference); each asserts the error names offending construct AND location (SC-002: 100%)

### Implementation for User Story 1

- [X] T015 [US1] Canonical CDDL emitter in `csharp/glp_schema_lang/lower/CddlEmitter.cs` — full contracts/lowering.md mapping table, canonical ordering/indent/trailing-comma discipline matching the shipped `crdt_message` artifact idioms (R5; depends T004)
- [X] T016 [US1] Lowering + allocation in `csharp/glp_schema_lang/lower/Lowering.cs` — `Lower(SchemaDocument) → LoweringArtifactSet | LoweringError`; deterministic payload-type allocation (lowest free ≥ 0x13, declaration order); unlowerable detection (depends T015, T011)
- [X] T017 [US1] Registration in `csharp/glp_schema_lang/registry/SchemaLangRegistry.cs` — `Register(doc, artifacts, mode)`: collision checks over seed ∪ overlay, all-or-nothing write, RegistryRecord with sha256(cddl)+sha256(qmedit)+xsd_source (FR-004, R9 hashes; depends T016)
- [X] T018 [US1] Make T012–T014 green; then SC-006 walkthrough test in `csharp/glp_schema_lang.tests/EndToEndWalkthroughTests.cs` — scripted author→validate→lower→register→inspect flow with zero hand-written CDDL/functor text (quickstart steps 1–4)

**Checkpoint**: US1 fully functional — the MVP. Commit.

## Phase 4: User Story 2 — Validate message instances against an XSD-level schema (P2)

**Goal**: pass/fail verdicts against registered schemas; failures name the violated
element/facet and its instance path; unregistered kind is a loud explicit error.

**Independent Test**: one registered schema + a corpus of conforming and non-conforming
instances; every verdict correct, every failure construct-named (US2 acceptance scenarios 1–3).

### Tests for User Story 2 (write first — must fail)

- [X] T019 [P] [US2] Instance validation tests in `csharp/glp_schema_lang.tests/InstanceValidationTests.cs` — conforming instance → Pass; facet violation (out-of-range, length, pattern, enum) → Fail naming facet + instance path; composition violation (missing mandatory element, wrong choice-branch arity, occurs out of bounds, sequence order) → Fail naming element; unregistered functor → NoSchemaRegisteredError, never a silent pass (FR-008); deep instance bounded/deterministic (no stack overflow on adversarial nesting — edge case)
- [X] T020 [P] [US2] Corpus agreement tests in `csharp/glp_schema_lang.tests/CorpusAgreementTests.cs` — `Message → InstanceValue` adapter over `SampleMessages.All()`; run 041 golden messages + derived non-conforming mutations through `MessageCodec.Decode`+`DecodeGuard.Check` (registry level) AND `InstanceValidator` (XSD level, against the re-expressed `crdt_message` schema from T022 — no US3 dependency); assert zero polarity contradictions and narrowing-only (SC-003, FR-007, scoped per contracts/validation-api.md agreement law)

### Implementation for User Story 2

- [X] T021 [US2] InstanceValidator in `csharp/glp_schema_lang/validate/InstanceValidator.cs` — check order: kind resolution via overlay → structure → facets (narrowing only); Violation records with constructKind/name/schemaLocation/instancePath; iterative traversal bounded by the schema DAG (contracts/validation-api.md; depends T009, T011, T006)
- [X] T022 [US2] Make T019–T020 green, incl. authoring the `crdt_message` re-expression schema in the 043 DSL used by T020 (feeds SC-001; store under `csharp/glp_schema_lang.tests/schemas/crdt_message.043.txt`)

**Checkpoint**: US1+US2 independently functional. Commit.

## Phase 5: User Story 3 — Lift an existing registry entry into the XSD-level view (P3)

**Goal**: existing entries render in the 043 language where expressible; unexpressible
constructs are reported per-construct, never approximated; out-of-band edits are flagged as drift.

**Independent Test**: lift every seeded registry entry (041 MVP set); each yields a faithful
rendering or an explicit fidelity report (US3 acceptance scenarios 1–2; SC-004).

### Tests for User Story 3 (write first — must fail)

- [X] T023 [P] [US3] Lift + fidelity tests in `csharp/glp_schema_lang.tests/LiftFidelityTests.cs` — lift seeded `crdt_message` → rendering; re-lower and assert accept/reject equivalence over the T020 corpus (SC-004 lower-then-compare); lift `il_program`/`result_envelope` → whole-entry Partial "no CDDL artifact" report; a CDDL construct outside the subset → per-construct UnexpressibleConstruct, outcome Partial, zero silent approximation; round-trip Lift(Lower(doc)) structural equivalence for the `chat` schema (FR-010)
- [X] T024 [P] [US3] Drift tests in `csharp/glp_schema_lang.tests/DriftTests.cs` — register via 043, then mutate the overlay entry's CDDL out-of-band (test seam); lift/view → DriftReport naming the diverged form + rendering reflects CURRENT registry truth, stale XSD source flagged not shown as current (FR-013)

### Implementation for User Story 3

- [X] T025 [US3] CDDL-subset parser in `csharp/glp_schema_lang/lift/CddlSubsetParser.cs` — recursive-descent over the emitter subset + shipped `crdt_message` idioms (`&(…)` enums, `? key`, `[* t]`, `[a*b t]`, ranges, `.size`, `.regexp`, named rule refs); out-of-subset constructs captured verbatim with location (R10; depends T004)
- [X] T026 [US3] Lifter + fidelity + drift in `csharp/glp_schema_lang/lift/Lifter.cs` — `Lift(registry, functor) → LiftResult{rendering, FidelityReport, DriftReport?}`; hash-compare drift detection per R9; current-truth rendering law (contracts/lift-fidelity.md; depends T025, T017)
- [X] T027 [US3] Make T023–T024 green

**Checkpoint**: lift closes the round-trip over the whole seeded registry. Commit.

## Phase 6: User Story 4 — Evolve a schema under a compatibility mode (P3)

**Goal**: version-evolution verdicts under the declared Confluent-style mode; breaking
constructs named; incompatible registration only with a recorded override; no declared mode ⇒
explicit refusal.

**Independent Test**: curated evolution suite (additions, removals, facet widen/narrow, choice
changes) gets the expected verdict in 100% of cases (US4 acceptance scenarios 1–3; SC-005).

### Tests for User Story 4 (write first — must fail)

- [X] T028 [P] [US4] SC-005 evolution suite in `csharp/glp_schema_lang.tests/CompatEvolutionTests.cs` — ≥10 `[Theory]` cases spanning every contracts/compat-evolution.md rule-table row (add optional/mandatory, remove optional/mandatory, facet widen/narrow incl. pattern-inclusion + conservative-breaking, occurs widen/narrow, choice add/remove, type change, reorder), each under backward/forward/full; transitive checked against a 3-version chain; verdicts name breaking construct + rule row
- [X] T029 [P] [US4] Refusal + override tests in `csharp/glp_schema_lang.tests/EvolutionRegistrationTests.cs` — no declared mode → NoCompatModeDeclaredError on check AND register (clarification 3); incompatible v2 registration without override → refused; with OverrideRecord{verdict, acknowledger, reason} → registered and record retrievable on the RegistryRecord (US4 AS-3)

### Implementation for User Story 4

- [X] T030 [US4] CompatChecker in `csharp/glp_schema_lang/evolve/CompatChecker.cs` — construct-level rule table (contracts/compat-evolution.md), NFA language-inclusion for pattern changes with conservative-breaking fallback, transitive = full check over VersionChain (R8; depends T006, T011)
- [X] T031 [US4] Versioned registration in `csharp/glp_schema_lang/registry/SchemaLangRegistry.cs` — `RegisterVersion` / `RegisterVersionWithOverride`, refusal law, OverrideRecord storage, VersionChain maintenance (depends T030, T017)
- [X] T032 [US4] Make T028–T029 green

**Checkpoint**: all four stories independently functional. Commit.

## Phase 7: Polish & Cross-Cutting Acceptance

- [ ] T033 SC-001 re-expression acceptance in `csharp/glp_schema_lang.tests/ReExpressionTests.cs` — every kind in the 041 MVP set (the 3 seeded entries; DSL-formed = `crdt_message`) re-expressed in the 043 language lowers to entries with zero accept/reject divergence over the shared corpus; byte-only kinds recorded as fidelity-report-covered (ties SC-001 to the T022 schema + T020 corpus)
- [ ] T034 [P] Determinism sweep: audit every contract-path type for unordered iteration/timestamps/randomness (FR-005, R11); add a repeated-run determinism test to `csharp/glp_schema_lang.tests/LoweringTests.cs` if any gap found
- [ ] T035 [P] Update `specs/043-xsd-schema-language/quickstart.md` if any API drifted during implementation; verify the quickstart walkthrough compiles as written (SC-006 doc accuracy)
- [ ] T036 Full re-test: `dotnet test csharp/glp_schema_lang.tests` green AND substrate suites (`glp_wire_registry.tests`, `glp_crdtmsg.tests`) still green with `git diff --stat` showing zero changes under `csharp/glp_wire_registry/` and `csharp/glp_crdtmsg/` (FR-012); commit final state

---

## Dependencies & Execution Order

- **Phase 1 → Phase 2 → user stories**: T003 baseline gates everything; T004–T011 block all stories.
- **US1 (Phase 3)**: only foundational deps. **US2 (Phase 4)**: needs T017 (registered schemas). **US3 (Phase 5)**: needs T017; T023 corpus-equivalence also needs T020/T022. **US4 (Phase 6)**: needs T017 (+T006 for pattern inclusion); independent of US2/US3.
- After Phase 3, US2 and US4 can proceed in parallel; US3's T023 is best after US2's corpus lands.
- Within each story: tests ([P], different files) before implementation; implementation tasks in listed order.

## Parallel Opportunities

- Phase 2: T004, T005, T006(+T007) in parallel; then T008→T009→T010, T011 alongside T008.
- Each story's test tasks ([P]) in parallel; US2 ∥ US4 after US1; T034/T035 in parallel at polish.

## Implementation Strategy

**MVP = Phase 1 + Phase 2 + Phase 3 (US1)** — authoring-to-registration is the reason the layer
exists; stop and validate at the US1 checkpoint (SC-002, SC-006, FR-005 all green) before
proceeding. Then incremental: US2 (validation payoff) → US4 ∥ US3 → polish acceptance (SC-001,
SC-003, SC-004, SC-005 complete). Commit at every checkpoint; substrate suites re-run at each.
