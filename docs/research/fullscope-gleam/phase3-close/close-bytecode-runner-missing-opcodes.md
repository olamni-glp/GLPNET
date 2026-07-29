<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T063 close-bytecode-runner-missing-opcodes` (b2-c2-005)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-29
**Closes**: the runner-side opcode-implementation gaps carved out by
`verify-bytecode-bytecode-instruction-set` (b3-c1-005, close-out note): "`bytecode-runner`
opcode-implementation gaps (Distribute/Transmit/Requeue/Allocate runtime) are owned by
`close-bytecode-runner-missing-opcodes` (b2-c2-005) + `close-module-system-runtime-rpc`."
**Gleam-only** — no `self.glp`, no kernel, no type-system change (§1.14 not engaged).
**Suite**: `gleam test` 638/0 (was 636/0; +2 regression tests).

## Scope resolution

`Distribute`/`Transmit` (module RPC) were the real, emitted-and-needed gap and were
**already delivered by T078** (`close-module-system-runtime-rpc`) — the runner dispatches
both (`runner.gleam` `distribute`/`transmit`). This close therefore owns the residual:
the opcodes that still fell through the `step` dispatch to
`RunnerError(Unimplemented(opcodes.mnemonic(op)))` (runner.gleam catch-all).

Enumerating the 44-variant `Op` union against the `step` match, **six** opcodes fell
through to `Unimplemented`:

| Opcode | Spec | Emitted by Gleam codegen? | Emitted by Dart codegen? | Dart runner? |
|--------|------|---------------------------|--------------------------|--------------|
| `HeadList(slot)` | §6.6 | **no** (lists → `HeadStructure(".",2)`) | no | yes (runner.dart:4381) |
| `PutList(slot)` | §7.6 | **no** (body lists → `PutStructure(".",2)`) | no | yes (runner.dart:4490) |
| `HeadVariable(v, is_reader)` | §6.2/6.3 (S-pos) | **no** (→ `UnifyVariable`) | no | yes (runner.dart:1044) |
| `Allocate(slots)` | §9.4 | no | no | yes (runner.dart:4516) |
| `Deallocate` | §9.5 | no | no | yes (runner.dart:4541) |
| `Requeue(P, n)` | §9.2 | no (tail calls → `Spawn`) | no | yes (runner.dart:3308) |

**Decisive finding (verified by grep of both codegens):** *neither the Gleam nor the Dart
codegen emits any of these six.* Only the Dart **runner** carries defensive handlers; the
Gleam runner surfaced them as loud `Unimplemented` (never silent, per SC-004). They split
into two tiers.

## Tier A — DELIVERED (three opcodes routed to existing, tested handlers)

`HeadList`, `PutList`, `HeadVariable` each have an **exact** equivalent already implemented
and covered in the Gleam runner. The Dart runner's own comments confirm the equivalences
("Equivalent to HeadStructure('[|]', 2, argSlot)"; "Equivalent to PutStructure('[|]', 2,
argSlot)"; "unified … structure variable (at S position)"). Gleam list cons is `"."/2`
(`terms.gleam:39`, `codegen.gleam` emits `"."`), so:

```gleam
opcodes.HeadList(arg_slot) -> head_structure(program, ctx, pc, ".", 2, arg_slot)
opcodes.PutList(arg_slot)  -> put_structure(ctx, ".", 2, arg_slot)
opcodes.HeadVariable(var_index, is_reader) ->
  unify_variable(program, ctx, pc, var_index, is_reader)   // §8.1/§8.2 unified writer/reader at S
```

This adds **no new machinery** — it reuses the already-green `head_structure` /
`put_structure` / `unify_variable`, so it is a faithful port, not an invention (§1.3).

**Regression tests** (`test/glp/engine/runner_missing_opcodes_test.gleam`, +2): load a real
list program whose codegen output uses the LONG forms (`HeadStructure(".",2)` /
`UnifyVariable` / `PutStructure(".",2)`), mechanically rewrite those instructions to the
SHORTHAND opcodes, rebuild the program, and assert the reduction produces the **same** ground
outcome. Were any shorthand op still unhandled, the reduction would be
`RunnerError(Unimplemented(..))` rather than `Reduced`.
- `head_list_and_variable_bind_head_test` — `headof([a], X)` binds `X = a` on both forms;
  asserts ≥1 `HeadList` and ≥1 `HeadVariable` were actually inserted.
- `put_list_builds_body_arg_test` — `emit(a)` (body goal `cont([X?])` → `PutStructure(".",2)`)
  reduces to completion on both forms; asserts ≥1 `PutList` inserted.

## Tier B — DISPOSITIONED B1 (recorded parity-by-non-emission; engineer-ratified)

`Allocate`, `Deallocate`, `Requeue` depend on WAM machinery the Gleam runner **deliberately
does not have**: permanent-variable environment frames (`cx.E`), a continuation pointer
(`cx.CP`), and in-place tail-call (Dart mutates `kappa`). The Gleam runner is a **per-goal
reduction model**: `reduce` runs one goal and returns
`Reduced(heap, woken, spawn_reqs, …)`; every body/tail call lowers to `Spawn` and the
**scheduler** owns continuation, so there are no permanent frames to push/pop and no in-place
requeue. Dart's handlers for these three are themselves unreachable dead code (Dart codegen
emits none of them).

**Disposition (B1, engineer-ratified 2026-07-29):** keep `Allocate`/`Deallocate`/`Requeue`
as loud `Unimplemented`. Rationale:
- Both toolchains emit none of them → **parity holds by non-emission**; there is no program,
  on either runtime, that reaches these opcodes.
- Surfacing them loudly (never silent) is already the correct behaviour (SC-004).
- Building env-frame / CP / in-place-tail-call machinery for opcodes that can never be
  produced would be dead code that compensates for a non-problem (DISCIPLINE §1.3).

The rejected alternative **B2** (map onto the Gleam model: `Requeue`→`Spawn`,
`Allocate`/`Deallocate`→no-op `Advance`) is a *re-mapping* of runtime semantics for inputs
that cannot occur — a design choice, not a faithful port of Dart's WAM handlers — and was
declined in favour of the honest B1 record. Should a future codegen ever emit these (it does
not today, on either side), B2 is the recorded faithful-to-model mapping to revisit then.

## Result

- Runner `Unimplemented` set reduced from six opcodes to three, and the remaining three are
  dispositioned (not silently deferred).
- `runner.gleam` change is a T063-labelled dispatch block delegating to existing handlers.
- `gleam test` 638/0. Gleam-only.
