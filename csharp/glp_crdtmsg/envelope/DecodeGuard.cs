// Loud-fail decode invariant (feature 041-crdtmsg-mvp, T018).
//
// Contract C3 (envelope-header-registry.md) / FR-005 / SC-002:
//   The cross-surface guard applied after each surface Decode. It enforces (1) the payload_type is a
//   registered kind, and (2) every MUST-UNDERSTAND section whose type the receiver does not understand
//   is rejected — never silently kept. Unknown IGNORABLE sections pass through (carried verbatim by the
//   TLV codec). Per-surface "consume all bytes" is enforced inside each codec (binary: r.AtEnd; JSON:
//   no trailing tokens; CBOR: BytesRemaining==0); this guard adds the model-level policy checks.

using GlpRuntime.CrdtMsg.Model;
using WireReg = GlpRuntime.WireRegistry.WireRegistry;

namespace GlpRuntime.CrdtMsg.Envelope;

public static class DecodeGuard
{
    /// <summary>Reject a message whose payload_type is not a registered wire kind (data-model §1).</summary>
    public static void CheckPayloadTypeKnown(byte payloadType)
    {
        if (!WireReg.IsKnown(payloadType))
            throw new CrdtMsgException($"Unknown payload_type 0x{payloadType:X2} (not in wire registry)");
    }

    /// <summary>
    /// Reject a message carrying a must-understand section this receiver does not understand (FR-005).
    /// <paramref name="understoodTypes"/> is the set of section type_numbers this receiver handles.
    /// </summary>
    public static void CheckMustUnderstand(Message message, IReadOnlySet<long> understoodTypes)
    {
        foreach (var s in message.Sections)
        {
            if (TlvSection.IsMustUnderstand(s.TypeNumber) && !understoodTypes.Contains(s.TypeNumber))
                throw new CrdtMsgException(
                    $"Unknown must-understand section type_number {s.TypeNumber} — loud-fail (FR-005)");
        }
    }

    /// <summary>Convenience: full model-level guard for a decoded message.</summary>
    public static void Check(Message message, IReadOnlySet<long> understoodTypes)
    {
        CheckPayloadTypeKnown(message.PayloadType);
        CheckMustUnderstand(message, understoodTypes);
    }
}
