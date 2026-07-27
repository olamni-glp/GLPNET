<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T072 close-guards-guard-defined` (b3-c1-027)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Closes**: `verify-guards-guard-defined` (b3-c1-002) — both detail_ids DELIVERED, clean
**Backing detail_ids**: `guard-defined`, `guard-purity`

## What was verified DELIVERED

Both detail_ids DELIVERED with three-runtime (Dart + C# + Gleam) parity.

**`guard-defined`** — `glp_gleam/src/glp/bytecode/guard_defs.gleam` is a declared guard procedure
(clauses all test-only) compiled to a side table of clause specs, evaluated **three-valued** at
runtime by the `Guard` opcode handler with suspend-on-unbound-reader (port of the 049 Deliverable-A
form). `runner.gleam` imports it and dispatches via `defined_guard_table(program)` → `guard_generic`,
interpreting the defined guard three-valued over its clause spec.

**`guard-purity`** — enforced at **compile time**: `guard_defs.gleam` only admits a guard procedure
whose clauses are test-only (body empty or `true`; guards from the builtin subset or recursively
test-only; anonymous `GVar`s match anything and bind nothing). A clause that binds cannot compile
into the guard side table. At runtime the GUARD phase performs pure three-valued tests; all binding
is BODY-only, after Commit. A guard cannot bind or mutate.

## Runnable evidence (fresh-session reproducible)

| Check | Command | Result |
|---|---|---|
| Defined-guard positive (matches) | `bash test/parity/run_differential.sh programs/tests/test_defined_guards.glp` with `test(ch(a, b), R).` | Dart/C#/Gleam **all `R = ok, → succeeds`** (defined guard `channel/1` matches `ch/2`) |
| Defined-guard fallthrough | (same program) with `test(foo, R).` | Dart/C#/Gleam **all `R = not_channel, → succeeds`** (guard fails, `otherwise` fires) |
| Negative purity (effectful predicate in guard binds nothing, fails) | `bash test/parity/run_differential.sh programs/tests/test_time_guard.glp 'test_time(T).'` | all three **`T = <unbound>, → failed`** — `T` never bound, heap unmutated (Dart/C# additionally print `[WARN] Unknown guard predicate: time`) |

`programs/tests/test_defined_guards.glp` declares `channel(ch(_,_))` and uses `channel/1` in guard
position; both goals AGREE (normalized) across all three runtimes. `programs/tests/test_time_guard.glp`
is the executed negative — the guard neither bound its variable nor mutated the heap; purity held.

## Disposition (per detail_id)

| detail_id | disposition |
|---|---|
| `guard-defined` | **CONFIRMED-DELIVERED** — `guard_defs.gleam` side table + three-valued `Guard`-opcode eval; differential run of `test_defined_guards.glp` agrees across Dart/C#/Gleam (`R=ok` / `R=not_channel`) |
| `guard-purity` | **CONFIRMED-DELIVERED** — compile-time enforced (only test-only clauses compile to a guard); `test_time_guard.glp` negative run confirms an effectful predicate in guard position binds nothing and fails identically on all three runtimes |

**Close status: CLOSED — clean, no residual.** Both detail_ids DELIVERED with a positive and a
negative test each agreeing with Dart via the differential harness, exactly per the close acceptance.
