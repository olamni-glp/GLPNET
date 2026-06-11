---
description: "Task list for IL/Bytecode Round-Trip Codec Spike"
---

# Tasks: IL/Bytecode Round-Trip Codec Spike

**Input**: Design documents in `specs/029-il-codec-spike/` (plan.md, spec.md, research.md, data-model.md, contracts/il-codec-contract.md, quickstart.md)
**Tests**: INCLUDED — the verification harness IS the deliverable (FR-007/FR-008); test tasks are first-class.
**Organization**: grouped by user story (US1 P1 MVP → US2 P2 → US3 P3), then the phased-b increment, then polish.
**Remediations folded** (analyze findings, 2026-06-11): F4→T000, F1→T012 (loud-fail) + T029 (reflection completeness), F2→T015 (empty exemption), F3→T013/data-model (≥10 floor), F5→T031/T032/T033 (prereq verification). See `analysis.md`.

## Format: `[ID] [P?] [Story] Description`
- **[P]**: can run in parallel (different files, no dependency)
- All paths are repo-relative. Source: `csharp/glp_il_codec/`; tests: `csharp/glp_il_codec.tests/`; Lean: `csharp/glp_il_codec/lean/IlCodecRoundTrip/`.

---

## Phase 0: Baseline (Constitution VII — green before change)

- [X] T000 Run the existing suites green and commit a baseline checkpoint BEFORE any spike code: REPL suite (`DART="C:/Users/gavri/dart-sdk/bin/dart.exe" bash test/run_all_tests.sh`), codeconv `pytest`, and `dotnet build`/`dotnet test csharp/glp_link.tests`. The spike is purely additive (new projects; FR-012 forbids touching existing code), so a green baseline attributes any later failure correctly. (Remediation F4.)

---

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 Create `csharp/glp_il_codec/GlpIlCodec.csproj` (net10.0, Nullable/ImplicitUsings enable, RootNamespace `GlpRuntime.IlCodec`) referencing `..\..\out\csharp\glp_runtime_net.csproj` and `..\glp_link\GlpLink.csproj` (FrameCodec). Clobber-safe location, no Dart preimage.
- [X] T002 Create `csharp/glp_il_codec.tests/GlpIlCodec.Tests.csproj` (xUnit, matching `glp_link.tests`) referencing `GlpIlCodec`, `glp_runtime_net`, and `GlpLink`.
- [X] T003 [P] Scaffold the Lean project `csharp/glp_il_codec/lean/IlCodecRoundTrip/` (`lakefile.lean` + `IlCodecRoundTrip/Basic.lean`, mathlib dep) — `lake build` compiles an empty stub.
- [X] T004 [P] Add `csharp/glp_il_codec.tests/Corpus.cs` scaffold: a helper that compiles a named `programs/` GLP source to a `BytecodeProgram` + `VariableMap` via the standard pipeline (no new language constructs — A7).

---

## Phase 2: Foundational (Blocking Prerequisites)

**⚠️ CRITICAL**: the codec core below must exist before ANY gate (US1/US2/US3) can run.

- [X] T005 [P] Implement `PayloadHeader.cs` — `version=0x01` + `payloadType=0x10 (IL_PROGRAM)`; read/write + version/type-mismatch loud check (no new `FrameKind` value — A4).
- [X] T006 [P] Implement v1 family in `OpcodeDiscriminant.cs` — closed 1-byte discriminant table for every concrete `IOp` in `out/csharp/lib/bytecode/opcodes.cs` (incl. `[Obsolete]` `UnionSiAndGoto`/`ResetAndGoto`, A3). Family prefix `0x01`.
- [X] T007 [P] Implement v2 family in `OpcodeDiscriminant.cs` — 1-byte discriminant for the 6 `IOpV2` classes (`opcodes_v2.cs:13`) + the `isReader` byte (`opcodes_v2.cs:32,60,88`). Family prefix `0x02`.
- [X] T008 Implement `ConstantCodec.cs` — recursive sub-encoder for the closed whitelist `null|bool|int64|double|string|Rt.ConstTerm|Rt.StructTerm` (ctags 0x00–0x06, D1); `Rt.StructTerm` recurses to arbitrary depth (FR-005); out-of-whitelist → `IlCodecException` (FR-006).
- [X] T009 [P] Implement `VariableMap` codec block in `IlCodec.cs` (per-module `Dictionary<string,long>`, A2/result.cs:9).
- [X] T010 Implement `IlCodec.Encode(BytecodeProgram, VariableMap?)` — instruction loop, family dispatch (0x01/0x02), `Label` marker family 0x03; uses T005–T009 (depends on T005,T006,T007,T008,T009).
- [X] T011 Implement `IlCodec.Decode(byte[])` — rebuild instruction list; **recompute `Labels` via `IndexLabels`** from decoded instructions (D2); reconstruct `VariableMap` (depends on T010).
- [X] T012 Implement `IlCodecException` loud-failure paths: unknown/out-of-family instruction (D4), **a known-family instruction whose concrete class has no discriminant-table entry** (F1), out-of-whitelist constant (D1), truncated/corrupt payload (depends on T010,T011).

