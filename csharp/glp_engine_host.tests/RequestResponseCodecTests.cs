// T008 — round-trip + loud-fail tests for the split-protocol frame codec
// (contracts/wire-protocol.md rules 2/3; 038 trailing-byte convention).

using GlpRuntime.Link.Reliability;
using GlpRuntime.SplitProtocol;

namespace GlpRuntime.EngineHost.Tests;

public class RequestResponseCodecTests
{
    // ---- round-trips ----

    [Theory]
    [InlineData(RequestKind.LoadSource, "p(X) :- q(X?).")]
    [InlineData(RequestKind.RunGoal, "append([1], [2], Xs).")]
    public void RequestTextBody_RoundTrips(RequestKind kind, string body)
    {
        var request = RequestFrame.Text(42UL, kind, body);
        var decoded = RequestResponseCodec.DecodeRequestPayload(
            RequestResponseCodec.EncodeRequestPayload(request));

        Assert.Equal(42UL, decoded.RequestId);
        Assert.Equal(kind, decoded.Kind);
        Assert.Equal(body, decoded.BodyText());
    }

    [Theory]
    [InlineData(RequestKind.Snapshot)]
    [InlineData(RequestKind.Status)]
    [InlineData(RequestKind.Shutdown)]
    [InlineData(RequestKind.Ping)]
    public void RequestEmptyBody_RoundTrips(RequestKind kind)
    {
        var request = RequestFrame.Empty(ulong.MaxValue, kind);
        var decoded = RequestResponseCodec.DecodeRequestPayload(
            RequestResponseCodec.EncodeRequestPayload(request));

        Assert.Equal(ulong.MaxValue, decoded.RequestId);
        Assert.Equal(kind, decoded.Kind);
        Assert.Empty(decoded.Body);
    }

    [Theory]
    [InlineData(ResponseKind.Result)]
    [InlineData(ResponseKind.Ack)]
    [InlineData(ResponseKind.Deferred)]
    [InlineData(ResponseKind.ProtocolError)]
    [InlineData(ResponseKind.EngineBusy)]
    public void Response_RoundTrips(ResponseKind kind)
    {
        var body = new byte[] { 0x11, 0x00, 0xFF, 0x7E };
        var response = new ResponseFrame(7UL, kind, body);
        var decoded = RequestResponseCodec.DecodeResponsePayload(
            RequestResponseCodec.EncodeResponsePayload(response));

        Assert.Equal(7UL, decoded.RequestId);
        Assert.Equal(kind, decoded.Kind);
        Assert.Equal(body, decoded.Body);
    }

    [Fact]
    public void FrameLevel_RoundTrips_ThroughFrameCodec()
    {
        var request = RequestFrame.Text(9UL, RequestKind.RunGoal, "foo(Bar).");
        var frame = RequestResponseCodec.EncodeRequestFrame(request, messageId: 3);
        var decoded = RequestResponseCodec.DecodeRequestFrame(frame);
        Assert.Equal(request.RequestId, decoded.RequestId);
        Assert.Equal(request.Kind, decoded.Kind);
        Assert.Equal(request.BodyText(), decoded.BodyText());

        var response = ResponseFrame.Text(9UL, ResponseKind.Ack, "serving");
        var rframe = RequestResponseCodec.EncodeResponseFrame(response, messageId: 4);
        var rdecoded = RequestResponseCodec.DecodeResponseFrame(rframe);
        Assert.Equal(9UL, rdecoded.RequestId);
        Assert.Equal(ResponseKind.Ack, rdecoded.Kind);
        Assert.Equal("serving", rdecoded.BodyText());
    }

    [Fact]
    public void RequestId_EchoConvention_PreservesFullU64Range()
    {
        foreach (ulong id in new[] { 0UL, 1UL, uint.MaxValue + 1UL, ulong.MaxValue })
        {
            var decoded = RequestResponseCodec.DecodeRequestPayload(
                RequestResponseCodec.EncodeRequestPayload(RequestFrame.Empty(id, RequestKind.Ping)));
            Assert.Equal(id, decoded.RequestId);
        }
    }

