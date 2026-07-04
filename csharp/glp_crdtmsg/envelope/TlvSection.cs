// TLV section codec + criticality (feature 041-crdtmsg-mvp, T013).
//
// Contract C2 (envelope-header-registry.md) / data-model §3:
//   Each section = LEB128 type_number, LEB128 length, `length` opaque value bytes. The type_number's
//   numeric RANGE decides criticality: unknown+ignorable → skip-by-length (carried verbatim);
//   unknown+must-understand → loud-fail (enforced by DecodeGuard, T018). Greasing sections MUST be
//   emitted each run to keep the skip path exercised (FR-006).
//
// Criticality convention (chosen, documented): **odd type_number = must-understand, even = ignorable.**
// A single-bit rule (like the TLS GREASE / RFC "mandatory bit" idiom) is trivially testable and leaves
// dense space for both classes. type_number values are allocated in glp_wire_registry for known kinds.

using GlpRuntime.ResultCodec;
using GlpRuntime.CrdtMsg.Model;

namespace GlpRuntime.CrdtMsg.Envelope;

public static class TlvSection
{
    /// <summary>True if an unknown section of this type must be understood (odd type_number).</summary>
    public static bool IsMustUnderstand(long typeNumber) => (typeNumber & 1L) == 1L;

    /// <summary>A reserved ignorable (even) greasing type — emitted each run to exercise skip-by-length.</summary>
    public const long GreaseTypeNumber = 0x2A; // 42, even ⇒ ignorable

    /// <summary>Build the standard greasing section (ignorable, unknown-by-design, carried verbatim).</summary>
    public static Section Grease() => new(GreaseTypeNumber, new byte[] { 0xDE, 0xAD });

    /// <summary>Encode the ordered section list: count, then (type_number, length, value) per section.</summary>
    public static void EncodeSections(ByteWriter w, IReadOnlyList<Section> sections)
    {
        VarInt.WriteU64(w, (ulong)sections.Count);
        foreach (var s in sections)
        {
            if (s.TypeNumber < 0)
                throw new CrdtMsgException($"Invalid negative section type_number {s.TypeNumber}");
            VarInt.WriteU64(w, (ulong)s.TypeNumber);
            VarInt.WriteU64(w, (ulong)s.Value.Length);
            w.WriteBytes(s.Value);
        }
    }

    /// <summary>Decode the section list. Unknown sections are read by length and carried verbatim (skip).</summary>
    public static List<Section> DecodeSections(ByteReader r)
    {
        ulong count = VarInt.ReadU64(r);
        var sections = new List<Section>((int)Math.Min(count, 1024));
        for (ulong i = 0; i < count; i++)
        {
            long typeNumber = checked((long)VarInt.ReadU64(r));
            ulong len = VarInt.ReadU64(r);
            if (len > (ulong)(r.Length - r.Position))
                throw new CrdtMsgException(
                    $"Truncated section: declared length {len} exceeds {r.Length - r.Position} remaining bytes");
            byte[] value = r.ReadBytes((int)len);
            sections.Add(new Section(typeNumber, value));
        }
        return sections;
    }
}
