<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: ANTLR4 GLP grammar spike + IL-parity harness

## Grammar contract (`spike/antlr4-glp-grammar/Glp.g4`)

- **Input alphabet**: the token vocabulary of `out/csharp/lib/compiler/token.cs` (123 lines). Every
  token used by a corpus example MUST have a corresponding lexer rule. GLP-specific tokens that
  MUST be present: `READER` (`?`), `GUARD_SEP` (`|`), `COLONCOLONEQ` (`::=`), `UNIV` (`=..`),
  `UNIV_DECOMPOSE` (`..=`), `AT_LESS`/`AT_GREATER`/`AT_LESS_EQUAL`/`AT_GREATER_EQUAL`
  (`@<`,`@>`,`@=<`,`@>=`), `HASH` (`#`), `PROCEDURE`, and the standard clause/term punctuation.
- **Production rules**: MUST accept the language accepted by the hand-written recursive-descent
  parser (`parser.cs`/`parser.dart`) for the corpus under test — no more, no less. Faithful
  description only.
- **Constraint (IV-a / §1.14)**: The grammar MUST NOT define or accept any construct the
  hand-written parser rejects, nor reject any it accepts, without a recorded owner approval. A
  discovered need to do so is a STOP condition, not a grammar edit.

## Harness contract (`spike/antlr4-glp-grammar/harness/`)

- **parse(example) → {accepted: bool, ast}** for the generated parser; compared against the
  hand-written parser's accept/reject for the same example.
- **il(example, parser) → BytecodeProgram** — compile via the shared downstream pipeline
  (CodeGenerator/analyzer unchanged), varying only the parser front-end.
- **compare(handIL, genIL) → {identical: bool, cause?}** — structural/byte comparison of the
  bytecode instruction sequences (uses the delivered il-codec #4 serialization for deterministic
  comparison).
- **Output**: one `ILParityResult` row per example; aggregated into `REPORT.md`.

## Acceptance (maps to spec)

- Every corpus example accepted by the hand-written parser is accepted by the generated parser, or
  enumerated with cause (SC-001, US1-1).
- Every doubly-accepted example has identical IL or a recorded divergence-cause (SC-002, US1-2).
- No accepted-syntax change lands without a §1.14 approval (SC-004, US1-3).
