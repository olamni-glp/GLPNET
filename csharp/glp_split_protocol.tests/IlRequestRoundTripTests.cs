// T024 — wire-level round-trip of the 064 US3 IL request kinds
// (contracts/il-request-kind.md): LOAD_IL / RUN_GOAL_IL frames, the
// RunGoalIlBody sub-codec, the IlRefusal typed-error body, and the loud-fail
// discipline on every malformed shape (wire rule 3 / 038 convention).

using System.Text;

using GlpRuntime.IlCodec;

namespace GlpRuntime.SplitProtocol.Tests;

public class IlRequestRoundTripTests
{
    private static byte[] SampleEnvelope(string metadata = "wire/sample") =>
        CompiledIlEnvelopeCodec.Encode(new byte[] { 0x01, 0x02, 0x03, 0x04 }, metadata);

    // ---- LOAD_IL ----

    [Fact]
    public void LoadIl_Frame_RoundTrips_EnvelopeBytesVerbatim()
    {
        var envelope = SampleEnvelope();
        var request = new RequestFrame(7, RequestKind.LoadIl, envelope);

        var frame = RequestResponseCodec.EncodeRequestFrame(request, messageId: 1);
        var decoded = RequestResponseCodec.DecodeRequestFrame(frame);

        Assert.Equal(7UL, decoded.RequestId);
        Assert.Equal(RequestKind.LoadIl, decoded.Kind);
        Assert.Equal(envelope, decoded.Body); // the 062 envelope rides UNCHANGED (rule 1)
        // …and the carried bytes still verify as the shipped envelope codec's output.
        var reparsed = CompiledIlEnvelopeCodec.Decode(decoded.Body);
        Assert.Equal("wire/sample", reparsed.SourceMetadata);
    }

    [Fact]
    public void LoadIl_EmptyBody_IsRefusedLoudly()
    {
        var payload = RequestResponseCodec.EncodeRequestPayload(
            new RequestFrame(1, RequestKind.LoadSource, Array.Empty<byte>()));
        payload[9] = (byte)RequestKind.LoadIl; // splice the kind onto an empty body
        var ex = Assert.Throws<SplitProtocolException>(
            () => RequestResponseCodec.DecodeRequestPayload(payload));
        Assert.Contains("non-empty body", ex.Message);
    }

    // ---- RUN_GOAL_IL ----

    [Fact]
    public void RunGoalIl_Body_RoundTrips_WithoutInlineEnvelope()
    {
        var body = new RunGoalIlBody("main/0", null);
        var decoded = RunGoalIlBody.Decode(body.Encode());
        Assert.Equal("main/0", decoded.GoalRef);
        Assert.Null(decoded.InlineEnvelope);
    }

    [Fact]
    public void RunGoalIl_Body_RoundTrips_WithInlineEnvelope()
    {
        var envelope = SampleEnvelope("wire/one-shot");
        var body = new RunGoalIlBody("__goal__/0", envelope);
        var decoded = RunGoalIlBody.Decode(body.Encode());
        Assert.Equal("__goal__/0", decoded.GoalRef);
        Assert.Equal(envelope, decoded.InlineEnvelope);
    }

    [Fact]
    public void RunGoalIl_Frame_RoundTrips_EndToEnd()
    {
        var body = new RunGoalIlBody("run_case/0", SampleEnvelope());
        var request = new RequestFrame(42, RequestKind.RunGoalIl, body.Encode());
        var frame = RequestResponseCodec.EncodeRequestFrame(request, messageId: 9);
        var decoded = RequestResponseCodec.DecodeRequestFrame(frame);
        Assert.Equal(RequestKind.RunGoalIl, decoded.Kind);
        var d = RunGoalIlBody.Decode(decoded.Body);
        Assert.Equal(body.GoalRef, d.GoalRef);
        Assert.Equal(body.InlineEnvelope, d.InlineEnvelope);
    }

    [Fact]
    public void RunGoalIl_EmptyGoalRef_IsRefusedLoudly()
    {
        Assert.Throws<SplitProtocolException>(() => new RunGoalIlBody("", null).Encode());
        // …and on the decode side too (a zero goal_ref_len forged on the wire).
        var forged = new byte[8]; // goal_ref_len=0, envelope_len=0
        Assert.Throws<SplitProtocolException>(() => RunGoalIlBody.Decode(forged));
    }

