// Macaroon wire codec — the capability-slot byte format (feature 050 US3, T025/T026).
//
// Contract capability-gating.md: the static macaroon rides in the envelope capability slot
// (CapabilitySlot section 0x20, additive-optional v2). This codec defines the slot's byte layout
// using the shipped 038 byte conventions (ByteWriter/ByteReader: LEB128 varints, length-prefixed
// UTF-8 strings — the same container conventions as BinaryTermCodec). Decode is
// consume-all-or-throw (loud-fail, FR-007 discipline): truncation, trailing bytes, or a garbled
// field is a CrdtMsgException, never a silent partial macaroon.

using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.CrdtMsg.Cap;

/// <summary>Serializes a <see cref="Macaroon"/> to/from the capability-slot bytes (section <c>0x20</c>).</summary>
public static class MacaroonCodec
{
    public static byte[] Encode(Macaroon m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var w = new ByteWriter();
        w.WriteString(m.Location);
        w.WriteString(m.Identifier);
        VarInt.WriteU64(w, (ulong)m.Caveats.Count);
        foreach (var c in m.Caveats)
        {
            w.WriteString(c.Key);
            w.WriteString(c.Op);
            w.WriteString(c.Value);
        }
        VarInt.WriteU64(w, (ulong)m.Signature.Length);
        w.WriteBytes(m.Signature);
        return w.TakeBytes();
    }

    /// <summary>Decode the slot bytes; consume-all-or-throw. The signature is carried verbatim
    /// (<see cref="Macaroon.FromWire"/>) so <see cref="Macaroon.Verify"/> catches tampering.</summary>
    public static Macaroon Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        try
        {
            var r = new ByteReader(bytes);
            string location = r.ReadString();
            string identifier = r.ReadString();

            ulong caveatCount = VarInt.ReadU64(r);
            if (caveatCount > (ulong)(r.Length - r.Position))
                throw new CrdtMsgException($"macaroon caveat count {caveatCount} exceeds remaining bytes");
            var caveats = new List<Caveat>((int)caveatCount);
            for (ulong i = 0; i < caveatCount; i++)
                caveats.Add(new Caveat(r.ReadString(), r.ReadString(), r.ReadString()));

            ulong sigLen = VarInt.ReadU64(r);
            if (sigLen > (ulong)(r.Length - r.Position))
                throw new CrdtMsgException($"macaroon signature length {sigLen} exceeds remaining bytes");
            byte[] signature = r.ReadBytes((int)sigLen);

            if (!r.AtEnd)
                throw new CrdtMsgException($"{r.Length - r.Position} trailing byte(s) after macaroon");
            return Macaroon.FromWire(location, identifier, caveats, signature);
        }
        catch (Exception ex) when (ex is not CrdtMsgException)
        {
            // ByteReader truncation/format faults surface as the one loud-fail decode exception class.
            throw new CrdtMsgException($"malformed macaroon capability: {ex.Message}");
        }
    }
}
