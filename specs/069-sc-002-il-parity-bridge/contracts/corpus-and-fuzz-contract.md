<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: Corpus coverage floor & bounded fuzz

**Components**: `spike/antlr4-glp-grammar/corpus/` + `parity/GrammarFuzzer.cs` (FR-005, FR-006, SC-002, SC-003)

## Corpus coverage floor (FR-005 / clarification 2026-08-06)

- **C1 — Real programs**: the corpus includes every `programs/` book/lib/plays `.glp` file that BOTH
  front-ends accept at parse level. Files rejected by one front-end are logged as divergences, not
  silently dropped.
- **C2 — Per-construct floor**: for every distinct guard, operator, and type-alternative construct
  enumerated from `Glp.g4`, at least one corpus program exercises it. `MANIFEST.md` carries the
  enumerated-construct checklist; coverage is COMPLETE only when every construct is ticked by ≥1
  entry.
- **C3 — `mod`-functor**: the corpus MUST include `mod(...)` call forms (not only infix `mod`), so
  the D5 tokenization fix is actually exercised.
- **C4 — Negative controls preserved**: the spike's negative control (parse-accept / semantic-reject)
  stays in the corpus and must reject identically on both front-ends.

## Bounded fuzz (FR-006 / SC-003)

- **F1 — Targets**: generation is focused on the two ALL(*)-prediction-sensitive corners flagged in
  REPORT §7 — variable-versus-comparison dispatch, and deep type-alternative nesting.
- **F2 — Budget**: default 10,000 generated inputs (configurable). The gate is: complete the full
  budget with zero un-caused IL divergences.
- **F3 — Determinism**: inputs are a deterministic function of `index` + a fixed seed (no
  `Math.random`/wall-clock nondeterminism), so a run is reproducible and a divergence is replayable.
- **F4 — Halt-on-divergence**: the first un-caused divergence halts the run and captures the exact
  input for diagnosis (FR-008); the run is not "mostly green — continue".
- **F5 — Valid-program generation**: generated inputs are syntactically valid GLP (both front-ends
  should accept); the fuzz probes IL parity, not parser error-recovery.
