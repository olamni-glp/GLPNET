// CBOR surface codec (feature 041-crdtmsg-mvp, T017).
//
// Contract C1 / research R2: in-box System.Formats.Cbor, deterministic write (fixed field order,
// definite-length maps). Section values + capability slot are native CBOR byte strings (no base64 on
// this surface — still lossless, and canonicalizes to the same binary form). Unknown map keys are
// skipped (additive-optional forward tolerance). Decode is loud-fail: malformed CBOR throws, and any
// trailing bytes after the top-level value are rejected (BytesRemaining != 0), plus ToModel() re-checks.

using System.Formats.Cbor;
using GlpRuntime.CrdtMsg.Envelope;

namespace GlpRuntime.CrdtMsg.Model;

public sealed class CborCodec : ISurfaceCodec
{
    public string Name => "cbor";

    // Lax mode: we guarantee determinism ourselves via fixed field order + definite lengths, and stay
    // tolerant of key order / unknown keys on read (forward tolerance). Full canonical key-sorting is a
    // trivial future tightening that does not change the model round-trip.
    private const CborConformanceMode Mode = CborConformanceMode.Lax;

    public byte[] Encode(Message m)
    {
        var w = new CborWriter(Mode, convertIndefiniteLengthEncodings: true);
        w.WriteStartMap(5);
        w.WriteTextString("schema_version"); w.WriteInt32(m.SchemaVersion);
        w.WriteTextString("payload_type"); w.WriteInt32(m.PayloadType);
        w.WriteTextString("crdt_model"); w.WriteInt32((int)m.CrdtModel);
        w.WriteTextString("header"); WriteHeader(w, m.Header);
        w.WriteTextString("sections"); WriteSections(w, m.Sections);
        w.WriteEndMap();
        return w.Encode();
    }

    public Message Decode(byte[] bytes)
    {
        try
        {
            var r = new CborReader(bytes, Mode);
            var dto = ReadMessage(r);
            if (r.BytesRemaining != 0)
                throw new CrdtMsgException($"{r.BytesRemaining} trailing byte(s) after CBOR message");
            return dto.ToModel();
        }
        catch (CborContentException e) { throw new CrdtMsgException($"malformed CBOR: {e.Message}", e); }
        catch (InvalidOperationException e) { throw new CrdtMsgException($"malformed CBOR: {e.Message}", e); }
    }

    private static void WriteHeader(CborWriter w, Header h)
    {
        int n = 5 + (h.CapabilitySlot is null ? 0 : 1);
        w.WriteStartMap(n);
        w.WriteTextString("msg_id"); w.WriteTextString(h.MsgId);
        w.WriteTextString("from"); w.WriteTextString(h.From);
        w.WriteTextString("to"); w.WriteTextString(h.To);
        w.WriteTextString("seq"); w.WriteInt64(h.Seq);
        w.WriteTextString("policy"); WritePolicy(w, h.Policy);
        if (h.CapabilitySlot is not null)
        {
            w.WriteTextString("capability_slot"); w.WriteByteString(h.CapabilitySlot);
        }
        w.WriteEndMap();
    }

    private static void WritePolicy(CborWriter w, RoutingPolicy p)
    {
        w.WriteStartMap(3);
        w.WriteTextString("targets"); WriteStringArray(w, p.Targets);
        w.WriteTextString("waypoints"); WriteStringArray(w, p.Waypoints);
        w.WriteTextString("excludes"); WriteStringArray(w, p.Excludes);
        w.WriteEndMap();
    }

    private static void WriteStringArray(CborWriter w, IReadOnlyList<string> xs)
    {
        w.WriteStartArray(xs.Count);
        foreach (var x in xs) w.WriteTextString(x);
        w.WriteEndArray();
    }

    private static void WriteSections(CborWriter w, IReadOnlyList<Section> sections)
    {
        w.WriteStartArray(sections.Count);
        foreach (var s in sections)
        {
            w.WriteStartMap(2);
            w.WriteTextString("type_number"); w.WriteInt64(s.TypeNumber);
            w.WriteTextString("value"); w.WriteByteString(s.Value);
            w.WriteEndMap();
        }
        w.WriteEndArray();
    }

