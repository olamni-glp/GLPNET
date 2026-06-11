# Implementation Plan: IL/Bytecode Round-Trip Codec Spike

**Branch**: `029-il-codec-spike` | **Date**: 2026-06-11 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/029-il-codec-spike/spec.md`
**Authoritative design source**: `docs/research/repl-engine-separation/reconciliation/4-il-codec-spike.md` (the seed) and dossier `specs/026-engine-review-dossier/`.

## Summary

Prove, as a throwaway-or-keep EXPERIMENT, that a compiled GLP program round-trips through a
byte codec: `compile → encode → decode → execute`. The codec is **net-new C#**, living in a
clobber-safe project that references the regenerated engine product `out/csharp/glp_runtime_net.csproj`.
Equivalence is **structural identity** (decode reproduces the exact opcode objects — family,
`IsReader` polarity, operands, order), with **execute-equivalence** as an independent gate.
Scope covers both opcode families (v1 `IOp`, v2 `IOpV2`), recursive ground constant operands,
labels, and the per-module `VariableMap`; delivered in **two phases** — (a) per-module
`BytecodeProgram` round-trip, then (b) heap-embedded `ModuleTerm` traversal. A **Lean 4**
mechanized proof of `decode ∘ encode = id` over a simplified model (v1 family + ground
constants, sorry-free) establishes the formal confidence bar, driven via Lean-LSP-MCP with no
external LM API. The deliverable pins a locatable **correctness contract** (covered opcode
families, constant whitelist, carried metadata, preserved GLP properties, edge-case behavior).

The spike changes **no** runtime, scheduler, compiler, or REPL semantics (FR-012); it adds
only the codec, its verification harness, and the Lean model.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`, `LangVersion latest`, `Nullable enable`,
`ImplicitUsings enable`) — matching `csharp/glp_link/GlpLink.csproj`. Formal gate: **Lean 4**
(+ `mathlib`).
**Primary Dependencies**: the regenerated engine product `out/csharp/glp_runtime_net.csproj`
(`BytecodeProgram`, `IOp`/`IOpV2` families, `UnifyConstant`/`BodySetConst`, `Rt.StructTerm`,
`CompilationResult.VariableMap`, `ModuleTerm`, the compiler + runner for the execute-equivalence
harness); `csharp/glp_link/GlpLink.csproj` (`FrameCodec` — for the ride-as-payload integration
check); `xUnit` (matching `csharp/glp_link.tests`); Lean-LSP-MCP for the formal gate.
**Storage**: N/A — byte payloads are in-memory `byte[]`; the corpus is compiled from `programs/`.
**Testing**: xUnit (C#) for the pragmatic gates; `lake build` / Lean proof-check for the formal gate.
**Target Platform**: .NET 10 runtime (Windows dev host); Lean 4 toolchain (elan/lake).
**Project Type**: library (codec) + verification harness (tests) + formal model (Lean) — three
co-located deliverables under a single clobber-safe C# project plus a Lean sub-project.
**Performance Goals**: not latency-bound; correctness spike. Target: the full ≥10-program corpus
round-trips + execute-compares within a normal `dotnet test` run; the Lean simplified-model proof
checks in a single `lake build`.
**Constraints**: no external LM API (Constitution V; A6); no new `FrameKind` enum values
(A4 / seed T2 option 3 — payload-type byte lives in the payload header, transport frame contract
untouched); structural identity (FR-002); zero silent failures (SC-004); no changes to
runtime/scheduler/compiler/REPL (FR-012); hand-authored C# must live outside `out/csharp/` and
the gitignored `glp_runtime_net/` (clobber-safe rule, GlpLink.csproj header).
**Scale/Scope**: both opcode families (~50+ concrete v1 `IOp` classes; 6 v2 `IOpV2` classes,
each carrying `IsReader`); recursive `Rt.StructTerm` constants to arbitrary depth; ≥10-program
corpus across the FR-007 case matrix; two delivery phases (per-module, then heap-embedded).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

Constitution v1.0.0 (`.specify/memory/constitution.md`). Evaluated against this feature's
spec/plan; no violation found.

| Principle | Verdict | Basis |
|---|---|---|
| I. Spec-First | PASS | Spec exists, clarified (3/3 forks resolved); authoritative seed + dossier quoted; this plan derives from them, not from code. |
| II. Bug-Protocol / No-Workarounds | PASS | FR-006 + SC-004 mandate *loud* failure on unsupported opcode/constant — the opposite of try/catch "robustness". No masking branches planned. |
| III. SRSW inviolable | PASS (machine-checkable) | Codec *preserves* reader/writer polarity (FR-004); zero `skipSRSW` tokens introduced in any artifact. |
| IV-a. Language Authority | PASS | A7: no new GLP language constructs; FR-012: no compiler/runtime semantic change. |
| IV-b. Preserve Working Internals | PASS | Purely additive (new project); FR-012 forbids altering existing engine internals. |
| V. Claude-Only LM / No External API | PASS (machine-checkable) | A6/FR-010: Lean driven via Lean-LSP-MCP, "no external LM API"; zero `OPENAI_API_KEY`/`litellm`/`openai` tokens in artifacts. |
| VI-a. Additive/idempotent migrations | PASS (N/A) | No DB migration — codec + Lean only; single head `0010` untouched. |
| VI-b. Single PGLite cluster | PASS (N/A) | No PGLite consumer added. |
| VII. Test-Gated, Commit-Scoped | PASS (advisory) | Baseline-green before change; xUnit gates added; commit only the spike's paths; ship via GitFlow. |
| VIII. Single Source of Truth & Traceability | PASS | Dossier #1 is the one authoritative source the spike references; roadmap → pipeline → tasks chain intact (this is epic feature #4). |

**No gate violations → Complexity Tracking is for risk-tracking only (see below), not justification.**

## Project Structure

### Documentation (this feature)

```text
specs/029-il-codec-spike/
├── spec.md              # Clarified specification (3/3 forks resolved)
├── plan.md              # This file
├── research.md          # Phase 0 — design decisions (all unknowns resolved)
├── data-model.md        # Phase 1 — entities, discriminant tables, gate map
├── quickstart.md        # Phase 1 — how to run the harness + the Lean proof
├── contracts/
│   └── il-codec-contract.md   # Phase 1 — the pinned correctness contract (FR-011 deliverable)
└── tasks.md             # Phase 2 (/buildkit-tasks)
```

### Source Code (repository root)

```text
csharp/glp_il_codec/                 # NEW — clobber-safe hand-authored codec (no Dart preimage)
├── GlpIlCodec.csproj                #   net10.0; refs ..\..\out\csharp\glp_runtime_net.csproj
│                                    #   (+ ..\glp_link\GlpLink.csproj for the FrameCodec ride-check)
├── IlCodec.cs                       #   public Encode(BytecodeProgram,VariableMap?) -> byte[] / Decode
├── OpcodeDiscriminant.cs            #   fixed-width family-prefix + per-class discriminant tables
├── ConstantCodec.cs                 #   recursive Rt.StructTerm + whitelisted-primitive sub-encoder
├── PayloadHeader.cs                 #   version + payload-type byte (rides FrameCodec untouched)
├── HeapWalk.cs                      #   PHASE b — locate ModuleTerm-embedded BytecodeProgram on heap
└── lean/IlCodecRoundTrip/          #   Lean 4 simplified-model proof (decode∘encode = id)
    ├── lakefile.lean
    └── IlCodecRoundTrip/Basic.lean

csharp/glp_il_codec.tests/           # NEW — xUnit verification harness
├── GlpIlCodec.Tests.csproj          #   refs GlpIlCodec + glp_runtime_net (+ GlpLink)
├── Corpus.cs                        #   compiles the ≥10-program corpus from programs/
├── RoundTripIdentityTests.cs        #   FR-002 structural-identity gate
├── ExecuteEquivalenceTests.cs       #   FR-003 execute-equivalence gate
├── CoverageGateTests.cs             #   FR-008 every concrete opcode class exercised
├── ConstantWhitelistTests.cs        #   FR-006 loud-fail on out-of-whitelist value
├── GlpPropertyGateTests.cs          #   FR-004/SC-005 named gates (SRSW, phase order, Commit, suspension, 3-valued)
└── FrameRideTests.cs                #   A4 payload rides FrameCodec as Whole/Fragment unchanged
```

**Structure Decision**: The codec is a **net-new, clobber-safe C# project** `csharp/glp_il_codec/`,
mirroring the `csharp/glp_link/` arrangement (hand-authored code outside `out/csharp/` so the
codeconv mirror's regen oracle never names it as an output; it references the regenerated
`glp_runtime_net.csproj` for the bytecode/compiler/runtime types rather than forking them). Tests
live in the sibling `csharp/glp_il_codec.tests/` (xUnit, matching the `glp_link.tests` convention).
The Lean 4 model is co-located under `csharp/glp_il_codec/lean/` as a self-contained `lake` project.
Dart-first authoring + scaffold (the normal convergence discipline) is **deferred to #11** with the
Dart byte-parity goal (A5); for a one-runtime spike, C#-direct in the clobber-safe location is correct.

## Phase 0 — Research

See `research.md`. All NEEDS CLARIFICATION are resolved (3 via `/buildkit-clarify`, the rest via
the seed's recommendations recorded as spec Assumptions A1–A8 + the design decisions D1–D8 below).
No open unknowns remain blocking design.

## Phase 1 — Design & Contracts

- `data-model.md` — the serialized entities, the v1/v2 discriminant tables, the constant
  whitelist, and the property→gate map.
- `contracts/il-codec-contract.md` — the pinned **correctness contract** (the FR-011 deliverable).
- `quickstart.md` — run the harness (`dotnet test csharp/glp_il_codec.tests`) and check the Lean
  proof (`lake build` under `csharp/glp_il_codec/lean/IlCodecRoundTrip`).
- Agent context: CLAUDE.md `<!-- BUILDKIT -->` block updated to point at this plan.

## Complexity Tracking

*No Constitution violations.* This table tracks the one accepted **risk** the owner ratified in
`/buildkit-clarify`, per spec-discipline (surface it; do not bury it):

| Risk | Source | Why accepted | Containment |
|---|---|---|---|
| Phase b (heap-embedded `ModuleTerm`) couples to the heap-snapshot layout that **#7 owns and has not started** | Q1 owner override of the seed's defer-to-#7 recommendation (seed T1) | Owner wants the full codec ready for #7 + #11 in one spike | Phase b is bounded to *locate + round-trip an embedded `BytecodeProgram` reached as heap data* — it does **not** design #7's full snapshot format. Phase a ships and is independently valuable even if phase b is descoped under effort pressure. |
