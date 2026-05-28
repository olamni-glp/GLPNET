// Payload Serialization for irmaGLP
//
// Serializes terms and messages to bytes for inter-agent transport.
// Uses global variable IDs (creator:localId) for cross-agent routing.
//
// Specification: /docs/ma/irmaGLP-spec.md Section 6 and 8.3

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using GlpRuntime.Runtime;

namespace GlpRuntime.Multiagent;

/// <summary>
/// Global Variable ID encoding: creator:localId pair with by-value equality.
/// </summary>
public sealed class GlobalVarId : IEquatable<GlobalVarId>
{
    public string Creator { get; }
    public int LocalId { get; }

    public GlobalVarId(string creator, int localId)
    {
        Creator = creator;
        LocalId = localId;
    }

    /// <summary>Encode to wire string: creator:localId</summary>
    public string Encode() => $"{Creator}:{LocalId}";

    /// <summary>Decode from wire string: creator:localId</summary>
    public static GlobalVarId Decode(string encoded)
    {
        var parts = encoded.Split(':');
        if (parts.Length != 2)
            throw new FormatException($"Invalid global variable ID format: {encoded}");
        if (!int.TryParse(parts[1], out var localId))
            throw new FormatException($"Invalid local ID in global variable ID: {encoded}");
        return new GlobalVarId(parts[0], localId);
    }

    public override bool Equals(object? other) =>
        other is GlobalVarId g && g.Creator == Creator && g.LocalId == LocalId;

    public bool Equals(GlobalVarId? other) =>
        other is not null && other.Creator == Creator && other.LocalId == LocalId;

    public override int GetHashCode() => HashCode.Combine(Creator, LocalId);

    public static bool operator ==(GlobalVarId? left, GlobalVarId? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(GlobalVarId? left, GlobalVarId? right) => !(left == right);

    public override string ToString() => Encode();
}

/// <summary>
/// Helper type promoted from Dart anonymous record
/// <c>({String creator, int creatorLocalId, bool isReader})</c>
/// used as the return type of the <c>lookupVariable</c> callback.
/// </summary>
public readonly record struct VariableLookup(string Creator, int CreatorLocalId, bool IsReader);

/// <summary>
/// Payload serializer for terms and messages.
/// </summary>
public sealed class PayloadSerializer
{
    public string AgentId { get; }

    public PayloadSerializer(string agentId)
    {
        AgentId = agentId;
    }

    // Type tags for serialization (single-octet wire values)
    private const byte TagConstant = 1;
    private const byte TagVariable = 2;
    private const byte TagStruct = 3;
    private const byte TagList = 4;

    // ============================================================================
    // High-level message serialization
    // ============================================================================

    /// <summary>Serialize an OutboundMessage to bytes for transport.</summary>
    public byte[] SerializeMessage(OutboundMessage message)
    {
        using var ms = new MemoryStream();

        // Type tag — enum ordinal as single byte
        ms.WriteByte((byte)message.Type);

        // Destination: varlen-prefixed UTF-8 string
        var destBytes = Encoding.UTF8.GetBytes(message.Destination);
        var destLen = EncodeLength(destBytes.Length);
        ms.Write(destLen, 0, destLen.Length);
        ms.Write(destBytes, 0, destBytes.Length);

        // Payload: varlen-prefixed raw bytes
        var payloadArr = message.Payload.ToArray();
        var payloadLen = EncodeLength(payloadArr.Length);
        ms.Write(payloadLen, 0, payloadLen.Length);
        ms.Write(payloadArr, 0, payloadArr.Length);

        return ms.ToArray();
    }

    /// <summary>Deserialize bytes to OutboundMessage.</summary>
    public OutboundMessage DeserializeMessage(byte[] bytes)
    {
        int offset = 0;

        // Type
        byte typeIndex = bytes[offset++];
        if (!Enum.IsDefined(typeof(MessageType), (int)typeIndex))
            throw new FormatException($"Unknown message type: {typeIndex}");
        var type = (MessageType)typeIndex;

        // Destination
        var (destLength, destLengthSize) = DecodeLength(bytes.AsSpan(), offset);
        offset += destLengthSize;
        var destination = Encoding.UTF8.GetString(bytes, offset, destLength);
        offset += destLength;

        // Payload
        var (payloadLength, payloadLengthSize) = DecodeLength(bytes.AsSpan(), offset);
        offset += payloadLengthSize;
        var payload = new List<byte>(bytes.AsSpan(offset, payloadLength).ToArray());

        return new OutboundMessage(destination, type, payload);
    }

