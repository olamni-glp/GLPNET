<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Production-adoption decision — ANTLR4 shared-grammar front-end (feature 069, US3 / T020)

**Feature**: `069-sc-002-il-parity-bridge` · **Authority to ratify**: Gabi + Udi (DISCIPLINE §1.14).
**Supersedes the open item in** `REPORT.md` §7 ("SC-002 unproven until the bridge exists").

---

## Verdict — **ADOPT-WITH-CONDITIONS**

The ANTLR4 shared grammar (`Glp.g4`) + the parse-tree→engine-AST lowering bridge produce **byte-identical
compiled IL** to the production hand-written front-end everywhere both front-ends accept a program, with
**zero un-caused divergences** across every corpus and a 10 000-case bounded fuzz. The grammar+lowering
path is therefore sound enough to adopt for the **C# target**, **subject to the bounded conditions below**
— chiefly porting the hand-parser's post-parse semantic checks (BC-1) and completing per-target work for
Dart, plus an explicit non-ANTLR path for Gleam. This is not an unconditional GO and not a do-not-adopt.

---

## Evidence (all in `RESULTS.md`, committed — SC-006)

| Success criterion | Result | Source |
|---|---|---|
| **SC-001** — 7-file representative corpus byte-identical IL | **7/7 MATCH** | RESULTS.md "Representative corpus" |
| **SC-002** — expanded corpus byte-identical IL (both-accepted files) | tests/typed **71/72**, lib **8/8**, typed_book **175/223**, dynamic_dispatch **4/4** — **every one-sided reject is BC-1-bounded; 0 un-caused divergences** | RESULTS.md "Expanded corpus …" |
| **SC-003** — bounded fuzz (10 000), zero unexplained IL divergences | **PASS** — 5623 valid IL MATCH, 4377 both-reject, **0 un-caused** | RESULTS.md "Bounded fuzz" |
| **SC-005** — zero accepted-syntax changes, zero production modifications | **HELD** — only `spike/…` + two new self-contained typed test programs changed; production parsers/engine untouched (FR-010) | git history; REPORT.md §6 |
| **SC-006** — every comparison in a committed human-readable table | **HELD** | RESULTS.md |

Per-construct coverage floor (FR-005): **every** guard / operator / type-alternative box in
`corpus/CONSTRUCTS.md` §B is ticked with a cited IL- or parse-parity corpus file. No un-covered construct.

---

## Bounded conditions (residual — FR-011)

**BC-1 — the ANTLR grammar does not carry the hand-parser's post-parse semantics (the load-bearing
adoption condition).** `parser.cs::ParseModule` enforces, beyond pure syntax: (a) a `procedure`
declaration must have ≥1 clause unless the name resolves to a runtime-native guard or a
prelude/imported symbol via the REPL load context, and (b) a declaration's clauses must immediately
follow it. The pure-syntactic ANTLR grammar accepts programs these checks reject, so files depending on
prelude/imported/native symbols (or interleaving decls and clauses) show as one-sided rejects
(`hand=reject: … has no clauses` / `… must be immediately followed by its clauses`) — 1/72 tests/typed
(`satisfiable`, a 049 native guard) and 48/223 typed_book. **These are NOT grammar/lowering defects**
(every genuinely self-contained program matches); they are a comparator asymmetry. **Adopting the ANTLR
front-end in production requires porting these post-parse semantic checks + the native-guard/prelude/
import symbol-resolution context** — they live in `parser.cs`, not in the grammar. Until then, full
end-to-end parity is proven only for self-contained programs.

**BC-2 — F-069-1: production engine occurs-check (pre-existing, unrelated to the grammar).** Cyclic `=`
defined-guards (`A? = B? * A?`) overflow `DefinedGuardEvaluator._ApplySubstitution` (no occurs-check).
This is a shared-pipeline defect both front-ends hit identically — **not a parity divergence**. Per
**DEC F3** it is (a) filed as its own engine bug (`FINDINGS.md` F-069-1; reproducer
`fuzz-repro/fuzz-23-min.glp`) and (b) excluded from fuzz generation (cyclic `=` never yields IL). It does
not gate grammar adoption but should be fixed independently.

**BC-3 — Dart-target maturity.** Parity is demonstrated for the **C# target only**. ANTLR generates a
Dart parser from the same `Glp.g4` (grammar cost ≈ 0), but ANTLR's Dart target is less battle-tested than
C#/Java (REPORT.md §4). Replacing the production **Dart** front-end requires its own Dart-target lowering
bridge + a Dart-side parity pass before adoption.

**BC-4 — Gleam is not an ANTLR target.** "One grammar, every runtime" holds for ANTLR-supported languages
(C#, Dart, …) but **not Gleam** (REPORT.md §4): a Gleam consumer must parse via a generated parser as a
side-process or use a hand-written / different-tool parser. Single-sourcing the Gleam runtime's front-end
from `Glp.g4` is out of reach and must be planned separately.

**mod-functor — RESOLVED (T016), no longer a condition.** The `mod(...)` call form now lexes as ATOM in
both front-ends via a lexer semantic predicate (`InputStream.LA(1) != '('`), authored under **Gabi + Udi
approval** (§1.14). Faithful tokenization of existing syntax — no accepted-syntax change — verified
byte-identical IL (`mod_functor_call.glp`, MATCH).

---

## Recommended next step

A bounded **PREP/REFACTOR feature** (its own §1.14 review) that, per target in priority order (C# → Dart),
(1) ports the BC-1 post-parse semantic checks into a shared post-parse pass consumed by both front-ends,
(2) re-runs the parity sweeps end-to-end including prelude/import-dependent programs, and (3) swaps the
production parser only once its target's parity pass is green. Gleam (BC-4) is tracked as a distinct
non-ANTLR effort. This feature (069) changes no production code (FR-010); it authorizes the direction and
enumerates the cost.
