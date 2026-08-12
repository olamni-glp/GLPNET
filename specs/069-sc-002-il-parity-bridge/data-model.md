<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Phase 1 Data Model: SC-002 IL-parity bridge

This feature transforms data through a fixed sequence of representations; the "entities" are those
representations plus the mapping and result records. No persistent store is involved.

## Representations (the transform chain)

| Entity | What it is | Produced by | Consumed by |
|--------|-----------|-------------|-------------|
| **Source text** | a `.glp` program (corpus file or fuzz output) | corpus / fuzzer | both front-ends |
| **GrammarParseTree** | ANTLR `GlpParser` parse tree | shared-grammar front-end (`gen/`) | the lowering bridge |
| **EngineAst** | engine term/clause nodes (`out/csharp/lib/compiler/ast.cs`) | bridge (from parse tree) **and** production parser (`parser.cs`) | shared downstream pipeline |
| **CompiledIl** | `BytecodeProgram` | shared pipeline (`compiler.cs`/`codegen.cs`) | il-codec serializer |
| **IlBytes** | canonical byte serialization of `CompiledIl` | `csharp/glp_il_codec/IlCodec.cs` | the parity comparator |

Invariant: the EngineAst produced by the bridge and by the production parser feed the **same**
pipeline instance/configuration; only the front-end differs. Any IlBytes difference is therefore
attributable to the front-end (bridge correctness or a bounded tokenization condition), never to
downstream drift.

## LoweringMapping (the core of the bridge)

A total function from grammar rule → engine AST node constructor. Every rule in `Glp.g4` MUST have an
entry; an unmapped rule is a defect, not a silent pass-through.

| Grammar rule (Glp.g4) | Engine AST node (ast.cs) | Notes |
|-----------------------|--------------------------|-------|
| module / directive | module + directive nodes | soft-keyword predicates preserved (REPORT §6) |
| clause (fact / rule) | clause node (head, guards, body) | three-phase HEAD/GUARD/BODY split |
| head / goal / call | struct/atom/call node | functor + args |
| guard conjunction | guard list | pure tests |
| body conjunction | body goal list | `,`-separated goals |
| term: struct | struct node | functor(args) |
| term: list | list node | `[H|T]`, `[]`, nested, struct elements |
| term: var / anon `_` | variable node (writer/reader) | `?` reader marking |
| term: number / string / atom | constant node | typed constant |
| operator expr (arith/compare/`mod`/`=..`/`:=`/`=`) | operator node | `mod`-functor per D5 |
| type-alternative / type def | type node | deep nesting per D6 fuzz target |

*(The exact rule list is finalized against `Glp.g4` at implementation time; the count is ~22 per
REPORT §3. This table is the coverage contract, not an approximation to shortcut.)*

## Result records

- **ParityResult**: `{ input_id, verdict: MATCH | DIVERGE, first_diff_offset?, cause? }`. One per
  corpus/fuzz input. `cause` is set only for a DIVERGE that traces to a documented bounded condition
  (e.g. `mod`-functor if D5's fix is deferred); an un-caused DIVERGE is a defect (FR-008).
- **CorpusEntry**: `{ file, source: typed_book|lib|plays|tests-typed|per-construct, constructs_covered[] }`. The union
  of `constructs_covered` across all entries MUST cover every enumerated grammar construct (FR-005
  coverage floor).
- **FuzzInput**: `{ index, seed, target_corner: var-vs-comparison | deep-type-alt, source_text }`.
  Deterministic from `index`+`seed`.
- **AdoptionDecision**: `{ verdict: adopt | adopt-with-conditions | do-not-adopt, evidence_refs[],
  bounded_conditions[] }`. `bounded_conditions` MUST enumerate Dart-target maturity, Gleam-not-ANTLR,
  and any deferred `mod`-functor condition (FR-011).

## Reviewable artifacts (system of record — files, not a DB)

- `spike/antlr4-glp-grammar/corpus/MANIFEST.md` — CorpusEntry list + coverage checklist.
- `spike/antlr4-glp-grammar/RESULTS.md` — ParityResult table for corpus + fuzz summary (SC-006).
- `spike/antlr4-glp-grammar/DECISION.md` — the AdoptionDecision (SC-004).
