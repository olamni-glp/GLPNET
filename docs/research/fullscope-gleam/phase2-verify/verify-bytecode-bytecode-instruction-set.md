<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-bytecode-bytecode-instruction-set` (WP b3-c1-005, wave 2)

**Date**: 2026-07-23
**Method**: source-verification + discriminant-by-discriminant diff (Gleam `Op` union vs Dart `opcodes.dart`+`opcodes_v2.dart` production set + `docs/glp-bytecode-v216-complete.md` §2-14) + differential harness on `test_wxw.glp` + `rg -n 'lint'`. In-suite mirrors: T012 `opcodes_test.gleam`, `writer_mgu_adversarial_test.gleam` (green in the 465 floor).
**Paired close**: `close-bytecode-bytecode-instruction-set` (b3-c1-030) — **ACTIVATED** (lint gap only; instruction-set + mode-conversion CONFIRM).

**Architecture (risk-note resolution)**: the Gleam instance **is bytecode-level, not tree-walking** — `loader.gleam` Stage 5 `codegen.generate` emits a v2.16 `BytecodeProgram`; `opcodes.gleam` is the instruction set; `runner.gleam` dispatches opcodes. The conformance target applies and is met.

## Verdict table

| # | detail_id | verdict | basis |
|---|-----------|---------|-------|
| 1 | `bytecode-instruction-set` | **DELIVERED** | Gleam `Op` union is discriminant-complete vs the Dart **production** opcode set and §2-14. Every Dart production opcode maps to a Gleam variant; the Dart classes Gleam omits are exactly the ones `opcodes.gleam` documents as not-ported (asm.dart test-helpers + `@deprecated` + §12.8/13.1-removed v1 forms). The 6 spec-gap ops match the Dart reference (parity; doc-gap escalated, not silently patched). |
| 2 | `bytecode-mode-conversion` | **DELIVERED** | Mode-aware `GetVariable`/`GetValue` carry `is_reader` (§12.1-12.4 → `get_{writer,reader}_{variable,value}`). WxW enforced at runtime: `unify.gleam:88-89` `RWriter, RWriter -> Error(WriterToWriter(..))` ("reported loudly … never Fail (SC-004)"). Caveat below. |
| 3 | `bytecode-lint` | **ABSENT** | `glp/lint.gleam` is an empty-but-building placeholder ("no ported GLP semantics yet … heavy port lands in a downstream feature F4+"); Dart source of truth `glp_runtime/lib/lint/linter.dart` exists, not ported. |

## Evidence

### `bytecode-instruction-set` — discriminant diff (Gleam `Op` vs Dart production set)
- **Dart opcode classes** (`opcodes.dart`+`opcodes_v2.dart`): the 44 production discriminants + base `Op`/`OpV2` + a set of non-production classes.
- **Gleam `Op`** (44 variants): all 44 Dart production discriminants present — Allocate, ClauseTry/ClauseNext/TryNextClause/NoMoreClauses, Commit, Proceed, Spawn, Requeue, Deallocate, Nop, Halt, Label, HeadStructure/HeadVariable/HeadConstant/HeadNil/HeadList, GetVariable/GetValue, PutStructure/PutVariable/PutConstant/PutNil/PutList/PutBoundConst/PutBoundNil, UnifyVariable/UnifyConstant/SetVariable/SetConstant/UnifyVoid/Push/Pop/UnifyStructure, Guard/Ground/Known/Unknown/Otherwise/NoReaders/GroundEqual, Distribute/Transmit.
- **Omissions (deliberate, documented)**: `BodySetConst`/`BodySetConstArg`/`BodySetStructConstArgs`, `GuardFail`, `GuardNeedReader(+Arg)`, `HeadBindWriter(+Arg)`, `RequireReaderArg`/`RequireWriterArg`, `SuspendEnd`, `TailStep` (asm.dart test-helpers, never emitted by production codegen); `UnionSiAndGoto`/`ResetAndGoto` (@deprecated, subsumed by `ClauseNext`); 2-arg `GetVariable`/`GetValue` v1 forms (§12.8/13.1 REMOVED). Each named in the `opcodes.gleam` module note.
- **Spec-gap ops (parity with reference)**: `NoReaders`, `GroundEqual`, `PutBoundConst`, `PutBoundNil`, `Distribute`, `Transmit` are live in the Dart reference (emitted by codegen, dispatched by runner) but have no §-section — ported for corpus parity, doc-gap escalated separately (per the module note), not invented by Gleam.
- **Arithmetic/type guards are NOT dedicated opcodes on either side**: Dart `codegen.dart:515` emits ALL guards (incl. `<`/`>`/`=:=`, type guards) as generic `bc.Guard(guard.predicate, arity, negated)`. The §19.3 `guard_less`/§19.4 `guard_ground` forms are documentation, not emitted opcodes. Gleam's generic `Guard(pred, arity, negated)` is therefore **parity**, not a divergence.
- **In-suite mirror of DiscriminantCompletenessTests.cs**: T012 `opcodes_test.gleam` — `every_variant_has_a_mnemonic` (total over the inventory) + `mnemonics_are_pairwise_distinct` + `unified_instructions_flip_on_is_reader`. Green in the 465 floor.

### `bytecode-mode-conversion` — polarity + WxW
- Polarity: the unified `GetVariable`/`GetValue` (and `HeadVariable`/`PutVariable`/`SetVariable`/`UnifyVariable`) carry `is_reader`, rendering distinct writer/reader mnemonics — the §12 mode-aware model.
- WxW: `unify.gleam` header — "binding ONLY writers (never readers, never writer-to-writer) … reports WxW as `HeapError` (never `Fail`); no occurs-check"; line 88-89 returns `WriterToWriter` loudly (SC-004). Matches §15 ("writer-to-writer must FAIL immediately, definitive"). Covered by `writer_mgu_adversarial_test`/`unify_test`/`heap_test`/`parity_test` (green).
- ⚠️ **Caveat — the WP-named `test_wxw.glp` does NOT exercise runtime WxW.** Its clause `wxw_test(X, Y).` (two writers, no readers) is **SRSW-rejected at load, byte-identically** on both — Dart `SRSW violations found: … Variable "X"/"Y" has no reader`; Gleam `StagedError(SrswStage, SrswViolation, …, "… Variable \"X\"/\"Y\" has no reader")`. Through the harness all three "→ failed" (goal runs against the empty program). So `test_wxw.glp` agrees across runtimes only as **SRSW-rejection parity**; the runtime WxW is confirmed by source (`unify.gleam:88-89`) + the adversarial suite, not by this program. The close should note the program is mislabeled/insufficient for runtime WxW.

### `bytecode-lint` — ABSENT
`glp/lint.gleam` header: "placeholder … carries no exported definitions and no ported GLP semantics yet (tasks T009-T016 · research R-006). The heavy port lands in a downstream feature (F4+)." `rg -n 'lint' glp_gleam/src` → only this placeholder. Dart `glp_runtime/lib/lint/linter.dart` is the un-ported source of truth.

## Activation

`close-bytecode-bytecode-instruction-set` (b3-c1-030) — **ACTIVATED**, but narrow:
1. **`bytecode-instruction-set` + `bytecode-mode-conversion` = CONFIRM** (delivered; no fill-in port needed). The close's "discriminant-completeness test mirroring DiscriminantCompletenessTests.cs" is substantially met by T012 (mnemonic totality); if the engineer wants an exact encode-completeness mirror (every opcode has a **codec** registry entry, like the C# reflection test), that is a small test-add, not a runtime gap.
2. **`test_wxw.glp` finding** — the close's "test_wxw.glp agrees with Dart" acceptance is met only as SRSW-rejection parity; to actually pin runtime WxW the close should add/point-to a program that loads and then fails writer-to-writer at runtime (or cite `writer_mgu_adversarial_test`).
3. **`bytecode-lint` = the only real gap** — recommend **auxiliary-optional** (the linter is a dev-time bytecode checker, off the runtime/parity/language-surface critical path; `lint.gleam` already frames the heavy port as downstream F4+). Engineer call per the close deliverable ("ported OR explicitly recorded auxiliary-optional"). `bytecode-runner` opcode-implementation gaps (Distribute/Transmit/Requeue/Allocate runtime) are owned by `close-bytecode-runner-missing-opcodes` (b2-c2-005) + `close-module-system-runtime-rpc`, not this close.
