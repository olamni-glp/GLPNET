<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Corpus manifest — antlr4-glp-grammar spike (T009)

Representative typed subset from `programs/` selected to exercise the grammar's distinctive
constructs. Files are referenced in place (not copied — single source of truth per CLAUDE.md).

| # | Path | Exercises |
|---|------|-----------|
| 1 | `programs/tests/typed/append_dl.glp` | difference lists (`\`), clauses, recursion, reader/writer modes |
| 2 | `programs/tests/typed/arith_comparison.glp` | arithmetic comparison guards (`<`,`>`,`=<`,`>=`), `:=` |
| 3 | `programs/tests/typed/arith_diseq.glp` | arithmetic (dis)equality (`=:=`, `=\=`) |
| 4 | `programs/tests/typed/arith_guard_ground.glp` | `ground/1` guard, guard separator `|` |
| 5 | `programs/tests/typed/abandon_stream.glp` | anonymous-writer discard (`_`), streams/lists |
| 6 | `programs/tests/typed/cssg_precise/typed_social_agent.glp` | `procedure`/type decls (`::=`), structs, guards, unions |
| 7 (negative control) | `programs/tests/typed/abandon_reader_bad.glp` | a file the hand-written parser/checker REJECTS — the generated parser must reject-or-diverge identically |

**Selection rationale**: covers declarations (`procedure`, `::=`), guards (arith + `ground`),
reader/writer `?` modes, `=..`/`..=` (to be added when a univ example is confirmed), module `#`
calls, lists/structs/difference-lists, anonymous `_`, and ≥1 negative control — per research R3.

**Note (spike status)**: the corpus is fixed here as the intended parity set. Running the
generated parser against it (coverage SC-001) and computing IL parity (SC-002) is **gated behind
the §1.14 approval to author `Glp.g4`** — see `../PROPOSAL-1.14.md`.