**Checkpoint**: `Encode`/`Decode` exist for both families + constants + varmap — gates can now run.

---

## Phase 3: User Story 1 — Round-trip is demonstrably sound (P1) 🎯 MVP

**Goal**: a downstream author sees every corpus program round-trip and execute-equivalently.
**Independent Test**: `dotnet test --filter "Category=RoundTrip|Category=Execute"` is green on the corpus.

- [X] T013 [P] [US1] Build corpus cases 1–7 in `Corpus.cs`: v1-only, v2-only, mixed v1/v2, recursive-constant, label-bearing, empty, suspension-reaching (FR-007; phase a) — selecting from `programs/`. **The full corpus MUST total ≥10 concrete compiled programs** (cases 1–9 + ≥1 named constant-type-sweep program, case 10 — see data-model corpus table); the floor is met by construction, not by counting assertions. (F3.)
- [X] T014 [US1] `RoundTripIdentityTests.cs` — structural-identity assertion `Decode(Encode(p)) ≡ p` (family + class + operands + `IsReader` + order; `Labels` equal after recompute) over the corpus (FR-002, SC-001) (depends on T011,T013).
- [X] T015 [US1] `ExecuteEquivalenceTests.cs` — run a fixed goal against `p` vs `Decode(Encode(p))`; assert identical `ExecutionResult` incl. `Suspended` status (FR-003, SC-002) (depends on T011,T013). **The empty program is exempt** from this gate (no defined goal/result) and is covered by structural identity (T014) only. (F2.)

**Checkpoint**: MVP — the round-trip is proven sound on the corpus (phase a). Spike has delivered its core value.

---

## Phase 4: User Story 2 — Correctness contract pinned for reuse (P2)

**Goal**: each guarantee in `contracts/il-codec-contract.md` maps to a named, passing gate.
**Independent Test**: open the contract; every guarantee row resolves to a green test.

- [X] T016 [P] [US2] `ConstantWhitelistTests.cs` — out-of-whitelist constant value raises `IlCodecException`; zero silent drops (FR-006, SC-004) (depends on T012).
- [X] T017 [US2] `GlpPropertyGateTests.cs` — the five named gates: `Srsw_PolarityPreserved`, `PhaseOrderingPreserved`, `CommitPositionPreserved`, `SuspensionPreserved`, `ThreeValuedOpcodesPreserved` (FR-004, SC-005) (depends on T014).
- [X] T018 [P] [US2] Add corpus case 8 (obsolete-opcode program) + assert exact round-trip of `UnionSiAndGoto`/`ResetAndGoto` (A3) (depends on T006,T013).
- [X] T032 [US2] **Prereq for T019 (F5b)**: verify `csharp/glp_link` `FrameCodec` exposes a public API that wraps an arbitrary `byte[]` payload as `Whole`/`Fragment`. If the surface is internal, adjust the ride-check (e.g., `InternalsVisibleTo` or a thin public shim) rather than weakening the gate (depends on T001).
- [X] T019 [US2] `FrameRideTests.cs` — payload rides `GlpLink` `FrameCodec` as `Whole` and `Fragment` unchanged; `FrameKind` enum untouched (A4) (depends on T005,T010,T032).
- [X] T020 [US2] Cross-check `contracts/il-codec-contract.md`: confirm every Guarantee 1–8 row names a gate that exists and passes; correct any drift (FR-011, SC-006) (depends on T014,T015,T016,T017,T019).

**Checkpoint**: the contract is reusable — guarantees verified, not asserted.

---

## Phase 5: User Story 3 — Confidence bar (coverage + formal) (P3)

**Goal**: coverage gate + Lean simplified-model proof raise confidence beyond examples.
**Independent Test**: coverage report shows 100% concrete-class exercise; `lake build` is sorry-free.

