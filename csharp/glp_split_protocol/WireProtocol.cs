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
    /// <summary>UTF-8 program source; engine runs the full pipeline (FR-001).
    /// Text kind — remains valid during the 064 IL deprecation window
    /// (contracts/il-request-kind.md rule 3).</summary>
    LoadSource = 0x01,

    /// <summary>UTF-8 goal text; one goal per request (MVP). Text kind — remains
    /// valid during the 064 IL deprecation window.</summary>
    RunGoal = 0x02,

    /// <summary>On-demand snapshot trigger (FR-014); empty body.</summary>
    Snapshot = 0x03,

    /// <summary>Engine + restore/pending-snapshot state; empty body.</summary>
    Status = 0x04,

    /// <summary>Graceful shutdown: final snapshot then exit 0; empty body.</summary>
    Shutdown = 0x05,

    /// <summary>Supervisor liveness probe; empty body (wire rule 7).</summary>
    Ping = 0x06,

    /// <summary>
    /// 064 US3 (contracts/il-request-kind.md): body = the shipped 062
    /// CompiledIlEnvelope bytes, OPAQUE at this layer (glp_split_protocol does
    /// not reference glp_il_codec — the engine host decodes/verifies). Response:
    /// ACK "loaded module_ref=…" or a typed <see cref="ResponseKind.IlRefused"/>.
    /// A client chooses text or IL per session, never mixed per module (rule 3).
    /// </summary>
    LoadIl = 0x07,

    /// <summary>
    /// 064 US3: body = <see cref="RunGoalIlBody"/> — a goal_ref label
    /// ("functor/0", nullary — the 062 execute-on-B precedent) plus an optional
    /// inline CompiledIlEnvelope for one-shot goals. Response: the existing 038
    /// result envelope (RESULT) or a typed <see cref="ResponseKind.IlRefused"/>.
    /// </summary>
    RunGoalIl = 0x08,
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

    /// <summary>
    /// 064 US3 typed refusal for LOAD_IL/RUN_GOAL_IL (contracts/il-request-kind.md
    /// rules 1–2): body = <see cref="IlRefusal"/> — one hardening-taxonomy code
    /// (malformed | il_version_mismatch | digest_mismatch | mid_transfer_truncation)
    /// plus a UTF-8 reason. The engine keeps serving; it NEVER falls back silently
    /// to the text path.
    /// </summary>
    IlRefused = 0x86,
}

/// <summary>
/// The 062 receiver-hardening refusal taxonomy, verbatim (contracts/il-request-kind.md
/// rule 1). Wire-stable byte values — extend only by appending.
/// </summary>
public enum IlRefusalCode : byte
{
    /// <summary>Envelope/frame bytes that decode to nothing coherent.</summary>
    Malformed = 0x01,

    /// <summary>il_version incompatible with the engine's accepted major.</summary>
    IlVersionMismatch = 0x02,

    /// <summary>Integrity digest does not match the compiled form.</summary>
    DigestMismatch = 0x03,

    /// <summary>Frame shorter than its declared lengths (truncated mid-transfer).</summary>
    MidTransferTruncation = 0x04,
}

/// <summary>
/// Body of a <see cref="ResponseKind.IlRefused"/> response:
/// <c>[code u8][reason UTF-8 … to end]</c>. Loud-fail decode (wire rule 3).
/// </summary>
public sealed record IlRefusal(IlRefusalCode Code, string Reason)
{
    public byte[] Encode()
    {
        var reason = System.Text.Encoding.UTF8.GetBytes(Reason);
        var body = new byte[1 + reason.Length];
        body[0] = (byte)Code;
        reason.CopyTo(body, 1);
        return body;
    }

    public static IlRefusal Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < 1)
            throw new SplitProtocolException("IL_REFUSED body is empty (expected [code][reason])");
        byte code = body[0];
        if (code is < (byte)IlRefusalCode.Malformed or > (byte)IlRefusalCode.MidTransferTruncation)
            throw new SplitProtocolException($"unknown IL refusal code 0x{code:X2}");
        return new IlRefusal((IlRefusalCode)code,
            System.Text.Encoding.UTF8.GetString(body.Slice(1)));
    }
}

/// <summary>
/// Body of a <see cref="RequestKind.RunGoalIl"/> request:
/// <c>[goal_ref_len u32 BE][goal_ref UTF-8][envelope_len u32 BE][envelope bytes]</c>.
/// <c>envelope_len == 0</c> means no inline envelope (run against session-loaded IL
/// modules only). Trailing bytes are refused loudly (038 convention).
/// </summary>
public sealed record RunGoalIlBody(string GoalRef, byte[]? InlineEnvelope)
{
    public byte[] Encode()
    {
        if (string.IsNullOrEmpty(GoalRef))
            throw new SplitProtocolException("RUN_GOAL_IL goal_ref must be non-empty");
        var goalBytes = System.Text.Encoding.UTF8.GetBytes(GoalRef);
        var envelope = InlineEnvelope ?? Array.Empty<byte>();
        var body = new byte[4 + goalBytes.Length + 4 + envelope.Length];
        var s = body.AsSpan();
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(s, (uint)goalBytes.Length);
        goalBytes.CopyTo(s.Slice(4));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
            s.Slice(4 + goalBytes.Length), (uint)envelope.Length);
        envelope.CopyTo(s.Slice(8 + goalBytes.Length));
        return body;
    }

    public static RunGoalIlBody Decode(ReadOnlySpan<byte> body)
    {
        if (body.Length < 8)
            throw new SplitProtocolException(
                $"RUN_GOAL_IL body {body.Length} bytes is shorter than the 8-byte minimum");
        uint goalLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(body);
        if (goalLen == 0)
            throw new SplitProtocolException("RUN_GOAL_IL goal_ref must be non-empty");
        if (body.Length < 4 + (long)goalLen + 4)
            throw new SplitProtocolException(
                $"RUN_GOAL_IL body {body.Length} bytes shorter than goal_ref_len {goalLen} + headers");
        var goalRef = System.Text.Encoding.UTF8.GetString(body.Slice(4, (int)goalLen));
        uint envLen = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(
            body.Slice(4 + (int)goalLen));
        long expected = 4 + (long)goalLen + 4 + envLen;
        if (body.Length < expected)
            throw new SplitProtocolException(
                $"RUN_GOAL_IL body {body.Length} bytes shorter than declared {expected} (truncated)");
        if (body.Length > expected)
            throw new SplitProtocolException(
                $"RUN_GOAL_IL trailing bytes: body {body.Length} > declared {expected} (038 convention)");
        byte[]? envelope = envLen == 0 ? null : body.Slice(8 + (int)goalLen, (int)envLen).ToArray();
        return new RunGoalIlBody(goalRef, envelope);
    }
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
