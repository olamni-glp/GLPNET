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
the §1.14 approval to author `Glp.g4`** — see `../PROPOSAL-1.14.md`. That gate is now cleared
(authored 2026-08-04, Gabi + Udi); SC-001 = 7/7 MATCH (`../RESULTS.md`).

## Expanded corpus (T014 — SC-002)

The expanded corpus is drawn IN PLACE from across `programs/` via a both-front-end accept filter
(the harness `--parity --corpus <dir>` sweeps every `*.glp` recursively; one-sided rejects are logged
as divergences with an attributed cause, never silently dropped — FR-008). Referenced in place, not
copied (single source of truth). Swept sets and results (`../RESULTS.md`):

| Corpus dir | Files | MATCH | Divergences (all bounded, 0 un-caused) |
|------------|-------|-------|----------------------------------------|
| `programs/tests/typed` | 71 | 70 | 1 — `policy_guard_formb.glp` (BC-1: `satisfiable` native guard) |
| `programs/lib` | 8 | 8 | 0 (all self-contained) |
| `programs/typed_book` | 223 | 175 | 48 — BC-1 (prelude/imported/native decls + 1 interleaved-clause) |

Plus a bespoke self-contained typed program added for the T016 mod-functor case:

| # | Path | Exercises |
|---|------|-----------|
| 8 | `programs/tests/typed/mod_functor_call.glp` | `mod(...)` call form (ATOM functor) + infix `mod` in one clause; `:=` arith; MATCH |

**Bounded conditions** (see `../RESULTS.md` "Summary & bounded conditions" and `../FINDINGS.md`):
**BC-1** — the isolated hand-parser enforces decl↔clause well-formedness semantics the pure-syntactic
ANTLR grammar does not (prelude/import/native context + decl-clause adjacency); every genuinely
self-contained file matches. **BC-2** — F-069-1 engine occurs-check (fuzz scoped to non-cyclic `=`,
DEC F3). **mod-functor** — RESOLVED (T016).
