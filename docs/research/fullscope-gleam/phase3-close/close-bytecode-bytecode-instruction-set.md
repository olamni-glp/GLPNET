<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T062 close-bytecode-bytecode-instruction-set` (b3-c1-030)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Closes**: `verify-bytecode-bytecode-instruction-set` (b3-c1-005) — instruction-set + mode-conversion CONFIRM; lint gap dispositioned
**Backing detail_ids**: `bytecode-instruction-set`, `bytecode-mode-conversion`, `bytecode-lint`

## What was verified DELIVERED

The Gleam instance is bytecode-level (`loader.gleam` Stage 5 `codegen.generate` → v2.16
`BytecodeProgram`; `runner.gleam` dispatches opcodes), so the v2.16 conformance target applies
and is met. The Gleam `Op` union (`glp_gleam/src/glp/bytecode/opcodes.gleam`) is
**discriminant-complete**: all 44 production discriminants of the Dart source-of-truth
(`opcodes.dart` + `opcodes_v2.dart`, cross-read against `docs/glp-bytecode-v216-complete.md`
§2-14) are present. The Dart classes Gleam omits are exactly the documented non-production forms
(asm.dart test-helpers, `@deprecated` ops subsumed by `ClauseNext`, and the §12.8/13.1-REMOVED
v1 two-arg `GetVariable`/`GetValue`), each named in the `opcodes.gleam` module note. The six
spec-gap ops (`NoReaders`, `GroundEqual`, `PutBoundConst`, `PutBoundNil`, `Distribute`,
`Transmit`) match the Dart reference for corpus parity (doc-gap escalated, not invented).

Mode-conversion (`bytecode-mode-conversion`): the unified `GetVariable`/`GetValue` (and
`HeadVariable`/`PutVariable`/`SetVariable`/`UnifyVariable`) carry `is_reader`, rendering the §12
distinct writer/reader mnemonics. Writer-to-writer is enforced at runtime — `unify.gleam:88-89`
returns `Error(WriterToWriter(wa, wb))` "reported loudly … never Fail (SC-004)" — matching §15.

## Runnable evidence (fresh-session reproducible, this branch)

| Check | Command | Result |
|---|---|---|
| Discriminant-completeness (mirror of `DiscriminantCompletenessTests.cs`) | `gleam test` (runs `glp_gleam/test/glp/bytecode/opcodes_test.gleam`) | **green** — `every_variant_has_a_mnemonic` (totality) + `mnemonics_are_pairwise_distinct` + `unified_instructions_flip_on_is_reader` |
| Runtime WxW (writer-to-writer fails loudly, not silently) | `gleam test` (runs `glp_gleam/test/glp/engine/writer_mgu_adversarial_test.gleam`) | **green** (part of the ≥465/508 floor) |
| `test_wxw.glp` cross-runtime | `bash test/parity/run_differential.sh programs/tests/test_wxw.glp` | all three AGREE as **SRSW-rejection parity** (see note) |
| Linter presence sweep | `rg -n 'lint' glp_gleam/src` | only the `glp/lint.gleam` placeholder |

**`test_wxw.glp` note (carried from the verify caveat):** its clause `wxw_test(X, Y).` (two
writers, no readers) is **SRSW-rejected at load, byte-identically** on Dart and Gleam
(`… Variable "X"/"Y" has no reader`), so the goal runs against an empty program and all three
runtimes report "→ failed". `test_wxw.glp` therefore proves **SRSW-rejection parity**, not runtime
WxW. Runtime writer-to-writer is pinned separately by `unify.gleam:88-89` +
`writer_mgu_adversarial_test.gleam` (both cited above). The program is mislabeled for runtime WxW;
this close records that rather than repurposing it.

## Disposition (per detail_id)

| detail_id | disposition |
|---|---|
| `bytecode-instruction-set` | **CONFIRMED-DELIVERED** — `Op` union discriminant-complete (44 variants) vs Dart production set + §2-14; omissions documented; `opcodes_test.gleam` green |
| `bytecode-mode-conversion` | **CONFIRMED-DELIVERED** — `is_reader` polarity + runtime WxW at `unify.gleam:88-89`; adversarial suite green |
| `bytecode-lint` | **AUXILIARY-OPTIONAL (recorded, not ported)** — `glp/lint.gleam` is an empty-but-building placeholder; Dart source of truth `glp_runtime/lib/lint/linter.dart` un-ported. The linter is a dev-time bytecode checker, off the runtime/parity/language-surface critical path; `lint.gleam` frames the heavy port as downstream F4+. No runtime gap. |

**Scope note:** `bytecode-runner` opcode-implementation gaps (Distribute/Transmit/Requeue/Allocate
runtime faults) are owned by `close-bytecode-runner-missing-opcodes` (b2-c2-005) +
`close-module-system-runtime-rpc`, not this close.

**Close status: CLOSED.** Instruction-set + mode-conversion confirmed DELIVERED with green
in-suite discriminant-completeness and WxW tests; the sole residual — `bytecode-lint` — is
recorded **auxiliary-optional** per the close deliverable's explicit option.
