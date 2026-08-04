// T024 — the 062 hardening refusal taxonomy surfaced as TYPED responses on the
// engine host's IL path (contracts/il-request-kind.md rules 1–2): malformed |
// il_version_mismatch | digest_mismatch | mid_transfer_truncation. After every
// refusal the engine keeps serving; a refused envelope registers nothing; and
// the text/IL session gate refuses mixing LOUDLY — never a silent fallback
// (rule 3).

using GlpRuntime.IlCodec;
using GlpRuntime.ReplClient;

using RcExecutionStatus = GlpRuntime.ResultCodec.ExecutionStatus;

namespace GlpRuntime.SplitProtocol.Tests;

public class RefusalTaxonomyTests : IDisposable
{
    private readonly DispatcherFixture _host = new();
    private ulong _id;

    public void Dispose() => _host.Dispose();

    private ulong Next() => ++_id;

    private static readonly Lazy<byte[]> _goodEnvelope = new(() =>
        IlSession.Create().CompileUnit("procedure taxo.\ntaxo :- true.\n", "taxo_unit"));

    /// <summary>A structurally valid envelope carrying compiled IL (via the client
    /// compiler). Fresh copy per call — several tests corrupt it in place.</summary>
    private static byte[] GoodEnvelope() => (byte[])_goodEnvelope.Value.Clone();

    private async Task<IlRefusal> ExpectRefusal(byte[] envelopeBytes)
    {
        var response = await _host.SendAsync(
            new RequestFrame(Next(), RequestKind.LoadIl, envelopeBytes));
        Assert.Equal(ResponseKind.IlRefused, response.Kind);
        return IlRefusal.Decode(response.Body);
    }

    private async Task AssertStillServingIl()
    {
        // The engine keeps serving on ITS chosen path after a refusal (rule 2):
        // a well-formed LOAD_IL is accepted…
        var ok = await _host.SendAsync(new RequestFrame(Next(), RequestKind.LoadIl, GoodEnvelope()));
        Assert.Equal(ResponseKind.Ack, ok.Kind);
        Assert.Contains("module_ref=taxo_unit", ok.BodyText());
        // …and PING still answers.
        var ping = await _host.SendAsync(RequestFrame.Empty(Next(), RequestKind.Ping));
        Assert.Equal(ResponseKind.Ack, ping.Kind);
    }

    [Fact]
    public async Task DigestMismatch_IsTypedRefusal_AndEngineKeepsServing()
    {
        var envelope = GoodEnvelope();
        envelope[^1] ^= 0xFF; // corrupt the compiled form → digest check must fire
        var refusal = await ExpectRefusal(envelope);
        Assert.Equal(IlRefusalCode.DigestMismatch, refusal.Code);
        await AssertStillServingIl();
    }

    [Fact]
    public async Task IlVersionMismatch_IsTypedRefusal_AndEngineKeepsServing()
    {
        // Re-wrap a good compiled form under an incompatible major version.
        var decoded = CompiledIlEnvelopeCodec.Decode(GoodEnvelope());
        var wrong = CompiledIlEnvelopeCodec.Encode(decoded.CompiledForm, "taxo_v", ilVersion: "9.0.0");
        var refusal = await ExpectRefusal(wrong);
        Assert.Equal(IlRefusalCode.IlVersionMismatch, refusal.Code);
        await AssertStillServingIl();
    }

    [Fact]
    public async Task MidTransferTruncation_IsTypedRefusal_AndEngineKeepsServing()
    {
        var envelope = GoodEnvelope();
        var truncated = envelope.AsSpan(0, envelope.Length - 7).ToArray();
        var refusal = await ExpectRefusal(truncated);
        Assert.Equal(IlRefusalCode.MidTransferTruncation, refusal.Code);
        await AssertStillServingIl();
    }

    [Fact]
    public async Task Malformed_IsTypedRefusal_AndEngineKeepsServing()
    {
        // A wrong envelope-frame version byte: coherent length, incoherent frame.
        var envelope = GoodEnvelope();
        envelope[0] = 0x7E;
        var refusal = await ExpectRefusal(envelope);
        Assert.Equal(IlRefusalCode.Malformed, refusal.Code);
        await AssertStillServingIl();
    }

