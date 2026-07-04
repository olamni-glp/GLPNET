// Result-envelope frame codec — feature 038-result-codec-and-framecodec-ride.
//
// Wires the 2-byte frame header (version 0x01 + payloadType 0x11 RESULT_ENVELOPE)
// and the section framing (status, bindings, varToWriter, suspended, captured, error)
// over the term sub-codec. Mirrors the 029 PayloadHeader discipline (029 IL programs
// are payloadType 0x10; the envelope takes the next value 0x11) so envelopes and IL
// programs are never confusable on a shared wire (R3).
//
// Decode MUST consume every byte or fail loudly (FR-005, SC-004): bad version, bad
// payloadType, bad status, bad errorPresent, truncation, trailing bytes, unknown term
// tag, or an over-64-bit varint all raise ResultCodecException. Byte-for-byte mirror
// of glp_runtime/lib/codec/result_envelope_codec.dart.

using System.Collections.Generic;

namespace GlpRuntime.ResultCodec;

public static class ResultEnvelopeCodec
{
    /// <summary>Codec format version (matches shipped 029 PayloadHeader.Version).</summary>
    public const byte EnvelopeVersion = 0x01;

    /// <summary>
    /// Payload-type discriminant for a result envelope (029 IL program = 0x10; next = 0x11).
    /// Single-sourced from <c>glp_wire_registry</c> (SC-010, feature 041 T006) — this is a
    /// const alias of the ONE table entry, not an independent literal. The byte value is
    /// unchanged, so 038 wire byte-parity is preserved.
    /// </summary>
    public const byte PayloadTypeResultEnvelope = GlpRuntime.WireRegistry.PayloadType.ResultEnvelope;

    public static byte[] Encode(ResultEnvelope env)
    {
        var w = new ByteWriter();

        w.WriteByte(EnvelopeVersion);
        w.WriteByte(PayloadTypeResultEnvelope);
        w.WriteByte((byte)env.Status);

        // resolvedBindings — canonical (declaration) order
        w.WriteVarUInt(env.ResolvedBindings.Count);
        foreach (var kv in env.ResolvedBindings)
        {
            w.WriteString(kv.Key);
            TermCodec.EncodeTerm(w, kv.Value);
        }

        // varToWriter — canonical (declaration) order
        w.WriteVarUInt(env.VarToWriter.Count);
        foreach (var kv in env.VarToWriter)
        {
            w.WriteString(kv.Key);
            TermCodec.EncodeGlobalVarId(w, kv.Value);
        }

        // suspended — canonical order, deduped by goal_id at build time
        w.WriteVarUInt(env.Suspended.Count);
        foreach (var id in env.Suspended)
            TermCodec.EncodeGlobalVarId(w, id);

        // captured — carried but excluded from the byte-parity criterion (R4)
        w.WriteVarUInt(env.Captured.Length);
        w.WriteBytes(env.Captured);

        // error — present iff status==Failed
        if (env.Error != null)
        {
            w.WriteByte(0x01);
            w.WriteString(env.Error);
        }
        else
        {
            w.WriteByte(0x00);
        }

        return w.TakeBytes();
    }

    public static ResultEnvelope Decode(byte[] bytes)
    {
        var r = new ByteReader(bytes);

        byte version = r.ReadByte();
        if (version != EnvelopeVersion)
            throw new ResultCodecException(
                $"Unsupported envelope version 0x{version:X2} (expected 0x{EnvelopeVersion:X2})");

        byte payloadType = r.ReadByte();
        if (payloadType != PayloadTypeResultEnvelope)
            throw new ResultCodecException(
                $"Unexpected payload type 0x{payloadType:X2} " +
                $"(expected RESULT_ENVELOPE 0x{PayloadTypeResultEnvelope:X2})");

        var status = StatusFromWireByte(r.ReadByte());

        int bindingsCount = r.ReadVarUInt();
        var resolvedBindings = new List<KeyValuePair<string, Term>>();
        for (int i = 0; i < bindingsCount; i++)
        {
            string name = r.ReadString();
            Term term = TermCodec.DecodeTerm(r);
            resolvedBindings.Add(new KeyValuePair<string, Term>(name, term));
        }

        int varToWriterCount = r.ReadVarUInt();
        var varToWriter = new List<KeyValuePair<string, GlobalVarId>>();
        for (int i = 0; i < varToWriterCount; i++)
        {
            string name = r.ReadString();
            GlobalVarId id = TermCodec.DecodeGlobalVarId(r);
            varToWriter.Add(new KeyValuePair<string, GlobalVarId>(name, id));
        }

        int suspendedCount = r.ReadVarUInt();
        var suspended = new List<GlobalVarId>();
        for (int i = 0; i < suspendedCount; i++)
            suspended.Add(TermCodec.DecodeGlobalVarId(r));

        int capturedLen = r.ReadVarUInt();
        byte[] captured = r.ReadBytes(capturedLen);

        byte errorPresent = r.ReadByte();
        string? error = null;
        if (errorPresent == 0x01)
        {
            error = r.ReadString();
        }
        else if (errorPresent != 0x00)
        {
            throw new ResultCodecException(
                $"Corrupt envelope: errorPresent flag 0x{errorPresent:X2} (expected 0x00 or 0x01)");
        }

        if (!r.AtEnd)
            throw new ResultCodecException(
                $"Corrupt envelope: {r.Length - r.Position} unconsumed trailing byte(s) after envelope");

        return new ResultEnvelope(
            status,
            resolvedBindings,
            varToWriter,
            suspended,
            captured,
            error);
    }

    private static ExecutionStatus StatusFromWireByte(byte b) => b switch
    {
        0x00 => ExecutionStatus.Success,
        0x01 => ExecutionStatus.Suspended,
        0x02 => ExecutionStatus.Failed,
        _ => throw new ResultCodecException(
            $"Corrupt envelope: status byte 0x{b:X2} not in {{0x00,0x01,0x02}}"),
    };
}
