# Implementation Plan: Higher-Level XML-Schema-Style Schema Language over the Functor Registry

**Branch**: `043-xsd-schema-language` | **Date**: 2026-07-07 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/043-xsd-schema-language/spec.md`

## Summary

Add a rich schema layer strictly ABOVE the shipped 041 E9 functor-registry substrate: a
plaintext qmedit-family DSL carrying the XSD concepts (named simple types with facets, complex
types composed of sequences/choices with optionality and repetition, type reuse), with
schema-document validation, deterministic lowering to the registry's existing forms (canonical
CDDL artifact + functor registration per message kind), instance validation with
construct-located verdicts, lift of existing registry entries with fidelity + drift reporting,
and compatibility-mode-gated schema evolution. Technical approach: one new hand-authored C#
net10.0 library `csharp/glp_schema_lang/` referencing only the zero-dependency
`glp_wire_registry` leaf; the compile-time-closed E9 static tables stay byte-identical and are
consumed through an additive seeded in-memory overlay registry (research R2). All parsing,
lowering, lift, facet, and compatibility machinery is deterministic hand-rolled code (no LM on
any contract path; no new external dependencies) — the E9 agentic-translation seam is untouched.

## Technical Context

**Language/Version**: C# on net10.0 (`LangVersion latest`, `Nullable enable`,
`ImplicitUsings enable`) — matches the whole `csharp/` tree.
**Primary Dependencies**: `glp_wire_registry` (the E9 substrate — `WireRegistry`,
`SchemaRegistry`, `CompatMode`, reused not redefined). Tests additionally reference
`glp_crdtmsg` for the golden instance corpus (`SampleMessages`, `MessageCodec`, `DecodeGuard`).
**No new NuGet packages** in the production library (zero-dependency discipline of the registry
family); the pattern engine is a hand-rolled NFA over a restricted regex subset (research R6).
**Storage**: in-memory only — a seeded overlay registry (`SchemaLangRegistry`, research R2).
No PGLite, no `.pgdb` writes, no migrations, no files-as-database. This matches the substrate's
own persistence level (041's registry is static in-memory).
**Testing**: xUnit 2.9.3 / `Microsoft.NET.Test.Sdk` 17.14.1 via
`dotnet test csharp/glp_schema_lang.tests`; suites map 1:1 to success criteria — seeded-defect
suite (SC-002, ≥20 invalid documents), evolution suite (SC-005, ≥10 cases), corpus-agreement
harness (SC-001/SC-003/SC-004 over the 041 golden messages + derived mutations), golden-file
CDDL determinism tests (FR-005), end-to-end walkthrough test (SC-006). Substrate suites
(`glp_wire_registry.tests`, `glp_crdtmsg.tests`) are baselined green before and re-run after
(Test Protocol) — they must stay untouched and green (FR-012).
**Target Platform**: net10.0 class library, developed/tested on the Windows host; no runtime
service, no CLI (the substrate has none — the library is consumed via its API, like
`WireRegistry` itself).
**Project Type**: single library + test project (flat under `csharp/`, no `.sln`, clobber-safe
placement outside `out/csharp/` and `glp_runtime_net/`).
**Performance Goals**: N/A beyond the spec's boundedness edge case — validation is linear in
instance size (NFA simulation, no backtracking; schema DAG bounds all recursion depth).
**Constraints**: FR-012 — `WireRegistry.cs`/`SchemaRegistry.cs` and everything under
`glp_crdtmsg` are read-only substrate; deterministic lowering (FR-005 — no timestamps,
randomness, or unordered iteration on any contract path); loud-fail with construct-level
localization everywhere (FR-014); all-or-nothing registration; cycles rejected at
schema-validation (clarification 2); evolution refuses without a declared compat mode
(clarification 3).
**Scale/Scope**: 3 seeded registry kinds — SC-001's re-expression denominator is the one
DSL-formed kind (`crdt_message`); the two byte-only kinds (`il_program`, `result_envelope`)
carry no shape to re-express and are covered by explicit partial-fidelity reports (SC-004
path). Schema documents of tens of types; instance corpus = 3 golden messages + derived
conforming/non-conforming mutations; 8 production source areas (ast, parser, pattern,
validate, lower, lift, evolve, registry).

## Constitution Check

*GATE: evaluated against constitution v1.1.0 before Phase 0; re-checked after Phase 1 design: PASS.*

| Principle | Verdict | Rationale |
|---|---|---|
| I. Spec-First | PASS | Plan derives from spec.md (14 FRs, 3 encoded clarifications) + verified substrate reads (`WireRegistry.cs`, `SchemaRegistry.cs`, 041 spec FR-032..034). Contracts quote and reference the spec's FRs; no behavior is invented beyond it. |
| II. Bug-Protocol / No-Workarounds | PASS | Loud-fail error model everywhere (FR-014, research R12); no tolerant-input "robustness" — invalid schema/instance/evolution is an explicit located error, never masked. Substrate bugs, if found, STOP-and-report (041 code is read-only here). |
| III. SRSW machine scan | PASS | No GLP code is written by this feature. Artifact scan: zero occurrences of the forbidden token in spec/plan/tasks artifacts. |
| IV-a. Language Authority | PASS | No GLP language surface: the DSL is a standalone plaintext artifact language, not a GLP extension (spec Assumptions). If design ever suggests a GLP-side surface, it stops for §1.14 propose-first approval. |
| IV-b. Preserve Working Internals | PASS | No runtime internals touched; the E9 static tables are consumed read-only via the seeded overlay (research R2). |
| V. Claude-Only LM machine scan | PASS | No LM on any 043 contract path — lowering/lift/compat are deterministic code (spec Assumptions: contracts "verified by tests, not delegated to model judgement"). The E9 agentic qmedit↔CDDL seam remains Claude-agentic and unchanged. Artifact scan clean. |
| VI-a. Additive-only persistence | PASS | No migrations, no DB writes — in-memory overlay only (research R2). |
| VI-b. Single PGLite cluster | PASS | No cluster use at all; no second store created (the overlay is in-process state, not a persistence deployment). |
| VII. Test-gated, commit-scoped shipping | PASS | Baseline substrate + new suites green before/after each change; files staged by name; ship via buildkit GitFlow. |
| VIII. Single Source of Truth | PASS | spec.md is authoritative for behavior; contracts/ are the normative interface texts referenced by tests; the E9 registry remains the single registration substrate — 043 layers, never duplicates it (FR-012). |

No violations → Complexity Tracking not required.

## Project Structure

### Documentation (this feature)

```text
specs/043-xsd-schema-language/
├── plan.md              # This file
├── research.md          # Phase 0: R1–R12 decisions (substrate, overlay, DSL, CDDL subset, NFA, compat rules)
├── data-model.md        # Phase 1: AST, overlay records, verdicts, fidelity/drift/override records
├── quickstart.md        # Phase 1: build/test + SC-006 walkthrough + adapter seam
├── contracts/           # Phase 1: normative interface contracts
│   ├── schema-dsl.md            # the authoring language grammar + well-formedness (FR-001/002)
│   ├── lowering.md              # DSL→CDDL mapping, allocation, registration laws (FR-003/004/005)
│   ├── validation-api.md        # document + instance validation, agreement law (FR-002/006/007/008)
│   ├── compat-evolution.md      # rule table, refusal law, override record (FR-011)
│   └── lift-fidelity.md         # lift laws, fidelity report, drift detection (FR-009/010/013)
└── tasks.md             # Phase 2 (/bk-tasks — NOT created by /bk-plan)
```

### Source Code (repository root)

```text
csharp/
├── glp_schema_lang/                     # NEW — production library (refs glp_wire_registry ONLY)
│   ├── GlpSchemaLang.csproj             #   net10.0, RootNamespace GlpRuntime.SchemaLang,
│   │                                    #   InternalsVisibleTo glp_schema_lang.tests, clobber-safe note
│   ├── ast/                             #   SchemaDocument, NamedType, ElementDecl, Facet, Occurs…
│   ├── parser/                          #   DSL lexer + recursive-descent parser (schema-dsl.md)
│   ├── pattern/                         #   restricted-regex NFA: parse, emptiness, match, inclusion (R6)
│   ├── validate/                        #   SchemaValidator (doc), InstanceValidator + verdicts (R7/R12)
│   ├── lower/                           #   canonical CDDL emitter + payload-type allocation (R3/R5)
│   ├── lift/                            #   CDDL-subset parser, Lifter, FidelityReport, DriftReport (R9/R10)
│   ├── evolve/                          #   CompatChecker rule table, CompatVerdict, OverrideRecord (R8)
│   └── registry/                        #   SchemaLangRegistry seeded overlay + RegistryRecord (R2)
├── glp_schema_lang.tests/               # NEW — xUnit (refs glp_schema_lang, glp_wire_registry, glp_crdtmsg)
│   ├── GlpSchemaLang.Tests.csproj
│   ├── golden/                          #   golden CDDL files (FR-005 determinism)
│   ├── SchemaValidatorTests.cs          #   incl. SC-002 seeded-defect suite (≥20 cases)
│   ├── LoweringTests.cs                 #   golden + collision + all-or-nothing + allocation
│   ├── InstanceValidationTests.cs       #   verdict localization + FR-008 loud-fail
│   ├── CorpusAgreementTests.cs          #   SC-001/SC-003 vs MessageCodec/DecodeGuard (Message→InstanceValue adapter here)
│   ├── LiftFidelityTests.cs             #   SC-004 over all seeded entries + round-trip (FR-010)
│   ├── CompatEvolutionTests.cs          #   SC-005 suite (≥10 cases) + refusal + override
│   └── EndToEndWalkthroughTests.cs      #   SC-006 scripted walkthrough
└── glp_wire_registry/                   # UNCHANGED substrate (read-only, FR-012)
```

**Structure Decision**: single new library + tests following the exact conventions of the 041
family (flat under `csharp/`, per-project `dotnet build`/`dotnet test`, no `.sln`,
`InternalsVisibleTo` test pairing, clobber-safe placement). The production dependency arrow is
`glp_schema_lang → glp_wire_registry` only — the same direction as `glp_result_codec`, keeping
the byte-parity leaf a leaf. `glp_crdtmsg` is referenced by the **tests** project only (corpus
reuse); nothing under `glp_wire_registry/` or `glp_crdtmsg/` is modified.

## Complexity Tracking

No constitution violations — table not required.
