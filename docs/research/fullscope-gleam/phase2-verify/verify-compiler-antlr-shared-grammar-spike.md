<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-compiler-antlr-shared-grammar-spike` (WP b3-c1-004, wave 2)

**Date**: 2026-07-23
**Method**: source-verification + acceptance searches (`rg`) + Gleam-runner corpus runs vs Dart/C# goldens (`test/parity/run_differential.sh` + direct multi-load sessions; `DART=/c/Users/gavri/dart-sdk/bin/dart.exe`; C# REPL built; gleam 1.17/OTP29).
**Paired close**: `close-compiler-antlr-shared-grammar-spike` (b3-c2-029) — **ACTIVATED** (2 ABSENT + 1 runtime-blocked; see Activation).

## Verdict table

| # | detail_id | verdict | basis |
|---|-----------|---------|-------|
| 1 | `parser-recursive-descent` | **DELIVERED** | `parser.gleam` header: "recursive-descent parser … Hand-port (R1 — no parser generator) of the Dart parser; the Dart parser's behaviour is the conformance oracle, including error messages." All corpus programs parse; deprecated-directive + type-error rejections are content-identical to Dart. |
| 2 | `compile-mode-directive` | **DELIVERED** | `ast.gleam:21-27` `CompileMode { User; System }` ("`-mode(system).` allows [underscore-prefixed constants]"); `srsw.gleam:282-294` enforces the User-mode reserved-`_`-constant gate 1:1. `mad_predicates.glp` loads identically (`✓ Loaded`) on Dart+Gleam. |
| 3 | `compiler-strict-mode` | **DELIVERED** | Always-on strict type gate: `loader.gleam` type-check stage rejects on any `StagedError`, warnings pass. All 3 `type_errors/` negatives **rejected on both** (Dart `Type checking failed: … not well-typed`; Gleam `StagedError(TypeCheckStage, TypeError, …)`) — graceful staged rejections (well-typed-clause path; contrast F1). |
| 4 | `module-static-linking` | **ABSENT** | `loader.gleam` is a single-module standalone pipeline ("the compiled program IS the registration"); no project/directory linker. Named corpus `programs/tests/modules/` is **stale** (deprecated `-export`/`-import`), rejected byte-identically on both (parser parity) → cannot positively exercise linking; the mechanism is not present on the Gleam path. |
| 5 | `module-dynamic-dispatch` | **ABSENT** | No `_activate`/`_select` in `glp_gleam/src`. `dynamic_dispatch/` (current syntax) **loads + type-checks** on Gleam, but the `#` call runs `→ failed` + `Error: runner error: Unimplemented("distribute")` (Dart/C# → `R = 10`). Module *syntax* (parse + type-check of `-module`/`exported`/`imported`/`#`) is delivered; the *runtime* dispatch is not. |
| 6 | `reduce-metainterpreter` | **PARTIAL** | Compile-stage side: Dart's `unfoldReduceCalls` "has NO live caller in the reference pipeline … deliberately not ported" (`partial_eval.gleam:24`) → **parity-by-design** (dead in Dart too), no work. Runtime side: the metainterpreter program `tracing_meta.glp` **fails on Gleam** (`Zs`/`Tree` `<unbound>`, `→ failed`) vs Dart/C# full reduction trace — blocked by the **missing `'_copy'/2` kernel** (not in Gleam `prelude.is_builtin_procedure`). |
| 7 | `antlr-shared-grammar-spike` | **ABSENT — superseded (ruled)** | No ANTLR/`Glp.g4` artifact on the BEAM path (all `Glp.g4` hits are planning/3rtask prose); the parser is the R1 recursive-descent hand-port. Pre-ruled out-of-scope/superseded by **G5** in `rulings.md`. |

## Evidence

### Acceptance searches
- `rg -n 'strict|_activate|_select|reduce|linker|antlr' glp_gleam/src`: `_activate`/`_select` = **no hits**; `linker`/`antlr` = **no code hits**; `reduce` → `partial_eval.gleam` (guard-unfold `reduce_guards`, + the not-ported `unfoldReduceCalls` note); `strict` → comment-only.
- `Glp.g4` across repo → only `.specify/3rtask/runs/**` claim JSON + `docs/research/fullscope-gleam/**` plan/inventory prose. **No grammar file, no BEAM ANTLR target.**

