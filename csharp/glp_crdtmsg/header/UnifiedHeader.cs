// Unified, router-opaque header (feature 041-crdtmsg-mvp, T047).
//
// Contract C4 / FR-009/010: the header = {msg_id, from, to, seq, policy, capability_slot}. A router reads
// ONLY the routing view (from/to/seq/policy) to forward, and forwards the header+payload bytes VERBATIM —
// it never decodes or re-encodes the payload sections (payload-opacity). The CRDT op rides as a reserved
// MUST-UNDERSTAND section, extracted as raw bytes (never re-encoded) by the router.

using GlpRuntime.CrdtMsg.Model;

namespace GlpRuntime.CrdtMsg.Headers;

/// <summary>The fields a router reads to forward a message (nothing from the opaque payload).</summary>
public sealed record RouterView(string MsgId, string From, string To, long Seq, RoutingPolicy Policy);

public static class UnifiedHeader
{
    /// <summary>Reserved section type carrying a CRDT op. Odd ⇒ must-understand (a receiver must apply it).</summary>
    public const long OpSectionType = 0x13;

    public static RouterView Route(Message m) =>
        new(m.Header.MsgId, m.Header.From, m.Header.To, m.Header.Seq, m.Header.Policy);

    /// <summary>The raw op-section bytes, forwarded VERBATIM (the router never decodes the op).</summary>
    public static byte[]? OpBytes(Message m) =>
        m.Sections.FirstOrDefault(s => s.TypeNumber == OpSectionType)?.Value;
}
