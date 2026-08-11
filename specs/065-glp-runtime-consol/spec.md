<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Feature Specification: glp-runtime-consol

**Feature Branch**: `065-glp-runtime-consol`
**Created**: 2026-08-03
**Status**: Draft
**Input**: User description: "glp-runtime-consol"

## Overview

Consolidates the two genuine remaining GLP-runtime gaps surfaced by the 2026-08-03
`/bk-3rtask` gap audit (run `20260803T134205Z-8bcd`, 3 blind disjoint builders). The audit
found 12 of 16 non-closed runtime/engine roadmap features were already delivered by wave-4/062
and specs/050 (since closed as roadmap hygiene). Only two runtime gaps remain unshipped:

- **(A)** an ANTLR4 shared-grammar **feasibility spike** — the GLP parser surface is still
  hand-written recursive-descent across runtimes (C# `out/csharp/lib/compiler/parser.cs`, Dart
  `glp_runtime/lib/compiler/parser.dart`) with no single-sourced `.g4` grammar; and
- **(B)** removal of a dead C# abandon stub (`out/csharp/lib/runtime/abandon.cs`) that throws
  `NotImplementedException` — abandon is already delivered as the anonymous-writer discard
  semantic (062 US5; FCP has no dedicated abandon op).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - ANTLR4 shared-grammar feasibility spike (Priority: P1)

