---
path: lib/multiagent/payload_serializer.dart
cycle_group_id: 28
scc_siblings: []
generated_at: 2026-05-21T15:19:52Z
source_sha256: 6291cb396efe81564618f2dd1e207ebda0a7fd3e01e918356a0e2f62282655e0
schema_version: 1
---

# Conversion Plan: lib/multiagent/payload_serializer.dart

## 1. Source Analysis

File `glp_runtime_net/lib/multiagent/payload_serializer.dart` (773 lines, library directive `library;`) is the cross-agent wire serializer for irmaGLP. It defines two top-level classes and a private set of binary-encoding helpers.

Imports (3):
- `dart:convert` — `utf8` codec.
- `dart:typed_data` — `Uint8List`, `BytesBuilder`, `ByteData`, `Endian`.
- `package:glp_runtime/runtime/terms.dart` — `Term`, `ConstTerm`, `VarRef`, `StructTerm`.
- `package:glp_runtime/multiagent/message_queue.dart` — `OutboundMessage`, `MessageType`.
- `package:glp_runtime/multiagent/mad_helpers.dart` — `GlobalName`.

Top-level declarations:

1. **`class GlobalVarId`** (lines 16–52) — value-pair entity with `final String creator`, `final int localId`; `encode()` returns `'creator:localId'`; static `decode(String)` parses the format and throws `FormatException` on bad arity or non-int `localId`; overrides `==` (by-value on both fields), `hashCode` via `Object.hash(creator, localId)`, and `toString()` returns `encode()`.

2. **`class PayloadSerializer`** (lines 55–773) with `final String agentId` and four `static const int` tag constants (`_tagConstant=1`, `_tagVariable=2`, `_tagStruct=3`, `_tagList=4`).

Public methods on `PayloadSerializer`:
- `serializeMessage(OutboundMessage) → Uint8List` — frames [type-byte][varlen dest length][dest UTF-8][varlen payload length][payload bytes].
- `deserializeMessage(Uint8List) → OutboundMessage` — mirror parse; `MessageType.values[typeIndex]` for ordinal-to-enum.
- `createAssignmentPayloadV2(int varId, Term value, bool Function(int) isReader, {lookupVariable}) → List<int>`.
- `deserializeAssignmentPayload(List<int>, int Function(bool), {onVariableImported}) → (GlobalVarId, Term)`.
- `createGlobalSendPayload(GlobalName, Term, bool Function(int), {lookupVariable}) → List<int>` — type byte (0=writer/1=reader), varlen agent, varlen index, term.
- `createSerializerPayload(GlobalName, Term, bool Function(int), {lookupVariable}) → List<int>` — `assert(isWriter && index==0)`; wraps content in list cell `[content | #serializer:agent:0]`.
- `_buildSerializerListCell(Term, GlobalName) → Term` — private; builds `StructTerm('.', [content, ConstTerm('#serializer:agent:0')])`.
- `deserializeGlobalSendPayload(List<int>, int Function(bool), {onVariableImported}) → (GlobalName, Term)`.
- `createReadRequestPayload(int varId, String requester) → List<int>`.
- `deserializeReadRequestPayload(List<int>) → int`.
- `createAbandonPayload(int writerId) → List<int>`.
- `deserializeAbandonPayload(List<int>) → int`.
- `serializeTermWithCallbacks(Term, String, bool Function(int), {lookupVariable}) → List<int>`.
- `_serializeTermRecursiveV2(Term, String, BytesBuilder, bool Function(int), lookupVariable?)` — `if/else if` chain on `term is ConstTerm | VarRef | StructTerm` with `throw UnsupportedError` default; writer branch encodes `pairedReaderLocalId = creatorLocalId + 1`.
- `_serializeConstant(dynamic, BytesBuilder)` — `value == 'nil'` (byte 0), `is int` (byte 1, big-endian int64), `is double` (byte 2, big-endian float64), `is String` (byte 3, varlen UTF-8), `is bool` (byte 4, 1/0), else `throw UnsupportedError`.
- `createAgentMessagePayload(Term, bool Function(int), {lookupVariable}) → List<int>`.
- `serializeAgentMessage(Term) → List<int>` — ground-only; passes lambda that throws `StateError` if `VarRef` encountered.
- `static deserializeAgentMessagePayloadWithMapping(List<int>, int Function(bool), {onVariableImported}) → (Term, Map<int, GlobalVarId>)` — throwaway-instance pattern: constructs `PayloadSerializer('')` to call private instance `_deserializeTermWithMappingV2`.
- `deserializeAgentMessagePayload(List<int>, int Function(bool), {onVariableImported}) → Term`.
- `_deserializeTermWithMappingV2(List<int>, int offset, Map<String,int> varMapping, int Function(bool), onVariableImported?) → (Term, int)` — switch on tag byte `_tagConstant | _tagVariable | _tagStruct`, throws `FormatException('Unknown term tag: $tag')` on default and `FormatException('Unexpected end of input')` on `offset >= bytes.length`.
- `_deserializeConstant(List<int>, int offset) → (dynamic, int)` — switch on type tag `0..4`, throws `FormatException('Unknown constant type tag: $typeTag')` and `FormatException('Unexpected end of input in constant')`.
- `_encodeLength(int) → List<int>` — three-band varlen: 1 byte if <128 (tag bits `00`), 2 bytes if <16384 (tag bits `10` via `0x80`), 4 bytes otherwise (tag bits `11` via `0xC0`); 30-bit ceiling.
- `_decodeLength(List<int>, int offset) → (int, int)` — mirror, returns `(length, bytesConsumed)`.
- `_encodeInt64(int) → List<int>` — `ByteData(8); setInt64(0, value, Endian.big)`.
- `_decodeInt64(List<int>, int offset) → int` — mirror via `ByteData.sublistView` and `Endian.big`.
- `_encodeFloat64(double) → List<int>` — analogous via `setFloat64`.
- `_decodeFloat64(List<int>, int offset) → double` — analogous via `getFloat64`.

