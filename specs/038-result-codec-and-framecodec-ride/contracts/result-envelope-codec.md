# Contract: Result-Envelope Codec (Section-15 term portion)

**Feature**: `038-result-codec-and-framecodec-ride` · **Status**: candidate (byte-parity-final gated on D4 ISA freeze + ED-6, per FR-009/FR-010)

This is the authoritative wire + API contract for the result-envelope codec. Dart (source of truth), C#, and Gleam MUST all conform byte-for-byte on the non-gated corpus (SC-002). It rides the Section-15 *term* codec (ED-6 obl#1) and reuses the 029 term-codec byte conventions; 029 is the **C# oracle only** (FR-007).

## 1. API surface (per runtime)

```
encode(envelope: ResultEnvelope) -> bytes
decode(bytes) -> ResultEnvelope            # MUST consume all bytes or fail loudly
```

- Dart: `glp_runtime/lib/codec/result_envelope_codec.dart` — `Uint8List encodeResultEnvelope(ResultEnvelope)`, `ResultEnvelope decodeResultEnvelope(Uint8List)`.
- C#: `csharp/glp_result_codec/ResultEnvelopeCodec.cs` — `static byte[] Encode(ResultEnvelope)`, `static ResultEnvelope Decode(byte[])`; throws `ResultCodecException` on any malformed input.
- Gleam: `glp_gleam/src/glp/codec/result_envelope.gleam` — `encode(ResultEnvelope) -> BitArray`, `decode(BitArray) -> Result(ResultEnvelope, CodecError)`.

## 2. Byte conventions (term portion — identical to 029)

| Primitive | Encoding |
|---|---|
| count / length | unsigned **LEB128 varint** (>64 bits ⇒ loud-fail) |
| int64 | fixed **8-byte little-endian** |
| double | IEEE-754 bit pattern, 8 bytes (`DoubleToInt64Bits`) — **GATED** (ED-6) |
| string | varint length + UTF-8 bytes |
| bool | 1 byte `0x00`/`0x01` |

Endianness for the term codec is **little-endian** (matches shipped 029; the big-endian `FrameCodec` TLV is the separate #36 seam — FR-006).

## 3. Term encoding (tags)

```
Term :=
  0x00                                              # null
| 0x01 <bool:1>                                      # bool
| 0x02 <int64:8 LE>                                  # int
| 0x03 <ieee754:8>                                   # real/float  [GATED — ED-6 AtomVM /float]
| 0x04 <len:varint> <utf8>                           # string
| 0x05 <atomName: 0x04-body>                         # ConstTerm(atom)
| 0x06 <functor:string> <arity:varint> Term{arity}  # StructTerm
| 0x07 <GlobalVarId>                                 # unbound VarRef
GlobalVarId := <agentId:string> <localId:int64 LE>
```

Tags `0x00–0x06` are **byte-identical to 029 `ConstantCodec`** for shared inputs (cross-check V5). `0x07` is the only added tag (goal-level unbound variable; outside 029's IL scope).

## 4. Envelope frame

```
ResultEnvelope :=
  0x01                       # version
  0x11                       # payloadType = RESULT_ENVELOPE  (029 IL program = 0x10)
  <status:1>                 # 0x00 success | 0x01 suspended | 0x02 failed
  <bindingsCount:varint>   ( <name:string> <Term> )*
  <varToWriterCount:varint>( <name:string> <GlobalVarId> )*
  <suspendedCount:varint>  ( <GlobalVarId> )*
  <capturedLen:varint> <captured:bytes>     # EXCLUDED from byte-parity diff (R4)
  <errorPresent:1> ( <errorString:string> )?   # errorString present iff errorPresent==0x01
```

**Canonical order**: `bindings`, `varToWriter`, `suspended` are emitted in the producing engine's deterministic declaration/binding order — identical across runtimes. Map iteration order MUST NOT leak (parity).

## 5. Loud-fail rules (FR-005, SC-004) — all MUST reject, never silently accept

1. `version != 0x01` or `payloadType != 0x11` ⇒ reject.
2. Unknown term tag (not `0x00–0x07`) ⇒ reject.
3. `status` not in `{0x00,0x01,0x02}` ⇒ reject.
4. `errorPresent` not in `{0x00,0x01}` ⇒ reject.
5. Truncated input (need more bytes than available) ⇒ reject.
6. **Trailing bytes** (input not fully consumed after a complete envelope) ⇒ reject (mirrors `IlCodec.cs:89-90`).
7. varint > 64 bits ⇒ reject.

## 6. Deep-resolve + truncation (R5)

- Server-side deep-resolve to **depth 32** (matches Dart `_ResolveDeepForTrace`) before encoding; a deep-resolved binding holds no bound `VarRef`.
- On hitting the depth bound, the binding carries an **explicit truncation marker** term (`StructTerm("$truncated", [])`) — never a silent cut. The marker is a normal, decodable `Term`; it is deterministic and parity-stable.

## 7. Byte-parity criterion (SC-002)

For each logical result `R` in the **non-gated** golden corpus: `encode_Dart(R) == encode_CSharp(R) == encode_Gleam(R) == golden(R)` as byte sequences, with `captured` masked out of the comparison. The golden is authored from the Dart encoder (R9). Gated entries (float `0x03`, 64-bit-int edges, cyclic terms) are quarantined and NOT asserted byte-final (R11/R6).

## 8. Round-trip criterion (SC-001)

For each `R` in the in-scope corpus: `decode(encode(R)) == R` field-by-field (incl. `captured` value, status, every binding/term, var→writer global ids, suspended set, error).

## 9. Out of scope (handoffs)

- **Framing / transport** → link-layer #36. Recorded handoff: `FrameCodec.cs:64 OffKind` is the **fragmentation** discriminant (`FrameKind {Whole=0, Fragment=1}`), **not** a payload-type discriminator — #36 needs its own payload-type prefix byte (this codec already reserves `0x11` for `RESULT_ENVELOPE`).
- **Opcode / IOpV2 bytecode portion** of Section-15 → governed by the v2-ISA freeze (D4) + F5 runner; NOT encoded by this codec.
- **Cyclic-term final semantics** → owner decision D5/FORK-1 (FR-008).
- **029 IL codec** is the C# byte-layout **oracle only** (FR-007) — not proof for Dart/Gleam.
