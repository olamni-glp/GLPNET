<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: IL-parity oracle & comparator

**Component**: `spike/antlr4-glp-grammar/parity/IlParityComparator.cs` (FR-003, FR-004, FR-008, FR-009)

## Interface

```
ParityResult Compare(string sourceGlp)
```

Steps (both front-ends, identical downstream pipeline):
1. `astA = GlpLoweringVisitor.Lower(GlpParser(source))`   — shared-grammar front-end via bridge
2. `astB = GlpRuntime.Compiler.Parser.ParseModule(source)` — production front-end
3. `ilA = Compile(astA)`, `ilB = Compile(astB)`           — same compile path (research D3)
4. `bytesA = IlCodec.Serialize(ilA)`, `bytesB = IlCodec.Serialize(ilB)` — canonical oracle (D2)
5. compare `bytesA` vs `bytesB`

## Guarantees (MUST)

- **P1 — Byte-identity is the standard**: verdict is `MATCH` iff `bytesA == bytesB` byte-for-byte.
  Semantically-equal-but-differently-serialized IL is a `DIVERGE`, not a pass (spec Assumptions).
- **P2 — First-diff localization**: a `DIVERGE` result MUST carry the first differing offset (and the
  decoded instruction at that offset when available), not just a boolean (FR-004).
- **P3 — No silent acceptance**: a `DIVERGE` is a defect to diagnose to root cause and fix in the
  bridge, UNLESS it traces to a documented bounded condition, in which case `cause` is set and the
  condition is recorded in `RESULTS.md`/`DECISION.md` (FR-008).
- **P4 — Both-accept precondition**: parity is only asserted for inputs BOTH front-ends parse. An
  input rejected by exactly one front-end at parse level is itself a divergence (front-ends must agree
  on acceptance); an input rejected by BOTH downstream (e.g. an SRSW violation) must be rejected
  identically — the comparison covers the shared pipeline up to the identical rejection.
- **P5 — Reviewable output**: every `Compare` result is appended to `RESULTS.md` in a committed table
  so the full result set is auditable without re-running the harness (FR-009, SC-006).

## Scope for SC-001 / SC-002

- SC-001: 7-file representative corpus → 100% MATCH.
- SC-002: expanded corpus (coverage floor per `corpus-contract.md`) → 100% MATCH or caused-DIVERGE,
  zero un-caused DIVERGE.
