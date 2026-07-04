// v2 additive capability slot + old-reader skip-by-length (feature 041-crdtmsg-mvp, T048).
//
// Contract C4/C5 / FR-011 / SC-008: the capability slot is envelope-v2 ADDITIVE-OPTIONAL. It rides as a
// reserved IGNORABLE (even type_number) TLV section, so a v1 reader — which carries every unknown
// ignorable section verbatim (skip-by-length) — processes all its known fields unimpeded and simply
// preserves the slot. A v2 reader recognises the type and extracts it. This reuses the TLV criticality
// mechanism (T013): additive evolution needs no new framing.

using GlpRuntime.CrdtMsg.Model;

namespace GlpRuntime.CrdtMsg.Headers;

public static class CapabilitySlot
{
    /// <summary>Reserved section type for the capability slot. Even ⇒ ignorable ⇒ a v1 reader skips it.</summary>
    public const long SectionType = 0x20;

    /// <summary>Attach a capability slot to a message (envelope v2). Returns a new message.</summary>
    public static Message Attach(Message m, byte[] capability)
    {
        var sections = new List<Section>(m.Sections) { new(SectionType, capability) };
        return m with { SchemaVersion = 2, Sections = sections };
    }

    /// <summary>A v2 reader extracts the capability slot bytes (null if absent).</summary>
    public static byte[]? Extract(Message m) =>
        m.Sections.FirstOrDefault(s => s.TypeNumber == SectionType)?.Value;

    public static bool Present(Message m) => m.Sections.Any(s => s.TypeNumber == SectionType);
}