    // --- readers (key-dispatched; unknown keys skipped) ---

    private static MessageDto ReadMessage(CborReader r)
    {
        var dto = new MessageDto();
        int? n = r.ReadStartMap();
        for (int i = 0; NotAtMapEnd(r, n, i); i++)
        {
            switch (r.ReadTextString())
            {
                case "schema_version": dto.SchemaVersion = ReadInt(r); break;
                case "payload_type": dto.PayloadType = ReadInt(r); break;
                case "crdt_model": dto.CrdtModel = ReadInt(r); break;
                case "header": dto.Header = ReadHeader(r); break;
                case "sections": dto.Sections = ReadSections(r); break;
                default: r.SkipValue(); break;
            }
        }
        r.ReadEndMap();
        return dto;
    }

    private static HeaderDto ReadHeader(CborReader r)
    {
        var h = new HeaderDto();
        int? n = r.ReadStartMap();
        for (int i = 0; NotAtMapEnd(r, n, i); i++)
        {
            switch (r.ReadTextString())
            {
                case "msg_id": h.MsgId = r.ReadTextString(); break;
                case "from": h.From = r.ReadTextString(); break;
                case "to": h.To = r.ReadTextString(); break;
                case "seq": h.Seq = r.ReadInt64(); break;
                case "policy": h.Policy = ReadPolicy(r); break;
                case "capability_slot": h.CapabilitySlot = Convert.ToBase64String(r.ReadByteString()); break;
                default: r.SkipValue(); break;
            }
        }
        r.ReadEndMap();
        return h;
    }

    private static PolicyDto ReadPolicy(CborReader r)
    {
        var p = new PolicyDto();
        int? n = r.ReadStartMap();
        for (int i = 0; NotAtMapEnd(r, n, i); i++)
        {
            switch (r.ReadTextString())
            {
                case "targets": p.Targets = ReadStringArray(r); break;
                case "waypoints": p.Waypoints = ReadStringArray(r); break;
                case "excludes": p.Excludes = ReadStringArray(r); break;
                default: r.SkipValue(); break;
            }
        }
        r.ReadEndMap();
        return p;
    }

    private static List<SectionDto> ReadSections(CborReader r)
    {
        var list = new List<SectionDto>();
        int? n = r.ReadStartArray();
        for (int i = 0; NotAtArrayEnd(r, n, i); i++)
        {
            var s = new SectionDto();
            int? m = r.ReadStartMap();
            for (int j = 0; NotAtMapEnd(r, m, j); j++)
            {
                switch (r.ReadTextString())
                {
                    case "type_number": s.TypeNumber = r.ReadInt64(); break;
                    case "value": s.Value = Convert.ToBase64String(r.ReadByteString()); break;
                    default: r.SkipValue(); break;
                }
            }
            r.ReadEndMap();
            list.Add(s);
        }
        r.ReadEndArray();
        return list;
    }

    private static List<string> ReadStringArray(CborReader r)
    {
        var list = new List<string>();
        int? n = r.ReadStartArray();
        for (int i = 0; NotAtArrayEnd(r, n, i); i++) list.Add(r.ReadTextString());
        r.ReadEndArray();
        return list;
    }

    private static int ReadInt(CborReader r)
    {
        long v = r.ReadInt64();
        if (v is < int.MinValue or > int.MaxValue)
            throw new CrdtMsgException($"CBOR integer {v} exceeds Int32 range");
        return (int)v;
    }

    private static bool NotAtMapEnd(CborReader r, int? definiteCount, int i) =>
        definiteCount.HasValue ? i < definiteCount.Value : r.PeekState() != CborReaderState.EndMap;

    private static bool NotAtArrayEnd(CborReader r, int? definiteCount, int i) =>
        definiteCount.HasValue ? i < definiteCount.Value : r.PeekState() != CborReaderState.EndArray;
}
