<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-langsurface-channel-convention` (WP b3-c1-003, wave 2)

**Date**: 2026-07-22
**Method**: source-verification + in-suite unit-parity tests + 3-runtime differential harness (Dart + C# + Gleam), `test/parity/run_differential.sh` (`DART=/c/Users/gavri/dart-sdk/bin/dart.exe`; C# REPL built at `out/csharp/glp_repl/…`; gleam 1.17/OTP29 — required `gleam clean` first to clear the WSL/Windows build-thrash).
**Paired close**: `close-langsurface-channel-convention` (b3-c1-028) — **ACTIVATED** (one negative-path error-surface divergence; see Finding).

## Verdict table

| # | detail_id | verdict | basis |
|---|-----------|---------|-------|
| 1 | `channel-convention` | **DELIVERED** | `Channel(In,Out) ::= ch(In,Out?)` lives in the shared `programs/self.glp` (loaded by all three runtimes); Gleam consumes it via `param_expansion` + mode traversal. `new_channel`/`send`/`receive` idioms run byte-identically (`make_pair`, `param_channel` → `A=ch(_V1,_V2) B=ch(_V2,_V1)` on all three). `prelude.gleam` marks Channel library-level (not protected), matching Dart. |
| 2 | `clause-programming-idioms` | **DELIVERED** | bind / relay / passthrough / channel-route idioms parse, type-check and run with parity across the 5 named programs. |
| 3 | `srsw-anonymous-writer` | **DELIVERED** | `srsw.gleam` 1:1 Dart port: `_`-prefixed writers/readers exempt (`record_writer`/`record_reader`), plain `UnderscoreTerm` exempt; plus constant-type (`mark_constant_type_vars`) and ground-guard relaxations. In-suite `srsw_test.gleam` green; `test_ground` (anonymous `_` tail + ground guard) runs with parity. |
| 4 | `type-guard-set` | **DELIVERED** | `prelude.gleam` `is_builtin_procedure`/`is_predefined_procedure` carry the fixed guard vocabulary 1:1 from Dart (`ground`/`known`/`integer`/`atom`/`=?=`/`</>/=<`/`@<`-family/`=..`/…). `list/1`, `atom/1`, `=?=`+`otherwise` all run with parity. |
| 5 | `type-parameterized` | **DELIVERED** (logic) — **with a recorded error-surface divergence on the negative path** | `param_expansion.gleam` 1:1 Dart port (template separation, instantiation worklist, `Stream(_)≡Stream` wildcard collapse, call-site inference, nested refs). Positives (`Stream(Integer)` merge → `[1,2]`; parameterized `Channel`) parity. Arity-mismatch **is detected** identically (`UnknownTypeError: Stream?` on all three) — but Gleam raises it as an **uncaught panic** vs Dart/C# graceful load diagnostic. See Finding F1. |

## Evidence

### Source (all five surfaces are 1:1 Dart ports)
- `channel-convention` / `type-parameterized`: `glp_gleam/src/glp/analysis/type_checker/param_expansion.gleam` (Dart `param_expansion.dart`); Channel + Stream defined in `programs/self.glp` via the scope chain (`prelude.gleam` header: "all prelude definitions live in programs/self.glp").
- `srsw-anonymous-writer`: `glp_gleam/src/glp/analysis/srsw.gleam:198-230,296` — anonymous exemption + both SRSW relaxations, "NO escape mechanism … the check always runs".
- `type-guard-set`: `glp_gleam/src/glp/analysis/prelude.gleam:16-119` — guard/predefined vocabulary ("ported 1:1 — D4 discipline: no additions").

### In-suite unit-parity tests (green in the 465 floor)
`glp_gleam/test/glp/analysis/`: `srsw_test`, `param_expansion_test`, `type_checker_test`, `well_typed_clause_test`, `moded_head_test`, `moded_term_test`, `subtyping_test`, `clause_validation_test`, `program_dfa_test`, `type_environment_builder_test` — the unit-level Dart-parity harness for exactly these surfaces.

### Differential harness — runtime parity (all AGREE, normalized)

| program | goal | outcome (Dart ≡ C# ≡ Gleam) |
|---|---|---|
| `test_passthrough.glp` | `passthrough(hello, X).` | `X = hello, → succeeds` |
| `test_ground.glp` | `test_ground([foo(1), z]).` | `→ succeeds` |
| `test_channel_route.glp` | `test_net(R).` | `→ failed` (parity) |
| `test_channel_guards.glp` | `make_pair(A, B).` | `A=ch(_V1,_V2) B=ch(_V2,_V1), → succeeds` |
| `test_new_channel_guard.glp` | `make_channels(A, B).` | `→ failed` (parity) |
| `typed/param_stream_integer.glp` | `merge([1],[2],Z).` | `Z = [1, 2], → succeeds` |
| `typed/param_channel.glp` | `new_channel(A, B).` | `A=ch(_V1,_V2) B=ch(_V2,_V1), → succeeds` |
| `typed/test_guards_comprehensive.glp` | `test_list_ok([a,b], R).` | `R = ok, → succeeds` |
| `typed/atom_guard.glp` | `is_atom(hello, R).` | `R = yes, → succeeds` |
| `typed/test_ground_equal.glp` | `test(a, b, R).` | `R = not_equal, → succeeds` |

### Negative programs (load-reject parity)
- `typed/decline_reader_bad.glp` (undefined guard `reader/1`) → **REJECTED on all three**, content byte-identical. Gleam: `StagedError(TypeCheckStage, TypeError, Pos(9,1), "Body atom 0 (reader) is not well-typed:\n  Inconsistent path: Undefined procedure: reader/1\n  Path: (reader/1, 0, output)")`; Dart/C#: `Error loading …: Type checking failed: … Undefined procedure: reader/1 … at line 9`. The check is not a rubber stamp — it rejects, identically.
- `typed/param_arity_mismatch.glp` (`Stream(Integer,String)` — arity 2 vs 1) → **DIVERGENCE** (Finding F1).

## 🔴 Finding F1 — negative-path error surface diverges (activates `close-langsurface-channel-convention`)

All three runtimes **detect** the ill-formed type (`UnknownTypeError: Stream?`). But:
- **Dart / C#**: surface it as a caught load-time diagnostic — `GLP> Error loading …/param_arity_mismatch.glp: UnknownTypeError: Stream?` — and the REPL survives.
- **Gleam**: raises it as an **uncaught `panic`** that escapes the Result-based loader stage and crashes the REPL:
  `runtime error: panic` / `UnknownTypeError: Stream?` at `glp/analysis/type_checker/program_dfa.build_procedure_automaton` (`program_dfa.gleam:580`), via `type_checker.check_module` (`type_checker.gleam:179`) ← `loader.type_check_stage` (`loader.gleam:231`).

Root cause: `program_dfa.gleam:580` is `Error(_) -> panic as { "UnknownTypeError: " <> target_name }`, where the Dart source uses a `states[…]!` null-assertion — a **catchable** exception that the Dart/C# loaders wrap into a graceful diagnostic. The Gleam port translated `!` to `panic` (unrecoverable), so it bypasses the `StagedError`/Result channel that `decline_reader_bad` correctly uses. Through the differential harness (which drops Gleam stderr) this shows as `RESULT: 2 divergent pair(s): dart/gleam csharp/gleam`.

Scope note (engineer): the defect is in **shared type-checker infrastructure** (`program_dfa` automaton build), not in the parameterized-type logic itself — any unknown type reaching automaton construction panics, and it surfaced here via the parameterized-type arity-mismatch negative. `close-langsurface-channel-convention` cannot meet its "typed corpus sample all agree with Dart goldens" acceptance until `program_dfa`'s `panic` is threaded back as a returned/catchable `StagedError(TypeCheckStage, …)`. Because the fix touches shared infra (and mirrors the Dart `states[…]!` → exception → caught pattern), flag for the engineer whether it lands under this close or a shared type-checker-robustness close.

## Activation

`close-langsurface-channel-convention` (b3-c1-028) — **ACTIVATED**, scoped to Finding F1 only: convert the `program_dfa.gleam:580` `panic` (and the analogous `states[…]!` sites) into a returned `StagedError(TypeCheckStage, …)` so `param_arity_mismatch.glp` (and any unknown-type-at-automaton-build) yields a Dart-identical graceful load-error instead of crashing the Gleam REPL. All other four detail_ids are DELIVERED with parity and need no close work beyond delivered-confirmation.
