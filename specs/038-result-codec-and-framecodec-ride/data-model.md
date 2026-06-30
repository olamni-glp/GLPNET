# Phase 1 Data Model: Result Envelope + Section-15 Term Codec

**Feature**: `038-result-codec-and-framecodec-ride` · **Plan**: [plan.md](./plan.md) · **Research**: [research.md](./research.md)

The authoritative wire contract is [contracts/result-envelope-codec.md](./contracts/result-envelope-codec.md); this file defines the in-memory entities each runtime builds/consumes and the term tag table they share.

---

## 1. Result Envelope (the entity)

A heap-independent, transportable result value (FR-001). Identical logical shape in Dart (source of truth), C#, Gleam.

| Field | Type | Notes |
|---|---|---|
| `status` | enum `Success \| Suspended \| Failed` | Backed by existing `ExecutionStatus` (FB-M1-41, `scheduler`/`glp_engine`). |
| `resolvedBindings` | ordered map `name → Term` | **Deep-resolved** (heap-independent) values; the realign-lite *parallel* field alongside the existing shallow `bindings` (DISPOSITIONS #11). Depth-bounded per R5. |
| `varToWriter` | ordered map `name → GlobalVarId` | Unbound variable → its writer by **global identity** `agentId:localId` (R7, FB-M1-14). No local heap address. |
| `suspended` | set of `GlobalVarId` | Blocking-reader / suspended-goal set (FB-M1-42); built from `goal_id`s, **deduped by `goal_id`** (R10 / 034 review note). Infrastructure/serve goals excluded. |
| `captured` | bytes (UTF-8) | Captured output as data. **Excluded** from the byte-parity criterion (R4, PB:9). |
| `error` | optional string | Present only when `status = Failed`. Error-kind discriminant deferred (5-…ride U4). |

**Invariant (SC-003)**: no field, transitively, contains a live heap address. Enforced by server-side deep-resolve before emission + a test that reconstructs every field with no heap handle.

**Ordering invariant (parity)**: `resolvedBindings`, `varToWriter`, and `suspended` serialize in a **deterministic canonical order** (binding/declaration order as recorded by the producing engine; identical across runtimes) so encodings are byte-identical (SC-002). Canonical order is part of the contract, not incidental map iteration order.

### Per-runtime backing

- **Dart** (source of truth): new `glp_runtime/lib/codec/result_envelope.dart` — `ResultEnvelope` built from `ExecutionResult` (`glp_engine.dart:34-48`) + `DrainResult.{SuspendedGoals,BlockingReaders}` (currently dropped before `ExecutionResult` — the envelope builder re-collects them). Reuses the deep-resolve walk (R5).
- **C#** (reference): new `csharp/glp_result_codec/ResultEnvelope.cs` — `sealed record ResultEnvelope`, fed from `ExecutionResult` (`glp_engine.cs:51-80`) + `DrainResult` (`scheduler.cs:58-91`).
- **Gleam** (port): new `glp_gleam/src/glp/codec/result_envelope.gleam` — built on 034 `glp/runtime/{terms,heap,suspension}`; `Term` from `terms.gleam`, deep-resolve over `heap.deref`.

## 2. Term (rides 034 / 029)

The envelope's `resolvedBindings` values are `Term`s. The canonical term model is 034 Gleam `glp/runtime/terms.gleam`, mirrored in Dart/C#:

```
Term = ConstTerm(Constant) | StructTerm(functor: String, args: List(Term)) | VarRef(GlobalVarId)
Constant = ConstAtom(String) | ConstInt(Int) | ConstReal(Float) | ConstString(String)
```

- A deep-resolved binding never contains a *bound* `VarRef` (it is resolved away); a remaining `VarRef` is an **unbound** variable, encoded by `GlobalVarId` (R7).
- Lists are `StructTerm(".", [head, tail])`; `nil` is `ConstAtom("nil")` (034 convention).
- Reader/writer polarity is **not** carried in the term (034 FR-002: role lives in the heap cell, not the term); the envelope carries polarity only implicitly via `varToWriter` membership.

## 3. Section-15 Term Tag Table (shared, rides 029 ConstantCodec)

| Tag | Meaning | Payload |
|---|---|---|
| `0x00` | null | — |
| `0x01` | bool | 1 byte (`0x00`/`0x01`) |
| `0x02` | int64 | fixed 8-byte **little-endian** |
| `0x03` | double | IEEE-754 bit pattern, 8 bytes (`DoubleToInt64Bits`) — **gated** (ED-6 AtomVM `/float`, R11) |
| `0x04` | string | varint length + UTF-8 |
| `0x05` | ConstTerm (atom) | atom-name via tag `0x04` body |
| `0x06` | StructTerm | functor (string) + arity (varint) + `arity` recursive Terms |
| `0x07` | VarRef (unbound) | `GlobalVarId` (see §4) — **new** vs 029 (029 had no goal-level vars; FR-007 boundary) |

- **Counts/lengths**: unsigned LEB128 varint (`ByteIo.WriteVarUInt`; >64 bits ⇒ loud-fail).
- **Unknown tag on decode ⇒ loud-fail** (`IlCodecException`-equivalent) (FR-005).
- `0x07` is the one term tag **added** beyond 029's `ConstantCodec` (029 encodes ground constants + structs; the envelope additionally carries unbound goal variables). The `0x00–0x06` bytes are byte-identical to 029 for shared inputs (conformance test).

## 4. GlobalVarId

```
GlobalVarId = { agentId: String, localId: int64 }
```

Wire: `agentId` (tag `0x04` string body) + `localId` (fixed 8-byte LE). Scheme `agentId:localId` (FB-M1-14, DISPOSITIONS #15). Identity equality is `(agentId, localId)` — never the local heap address.

## 5. Envelope Wire Frame (summary — full layout in contracts/)

```
[version:0x01][payloadType:0x11]
[status:1]                              # 0x00 success | 0x01 suspended | 0x02 failed
[bindingsCount:varint] ( name:str , Term )*
[varToWriterCount:varint] ( name:str , GlobalVarId )*
[suspendedCount:varint] ( GlobalVarId )*
[capturedLen:varint][captured bytes]    # excluded from parity diff
[errorPresent:1] ( errorString:str )?
# decode MUST end exactly at end-of-input — trailing bytes ⇒ loud-fail
```

## 6. State / lifecycle

The envelope is **immutable** once emitted. Lifecycle: engine finishes a goal → server-side deep-resolve (depth-32, R5) → assemble `ResultEnvelope` → `encode` → bytes (in-process consumer reads the value directly; remote consumer decodes the bytes). `decode(encode(R)) == R` on the in-scope corpus (SC-001). No mutation, no heap reference retained.

## 7. Validation rules (→ tests)

- **V1** No live heap address in any encoded envelope (SC-003).
- **V2** `decode(encode(R)) == R` field-by-field for the in-scope corpus (SC-001), excluding `captured` from the equality of *bytes* but including it in value round-trip.
- **V3** Dart/C#/Gleam encode the same logical result to **byte-identical** output on the non-gated corpus (SC-002).
- **V4** Trailing/garbage bytes, unknown tag, bad version/payloadType ⇒ loud rejection; **0** silent acceptances (SC-004).
- **V5** Term tags `0x00–0x06` are byte-identical to 029 `ConstantCodec` for shared term inputs (oracle consistency, FR-007 boundary respected — used as a C# cross-check, not as Dart/Gleam proof).
- **V6** Gated (float `0x03`, 64-bit-int edges, cyclic terms) are **quarantined** in the corpus and **not** asserted byte-final until their gates clear (R11/R6).
