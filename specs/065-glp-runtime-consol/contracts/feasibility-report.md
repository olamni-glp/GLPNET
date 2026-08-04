<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Contract: Feasibility report shape (`spike/antlr4-glp-grammar/REPORT.md`)

The report is the authoritative Scope-A deliverable. It MUST be reviewable without running the
spike (SC-003) and contain, in order:

1. **Verdict** — one of `GO` / `NO-GO` / `GO-WITH-CONDITIONS`, with a one-paragraph justification.
2. **Grammar coverage** — the corpus manifest; per-example accepted-by-hand-written vs
   accepted-by-generated; the coverage percentage (SC-001) and any non-covered construct with
   cause.
3. **IL parity** — for doubly-accepted examples, identical-IL count/percentage (SC-002) and an
   enumerated list of every divergence with its cause.
4. **Multi-target cost** — C# (primary) result; C++/Dart/Gleam trial outcome or explicit deferral
   with rationale.
5. **Dependency posture** — confirmation that compiled-IL (#11) and il-codec (#4) are delivered and
   were used for the comparison; any residual dependency.
6. **§1.14 status** — whether any accepted-syntax change was needed; if so, a reference to the
   written owner proposal (and confirmation that no such change was made without approval).
7. **Residual risks & recommendation** — what a future production-adoption (PREP/REFACTOR) feature
   would need; risks (e.g. Dart-target ANTLR maturity, corpus breadth).

## Success gate for the report

- States an unambiguous verdict (SC-003).
- Zero accepted-syntax changes recorded as landed without approval (SC-004).