Key Dart 3 features used: record-tuple returns `(T1, T2)`, record-pattern destructuring `final (a, b) = …`, anonymous-record callback return type `({String creator, int creatorLocalId, bool isReader})`, named optional parameters with `{...}` syntax.

Cross-agent wire-format invariants (load-bearing):
- All multi-byte numerics are **big-endian** (`Endian.big`) — explicit Dart choice.
- All string boundaries are **UTF-8** — explicit.
- Custom 1/2/4-byte tagged-prefix varlen length encoding — hand-rolled, NOT LEB128.
- `MessageType.index` written as a single byte — relies on declaration-order ordinal stability.
- Single-threaded synchronous code — no `async`, no `Future`, no `Stream`, no isolates.

## 2. Dart → C#/.NET Conversion Plan

Each construct below mirrors the ratified convspec verbatim. Construct-key column matches convspec `constructs[*].construct_key` 1:1.

### 2.1 `class GlobalVarId` → `public sealed class GlobalVarId : IEquatable<GlobalVarId>`
(construct: `dart.entity_class.value_pair_equality_by_two_fields_hashobjecthash`)

- Reference type `sealed class` (NOT `record`, NOT `record struct`) — preserves file's class style and avoids `EqualityContract`/`with`-expression baggage.
- Read-only auto-properties `public string Creator { get; }`, `public int LocalId { get; }`; both set via constructor `public GlobalVarId(string creator, int localId)`.
- `public string Encode() => $"{Creator}:{LocalId}";`.
- `public static GlobalVarId Decode(string encoded)` — `string.Split(':')`; on arity ≠ 2 throw `System.FormatException("Invalid global variable ID format: " + encoded)`; `int.TryParse(parts[1], out var localId)` and on failure throw `System.FormatException("Invalid local ID in global variable ID: " + encoded)`.
- Override `Equals(object?)`, implement `bool Equals(GlobalVarId?)` via `IEquatable<GlobalVarId>`, override `GetHashCode()` → `System.HashCode.Combine(Creator, LocalId)`.
- Overload `==`/`!=` (Microsoft Learn: when you override `Equals` you MUST override `GetHashCode`; when you overload `==` you MUST overload `!=`).
- `public override string ToString() => Encode();`.

### 2.2 `class PayloadSerializer` → `public sealed class PayloadSerializer`
(construct: `dart.class.utf8_length_prefixed_binary_wire_serializer_with_varlen_length_encoding`)

