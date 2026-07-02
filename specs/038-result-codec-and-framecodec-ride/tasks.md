# Tasks: Result-Envelope Codec (rides the Section-15 term codec)

**Input**: Design documents from `specs/038-result-codec-and-framecodec-ride/`
**Prerequisites**: plan.md, spec.md, research.md (R1–R11), data-model.md, contracts/result-envelope-codec.md, quickstart.md

**Tests**: INCLUDED — this feature is a codec whose acceptance criteria (SC-001 round-trip, SC-002 byte-parity, SC-003 no-heap-address, SC-004 loud-fail) are test-defined; each user story's Independent Test + Acceptance Scenarios are test-shaped.

**Organization**: by user story (US1 P1, US2 P2, US3 P3). Runtime order follows the oracle pattern: **Dart = source of truth → C# = reference → Gleam = port (on 034)**.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: parallelizable (different files, no incomplete-task dependency)
- Paths are exact; per plan.md "Project Structure".

---

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 [P] Create the Dart codec module dir + empty modules `glp_runtime/lib/codec/{result_envelope.dart,term_codec.dart,result_envelope_codec.dart}` and test dir `glp_runtime/test/codec/`.
- [X] T002 [P] Create the C# clobber-safe project `csharp/glp_result_codec/` (`GlpRuntime.ResultCodec`, .csproj) + `tests/` dir; do NOT modify the shipped `csharp/glp_il_codec/`.
- [X] T003 [P] Create the Gleam codec dir + empty modules `glp_gleam/src/glp/codec/{term_codec.gleam,result_envelope.gleam}` and test dir `glp_gleam/test/glp/codec/`.
- [X] T004 Create the shared golden-corpus dir `specs/038-result-codec-and-framecodec-ride/contracts/golden/` and a `corpus-manifest.md` listing the in-scope result shapes (FB-M1-17/41/42 + FB-M2-06) and the quarantined gated shapes (float, 64-bit-int edge, cyclic) — gated entries clearly labelled per research R11.

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: the shared byte primitives + value types every story rides. No user story can begin until this is complete.

