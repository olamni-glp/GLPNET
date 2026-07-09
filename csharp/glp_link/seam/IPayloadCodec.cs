using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Seam;

/// <summary>
/// Host-side, below-GLP per-link payload codec (feature 050, FR-005/6/7): encodes the ground message
/// tree the program ships into application-payload bytes, and decodes inbound bytes back to a ground
/// tree. The 025 reliability sublayer (<c>FrameCodec</c> length+CRC+seq, reassembly, ordering) wraps
/// the codec's bytes UNCHANGED, so the codec is purely the L5 application-payload surface (SC-002
/// read at L5, analyze note A1). The transport stays term-agnostic; the concrete codec is selected
/// per link at establishment — loopback/tcp keep the default ground-relay blob
/// (<see cref="DefaultPayloadCodec"/>), the <c>"quic"</c> link gets the 041 crdtmsg envelope
/// (<c>CrdtMsgPayloadCodec</c>, injected at the composition root — research D-1).
/// </summary>
public interface IPayloadCodec
{
    /// <summary>Encode a VarRef-free ground message tree into application-payload bytes.</summary>
    byte[] Encode(Term ground);

    /// <summary>Decode application-payload bytes back into a ground message tree; loud-fail on malformed input.</summary>
    Term Decode(byte[] payload);
}