    // ============================================================================
    // Assignment message payload
    // ============================================================================

    /// <summary>Create assignment payload: varId + serialized term.</summary>
    public byte[] CreateAssignmentPayloadV2(
        int varId,
        Term value,
        Func<int, bool> isReader,
        Func<int, VariableLookup>? lookupVariable = null)
    {
        using var ms = new MemoryStream();

        // Variable ID as global ID
        var globalId = new GlobalVarId(AgentId, varId);
        var idBytes = Encoding.UTF8.GetBytes(globalId.Encode());
        var idLen = EncodeLength(idBytes.Length);
        ms.Write(idLen, 0, idLen.Length);
        ms.Write(idBytes, 0, idBytes.Length);

        // Serialized term
        var termBytes = SerializeTermWithCallbacks(value, AgentId, isReader, lookupVariable);
        ms.Write(termBytes, 0, termBytes.Length);

        return ms.ToArray();
    }

    /// <summary>Parse assignment payload to (globalVarId, value).</summary>
    public (GlobalVarId GlobalId, Term Term) DeserializeAssignmentPayload(
        byte[] payload,
        Func<bool, int> allocateImportedVar,
        Action<int, bool, GlobalVarId, int?>? onVariableImported = null)
    {
        int offset = 0;

        var (idLength, idLengthSize) = DecodeLength(payload.AsSpan(), offset);
        offset += idLengthSize;
        var globalIdStr = Encoding.UTF8.GetString(payload, offset, idLength);
        offset += idLength;
        var globalId = GlobalVarId.Decode(globalIdStr);

        var varMapping = new Dictionary<string, int>();
        var (term, _) = DeserializeTermWithMappingV2(payload, offset, varMapping, allocateImportedVar, onVariableImported);

        return (globalId, term);
    }

    // ============================================================================
    // Global send message payload (madGLP)
    // ============================================================================

    /// <summary>Create global_send payload: GlobalName + serialized term.</summary>
    public byte[] CreateGlobalSendPayload(
        GlobalName globalName,
        Term value,
        Func<int, bool> isReader,
        Func<int, VariableLookup>? lookupVariable = null)
    {
        using var ms = new MemoryStream();

        // GlobalName type (0=writer, 1=reader)
        ms.WriteByte(globalName.IsWriter ? (byte)0 : (byte)1);

        // GlobalName agent: varlen-prefixed UTF-8
        var agentBytes = Encoding.UTF8.GetBytes(globalName.Agent);
        var agentLen = EncodeLength(agentBytes.Length);
        ms.Write(agentLen, 0, agentLen.Length);
        ms.Write(agentBytes, 0, agentBytes.Length);

        // GlobalName index: varlen int
        var indexLen = EncodeLength(globalName.Index);
        ms.Write(indexLen, 0, indexLen.Length);

        // Serialized term
        var termBytes = SerializeTermWithCallbacks(value, AgentId, isReader, lookupVariable);
        ms.Write(termBytes, 0, termBytes.Length);

        return ms.ToArray();
    }

    /// <summary>Create serializer payload: wraps content in list cell [T | _w(q,0)].</summary>
    public byte[] CreateSerializerPayload(
        GlobalName serializerName,
        Term content,
        Func<int, bool> isReader,
        Func<int, VariableLookup>? lookupVariable = null)
    {
        Debug.Assert(serializerName.IsWriter && serializerName.Index == 0,
            "Serializer payload requires _w(agent, 0) global name");

        using var ms = new MemoryStream();

        // GlobalName type (0=writer for serializer)
        ms.WriteByte((byte)0);

        // GlobalName agent: varlen-prefixed UTF-8
        var agentBytes = Encoding.UTF8.GetBytes(serializerName.Agent);
        var agentLen = EncodeLength(agentBytes.Length);
        ms.Write(agentLen, 0, agentLen.Length);
        ms.Write(agentBytes, 0, agentBytes.Length);

        // GlobalName index = 0 for serializer
        var indexLen = EncodeLength(0);
        ms.Write(indexLen, 0, indexLen.Length);

        // List cell [content | serializer_ref]
        var listCell = BuildSerializerListCell(content, serializerName);
        var termBytes = SerializeTermWithCallbacks(listCell, AgentId, isReader, lookupVariable);
        ms.Write(termBytes, 0, termBytes.Length);

        return ms.ToArray();
    }