An engine/runtime maintainer wants to know whether the GLP language can be single-sourced as one
ANTLR4 `.g4` grammar that generates a parser front-end producing the **same** intermediate
representation (IL/bytecode) as the existing hand-written parser — so the three runtimes
(C#, Dart, and a trial C++/Gleam target) could eventually converge on one grammar instead of
independently maintained hand-written parsers that must be kept byte-parity by hand.

**Why this priority**: This is the only substantive unshipped runtime deliverable. It de-risks
the largest source of cross-runtime drift (divergent hand-written parsers) and underpins the
formal grammar metric for all language-touching seeds. It is a spike (exploratory, de-risking),
so its value is the confirmed feasibility verdict + a reusable grammar prototype, not a
production parser replacement.

**Independent Test**: Can be fully tested by producing a `.g4` grammar that covers the existing
GLP token vocabulary (`out/csharp/lib/compiler/token.cs`), generating a parser front-end for at
least the primary target (C#), parsing a representative corpus of working GLP definitions, and
confirming the generated parser yields identical IL to the existing hand-written parser for that
corpus (or documenting precisely where and why it diverges). Delivers a go/no-go feasibility
verdict independent of Scope B.

**Acceptance Scenarios**:

1. **Given** the existing GLP token vocabulary and a corpus of working GLP definitions, **When**
   the shared `.g4` grammar is generated into a parser front-end and each corpus example is
   parsed, **Then** every example that the hand-written parser accepts is accepted by the
   generated parser (grammar coverage is proven against the corpus).
2. **Given** a corpus example parsed by both the generated parser and the hand-written parser,
   **When** both ASTs are compiled through the shared code-generation pipeline, **Then** the
   emitted IL/bytecode instruction sequences are identical — or every divergence is enumerated
   with its cause in the feasibility report.
3. **Given** the spike reveals that faithfully expressing the language requires a change to the
   **accepted** GLP syntax, **When** that need is identified, **Then** work STOPS at a
   language-authority gate (a written proposal to Gabi + Udi per DISCIPLINE §1.14) and no
   syntax-affecting change is made before explicit approval.
4. **Given** the spike concludes, **When** the feasibility report is written, **Then** it states
   a clear go/no-go verdict, the multi-target generation cost (C# proven; C++/Dart/Gleam trial or
   deferred), the dependency posture (compiled-IL #11 and il-codec #4, both delivered), and any
   residual risks.

---

### User Story 2 - Abandon dead-stub cleanup (Priority: P2)

An engine/runtime maintainer wants the dead `AbandonOps.AbandonWriter` stub removed so the C#
runtime contains no `NotImplementedException`-throwing placeholder for a capability that is
already delivered by other means — keeping the codebase honest about what is and is not
implemented.

**Why this priority**: Low-risk dead-code removal that improves codebase honesty. Abandon is
already delivered as the anonymous-writer discard semantic (062 US5), so the stub is unreachable
and misleading. Independent of the spike; a viable slice on its own.

**Independent Test**: Can be fully tested by confirming no production code path calls
`AbandonOps.AbandonWriter`, removing (or obsoleting) `out/csharp/lib/runtime/abandon.cs`, and
confirming the C# solution still builds green with zero errors and the existing test suites stay
green.

**Acceptance Scenarios**:

1. **Given** the C# runtime source tree, **When** all references to `AbandonOps`/`AbandonWriter`
   are searched, **Then** the search confirms the stub has no production callers before removal.
2. **Given** the confirmed-dead stub, **When** `abandon.cs` is removed, **Then** the C# engine
   solution compiles with zero errors and the pre-existing test baselines (REPL, engine unit,
   Gleam, C#) remain green with no new failures.

---

### Edge Cases

- **Grammar cannot cover a construct**: If a GLP construct in the corpus cannot be expressed in
  the `.g4` grammar without a syntax change, this is a spike finding — record it and, if it would
  alter accepted syntax, escalate to the §1.14 gate rather than changing the language.
- **IL divergence for an accepted example**: A generated-vs-hand-written IL mismatch on an
  example both parsers accept is a finding to enumerate (with cause), not a silent pass; it does
  not by itself fail the spike but must be reported.
- **Abandon stub has a hidden caller**: If a caller of `AbandonWriter` is found, STOP — the stub
  is not dead; report per the Bug Protocol before removing anything.
- **Multi-target generation infeasible for a trial target**: If C++/Dart/Gleam generation from
  the shared grammar is impractical within the spike, that is a documented deferral, not a
  failure of the C# feasibility verdict.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The feature MUST deliver an ANTLR4 `.g4` grammar that covers the existing GLP token
  vocabulary (as defined by `out/csharp/lib/compiler/token.cs` / the Dart lexer) for the corpus
  under test.
- **FR-002**: The feature MUST generate a parser front-end from the shared grammar for at least
  the primary target (C#), and MUST attempt (or explicitly defer with rationale) a trial target.
- **FR-003**: The feature MUST parse a representative corpus of working GLP definitions with the
  generated parser and record, per example, whether it is accepted.
- **FR-004**: The feature MUST compare IL/bytecode produced via the generated parser against the
  IL produced via the existing hand-written parser for the same corpus, and MUST enumerate every
  divergence with its cause.
- **FR-005**: The feature MUST NOT change the **accepted** GLP syntax. If faithful grammar
  expression appears to require a syntax change, the feature MUST STOP and produce a written
  §1.14 language-authority proposal for Gabi + Udi approval before any such change.
- **FR-006**: The feature MUST produce a feasibility report with a go/no-go verdict, multi-target
  generation cost, dependency posture, and residual risks.
- **FR-007**: The feature MUST confirm (by source search) that `AbandonOps.AbandonWriter` has no
  production callers before removing it.
- **FR-008**: The feature MUST remove (or obsolete) the dead `out/csharp/lib/runtime/abandon.cs`
  stub once confirmed dead.
- **FR-009**: After both scopes, the C# engine solution MUST build with zero errors and all
  pre-existing test baselines (REPL, engine unit, Gleam, C#) MUST remain green with no new
  failures.
- **FR-010**: The spike's grammar/parser artifacts MUST be additive (a prototype under a spike
  path); the feature MUST NOT replace the production hand-written parsers as part of this spike.

### Key Entities

- **Shared GLP grammar**: A single ANTLR4 `.g4` definition of the GLP surface syntax (tokens +
  production rules), intended as the single source from which per-runtime parser front-ends are
  generated.
- **Generated parser front-end**: The parser produced from the shared grammar for a target
  runtime (C# primary; C++/Dart/Gleam trial or deferred).
- **Corpus / IL parity baseline**: The set of working GLP definitions parsed by both the
  generated and hand-written parsers, and the compiled IL used to confirm equivalence.
- **Feasibility report**: The spike's written deliverable — verdict, costs, dependencies, risks.
- **Abandon stub**: `out/csharp/lib/runtime/abandon.cs` — the dead `NotImplementedException`
  placeholder to be removed.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of the working-GLP corpus examples accepted by the hand-written parser are
  also accepted by the generated parser (or every non-accepted example is enumerated with cause).
- **SC-002**: For 100% of corpus examples accepted by both parsers, the IL/bytecode is confirmed
  identical, or each divergence is documented with its cause in the feasibility report.
- **SC-003**: The feasibility report states an unambiguous go/no-go verdict and is reviewable by
  an engine maintainer without running the spike.
- **SC-004**: Zero changes to accepted GLP syntax land without a recorded §1.14 approval; any
  syntax-change need is captured as a written proposal, not an implemented change.
- **SC-005**: After the abandon cleanup, a source search returns zero references to the removed
  stub and the C# solution builds with zero errors.
- **SC-006**: All pre-existing test baselines remain green (no new failures introduced by either
  scope).

## Assumptions

- **Roadmap hygiene already done**: The 12 already-delivered features identified by the gap audit
  have already been closed on the roadmap; closing them is not part of this feature's
  implementation (it is a roadmap operation handled separately).
- **Dependencies delivered**: The prerequisites for a meaningful identical-IL comparison —
  compiled-IL-on-the-wire (#11) and the il-codec round-trip foundation (#4) — are already
  delivered (wave-4/062, specs/050), so the spike can perform IL comparison without first
  building those foundations.
- **Spike, not replacement**: This is an EXPERIMENT/feasibility spike. The production hand-written
  parsers stay in place; the grammar and generated parser are additive prototypes. A future
  PREP/REFACTOR feature would decide any production adoption.
- **Primary target is C#**: C# is the primary generation target (matching the as-built engine);
  C++/Dart/Gleam are trial or deferred targets, consistent with the dossier.
- **Out of scope (do NOT re-fold)**: qr-link-provisioning (a distributed-connectivity feature,
  not runtime); atomic-toolchain-installs (#3) and batch-roadmap-advance (#10) (buildkit repo,
  not glpnet).
- **ANTLR4 toolchain**: The ANTLR4 tool + a target runtime library are available (or installable)
  in the environment for grammar generation; if unavailable, the spike degrades to a documented
  grammar + a manual coverage argument rather than a generated-parser run, recorded as a
  limitation.