    // ---- loud fails (wire rule 3) ----

    [Fact]
    public void UnknownPayloadType_LoudFails()
    {
        var payload = RequestResponseCodec.EncodeRequestPayload(
            RequestFrame.Empty(1UL, RequestKind.Ping));
        payload[0] = 0x7F;
        Assert.Throws<SplitProtocolException>(
            () => RequestResponseCodec.DecodeRequestPayload(payload));
    }

    [Fact]
    public void UnknownRequestKind_LoudFails()
    {
        var payload = RequestResponseCodec.EncodeRequestPayload(
            RequestFrame.Empty(1UL, RequestKind.Ping));
        payload[9] = 0x3B;
        Assert.Throws<SplitProtocolException>(
            () => RequestResponseCodec.DecodeRequestPayload(payload));
    }

    [Fact]
    public void UnknownResponseKind_LoudFails()
    {
        var payload = RequestResponseCodec.EncodeResponsePayload(
            ResponseFrame.Empty(1UL, ResponseKind.Ack));
        payload[9] = 0x01; // a REQUEST kind byte arriving as a response kind
        Assert.Throws<SplitProtocolException>(
            () => RequestResponseCodec.DecodeResponsePayload(payload));
    }

    [Fact]
    public void RequestDecodedAsResponse_LoudFails()
    {
        var payload = RequestResponseCodec.EncodeRequestPayload(
            RequestFrame.Empty(1UL, RequestKind.Status));
        Assert.Throws<SplitProtocolException>(
            () => RequestResponseCodec.DecodeResponsePayload(payload));
    }

    [Fact]
    public void TrailingBytes_LoudFail()
    {
        var payload = RequestResponseCodec.EncodeRequestPayload(
            RequestFrame.Text(1UL, RequestKind.RunGoal, "g."));
        var padded = payload.Concat(new byte[] { 0x00 }).ToArray();
        Assert.Throws<SplitProtocolException>(
            () => RequestResponseCodec.DecodeRequestPayload(padded));
    }

    [Fact]
    public void TruncatedBody_LoudFails()
    {
        var payload = RequestResponseCodec.EncodeRequestPayload(
            RequestFrame.Text(1UL, RequestKind.LoadSource, "some program text"));
        var truncated = payload.AsSpan(0, payload.Length - 3).ToArray();
        Assert.Throws<SplitProtocolException>(
            () => RequestResponseCodec.DecodeRequestPayload(truncated));
    }

    [Fact]
    public void ShortHeader_LoudFails()
    {
        Assert.Throws<SplitProtocolException>(
            () => RequestResponseCodec.DecodeRequestPayload(new byte[] { 0x40, 0x00 }));
    }

    [Fact]
    public void CorruptFrameCodecFrame_LoudFails()
    {
        var frame = RequestResponseCodec.EncodeRequestFrame(
            RequestFrame.Empty(1UL, RequestKind.Ping), messageId: 1);
        frame[^1] ^= 0xFF; // flip a chunk byte → CRC mismatch inside FrameCodec
        Assert.Throws<SplitProtocolException>(
            () => RequestResponseCodec.DecodeRequestFrame(frame));
    }

    [Fact]
    public void FragmentedFrame_LoudFails_WholeFramesOnly()
    {
        var payload = RequestResponseCodec.EncodeRequestPayload(
            RequestFrame.Text(1UL, RequestKind.LoadSource, new string('x', 256)));
        var frames = FrameCodec.Encode(payload, messageId: 1, maxFrameBytes: 128);
        Assert.True(frames.Count > 1);
        Assert.Throws<SplitProtocolException>(
            () => RequestResponseCodec.DecodeRequestFrame(frames[0]));
    }
}