    /// <summary>Build the list cell [content | _w(q,0)] for serializer messages.</summary>
    private Term BuildSerializerListCell(Term content, GlobalName serializerName)
    {
        var serializerMarker = new ConstTerm($"#serializer:{serializerName.Agent}:0");
        return new StructTerm(".", new Term[] { content, serializerMarker });
    }

    /// <summary>Parse global_send payload to (GlobalName, Term).</summary>
    public (GlobalName GlobalName, Term Term) DeserializeGlobalSendPayload(
        byte[] payload,
        Func<bool, int> allocateImportedVar,
        Action<int, bool, GlobalVarId, int?>? onVariableImported = null)
    {
        int offset = 0;

        var isWriter = payload[offset] == 0;
        offset++;

        var (agentLength, agentLengthSize) = DecodeLength(payload.AsSpan(), offset);
        offset += agentLengthSize;
        var agent = Encoding.UTF8.GetString(payload, offset, agentLength);
        offset += agentLength;

        var (index, indexSize) = DecodeLength(payload.AsSpan(), offset);
        offset += indexSize;

        var globalName = isWriter
            ? GlobalName.Writer(agent, index)
            : GlobalName.Reader(agent, index);

        var remainingPayload = payload.AsSpan(offset).ToArray();
        var varMapping = new Dictionary<string, int>();
        var (term, _) = DeserializeTermWithMappingV2(remainingPayload, 0, varMapping, allocateImportedVar, onVariableImported);

        return (globalName, term);
    }

    // ============================================================================
    // Read request message payload
    // ============================================================================

    /// <summary>Create read request payload: varId + requester.</summary>
    public byte[] CreateReadRequestPayload(int varId, string requester)
    {
        using var ms = new MemoryStream();

        // Variable ID as global ID
        var globalId = new GlobalVarId(AgentId, varId);
        var idBytes = Encoding.UTF8.GetBytes(globalId.Encode());
        var idLen = EncodeLength(idBytes.Length);
        ms.Write(idLen, 0, idLen.Length);
        ms.Write(idBytes, 0, idBytes.Length);

        // Requester
        var reqBytes = Encoding.UTF8.GetBytes(requester);
        var reqLen = EncodeLength(reqBytes.Length);
        ms.Write(reqLen, 0, reqLen.Length);
        ms.Write(reqBytes, 0, reqBytes.Length);

        return ms.ToArray();
    }

    /// <summary>Parse read request payload to varId (requester is in message header).</summary>
    public int DeserializeReadRequestPayload(byte[] payload)
    {
        int offset = 0;

        var (idLength, idLengthSize) = DecodeLength(payload.AsSpan(), offset);
        offset += idLengthSize;
        var globalIdStr = Encoding.UTF8.GetString(payload, offset, idLength);
        var globalId = GlobalVarId.Decode(globalIdStr);

        return globalId.LocalId;
    }

    // ============================================================================
    // Abandon message payload
    // ============================================================================

    /// <summary>Create abandon payload: writerId.</summary>
    public byte[] CreateAbandonPayload(int writerId)
    {
        using var ms = new MemoryStream();

        var globalId = new GlobalVarId(AgentId, writerId);
        var idBytes = Encoding.UTF8.GetBytes(globalId.Encode());
        var idLen = EncodeLength(idBytes.Length);
        ms.Write(idLen, 0, idLen.Length);
        ms.Write(idBytes, 0, idBytes.Length);

        return ms.ToArray();
    }

