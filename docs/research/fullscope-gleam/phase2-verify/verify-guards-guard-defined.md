<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-guards-guard-defined` (WP b3-c1-002, wave 2)

**Date**: 2026-07-21
**Method**: source-verification + 3-runtime differential harness (Dart + C# + Gleam).
**Paired close**: `close-guards-guard-defined` — **NOT activated** (both detail_ids DELIVERED).

## Verdict table

| # | detail_id | verdict | basis |
|---|-----------|---------|-------|
| 1 | `guard-defined` | **DELIVERED** | `guard_defs.gleam` side table + `defined_guard_table` + three-valued `Guard`-opcode eval with suspend-on-unbound; full Dart/C#/Gleam parity |
| 2 | `guard-purity` | **DELIVERED** | purity **enforced at compile time** — only test-only clauses compile to a guard; effectful predicate in guard position binds nothing and fails (negative run, not assumed) |

## Evidence

### `guard-defined` — DELIVERED
- **Source**: `glp_gleam/src/glp/bytecode/guard_defs.gleam` — "a declared guard procedure whose clauses are all test-only … compiles to a side table of clause specs, evaluated three-valued at runtime by the `Guard` opcode handler (suspension on unbound readers)" (port of the 049 Deliverable-A form). `runner.gleam` imports it (`import glp/bytecode/guard_defs`, line 47) and dispatches via `defined_guard_table(program)` → `guard_generic` (runner.gleam:2244-2259), interpreting the defined guard three-valued over its clause spec.
- **Runnable** (`test/parity/run_differential.sh programs/tests/test_defined_guards.glp`) — the program declares `channel(ch(_,_))` and uses `channel/1` in guard position:
  - `test(ch(a, b), R).` → Dart/C#/Gleam **all `R = ok, → succeeds`** (defined guard matches ch/2).
  - `test(foo, R).` → Dart/C#/Gleam **all `R = not_channel, → succeeds`** (guard fails, `otherwise` fires).
  - Both: **all runtimes AGREE (normalized)**.

### `guard-purity` — DELIVERED (compile-time enforced)
- **Structural / compile-time enforcement**: `guard_defs.gleam` only admits a guard procedure whose clauses are **test-only** — "body empty or `true`; guards drawn from the builtin subset or recursively test-only" — and anonymous `GVar`s "match anything and bind nothing." A clause that binds cannot compile into the guard side table. At runtime the GUARD phase performs pure three-valued tests; all binding is BODY-only, after Commit (`kernels.gleam:12` — kernels "run in the BODY phase, AFTER Commit"). So a guard cannot bind or mutate.
- **Negative run (per the WP risk note — run, not assumed)**: an effectful/unregistered predicate in guard position does **not** bind and fails. `test/parity/run_differential.sh programs/tests/test_time_guard.glp 'test_time(T).'` → `T = <unbound>, → failed` on all three runtimes (Gleam matches the outcome; Dart/C# additionally print `[WARN] Unknown guard predicate: time`). The guard neither bound `T` nor mutated the heap — purity held.
- **Caveat**: there is no dedicated in-slice *negative-purity* program (matches inventory b3-c1-013 "no-test"); the equivalent negative above was executed. A dedicated negative-purity reproducer would strengthen the tripwire but is not required for the DELIVERED verdict.

## Activation

None. `guard-defined` and `guard-purity` are both DELIVERED with parity; `close-guards-guard-defined` has no gap to close (it can be recorded delivered-confirmed rather than executed).
