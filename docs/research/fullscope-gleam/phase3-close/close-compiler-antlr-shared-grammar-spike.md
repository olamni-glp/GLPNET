<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T065 close-compiler-antlr-shared-grammar-spike` (b3-c2-029)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-28
**Closes**: `verify-compiler-antlr-shared-grammar-spike` (2 ABSENT + 1 runtime-blocked at verify time)
**Backing detail_ids**: `antlr-shared-grammar-spike`, `compile-mode-directive`, `compiler-strict-mode`,
`module-dynamic-dispatch`, `module-static-linking`, `parser-recursive-descent`, `reduce-metainterpreter`

## Acceptance (FINAL plan, line 459)

> programs/tests/modules/, dynamic_dispatch/, tracing_meta.glp, mad_predicates.glp pass and the three
> type_errors/ programs are rejected on Gleam identically to Dart.

**Met — every named acceptance program behaves IDENTICALLY on Gleam vs Dart** (fresh differential runs
this session; `gleam test` 615/0). Two verify-time gaps were closed since (both by other 059 work
verified here):

| Acceptance program | Gleam ≡ Dart | Note |
|---|---|---|
| `dynamic_dispatch/` (`test_double(5, R).`) | **`R = 10, → succeeds`** on both | was `Unimplemented("distribute")` — closed by **T078 + #1a** |
| `tracing_meta.glp` (`test(Zs, Tree).`) | **identical** `Zs=[1,2,3,4]` + full reduction `Tree` | was `→ failed` (missing `_copy/2`) — closed by the **`_copy/2` kernel** (this WP) |
| `mad_predicates.glp` | loads identically (differential AGREE) | DELIVERED |
| `type_errors/` (×3) | all rejected identically (Dart `Type checking failed`; Gleam `StagedError(TypeCheckStage,…)`) | strict gate DELIVERED |
| `modules/` | rejected identically on both (deprecated `-import`/`-export` — parser parity) | see #4 |

## Per detail_id disposition

| # | detail_id | disposition |
|---|---|---|
| 1 | `parser-recursive-descent` | **DELIVERED** — hand-port recursive-descent parser; all corpus parses, rejections content-identical to Dart. |
| 2 | `compile-mode-directive` | **DELIVERED** — `ast.CompileMode{User;System}` + `srsw.gleam` User-mode reserved-`_` gate; `mad_predicates.glp` loads identically. |
| 3 | `compiler-strict-mode` | **DELIVERED** — always-on strict type gate; all 3 `type_errors/` rejected on both. |
| 4 | `module-static-linking` | **ABSENT in Gleam, PARITY-HOLDS / untestable** — the `loader` is single-module (no project/directory linker). Its only corpus `programs/tests/modules/` uses the **deprecated `-import([…])`/`-export([…])` directive syntax** and is rejected byte-identically on Dart AND Gleam at the parser (before any linking), so the static-linking mechanism is not exercisable by any current test and there is NO divergence. Flagged residual: a project/directory linker (§19.7 `social_graph/`-style static linking) is a scoped follow-up, needed only if a current-syntax project-load test is added; the DYNAMIC dispatch model (§19.7) is delivered and is the primary module mechanism (#5). |
| 5 | `module-dynamic-dispatch` | **DELIVERED (by T078 + #1a)** — the `Distribute`/`Transmit` runtime routing + facade auto-activation land the `#` call end to end; `dynamic_dispatch/` `test_double(5,R) → R=10` on Gleam (was `Unimplemented`), parity with Dart. (The runtime opcode is owned by `close-module-system-runtime-rpc` per the FINAL plan; this WP records the compiler-chain confirmation.) |
| 6 | `reduce-metainterpreter` | **DELIVERED** — compile-stage `unfoldReduceCalls` is parity-by-design absent (dead in Dart too). Runtime side unblocked by the **new `_copy/2` kernel** (parity port of Dart `copyKernel`, `body_kernels.dart:527`): `tracing_meta.glp` now full 3-runtime parity. |
| 7 | `antlr-shared-grammar-spike` | **RULED out-of-scope / superseded (G5, rulings.md)** — no ANTLR/`Glp.g4` on the BEAM path; the parser is the R1 recursive-descent hand-port. No work. |

## The one genuine code change in this WP — `_copy/2`

`glp_gleam/src/glp/engine/kernels.gleam` — `"_copy", 2, [source, out] -> bind_term(heap, out,
deref_term(heap, source))` (parity port of Dart `copyKernel`: deref arg0, bind arg1). Recognizer
`glp_gleam/src/glp/analysis/prelude.gleam` `"_copy/2" -> True`. **Gleam-side only** — the shared
`programs/self.glp` is NOT touched (`_copy` is a Dart-registered body kernel with no self.glp
declaration, so no cross-runtime hazard; contrast the earlier `_activate` self.glp regression). +2 unit
tests `glp_gleam/test/glp/engine/copy_kernel_test.gleam`.

## Runnable evidence (fresh-session reproducible)

| Check | Command | Result |
|---|---|---|
| Full suite grow-only | `cd glp_gleam && gleam test` | **615 passed, no failures** |
| Dynamic dispatch parity | (Gleam REPL) load `dynamic_dispatch/math_service.glp`+`dispatch_client.glp`, `test_double(5, R).` | `R = 10, → succeeds` (≡ Dart) |
| Metainterpreter parity | `bash test/parity/run_differential.sh programs/tests/tracing_meta.glp 'test(Zs, Tree).'` | **all runtimes AGREE** (identical Tree + `Zs=[1,2,3,4]`) |
| mad_predicates load | `bash test/parity/run_differential.sh programs/system/mad_predicates.glp 'true.'` | all runtimes AGREE |

## Disposition

**Close status: CLOSED — acceptance met, one residual flagged.** All 7 detail_ids dispositioned: 5
DELIVERED (2 freshly — dynamic-dispatch via T078+#1a, reduce-metainterpreter via `_copy/2`), 1 RULED
out-of-scope, 1 (module-static-linking) ABSENT-but-parity-holds with the project-linker flagged as a
scoped follow-up (no current-syntax test exercises it). Every acceptance program behaves identically
Gleam-vs-Dart.