### Corpus runs (Gleam runner vs Dart/C# goldens)

| corpus | invocation | Dart / C# | Gleam | verdict signal |
|---|---|---|---|---|
| `dynamic_dispatch/` | load `math_service`+`dispatch_client`, `test_double(5,R).` | `R = 10, → succeeds` | `R = X2, → failed` + `Unimplemented("distribute")` | dynamic-dispatch ABSENT (runtime) |
| `modules/` | load `math`+`main`, `test_double(R).` | `Error loading …: [syntax] The -export() … is no longer supported` | `StagedError(ParseStage, ParseError, … no longer supported)` | corpus stale; rejection byte-identical (parser parity) |
| `type_errors/` (×3) | load each | all rejected (`Type checking failed`) | all rejected (`StagedError(TypeCheckStage, …)`) | strict gate DELIVERED |
| `tracing_meta.glp` | `test(Zs, Tree).` | `Zs=[1,2,3,4]`, full `reduction(...)` tree, `→ succeeds` | `Zs`/`Tree` `<unbound>`, `→ failed` | reduce metainterpreter blocked by missing `'_copy'/2` |
| `mad_predicates.glp` | load | `✓ Loaded` | `✓ Loaded` | compile-mode parity (note: file has no `-mode(system)` directive) |

### Source anchors
- `glp_gleam/src/glp/parser/parser.gleam` (recursive-descent, R1, Dart oracle) · `parser/lexer.gleam`.
- `glp_gleam/src/glp/parser/ast.gleam:21-27` (`CompileMode`) · `analysis/srsw.gleam:282-294` (User-mode reserved-constant gate).
- `glp_gleam/src/glp/compiler/loader.gleam` (single-module standalone pipeline; strict staged rejection) · `compiler/partial_eval.gleam:24` (`unfoldReduceCalls` not ported).
- `Unimplemented("distribute")` — the runtime `Distribute` opcode is unimplemented in `glp/engine/runner.gleam`.

## Activation

`close-compiler-antlr-shared-grammar-spike` (b3-c2-029) — **ACTIVATED**. Scope, with cross-references (avoid double work):

1. **`module-dynamic-dispatch` + `module-static-linking` (ABSENT)** — implement the compiler-side dispatch table (`_select`) + project linker AND the runtime `#` routing. ⚠️ **The runtime `Distribute`/`Transmit` opcode is explicitly owned by `close-module-system-runtime-rpc` (b2-c2-007)** per the FINAL plan (line 442 "Distribute/Transmit runtime explicitly owned by close-module-system-runtime-rpc to avoid double work"); coordinate so the compiler-close does the emit/link and the module-system-close does the runtime opcode. `module-system` scope-chain parity is separately handled by `verify-module-system-scope-chain` (b2-c2-021).
2. **`reduce-metainterpreter` (PARTIAL)** — no compiler work (compile-stage reduce-generation is parity-by-design absent). The runtime failure is a **missing `'_copy'/2` kernel** → route to `close-runtime-arithmetic-expression` (`heap-value-copy-semantics` / `system-predicate-registry` are its detail_ids). `tracing_meta.glp` will pass once `_copy/2` lands.
3. **`antlr-shared-grammar-spike`** — resolved by **G5** (out-of-scope, superseded); no work; `rule-compiler-antlr-shared-grammar-spike` records it.
4. **Corpus-staleness flag (for the close)** — the close acceptance says "programs/tests/modules/ … pass", but that corpus uses deprecated `-export`/`-import` directives (rejected identically on both runtimes today). The close must **refresh `programs/tests/modules/` to current `exported`/`imported procedure` syntax** to actually exercise static linking — otherwise "modules/ pass" reduces to byte-identical rejection parity, which does not prove linking.

`parser-recursive-descent`, `compile-mode-directive`, `compiler-strict-mode` are DELIVERED with parity and need only delivered-confirmation.
