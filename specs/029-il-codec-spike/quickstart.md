# Quickstart — IL/Bytecode Round-Trip Codec Spike

How to build, run the verification harness, and check the Lean proof. The spike adds two C#
projects and one Lean project; it changes nothing in the existing engine.

## Prerequisites
- .NET 10 SDK (the toolchain used by `csharp/glp_link`).
- Lean 4 toolchain (`elan` + `lake`) for the formal gate; Lean-LSP-MCP for Claude-driven tactics
  (no external LM API — Constitution V / A6).
- The regenerated engine product builds: `dotnet build out/csharp/glp_runtime_net.csproj`.

## Build the codec
```
dotnet build csharp/glp_il_codec/GlpIlCodec.csproj
```

## Run the verification harness (the deliverable gates)
```
dotnet test csharp/glp_il_codec.tests/GlpIlCodec.Tests.csproj
```
Green means **44/44**, on the ≥10-program corpus:
- `RoundTripIdentityTests` — structural identity (SC-001)
- `ExecuteEquivalenceTests` — identical `ExecutionStatus` incl. `Suspended` (SC-002)
- `CoverageGateTests` — every concrete v1/v2 opcode class exercised + D7 constant sweep (SC-003)
- `DiscriminantCompletenessTests` — reflection completeness; unregistered class fails loud (F1)
- `ConstantWhitelistTests` — loud failure on out-of-whitelist constants (SC-004)
- `GlpPropertyGateTests` — the five named GLP-property gates (SC-005)
- `ObsoleteOpcodeTests` — `UnionSiAndGoto`/`ResetAndGoto` round-trip exactly (A3)
- `FrameRideTests` — payload rides `FrameCodec` (Whole + Fragment) unchanged (A4)
- `PhaseBHeapEmbeddedTests` — heap-embedded `ModuleTerm` round-trip + execute-equiv (FR-009b)

## Check the formal proof (SC-007)
```
cd csharp/glp_il_codec/lean/IlCodecRoundTrip
lake build
```
Success = `decode ∘ encode = id` over the simplified model (v1 + ground constants) compiles with
**zero `sorry`**. (`#print axioms IlCodecRoundTrip.decode_encode` → `[propext]` only.) On Windows the
toolchain is installed via `winget install Lean.Elan` + `elan toolchain install stable`; if `lake`
is not on `PATH`, prefix it with `~/.elan/bin` (e.g. `C:\Users\<you>\.elan\bin\lake.exe`).

## Demonstrate the spike's whole value (US1, no other feature present)
```
dotnet test csharp/glp_il_codec.tests/GlpIlCodec.Tests.csproj --filter "Category=RoundTrip|Category=Execute"
```
If both pass on the corpus, a downstream author (#7/#11) can trust the round-trip is sound.

## Read the contract (US2)
`specs/029-il-codec-spike/contracts/il-codec-contract.md` — covered families, constant whitelist,
carried metadata, preserved properties, edge-case behavior — without reading the implementation.

## Keep-or-throwaway decision (A8) — DECISION: **KEEP** (2026-06-11)
All in-scope gates are green: **44/44** C# gates (phase a + phase b) + the Lean simplified-model
proof (sorry-free, `propext` only). The round-trip is demonstrably sound across both opcode
families, recursive ground constants, labels, the per-module VariableMap, the obsolete opcodes, and
a heap-embedded `ModuleTerm`; failure is loud everywhere; the payload rides `FrameCodec` unchanged.
Downstream features #7 (persistence) and #11 (compiled-IL-on-the-wire) can build on
`GlpRuntime.IlCodec` and the pinned contract. Findings fed back to the seed
(`docs/research/repl-engine-separation/reconciliation/4-il-codec-spike.md`).

**Notable findings recorded for the seed/dossier**:
- The v2 `IOpV2` family has **7** concrete classes, not 6 — the design docs undercounted `Unknown`
  (the one v2 opcode with no `IsReader` byte). The registry is exhaustive by reflection (T029).
- Execute-equivalence is run via the engine's public runner seam on nullary goals (status incl.
  `Suspended`); the harness needed `StrictTypes=false` to compile loosely-typed corpus fixtures —
  the codec is type-agnostic (it round-trips whatever bytecode compiles).
- Labels are **not** serialized; recomputing them via `IndexLabels` on decode is sufficient (D2).
- The Lean proof needed **no mathlib** — core-Lean structural induction suffices for the bar.
