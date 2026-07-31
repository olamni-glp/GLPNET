// WireProtocol — REQUEST/RESPONSE payload types + kind bytes for the 061
// client↔engine split (T006; contracts/wire-protocol.md).
//
// These two payload-type discriminants are NEW wire kinds carried inside
// FrameCodec chunks; they change no existing FrameCodec payload type. The
// canonical registry (glp_wire_registry.PayloadType) owns 0x10–0x12 and
// reserves 0x12+ for messaging kinds; the split-protocol bytes sit in a
// disjoint 0x40 block so neither table can collide. glp_split_protocol
// deliberately does NOT reference glp_wire_registry (T001: refs glp_link
// only — the client must stay thin, R7); folding these rows into the
// registry is possible later without a byte change.

namespace GlpRuntime.SplitProtocol;

/// <summary>Loud-fail exception for any malformed/unknown split-protocol frame (wire rule 3).</summary>
public sealed class SplitProtocolException : Exception
{
    public SplitProtocolException(string message) : base(message) { }
}

/// <summary>Payload-type discriminants for the split protocol (leading byte of the FrameCodec chunk).</summary>
public static class SplitPayloadType
{
    /// <summary>A client→engine request frame.</summary>
    public const byte Request = 0x40;

    /// <summary>An engine→client response frame.</summary>
    public const byte Response = 0x41;
}

/// <summary>Request kinds (contracts/wire-protocol.md — client→engine).</summary>
public enum RequestKind : byte
{
    /// <summary>UTF-8 program source; engine runs the full pipeline (FR-001).</summary>
    LoadSource = 0x01,

    /// <summary>UTF-8 goal text; one goal per request (MVP).</summary>
    RunGoal = 0x02,

    /// <summary>On-demand snapshot trigger (FR-014); empty body.</summary>
    Snapshot = 0x03,

    /// <summary>Engine + restore/pending-snapshot state; empty body.</summary>
    Status = 0x04,

    /// <summary>Graceful shutdown: final snapshot then exit 0; empty body.</summary>
    Shutdown = 0x05,

    /// <summary>Supervisor liveness probe; empty body (wire rule 7).</summary>
    Ping = 0x06,
}

/// <summary>Response kinds (contracts/wire-protocol.md — engine→client).</summary>
public enum ResponseKind : byte
{
    /// <summary>Body = 038 ResultEnvelope bytes (ground-only subset, engine-pre-rendered bindings).</summary>
    Result = 0x81,

    /// <summary>Body = UTF-8 status string + optional trailing seq (ACK for SNAPSHOT/SHUTDOWN/PING/STATUS).</summary>
    Ack = 0x82,

    /// <summary>Snapshot parked pending quiescence (wire rule 5); empty body.</summary>
    Deferred = 0x83,

    /// <summary>Body = UTF-8 reason; engine keeps serving (FR-006, wire rule 3).</summary>
    ProtocolError = 0x84,

    /// <summary>Restore in progress — only STATUS/PING served (wire rule 4); empty body.</summary>
    EngineBusy = 0x85,
}

/// <summary>One client→engine request: id (echoed), kind, body bytes.</summary>
/// <param name="RequestId">Client-monotonic uint64; echoed verbatim in the response.</param>
/// <param name="Kind">The request kind byte.</param>
/// <param name="Body">Kind-specific body (UTF-8 text for LOAD_SOURCE/RUN_GOAL, else empty).</param>
public sealed record RequestFrame(ulong RequestId, RequestKind Kind, byte[] Body)
{
    /// <summary>Convenience: a request with a UTF-8 text body.</summary>
    public static RequestFrame Text(ulong requestId, RequestKind kind, string body) =>
        new(requestId, kind, System.Text.Encoding.UTF8.GetBytes(body));

    /// <summary>Convenience: a body-less request (SNAPSHOT/STATUS/SHUTDOWN/PING).</summary>
    public static RequestFrame Empty(ulong requestId, RequestKind kind) =>
        new(requestId, kind, Array.Empty<byte>());

    /// <summary>The body decoded as UTF-8 text.</summary>
    public string BodyText() => System.Text.Encoding.UTF8.GetString(Body);
}

/// <summary>One engine→client response: echoed id, kind, body bytes.</summary>
/// <param name="RequestId">Echo of the request's id (wire rule 2).</param>
/// <param name="Kind">The response kind byte.</param>
/// <param name="Body">Kind-specific body (envelope bytes for RESULT, UTF-8 text for ACK/PROTOCOL_ERROR).</param>
public sealed record ResponseFrame(ulong RequestId, ResponseKind Kind, byte[] Body)
{
    /// <summary>Convenience: a response with a UTF-8 text body.</summary>
    public static ResponseFrame Text(ulong requestId, ResponseKind kind, string body) =>
        new(requestId, kind, System.Text.Encoding.UTF8.GetBytes(body));

    /// <summary>Convenience: a body-less response (DEFERRED/ENGINE_BUSY).</summary>
    public static ResponseFrame Empty(ulong requestId, ResponseKind kind) =>
        new(requestId, kind, Array.Empty<byte>());

    /// <summary>The body decoded as UTF-8 text.</summary>
    public string BodyText() => System.Text.Encoding.UTF8.GetString(Body);
}