    [Fact]
    public void RunGoalIl_TruncatedBody_IsRefusedLoudly()
    {
        var whole = new RunGoalIlBody("run_case/0", SampleEnvelope()).Encode();
        var ex = Assert.Throws<SplitProtocolException>(
            () => RunGoalIlBody.Decode(whole.AsSpan(0, whole.Length - 3)));
        Assert.Contains("truncated", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RunGoalIl_TrailingBytes_AreRefusedLoudly()
    {
        var whole = new RunGoalIlBody("run_case/0", null).Encode();
        var padded = whole.Concat(new byte[] { 0xFF }).ToArray();
        var ex = Assert.Throws<SplitProtocolException>(() => RunGoalIlBody.Decode(padded));
        Assert.Contains("trailing", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- IL_REFUSED response ----

    [Theory]
    [InlineData(IlRefusalCode.Malformed)]
    [InlineData(IlRefusalCode.IlVersionMismatch)]
    [InlineData(IlRefusalCode.DigestMismatch)]
    [InlineData(IlRefusalCode.MidTransferTruncation)]
    public void IlRefusal_RoundTrips_EveryTaxonomyCode(IlRefusalCode code)
    {
        var refusal = new IlRefusal(code, "reason text (062 taxonomy)");
        var response = new ResponseFrame(3, ResponseKind.IlRefused, refusal.Encode());
        var frame = RequestResponseCodec.EncodeResponseFrame(response, messageId: 2);
        var decoded = RequestResponseCodec.DecodeResponseFrame(frame);
        Assert.Equal(ResponseKind.IlRefused, decoded.Kind);
        var back = IlRefusal.Decode(decoded.Body);
        Assert.Equal(code, back.Code);
        Assert.Equal("reason text (062 taxonomy)", back.Reason);
    }

    [Fact]
    public void IlRefusal_UnknownCode_IsRefusedLoudly()
    {
        var body = new byte[] { 0x7F }.Concat(Encoding.UTF8.GetBytes("x")).ToArray();
        Assert.Throws<SplitProtocolException>(() => IlRefusal.Decode(body));
        Assert.Throws<SplitProtocolException>(() => IlRefusal.Decode(Array.Empty<byte>()));
    }

    // ---- kind-range guards keep failing loudly past the new kinds ----

    [Fact]
    public void UnknownKindsPastTheNewOnes_StayLoud()
    {
        var payload = RequestResponseCodec.EncodeRequestPayload(
            new RequestFrame(1, RequestKind.RunGoalIl, new byte[] { 1 }));
        payload[9] = 0x09; // one past RUN_GOAL_IL
        Assert.Throws<SplitProtocolException>(
            () => RequestResponseCodec.DecodeRequestPayload(payload));

        var response = RequestResponseCodec.EncodeResponsePayload(
            new ResponseFrame(1, ResponseKind.IlRefused, new byte[] { 0x01 }));
        response[9] = 0x87; // one past IL_REFUSED
        Assert.Throws<SplitProtocolException>(
            () => RequestResponseCodec.DecodeResponsePayload(response));
    }

    // ---- text kinds keep working during the deprecation window (rule 3) ----

    [Fact]
    public void TextKinds_StillRoundTrip()
    {
        var load = RequestFrame.Text(1, RequestKind.LoadSource, "p(a).");
        var decodedLoad = RequestResponseCodec.DecodeRequestFrame(
            RequestResponseCodec.EncodeRequestFrame(load, 1));
        Assert.Equal(RequestKind.LoadSource, decodedLoad.Kind);
        Assert.Equal("p(a).", decodedLoad.BodyText());

        var goal = RequestFrame.Text(2, RequestKind.RunGoal, "p(X)");
        var decodedGoal = RequestResponseCodec.DecodeRequestFrame(
            RequestResponseCodec.EncodeRequestFrame(goal, 2));
        Assert.Equal(RequestKind.RunGoal, decodedGoal.Kind);
        Assert.Equal("p(X)", decodedGoal.BodyText());
    }
}
