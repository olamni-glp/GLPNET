<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# §1.14 Language-Authority Proposal — ANTLR4 shared-grammar spike (feature 065, US1/Scope A, T010)

**Status:** 🔴 AWAITING APPROVAL — Gabi + Udi. Work STOPPED at this gate.
**Raised by:** Olamnit pipeline session, 2026-08-03. **Feature:** `065-glp-runtime-consol`.
**Authority:** DISCIPLINE §1.14 (Language Design Authority) · Constitution IV-a · CLAUDE.md
"Language Authority". Directive: Gabi — *"implement (STOP at the §1.14 gate on the antlr4
grammar)"*.

## Why this gate

Authoring `spike/antlr4-glp-grammar/Glp.g4` (task T010) defines a formal grammar for the GLP
surface syntax intended to single-source parsing across ≥3 runtimes (C#, Dart, trial Gleam/C++).
That is **language-surface work**. Even though the spike is additive and the intent is to
**faithfully describe the EXISTING accepted syntax** (change nothing), the act of writing the
authoritative grammar is exactly what §1.14 / IV-a reserve for explicit owner approval. Per Gabi's
directive and the restart handover, I have **stopped here** rather than author it autonomously.

## What is already done (non-gated, landed)

- **US2 / Scope B COMPLETE**: dead `AbandonOps` stub removed (`out/csharp/lib/runtime/abandon.cs`
  + its `Converted.props` compile entry); engine csproj + full `glp_runtime_net.sln` build 0-err
  (commit `8bc4b698`). Abandon remains delivered as the anonymous-writer discard semantic (062 US5).
- **US1 prep (T007–T009, non-gated)**:
  - **Toolchain confirmed available**: Java 17 (`/c/Users/smbuser/java/jdk-17.0.19+10`), `dotnet`
    10.0.301, network reachable for the ANTLR4 complete jar + the `Antlr4.Runtime.Standard` NuGet
    C# runtime. ANTLR jar is not yet vendored but is fetchable.
  - **Spike scaffold** created: `spike/antlr4-glp-grammar/{corpus,harness,gen}`.
  - **Corpus manifest** fixed: `spike/antlr4-glp-grammar/corpus/MANIFEST.md` (7 representative
    files incl. 1 negative control).
  - **Token vocabulary enumerated** from `out/csharp/lib/compiler/token.cs` (authoritative, 49
    token types — see below).

## The proposal (what I am asking approval to do)

Author `Glp.g4` as a **faithful, additive, throwaway-or-keep spike artifact** that:

1. **Lexer** — one rule per existing token type, no more. Full vocabulary from `token.cs`:
   `ATOM, VARIABLE, READER(X?), NUMBER, STRING, LPAREN, RPAREN, LBRACKET, RBRACKET, LBRACE,
   RBRACE, DOT, COMMA, PIPE, QUESTION, SEMICOLON, IMPLIES(:-), ASSIGN(:=), GUARD_SEP(|),
   PLUS, MINUS, STAR, SLASH, SLASH_SLASH(//), MOD, LESS, GREATER, LESS_EQUAL(=<),
   GREATER_EQUAL(>=), EQUALS(=), ARITH_EQUAL(=:=), ARITH_NOT_EQUAL(=\=), GROUND_EQUAL(=?=),
   UNIV(=..), UNIV_DECOMPOSE(..=), UNDERSCORE(_), TILDE(~), HASH(#), BACKSLASH(\), AT(@),
   AT_LESS(@<), AT_GREATER(@>), AT_LESS_EQUAL(@=<), AT_GREATER_EQUAL(@>=), COLONCOLONEQ(::=),
   PROCEDURE, EOF`.
2. **Parser** — production rules that accept the language the hand-written recursive-descent
   parsers (`out/csharp/lib/compiler/parser.cs` 1918 LOC / `glp_runtime/lib/compiler/parser.dart`
   1773 LOC) already accept for the corpus — **no more, no less**. Derived by reading those
   parsers as the ground truth (not by inventing syntax).
3. **Generate** the C# parser front-end (`-Dlanguage=CSharp`) and **verify coverage** (SC-001)
   by parsing the corpus, and **IL parity** (SC-002) by compiling corpus examples through the
   shared downstream pipeline with only the parser front-end swapped and comparing bytecode.
4. **Report** a go/no-go verdict (`REPORT.md`), trial-target cost, and residual risks.

### Explicit commitments (the guardrails I will hold)

- **No change to accepted GLP syntax.** The grammar describes; it does not extend, restrict, or
  redesign. If, while writing it, I find a construct that cannot be expressed **without** changing
  what the language accepts, I will STOP again and bring the specific construct back to you before
  any change (FR-005 / SC-004).
- **Additive & non-production.** The grammar/harness live only under `spike/antlr4-glp-grammar/`.
  The production hand-written parsers are **not** touched (FR-010). Any future *adoption* that
  would replace a production parser is a separate PREP/REFACTOR feature requiring its own §1.14
  review.
- **Known-limitation honesty.** Documented parser quirks (e.g. `=..` restricted to clause heads;
  structs-inside-lists in REPL goals — see `docs/known-issues.md`) will be described **as they
  currently behave**, not "fixed" in the grammar.

## Decisions requested from Gabi + Udi

1. **Approve authoring `Glp.g4`** as a faithful, additive spike per the above? (yes / no / adjust scope)
2. **Scope of the parity run**: the 7-file corpus subset (proposed) vs a larger set?
3. **Trial second target**: attempt Gleam/C++/Dart generation now, or defer to the report as a
   cost estimate?

Until (1) is approved, T010–T015 remain **not started** and the marathon run
`mrun-09a6c7f8d528` stays undischargeable (2 of 3 discharge items pending on this gate).
