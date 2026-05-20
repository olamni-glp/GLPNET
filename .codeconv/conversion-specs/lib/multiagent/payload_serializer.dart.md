# Conversion Spec — lib/multiagent/payload_serializer.dart

> Conversion-spec artifact (FR-011). Spec-only (FR-023): describes the
> Dart→C# conversion of the cross-agent payload wire serializer;
> contains NO compilable C#. A later codegen stage consumes the
> structured block.

```yaml
schema_version: 1
source_path: lib/multiagent/payload_serializer.dart
source_sha256: 6291cb396efe81564618f2dd1e207ebda0a7fd3e01e918356a0e2f62282655e0
target_code_unit: lib/multiagent/payload_serializer.cs
constructs:
  - construct_key: dart.entity_class.value_pair_equality_by_two_fields_hashobjecthash
    source_form: >-
      class GlobalVarId { final String creator; final int localId;
      GlobalVarId(this.creator, this.localId);
      String encode() => '$creator:$localId';
      static GlobalVarId decode(String encoded) { ... throws FormatException ... }
      @override bool operator ==(Object other) => other is GlobalVarId &&
        other.creator == creator && other.localId == localId;
      @override int get hashCode => Object.hash(creator, localId);
      @override String toString() => encode(); }
    target_decision: >-
      `public sealed class GlobalVarId : IEquatable<GlobalVarId>` with
      read-only auto-properties `public string Creator { get; }` and
      `public int LocalId { get; }`, both set via the constructor.
      Override `Equals(object?)`, `bool Equals(GlobalVarId?)`,
      `GetHashCode()`, and `==`/`!=` operators (Microsoft Learn: when
      you override `Equals` you MUST override `GetHashCode`; when you
      overload `==` you MUST overload `!=`). `Object.hash(creator,
      localId)` (Dart `dart:core`) → `HashCode.Combine(Creator,
      LocalId)` (Microsoft Learn `System.HashCode.Combine`: the
      canonical replacement for `Object.hash` in .NET — supersedes
      manual XOR/Multiply hand-rolls). Encode method:
      `public string Encode() => $"{Creator}:{LocalId}";`. Decode:
      static `public static GlobalVarId Decode(string encoded)` —
      `string.Split(':')` returns `string[]`; parse the second part
      with `int.TryParse` and throw `FormatException` (`System.
      FormatException`) on either parse failure or wrong arity,
      matching Dart's `FormatException` 1:1. `ToString()` returns
      `Encode()`. NOT a `record` — although by-value equality on the
      two fields is exactly the record default, the surrounding
      file's classes use plain `sealed class` everywhere (entity
      `PayloadSerializer` is also a plain class) AND a record would
      synthesise `EqualityContract` + `with`-expression baggage not
      in the Dart source.
    idiom_id: null
    research_finding_id: rf-dart-value-pair-eq-objecthash-to-csharp-iequatable-hashcode-combine
    nuance: >-
      Two nuances. (1) Value-vs-reference: `GlobalVarId` is a small
      two-field value-like type but stays a reference-type `class`
      (NEVER `record struct` or `readonly struct`) because instances
      flow through `Map<String,int>`-style mappings (later converted
      to `Dictionary<string,int>`) and are repeatedly hashed; a
      value type would force boxing on dictionary keys and break the
      shared-identity assumption that callers can compare two
      instances directly with `==`. (2) Hash-equality contract:
      `Object.hash(creator, localId)` in Dart and
      `HashCode.Combine(Creator, LocalId)` in .NET both fold N
      arguments into a single 32-bit hash (Microsoft Learn
      `HashCode.Combine<T1, T2>` is "intended to be used in a custom
      `GetHashCode()` implementation"). The two are not bit-identical
      but both satisfy the equality-implies-equal-hash contract; the
      wire format does NOT depend on hash value (only on
      `Encode()`/`Decode()` string form), so cross-process behaviour
      is unaffected. FormatException nuance: Dart and .NET both
      expose a `FormatException` in their core libraries (Dart
      `dart:core` / .NET `System`); the message strings are preserved
      verbatim.
  - construct_key: dart.class.utf8_length_prefixed_binary_wire_serializer_with_varlen_length_encoding
    source_form: >-
      class PayloadSerializer { final String agentId;
      PayloadSerializer(this.agentId);
      static const int _tagConstant = 1, _tagVariable = 2,
      _tagStruct = 3, _tagList = 4;
      // BytesBuilder building, addByte + add(List<int>),
      // _encodeLength variable-length 1/2/4 bytes,
      // utf8.encode/decode at every string boundary,
      // ByteData big-endian int64/float64 encode/decode,
      // sublist(offset, offset+n) decode slicing,
      // mutable int offset cursor through every parse method. }
    target_decision: >-
      `public sealed class PayloadSerializer` with a read-only
      `public string AgentId { get; }` set via constructor, and
      `private const byte TagConstant = 1; TagVariable = 2;
      TagStruct = 3; TagList = 4;` (Dart `int` tag constants → C#
      `byte` because they are written via `BytesBuilder.addByte` —
      single octets — and the bit-width must match the wire format
      exactly). All `BytesBuilder` usage maps to `using var ms =
      new System.IO.MemoryStream(); using var bw = new
      System.IO.BinaryWriter(ms);` is REJECTED because
      `BinaryWriter.Write(int)` defaults to little-endian and the
      Dart source uses explicit big-endian (`Endian.big`). Instead
      use `System.IO.MemoryStream` directly with hand-written
      `ms.WriteByte(b)` and `ms.Write(span)` calls, mirroring
      `BytesBuilder.addByte` and `BytesBuilder.add(List<int>)`
      exactly (Microsoft Learn `MemoryStream.WriteByte(byte)` and
      `Stream.Write(ReadOnlySpan<byte>)` — the latter available since
      .NET Core 2.1). Final `builder.toBytes()` becomes `ms.ToArray()`
      returning `byte[]`. `Uint8List` (Dart typed-data) → `byte[]`
      at every public boundary. `utf8.encode(s)` →
      `System.Text.Encoding.UTF8.GetBytes(s)` (Microsoft Learn
      `Encoding.UTF8` is a singleton; do NOT call `new UTF8Encoding()`
      — same instance every call). `utf8.decode(bytes)` →
      `System.Text.Encoding.UTF8.GetString(byte[] | ReadOnlySpan<byte>)`.
      `ByteData(8); data.setInt64(0, value, Endian.big);` →
      `System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(span,
      value)` (Microsoft Learn: documented decisive API for explicit
      big-endian 8-byte writes — replaces the Dart `ByteData` +
      `Endian.big` idiom). `data.getInt64(0, Endian.big)` →
      `BinaryPrimitives.ReadInt64BigEndian(span)`. `setFloat64`/
      `getFloat64` → `BinaryPrimitives.WriteDoubleBigEndian` /
      `ReadDoubleBigEndian` (available since .NET 5 per Microsoft
      Learn; for earlier targets, fall back to manual
      `BitConverter.DoubleToInt64Bits` + endian-swap — escalation
      avoided: the target framework is .NET 8+ per the codeconv-016
      langpair scaffold; .NET 5+ APIs are in-scope).
      `_encodeLength(int)` returns a `List<int>` of 1/2/4 bytes via a
      custom variable-length tag scheme (see separate idiom below).
      `bytes.sublist(offset, offset + n)` → `byteArr.AsSpan(offset,
      n).ToArray()` when a fresh `byte[]` is required, otherwise
      `AsSpan(offset, n)` (zero-copy `ReadOnlySpan<byte>` slice;
      Microsoft Learn `MemoryExtensions.AsSpan`). The pervasive
      mutable `int offset` cursor pattern is preserved as a plain
      `int` local through each parser method, matching Dart
      exactly — NOT refactored to a `ref int offset` parameter
      because Dart's tuple-return `(value, bytesConsumed)` already
      threads the consumed-bytes back to the caller.
    idiom_id: null
    research_finding_id: rf-dart-bytesbuilder-bytedata-to-csharp-memorystream-binaryprimitives
    nuance: >-
      Multi-layered binary-wire nuance. (1) Endianness: Dart
      `Endian.big` is the explicit serialiser choice and is
      load-bearing (the wire protocol is cross-process and
      cross-language — receiving agents may be on different
      architectures); the C# target MUST use
      `BinaryPrimitives.{Write,Read}{Int64,Double}BigEndian` and NOT
      `BinaryWriter`/`BinaryReader` (whose default is little-endian
      per Microsoft Learn `BinaryWriter.Write(Int64)` — "the lowest
      byte" written first). (2) Byte-width: Dart `int` is 64-bit
      signed, but the type tags (`_tagConstant=1`..) are written via
      `addByte` so they round-trip as 8-bit values; C# tags MUST be
      typed `byte` (not `int`), otherwise an implicit widening could
      silently change wire output (no functional change in this code,
      but the type discipline catches future drift). (3) `List<int>`
      vs `Uint8List`: Dart uses `List<int>` for length-encoding
      returns (a list of 1–4 bytes) but `Uint8List` for the
      `serializeMessage` output; in C# both collapse to `byte[]` (or
      `ReadOnlyMemory<byte>` for zero-copy slices) — there is no
      separate "list of bytes" vs "typed byte array" distinction in
      .NET. (4) UTF-8 boundary: every string crossing the wire is
      encoded/decoded through UTF-8 explicitly (the Dart source never
      relies on a default encoding); C# preserves that by using
      `Encoding.UTF8` everywhere — NOT `Encoding.Default` (platform-
      dependent) and NOT relying on `BinaryWriter`'s built-in
      length-prefixed string format (which uses LEB128, a DIFFERENT
      variable-length scheme — see next idiom). (5) Sublist
      semantics: Dart `bytes.sublist(a, b)` returns a fresh copy; for
      performance the C# port can use `ReadOnlySpan<byte>` slices
      where the value does not escape the local scope, but at every
      public boundary (e.g. the bytes handed to `Encoding.UTF8.
      GetString`) the slice is read-only and the spec preserves a
      copy-on-extract semantics by default; the codegen stage may
      narrow this to a Span where lifetime analysis proves safety.
  - construct_key: dart.method.custom_variable_length_int_encoding_three_band_1_2_4_bytes
    source_form: >-
      List<int> _encodeLength(int length) {
        if (length < 128) return [length];
        else if (length < 16384) return [0x80 | (length >> 8), length & 0xFF];
        else return [ 0xC0 | (length >> 24), (length >> 16) & 0xFF,
                      (length >> 8) & 0xFF, length & 0xFF ]; }
      (int, int) _decodeLength(List<int> bytes, int offset) {
        final first = bytes[offset];
        if (first < 0x80) return (first, 1);
        else if (first < 0xC0) return (((first & 0x3F) << 8) |
                                       bytes[offset + 1], 2);
        else return (((first & 0x3F) << 24) |
                     (bytes[offset + 1] << 16) | ... , 4); }
    target_decision: >-
      Preserve the custom 1/2/4-byte tagged-prefix variable-length
      integer encoding VERBATIM — it is a private wire-format detail
      shared between every `serialize*`/`deserialize*` pair in this
      file, and MUST NOT be replaced by a stock library scheme
      (LEB128, VarInt-128, BinaryWriter 7-bit-encoded length). C#
      shape: `private static byte[] EncodeLength(int length)` and
      `private static (int Value, int BytesConsumed) DecodeLength(
      ReadOnlySpan<byte> bytes, int offset)`. Bit operations
      translate 1:1: Dart `0x80 | (length >> 8)` →
      `(byte)(0x80 | (length >> 8))`; Dart `length & 0xFF` →
      `(byte)(length & 0xFF)`. Return shape: `byte[]` of length 1, 2,
      or 4. The decoder returns a C# tuple `(int, int)` (named
      `(int Value, int BytesConsumed)` for clarity). The three bands
      and their tag bits `0x00`/`0x80`/`0xC0` are protocol constants
      and reproduced as-is. NOT replaced by `BinaryWriter.Write7Bit
      EncodedInt` — that is LEB128 (7 data bits per byte, MSB =
      continuation), an INCOMPATIBLE wire format that would corrupt
      every payload. NOT replaced by `BinaryPrimitives.Write
      Int32BigEndian` (4 bytes always — wastes space for short
      payloads and again changes the wire format).
    idiom_id: null
    research_finding_id: rf-dart-custom-tagged-varlen-to-csharp-preserve-verbatim
    nuance: >-
      Wire-format-fidelity nuance. The encoding is a hand-rolled
      "tagged-prefix" varlen: 1 byte for length<128 (high bit 0),
      2 bytes for length<16384 (high bits 10), 4 bytes otherwise
      (high bits 11), with the tag bits multiplexed INTO the length
      bytes. This is INCOMPATIBLE with every stock .NET
      length-encoder (`BinaryWriter.Write(string)` uses LEB128
      `Write7BitEncodedInt`; `BinaryPrimitives` writes fixed-width
      ints; `BitConverter` likewise). Therefore the spec mandates
      manual reproduction. Width nuance: the largest representable
      length is `((0x3F) << 24) | 0xFFFFFF = 2^30-1 ≈ 1 GiB`, well
      within `int` range in both Dart (64-bit) and C# (`int` is 32-bit
      signed in .NET — exactly large enough). No silent widening to
      `long`; the wire format encodes 30 bits maximum and an input
      `length` requiring >4 bytes would corrupt the high tag bits,
      matching Dart's silent behaviour exactly (a separate ESCALATION
      candidate if a future caller needs >1 GiB payloads — but FR-013
      says preserve current semantics, do not silently "fix"). C#
      shift operators on `int` are well-defined for positive operands
      identically to Dart `int` (Microsoft Learn `<<` and `>>` on
      `int`); the unsigned tag bits `0xC0` are stored as `byte`
      literals (no sign-extension concern at the wire boundary).
  - construct_key: dart.method.bytesbuilder_length_prefixed_string_payload_frame
    source_form: >-
      Uint8List serializeMessage(OutboundMessage message) {
        final builder = BytesBuilder();
        builder.addByte(message.type.index);
        final destBytes = utf8.encode(message.destination);
        builder.add(_encodeLength(destBytes.length));
        builder.add(destBytes);
        builder.add(_encodeLength(message.payload.length));
        builder.add(message.payload);
        return builder.toBytes(); }
      OutboundMessage deserializeMessage(Uint8List bytes) { ... mirror ... }
    target_decision: >-
      `public byte[] SerializeMessage(OutboundMessage message)` and
      `public OutboundMessage DeserializeMessage(byte[] bytes)`.
      `MessageType.index` (Dart's implicit enum ordinal) →
      `(int)message.Type` (C# enum-to-int cast — Microsoft Learn:
      "an enum type has an underlying type, which can be any of the
      integral numeric types"); both produce the same ordinal
      mapping for a plain enum, so wire compatibility is preserved
      (see message_queue.dart.md construct
      `enum MessageType plain-value-enum`). `MessageType.values
      [typeIndex]` (Dart static getter on enum) →
      `(MessageType)bytes[offset]` (cast int to enum — Microsoft
      Learn: "explicit conversion exists from any integral numeric
      type to any enum type"). Single-byte ordinal: the cast first
      narrows from `byte` → `int` → `MessageType`. Frame structure
      is preserved verbatim: [1 byte tag][varlen dest length][dest
      UTF-8 bytes][varlen payload length][payload bytes]. Both
      `destination` and `payload` use the same length-prefixed
      framing, but `destination` is a string (encoded via UTF-8)
      whereas `payload` is already `byte[]` and is written
      directly — no double-encoding.
    idiom_id: null
    research_finding_id: rf-dart-enum-ordinal-wire-mapping-to-csharp-enum-int-cast
    nuance: >-
      Two intertwined nuances. (1) Enum ordinal as wire tag: Dart
      `MessageType.index` is the declaration-order ordinal (0, 1, …)
      and is documented as such in the Dart enum reference. C# enum
      defaults to underlying `int` with declaration-order ordinal
      values starting at 0 (Microsoft Learn enum reference: "By
      default, the associated constant values of enum members are of
      type `int`; they start with zero and increase by one
      following the definition text order."). Therefore
      `(int)MessageType.Assignment == 0` and `(int)MessageType.
      AgentMessage == 1`, matching Dart `MessageType.assignment.
      index == 0` exactly — wire compatibility is automatic IFF the
      C# enum declaration order matches the Dart enum declaration
      order. The spec REQUIRES the codegen to declare
      `MessageType { Assignment, AgentMessage }` in that exact order
      and to NOT assign explicit values (which would let a future
      maintainer accidentally renumber and break the wire format).
      (2) `MessageType.index` is a 64-bit Dart `int` truncated to a
      single byte via `addByte`; for 2 enum cases this is fine, but
      the spec records the ceiling: any future addition beyond 256
      cases would overflow the byte. Codegen MUST emit
      `(byte)message.Type` and rely on .NET overflow checking off
      (default `checked` is OFF for explicit casts per Microsoft
      Learn `checked`/`unchecked`) — matching Dart's silent
      truncation. Reverse cast `(MessageType)bytes[offset]` is a
      valid C# explicit cast for any int value, even one outside
      the declared enum members; Microsoft Learn warns that the cast
      "doesn't fail" — matching Dart's behaviour where
      `MessageType.values[typeIndex]` throws `RangeError` for an
      out-of-range index. The Dart source CAN throw; the C# port
      preserves the throwing contract by adding a defensive
      `Enum.IsDefined(typeof(MessageType), tag)` check (Microsoft
      Learn `Enum.IsDefined`) and throwing
      `FormatException("Unknown message type: " + tag)` when false,
      to preserve Dart's "RangeError on bad ordinal" failure mode.
  - construct_key: dart.method.tuple_return_partial_parse_offset_consumed_pattern
    source_form: >-
      (int, int) _decodeLength(List<int> bytes, int offset) { ... }
      (Term, int) _deserializeTermWithMappingV2(...) { ... }
      (dynamic, int) _deserializeConstant(...) { ... }
      Callers: `final (idLength, idLengthSize) = _decodeLength(payload, offset);
                offset += idLengthSize;`
    target_decision: >-
      Dart record-tuple returns (`(Term, int)`, `(int, int)`,
      `(dynamic, int)`) map to C# named tuples
      `(Term Value, int BytesConsumed)`, `(int Length, int Size)`,
      `(object? Value, int BytesConsumed)` (Microsoft Learn
      `ValueTuple<T1,T2>` — value-type tuples introduced in C# 7;
      named fields are syntactic sugar but improve readability at
      every call site). `dynamic` (Dart) → `object?` (C#) at the
      tuple field — NOT `dynamic` in C# because Dart `dynamic`
      bypasses type checking statically AND dynamically, whereas C#
      `dynamic` opts into the DLR (Microsoft Learn `dynamic`:
      "type-checking occurs at run time" — much heavier than what
      Dart `dynamic` means here, which is "value is one of int /
      double / String / bool / 'nil' marker"). Caller patterns map
      1:1: `final (a, b) = method(...)` → `var (a, b) = method(...)`
      (C# tuple deconstruction since C# 7). `(_, x)` Dart anonymous
      discard → C# `(_, var x) = ...`.
    idiom_id: null
    research_finding_id: rf-dart-record-tuple-to-csharp-named-valuetuple
    nuance: >-
      Two nuances. (1) Dart `dynamic` vs C# `dynamic`: superficial
      keyword match, opposite semantics. Dart `dynamic` is
      "skip static type-check"; C# `dynamic` is "DLR-dispatched, all
      member access goes through `CallSite<T>` at runtime"
      (Microsoft Learn `dynamic` reference). For a *value* whose
      runtime type is `int | double | string | bool | "nil"`, the
      correct C# target is `object?` with downstream type-pattern
      switching (`switch (v) { case int i: ...; case double d: ...
      }`), NOT `dynamic`. (2) Tuple-as-pseudo-out-parameter: Dart
      lacks `out` params; tuple return is the idiomatic
      "value + bytes-consumed" pattern. C# has both `out` and tuple
      return; the spec deliberately picks tuple return (not `out`)
      because the existing call-site pattern
      `(value, size) = decode(...); offset += size;` reads more
      naturally with deconstruction than with `out int size`. This
      is a STYLE decision recorded explicitly (FR-009) — both are
      semantically equivalent.
  - construct_key: dart.method.runtime_typed_polymorphic_dispatch_on_term_sumtype_via_is_chain
    source_form: >-
      void _serializeTermRecursiveV2(Term term, ...) {
        if (term is ConstTerm) { builder.addByte(_tagConstant);
                                  _serializeConstant(term.value, builder); }
        else if (term is VarRef) { builder.addByte(_tagVariable); ... }
        else if (term is StructTerm) { builder.addByte(_tagStruct); ... }
        else { throw UnsupportedError(
          'Cannot serialize term type: ${term.runtimeType}'); } }
    target_decision: >-
      Exhaustive type-pattern switch on the `Term` sealed hierarchy
      (closed sum-type closure technique established in
      `lib/runtime/terms.dart` spec — see
      `dart.abstract_base_class.empty_open_marker_for_closed_sum_type`
      construct): `switch (term) { case ConstTerm c: ms.WriteByte
      (TagConstant); SerializeConstant(c.Value, ms); break; case
      VarRef v: ms.WriteByte(TagVariable); ... break; case StructTerm
      s: ms.WriteByte(TagStruct); ... break; default: throw new
      NotSupportedException($"Cannot serialize term type:
      {term.GetType()}"); }` (Microsoft Learn pattern matching:
      "type pattern matches an expression whose runtime type is
      compatible with the given type"). Dart `UnsupportedError` →
      C# `System.NotSupportedException` (Microsoft Learn: thrown
      "when an invoked method is not supported, or when there is an
      attempt to read, seek, or write to a stream that does not
      support the invoked functionality"). `term.runtimeType` (Dart
      `Object.runtimeType` returning `Type`) → `term.GetType()`
      (Microsoft Learn `Object.GetType()` returning
      `System.Type` — exact counterpart). The exhaustive default
      arm is REQUIRED (per terms.dart spec): C# pattern-match
      switches do not currently enforce exhaustiveness on a sealed
      hierarchy (no exhaustiveness check at compile time —
      Microsoft Learn switch-expression non-exhaustiveness warning
      CS8509 is best-effort), so the throwing default preserves
      Dart's "no fall-through" semantics. `MutualRefTerm` and
      `ModuleTerm` (the two Term leaves NOT enumerated here) are
      intentionally NOT serializable across agents: the Dart source
      throws on them, the C# port MUST do the same — codegen does
      NOT silently fall through, does NOT silently serialise as
      "constant", and does NOT reorder the cases (the order must
      remain ConstTerm → VarRef → StructTerm to preserve the
      ConstTerm-most-common fast-path).
    idiom_id: null
    research_finding_id: rf-dart-is-chain-on-sealed-sumtype-to-csharp-typepattern-switch
    nuance: >-
      Sealed-sum-type-dispatch nuance — load-bearing across this
      file. Dart's `if (term is X) … else if (term is Y) …` chain
      on a closed `Term` hierarchy is the canonical sum-type
      dispatch (see `lib/runtime/terms.dart` spec for the closure
      argument); C# pattern-match switch is the documented
      counterpart (Microsoft Learn pattern matching). Two specific
      semantics MUST be preserved: (1) the throwing default arm
      (Dart `throw UnsupportedError` → C# `throw new
      NotSupportedException`) — NEVER silently "best-effort
      serialise" an unknown subtype, and (2) the case order
      (ConstTerm first as the hot path, then VarRef, then
      StructTerm) — pattern-match on a class hierarchy in C# is
      NOT order-independent when subtypes overlap (here they
      don't — they're disjoint sealed leaves — but the codegen
      preserves order on principle). `MutualRefTerm` and
      `ModuleTerm` are deliberately excluded: a mutual-reference
      stream tail and an opaque bytecode payload are not
      cross-agent transportable concepts; serialising them would
      require a wire protocol the source has not defined (an
      ESCALATION-worthy gap if a future stage tries — but as of
      this file the source's throwing behaviour is the contract).
  - construct_key: dart.method.dispatch_on_constant_dynamic_value_runtime_type_to_csharp_typepattern
    source_form: >-
      void _serializeConstant(dynamic value, BytesBuilder builder) {
        if (value == 'nil') { builder.addByte(0); }
        else if (value is int)    { builder.addByte(1); builder.add(_encodeInt64(value)); }
        else if (value is double) { builder.addByte(2); builder.add(_encodeFloat64(value)); }
        else if (value is String) { builder.addByte(3); ... }
        else if (value is bool)   { builder.addByte(4); ... }
        else { throw UnsupportedError(...); } }
    target_decision: >-
      `private static void SerializeConstant(object? value,
      MemoryStream ms)`. C# type-pattern switch over `object?` with
      ordered cases mirroring Dart, with one critical re-ordering
      tweak: the `value == "nil"` literal check must come BEFORE the
      `is String` case (in Dart, `value == 'nil'` is true only when
      `value` is the string `'nil'`, and the `else if` chain ensures
      strings other than `'nil'` fall through to the `is String`
      arm — preserve that). C# shape: `switch (value) { case
      "nil": ms.WriteByte(0); break; case int i: ms.WriteByte(1);
      WriteInt64BigEndian(ms, i); break; case double d:
      ms.WriteByte(2); WriteFloat64BigEndian(ms, d); break; case
      string s: ms.WriteByte(3); WriteUtf8LengthPrefixed(ms, s);
      break; case bool b: ms.WriteByte(4); ms.WriteByte(b ? (byte)1
      : (byte)0); break; default: throw new NotSupportedException
      ($"Cannot serialize constant type: {value?.GetType()}"); }`.
      The "nil" Dart-side singleton string is preserved as the
      C# string literal `"nil"`. NOT a separate dedicated `Nil`
      enum case — codegen would diverge from the source's
      string-based marker, breaking re-ingest by Dart agents.
      Bool encoding: Dart `value ? 1 : 0` → C# `(byte)(b ? 1 : 0)`
      with explicit byte cast (C# does not auto-widen `bool` to
      numeric).
    idiom_id: null
    research_finding_id: rf-dart-dynamic-runtime-type-dispatch-to-csharp-typepattern-switch
    nuance: >-
      Three nuances. (1) Dart `int` and `double` are distinct types
      at runtime (`x is int` excludes doubles, `x is double`
      excludes ints); C# `int` (`System.Int32`) and `double`
      (`System.Double`) are also distinct, matching exactly — but
      the Dart number tower has subtle integer-vs-double coercion
      rules (`1.0 is int` is false; `1 is double` is false). The
      C# `case int i` / `case double d` patterns preserve this
      disjointness exactly (Microsoft Learn type-pattern: "matches
      an expression whose runtime type is `T`"). (2) The "nil"
      marker is a STRING `'nil'`, not a sentinel object —
      preserved as a literal string match to keep wire format
      stable. (3) `dynamic` Dart parameter → `object?` C#
      parameter (NOT `dynamic`, as established in the tuple-return
      idiom above): the call sites pass `term.value` which is
      typed `Object?` in Dart, so `object?` is the exact target;
      callers cast through the type-pattern switch.
  - construct_key: dart.method.optional_callback_param_record_tuple_argtype
    source_form: >-
      List<int> serializeTermWithCallbacks(Term term, String agentId,
        bool Function(int addr) isReader,
        {({String creator, int creatorLocalId, bool isReader})
          Function(int addr)? lookupVariable})
    target_decision: >-
      Functional callback parameters with Dart record-type return.
      `bool Function(int)` → `System.Func<int, bool>` (Microsoft
      Learn `Func<T, TResult>` delegate). The
      `({String creator, int creatorLocalId, bool isReader})
      Function(int addr)?` callback returns a NAMED Dart record
      (anonymous record type with named fields) — C# has no
      anonymous-record type at delegate boundaries; the spec
      mandates a NAMED helper type: `public readonly record struct
      VariableLookup(string Creator, int CreatorLocalId, bool
      IsReader)` and the callback becomes `System.Func<int,
      VariableLookup>?` (nullable delegate). `readonly record
      struct` is justified here (NOT a plain `class` like
      `GlobalVarId`) because: (a) the value is constructed at the
      callback boundary, immediately destructured, and never stored
      in a collection — boxing cost is irrelevant; (b) Dart's
      anonymous record (`(T1, T2, T3)` or `({T1 a, T2 b, T3 c})`) is
      a value type with structural equality (Dart records are
      always value types per the Dart records reference); (c)
      `record struct` exactly preserves "value semantics +
      structural equality + readonly fields" (Microsoft Learn
      records: `record struct` is the value-type variant; `readonly
      record struct` makes fields immutable). The nested callback
      `void Function(int localAddr, bool isReader, GlobalVarId
      globalId, int? pairedReaderCreatorLocalId)? onVariableImported`
      → `System.Action<int, bool, GlobalVarId, int?>?` (`int?` is
      `Nullable<int>` — Dart's nullable `int?` maps to the
      `Nullable<T>` value type form per Microsoft Learn nullable
      value types; NOT to NRT `int?` which doesn't exist on value
      types). `int Function(bool isReader) allocateImportedVar` →
      `System.Func<bool, int> allocateImportedVar`. ALL callback
      parameters preserve nullability exactly — required Dart
      callbacks become non-nullable C# delegates, optional Dart
      callbacks become nullable C# delegates.
    idiom_id: null
    research_finding_id: rf-dart-anon-record-callback-to-csharp-record-struct-helper
    nuance: >-
      Dart anonymous-record-typed callback returns are the killer
      nuance. Dart 3+ records (`({T1 a, T2 b})`) are first-class
      structurally-typed value types with named fields — no
      declaration required. C# 9+ records support
      `record class` (reference) and C# 10+ `record struct` (value),
      both NOMINALLY typed (must be declared); there is no
      "structural anonymous record" type in C#. Therefore the spec
      promotes the anonymous Dart record to a NAMED `readonly
      record struct VariableLookup` declared next to the consuming
      class (Microsoft Learn `record struct`: "an immutable value
      type" with synthesised `Equals`/`GetHashCode`/`Deconstruct`).
      Why `readonly record struct` and not `record class`: Dart
      records are VALUE types (records are not reference identity —
      two `(creator: 'a', creatorLocalId: 1, isReader: false)`
      values are `==`); `record class` would synthesise structural
      equality too but introduces a reference allocation per
      callback return. The destructuring at the call site —
      `creator = info.creator; creatorLocalId = info.creatorLocalId;`
      — uses property access; the spec preserves that pattern in C#
      (`creator = info.Creator; creatorLocalId = info.CreatorLocalId;`)
      rather than tuple deconstruction, matching Dart 1:1. Nullable
      delegate vs nullable underlying: `Func<int, VariableLookup>?`
      (nullable delegate reference) is the C# counterpart of Dart
      `({…}) Function(int)?` (nullable function type) — both null-
      check at the call site (`lookupVariable?.call(addr)` Dart →
      `lookupVariable?.Invoke(addr)` C#, Microsoft Learn null-
      conditional operator on delegates).
  - construct_key: dart.method.assert_runtime_check_to_csharp_argumentexception_or_debug_assert
    source_form: >-
      assert(serializerName.isWriter && serializerName.index == 0,
             'Serializer payload requires _w(agent, 0) global name');
    target_decision: >-
      Dart `assert(condition, message)` is enabled in debug mode
      only (the Dart spec: "Assert statements are only evaluated
      during development"); release builds elide them entirely. C#
      has TWO candidate targets: `System.Diagnostics.Debug.Assert`
      (compiled out in Release per `DEBUG` symbol — closest
      semantic match per Microsoft Learn `Debug.Assert`) OR
      `if (!cond) throw new ArgumentException(message)` (always
      checked — stricter than Dart). The spec mandates
      `Debug.Assert(serializerName.IsWriter && serializerName.Index
      == 0, "Serializer payload requires _w(agent, 0) global
      name");` to preserve Dart's "debug-only check, no runtime
      cost in release" semantics. Codegen MUST NOT silently
      promote to an unconditional throw — that would change
      production behaviour for any caller depending on the elided
      check.
    idiom_id: null
    research_finding_id: rf-dart-assert-to-csharp-debug-assert
    nuance: >-
      Assertion-elision nuance. Dart `assert` is documented as
      "evaluated only in development" and silently elided in
      production builds (`--release`/`flutter build` defaults).
      C# `Debug.Assert` is the documented counterpart (Microsoft
      Learn: "By default, the `Debug.Assert` method works only in
      debug builds"). Critical alternative REJECTED:
      `Trace.Assert` is enabled in both Debug and Release —
      stronger than Dart and would change semantics; not used.
      Critical alternative REJECTED: `if (!cond) throw new
      ArgumentException(…)` is an unconditional throw —
      strictly stronger than Dart, and a caller relying on
      production elision (legitimate Dart pattern) would
      regress. The spec is explicit: `Debug.Assert` and ONLY
      `Debug.Assert` for raw `assert(...)` statements.
  - construct_key: dart.staticmethod.factory_deserialiser_with_temp_instance_workaround
    source_form: >-
      static (Term, Map<int, GlobalVarId>)
      deserializeAgentMessagePayloadWithMapping(
        List<int> payload,
        int Function(bool isReader) allocateImportedVar,
        {void Function(int localAddr, bool isReader, GlobalVarId
          globalId, int? pairedReaderCreatorLocalId)? onVariableImported}
      ) {
        ...
        final serializer = PayloadSerializer('');  // tmp instance,
                                                   // agentId unused
        final (term, _) = serializer._deserializeTermWithMappingV2(...);
        ...
      }
    target_decision: >-
      Static factory pattern with a throwaway instance to access a
      private instance method. C# shape: `public static (Term Term,
      Dictionary<int, GlobalVarId> Mapping)
      DeserializeAgentMessagePayloadWithMapping(byte[] payload,
      Func<bool, int> allocateImportedVar, Action<int, bool,
      GlobalVarId, int?>? onVariableImported = null)`. The
      `PayloadSerializer('')` workaround (passing empty agentId
      because the deserialiser doesn't need it) is PRESERVED 1:1
      as `var tempSerializer = new PayloadSerializer("");` — the
      conversion does NOT refactor `_deserializeTermWithMappingV2`
      into a static method, because doing so would create a public
      static surface where Dart has a private instance surface, and
      future Dart-side changes (e.g. adding agentId-dependent logic
      to the deserialiser) would silently diverge. The
      throwaway-instance idiom is documented as such with a code
      comment: `// Note: agentId is unused during deserialisation;
      empty string is a sentinel matching the Dart source.`
      Map inversion (`for (final entry in globalToLocal.entries) {
      localToGlobal[entry.value] = GlobalVarId.decode(entry.key); }`)
      → C# `foreach (var kvp in globalToLocal) { localToGlobal[
      kvp.Value] = GlobalVarId.Decode(kvp.Key); }` (Microsoft Learn
      `Dictionary<TKey,TValue>.GetEnumerator` returns
      `KeyValuePair<TKey,TValue>`).
    idiom_id: null
    research_finding_id: rf-dart-throwaway-instance-static-method-to-csharp-preserve-pattern
    nuance: >-
      Refactor-temptation nuance. The Dart source uses an awkward
      pattern: a static method that constructs a fresh instance
      solely to invoke a private instance method
      (`_deserializeTermWithMappingV2`). The CLEAN C# refactor
      would be to make `_deserializeTermWithMappingV2` static (it
      doesn't reference `agentId` or any other instance state in
      the deserialiser code-path). However: (1) FR-013
      escalate-don't-guess + FR-009 preserve-semantics: the Dart
      source has it as an instance method, possibly for future
      extensibility; (2) the static-factory caller passes
      `PayloadSerializer('')` — an empty-string agentId sentinel —
      which is itself a Dart-side workaround signal; refactoring
      the C# side hides that signal. The spec deliberately
      PRESERVES the throwaway-instance pattern with an explanatory
      comment; a separate codeconv-018 ESCALATION-style
      improvement is the right venue if this is ever cleaned up
      (idiom-vs-idiom conflict between "preserve source" and
      "remove dead workarounds" — not raised here because the
      source semantics are unambiguous and the cleaner refactor is
      additive, not subtractive).
  - construct_key: dart.method.utf8_decode_bytes_with_string_var_id_mapping
    source_form: >-
      final globalIdStr = utf8.decode(idBytes);
      final globalId = GlobalVarId.decode(globalIdStr);
      ...
      final varMapping = <String, int>{};
      if (varMapping.containsKey(globalIdStr)) {
        localVarId = varMapping[globalIdStr]!;
      } else {
        localVarId = allocateImportedVar(isReader);
        varMapping[globalIdStr] = localVarId;
        onVariableImported?.call(localVarId, isReader, globalId,
                                  pairedReaderCreatorLocalId); }
    target_decision: >-
      `Map<String, int>` (Dart) → `Dictionary<string, int>` (C#);
      `<String, int>{}` literal → `new Dictionary<string, int>()`.
      `containsKey(k)` → `ContainsKey(k)` (Microsoft Learn:
      `Dictionary<TKey,TValue>.ContainsKey`). The Dart
      `varMapping[k]!` (non-null assertion) → `varMapping[k]`
      directly in C# — `Dictionary<TKey, TValue>` index getter
      throws `KeyNotFoundException` on miss (Microsoft Learn:
      "`KeyNotFoundException` is thrown if the key isn't found");
      the surrounding `ContainsKey` guard makes the throw
      unreachable. Optimisation note: `TryGetValue` is the more
      idiomatic single-call form (`if (varMapping.TryGetValue(
      globalIdStr, out var existing)) { localVarId = existing; }
      else { ... }`) — the spec PREFERS `TryGetValue` here because
      it's a documented .NET idiom that combines the existence
      check and the read into one lookup, matching the Dart cost
      (one map lookup, not two). `onVariableImported?.call(...)`
      (Dart null-aware method call) → `onVariableImported?.Invoke
      (localVarId, isReader, globalId, pairedReaderCreatorLocalId)`
      (Microsoft Learn null-conditional operator with delegate
      invocation: "the form `a?.X(Y, Z)` evaluates `a.X(Y, Z)`
      only if `a` is non-null").
    idiom_id: null
    research_finding_id: rf-dart-map-containskey-bang-access-to-csharp-trygetvalue
    nuance: >-
      Two nuances. (1) Map-lookup idiom: Dart's
      `containsKey + [k]!` is two lookups; C#'s `TryGetValue` is
      one (and it's the documented idiom — Microsoft Learn
      Dictionary<TKey,TValue>.TryGetValue is the "preferred way of
      looking up values"). Preserving the Dart two-lookup form
      would be a faithful but inferior translation; the spec
      promotes to `TryGetValue` because (a) it's a recurring
      codeconv-016 idiom (see message_queue.dart construct
      "Map-String-Queue-FIFO-per-destination
      _queuesByDestination putIfAbsent" — exactly the same
      promotion), and (b) the semantics are byte-identical (one
      lookup vs two, both throw on a third-party concurrent
      mutator that mid-call is N/A here — single-threaded). (2)
      Null-aware method invocation: `?.call` Dart → `?.Invoke` C#
      is a 1:1 mapping; both languages short-circuit if the
      delegate reference is null (Microsoft Learn null-
      conditional + delegate invoke).
  - construct_key: dart.tuple_pattern.dart3_record_pattern_destructuring_call_site
    source_form: >-
      final (idLength, idLengthSize) = _decodeLength(payload, offset);
      offset += idLengthSize;
      final (term, _) = _deserializeTermWithMappingV2(...);
    target_decision: >-
      Dart 3+ record-pattern destructuring at call sites
      (`final (a, b) = method(...)`) → C# tuple deconstruction
      (`var (a, b) = method(...)` — Microsoft Learn: "starting in
      C# 7, tuples are deconstructable"). Dart anonymous discard
      `_` → C# discard `_` in deconstruction (Microsoft Learn
      discards: "you can use the discard `_` to denote any number
      of variables you don't intend to use"). Implicit-typed
      destructuring (`var (a, b) = ...`) is the documented C#
      idiom (`Microsoft Learn Tuple deconstruction`); the spec
      uses `var` for consistency with C# style guides while
      preserving the Dart-side discard pattern. Each call site
      maps 1:1 — no surface rearrangement.
    idiom_id: null
    research_finding_id: rf-dart3-record-pattern-to-csharp-tuple-deconstruction
    nuance: >-
      Dart-3-records nuance. Dart 3 records (`(T1, T2)`) and Dart
      3 patterns (`final (a, b) = expr;`) are NEW LANGUAGE
      FEATURES (Dart 3.0 release notes) and the source uses them
      pervasively for "value + bytes-consumed" returns. C# tuple
      deconstruction has existed since C# 7 (much older) and is
      the documented direct counterpart (Microsoft Learn:
      "deconstruction of a tuple"). The two are
      semantically identical at the call site; the difference is
      type-system depth (Dart records have structural typing and
      named fields; C# `ValueTuple` is nominal with position-or-
      name access — both work here because every record-tuple in
      this file is unnamed/positional). The spec preserves
      destructuring at call sites verbatim.
  - construct_key: dart.method.format_exception_wire_protocol_throw_with_message_interpolation
    source_form: >-
      throw FormatException('Invalid global variable ID format: $encoded');
      throw FormatException('Unexpected end of input');
      throw FormatException('Unknown term tag: $tag');
      throw FormatException('Unknown constant type tag: $typeTag');
    target_decision: >-
      Dart `FormatException` (from `dart:core`) → C# `System.
      FormatException` (Microsoft Learn: "The exception that is
      thrown when the format of an argument is invalid, or when a
      composite format string is not well formed"). Message
      strings preserved VERBATIM with C# `$` interpolation
      replacing Dart `$`: `throw new FormatException($"Invalid
      global variable ID format: {encoded}");`, `throw new
      FormatException($"Unknown term tag: {tag}");`, etc. NOT
      promoted to `ArgumentException` / `InvalidDataException` /
      `IOException` even though those might be more "idiomatic"
      for a binary-deserialiser failure in some C# style guides —
      because (a) the Dart side throws `FormatException` and a
      cross-language test harness MAY pattern-match on exception
      type (FR-013: don't silently change observable behaviour),
      and (b) `FormatException` is the standard .NET counterpart
      with matching semantics ("format of an argument is
      invalid").
    idiom_id: null
    research_finding_id: rf-dart-format-exception-to-csharp-format-exception
    nuance: >-
      Exception-mapping nuance. Both `dart:core.FormatException`
      and `System.FormatException` describe "the input is well-
      formed at the byte level but does not match the expected
      format" — exact semantic counterparts (Microsoft Learn
      `FormatException` doc compared to Dart API). REJECTED
      alternatives: (1) `System.IO.InvalidDataException` —
      narrower (stream-data corruption), and Dart has no
      equivalent so cross-language harness comparisons would
      regress; (2) `ArgumentException` — about argument validity
      at the API boundary, not about wire-format parsing, and
      Dart's `FormatException` is the documented "wire/string-
      format problem" type. Message interpolation:
      `$encoded` Dart → `{encoded}` C# (Microsoft Learn
      interpolated strings); the embedded variable values are
      reproduced 1:1.
conversion_units:
  - class GlobalVarId (sealed, IEquatable<GlobalVarId>; readonly Creator/LocalId; Encode/Decode statics; HashCode.Combine; ==/!= overloads; FormatException on parse failure)
  - readonly record struct VariableLookup (Creator, CreatorLocalId, IsReader) (helper for lookupVariable callback return — promoted from Dart anonymous record)
  - class PayloadSerializer (sealed; AgentId readonly property; const byte TagConstant/TagVariable/TagStruct/TagList)
  - "  - SerializeMessage(OutboundMessage) -> byte[]  (1B type, varlen-prefixed UTF-8 dest, varlen-prefixed byte[] payload)"
  - "  - DeserializeMessage(byte[]) -> OutboundMessage  (mirror; enum cast via byte → MessageType with Enum.IsDefined check + throw FormatException on unknown)"
  - "  - CreateAssignmentPayloadV2(int, Term, Func<int,bool>, Func<int,VariableLookup>?) -> byte[]"
  - "  - DeserializeAssignmentPayload(byte[], Func<bool,int>, Action<int,bool,GlobalVarId,int?>?) -> (GlobalVarId, Term)"
  - "  - CreateGlobalSendPayload(GlobalName, Term, Func<int,bool>, Func<int,VariableLookup>?) -> byte[]"
  - "  - CreateSerializerPayload(GlobalName, Term, Func<int,bool>, Func<int,VariableLookup>?) -> byte[]  (Debug.Assert on isWriter && index==0)"
  - "  - BuildSerializerListCell(Term, GlobalName) -> Term  (private; builds StructTerm('.', [content, ConstTerm('#serializer:agent:0')]))"
  - "  - DeserializeGlobalSendPayload(byte[], Func<bool,int>, Action<int,bool,GlobalVarId,int?>?) -> (GlobalName, Term)"
  - "  - CreateReadRequestPayload(int, string) -> byte[]"
  - "  - DeserializeReadRequestPayload(byte[]) -> int"
  - "  - CreateAbandonPayload(int) -> byte[]"
  - "  - DeserializeAbandonPayload(byte[]) -> int"
  - "  - SerializeTermWithCallbacks(Term, string, Func<int,bool>, Func<int,VariableLookup>?) -> byte[]"
  - "  - SerializeTermRecursiveV2(Term, string, MemoryStream, Func<int,bool>, Func<int,VariableLookup>?)  (private; exhaustive type-pattern switch; throws NotSupportedException on MutualRefTerm/ModuleTerm)"
  - "  - SerializeConstant(object?, MemoryStream)  (private; type-pattern switch with 'nil' literal first, then int/double/string/bool; big-endian Int64/Float64 via BinaryPrimitives)"
  - "  - CreateAgentMessagePayload(Term, Func<int,bool>, Func<int,VariableLookup>?) -> byte[]"
  - "  - SerializeAgentMessage(Term) -> byte[]  (ground-term-only convenience; throws StateError-equivalent InvalidOperationException when VarRef encountered)"
  - "  - static DeserializeAgentMessagePayloadWithMapping(byte[], Func<bool,int>, Action<int,bool,GlobalVarId,int?>?) -> (Term, Dictionary<int, GlobalVarId>)  (throwaway-instance pattern preserved)"
  - "  - DeserializeAgentMessagePayload(byte[], Func<bool,int>, Action<int,bool,GlobalVarId,int?>?) -> Term"
  - "  - DeserializeTermWithMappingV2(byte[]|ReadOnlySpan<byte>, int, Dictionary<string,int>, Func<bool,int>, Action<int,bool,GlobalVarId,int?>?) -> (Term, int)  (private; exhaustive switch on tag byte; throws FormatException on unknown tag)"
  - "  - DeserializeConstant(byte[]|ReadOnlySpan<byte>, int) -> (object?, int)  (private; switch on byte type tag; throws FormatException on unknown tag)"
  - "  - static EncodeLength(int) -> byte[]  (1/2/4-byte tagged-prefix varlen, preserved verbatim)"
  - "  - static DecodeLength(ReadOnlySpan<byte>, int) -> (int, int)  (mirror)"
  - "  - static EncodeInt64(long) -> byte[]   (big-endian via BinaryPrimitives.WriteInt64BigEndian)"
  - "  - static DecodeInt64(ReadOnlySpan<byte>, int) -> long  (big-endian via BinaryPrimitives.ReadInt64BigEndian)"
  - "  - static EncodeFloat64(double) -> byte[]  (big-endian via BinaryPrimitives.WriteDoubleBigEndian)"
  - "  - static DecodeFloat64(ReadOnlySpan<byte>, int) -> double  (big-endian via BinaryPrimitives.ReadDoubleBigEndian)"
escalations: []
```

## Rationale & Research Provenance

This file is the cross-agent wire serializer: a `PayloadSerializer`
class that frames terms, variables, message-typed envelopes, and four
flavours of madGLP control payloads (assignment, global-send, read-
request, abandon) into a length-prefixed binary protocol with explicit
big-endian numerics, UTF-8 string boundaries, and a custom 1/2/4-byte
variable-length integer encoding. The companion `GlobalVarId`
value-pair (creator + localId) is a small entity with by-value
equality. Every non-trivial decision is grounded below with both a
deep-analysis basis and an authoritative (official Dart and/or
Microsoft Learn) research basis per FR-009/FR-010/FR-024 and
SC-006.

### rf-dart-value-pair-eq-objecthash-to-csharp-iequatable-hashcode-combine

**Deep analysis.** `GlobalVarId` holds two `final` fields (`String
creator`, `int localId`), overrides `==`/`hashCode` for by-value
equality, and provides `encode()` / `decode(String)` round-trip
helpers. `Object.hash(creator, localId)` is Dart's documented
multi-argument hash combiner.

**Research (authoritative).** Microsoft Learn `System.HashCode.
Combine`
(https://learn.microsoft.com/en-us/dotnet/api/system.hashcode.combine):
"Combines values into a hash code." — the documented .NET counterpart
of Dart's `Object.hash`. Microsoft Learn `IEquatable<T>`
(https://learn.microsoft.com/en-us/dotnet/api/system.iequatable-1) and
"How to define value equality for a type"
(https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/statements-expressions-operators/how-to-define-value-equality-for-a-type)
together pin the idiom: when a class needs by-value equality on its
fields, implement `IEquatable<T>`, override
`Equals(object?)`/`GetHashCode()`, and overload `==`/`!=`. Verbatim
queries: "C# HashCode.Combine multi-field hash combiner"; "C#
IEquatable two-field value class equality".

**Conclusion.** `sealed class GlobalVarId : IEquatable<GlobalVarId>`
with manual `Equals`/`GetHashCode`/`==`/`!=` using `HashCode.Combine
(Creator, LocalId)`. Plain class, not `record` — preserves the
file's class style and avoids `EqualityContract`/`with`-expression
baggage. Carry-forward to `lib/runtime/terms.dart` spec where the
same `IEquatable<T>` idiom is applied to `VarRef`.

### rf-dart-bytesbuilder-bytedata-to-csharp-memorystream-binaryprimitives

**Deep analysis.** The file builds wire payloads via
`BytesBuilder.addByte(int)` and `BytesBuilder.add(List<int>)`, then
materialises with `toBytes()` → `Uint8List`. Numeric serialisation
uses `ByteData(8)` with explicit `Endian.big` for 64-bit ints and
doubles. Every string is UTF-8 encoded at the boundary.

**Research (authoritative).** Microsoft Learn `System.IO.MemoryStream`
(https://learn.microsoft.com/en-us/dotnet/api/system.io.memorystream):
"Creates a stream whose backing store is memory." — the canonical
mutable byte-buffer. Microsoft Learn `System.Buffers.Binary.
BinaryPrimitives.WriteInt64BigEndian`
(https://learn.microsoft.com/en-us/dotnet/api/system.buffers.binary.binaryprimitives.writeint64bigendian)
and `WriteDoubleBigEndian` / `ReadInt64BigEndian` /
`ReadDoubleBigEndian` (.NET 5+): "Writes an `Int64` into a span of
bytes, as big endian." — the decisive replacement for the Dart
`ByteData` + `Endian.big` idiom. Microsoft Learn
`System.Text.Encoding.UTF8`
(https://learn.microsoft.com/en-us/dotnet/api/system.text.encoding.utf8):
"Gets an encoding for the UTF-8 format." — singleton, no `new
UTF8Encoding()` allocation per call. Microsoft Learn `System.IO.
BinaryWriter`
(https://learn.microsoft.com/en-us/dotnet/api/system.io.binarywriter)
is explicitly REJECTED: "Writes primitive types in binary to a
stream and supports writing strings in a specific encoding" but the
default integer write is little-endian (`Write(Int64)` docs:
"writes an eight-byte signed integer to the current stream and
advances the stream position by eight bytes" — the byte order is
little-endian per platform convention), which is INCOMPATIBLE with
Dart's `Endian.big`. Verbatim queries: "C# big endian Int64 byte
write"; "C# UTF-8 encoding singleton"; "C# MemoryStream byte buffer
builder"; "BinaryWriter default endianness".

**Conclusion.** Use `MemoryStream` directly (no `BinaryWriter`) with
`BinaryPrimitives.{Write,Read}{Int64,Double}BigEndian` for explicit
big-endian numerics and `Encoding.UTF8.{GetBytes,GetString}` at
every string boundary. `Uint8List` → `byte[]` at every public
surface; `ReadOnlySpan<byte>` for zero-copy internal slicing where
lifetime allows.

### rf-dart-custom-tagged-varlen-to-csharp-preserve-verbatim

**Deep analysis.** `_encodeLength`/`_decodeLength` implement a
hand-rolled 1/2/4-byte tagged-prefix variable-length integer
encoding with high-bit tags `0x00` (1 byte, <128), `0x80` (2 bytes,
<16384), `0xC0` (4 bytes, <2^30). The bit layout multiplexes tag
bits INTO the length bytes — incompatible with every stock .NET
length-encoder.

**Research (authoritative).** Microsoft Learn
`BinaryWriter.Write7BitEncodedInt`
(https://learn.microsoft.com/en-us/dotnet/api/system.io.binarywriter.write7bitencodedint):
"Writes a 32-bit integer in a compressed format" — this is **LEB128**
(7 data bits per byte, MSB = continuation), a DIFFERENT wire
format. Verbatim quote from Microsoft Learn: "The integer of the
value parameter is written out seven bits at a time, starting with
the seven least-significant bits. The high bit of a byte indicates
whether there are more bytes to be written after this one." — bit-
incompatible with the Dart custom scheme.
Microsoft Learn `BinaryPrimitives.WriteInt32BigEndian` writes 4
bytes always — wastes space and changes the wire format. Verbatim
queries: "BinaryWriter Write7BitEncodedInt LEB128 wire format"; "C#
variable-length integer custom encoding preserve".

**Conclusion.** Preserve `EncodeLength`/`DecodeLength` byte-for-byte
in C#. Use plain `byte`-typed bit-arithmetic with the same masks
(`0x80`, `0xC0`, `0x3F`, `0xFF`) and shifts (`>> 8`, `>> 16`,
`>> 24`). Wire compatibility with Dart senders/receivers is the
load-bearing constraint.

### rf-dart-enum-ordinal-wire-mapping-to-csharp-enum-int-cast

**Deep analysis.** `serializeMessage` writes `message.type.index`
(Dart enum ordinal) as a single byte; `deserializeMessage` reads it
back via `MessageType.values[typeIndex]`. Wire-format compatibility
requires the C# enum to have the same declaration-order ordinals.

**Research (authoritative).** Microsoft Learn enum reference
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/enum):
"By default, the associated constant values of enum members are of
type `int`; they start with zero and increase by one following the
definition text order." — identical ordinal scheme to Dart's
implicit enum `.index`. Microsoft Learn `Enum.IsDefined`
(https://learn.microsoft.com/en-us/dotnet/api/system.enum.isdefined):
"Returns an indication whether a constant with a specified value
exists in a specified enumeration." — used to preserve Dart's
`MessageType.values[N]` `RangeError`-on-out-of-range behaviour by
throwing `FormatException` instead of silently producing an invalid
enum value (Microsoft Learn explicit-cast docs: "Explicit
conversion exists from any integral numeric type to any enum type"
without runtime validation). Verbatim queries: "C# enum default
ordinal declaration order"; "C# Enum.IsDefined validate cast int to
enum".

**Conclusion.** Cast `(byte)message.Type` on write, `(MessageType)
bytes[offset]` on read, guarded by `Enum.IsDefined` with a throwing
`FormatException` fallback to match Dart's `RangeError`-on-bad-
ordinal semantics. The codegen MUST declare `MessageType
{ Assignment, AgentMessage }` in that exact order with no explicit
values. Carry-forward from `message_queue.dart` spec
(`enum MessageType plain-value-enum`).

### rf-dart-record-tuple-to-csharp-named-valuetuple

**Deep analysis.** The file returns `(int, int)`, `(Term, int)`,
`(dynamic, int)`, `(GlobalVarId, Term)`, `(GlobalName, Term)`, and
`(Term, Map<int, GlobalVarId>)` from various parser methods —
Dart-3 record tuples used for "primary value + bookkeeping" returns
(typically bytes-consumed cursors).

**Research (authoritative).** Microsoft Learn `System.ValueTuple`
(https://learn.microsoft.com/en-us/dotnet/api/system.valuetuple-2):
"Provides static methods for creating value tuples." Microsoft
Learn tuple types
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/value-tuples):
"Available in C# 7.0 and later, the tuples feature provides
concise syntax to group multiple data elements in a lightweight
data structure." Microsoft Learn tuple deconstruction
(https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/deconstruct):
"You can deconstruct a tuple into separate variables." Microsoft
Learn `dynamic`
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/reference-types#the-dynamic-type):
"For dynamic operations, the type of the variable doesn't matter
until run time" — heavier than Dart `dynamic`'s "skip static check"
semantics. Verbatim queries: "C# named ValueTuple return type"; "C#
dynamic versus Dart dynamic"; "C# tuple deconstruction discard".

**Conclusion.** Map record tuples to NAMED C# `ValueTuple` types at
each method signature (e.g. `(int Value, int BytesConsumed)`). Dart
`dynamic` → `object?` in tuple fields and parameters (NOT C#
`dynamic`). Call-site destructuring with `var (a, b) = …` and
discard `_`.

### rf-dart-is-chain-on-sealed-sumtype-to-csharp-typepattern-switch

**Deep analysis.** `_serializeTermRecursiveV2` dispatches on the
`Term` sealed sum-type via `if (term is X) … else if (term is Y) …`
chain, with a throwing default arm for the two unrepresentable
leaves (`MutualRefTerm`, `ModuleTerm`).

**Research (authoritative).** Microsoft Learn type patterns
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#type-pattern):
"A type pattern matches an expression whose runtime type is
compatible with a given type." Microsoft Learn switch expressions
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/switch-expression):
"A switch expression that uses type patterns can implement
exhaustive dispatch on a sealed class hierarchy" — though
exhaustiveness is best-effort warning CS8509, not guaranteed; the
throwing default arm is the defensive complement. Microsoft Learn
`NotSupportedException`
(https://learn.microsoft.com/en-us/dotnet/api/system.notsupportedexception):
"The exception that is thrown when an invoked method is not
supported, or when there is an attempt to read, seek, or write to a
stream that does not support the invoked functionality." — exact
counterpart of Dart `UnsupportedError` (Dart `dart:core`
`UnsupportedError`: "thrown when the called operation is not
supported"). Carry-forward from `lib/runtime/terms.dart` spec
construct
`dart.abstract_base_class.empty_open_marker_for_closed_sum_type` —
the closure argument for the `Term` hierarchy and the throwing-
default-arm convention. Verbatim queries: "C# type pattern switch
sealed hierarchy"; "C# NotSupportedException Dart UnsupportedError".

**Conclusion.** Type-pattern switch on `term` with explicit cases
for `ConstTerm`, `VarRef`, `StructTerm` (in that order), and a
throwing default arm `throw new NotSupportedException(...)`.
`MutualRefTerm` and `ModuleTerm` deliberately excluded from the
wire format (preserved from Dart source).

### rf-dart-dynamic-runtime-type-dispatch-to-csharp-typepattern-switch

**Deep analysis.** `_serializeConstant` dispatches on the runtime
type of a `dynamic` value: first equality-check against the string
`'nil'`, then `is int` / `is double` / `is String` / `is bool` in
order, with a throwing default.

**Research (authoritative).** Microsoft Learn pattern matching
overview
(https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/pattern-matching):
"Pattern matching is a technique where you test an expression to
determine if it has certain characteristics" — type patterns and
constant patterns both supported. Microsoft Learn constant pattern
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/patterns#constant-pattern):
"You use a constant pattern to test if an expression result equals
a specified constant" — used here for the `"nil"` literal match.
Microsoft Learn integral numeric types
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/integral-numeric-types):
`int` and `double` are distinct runtime types in C#, matching
Dart's `int`/`double` disjointness. Verbatim queries: "C# constant
pattern string literal switch"; "C# type pattern int double
disjoint dispatch".

**Conclusion.** C# `switch (value)` with case order `"nil"` →
`int i` → `double d` → `string s` → `bool b` → throwing default.
The string-marker `"nil"` is preserved (not promoted to a sentinel
constant) to keep wire round-trip exact.

### rf-dart-anon-record-callback-to-csharp-record-struct-helper

**Deep analysis.** `lookupVariable` callback returns Dart's
anonymous record `({String creator, int creatorLocalId, bool
isReader})` — an inline structurally-typed record with named
fields. Used to provide imported-variable provenance during term
serialisation.

**Research (authoritative).** Dart records reference
(https://dart.dev/language/records): "Records are an anonymous,
immutable, aggregate type… Records are real values; you can store
them in variables, nest them, pass them to and return them from
functions, and store them in data structures such as lists, maps,
and sets." — Dart records are value types with structural typing.
Microsoft Learn `record struct`
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record):
"`record struct` is a value type that overrides members from
`System.Object` … synthesizes a method that overrides `Equals
(Object)`" — the value-type variant of records, available since C#
10. Microsoft Learn `readonly record struct`
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record#nondestructive-mutation):
"You can declare a `readonly record struct` to indicate that the
type is immutable." — exact match for Dart's immutable-record
semantics. C# anonymous types REJECTED: they exist but are
`internal sealed class` (reference type, structural equality but
not usable across method boundaries). Verbatim queries: "C# named
record struct callback return"; "Dart anonymous record to C#";
"readonly record struct immutable value type".

**Conclusion.** Promote the anonymous Dart record to a NAMED
`public readonly record struct VariableLookup(string Creator, int
CreatorLocalId, bool IsReader)`. The callback signature becomes
`Func<int, VariableLookup>?`. Call-site property access maps 1:1.

### rf-dart-assert-to-csharp-debug-assert

**Deep analysis.** `createSerializerPayload` uses Dart `assert(...)`
to enforce a precondition (`isWriter && index == 0`). Dart assert
is documented as debug-only — silently elided in production
builds.

**Research (authoritative).** Dart language tour assertions
(https://dart.dev/language/error-handling#assert): "During
development, use an assert statement … to disrupt normal execution
if a boolean condition is false." and "Production code ignores the
assert statement." Microsoft Learn `Debug.Assert`
(https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.debug.assert):
"Checks for a condition; if the condition is `false`, displays a
message box that shows the call stack. … This method is ignored in
Release builds by default." Microsoft Learn `Trace.Assert`
(https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.trace.assert):
"Checks for a condition; if the condition is `false`, outputs a
specified message and displays a message box that shows the call
stack." — `Trace.Assert` is enabled in BOTH debug and release,
making it semantically stronger than Dart `assert`. Verbatim
queries: "C# Debug.Assert versus Trace.Assert release build"; "Dart
assert production elision".

**Conclusion.** Map `assert(cond, msg)` to `Debug.Assert(cond, msg)`
ONLY — never `Trace.Assert`, never an unconditional `throw`.
Preserves Dart's debug-only contract exactly.

### rf-dart-throwaway-instance-static-method-to-csharp-preserve-pattern

**Deep analysis.** `deserializeAgentMessagePayloadWithMapping` is a
static method that constructs `PayloadSerializer('')` solely to
invoke the private instance method `_deserializeTermWithMappingV2`.
The throwaway-instance pattern is awkward but intentional in the
source.

**Research (authoritative).** FR-013 (escalate-don't-guess) and
FR-009 (preserve-semantics) jointly require preserving source
patterns when the cleaner alternative would diverge silently.
Microsoft Learn static methods
(https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/static-classes-and-static-class-members):
static methods cannot access instance state, but a private
instance method that doesn't reference instance state can be
refactored to static — at the cost of changing the Dart-side
counterpart's signature in a future cross-language sync. Verbatim
query: "C# preserve Dart instance method versus refactor static
when no instance state used".

**Conclusion.** Preserve `var tempSerializer = new
PayloadSerializer("");` followed by `tempSerializer.
DeserializeTermWithMappingV2(...)`. Add a code comment explaining
the workaround so future cleanup is an explicit, sync'd
refactor — not a silent C#-side drift.

### rf-dart-map-containskey-bang-access-to-csharp-trygetvalue

**Deep analysis.** `varMapping.containsKey(globalIdStr)` followed
by `varMapping[globalIdStr]!` is the Dart two-lookup idiom for
"get-or-default". `Dictionary<TKey, TValue>` in C# has a documented
single-lookup form.

**Research (authoritative).** Microsoft Learn
`Dictionary<TKey,TValue>.TryGetValue`
(https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2.trygetvalue):
"Gets the value associated with the specified key. … When this
method returns, contains the value associated with the specified
key, if the key is found." — the documented "lookup-or-default" C#
idiom. Microsoft Learn dictionary index docs (same page) confirm
the indexer throws `KeyNotFoundException` on miss. Verbatim
queries: "C# Dictionary TryGetValue versus ContainsKey indexer";
"C# null-conditional invoke delegate".

**Conclusion.** Promote the two-lookup form to `TryGetValue` —
established codeconv-016 idiom (see `message_queue.dart` construct
`Map-String-Queue-FIFO-per-destination`). Single dictionary lookup,
single allocation path, exactly the same observable behaviour.

### rf-dart3-record-pattern-to-csharp-tuple-deconstruction

**Deep analysis.** Call sites use Dart 3 record-pattern
destructuring with anonymous discards: `final (a, b) = method(...);`
and `final (a, _) = method(...);`.

**Research (authoritative).** Dart 3 records and patterns
(https://dart.dev/language/records): destructuring with `final
(a, b) = expr;`. Microsoft Learn tuple deconstruction
(https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/deconstruct):
"You can deconstruct a tuple into separate variables." Microsoft
Learn discards
(https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/functional/discards):
"Discards are placeholder variables that are intentionally unused
in application code." Verbatim queries: "C# tuple deconstruction
discard variable"; "Dart 3 record pattern to C# tuple
destructure".

**Conclusion.** `final (a, b) = method(...)` → `var (a, b) =
method(...)`. Discards `_` map 1:1.

### rf-dart-format-exception-to-csharp-format-exception

**Deep analysis.** Four `FormatException` throws across the
deserialisers: malformed GlobalVarId, premature EOF, unknown term
tag, unknown constant type tag. Each carries an interpolated
message describing the failure point.

**Research (authoritative).** Microsoft Learn `System.
FormatException`
(https://learn.microsoft.com/en-us/dotnet/api/system.formatexception):
"The exception that is thrown when the format of an argument is
invalid, or when a composite format string is not well formed." —
exact semantic counterpart of Dart's `dart:core.FormatException`.
Microsoft Learn interpolated strings
(https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/tokens/interpolated):
`$"{expr}"` substitution. REJECTED alternatives:
`InvalidDataException` (narrower, stream-specific);
`ArgumentException` (about API arguments, not wire-format); both
would silently change cross-language exception-type contracts.
Verbatim queries: "C# FormatException versus InvalidDataException
binary deserializer"; "C# interpolated string variable
substitution".

**Conclusion.** All four `FormatException` throws preserved as
`System.FormatException` with C# `$"..."` interpolated messages.
Message text preserved verbatim.

## Notes — well-known nuances explicitly addressed (FR-009 / US2 AS4)

- **Endianness**: explicit big-endian for all 64-bit numerics
  (`Endian.big` → `BinaryPrimitives.{Write,Read}{Int64,Double}
  BigEndian`). Load-bearing for cross-process wire compatibility.
- **Byte-width**: type tags and bool encoding explicitly `byte`
  (not `int`) in C#; preserves single-octet wire footprint.
  Length-encoder bands cap at 30 effective bits — same as Dart's
  silent ceiling. NOT escalated (FR-013: preserve semantics, do
  not silently "fix").
- **UTF-8 boundaries**: every string crossing the wire is
  explicitly UTF-8 encoded/decoded; never relies on platform
  default. C# uses `Encoding.UTF8` singleton.
- **Null-safety**: all callback delegates that are Dart-optional
  (`bool Function(int)?`, `(...) Function(int)?`, `void Function
  (...)?`) become nullable C# delegates (`Func<int, bool>?`,
  `Func<int, VariableLookup>?`, `Action<int, bool, GlobalVarId,
  int?>?`). Required Dart callbacks become non-nullable C#
  delegates. The `int?` ("nullable int") parameters map to
  `System.Nullable<int>` value-type form (Microsoft Learn nullable
  value types).
- **Exception handling**: `FormatException` → `FormatException`
  (1:1, semantic counterpart); `UnsupportedError` →
  `NotSupportedException` (1:1, semantic counterpart);
  `StateError` (in `serializeAgentMessage` callback) →
  `InvalidOperationException` (Microsoft Learn: "The exception
  that is thrown when a method call is invalid for the object's
  current state" — exact match for Dart `StateError`).
  `assert(...)` → `Debug.Assert(...)` (debug-only, NOT
  `Trace.Assert` or unconditional throw).
- **Reference-vs-value**: `PayloadSerializer` and `GlobalVarId`
  are reference types (`sealed class`). `VariableLookup` (the
  promoted anonymous-record helper) is a `readonly record
  struct` (value type) — Dart records are value types and the
  callback boundary discards instances immediately after
  destructuring, so the value-type form preserves semantics
  without allocation overhead.
- **Stream / IAsyncEnumerable / Future / Task**: ABSENT. The
  serializer is synchronous; no `async`, no `Future`, no
  `Stream`. C# codegen MUST NOT introduce
  `System.Threading.Channels.Channel<T>`, `Task<T>`, or
  `IAsyncEnumerable<T>` — would invent async semantics the source
  does not have.
- **Thread-safety**: source is single-threaded (no locks, no
  isolates). C# port preserves single-threaded contract; no
  `ConcurrentDictionary`, no `Interlocked`.
- **Mutable cursor (`int offset`)**: pervasive read-cursor pattern
  in deserialisers; preserved as a plain `int` local in C#
  methods (NOT refactored to `ref int offset` — Dart's tuple-
  return `(value, bytesConsumed)` already threads the cursor
  back).
- **Term-tree polymorphism**: dispatch via type-pattern switch
  on the sealed `Term` hierarchy (closure technique established
  in `lib/runtime/terms.dart` spec); throwing default arms on
  unrepresentable leaves preserved.
- **Identifier casing**: Dart camelCase methods (`serializeMessage`,
  `_encodeLength`, `createGlobalSendPayload`, …) → C# PascalCase
  (`SerializeMessage`, `EncodeLength`, `CreateGlobalSendPayload`);
  private fields keep the leading underscore in the C# form
  (`_tagConstant` → `TagConstant` as a private `const` per .NET
  naming conventions: const fields are PascalCase even when
  private).
- **`MutualRefTerm` / `ModuleTerm` cross-agent transport**: NOT
  defined by the source (the serialiser's default arm throws); C#
  port preserves the throwing contract. A future cross-agent
  mutual-ref protocol is an explicit ESCALATION venue, NOT a
  silent codegen invention.
- Zero escalations: every non-trivial construct resolved from
  authoritative Dart and/or Microsoft Learn official documentation.