- [X] T021 [US3] `CoverageGateTests.cs` — assert every concrete v1 `IOp` and v2 `IOpV2` class is exercised by ≥1 encode+decode (FR-008, SC-003); run the D7 constant-type sweep over compiled `programs/` to confirm the D1 whitelist is empirically complete (depends on T013,T018).
- [X] T029 [US3] `DiscriminantCompletenessTests.cs` — by reflection over `glp_runtime_net`, assert **every** concrete `IOp` and `IOpV2` subtype has a discriminant-table entry (independent of corpus), and that `Encode` of a synthesized instruction whose class lacks an entry fails loud with `IlCodecException`. Closes the silent-gap a corpus-only coverage gate (T021) would leave for an opcode class no corpus program uses. (F1; strengthens SC-004/FR-008) (depends on T006,T007,T012).
- [X] T031 [US3] **Prereq for T022 (F5a)**: verify the formal-gate toolchain is available on this host — Lean 4 (`elan`/`lake`), mathlib fetch, and the Lean-LSP-MCP connector (no external LM API — A6). If unavailable, escalate (blocks SC-007) rather than silently skipping the formal gate (depends on T003).
- [X] T022 [US3] Lean simplified model in `IlCodecRoundTrip/Basic.lean` — `inductive Op` (v1 subset), `inductive Const` (null|bool|int|str ground), `encode`/`decode`; via Lean-LSP-MCP, no external LM API (A6, Constitution V) (depends on T003).
- [X] T023 [US3] Lean theorem `roundtrip (p) : decode (encode p) = p` — **sorry-free**, `lake build` green (FR-010, SC-007) (depends on T022).

**Checkpoint**: all three stories' in-scope gates green (phase a).

---

## Phase 6: Phase b — Heap-embedded `ModuleTerm` (FR-009b) ⚠️ accepted-risk increment

**Goal**: extend round-trip soundness to `ModuleTerm`-embedded programs reached as heap data.
**Note**: bounded per D5 — round-trips *embedded programs*, does NOT design #7's snapshot envelope.

- [X] T033 [US1] **Prereq for T024 (F5c)**: define and verify how the phase-b test heap is constructed — drive the engine through module activation so a `ModuleTerm` is stored on the `Heap` (`glp_activation.cs:78-89`), confirming a real embedded `BytecodeProgram` is reachable; document the construction in `Corpus.cs` (depends on T011).
- [X] T024 [US1] Implement `HeapWalk.cs` — locate `ModuleTerm` instances on the engine `Heap` (`terms.cs:146-156`, `glp_activation.cs:78-89`) and read `ModuleTerm.Bytecode` (always a `BytecodeProgram`) (depends on T011).
- [X] T025 [US1] `IlHeapCodec.RoundTripEmbedded(Heap)` + corpus case 9 (heap-embedded `ModuleTerm`): round-trip each embedded program via `IlCodec`; structural + execute equivalence (FR-009b) (depends on T024,T014,T015).

**Checkpoint**: phase b complete — full codec ready for #7 + #11 consumers.

---

## Phase 7: Polish & Decision

- [ ] T026 [P] Pin the correct Typed-Datalog-IR citation in `research.md`/seed (seed formal-tooling §4 open item).
- [ ] T027 Run `quickstart.md` end-to-end; record the **keep-or-throwaway** decision (A8) and feed findings back into the seed/dossier.
- [ ] T028 [P] Baseline-green re-check of existing suites (no runtime/scheduler/compiler/REPL change — FR-012); commit only the spike's paths (Constitution VII).

---

## Dependencies & Execution Order

- **Baseline (T000)** runs FIRST, before Setup (Constitution VII).
- **Setup (T001–T004)** → no deps; T003/T004 parallel.
- **Remediation tasks**: T029 after T006,T007,T012 · T031 before T022 · T032 before T019 · T033 before T024.
- **Foundational (T005–T012)** BLOCKS all stories. T005/T006/T007/T009 parallel; T008 then T010 then T011 then T012.
- **US1 (T013–T015)** after Foundational → MVP.
- **US2 (T016–T020)** after Foundational; T017 builds on T014; T020 closes after its gates.
- **US3 (T021–T023)** after Foundational; T021 after corpus (T013,T018); T022→T023 (Lean) parallel to the C# stories after T003.
- **Phase b (T024–T025)** after US1 (needs T011,T014,T015).
- **Polish (T026–T028)** last.

### Parallel opportunities
- T003 + T004 together.
- T005 + T006 + T007 + T009 together (distinct files).
- The Lean track (T022→T023) runs in parallel with the C# US1/US2 work once T003 is done.
- T016 + T018 together; T026 + T028 together.

## Implementation Strategy

**MVP = Phase 1 + Phase 2 + Phase 3 (US1).** Stop and validate: if round-trip identity +
execute-equivalence are green on the corpus, the spike's core question is answered. US2 (contract),
US3 (coverage + Lean), and Phase b are incremental confidence/scope additions, each independently
demonstrable. Phase b is the accepted-risk increment and the first candidate to descope under
effort pressure (plan Complexity Tracking).

## Notes
- Tests included because the harness is the deliverable; within a story, the codec core (Phase 2)
  must compile before its gates run (realistic order for a verification spike).
- No `skipSRSW`, no `OPENAI_API_KEY`/`litellm`/`openai` anywhere (Constitution III, V).
- Commit after each logical group, staging only `csharp/glp_il_codec*/**` + `specs/029-*` (Constitution VII).
