<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 1 Data Model: glp-runtime-consol

This is a spike + cleanup; the "data model" is the set of spike artifacts and the parity record,
not a persisted schema. No database entities are created (Constitution VI-a/b: no migrations).

## Entities

### SharedGrammar
- **Represents**: The single ANTLR4 `.g4` definition of the GLP surface syntax under test.
- **Fields**: lexer rules (derived from `token.cs` vocabulary), parser production rules, grammar
  version/date, coverage note (which constructs are in-scope for the spike).
- **Rules**: MUST describe only the *existing* accepted syntax (FR-005). MUST cover every token
  in the corpus under test (FR-001).
- **Location**: `spike/antlr4-glp-grammar/Glp.g4`.

### GeneratedParser
- **Represents**: The parser front-end generated from `SharedGrammar` for a target runtime.
- **Fields**: target language (C# = primary; C++/Dart/Gleam = trial/deferred), generation command,
  runtime dependency (`Antlr4.Runtime.Standard`).
- **Rules**: Additive build artifact; MUST NOT replace `out/csharp/lib/compiler/parser.cs`
  (FR-010).
- **Location**: `spike/antlr4-glp-grammar/gen/`.

### CorpusExample
- **Represents**: One working GLP definition used in the parity harness.
- **Fields**: source path, accepted-by-hand-written (bool), accepted-by-generated (bool),
  is-negative-control (bool).
- **Rules**: The selected subset (research R3) MUST exercise declarations, guards, reader/writer
  modes, `::=` unions, `=..`/`..=`, module `#` calls, lists/structs, and ≥1 negative control.
- **Location**: `spike/antlr4-glp-grammar/corpus/` (files or a manifest referencing `programs/`).

### ILParityResult
- **Represents**: The per-example comparison of IL produced via the hand-written vs generated
  parser.
- **Fields**: example ref, hand-written IL (bytecode instruction sequence), generated IL,
  identical (bool), divergence-cause (text, when not identical).
- **Rules**: For every example accepted by both parsers, either identical == true or a
  divergence-cause is recorded (FR-004, SC-002).
- **Location**: harness output, summarized in `REPORT.md`.

### FeasibilityReport
- **Represents**: The authoritative spike deliverable.
- **Fields**: verdict (go/no-go), coverage summary (SC-001), IL-parity summary (SC-002),
  multi-target cost (C# proven; trials/deferrals), dependency posture (#11 + #4 delivered),
  residual risks, any §1.14 proposal reference.
- **Rules**: MUST be reviewable without running the spike (SC-003); MUST state an unambiguous
  verdict.
- **Location**: `spike/antlr4-glp-grammar/REPORT.md` (shape in `contracts/feasibility-report.md`).

### AbandonStub (removal target)
- **Represents**: The dead `AbandonOps.AbandonWriter` placeholder.
- **Fields**: file path, caller count (MUST be 0 before removal).
- **Rules**: Removed only after dead-confirmation (FR-007); if a caller exists → STOP/report
  (Bug-Protocol II).
- **Location**: `out/csharp/lib/runtime/abandon.cs`.

## State transitions

`SharedGrammar drafted → GeneratedParser built → Corpus parsed → ILParityResult recorded →
FeasibilityReport verdict`. Any point where faithful grammar expression requires an accepted-syntax
change transitions to **§1.14 GATE (STOP)** instead of proceeding.

`AbandonStub: present → dead-confirmed → removed → C# build green`. A found caller transitions to
**STOP/report** instead of removal.
