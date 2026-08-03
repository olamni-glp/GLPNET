// T022/T024 — the client-side compile+ship half (IlSession): goal_ref shaping
// for bare nullary goals, one-shot inline compilation for goals with
// arguments/conjunctions, and the end-to-end inline happy path against the
// engine host's IL execute path.

using GlpRuntime.IlCodec;
using GlpRuntime.ReplClient;
using GlpRuntime.ResultCodec;

using RcExecutionStatus = GlpRuntime.ResultCodec.ExecutionStatus;

namespace GlpRuntime.SplitProtocol.Tests;

public class IlSessionTests
{
    private static readonly Lazy<IlSession> Client = new(IlSession.Create);

    [Fact]
    public void BareNullaryGoal_ShipsAsGoalRef_NoInlineEnvelope()
    {
        var body = Client.Value.GoalBody("run_case.");
        Assert.Equal("run_case/0", body.GoalRef);
        Assert.Null(body.InlineEnvelope);
    }

    [Fact]
    public void GoalWithArguments_CompilesIntoAOneShotInlineEnvelope()
    {
        var body = Client.Value.GoalBody("test_constant(foo, R), out_term(R?).");
        Assert.Equal(IlSession.OneShotGoalRef, body.GoalRef);
        Assert.NotNull(body.InlineEnvelope);
        // The inline bytes are a verifiable 062 envelope whose program carries
        // the wrapper's nullary entry label.
        var (decoded, envelope) = CompiledIlEnvelopeCodec.Unwrap(body.InlineEnvelope!);
        Assert.Equal("__goal__", envelope.SourceMetadata);
        Assert.Contains("__goal__/0", decoded.Program.Labels.Keys);
    }

    [Fact]
    public async Task OneShotInlineGoal_RunsAgainstLoadedIlModules_EndToEnd()
    {
        using var host = new DispatcherFixture();
        ulong id = 0;

        var prelude = await host.SendAsync(new RequestFrame(
            ++id, RequestKind.LoadIl, Client.Value.PreludeEnvelope()));
        Assert.Equal(ResponseKind.Ack, prelude.Kind);

        // The same shape the interactive IL client ships for `load`:
        var unit =
            "-mode(system).\n" +
            "exported procedure test_constant(Constant?, Constant).\n" +
            "test_constant(X, X?) :- constant(X?) | true.\n" +
            "procedure out_term(_?).\n" +
            "out_term(T) :- ground(T?) | '_output'(T?).\n";
        var load = await host.SendAsync(new RequestFrame(
            ++id, RequestKind.LoadIl, Client.Value.CompileUnit(unit, "il_session_unit")));
        Assert.Equal(ResponseKind.Ack, load.Kind);

        // …and the same shape it ships for a goal with arguments:
        var body = Client.Value.GoalBody("test_constant(foo, R), out_term(R?).");
        var run = await host.SendAsync(new RequestFrame(
            ++id, RequestKind.RunGoalIl, body.Encode()));
        Assert.Equal(ResponseKind.Result, run.Kind);

        var envelope = ResultEnvelopeCodec.Decode(run.Body);
        Assert.Equal(RcExecutionStatus.Success, envelope.Status);
        Assert.Contains("foo", System.Text.Encoding.UTF8.GetString(envelope.Captured));
        Assert.Empty(envelope.ResolvedBindings);
    }

    [Fact]
    public void GoalThatDoesNotCompile_FailsLoudly_ClientSide()
    {
        // SRSW violation in the one-shot wrapper — the CLIENT's compiler rejects
        // it before anything touches the wire (no silent text fallback).
        Assert.ThrowsAny<Exception>(() => Client.Value.GoalBody("p(X?, X?)."));
    }
}