- [X] T005 [P] Define `GlobalVarId {agentId:String, localId:int64}` value type with `(agentId,localId)` identity in Dart `glp_runtime/lib/codec/result_envelope.dart`, C# `csharp/glp_result_codec/ResultEnvelope.cs`, Gleam `glp_gleam/src/glp/codec/term_codec.gleam` (data-model §4). [Gleam value types landed in term_codec.gleam — non-circular module split.]
- [X] T006 [P] Define the `ResultEnvelope` value type `{status, resolvedBindings, varToWriter, suspended, captured, error}` (immutable) in all three runtimes (same files as T005; Gleam `result_envelope.gleam`), per data-model §1. [Ordered bindings/varToWriter via insertion-ordered map (Dart) / `IReadOnlyList<KeyValuePair>` (C#) / `List(#(String,_))` (Gleam) so canonical order is part of the value — parity invariant.]
- [X] T007 [US-shared] Implement the **term sub-codec** byte primitives in Dart `glp_runtime/lib/codec/term_codec.dart`: LEB128 varint (≤64 bits else loud-fail), fixed 8-byte LE int64, IEEE-754 double-bits, varint+UTF-8 string (contract §2).
- [X] T008 [P] Mirror the term sub-codec primitives in C# `csharp/glp_result_codec/TermCodec.cs` (byte-conventions identical to 029 `ByteIo`; parallel impl, no code reuse from shipped 029).
- [X] T009 [P] Mirror the term sub-codec primitives in Gleam `glp_gleam/src/glp/codec/term_codec.gleam` (BitArray; matches the same byte layout; runs on AtomVM 0.6.6).
- [X] T010 Implement `Term` encode/decode (tags `0x00–0x07`, recursive `StructTerm`, `VarRef`→`0x07 GlobalVarId`) in Dart `term_codec.dart`, riding T007; unknown tag ⇒ loud-fail (contract §3).
- [X] T011 [P] Same `Term` encode/decode (tags `0x00–0x07`) in C# `TermCodec.cs` and Gleam `term_codec.gleam`.
- [X] T012 Implement the envelope **frame header** (`version 0x01` + `payloadType 0x11`) + the section framing skeleton (status byte, length-prefixed bindings/varToWriter/suspended, capturedLen, errorPresent) encode/decode in Dart `result_envelope_codec.dart`, with **loud-fail on trailing bytes / bad version / bad payloadType / bad status / bad errorPresent** (contract §4, §5).
- [X] T013 [P] Mirror the frame header + section framing + loud-fail in C# `ResultEnvelopeCodec.cs` and Gleam `result_envelope.gleam`.

**Checkpoint**: term sub-codec + envelope frame + value types exist and loud-fail in all three runtimes.

---

## Phase 3: User Story 1 — Heap-independent result envelope across the seam (Priority: P1) 🎯 MVP

**Goal**: produce the envelope `{Status, ResolvedBindings, var→writer, suspended, captured, Error}` carrying no live heap address; a consumer reads it identically in-process vs decoded-from-bytes.

**Independent Test**: build envelopes for the M1 result corpus, assert no field references a heap address, and assert a no-heap consumer reconstructs every field; in-process value == decode(encode(value)).

### Tests for User Story 1

- [X] T014 [P] [US1] Round-trip test `decode(encode(R)) == R` field-by-field (incl. `captured` value) over the in-scope corpus — Dart `glp_runtime/test/codec/result_envelope_codec_test.dart` (SC-001, contract §8).
- [X] T015 [P] [US1] No-heap-address test: reconstruct every field with no heap handle; assert 0 live heap addresses — Dart `glp_runtime/test/codec/no_heap_address_test.dart` (SC-003, V1).
- [X] T016 [P] [US1] In-process-vs-bytes equality test: the value read in-process equals the value decoded from bytes (Acceptance #2) — Dart `result_envelope_codec_test.dart`.

### Implementation for User Story 1

- [X] T017 [US1] Implement server-side **deep-resolve** (depth-32, `$truncated` marker on bound hit, never silent) over the heap — Dart `glp_runtime/lib/codec/result_envelope_builder.dart` (reference behaviour; research R5, contract §6). Matches the existing `_ResolveDeepForTrace` depth; adds the explicit marker (additive, no semantics change). [Builder placed in a dedicated `result_envelope_builder.dart` to keep the codec value types engine-free.]
- [X] T018 [US1] Implement the **envelope builder** that collects `status` (from `ExecutionStatus`), `resolvedBindings` (deep-resolved, canonical order), `varToWriter` (GlobalVarId), `suspended` (from `DrainResult.blockingReaders`, sorted+deduped, infra goals excluded), `captured`, `error` from the engine result + `DrainResult` — Dart `result_envelope_builder.dart` (data-model §1; R10 dedupe note). Owner-ruled mappings 2026-06-30: every `rt.ConstTerm(String)`→atom; `GlobalVarId.agentId`=per-glpnet-instance unique id, `localId`=writer addr.
- [X] T019 [US1] Wire the full envelope encode/decode (frame T012 + term T010 + builder T018) end-to-end — Dart `result_envelope_codec.dart` + `result_envelope_builder.dart`. **MVP checkpoint: Dart round-trip + no-heap-address green** (built-from-real-heap envelope round-trips; 55 codec tests pass). [Live-goal `GlpEngine.runGoalToEnvelope` entry point = follow-up; the builder is proven over the real heap.]
- [X] T020 [P] [US1] C# deep-resolve + envelope builder. **PATH CORRECTED** (owner A+B, 2026-07-01): the shipped `csharp/glp_result_codec/` is charter-bound engine-free (no `glp_runtime_net` ref, FR-007), so the builder lives in a NEW project `csharp/glp_result_codec_builder/` (refs codec + runtime) behind an `IHeapView` seam (`ResultEnvelopeBuilder.cs`/`IHeapView.cs`/`HeapFcpView.cs`). Mirrors the Dart `result_envelope_builder.dart`. ✅ 7/7 tests green.
- [X] T021 [US1] C# full envelope encode/decode end-to-end — built envelope round-trips through the shipped `ResultEnvelopeCodec` (test `Build_BoundAndUnbound_…RoundTrips`). ✅
- [X] T022 [P] [US1] Gleam deep-resolve over 034 `glp/runtime/heap.deref` (recursive, depth-32, `$truncated` marker) + envelope builder on 034 `terms`. **PATH CORRECTED** (2026-07-01): placed in a NEW `glp_gleam/src/glp/codec/result_envelope_builder.gleam` (not `result_envelope.gleam`) — consistent with the ratified C# decision A (separate builder) and the Dart reference's own `result_envelope_builder.dart` split, keeping the pure codec free of runtime heap/terms imports/collisions. `deep_resolve` threads the `Heap` (034 deref is address-based + path-compressing) and surfaces heap errors loudly (`DerefFailed`). **U1** honored: `agent_id`/`status`/`blocking_readers` are explicit params. ✅ 6 new tests green (74 total).
- [X] T023 [US1] Gleam full envelope encode/decode end-to-end — built envelope round-trips through the shipped `result_envelope.encode`/`decode` (test `build_envelope_bound_unbound_and_roundtrip_test`). ✅
- [X] T024 [P] [US1] Port the round-trip + no-heap-address tests to C# (`csharp/glp_result_codec/tests/`) and Gleam (`glp_gleam/test/glp/codec/result_envelope_codec_test.gleam`). [C# 84/84, Gleam 68/68 green; both verified independently.]
- [X] T025 [US1] Add the suspended-status acceptance case (Acceptance #3): a suspended goal emits `Status=suspended` + the blocking-reader set, no heap address leaks — tests in all three runtimes. New dedicated files `suspended_acceptance_test.{dart,cs,gleam}`: assert `Status=Suspended`, the exact blocking-reader `GlobalVarId` set, and that the remaining variable inside a partial binding is a `VarRef(GlobalVarId)` (never a raw addr). ✅ Dart +2, C# 113, Gleam 79 green.

**Checkpoint**: US1 fully functional + independently testable in all three runtimes.

---

## Phase 4: User Story 2 — Cross-runtime byte-parity of the codec (Priority: P2)

**Goal**: Dart, C#, Gleam encode the same logical result to **byte-identical** output (FB-M2-06).

**Independent Test**: for the shared corpus, run each runtime's encoder, diff byte streams; require 100% identical (captured masked).

### Tests for User Story 2

- [X] T026 [P] [US2] Golden byte-identity test in Dart: `encode(R) == golden(R)` for the non-gated corpus — `glp_runtime/test/codec/golden_corpus_test.dart` (SC-002).
- [X] T027 [P] [US2] Golden byte-identity tests in C# (`csharp/glp_result_codec/tests/GoldenByteIdentityTests.cs`) and Gleam (`glp_gleam/test/glp/codec/golden_corpus_test.gleam`) — each READS the pinned `corpus.hex` (drift guard, not inlined) and asserts `encode(corpus[name]) == golden[name]` for all 13 non-gated entries + name-set coverage (captured masked, R4). Gleam reads via OTP `file:read_file/1` FFI (no new dep); hex via `int.base_parse`. ✅ C# 111 tests, Gleam 77 tests green.
- [X] T028 [P] [US2] Decode-other-runtime test (Acceptance #2): the Dart-authored golden bytes (shared byte source) decode, in C# and Gleam, back to the corpus envelope — `Decode(golden[name]) == corpus[name]` for all 13 non-gated entries. Same two test files. ✅ green.

### Implementation for User Story 2

- [X] T029 [US2] Golden-corpus generator tool — Dart `glp_runtime/tool/gen_result_golden.dart` → `specs/038-.../contracts/golden/corpus.hex` from the shared result corpus (Dart = source of truth, R9).
- [X] T030 [US2] Generate `corpus.hex` (non-gated entries only) and commit it as the pinned contract artifact.
- [ ] T031 [US2] Cross-runtime diff harness + quickstart wiring (`quickstart.md` §"golden corpus harness") — all three encoders reproduce `corpus.hex`.
- [ ] T032 [US2] **V5 oracle cross-check (C#)**: assert the result codec's term bytes (`0x00–0x06`) are byte-identical to 029 `GlpRuntime.IlCodec` `ConstantCodec` for shared term inputs — `csharp/glp_result_codec/tests/OracleConsistencyTests.cs` (FR-007 boundary: cross-check only, NOT proof for Dart/Gleam).

**Checkpoint**: non-gated corpus byte-identical across all three runtimes.

---

## Phase 5: User Story 3 — Deref + variable→writer fidelity (Priority: P3)

**Goal**: deeply-nested bound terms + unbound var→writer references encode/decode preserving GLP deref semantics (depth-bounded; writer identity), matching the Dart reference (FB-M1-17/41/42).

**Independent Test**: encode/decode the deref + var→writer corpus; assert structural + identity equality vs recorded Dart outcomes.

### Tests for User Story 3

- [X] T033 [P] [US3] Nested-bound-term fidelity test: deref resolution matches the reference up to depth-32 (no over/under-resolve; `$truncated` at the bound) — all three runtimes (Acceptance #1, contract §6). New `deref_fidelity_test.{dart,cs,gleam}` build a real heap (Dart `GlpRuntime.heap`, C# `MapHeap:IHeapView`, Gleam 034 `heap`) and pin the boundary EXACTLY: a 32-deep struct chain resolves fully (no marker); helpers `_depthToMarker`/`_containsTruncated`. ✅ Dart 6, C# builder 13, Gleam 87.
- [X] T034 [P] [US3] var→writer identity test: an unbound variable paired to a writer round-trips by **GlobalVarId** (`agentId:localId`), identity preserved — all three runtimes (Acceptance #2, R7). Same files: multiple unbound query vars → ordered var→writer by (agentId, localId); an unbound var nested in a bound struct keeps its GlobalVarId; both survive the codec round-trip. Per-runtime (localId = local writer addr, R7). ✅

### Implementation for User Story 3

- [X] T035 [US3] Author the deref + var→writer fidelity corpus (depth-1, depth-32-bound, multi-var→writer) under `specs/038-.../contracts/golden/` (non-gated), with recorded Dart outcomes as the reference. `contracts/golden/deref-corpus.md` — 5 vectors + recorded Dart outcomes; notes which are address-independent (byte-identical cross-runtime) vs identity-preserving per-runtime. ✅
- [X] T036 [US3] Ensure the canonical serialization order (bindings/varToWriter/suspended) is deterministic + identical across runtimes (parity invariant, data-model §1) — verified by T033/T034 across runtimes. New `canonical_order_test.{dart,cs,gleam}`: encode is deterministic + bindings/varToWriter keep declaration order (non-alphabetical insertion order — map iteration MUST NOT leak); cross-runtime identity additionally pinned by golden `multi_binding`/`var_to_writer`. ✅
- [X] T037 [US3] Add the depth-32 truncation-marker fidelity case to the corpus + assertions (matches reference depth, explicit marker; R5). In `deref_fidelity_test.*`: a 33-deep chain yields `$truncated` at EXACTLY depth 33 (`_depthToMarker == 33`), and the marker is a normal decodable term (never a silent cut). ✅ all three runtimes.

**Checkpoint**: all three user stories independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T038 [P] Loud-fail fuzz suite (SC-004, V4): trailing/garbage bytes, unknown tag, bad version/payloadType/status/errorPresent, truncated input ⇒ reject; assert **0** silent acceptances — all three runtimes. New `loud_fail_fuzz_test.{dart,cs,gleam}`: for every non-gated corpus entry — trailing garbage + EVERY strict truncation; plus corrupt header bytes (ver/ptype/status/errPresent) + unknown/reserved term tags (0x00/0x08/0x09/0x20/0xFF). Counts silent acceptances; asserts **0**. ✅ Dart 3, C# 118, Gleam 90.
- [ ] T039 [P] Gated-cases quarantine: **float** (`0x03`) corpus run + decode-verified on **AtomVM 0.6.6** (`/opt/atomvm/AtomVM-static`, ED-6 `/float` spike, FR-011); record result. Keep in a separate corpus section, NOT in the SC-002 byte-final assertion (R11).
- [ ] T040 [P] Gated-cases quarantine: **64-bit-int edge** corpus run on AtomVM-static (Gleam `Int` bignum masking — plain `gleam test` is NOT an AtomVM-faithfulness signal); record. NOT byte-final.
- [X] T041 [P] Cyclic-term **defer-to-runtime** test: a cyclic/cross-goal term encodes via depth-bounded deref and never loops; assert consistency with runtime deref, do NOT define codec-local cycle policy (FR-008, D5/FORK-1 OPEN — surface, never self-decide). New `cyclic_term_test.{dart,cs,gleam}`: a self-referential struct `s(Self)` resolves to the depth-bounded `$truncated` marker (terminates = no-loop proof; explicit marker = no silent cut) and round-trips. **TEST only** — the D5/FORK-1 codec-cycle-policy decision remains OPEN and is surfaced to the owner, NOT self-decided. ✅ Dart 1, C# builder 14, Gleam 91.
- [ ] T042 [P] (Optional) Lean `decode∘encode=id` round-trip proof for the term sub-codec, mirroring 029's `lean/IlCodecRoundTrip/` — `csharp/glp_result_codec/lean/` (sorry-free; simplified ground-term model).
- [ ] T043 Record the **#36 handoff** fact in the contract/handoff note: `FrameCodec.cs:64 OffKind` = fragmentation discriminant, NOT payload-type → #36 needs a payload-type prefix byte (this codec reserves `0x11`); framing/transport are out of scope here (FR-006).
- [ ] T044 [P] Documentation: confirm `data-model.md`/`contracts/` match the shipped bytes; update `quickstart.md` if paths changed.
- [ ] T045 Run `quickstart.md` validation end-to-end (build + test all three runtimes + golden harness); confirm the Definition of Done checklist.

---

## Dependencies & Execution Order

- **Setup (Phase 1)**: no dependencies.
- **Foundational (Phase 2)**: depends on Setup; **BLOCKS all user stories** (term sub-codec + frame + value types).
- **US1 (Phase 3, P1)**: after Foundational; the MVP. T017→T018→T019 (Dart) gate the Dart slice; C#/Gleam slices (T020–T023) parallel after their foundational primitives.
- **US2 (Phase 4, P2)**: after US1 (needs working per-runtime encoders to diff); T029→T030→T031.
- **US3 (Phase 5, P3)**: after US1; mostly assertion + corpus on the US1 implementation.
- **Polish (Phase 6)**: after the desired stories; gated cases (T039–T041) need the AtomVM toolchain.

### Within each story
- Tests written to FAIL before implementation (round-trip/no-heap/byte-parity).
- Value types + primitives (Foundational) before builders; builders before end-to-end encode/decode.
- Dart (source of truth) before C#/Gleam in each story; the golden is authored from Dart.

### Parallel opportunities
- T001/T002/T003 (per-runtime scaffolds) parallel.
- Within Foundational: C# + Gleam mirrors (T008/T009, T011, T013) parallel once the Dart reference (T007/T010/T012) lands.
- US1 C#/Gleam slices (T020–T023) parallel after Foundational.
- Polish gated-case runs (T039/T040/T041) parallel.

---

## Implementation Strategy

### MVP (User Story 1, Dart slice)
1. Phase 1 Setup → Phase 2 Foundational (Dart term codec + frame + types).
2. Phase 3 US1 Dart: T017 deep-resolve → T018 builder → T019 end-to-end → T014/T015 green.
3. **STOP & VALIDATE**: Dart round-trip + no-heap-address green = the envelope seam is real. (Marathon MVP sub-checkpoint here.)

### Incremental delivery
- US1 across all three runtimes → US2 byte-parity golden → US3 fidelity → Polish (loud-fail + gated-cases quarantine + handoff).
- Byte-parity-**final** is explicitly deferred: float/64-bit/cyclic stay quarantined and the whole-Section-15 "final" declaration waits on the D4 ISA freeze + ED-6 (FR-009/FR-010) — NOT a task to "complete" here.

## Notes
- [P] = different files, no incomplete-task dependency.
- 029 is the C# byte-layout **oracle only** (FR-007) — never cited as proof for the Dart/Gleam path.
- Commit after each task or logical group; commit only files you worked on (Constitution VII).
- Gated cases (float/64-bit/cyclic) are surfaced + quarantined, never self-decided (D5 open; ED-6/D4 gates).
