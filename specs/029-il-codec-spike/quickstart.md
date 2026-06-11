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
Green means, on the ≥10-program corpus:
- `RoundTripIdentityTests` — structural identity (SC-001)
- `ExecuteEquivalenceTests` — identical `ExecutionResult` (SC-002)
- `CoverageGateTests` — every concrete v1/v2 opcode class exercised (SC-003)
- `ConstantWhitelistTests` — loud failure on out-of-whitelist constants (SC-004)
- `GlpPropertyGateTests` — the five named GLP-property gates (SC-005)
- `FrameRideTests` — payload rides `FrameCodec` (Whole + Fragment) unchanged (A4)

## Check the formal proof (SC-007)
```
cd csharp/glp_il_codec/lean/IlCodecRoundTrip
lake build
```
Success = `decode ∘ encode = id` over the simplified model (v1 + ground constants) compiles with
**zero `sorry`**.

## Demonstrate the spike's whole value (US1, no other feature present)
```
dotnet test csharp/glp_il_codec.tests/GlpIlCodec.Tests.csproj --filter "Category=RoundTrip|Category=Execute"
```
If both pass on the corpus, a downstream author (#7/#11) can trust the round-trip is sound.

## Read the contract (US2)
`specs/029-il-codec-spike/contracts/il-codec-contract.md` — covered families, constant whitelist,
carried metadata, preserved properties, edge-case behavior — without reading the implementation.

## Keep-or-throwaway decision (A8)
Keep the codec iff all in-scope gates above are green (incl. the Lean simplified-model proof). A
failing spike still delivers value: it pins *why* the round-trip is hard and what the contract must
say (record findings back into the seed / dossier).
