<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# IL-parity results — SC-002 IL-parity bridge (feature 069)

**Toolchain**: dotnet 10.0.301; Antlr4.Runtime.Standard 4.13.1; ANTLR 4.13.2 (gen, -visitor); Java 17 (jdk-17.0.19+10)

## Representative corpus (7 files) — SC-001

| # | input | verdict | first-diff | cause |
|---|-------|---------|-----------|-------|
| 1 | append_dl.glp | MATCH |  |  |
| 2 | arith_comparison.glp | MATCH |  |  |
| 3 | arith_diseq.glp | MATCH |  |  |
| 4 | arith_guard_ground.glp | MATCH |  |  |
| 5 | abandon_stream.glp | MATCH |  |  |
| 6 | typed_social_agent.glp | MATCH |  |  |
| 7 | abandon_reader_bad.glp | MATCH |  |  |

**Totals**: 7/7 MATCH. Un-caused divergences (defects — FR-008): 0.

