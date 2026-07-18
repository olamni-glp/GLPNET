namespace GlpRuntime.Link.Seam;

/// <summary>
/// A per-link <see cref="IPayloadCodec"/> loud-failed on an outbound (or inbound) term — e.g. a
/// ground term that is not <c>crdtmsg/7</c> handed to the <c>"quic"</c> link's codec (feature 050,
/// FR-005/FR-009). Egress converts whatever the injected codec throws into this seam type so
/// <c>glp_link</c> stays codec-agnostic: the concrete codec exception (e.g. <c>CrdtMsgException</c>,
/// which lives BELOW the seam in <c>glp_crdtmsg</c>) never needs to be visible here.
/// </summary>
/// <remarks>
/// Extends <see cref="InvalidOperationException"/> deliberately: the egress controlled-failure path
/// (the <c>Out</c>-stream drainer drops the frame; <c>'_link_send'</c> Aborts) already catches
/// <see cref="InvalidOperationException"/> for the ground-relay gate, so a codec rejection flows
/// through that SAME controlled channel — a bad frame is dropped/aborted, never an uncaught crash of
/// the runner thread — while callers can still catch this subtype first to distinguish "codec rejected
/// the term" from "non-ground term reached the wire".
/// </remarks>
public sealed class PayloadCodecException : InvalidOperationException
{
    public PayloadCodecException(string message) : base(message) { }

    public PayloadCodecException(string message, Exception inner) : base(message, inner) { }
}
