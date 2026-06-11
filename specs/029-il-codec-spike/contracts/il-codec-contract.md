# IL Codec Correctness Contract (FR-011 deliverable)

This is the **pinned, locatable contract** a downstream-feature author (#7 persistence, #11
compiled-IL-on-the-wire) can read *without* reading the implementation. It states what the codec
guarantees and how each guarantee is verified.

## Public surface

```csharp
namespace GlpRuntime.IlCodec;

public static class IlCodec
{
    // Encode a raw per-module compiled program (+ optional per-module VariableMap) to bytes.
    // Throws IlCodecException on any out-of-whitelist constant or out-of-family instruction.
    public static byte[] Encode(BytecodeProgram program,
                                IReadOnlyDictionary<string, long>? variableMap = null);

    // Decode bytes back to a structurally identical program (+ VariableMap).
    // Labels are recomputed from the decoded instruction list (canonical form).
    public static DecodedProgram Decode(byte[] payload);
}

// Decode result: the rebuilt program plus its optional per-module variable map.
public sealed record DecodedProgram(BytecodeProgram Program,
                                    IReadOnlyDictionary<string, long>? VariableMap);

public sealed class IlCodecException : Exception { /* loud, attributable */ }
```

Phase b (heap-embedded) adds:
```csharp
public static class HeapWalk  // PHASE b — locate
{
    // Find every ModuleTerm-embedded BytecodeProgram stored on the engine heap.
    public static IReadOnlyList<EmbeddedProgram> FindEmbeddedPrograms(HeapFCP heap);
}

public static class IlHeapCodec  // PHASE b — round-trip
{
    // Round-trip each heap-embedded program via IlCodec. Does NOT define #7's snapshot envelope.
    public static IReadOnlyList<EmbeddedRoundTrip> RoundTripEmbedded(HeapFCP heap);
}
```

## Guarantee 1 — Round-trip structural identity (FR-002)
`Decode(Encode(p)) ≡ p` by **structural identity**: same instruction count and order; for every
instruction the same opcode **family** (v1 `IOp` / v2 `IOpV2`), the same concrete class, the same
operands, and — for v2 — the same `IsReader` polarity. Labels compare equal after recompute.
**Verified by**: `RoundTripIdentityTests` (100% of corpus, SC-001).

## Guarantee 2 — Execute-equivalence (FR-003, independent of G1)
Running a fixed goal against `p` and against `Decode(Encode(p))` yields an identical
`ExecutionStatus` — **including `Suspended`** — via the engine's own public runner seam (so the
two runs differ only in the program object). The runnable corpus uses nullary goals (`succeed`,
`fail`, `suspend`), for which the query-binding set is empty by construction, so status is the
operative witness; each run is also anchored against its intended status. **The empty program and
the synthetic opcode-coverage programs are exempt** (no defined goal/result) and are covered by
Guarantee 1 only.
**Verified by**: `ExecuteEquivalenceTests` (every runnable corpus program, incl. a genuinely
suspending one, SC-002).

## Guarantee 3 — Covered opcode families & coverage (FR-008)
Both families are covered: **v1 `IOp`** (all 53 concrete classes incl. `Label` and the `[Obsolete]`
`UnionSiAndGoto`/`ResetAndGoto`, round-tripped exactly per A3) and **v2 `IOpV2`** (all 7 classes —
the design docs' "6" undercounted `Unknown`, which carries no `IsReader` byte).
Every concrete opcode class is exercised by ≥1 encode+decode. Additionally, a **reflection
completeness check** asserts every concrete `IOp`/`IOpV2` subtype has a discriminant entry
(independent of corpus); a class with no entry fails loud — closing the gap corpus-only coverage
would leave (F1).
**Verified by**: `CoverageGateTests` (100% of concrete classes, SC-003) + `DiscriminantCompletenessTests` (T029).

## Guarantee 4 — Supported constant operand types (FR-005/FR-006)
Closed whitelist: `null | bool | int64 | double | string | Rt.ConstTerm | Rt.StructTerm`
(recursive, any depth). **Any other runtime type → `IlCodecException`** (no silent drop, no lossy
`ToString()`). Recursive `Rt.StructTerm` operands are reconstructed identically to arbitrary depth.
**Verified by**: `ConstantWhitelistTests` + the constant-type sweep (SC-004).

## Guarantee 5 — Preserved GLP design properties (FR-004/SC-005)
| Property | Gate |
|---|---|
| SRSW reader/writer polarity | `GlpPropertyGateTests.Srsw_PolarityPreserved` |
| Three-phase HEAD→GUARD→BODY ordering | `GlpPropertyGateTests.PhaseOrderingPreserved` |
| Committed-choice `Commit` position | `GlpPropertyGateTests.CommitPositionPreserved` |
| Suspension (`SuspendEnd` etc. + `Suspended` status) | `GlpPropertyGateTests.SuspensionPreserved` |
| Three-valued unification opcodes | `GlpPropertyGateTests.ThreeValuedOpcodesPreserved` |

## Guarantee 6 — Carried metadata
- **VariableMap**: per-module `Dictionary<string,long>` carried alongside each program (A2).
- **Labels**: **NOT carried** — recomputed on decode via `IndexLabels`; lookups behave identically.

## Guarantee 7 — Transport integration (A4)
The payload rides `FrameCodec` as an opaque body; the `FrameKind` enum (`Whole`/`Fragment`) is
**unchanged**. IL vs result-envelope is distinguished by the **payload-type byte** in the payload
header (`0x10 = IL_PROGRAM`), not by a new `FrameKind` value.
**Verified by**: `FrameRideTests` (payload survives Whole and Fragment framing).

## Guarantee 8 — Formal round-trip soundness (FR-010/SC-007)
A Lean 4 proof of `decode ∘ encode = id` over a simplified model (v1 family + ground constants)
compiles **sorry-free**. Located at `csharp/glp_il_codec/lean/IlCodecRoundTrip`.
*Stretch (NOT guaranteed by this spike)*: v2 + recursive constants in the model; Z3
discriminant-uniqueness; Dart byte-parity (→ #11, A5).

## Edge-case behavior (defined, not accidental)
| Case | Behavior |
|---|---|
| Empty program | round-trips to an equally empty, executable program |
| Obsolete opcodes | round-tripped exactly (discriminant preserved) — A3 |
| Derived label table | recomputed on decode; lookups identical |
| Unknown / out-of-family instruction | `IlCodecException` (loud) — D4 |
| Out-of-whitelist constant value | `IlCodecException` (loud) — D1 |
| Truncated / version-mismatched payload | `IlCodecException` (loud) |

## Explicit non-guarantees (scope boundary)
- No `CombinedProgram` (label-filtered) target — raw per-module only (A1).
- No goal-level `VariableMap` (→ #2).
- No full engine-state snapshot envelope (→ #7); phase b only round-trips *embedded programs*.
- No Dart byte-parity (→ #11, A5).
- No change to runtime/scheduler/compiler/REPL semantics (FR-012).
