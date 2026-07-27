<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-codec-compiled-il-on-the-wire` (WP b3-c1-011, wave 2)

**Date**: 2026-07-22
**Method**: source-verification + Gleam-side execution — the 038 golden vectors re-run against the Gleam codec through `gleam test` (the WP's risk is "module presence ≠ byte parity", so this verdict rests on a live golden rerun, not on module existence).
**Paired downstream**: **no close-WP activation.** The delivered codec seam is DELIVERED; the IL-codec family is **owner-deferred, not a gap**. This verify's existence check + the `b1-c1-040`/`b1-c1-042` deferral records are the cited evidence for the rule-request **`rule-codec-compiled-il-on-the-wire` (b3-c2-024)**, which asks the engineer to rule compiled-IL-on-the-wire out-of-scope as a post-feature follow-on.

## Environment / commands run

- `wsl -e bash -lc 'cd glp_gleam && gleam clean && gleam test'` (WSL Ubuntu, gleam 1.17.0, OTP-25 clean build) → **465 passed, no failures** — the pinned floor. This suite includes `test/glp/codec/golden_corpus_test.gleam`, which **reads `../specs/038-result-codec-and-framecodec-ride/contracts/golden/corpus.hex` live via `file:read_file/1`** (not inlined bytes) and asserts `encode(corpus[name]) == golden_bytes[name]` per non-gated entry plus a name-set drift guard — so a stale/edited golden set fails the suite rather than false-passing.
- `grep -rn 'il_codec' glp_gleam/src` → **no matches** (no Gleam IL codec module).
- `rg -n 'il_codec|envelope|tlv' glp_gleam/src` → the three delivered codec modules (`glp/codec/{term_codec,result_envelope,result_envelope_builder}.gleam`) + envelope consumers (`repl/results`, `engine`, `engine/output_capture`, …); **no `il_codec`**.
- `ls glp_gleam/src/glp/bytecode/` → `opcodes.gleam`, `program.gleam`, `guard_defs.gleam` — the ISA model, **no `BytecodeProgram → bytes` serializer**.
- Cross-wave corroboration: `guard-atomvm-gated-probe` (RESOLVED this wave) ran the **gated** term-codec entries (float `0x03`, int64 edges) on real AtomVM 0.7.0-alpha.1 → byte-identical + round-trip `true`, extending the term-codec byte-parity evidence to the entries `corpus.hex` deliberately excludes.

## Verdict table

| # | detail_id | verdict | basis |
|---|-----------|---------|-------|
| 1 | `term-codec-tlv` | **DELIVERED** (byte-parity) | Section-15 TLV term codec (`term_codec.gleam`): LEB128 varints, 8-byte LE int64, IEEE-754 double LE, varint+UTF-8 strings, tags `0x02..0x06` + `0x07` unbound-VarRef; loud-fail on malformed. Byte-identity to the Dart source-of-truth proven by the live `corpus.hex` rerun; gated float/int64 edges additionally confirmed on real AtomVM this wave. |
| 2 | `result-envelope-codec` | **DELIVERED** | The `0x01`/`0x11` result envelope (`result_envelope.gleam`): status, bindings, var-to-writer, suspended, captured, error, canonical order. `result_envelope_codec_test` + golden cross-decode (T028) green. |
| 3 | `result-envelope-builder` | **DELIVERED** | Depth-bounded deep-resolve builder with truncation + circular markers (`result_envelope_builder.gleam`). `deref_fidelity_test`, `cyclic_term_test`, `suspended_acceptance_test` green. |
| 4 | `il-codec` | **ABSENT — by design** | No `il_codec` module anywhere in `glp_gleam/src`; `bytecode/` holds the ISA model (`opcodes`, `program`, `guard_defs`) but no `BytecodeProgram → bytes` serializer. The engine assembles goal-terms over **pre-compiled** bytecode and does not emit IL at runtime (`b1-c1-040`). |
| 5 | `il-codec-round-trip` | **ABSENT — by design** | No IL codec ⇒ no IL round-trip. Same owner-recorded deferral; not a missing-implementation gap. |
| 6 | `compiled-il-on-the-wire` | **ABSENT — owner-deferred (NOT a gap)** | Source text on the wire for the MVP; the compiler factored into a front-end (compiled IL on the wire) is a **deliberately-deferred follow-up with no spec dir** (`b1-c1-042`), reconciled owner-side in 026 US2 (`b1-c1-040`). Confirmed absent by construction, exactly as the deferral records state. |

**Tally**: DELIVERED 3 (the entire delivered codec seam) · ABSENT-by-design 3 (the IL-codec family — owner-deferred, feeds the rule-request, **not** a close).

## Per-capability evidence

### 1. `term-codec-tlv` — DELIVERED (byte-parity)
`glp_gleam/src/glp/codec/term_codec.gleam` is the Section-15 term portion of the result-envelope codec, a **parallel** reimplementation (not a reuse of 029; FR-007) whose byte conventions are identical to the shipped 029 `GlpRuntime.IlCodec` and the Dart source-of-truth (R9): unsigned LEB128 varints, fixed 8-byte LE int64, IEEE-754 double bit pattern (8 bytes LE), varint+UTF-8 strings, term tags `0x02..0x06`, plus the one added tag `0x07` (unbound `VarRef` → `GlobalVarId`). Malformed input fails loudly with `CodecError` (truncated reads, >64-bit varints, invalid UTF-8, unknown/029-reserved `0x00`/`0x01` tags). **Byte-parity is proven, not assumed**: `golden_corpus_test.encode_reproduces_pinned_golden_test` re-reads `corpus.hex` and byte-matches for every non-gated shape (`empty_success`, `success_atom`, `success_int`/`_negative`, `success_string`, `success_nested_struct`, `success_list`, `success_deep_nested`, `multi_binding`, `var_to_writer`, `suspended`/`_with_binding`, `failed`). The gated shapes (`0x03` float, 64-bit-int edges) — excluded from `corpus.hex` as round-trip-only (R11/R6) — were separately run on **real AtomVM** this wave (`guard-atomvm-gated-probe`) and are byte-identical there too.

### 2. `result-envelope-codec` — DELIVERED
`result_envelope.gleam` encodes/decodes the ED-1 `0x01`/`0x11` envelope as one wire contract (status, bindings, var-to-writer, suspended set, captured, error) in canonical order. `result_envelope_codec_test` and the golden T028 cross-decode (decode the Dart-authored golden bytes back to the corpus envelope) are in the 465-green suite. The `captured` field stays always-empty per the recorded owner-approved deferral (codec-envelope freeze), so the parity criterion needs no masking (R4).

### 3. `result-envelope-builder` — DELIVERED
`result_envelope_builder.gleam` is the depth-bounded deep-resolve builder (heap-address → global-var-identity, truncation + circular markers, suspended-status by global var ids). `deref_fidelity_test`, `cyclic_term_test`, and `suspended_acceptance_test` cover it; all green. Cyclic/cross-goal-cyclic terms are gated (owner decision D5/FORK-1) — the builder defers to runtime deref and never loops, matching the manifest's quarantine note.

### 4–5. `il-codec` / `il-codec-round-trip` — ABSENT by design
`grep -rn 'il_codec' glp_gleam/src` is empty and `bytecode/` contains no `BytecodeProgram → bytes` serializer. This is the direct code-level confirmation of `b1-c1-040`: "the engine does not generate IL at runtime (goal-term assembly over pre-compiled bytecode)". There is no IL codec to round-trip. Recorded as ABSENT-by-design (owner-recorded), **not** as a missing implementation — no reproducer is owed and no close is activated.

### 6. `compiled-il-on-the-wire` — ABSENT, owner-deferred
The MVP puts **source text on the wire**; relocating the compiler into a front-end so that compiled IL travels the wire is a designed, deliberately-deferred follow-up with no spec dir. Evidence chain:
- `b1-c1-042` (`sliceC-unconfirmed-gaps.md:90`): "Compiled-IL-on-the-wire with the compiler factored into the front-end is a designed, deliberately-deferred follow-up (roadmap refined, no spec dir)" · `roadmap-snapshot-2026-07-19.md:37 ([refined])`.
- `b1-c1-040` (`sliceC-unconfirmed-gaps.md:372`): "the parser/compiler stays engine-internal for the MVP (source text on the wire, compiler relocation a follow-up feature) and the engine does not generate IL at runtime" · `specs/026-engine-review-dossier/spec.md:70-97 (US2)`.

The existence check confirms reality matches these records: no IL-on-the-wire path exists on the Gleam side.

## Activation

**No close WP is activated.** The three delivered detail_ids need no close work; the three ABSENT detail_ids are owner-deferred by construction, not gaps.

This verdict is the cited input to **`rule-codec-compiled-il-on-the-wire` (b3-c2-024)** — a rule-request that files, for an engineer ruling, the proposal to mark `compiled-il-on-the-wire` out-of-scope as a post-feature follow-on, citing (a) this existence check and (b) the `b1-c1-040`/`b1-c1-042` deferral records. **Wave-4 dependency**: `build-fe-be-process-split` assumes source-text-on-the-wire; a ruling that instead *rejects* the deferral would change that build's wire contract, so the ruling must land before wave 4.