- `public string AgentId { get; }` read-only, set via constructor `public PayloadSerializer(string agentId)`.
- `private const byte TagConstant = 1; private const byte TagVariable = 2; private const byte TagStruct = 3; private const byte TagList = 4;` (Dart `int` tag constants → C# `byte` because they are written via `BytesBuilder.addByte`, i.e. single octets; bit-width must match wire format).
- Buffer construction: `using var ms = new System.IO.MemoryStream(); … return ms.ToArray();` (Dart `BytesBuilder` → C# `MemoryStream`; `BinaryWriter` is REJECTED — its default endianness is little-endian).
- `BytesBuilder.addByte(b)` → `ms.WriteByte(b)`; `BytesBuilder.add(List<int>)` → `ms.Write(bytes, 0, bytes.Length)` or `ms.Write(span)`.
- `Uint8List` → `byte[]` at every public boundary; `List<int>` (used by Dart for length-encoder return and `payload` parameters) → `byte[]` (or `ReadOnlySpan<byte>` for zero-copy internal slicing where lifetime allows).
- `utf8.encode(s)` → `System.Text.Encoding.UTF8.GetBytes(s)`; `utf8.decode(bytes)` → `System.Text.Encoding.UTF8.GetString(bytes)`. Use the `Encoding.UTF8` singleton — NEVER `new UTF8Encoding()`.
- `bytes.sublist(offset, offset + n)` → `byteArr.AsSpan(offset, n).ToArray()` when a fresh `byte[]` is required for a public boundary; `AsSpan(offset, n)` (zero-copy `ReadOnlySpan<byte>`) when the slice is consumed locally.
- The mutable `int offset` cursor pattern is preserved as a plain `int` local through each parser method — NOT refactored to `ref int offset` because Dart's `(value, bytesConsumed)` tuple already threads the cursor back.

### 2.3 Variable-length length encoding (`_encodeLength` / `_decodeLength`)
(construct: `dart.method.custom_variable_length_int_encoding_three_band_1_2_4_bytes`)

- `private static byte[] EncodeLength(int length)` — three branches preserved verbatim:
  - `length < 128`: `return new byte[] { (byte)length };`
  - `length < 16384`: `return new byte[] { (byte)(0x80 | (length >> 8)), (byte)(length & 0xFF) };`
  - else: `return new byte[] { (byte)(0xC0 | (length >> 24)), (byte)((length >> 16) & 0xFF), (byte)((length >> 8) & 0xFF), (byte)(length & 0xFF) };`
- `private static (int Value, int BytesConsumed) DecodeLength(ReadOnlySpan<byte> bytes, int offset)` — mirror: read `first = bytes[offset]`; if `first < 0x80` return `(first, 1)`; else if `first < 0xC0` return `(((first & 0x3F) << 8) | bytes[offset + 1], 2)`; else return `(((first & 0x3F) << 24) | (bytes[offset + 1] << 16) | (bytes[offset + 2] << 8) | bytes[offset + 3], 4)`.
- NOT replaced by `BinaryWriter.Write7BitEncodedInt` (LEB128 — incompatible wire format) or `BinaryPrimitives.WriteInt32BigEndian` (fixed 4 bytes — changes wire format).
- 30-bit ceiling preserved (silent wrap on >2^30 values — matches Dart).

### 2.4 `serializeMessage` / `deserializeMessage`
(construct: `dart.method.bytesbuilder_length_prefixed_string_payload_frame`)

- `public byte[] SerializeMessage(OutboundMessage message)`:
  - `ms.WriteByte((byte)message.Type);` — Dart `.index` → C# enum-to-byte cast (Microsoft Learn: explicit conversion from enum to integral type).
  - `var destBytes = Encoding.UTF8.GetBytes(message.Destination); ms.Write(EncodeLength(destBytes.Length), 0, …); ms.Write(destBytes, 0, destBytes.Length);`.
  - `ms.Write(EncodeLength(message.Payload.Length), 0, …); ms.Write(message.Payload, 0, message.Payload.Length);`.
- `public OutboundMessage DeserializeMessage(byte[] bytes)`:
  - `int offset = 0; byte typeIndex = bytes[offset++]; if (!Enum.IsDefined(typeof(MessageType), typeIndex)) throw new FormatException("Unknown message type: " + typeIndex); var type = (MessageType)typeIndex;` — preserves Dart `MessageType.values[N]` `RangeError` semantics via `Enum.IsDefined` guard.
  - Decode dest length/bytes via `DecodeLength` + `Encoding.UTF8.GetString`.
  - Decode payload length/bytes; return `new OutboundMessage(destination, type, payload)`.
- The codegen MUST declare `MessageType { Assignment, AgentMessage }` in Dart declaration order with NO explicit values (per `message_queue.dart` spec carry-forward).

### 2.5 Tuple-return partial-parse `(value, bytesConsumed)` pattern
(construct: `dart.method.tuple_return_partial_parse_offset_consumed_pattern`)

- All record-tuple returns map to named C# `ValueTuple`s:
  - `(int, int) _decodeLength` → `(int Value, int BytesConsumed)`.
  - `(Term, int) _deserializeTermWithMappingV2` → `(Term Term, int BytesConsumed)`.
  - `(dynamic, int) _deserializeConstant` → `(object? Value, int BytesConsumed)`.
  - `(GlobalVarId, Term) deserializeAssignmentPayload` → `(GlobalVarId GlobalId, Term Term)`.
  - `(GlobalName, Term) deserializeGlobalSendPayload` → `(GlobalName GlobalName, Term Term)`.
  - `(Term, Map<int, GlobalVarId>) deserializeAgentMessagePayloadWithMapping` → `(Term Term, Dictionary<int, GlobalVarId> Mapping)`.
- Dart `dynamic` → C# `object?` (NOT C# `dynamic` — different semantics; C# `dynamic` opts into DLR).
- Caller deconstruction `final (a, b) = method(...);` → `var (a, b) = method(...);`; `final (a, _) = …` → `var (a, _) = …` (discard preserved).

### 2.6 Term sealed-sum-type dispatch via `if (term is X)` chain
(construct: `dart.method.runtime_typed_polymorphic_dispatch_on_term_sumtype_via_is_chain`)

- `private void SerializeTermRecursiveV2(Term term, string agentId, MemoryStream ms, Func<int, bool> isReaderFn, Func<int, VariableLookup>? lookupVariable)`:
  - `switch (term) {`
  - `  case ConstTerm c: ms.WriteByte(TagConstant); SerializeConstant(c.Value, ms); break;`
  - `  case VarRef v: ms.WriteByte(TagVariable); /* see 2.7 */ break;`
  - `  case StructTerm s: ms.WriteByte(TagStruct); /* see 2.8 */ break;`
  - `  default: throw new NotSupportedException($"Cannot serialize term type: {term.GetType()}");`
  - `}`
- Case ORDER preserved (`ConstTerm` → `VarRef` → `StructTerm`) — ConstTerm-most-common fast path.
- `MutualRefTerm` and `ModuleTerm` deliberately fall through to throwing default — wire format does not transport them; codegen MUST NOT silently handle them.
- Dart `UnsupportedError` → C# `System.NotSupportedException` (1:1 semantic counterpart).
- `term.runtimeType` → `term.GetType()`.

### 2.7 `VarRef` serialisation branch
(part of `_serializeTermRecursiveV2`)

- Inside the `case VarRef v:` arm:
  - `var addr = v.Addr; var isReaderVar = isReaderFn(addr);`
  - `string creator; int creatorLocalId;`
  - `if (lookupVariable != null) { var info = lookupVariable(addr); creator = info.Creator; creatorLocalId = info.CreatorLocalId; } else { creator = agentId; creatorLocalId = addr; }`
  - `var globalId = new GlobalVarId(creator, creatorLocalId); var encoded = Encoding.UTF8.GetBytes(globalId.Encode()); ms.Write(EncodeLength(encoded.Length), …); ms.Write(encoded, …); ms.WriteByte(isReaderVar ? (byte)1 : (byte)0);`
  - For writers (`!isReaderVar`): `var pairedReaderLocalId = creatorLocalId + 1; ms.Write(EncodeLength(pairedReaderLocalId), …);`.
- The "writer paired-reader = creatorLocalId + 1" assumption is preserved verbatim (per spec Section 5.3 invariant noted in source comment).

### 2.8 `StructTerm` serialisation branch
(part of `_serializeTermRecursiveV2`)

- Inside the `case StructTerm s:` arm:
  - `var functorBytes = Encoding.UTF8.GetBytes(s.Functor); ms.Write(EncodeLength(functorBytes.Length), …); ms.Write(functorBytes, …); ms.Write(EncodeLength(s.Args.Count), …);`
  - `foreach (var arg in s.Args) { SerializeTermRecursiveV2(arg, agentId, ms, isReaderFn, lookupVariable); }`.

### 2.9 `_serializeConstant` runtime-type dispatch
(construct: `dart.method.dispatch_on_constant_dynamic_value_runtime_type_to_csharp_typepattern`)

- `private static void SerializeConstant(object? value, MemoryStream ms)`:
  - `switch (value) {`
  - `  case "nil": ms.WriteByte(0); break;` — constant-pattern FIRST (must precede `case string s` so non-`"nil"` strings fall through correctly).
  - `  case int i: ms.WriteByte(1); WriteInt64BigEndian(ms, i); break;`
  - `  case double d: ms.WriteByte(2); WriteFloat64BigEndian(ms, d); break;`
  - `  case string s: ms.WriteByte(3); var sb = Encoding.UTF8.GetBytes(s); ms.Write(EncodeLength(sb.Length), …); ms.Write(sb, …); break;`
  - `  case bool b: ms.WriteByte(4); ms.WriteByte(b ? (byte)1 : (byte)0); break;`
  - `  default: throw new NotSupportedException($"Cannot serialize constant type: {value?.GetType()}");`
  - `}`
- C# `int`/`double` are distinct runtime types matching Dart's `int`/`double` disjointness exactly.
- `"nil"` preserved as a literal string match (NOT promoted to a sentinel enum) to keep wire round-trip exact with Dart agents.

### 2.10 Big-endian int64 / float64 helpers
(part of `dart.class.utf8_length_prefixed_binary_wire_serializer_with_varlen_length_encoding`)

- `private static byte[] EncodeInt64(long value) { var buf = new byte[8]; System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(buf, value); return buf; }`.
- `private static long DecodeInt64(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt64BigEndian(bytes.Slice(offset, 8));`.
- `private static byte[] EncodeFloat64(double value) { var buf = new byte[8]; BinaryPrimitives.WriteDoubleBigEndian(buf, value); return buf; }`.
- `private static double DecodeFloat64(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadDoubleBigEndian(bytes.Slice(offset, 8));`.
- Target framework .NET 8+ per codeconv-016 langpair scaffold; `BinaryPrimitives.{Write,Read}DoubleBigEndian` is .NET 5+ — in-scope.
- Dart `int` is 64-bit → C# `long` for the integer counterpart (NOT `int`, which is 32-bit in .NET). The Dart→C# size widening is preserved by typing the wire helpers on `long`; downstream callers in `_serializeConstant` widen with `(long)i` where the source `i` is C# `int`.

### 2.11 Optional callback parameters with anonymous-record return
(construct: `dart.method.optional_callback_param_record_tuple_argtype`)

- Promoted helper type at top of file (alongside `PayloadSerializer`):
  - `public readonly record struct VariableLookup(string Creator, int CreatorLocalId, bool IsReader);`
  - `readonly record struct` chosen because: (a) Dart records are value types with structural equality; (b) callbacks construct + immediately destructure (no collection storage); (c) avoids boxing.
- Callback signatures:
  - `bool Function(int addr) isReader` (required) → `Func<int, bool> isReader`.
  - `({String creator, int creatorLocalId, bool isReader}) Function(int addr)? lookupVariable` (optional) → `Func<int, VariableLookup>? lookupVariable = null`.
  - `int Function(bool isReader) allocateImportedVar` (required) → `Func<bool, int> allocateImportedVar`.
  - `void Function(int localAddr, bool isReader, GlobalVarId globalId, int? pairedReaderCreatorLocalId)? onVariableImported` → `Action<int, bool, GlobalVarId, int?>? onVariableImported = null`.
- Dart nullable `int?` parameter type → C# `int?` = `System.Nullable<int>` (nullable value type).
- `lookupVariable?.call(addr)` → `lookupVariable?.Invoke(addr)`; `onVariableImported?.call(…)` → `onVariableImported?.Invoke(…)`.

### 2.12 `assert(...)` precondition
(construct: `dart.method.assert_runtime_check_to_csharp_argumentexception_or_debug_assert`)

- In `CreateSerializerPayload`:
  - `System.Diagnostics.Debug.Assert(serializerName.IsWriter && serializerName.Index == 0, "Serializer payload requires _w(agent, 0) global name");`
- ONLY `Debug.Assert` — NEVER `Trace.Assert` (always-on, would change production semantics) and NEVER unconditional `throw new ArgumentException` (strictly stronger than Dart's debug-only contract).

### 2.13 Static factory with throwaway-instance idiom
(construct: `dart.staticmethod.factory_deserialiser_with_temp_instance_workaround`)

- `public static (Term Term, Dictionary<int, GlobalVarId> Mapping) DeserializeAgentMessagePayloadWithMapping(byte[] payload, Func<bool, int> allocateImportedVar, Action<int, bool, GlobalVarId, int?>? onVariableImported = null)`:
  - `var globalToLocal = new Dictionary<string, int>();`
  - `// Note: agentId is unused during deserialisation; empty string is a sentinel matching the Dart source.`
  - `var tempSerializer = new PayloadSerializer("");`
  - `var (term, _) = tempSerializer.DeserializeTermWithMappingV2(payload, 0, globalToLocal, allocateImportedVar, onVariableImported);`
  - `var localToGlobal = new Dictionary<int, GlobalVarId>();`
  - `foreach (var kvp in globalToLocal) { localToGlobal[kvp.Value] = GlobalVarId.Decode(kvp.Key); }`
  - `return (term, localToGlobal);`
- Throwaway-instance pattern PRESERVED verbatim with explanatory comment — NOT refactored to a static private helper (preserves Dart-side instance-method signature for future cross-language sync).

### 2.14 `Dictionary<string,int>` lookup with `TryGetValue` promotion
(construct: `dart.method.utf8_decode_bytes_with_string_var_id_mapping`)

- `Map<String, int> varMapping = <String, int>{}` → `var varMapping = new Dictionary<string, int>();`.
- `if (varMapping.containsKey(k)) { localVarId = varMapping[k]!; } else { … }` → `if (varMapping.TryGetValue(globalIdStr, out var existing)) { localVarId = existing; } else { localVarId = allocateImportedVar(isReader); varMapping[globalIdStr] = localVarId; onVariableImported?.Invoke(localVarId, isReader, globalId, pairedReaderCreatorLocalId); }`.
- `TryGetValue` is the single-lookup .NET idiom (Microsoft Learn-documented) and matches the carry-forward decision in `message_queue.dart` spec (`Map-String-Queue-FIFO-per-destination`).

### 2.15 Dart-3 record-pattern destructuring at call sites
(construct: `dart.tuple_pattern.dart3_record_pattern_destructuring_call_site`)

- `final (a, b) = method(...);` → `var (a, b) = method(...);` (C# 7+ tuple deconstruction).
- `final (a, _) = method(...);` → `var (a, _) = method(...);` (C# discard pattern).
- Every call site that decodes a length-prefixed sub-frame uses the `var (length, lengthSize) = DecodeLength(payload, offset); offset += lengthSize;` cursor-advance pattern, preserved verbatim.

### 2.16 `FormatException` wire-protocol throws
(construct: `dart.method.format_exception_wire_protocol_throw_with_message_interpolation`)

- Four `FormatException` throw sites preserved:
  - `GlobalVarId.Decode` parse-failure: `throw new System.FormatException($"Invalid global variable ID format: {encoded}");` and `throw new System.FormatException($"Invalid local ID in global variable ID: {encoded}");`.
  - `_deserializeTermWithMappingV2` premature-EOF: `throw new FormatException("Unexpected end of input");`.
  - `_deserializeTermWithMappingV2` unknown-tag default: `throw new FormatException($"Unknown term tag: {tag}");`.
  - `_deserializeConstant` premature-EOF and unknown-tag: `throw new FormatException("Unexpected end of input in constant");` and `throw new FormatException($"Unknown constant type tag: {typeTag}");`.
- NEVER promoted to `InvalidDataException` (narrower) or `ArgumentException` (wrong semantic axis).
- `serializeAgentMessage` ground-only check `throw StateError(...)` (in the `isReader` callback lambda) → `throw new System.InvalidOperationException("Ground term expected, but VarRef found")` (Microsoft Learn: `InvalidOperationException` is the exact counterpart of Dart `StateError`).

### 2.17 Payload public-API methods (signatures table)

Mirrors convspec `conversion_units` list 1:1 (PascalCase identifiers, C# types):

- `byte[] SerializeMessage(OutboundMessage message)`
- `OutboundMessage DeserializeMessage(byte[] bytes)`
- `byte[] CreateAssignmentPayloadV2(int varId, Term value, Func<int, bool> isReader, Func<int, VariableLookup>? lookupVariable = null)`
- `(GlobalVarId GlobalId, Term Term) DeserializeAssignmentPayload(byte[] payload, Func<bool, int> allocateImportedVar, Action<int, bool, GlobalVarId, int?>? onVariableImported = null)`
- `byte[] CreateGlobalSendPayload(GlobalName globalName, Term value, Func<int, bool> isReader, Func<int, VariableLookup>? lookupVariable = null)`
- `byte[] CreateSerializerPayload(GlobalName serializerName, Term content, Func<int, bool> isReader, Func<int, VariableLookup>? lookupVariable = null)`
- `private Term BuildSerializerListCell(Term content, GlobalName serializerName)`
- `(GlobalName GlobalName, Term Term) DeserializeGlobalSendPayload(byte[] payload, Func<bool, int> allocateImportedVar, Action<int, bool, GlobalVarId, int?>? onVariableImported = null)`
- `byte[] CreateReadRequestPayload(int varId, string requester)`
- `int DeserializeReadRequestPayload(byte[] payload)`
- `byte[] CreateAbandonPayload(int writerId)`
- `int DeserializeAbandonPayload(byte[] payload)`
- `byte[] SerializeTermWithCallbacks(Term term, string agentId, Func<int, bool> isReader, Func<int, VariableLookup>? lookupVariable = null)`
- `private void SerializeTermRecursiveV2(Term term, string agentId, MemoryStream ms, Func<int, bool> isReaderFn, Func<int, VariableLookup>? lookupVariable)`
- `private static void SerializeConstant(object? value, MemoryStream ms)`
- `byte[] CreateAgentMessagePayload(Term term, Func<int, bool> isReader, Func<int, VariableLookup>? lookupVariable = null)`
- `byte[] SerializeAgentMessage(Term term)` (ground-only convenience; throws `InvalidOperationException` if a `VarRef` is encountered)
- `public static (Term Term, Dictionary<int, GlobalVarId> Mapping) DeserializeAgentMessagePayloadWithMapping(byte[] payload, Func<bool, int> allocateImportedVar, Action<int, bool, GlobalVarId, int?>? onVariableImported = null)`
- `Term DeserializeAgentMessagePayload(byte[] payload, Func<bool, int> allocateImportedVar, Action<int, bool, GlobalVarId, int?>? onVariableImported = null)`
- `private (Term Term, int BytesConsumed) DeserializeTermWithMappingV2(byte[] bytes, int offset, Dictionary<string, int> varMapping, Func<bool, int> allocateImportedVar, Action<int, bool, GlobalVarId, int?>? onVariableImported)`
- `private static (object? Value, int BytesConsumed) DeserializeConstant(byte[] bytes, int offset)`
- `private static byte[] EncodeLength(int length)`
- `private static (int Value, int BytesConsumed) DecodeLength(ReadOnlySpan<byte> bytes, int offset)`
- `private static byte[] EncodeInt64(long value)`
- `private static long DecodeInt64(ReadOnlySpan<byte> bytes, int offset)`
- `private static byte[] EncodeFloat64(double value)`
- `private static double DecodeFloat64(ReadOnlySpan<byte> bytes, int offset)`

### 2.18 Cross-cutting nuances (carried forward from convspec § Notes)

- **Endianness**: ALL 64-bit numerics via `BinaryPrimitives.{Write,Read}{Int64,Double}BigEndian`. NEVER `BinaryWriter`/`BinaryReader` (their default is little-endian).
- **Byte-width discipline**: tag constants typed `byte`; bool encoding `(byte)(b ? 1 : 0)`; preserves single-octet wire footprint.
- **UTF-8 boundary**: every string crossing the wire uses `Encoding.UTF8` singleton — NEVER `Encoding.Default`, NEVER `BinaryWriter`'s built-in length-prefixed string (it uses LEB128).
- **Null-safety**: Dart-optional callbacks (`bool Function(int)?`, `({…}) Function(int)?`, `void Function(…)?`) → nullable C# delegates (`Func<int, bool>?`, `Func<int, VariableLookup>?`, `Action<int, bool, GlobalVarId, int?>?`). Required Dart callbacks → non-nullable C# delegates. `int?` parameter → `System.Nullable<int>`.
- **Reference-vs-value**: `PayloadSerializer` and `GlobalVarId` are reference types (`sealed class`). `VariableLookup` is `readonly record struct` (value type). Term subclasses (`ConstTerm`, `VarRef`, `StructTerm`) remain reference types per `lib/runtime/terms.dart` spec.
- **No async/Stream**: source is synchronous; C# port MUST NOT introduce `Task<T>`, `IAsyncEnumerable<T>`, or `System.Threading.Channels.Channel<T>`.
- **Single-threaded**: no `lock`, no `ConcurrentDictionary`, no `Interlocked`. Cursor `int offset` is a plain local (NOT `ref int`).
- **Identifier casing**: Dart camelCase methods → C# PascalCase (`serializeMessage` → `SerializeMessage`); Dart `_camelCase` private members → C# `PascalCase` (per .NET naming convention — private `const` fields are PascalCase, e.g. `_tagConstant` → `TagConstant`).
- **`MutualRefTerm` / `ModuleTerm`**: throwing default arm preserved — wire format does not transport them.

## 3. Decomposed Task Units

- T1: Add `using` imports — `System`, `System.Buffers.Binary`, `System.Collections.Generic`, `System.Diagnostics`, `System.IO`, `System.Text`, and the C# counterparts of `glp_runtime/runtime/terms.dart` (`GLPRuntime.Runtime.Terms`), `glp_runtime/multiagent/message_queue.dart` (`GLPRuntime.Multiagent.MessageQueue`), `glp_runtime/multiagent/mad_helpers.dart` (`GLPRuntime.Multiagent.MadHelpers`).
- T2: Emit `public sealed class GlobalVarId : IEquatable<GlobalVarId>` per §2.1 (properties, constructor, `Encode`, `Decode`, `Equals`, `GetHashCode`, `==`/`!=`, `ToString`).
- T3: Emit `public readonly record struct VariableLookup(string Creator, int CreatorLocalId, bool IsReader)` per §2.11.
- T4: Emit `public sealed class PayloadSerializer` skeleton per §2.2 (`AgentId` property, constructor, `private const byte` tag constants).
- T5: Emit private `EncodeLength` / `DecodeLength` per §2.3.
- T6: Emit private `EncodeInt64` / `DecodeInt64` / `EncodeFloat64` / `DecodeFloat64` per §2.10 using `BinaryPrimitives.{Write,Read}{Int64,Double}BigEndian`.
- T7: Emit `SerializeMessage` / `DeserializeMessage` per §2.4 with `Enum.IsDefined` guard on `MessageType` cast.
- T8: Emit `CreateAssignmentPayloadV2` / `DeserializeAssignmentPayload` per §2.17 signatures, framing per `_encodeLength`/`_decodeLength`.
- T9: Emit `CreateGlobalSendPayload` per §2.17 — byte 0/1 writer/reader marker, varlen agent, varlen index, term.
- T10: Emit `CreateSerializerPayload` per §2.12 (with `Debug.Assert` precondition) and private `BuildSerializerListCell` returning `new StructTerm(".", new[] { content, new ConstTerm($"#serializer:{serializerName.Agent}:0") })`.
- T11: Emit `DeserializeGlobalSendPayload` per §2.17 — mirror parse.
- T12: Emit `CreateReadRequestPayload` / `DeserializeReadRequestPayload` per §2.17.
- T13: Emit `CreateAbandonPayload` / `DeserializeAbandonPayload` per §2.17.
- T14: Emit `SerializeTermWithCallbacks` (public driver) and private `SerializeTermRecursiveV2` with sealed-sum-type type-pattern switch per §2.6, §2.7, §2.8; throwing `NotSupportedException` default.
- T15: Emit private `SerializeConstant` per §2.9 with `"nil"` constant-pattern FIRST, then `int` / `double` / `string` / `bool`, then throwing default.
- T16: Emit `CreateAgentMessagePayload` and `SerializeAgentMessage` per §2.17 — the latter passes lambda `_ => throw new InvalidOperationException("Ground term expected, but VarRef found")` as the `isReader` callback.
- T17: Emit static `DeserializeAgentMessagePayloadWithMapping` per §2.13 with throwaway-instance idiom preserved and explanatory code comment.
- T18: Emit `DeserializeAgentMessagePayload` per §2.17 — thin wrapper over `DeserializeTermWithMappingV2`.
- T19: Emit private `DeserializeTermWithMappingV2` per §2.17 — switch on tag byte; preserves `TryGetValue` lookup promotion per §2.14; throwing `FormatException` on EOF and unknown tag.
- T20: Emit private `DeserializeConstant` per §2.17 — switch on type tag; throwing `FormatException` on EOF and unknown tag.
- T21: Verify cross-cutting nuances per §2.18 (endianness, UTF-8 singleton, nullability, no async, single-thread, casing, throwing-default contract).

## 4. Research Findings

None required. Every construct decision in §2 is verbatim-derivable from the ratified convspec at `.codeconv/conversion-specs/lib/multiagent/payload_serializer.dart.md` (which itself cites 12 Microsoft Learn URLs across the 12 constructs: `HashCode.Combine`, `IEquatable<T>`, `MemoryStream`, `BinaryPrimitives.{Write,Read}{Int64,Double}BigEndian`, `Encoding.UTF8`, `BinaryWriter.Write7BitEncodedInt` (rejected), `Enum.IsDefined`, `ValueTuple`, type-pattern switch, `NotSupportedException`, `record struct`, `Debug.Assert`, `Dictionary.TryGetValue`, tuple deconstruction, `FormatException`, `InvalidOperationException`). The convspec also carries forward closure decisions from `lib/runtime/terms.dart` spec (sealed-sum-type closure technique, throwing-default-arm convention, `IEquatable<T>` idiom) and from `lib/multiagent/message_queue.dart` spec (enum declaration-order ordinal contract, `TryGetValue` lookup idiom).

## 5. Consistency Pass

- §2.1 (`GlobalVarId`) — fixed — derived from convspec construct `dart.entity_class.value_pair_equality_by_two_fields_hashobjecthash` and rf-dart-value-pair-eq-objecthash-to-csharp-iequatable-hashcode-combine.
- §2.2 (`PayloadSerializer`) — fixed — derived from convspec construct `dart.class.utf8_length_prefixed_binary_wire_serializer_with_varlen_length_encoding` and rf-dart-bytesbuilder-bytedata-to-csharp-memorystream-binaryprimitives.
- §2.3 (varlen length) — fixed — derived from convspec construct `dart.method.custom_variable_length_int_encoding_three_band_1_2_4_bytes` and rf-dart-custom-tagged-varlen-to-csharp-preserve-verbatim.
- §2.4 (`serializeMessage`) — fixed — derived from convspec construct `dart.method.bytesbuilder_length_prefixed_string_payload_frame` and rf-dart-enum-ordinal-wire-mapping-to-csharp-enum-int-cast.
- §2.5 (tuple return) — fixed — derived from convspec construct `dart.method.tuple_return_partial_parse_offset_consumed_pattern` and rf-dart-record-tuple-to-csharp-named-valuetuple.
- §2.6 / §2.7 / §2.8 (term dispatch) — fixed — derived from convspec construct `dart.method.runtime_typed_polymorphic_dispatch_on_term_sumtype_via_is_chain` and rf-dart-is-chain-on-sealed-sumtype-to-csharp-typepattern-switch; carry-forward from `lib/runtime/terms.dart` spec.
- §2.9 (`_serializeConstant`) — fixed — derived from convspec construct `dart.method.dispatch_on_constant_dynamic_value_runtime_type_to_csharp_typepattern` and rf-dart-dynamic-runtime-type-dispatch-to-csharp-typepattern-switch.
- §2.10 (int64/float64 helpers) — fixed — derived from convspec rf-dart-bytesbuilder-bytedata-to-csharp-memorystream-binaryprimitives (`BinaryPrimitives.{Write,Read}{Int64,Double}BigEndian`).
- §2.11 (callbacks + `VariableLookup`) — fixed — derived from convspec construct `dart.method.optional_callback_param_record_tuple_argtype` and rf-dart-anon-record-callback-to-csharp-record-struct-helper.
- §2.12 (`assert`) — fixed — derived from convspec construct `dart.method.assert_runtime_check_to_csharp_argumentexception_or_debug_assert` and rf-dart-assert-to-csharp-debug-assert.
- §2.13 (throwaway-instance static factory) — fixed — derived from convspec construct `dart.staticmethod.factory_deserialiser_with_temp_instance_workaround` and rf-dart-throwaway-instance-static-method-to-csharp-preserve-pattern.
- §2.14 (`TryGetValue`) — fixed — derived from convspec construct `dart.method.utf8_decode_bytes_with_string_var_id_mapping` and rf-dart-map-containskey-bang-access-to-csharp-trygetvalue; carry-forward from `lib/multiagent/message_queue.dart` spec.
- §2.15 (record-pattern destructuring) — fixed — derived from convspec construct `dart.tuple_pattern.dart3_record_pattern_destructuring_call_site` and rf-dart3-record-pattern-to-csharp-tuple-deconstruction.
- §2.16 (`FormatException` / `InvalidOperationException`) — fixed — derived from convspec construct `dart.method.format_exception_wire_protocol_throw_with_message_interpolation` and rf-dart-format-exception-to-csharp-format-exception (plus the `StateError` → `InvalidOperationException` mapping noted in convspec § Notes "Exception handling").
- §2.17 (signatures table) — fixed — derived from convspec `conversion_units` block (1:1 enumeration).
- §2.18 (cross-cutting nuances) — fixed — derived from convspec § Notes "well-known nuances explicitly addressed (FR-009 / US2 AS4)".

## 6. Escalations

None.