    [Fact]
    public async Task RefusedEnvelope_RegistersNothing()
    {
        var envelope = GoodEnvelope();
        envelope[^1] ^= 0xFF;
        await ExpectRefusal(envelope);

        // The refused module's entry must NOT be runnable afterwards.
        var run = await _host.SendAsync(new RequestFrame(Next(), RequestKind.RunGoalIl,
            new RunGoalIlBody("taxo/0", null).Encode()));
        Assert.Equal(ResponseKind.Result, run.Kind);
        var env = GlpRuntime.ResultCodec.ResultEnvelopeCodec.Decode(run.Body);
        Assert.Equal(RcExecutionStatus.Failed, env.Status);
        Assert.Contains("not found", env.Error);
    }

    [Fact]
    public async Task InlineEnvelopeRefusals_UseTheSameTaxonomy()
    {
        var envelope = GoodEnvelope();
        envelope[^1] ^= 0xFF;
        var run = await _host.SendAsync(new RequestFrame(Next(), RequestKind.RunGoalIl,
            new RunGoalIlBody("taxo/0", envelope).Encode()));
        Assert.Equal(ResponseKind.IlRefused, run.Kind);
        Assert.Equal(IlRefusalCode.DigestMismatch, IlRefusal.Decode(run.Body).Code);
    }

    [Fact]
    public async Task NonNullaryGoalRef_IsALoudProtocolError()
    {
        await _host.SendAsync(new RequestFrame(Next(), RequestKind.LoadIl, GoodEnvelope()));
        var run = await _host.SendAsync(new RequestFrame(Next(), RequestKind.RunGoalIl,
            new RunGoalIlBody("taxo/2", null).Encode()));
        Assert.Equal(ResponseKind.ProtocolError, run.Kind);
        Assert.Contains("NULLARY", run.BodyText());
    }

    // ---- the never-mixed session gate (rule 3) ----

    [Fact]
    public async Task TextSession_RefusesIlKinds_NoSilentFallback()
    {
        var load = await _host.SendAsync(
            RequestFrame.Text(Next(), RequestKind.LoadSource, "procedure taxo.\ntaxo :- true.\n"));
        Assert.Equal(ResponseKind.Ack, load.Kind);

        var mixed = await _host.SendAsync(
            new RequestFrame(Next(), RequestKind.LoadIl, GoodEnvelope()));
        Assert.Equal(ResponseKind.ProtocolError, mixed.Kind);
        Assert.Contains("never", mixed.BodyText());

        // The text session keeps serving text.
        var goal = await _host.SendAsync(RequestFrame.Text(Next(), RequestKind.RunGoal, "taxo"));
        Assert.Equal(ResponseKind.Result, goal.Kind);
    }

    [Fact]
    public async Task IlSession_RefusesTextKinds_NoSilentFallback()
    {
        var load = await _host.SendAsync(
            new RequestFrame(Next(), RequestKind.LoadIl, GoodEnvelope()));
        Assert.Equal(ResponseKind.Ack, load.Kind);

        var mixedLoad = await _host.SendAsync(
            RequestFrame.Text(Next(), RequestKind.LoadSource, "p(a).\n"));
        Assert.Equal(ResponseKind.ProtocolError, mixedLoad.Kind);

        var mixedGoal = await _host.SendAsync(
            RequestFrame.Text(Next(), RequestKind.RunGoal, "taxo"));
        Assert.Equal(ResponseKind.ProtocolError, mixedGoal.Kind);

        // The IL session keeps serving IL; STATUS surfaces the path.
        var run = await _host.SendAsync(new RequestFrame(Next(), RequestKind.RunGoalIl,
            new RunGoalIlBody("taxo/0", null).Encode()));
        Assert.Equal(ResponseKind.Result, run.Kind);
        var status = await _host.SendAsync(RequestFrame.Empty(Next(), RequestKind.Status));
        Assert.Contains("session_path=il", status.BodyText());
    }
}
