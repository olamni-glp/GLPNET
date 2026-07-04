// Loud-fail exception for the messaging layer (feature 041-crdtmsg-mvp).
//
// Contract C3 (envelope-header-registry.md) / FR-005: every decoder consumes all bytes or throws —
// bad version, unknown payload_type, unknown must-understand tag, truncation, trailing bytes all
// reject. No partial/silent acceptance (SC-002 = 0% silent acceptance). This is the single loud-fail
// type all four surfaces raise (mirrors the ResultCodecException discipline).

namespace GlpRuntime.CrdtMsg.Envelope;

/// <summary>Raised on any malformed / out-of-policy message. Never swallowed; never a silent drop.</summary>
public sealed class CrdtMsgException : Exception
{
    public CrdtMsgException(string message) : base(message) { }
    public CrdtMsgException(string message, Exception inner) : base(message, inner) { }
}
