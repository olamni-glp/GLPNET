<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Implementation Plan: SC-002 IL-parity bridge

**Branch**: `069-sc-002-il-parity-bridge` | **Date**: 2026-08-06 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/069-sc-002-il-parity-bridge/spec.md`

## Summary

Build a lowering bridge that maps the feature-065 shared ANTLR grammar's parse tree
(`GlpParser`, generated into `spike/antlr4-glp-grammar/gen/`) to the production engine's own AST
(`out/csharp/lib/compiler/ast.cs`), so that BOTH front-ends — the shared-grammar front-end (via the
bridge) and the production hand-written parser (`out/csharp/lib/compiler/parser.cs`) — feed the
identical, unchanged downstream pipeline (SRSW → partial-eval → type-check → compile) and emit
`BytecodeProgram`. Parity is proven example-by-example by serializing both outputs with the delivered
`glp_il_codec` (`csharp/glp_il_codec/IlCodec.cs`) and comparing for byte-identity. The feature then
expands the parity corpus, fuzzes the prediction-sensitive corners, and delivers a written
production-adoption decision. Production parsers stay untouched throughout (FR-010).

## Technical Context

**Language/Version**: C# / .NET 10 (dotnet 10.0.301); ANTLR 4.13.2 (grammar codegen, vendored jar) +
`Antlr4.Runtime.Standard` 4.13.1; Java 17 (OpenJDK, `~/java/jdk-17.0.19+10`) to regenerate the parser.
**Primary Dependencies**: generated ANTLR parser (`spike/antlr4-glp-grammar/gen/`); engine compiler
pipeline (`out/csharp/lib/compiler/` — `ast.cs`, `parser.cs`, `partial_evaluator.cs`, `analyzer.cs`,
`compiler.cs`, `codegen.cs`); `glp_il_codec` deterministic serialization (equality oracle, delivered);
compiled-IL-on-the-wire (feature 062, delivered).
**Storage**: N/A — the parity corpus is a set of `.glp` files on disk; results are committed Markdown.
**Testing**: xUnit for the bridge + parity comparator; the runnable parity harness
(`spike/antlr4-glp-grammar/harness/`, `dotnet run`); the GLP REPL regression suite
(`test/run_all_tests.sh`) as an unchanged-baseline guard (production is not modified, so it must stay
green).
**Target Platform**: Windows dev host, .NET 10 runtime.
**Project Type**: compiler tooling — a parse-tree→AST lowering bridge plus a parity/fuzz harness.
**Performance Goals**: not latency-bound; the full-corpus parity run completes in seconds; the fuzz
gate runs a default budget of 10,000 generated inputs.
**Constraints**: byte-identical `BytecodeProgram` is the parity standard; production parsers and the
accepted GLP surface syntax MUST remain untouched (FR-010 / SC-005); any grammar-lexer change for the
`mod`-functor case is tokenization engineering of existing syntax, not an accepted-syntax change
(consistent with REPORT §6 and DISCIPLINE §1.14).
**Scale/Scope**: ~250–400 LOC bridge (~22 visitor methods, one per grammar rule) + pipeline-invocation
glue; expanded corpus (accepted book/lib/plays + per-construct programs); bounded 10k-input fuzz.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Assessment | Verdict |
|-----------|------------|---------|
| I. Spec-First | Spec exists, grounded verbatim in `spike/antlr4-glp-grammar/REPORT.md` §3/§7; plan traces to it. | PASS |
| II. Bug-Protocol / No-Workarounds | FR-008 requires every IL divergence be diagnosed to root cause and fixed or reported as a bounded finding — no masking. No try/catch "robustness" over divergences. | PASS |
| III. SRSW inviolable | No GLP clauses are authored; this is C# tooling over GLP source. No `skipSRSW` token anywhere in the artifacts. | PASS (N/A) |
| IV-a. Language Authority | FR-010/SC-005: production parsers untouched, zero accepted-syntax change. The bridge is downstream of parse; the `mod`-functor lexer fix (FR-007) is tokenization of *existing* syntax (REPORT §6), not a new guard/predicate/kernel/directive. The grammar itself was already §1.14-approved in 065 — because that edit re-touches the owner-approved `Glp.g4`, T016 carries an explicit propose-first §1.14 re-confirm before landing. | PASS |
| IV-b. Preserve Working Internals | The production engine, parser, and all load-bearing internals are read-only inputs to the bridge; nothing is removed. | PASS |
| V. Claude-Only LM / No External API | No LM in the loop; fuzzing is deterministic grammar-driven generation, not model-backed. No `openai`/`litellm`/`OPENAI_API_KEY`. | PASS |
| VI-a/b. Persistence | No DB migrations; no new PGLite cluster; corpus is plain files. | PASS (N/A) |
| VII. Test-Gated, Commit-Scoped Shipping | Baseline green before change, re-test after; commit only feature files; ship via GitFlow. | PASS (methodology) |
| VIII. Single Source of Truth & Traceability | REPORT is the authoritative spike source; feature traces roadmap→pipeline→tasks; results/decision are single committed artifacts. | PASS |

**No violations.** Complexity Tracking is empty.

## Project Structure

### Documentation (this feature)

```text
specs/069-sc-002-il-parity-bridge/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (bridge node-mapping + entities)
├── quickstart.md        # Phase 1 output (how to run the parity + fuzz gates)
├── contracts/           # Phase 1 output (lowering + parity-oracle + fuzz contracts)
└── tasks.md             # Phase 2 output (/bk-tasks — NOT created here)
```

### Source Code (repository root)

```text
spike/antlr4-glp-grammar/
├── Glp.g4                    # shared grammar (existing; lexer touched ONLY for FR-007 mod-functor)
├── gen/                      # generated ANTLR C# parser (existing; regenerated if Glp.g4 changes)
├── bridge/                   # NEW — the lowering bridge project
│   ├── GlpLoweringVisitor.cs #   ANTLR parse tree -> engine AST (ast.cs nodes), ~22 rule visitors
│   ├── PipelineDriver.cs     #   invokes the shared downstream pipeline on the lowered AST
│   └── Bridge.csproj         #   references gen/ + out/csharp/lib/compiler + csharp/glp_il_codec
├── parity/                   # NEW — the IL-parity comparator + fuzz
│   ├── IlParityComparator.cs #   both front-ends -> BytecodeProgram -> IlCodec bytes -> compare
│   ├── GrammarFuzzer.cs      #   bounded generative fuzz over prediction-sensitive corners
│   └── Parity.csproj
├── corpus/                   # EXPANDED — MANIFEST.md + accepted programs/ (typed_book,lib,plays,tests/typed) + per-construct files
├── harness/                  # EXISTING coverage harness, extended to drive the parity run
└── RESULTS.md, DECISION.md   # NEW — reviewable per-file parity table + adoption decision (SC-004/006)
```

**Structure Decision**: All new code lives under the existing spike (`spike/antlr4-glp-grammar/`),
alongside the grammar, generated parser, and harness it depends on. Nothing under `out/csharp/lib/`
(production) or the GLP `programs/` tree is modified except by read-only reference. This is the
simplest layout that satisfies FR-010 (production untouched) while keeping the bridge, comparator,
corpus, and decision co-located with their 065 spike lineage.

## Complexity Tracking

> No Constitution Check violations — section intentionally empty.
