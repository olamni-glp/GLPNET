<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Implementation Plan: glp-runtime-consol

**Branch**: `065-glp-runtime-consol` | **Date**: 2026-08-03 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/065-glp-runtime-consol/spec.md`

## Summary

Two independent scopes consolidating the last genuine GLP-runtime gaps from the 2026-08-03 gap
audit:

- **(A) ANTLR4 shared-grammar feasibility spike** — author a single `.g4` grammar covering the
  existing GLP token vocabulary (`out/csharp/lib/compiler/token.cs`), generate a C# parser
  front-end (trial C++/Dart/Gleam deferred), parse a corpus of working GLP definitions, and
  confirm the generated parser produces IL identical to the existing hand-written parser — or
  enumerate every divergence. Deliverable is a **go/no-go feasibility report + additive grammar
  prototype**, not a production parser replacement. **Gated by Constitution IV-a / DISCIPLINE
  §1.14**: any accepted-syntax change STOPS for a written Gabi + Udi proposal.
- **(B) Abandon dead-stub cleanup** — remove `out/csharp/lib/runtime/abandon.cs`
  (`AbandonOps.AbandonWriter` throws `NotImplementedException`) after confirming zero production
  callers. Abandon is delivered as the anonymous-writer discard semantic (062 US5).

Implementation sequences **B first** (no gate, low risk) then **A** (spike, STOP at the §1.14
gate before any syntax-affecting change).

## Technical Context

**Language/Version**: C# (.NET, `dotnet` 10.0.301) for the engine + generated parser target;
Dart 3.x for the reference parser/lexer mirror; ANTLR4 (tool ≥ 4.13, run under Java 17 at
`/c/Users/smbuser/java/jdk-17.0.19+10`); Gleam 1.17 / C++ as deferred trial targets.
**Primary Dependencies**: ANTLR4 tool jar (to be acquired — not yet vendored), `Antlr4.Runtime.Standard`
C# runtime (NuGet) for the generated parser; existing `out/csharp/lib/compiler` pipeline
(lexer→parser→PE→typecheck→analyzer→codegen) as the IL baseline.
**Storage**: N/A (spike artifacts are files under the spec's spike path; no DB writes).
**Testing**: `bash test/run_all_tests.sh` (REPL, `export DART=/c/src/flutter/bin/cache/dart-sdk/bin/dart`
first); C# engine `dotnet test`; the spike's own corpus-parse + IL-parity harness.
**Target Platform**: Windows dev host (Olamnit); runtimes are cross-platform.
**Project Type**: compiler/runtime (language toolchain) — spike + cleanup, additive.
**Performance Goals**: N/A (feasibility spike; correctness/parity, not throughput).
**Constraints**: Zero change to accepted GLP syntax without recorded §1.14 approval (IV-a).
Additive only — production hand-written parsers stay in place (FR-010). Pre-existing test
baselines stay green (FR-009).
**Scale/Scope**: token vocabulary = 123 lines (`token.cs`); hand-written parsers = 1918 (C#) /
1773 (Dart) lines; corpus source pool = 1175 `.glp` files (a representative typed subset is
selected for the parity harness, not all 1175).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Relevance | Status |
|-----------|-----------|--------|
| I. Spec-First | Spec exists, quoted, consistency-checked; both scopes trace to it. | PASS |
| II. Bug-Protocol / No-Workarounds | Edge cases (hidden abandon caller; IL divergence) route to STOP-and-report, not workarounds. | PASS |
| III. SRSW inviolable | No `skipSRSW`; grammar describes SRSW-conformant surface syntax only. | PASS (no token) |
| **IV-a. Language Authority** | **Grammar spike touches parser surface.** Spec FR-005 / SC-004 + acceptance US1-3 forbid any accepted-syntax change without a written §1.14 owner proposal. Spike is additive; production parsers untouched. | **PASS (gated)** |
| IV-b. Preserve Working Internals | Hand-written parsers, `_ClauseVar`, `_TentativeStruct` untouched; only dead `abandon.cs` removed after dead-confirmation. | PASS |
| V. Claude-Only LM | No LM-in-the-loop path; ANTLR/dotnet are deterministic tools. No `openai`/`litellm`. | PASS (no token) |
| VI-a/b. Persistence | No migrations, no second PGLite cluster; spike writes files only. | PASS |
| VII. Test-Gated, Commit-Scoped | Baseline-green before/after; stage-by-name commits; ship via GitFlow. | PASS |
| VIII. Single Source of Truth | Feasibility report is the one authoritative spike artifact; references the memo, does not duplicate. | PASS |

No violations. Complexity Tracking not required. The IV-a gate is a **runtime STOP condition
during implement**, not a plan-time violation: the plan's design keeps the spike additive and
syntax-preserving by construction.

## Project Structure

### Documentation (this feature)

```text
specs/065-glp-runtime-consol/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (spike entities)
├── quickstart.md        # Phase 1 output (how to run the spike + cleanup)
├── contracts/
│   ├── grammar-spike.md         # the .g4 grammar + IL-parity harness contract
│   └── feasibility-report.md    # required shape of the go/no-go report
└── tasks.md             # Phase 2 output (/bk-tasks — NOT created here)
```

### Source Code (repository root)

```text
# Scope A — additive spike artifacts (NEW; production parsers untouched)
spike/antlr4-glp-grammar/
├── Glp.g4                       # the shared grammar (tokens + rules) for the corpus under test
├── gen/                         # ANTLR4-generated C# parser front-end (build artifact)
├── harness/                     # corpus-parse + IL-parity comparison harness (C#)
├── corpus/                      # selected representative working-GLP examples (or a manifest)
└── REPORT.md                    # the feasibility go/no-go report (mirrors contracts/feasibility-report.md)

# Existing baselines READ by the spike (NOT modified):
out/csharp/lib/compiler/{token.cs,parser.cs,lexer.cs,compiler.cs}   # C# hand-written pipeline + IL
glp_runtime/lib/compiler/{parser.dart,lexer.dart}                    # Dart mirror

# Scope B — dead-code removal
out/csharp/lib/runtime/abandon.cs                                    # REMOVED after dead-confirmation
```

**Structure Decision**: A single spike directory `spike/antlr4-glp-grammar/` isolates all
Scope-A artifacts so they are unambiguously additive and never confused with the production
`out/csharp/lib/compiler/` parsers (FR-010). Scope B is a targeted single-file removal. Both live
in the existing repo layout; no new top-level project.

## Complexity Tracking

> Not required — Constitution Check has no unjustified violations.