    /// <summary>Parse abandon payload to varId.</summary>
    public int DeserializeAbandonPayload(byte[] payload)
    {
        int offset = 0;

        var (idLength, idLengthSize) = DecodeLength(payload.AsSpan(), offset);
        offset += idLengthSize;
        var globalIdStr = Encoding.UTF8.GetString(payload, offset, idLength);
        var globalId = GlobalVarId.Decode(globalIdStr);

        return globalId.LocalId;
    }

    // ============================================================================
    // Term serialization
    // ============================================================================

    /// <summary>Serialize a term to bytes.</summary>
    public byte[] SerializeTermWithCallbacks(
        Term term,
        string agentId,
        Func<int, bool> isReader,
        Func<int, VariableLookup>? lookupVariable = null)
    {
        using var ms = new MemoryStream();
        SerializeTermRecursiveV2(term, agentId, ms, isReader, lookupVariable);
        return ms.ToArray();
    }

    private void SerializeTermRecursiveV2(
        Term term,
        string agentId,
        MemoryStream ms,
        Func<int, bool> isReaderFn,
        Func<int, VariableLookup>? lookupVariable)
    {
        switch (term)
        {
            case ConstTerm c:
                ms.WriteByte(TagConstant);
                SerializeConstant(c.Value, ms);
                break;

            case VarRef v:
            {
                ms.WriteByte(TagVariable);

                var addr = v.Addr;
                var isReaderVar = isReaderFn(addr);

                string creator;
                int creatorLocalId;
                if (lookupVariable != null)
                {
                    var info = lookupVariable(addr);
                    creator = info.Creator;
                    creatorLocalId = info.CreatorLocalId;
                }
                else
                {
                    creator = agentId;
                    creatorLocalId = addr;
                }

                var globalId = new GlobalVarId(creator, creatorLocalId);
                var encoded = Encoding.UTF8.GetBytes(globalId.Encode());
                var encLen = EncodeLength(encoded.Length);
                ms.Write(encLen, 0, encLen.Length);
                ms.Write(encoded, 0, encoded.Length);
                ms.WriteByte(isReaderVar ? (byte)1 : (byte)0);

                // For writers, also encode the paired reader's creatorLocalId
                if (!isReaderVar)
                {
                    var pairedReaderLocalId = creatorLocalId + 1;
                    var pairedLen = EncodeLength(pairedReaderLocalId);
                    ms.Write(pairedLen, 0, pairedLen.Length);
                }
                break;
            }

            case StructTerm s:
            {
                ms.WriteByte(TagStruct);

                var functorBytes = Encoding.UTF8.GetBytes(s.Functor);
                var functorLen = EncodeLength(functorBytes.Length);
                ms.Write(functorLen, 0, functorLen.Length);
                ms.Write(functorBytes, 0, functorBytes.Length);

                var arityLen = EncodeLength(s.Args.Count);
                ms.Write(arityLen, 0, arityLen.Length);

                foreach (var arg in s.Args)
                    SerializeTermRecursiveV2(arg, agentId, ms, isReaderFn, lookupVariable);
                break;
            }

            default:
                throw new NotSupportedException($"Cannot serialize term type: {term.GetType()}");
        }
    }

    private static void SerializeConstant(object? value, MemoryStream ms)
    {
        switch (value)
        {
            case "nil":
                ms.WriteByte(0);
                break;
            case int i:
                ms.WriteByte(1);
                var intBuf = EncodeInt64((long)i);
                ms.Write(intBuf, 0, intBuf.Length);
                break;
            case long l:
                ms.WriteByte(1);
                var longBuf = EncodeInt64(l);
                ms.Write(longBuf, 0, longBuf.Length);
                break;
            case double d:
                ms.WriteByte(2);
                var dblBuf = EncodeFloat64(d);
                ms.Write(dblBuf, 0, dblBuf.Length);
                break;
            case string s:
                ms.WriteByte(3);
                var sb = Encoding.UTF8.GetBytes(s);
                var sbLen = EncodeLength(sb.Length);
                ms.Write(sbLen, 0, sbLen.Length);
                ms.Write(sb, 0, sb.Length);
                break;
            case bool b:
                ms.WriteByte(4);
                ms.WriteByte(b ? (byte)1 : (byte)0);
                break;
            default:
                throw new NotSupportedException($"Cannot serialize constant type: {value?.GetType()}");
        }
    }

