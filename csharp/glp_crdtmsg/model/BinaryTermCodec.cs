// Binary-term surface — the CANONICAL, deterministic form (feature 041-crdtmsg-mvp, T014).
//
// Contract C1 (envelope-header-registry.md) / research R1/R2:
//   TLV-outer / Section-15-term-codec-inner. The container reuses the shipped glp_result_codec
//   ByteWriter/ByteReader (LEB128 varints, length-prefixed UTF-8 strings — the 038 byte conventions);
//   section VALUES are opaque bytes (a section that carries a GLP term was produced with TermCodec —
//   that is the "term-codec-inner"). Decode consumes every byte or throws (FR-005).
//
// Acyclicity: sections are opaque byte blobs, so no cyclic object graph is reachable through the
// envelope — the active CycleGuard (CyclicTermException as a transport fault) lives at op-apply
// (T029, FR-031), not here. Capability slot: NOT carried on the binary surface until envelope v2
// (T048); a non-null slot here is a loud-fail, never a silent drop.

using GlpRuntime.ResultCodec;
using GlpRuntime.CrdtMsg.Envelope;

namespace GlpRuntime.CrdtMsg.Model;

public sealed class BinaryTermCodec : ISurfaceCodec
{
    public string Name => "binary-term";

    public byte[] Encode(Message m)
    {
        var w = new ByteWriter();
        w.WriteByte(VersionPolicy.CodecFormatVersion);   // TIER-2 hard gate
        w.WriteByte(m.PayloadType);                       // wire-registry id
        VarInt.WriteU64(w, (ulong)m.SchemaVersion);       // TIER-1 emit
        w.WriteByte((byte)m.CrdtModel);
        WriteHeader(w, m.Header);
        TlvSection.EncodeSections(w, m.Sections);
        return w.TakeBytes();
    }

    public Message Decode(byte[] bytes)
    {
        var r = new ByteReader(bytes);
        VersionPolicy.CheckCodecFormatVersion(r.ReadByte());
        byte payloadType = r.ReadByte();
        DecodeGuard.CheckPayloadTypeKnown(payloadType);
        int schemaVersion = ReadBoundedInt(r, "schema_version");
        VersionPolicy.CheckSchemaVersionAccepted(schemaVersion);
        CrdtModel crdtModel = DecodeCrdtModel(r.ReadByte());
        Header header = ReadHeader(r);
        var sections = TlvSection.DecodeSections(r);
        if (!r.AtEnd)
            throw new CrdtMsgException($"{r.Length - r.Position} trailing byte(s) after message");
        return new Message(schemaVersion, payloadType, header, sections, crdtModel);
    }

    private static void WriteHeader(ByteWriter w, Header h)
    {
        w.WriteString(h.MsgId);
        w.WriteString(h.From);
        w.WriteString(h.To);
        VarInt.WriteU64(w, (ulong)h.Seq);
        WriteStringList(w, h.Policy.Targets);
        WriteStringList(w, h.Policy.Waypoints);
        WriteStringList(w, h.Policy.Excludes);
        if (h.CapabilitySlot is not null)
            throw new CrdtMsgException(
                "capability slot is not carried on the binary surface until envelope v2 (T048)");
    }

    private static Header ReadHeader(ByteReader r)
    {
        string msgId = r.ReadString();
        string from = r.ReadString();
        string to = r.ReadString();
        long seq = (long)VarInt.ReadU64(r);
        var targets = ReadStringList(r);
        var waypoints = ReadStringList(r);
        var excludes = ReadStringList(r);
        return new Header(msgId, from, to, seq, new RoutingPolicy(targets, waypoints, excludes), null);
    }

    private static void WriteStringList(ByteWriter w, IReadOnlyList<string> xs)
    {
        VarInt.WriteU64(w, (ulong)xs.Count);
        foreach (var x in xs) w.WriteString(x);
    }

    private static List<string> ReadStringList(ByteReader r)
    {
        ulong count = VarInt.ReadU64(r);
        if (count > (ulong)(r.Length - r.Position))
            throw new CrdtMsgException($"string-list count {count} exceeds remaining bytes");
        var xs = new List<string>((int)count);
        for (ulong i = 0; i < count; i++) xs.Add(r.ReadString());
        return xs;
    }

    private static int ReadBoundedInt(ByteReader r, string field)
    {
        ulong v = VarInt.ReadU64(r);
        if (v > int.MaxValue)
            throw new CrdtMsgException($"{field} value {v} exceeds Int32 range");
        return (int)v;
    }

    private static CrdtModel DecodeCrdtModel(byte b) => b switch
    {
        0 => CrdtModel.None,
        1 => CrdtModel.StateBased,
        2 => CrdtModel.OpBased,
        _ => throw new CrdtMsgException($"crdt_model byte 0x{b:X2} not in {{0,1,2}}"),
    };
}
