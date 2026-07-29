// RequestResponseCodec — encode/decode of split-protocol request/response
// payloads over the shipped FrameCodec (T007; contracts/wire-protocol.md).
//
// Payload layout inside one FrameCodec chunk (big-endian, matching the
// FrameCodec/serializer house style):
//
//   [0]       payload_type  (SplitPayloadType.Request / .Response)
//   [1..9)    request_id    u64 BE
//   [9]       kind          u8
//   [10..14)  body_len      u32 BE
//   [14..)    body          body_len bytes
//
// Loud-fail discipline (wire rule 3 / 038 convention): unknown payload type,
// unknown kind, body length overrun, and trailing bytes all throw
// SplitProtocolException — never a silent mis-decode.

using System.Buffers.Binary;

using GlpRuntime.Link.Reliability;

namespace GlpRuntime.SplitProtocol;

public static class RequestResponseCodec
{
    private const int HeaderLen = 14; // type(1) + request_id(8) + kind(1) + body_len(4)

    // ---- payload level (the FrameCodec chunk bytes) ----

    /// <summary>Encode a request into split-protocol payload bytes.</summary>
    public static byte[] EncodeRequestPayload(RequestFrame request) =>
        EncodePayload(SplitPayloadType.Request, request.RequestId, (byte)request.Kind, request.Body);

    /// <summary>Encode a response into split-protocol payload bytes.</summary>
    public static byte[] EncodeResponsePayload(ResponseFrame response) =>
        EncodePayload(SplitPayloadType.Response, response.RequestId, (byte)response.Kind, response.Body);

    /// <summary>Decode payload bytes that must be a request (engine side).</summary>
    public static RequestFrame DecodeRequestPayload(ReadOnlySpan<byte> payload)
    {
        var (payloadType, requestId, kind, body) = DecodePayload(payload);
        if (payloadType != SplitPayloadType.Request)
            throw new SplitProtocolException(
                $"expected REQUEST payload type 0x{SplitPayloadType.Request:X2}, got 0x{payloadType:X2}");
        if (kind is < (byte)RequestKind.LoadSource or > (byte)RequestKind.Ping)
            throw new SplitProtocolException($"unknown request kind 0x{kind:X2}");
        return new RequestFrame(requestId, (RequestKind)kind, body);
    }

    /// <summary>Decode payload bytes that must be a response (client side).</summary>
    public static ResponseFrame DecodeResponsePayload(ReadOnlySpan<byte> payload)
    {
        var (payloadType, requestId, kind, body) = DecodePayload(payload);
        if (payloadType != SplitPayloadType.Response)
            throw new SplitProtocolException(
                $"expected RESPONSE payload type 0x{SplitPayloadType.Response:X2}, got 0x{payloadType:X2}");
        if (kind is < (byte)ResponseKind.Result or > (byte)ResponseKind.EngineBusy)
            throw new SplitProtocolException($"unknown response kind 0x{kind:X2}");
        return new ResponseFrame(requestId, (ResponseKind)kind, body);
    }

    // ---- frame level (whole FrameCodec frames, as carried on the wire) ----

    /// <summary>
    /// Encode a request as ONE whole FrameCodec frame. Loopback MVP never
    /// fragments (no MTU); payloads above FrameCodec.MaxPayloadBytes loud-fail
    /// inside FrameCodec.
    /// </summary>
    public static byte[] EncodeRequestFrame(RequestFrame request, uint messageId) =>
        FrameCodec.Encode(EncodeRequestPayload(request), messageId)[0];

    /// <summary>Encode a response as ONE whole FrameCodec frame.</summary>
    public static byte[] EncodeResponseFrame(ResponseFrame response, uint messageId) =>
        FrameCodec.Encode(EncodeResponsePayload(response), messageId)[0];

    /// <summary>Parse a FrameCodec frame and decode its chunk as a request.</summary>
    public static RequestFrame DecodeRequestFrame(ReadOnlySpan<byte> frame) =>
        DecodeRequestPayload(WholeChunk(frame));

    /// <summary>Parse a FrameCodec frame and decode its chunk as a response.</summary>
    public static ResponseFrame DecodeResponseFrame(ReadOnlySpan<byte> frame) =>
        DecodeResponsePayload(WholeChunk(frame));

    // ---- internals ----

    private static byte[] WholeChunk(ReadOnlySpan<byte> frame)
    {
        ParsedFrame parsed;
        try
        {
            parsed = FrameCodec.ParseFrame(frame);
        }
        catch (FrameException ex)
        {
            throw new SplitProtocolException($"bad FrameCodec frame: {ex.Message}");
        }
        if (parsed.Kind != FrameKind.Whole)
            throw new SplitProtocolException(
                "split protocol carries whole frames only (loopback MVP never fragments)");
        return parsed.Chunk;
    }

    private static byte[] EncodePayload(byte payloadType, ulong requestId, byte kind, byte[] body)
    {
        var payload = new byte[HeaderLen + body.Length];
        var s = payload.AsSpan();
        s[0] = payloadType;
        BinaryPrimitives.WriteUInt64BigEndian(s.Slice(1), requestId);
        s[9] = kind;
        BinaryPrimitives.WriteUInt32BigEndian(s.Slice(10), (uint)body.Length);
        body.CopyTo(s.Slice(HeaderLen));
        return payload;
    }

    private static (byte PayloadType, ulong RequestId, byte Kind, byte[] Body) DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < HeaderLen)
            throw new SplitProtocolException(
                $"payload {payload.Length} bytes is shorter than the {HeaderLen}-byte header");

        byte payloadType = payload[0];
        if (payloadType is not (SplitPayloadType.Request or SplitPayloadType.Response))
            throw new SplitProtocolException($"unknown split payload type 0x{payloadType:X2}");

        ulong requestId = BinaryPrimitives.ReadUInt64BigEndian(payload.Slice(1));
        byte kind = payload[9];
        uint bodyLen = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(10));

        long expected = HeaderLen + (long)bodyLen;
        if (payload.Length < expected)
            throw new SplitProtocolException(
                $"payload {payload.Length} bytes shorter than header+body {expected}");
        if (payload.Length > expected)
            throw new SplitProtocolException(
                $"trailing bytes: payload {payload.Length} > header+body {expected} (038 convention)");

        return (payloadType, requestId, kind, payload.Slice(HeaderLen, (int)bodyLen).ToArray());
    }
}
