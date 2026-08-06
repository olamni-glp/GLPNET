<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: Lowering bridge (parse tree → engine AST)

**Component**: `spike/antlr4-glp-grammar/bridge/GlpLoweringVisitor.cs` (FR-001, FR-002)

## Interface

```
AstNode Lower(GlpParser.ModuleContext parseTree)
```

Input: an ANTLR `GlpParser` parse tree for a single module (produced by the generated front-end in
`gen/`). Output: the root engine AST node (`out/csharp/lib/compiler/ast.cs`) that the production
parser would produce for the same source.

## Guarantees (MUST)

- **G1 — Total rule coverage**: every rule in `Glp.g4` has a corresponding visitor method. A parse
  tree node whose rule has no visitor is a hard error (throw), never a silent skip or a generic
  pass-through node.
- **G2 — Structural fidelity**: the emitted AST uses the same node kinds, functor/arity, and
  writer/reader (`?`) marking the production parser emits, so the downstream pipeline cannot
  distinguish the two front-ends.
- **G3 — No downstream mutation**: the bridge only constructs `ast.cs` nodes; it does not modify the
  partial evaluator, analyzer, compiler, or codegen (FR-002, Constitution IV-b).
- **G4 — No production mutation**: `parser.cs`, `lexer.cs`, and `token.cs` under `out/csharp/lib/`
  are read-only references (FR-010). The only lexer change permitted is inside the shared grammar's
  own lexer for the `mod`-functor case (see `fuzz-contract.md` is not the place — see the parity
  contract and research D5), and even that touches `Glp.g4`, never production.

## Verification

Per corpus/fuzz input, lower via the bridge, compile, and compare IL bytes to the production
front-end's (see `parity-oracle-contract.md`). G1 is additionally asserted by a static check that the
visitor overrides one method per `Glp.g4` rule.