    // ============================================================================
    // Agent message payload
    // ============================================================================

    /// <summary>Create agent message payload: just the serialized term.</summary>
    public byte[] CreateAgentMessagePayload(
        Term term,
        Func<int, bool> isReader,
        Func<int, VariableLookup>? lookupVariable = null)
    {
        return SerializeTermWithCallbacks(term, AgentId, isReader, lookupVariable);
    }

    /// <summary>
    /// Serialize a ground term (no variables) as an agent message payload.
    /// Throws <see cref="InvalidOperationException"/> if a VarRef is encountered.
    /// </summary>
    public byte[] SerializeAgentMessage(Term term)
    {
        return SerializeTermWithCallbacks(
            term,
            AgentId,
            _ => throw new InvalidOperationException("Ground term expected, but VarRef found"));
    }

    /// <summary>
    /// Deserialize agent message payload with variable remapping and mapping output.
    /// </summary>
    public static (Term Term, Dictionary<int, GlobalVarId> Mapping) DeserializeAgentMessagePayloadWithMapping(
        byte[] payload,
        Func<bool, int> allocateImportedVar,
        Action<int, bool, GlobalVarId, int?>? onVariableImported = null)
    {
        var globalToLocal = new Dictionary<string, int>();

        // Note: agentId is unused during deserialisation; empty string is a sentinel matching the Dart source.
        var tempSerializer = new PayloadSerializer("");
        var (term, _) = tempSerializer.DeserializeTermWithMappingV2(
            payload, 0, globalToLocal, allocateImportedVar, onVariableImported);

        var localToGlobal = new Dictionary<int, GlobalVarId>();
        foreach (var kvp in globalToLocal)
            localToGlobal[kvp.Value] = GlobalVarId.Decode(kvp.Key);

        return (term, localToGlobal);
    }

    /// <summary>Deserialize agent message payload with fresh variable allocation.</summary>
    public Term DeserializeAgentMessagePayload(
        byte[] payload,
        Func<bool, int> allocateImportedVar,
        Action<int, bool, GlobalVarId, int?>? onVariableImported = null)
    {
        var varMapping = new Dictionary<string, int>();
        var (term, _) = DeserializeTermWithMappingV2(
            payload, 0, varMapping, allocateImportedVar, onVariableImported);
        return term;
    }

    /// <summary>Deserialize term with variable remapping (V2, isReader-aware).</summary>
    private (Term Term, int BytesConsumed) DeserializeTermWithMappingV2(
        byte[] bytes,
        int offset,
        Dictionary<string, int> varMapping,
        Func<bool, int> allocateImportedVar,
        Action<int, bool, GlobalVarId, int?>? onVariableImported)
    {
        var startOffset = offset;

        if (offset >= bytes.Length)
            throw new FormatException("Unexpected end of input");

        var tag = bytes[offset];
        offset++;

        switch (tag)
        {
            case TagConstant:
            {
                var (value, constSize) = DeserializeConstant(bytes, offset);
                return (new ConstTerm(value), 1 + constSize);
            }

            case TagVariable:
            {
                var (idLength, lengthSize) = DecodeLength(bytes.AsSpan(), offset);
                offset += lengthSize;

                var globalIdStr = Encoding.UTF8.GetString(bytes, offset, idLength);
                offset += idLength;
                var globalId = GlobalVarId.Decode(globalIdStr);

                var isReader = bytes[offset] == 1;
                offset++;

                int? pairedReaderCreatorLocalId = null;
                if (!isReader)
                {
                    var (pairedReaderId, pairedReaderIdSize) = DecodeLength(bytes.AsSpan(), offset);
                    offset += pairedReaderIdSize;
                    pairedReaderCreatorLocalId = pairedReaderId;
                }

                int localVarId;
                if (varMapping.TryGetValue(globalIdStr, out var existing))
                {
                    localVarId = existing;
                }
                else
                {
                    localVarId = allocateImportedVar(isReader);
                    varMapping[globalIdStr] = localVarId;
                    onVariableImported?.Invoke(localVarId, isReader, globalId, pairedReaderCreatorLocalId);
                }

                return (new VarRef(localVarId), offset - startOffset);
            }

            case TagStruct:
            {
                var (functorLength, functorLengthSize) = DecodeLength(bytes.AsSpan(), offset);
                offset += functorLengthSize;

                var functor = Encoding.UTF8.GetString(bytes, offset, functorLength);
                offset += functorLength;

                var (arity, aritySize) = DecodeLength(bytes.AsSpan(), offset);
                offset += aritySize;

                var args = new List<Term>(arity);
                for (int i = 0; i < arity; i++)
                {
                    var (arg, argSize) = DeserializeTermWithMappingV2(
                        bytes, offset, varMapping, allocateImportedVar, onVariableImported);
                    args.Add(arg);
                    offset += argSize;
                }

                return (new StructTerm(functor, args), offset - startOffset);
            }

            default:
                throw new FormatException($"Unknown term tag: {tag}");
        }
    }

