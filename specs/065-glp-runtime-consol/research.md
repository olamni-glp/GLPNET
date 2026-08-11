<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 0 Research: glp-runtime-consol

## R1 — ANTLR4 toolchain availability

- **Decision**: Run the ANTLR4 tool (`antlr-4.13.x-complete.jar`) under the installed Java 17
  (`/c/Users/smbuser/java/jdk-17.0.19+10/bin/java`); target C# via the `-Dlanguage=CSharp` option
  and consume the generated parser with the `Antlr4.Runtime.Standard` NuGet runtime under
  `dotnet` 10.0.301.
- **Rationale**: Java 17 and dotnet 10 are both present. ANTLR4's C# target is mature and its
  generated parser integrates as a normal NuGet dependency, so the harness is a standard `dotnet`
  project. The C# target matches the as-built engine (the IL baseline is C#).
- **Alternatives considered**: (a) ANTLR4 Dart target — the Dart runtime for ANTLR4 is less
  maintained; deferred to a trial. (b) Python/JS targets — irrelevant to the engine's IL. (c) A
  hand-rolled PEG — defeats the "single-sourced grammar" purpose.
- **Open action**: the ANTLR jar is **not yet vendored**. Acquiring it (download or vendor under
  the spike dir) is task T-setup. If acquisition is blocked in-environment, the spike degrades to
  a written grammar + a manual coverage argument (documented limitation per spec Assumption).

## R2 — What the grammar must cover (formal vocabulary)

- **Decision**: Derive the grammar's lexer rules from `out/csharp/lib/compiler/token.cs` (123
  lines; the concrete token vocabulary) and the parser rules from the structure of the
  hand-written recursive-descent parsers (`parser.cs` 1918 lines / `parser.dart` 1773 lines).
- **Rationale**: The token set is the authoritative surface vocabulary — GLP-specific tokens
  include `READER` (`X?`), `GUARD_SEP` (`|`), `UNIV`/`UNIV_DECOMPOSE` (`=..`/`..=`), the standard-
  order comparisons (`@<`, `@>`, `@=<`, `@>=`), `COLONCOLONEQ` (`::=`), `PROCEDURE`, `HASH` (`#`),
  etc. (per memo §Seed-vs-dossier-vs-code #2). Covering exactly this vocabulary keeps the grammar
  faithful to the accepted syntax and satisfies IV-a.
- **Alternatives considered**: Inferring the grammar from the corpus alone — rejected: it would
  under-cover constructs not present in the sample and risk silently changing accepted syntax.

## R3 — Corpus selection for the parity harness

- **Decision**: Select a **representative typed subset** from `programs/tests/typed/` (and a few
  book/lib examples) rather than all 1175 `.glp` files. The subset must exercise: procedure/type
  declarations, guards, reader/writer modes, `::=` type unions, `=..`/`..=`, module `#` calls,
  lists/structs, and negative-control files (e.g. `abandon_reader_bad.glp`) that the hand-written
  parser rejects.
- **Rationale**: The spike's goal is a feasibility verdict, not exhaustive validation. A curated
  subset covering the grammar's constructs gives a high-signal coverage + IL-parity result at
  bounded cost. The corpus manifest is recorded so the result is reproducible.
- **Alternatives considered**: All 1175 files — disproportionate for a spike and dominated by
  duplicative constructs; deferred to any future production-adoption feature.

## R4 — IL/identical-output comparison method

- **Decision**: Execute-equivalence at the IL level: compile each corpus example through
  (a) the existing hand-written pipeline and (b) a pipeline where only the parser front-end is the
  ANTLR4-generated one, feeding both into the **same** downstream `CodeGenerator`/analyzer, and
  compare the emitted `BytecodeProgram` instruction sequences for byte/structural identity.
- **Rationale**: This isolates the parser as the single variable — identical IL proves the
  generated parser produced an AST semantically equivalent to the hand-written one. The memo
  (§ classification #7) records this exact method and notes it leans on the il-codec (#4)
  round-trip foundation, **which is already delivered** (wave-4/062, per spec Assumption), so the
  bytecode can be serialized/compared deterministically.
- **Alternatives considered**: AST structural diff — rejected: the two parsers may build
  differently-shaped ASTs that still compile to identical IL; IL is the semantic ground truth.
  Source round-trip — rejected: not a semantic equivalence.

## R5 — §1.14 / IV-a gate handling

- **Decision**: The grammar is authored to describe the **existing** accepted syntax. If, while
  writing it, a construct cannot be expressed without altering what the language accepts, the
  spike STOPS and emits a written proposal to Gabi + Udi (DISCIPLINE §1.14 / Constitution IV-a)
  before any change. This is a runtime STOP condition during `/bk-implement`.
- **Rationale**: The grammar spike is explicitly a language-surface-adjacent activity; the gate is
  the project's non-negotiable control. Faithful description (not redesign) keeps the spike inside
  the gate.
- **Alternatives considered**: "Improve" the syntax opportunistically during the spike — forbidden
  by IV-a without approval.

## R6 — Scope B: confirming the abandon stub is dead

- **Decision**: Before removing `out/csharp/lib/runtime/abandon.cs`, grep the C# tree for
  `AbandonOps` / `AbandonWriter` references; confirm zero production callers; confirm abandon is
  provided by the anonymous-writer discard semantic (062 US5). Remove the file, rebuild the C#
  solution to zero errors, re-run baselines.
- **Rationale**: Bug-Protocol (II) — if a caller exists, the stub is not dead; STOP and report
  rather than delete. The 062 US5 delivery is the authoritative alternative implementation.
- **Alternatives considered**: Implement the stub instead of removing it — rejected: FCP has no
  dedicated abandon op; implementing one would be an unrequested language/runtime addition (IV-a).

## Resolved unknowns

All Technical Context items are resolved; no NEEDS CLARIFICATION remains. The only open action is
the ANTLR jar acquisition (R1), tracked as a task with a documented degradation path.
