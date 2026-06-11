# Phase 1 Data Model — IL/Bytecode Round-Trip Codec Spike

The "data model" here is the **serialized form** of a compiled program and the discriminant
tables that make it unambiguous. Source types are in `out/csharp/glp_runtime_net.csproj`
(`out/csharp/lib/...`); the codec adds only the serialized encoding, never new runtime types.

## Serialized entities

### Byte payload (top level)
```
PayloadHeader
  byte  version          = 0x01
  byte  payloadType      = 0x10  (IL_PROGRAM)   # distinguishes IL from result envelopes (A4)
ProgramBody
  varint instructionCount
  Instruction[instructionCount]
  byte  hasVariableMap   (0|1)
  VariableMap?           (present iff hasVariableMap == 1)
```
- **No `Labels` block** — recomputed on decode via `IndexLabels` (research D2).
- Rides `FrameCodec` as an opaque payload; the `FrameKind` enum (`Whole`/`Fragment`) is untouched
  (A4 / seed T2). `payloadType` is the *only* new discriminant surface and lives in the payload.

### Instruction
```
byte family            # 0x01 v1 IOp | 0x02 v2 IOpV2 | 0x03 Label marker
byte discriminant      # per-class, within family (tables below)
<class-specific operand block>
```

### v2 opcode extra field
Every v2 `IOpV2` carries `byte isReader (0|1)` immediately after its discriminant
(`opcodes_v2.cs:32,60,88`). Preserving this byte exactly **is** SRSW preservation (Shapiro
criterion 1 / FR-004).

### Constant operand (recursive sub-encoder — `ConstantCodec`)
```
byte ctag
  0x00 null
  0x01 bool      -> byte
  0x02 int64     -> 8 bytes (long)
  0x03 double    -> 8 bytes
  0x04 string    -> varint len + UTF-8
  0x05 ConstTerm -> ctag of the wrapped primitive   (Rt.ConstTerm)
  0x06 StructTerm-> varint functorLen + UTF-8 functor + varint arity + arity × (recurse) (Rt.StructTerm)
```
- Closed whitelist (research D1). Any value whose runtime type is not in this set →
  `IlCodecException` (FR-006, SC-004). `0x06` recurses to arbitrary depth (FR-005).

### VariableMap
`CompilationResult.VariableMap` = `Dictionary<string,long>` (`result.cs:9`), per-module (A2).
```
varint count
count × ( varint nameLen + UTF-8 name + 8-byte long register )
```

## Discriminant tables (closed; `OpcodeDiscriminant.cs`)

> The exact, complete enumeration is generated from the concrete classes in `opcodes.cs` /
> `opcodes_v2.cs` and frozen in `OpcodeDiscriminant.cs`. The table below names the **families and
> the load-bearing members the gates assert on**; the implementation table is exhaustive.

### Family 0x01 — v1 `IOp` (~50+ concrete classes, `opcodes.cs`)
Representative members (full set enumerated in code): `ClauseTry`, `ClauseNext`, `TryNextClause`,
`NoMoreClauses`, `GuardFail`, `GuardNeedReader`, `Otherwise`, **`Commit`** (`:28`), `SuspendEnd`,
`HeadStructure`, `HeadBindWriter`, `GetVariable`, `GetValue`, `UnifyConstant` (`:210`, recursive
operand), `BodySetConst` (`:77`), and the `[Obsolete]` `UnionSiAndGoto`/`ResetAndGoto` (`:53-66`,
round-tripped exactly per A3).

### Family 0x02 — v2 `IOpV2` (6 concrete classes, `opcodes_v2.cs:13`)
`HeadVariable`, `GetVariable`, `GetValue`, `UnifyVariable`, `PutVariable`, `SetVariable` — each
with `IsReader`.

### Family 0x03 — `Label` marker (`opcodes.cs:16-19`)
`varint labelId` (or interned name). Position in the instruction list is preserved; the `Labels`
dict is recomputed from these on decode (D2).

**Completeness (F1)**: a reflection test (`DiscriminantCompletenessTests`, task T029) asserts that
**every** concrete `IOp`/`IOpV2` subtype has a table entry, and that `Encode` of a class with no
entry fails loud (`IlCodecException`). This closes the gap that corpus-only coverage (FR-008) would
leave for an opcode class no corpus program happens to use.

## Preserved-property → named-gate map (FR-004 / SC-005)

| GLP property | Encoded by | Verifying gate (xUnit) |
|---|---|---|
| SRSW (reader/writer polarity) | v2 `isReader` byte; distinct v1 reader/writer classes | `GlpPropertyGateTests.Srsw_PolarityPreserved` |
| Three-phase HEAD→GUARD→BODY order | instruction order is preserved verbatim | `GlpPropertyGateTests.PhaseOrderingPreserved` |
| Committed-choice boundary | `Commit` discriminant + its index | `GlpPropertyGateTests.CommitPositionPreserved` |
| Suspension | `ClauseNext`/`TryNextClause`/`NoMoreClauses`/`SuspendEnd` discriminants + exec status | `GlpPropertyGateTests.SuspensionPreserved` |
| Three-valued unification | `GuardNeedReader`/`GuardFail`/`Otherwise`/`ClauseNext`/`NoMoreClauses` discriminants | `GlpPropertyGateTests.ThreeValuedOpcodesPreserved` |

## Verification corpus (FR-007 / D7)

≥10 compiled programs from `programs/`, one row per required case:

| # | Case | Selection note |
|---|---|---|
| 1 | v1-only | legacy/simple program emitting only `IOp` |
| 2 | v2-only | program whose codegen emits only `IOpV2` |
| 3 | mixed v1/v2 | `asm.cs` builder mixes `BC.*` + `V2.*` |
| 4 | recursive constant | ground-list program → `UnifyConstant(Rt.StructTerm)` (`codegen.cs:735-759`) |
| 5 | label-bearing | multi-clause program with `Label` markers |
| 6 | empty program | zero instructions (edge case) |
| 7 | suspension-reaching | goal that reaches `ExecutionStatus.Suspended` |
| 8 | obsolete-opcode | program containing `UnionSiAndGoto`/`ResetAndGoto` (A3) |
| 9 | heap-embedded `ModuleTerm` | program that stores a `ModuleTerm` on the heap (phase b) |
| 10 | constant-type sweep | ≥1 **named** program covering every `ctag` 0x00–0x06 (guarantees the ≥10 floor) |

**Floor (F3)**: the corpus MUST total **≥10 concrete compiled programs** (cases 1–10 above); the
sweep contributes named programs, not just assertions, so FR-007's ≥10 is met by construction.
**Empty-program exemption (F2)**: case 6 is verified by structural identity only and is exempt
from the execute-equivalence gate (no defined goal/result).

## Lean simplified model (formal gate, `lean/IlCodecRoundTrip`)

```
inductive Op            -- one constructor per v1 class in the simplified subset
inductive Const         -- null | bool | int | str  (ground only)
def Program := List Op
def encode : Program → List Byte
def decode : List Byte → Program
theorem roundtrip (p : Program) : decode (encode p) = p   -- sorry-free (SC-007)
```
Start: v1 family + ground constants. Stretch (out of pass bar, D8): v2 + recursive `Const`.

## Error model

Single exception type `IlCodecException` (loud failure, FR-006/SC-004), raised on: out-of-whitelist
constant value (D1), unknown/out-of-family `Instructions` element (D4), truncated/corrupt payload
on decode, version/payloadType mismatch. Never swallowed; never a silent drop.
