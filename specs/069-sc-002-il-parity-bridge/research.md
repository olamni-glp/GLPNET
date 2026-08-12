<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 0 Research: SC-002 IL-parity bridge

All Technical Context items are resolved from the delivered spike (`spike/antlr4-glp-grammar/`),
the engine compiler (`out/csharp/lib/compiler/`), and the delivered `glp_il_codec`. No unknowns
remain (no NEEDS CLARIFICATION markers).

## D1 — Lowering mechanism: ANTLR visitor, parse tree → engine AST

- **Decision**: Implement the bridge as an ANTLR `GlpParserBaseVisitor<AstNode>` that returns an
  engine AST node (`out/csharp/lib/compiler/ast.cs` node kinds) for each grammar rule, building the
  tree bottom-up (~22 visitor methods, one per rule).
- **Rationale**: A visitor returns values, so a rule visitor composes child results into the parent
  engine-AST node directly — the natural shape for a tree-to-tree transform. It maps 1:1 onto the
  REPORT §3 estimate ("one visitor method per grammar rule → engine node").
- **Alternatives considered**: (a) ANTLR *listener* — event-based, no return values, would need a
  side stack to assemble nodes; more code, more error-prone. (b) Re-target the production parser to
  emit from the ANTLR tree — rejected: mutates production (violates FR-010).

## D2 — Equality oracle: byte-identical BytecodeProgram via IlCodec

- **Decision**: Serialize each front-end's compiled `BytecodeProgram` with
  `csharp/glp_il_codec/IlCodec.cs` and compare the resulting bytes for exact equality; on mismatch,
  report the first differing byte/instruction offset.
- **Rationale**: `il-codec` provides deterministic `BytecodeProgram` (de)serialization and is exactly
  the oracle REPORT §5 identifies for this comparison; it is already delivered.
- **Alternatives considered**: structural AST comparison (compares the wrong stage — the target
  artifact is IL, not AST) and textual disassembly diff (non-canonical, whitespace-sensitive). Both
  rejected in favour of the canonical byte oracle.

## D3 — Shared downstream pipeline reuse; no new engine capability

- **Decision**: Feed the lowered engine AST into the existing compile path
  (`partial_evaluator.cs` → `analyzer.cs` → `compiler.cs`/`codegen.cs`) unchanged, the same path the
  production parser feeds.
- **Rationale**: REPORT §3 states the downstream pipeline is shared and unchanged and "no new engine
  capability is required"; reusing it is what makes the IL comparison meaningful (identical pipeline,
  only the front-end differs).
- **Alternatives considered**: a parallel compile path for the bridge — rejected: it would make an IL
  match prove nothing about the production pipeline.

## D4 — Corpus expansion: accepted real programs + per-construct floor

- **Decision**: The expanded corpus = every `programs/` book/lib/plays file that BOTH front-ends
  accept, PLUS ≥1 dedicated program per distinct guard, operator, and type-alternative construct
  enumerated from `Glp.g4`. Coverage is complete when every enumerated construct appears in ≥1 corpus
  program (clarification 2026-08-06).
- **Rationale**: real programs exercise realistic construct combinations; the per-construct floor
  guarantees no corner is silently uncovered (the REPORT §7 "corpus breadth" residual risk). It gives
  a measurable done-condition instead of "exhaustive".
- **Alternatives considered**: a fixed file count (arbitrary, not coverage-based) — rejected.

## D5 — `mod`-as-functor tokenization: lexer predicate/island first

- **Decision**: Resolve the `mod`-functor divergence (REPORT §6) with a lexer predicate / island so
  `mod` immediately followed by `(` tokenizes as a functor atom, else as the `MOD` operator —
  reproducing the hand-lexer. Only if this proves infeasible within scope is it recorded as an
  explicit bounded non-adoption condition (clarification 2026-08-06).
- **Rationale**: production adoption requires `mod(...)` call forms to compile identically; the
  hand-lexer already does exactly this peek. This is tokenization of existing syntax, not an
  accepted-syntax change (DISCIPLINE §1.14; REPORT §6).
- **Alternatives considered**: leave `mod` always `MOD` (spike behaviour — parity only holds because
  the spike corpus never calls `mod(...)`); rejected as the default since the expanded corpus must
  include the call form.

## D6 — Fuzzing: bounded grammar-driven generation, halt-on-divergence

- **Decision**: A bounded generative fuzz (default budget 10,000 inputs) drives valid programs
  targeting the two ALL(*)-prediction-sensitive corners — variable-versus-comparison dispatch and
  deep type-alternative nesting — through both front-ends; any IL divergence halts the run and is
  captured for diagnosis. Determinism is achieved with a fixed seed sequence (no `Math.random`-style
  nondeterminism), varying inputs by index.
- **Rationale**: REPORT §7 flags exactly these corners as reliant on ANTLR full-context prediction and
  "worth adversarial fuzzing before adoption". A fixed budget gives SC-003 a measurable gate.
- **Alternatives considered**: unbounded / coverage-guided fuzzing — rejected (no stop criterion, not
  measurable for a PREP feature).

## D7 — Regression safety: production baseline stays green

- **Decision**: Treat `test/run_all_tests.sh` (REPL suite, 546–547 baseline) as an unchanged-baseline
  guard; since production is not modified (FR-010), it MUST remain green. The bridge/parity/fuzz code
  is exercised by its own xUnit + `dotnet run` harness.
- **Rationale**: Constitution VII (test-gated) + the fact that a green production baseline is evidence
  FR-010 held.
