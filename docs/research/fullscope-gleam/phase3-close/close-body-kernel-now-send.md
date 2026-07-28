<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T061 close-body-kernel-now-send` (b2-c2-004)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-28
**Closes**: `body-kernel` — the effectful `_now` + `_send` body kernels completing the Gleam
kernel registry to the reference two-valued (success/abort) surface.
**Backing detail_ids**: `body-kernel`

## Acceptance (FINAL plan, line 427)

> `gleam test` in glp_gleam passes with new gleeunit cases (extending
> arith_guards_kernels_test.gleam or a sibling effectful-kernels test) exercising `_now` and `_send`
> success and abort paths.

Green under `gleam test` (**636 passed, 0 failures**; baseline 629 + 7 effectful-kernel tests) in the
NEW sibling `glp_gleam/test/glp/engine/effectful_kernels_test.gleam`.

## What was delivered

Two kernels, per their Dart references (`body_kernels.dart`):

| Kernel | State before | Change |
|---|---|---|
| **`_now/1`** (Dart `nowKernel`) | ABSENT — `self.glp`'s `now(T?) :- '_now'(T).` called an unimplemented kernel → `now/1` failed | IMPLEMENTED in `kernels.gleam`: `is_kernel("_now",1)` + a dispatch arm binding the output to `ConstInt(now_millis())`, where `now_millis()` = `erlang:system_time(millisecond)` via a new FFI (a BIF — AtomVM-safe, matching Dart `DateTime.now().millisecondsSinceEpoch`). Registered in `analysis/prelude.gleam` `is_builtin_procedure` (`"_now/1"`) so a clauseless decl parses + body calls type-check, exactly as `_output`/`_send`. |
| **`_send/3`** (Dart `sendKernel`) | Already DELIVERED — the madGLP effectful kernel (`mad_kernels.gleam`), dispatched via `effectful_spawn`→`mad_spawn`, already in `is_builtin_procedure`; success threads `M_p` (`mad_send_seam_test`) | No code change — this close ADDS the missing ABORT-path test coverage. |

### `_now/1` — an established language kernel, ported

`_now` is not a new language feature: `self.glp` already ships `now(T?) :- '_now'(T).` and the Dart
reference registers `_now`. This close completes the Gleam parity port of that existing kernel (the T061
WP authorises it); no new guard/directive/primitive-type — §1.14 not engaged. It is the one external-io
kernel here (its result varies per call, so it is excluded from the byte-parity corpus).

### `_send/3` — scoped to the reference messaging contract (plan risk honoured)

The plan flagged that `_send`'s routing target hangs on the unruled multiagent scope. This close touches
NO routing: `_send/3` stays exactly the reference body-kernel contract already delivered by the madGLP
`send_kernel`. The abort path is the runner's non-fatal `Failed` (Dart "not in madGLP mode (no
MadContext)" + bad global name), verified here.

## Tests (`effectful_kernels_test.gleam`, +7)

- **`_now` success**: `now_is_a_kernel` (registered), `now_binds_current_millis` (dispatch binds a
  positive integer), `now_end_to_end_binds_integer` (`engine.run(new(), "now(T)")` → `Success`, `T` an
  integer — driven through the engine facade via self.glp's wrapper).
- **`_now` abort**: `now_wrong_shape_is_not_dispatched` (a `_now/1` call with no output arg → not
  dispatched, the Dart wrong-arity abort).
- **`_send` abort**: `send_without_mad_context_fails` (no `MadState` → `Failed`),
  `send_bad_global_name_fails` (G an atom, not `_w`/`_r`, even in mad mode → `Failed`).
- **`_send` success control**: `send_in_mad_mode_succeeds` (mad mode + well-formed name → `Reduced` with
  a non-empty `M_p`), mirroring `mad_send_seam_test` so both paths live in one place.

## Discipline

Gleam-side only — `kernels.gleam` + `analysis/prelude.gleam` + one NEW test. No `self.glp` change (it
already declared `now`/`'_now'`), no Dart change (the Dart reference already registers `_now`/`_send`),
no new language surface. `gleam test` 636/0.