    private static (object? Value, int BytesConsumed) DeserializeConstant(byte[] bytes, int offset)
    {
        var startOffset = offset;

        if (offset >= bytes.Length)
            throw new FormatException("Unexpected end of input in constant");

        var typeTag = bytes[offset];
        offset++;

        switch (typeTag)
        {
            case 0: // nil
                return ("nil", offset - startOffset);

            case 1: // int (stored as big-endian int64)
            {
                var value = DecodeInt64(bytes.AsSpan(), offset);
                offset += 8;
                return (value, offset - startOffset);
            }

            case 2: // double (stored as big-endian float64)
            {
                var value = DecodeFloat64(bytes.AsSpan(), offset);
                offset += 8;
                return (value, offset - startOffset);
            }

            case 3: // string
            {
                var (length, lengthSize) = DecodeLength(bytes.AsSpan(), offset);
                offset += lengthSize;
                var value = Encoding.UTF8.GetString(bytes, offset, length);
                offset += length;
                return (value, offset - startOffset);
            }

            case 4: // bool
            {
                var value = bytes[offset] == 1;
                offset++;
                return (value, offset - startOffset);
            }

            default:
                throw new FormatException($"Unknown constant type tag: {typeTag}");
        }
    }

    // ============================================================================
    // Encoding/decoding helpers
    // ============================================================================

    /// <summary>
    /// Variable-length length encoding: 1/2/4-byte tagged-prefix scheme.
    /// NOT LEB128 — preserves Dart wire format exactly.
    /// </summary>
    private static byte[] EncodeLength(int length)
    {
        if (length < 128)
            return new byte[] { (byte)length };
        else if (length < 16384)
            return new byte[] { (byte)(0x80 | (length >> 8)), (byte)(length & 0xFF) };
        else
            return new byte[]
            {
                (byte)(0xC0 | (length >> 24)),
                (byte)((length >> 16) & 0xFF),
                (byte)((length >> 8) & 0xFF),
                (byte)(length & 0xFF)
            };
    }

    /// <summary>Decode variable-length length. Returns (Value, BytesConsumed).</summary>
    private static (int Value, int BytesConsumed) DecodeLength(ReadOnlySpan<byte> bytes, int offset)
    {
        var first = bytes[offset];
        if (first < 0x80)
            return (first, 1);
        else if (first < 0xC0)
            return (((first & 0x3F) << 8) | bytes[offset + 1], 2);
        else
            return (
                ((first & 0x3F) << 24) |
                (bytes[offset + 1] << 16) |
                (bytes[offset + 2] << 8) |
                bytes[offset + 3],
                4);
    }

    /// <summary>Encode int64 as 8 big-endian bytes.</summary>
    private static byte[] EncodeInt64(long value)
    {
        var buf = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(buf, value);
        return buf;
    }

    /// <summary>Decode 8 big-endian bytes to int64.</summary>
    private static long DecodeInt64(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadInt64BigEndian(bytes.Slice(offset, 8));

    /// <summary>Encode double as 8 big-endian bytes.</summary>
    private static byte[] EncodeFloat64(double value)
    {
        var buf = new byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(buf, value);
        return buf;
    }

    /// <summary>Decode 8 big-endian bytes to double.</summary>
    private static double DecodeFloat64(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadDoubleBigEndian(bytes.Slice(offset, 8));
}
