// T018 — multi-client serve tests (064 US2): three concurrent clients issuing
// interleaved goals with per-client reply isolation (US2 AS-2/SC-002), a
// disconnecting client leaving the others served (US2 AS-1), a mid-goal client
// crash discarding its pending replies without wedging the merge loop (spec
// edge case), and the legacy single-client mode still refusing the second
// client loudly (061 FR-002 preserved — mode defaults to legacy).

using System.Buffers.Binary;
using System.Net.Sockets;

using GlpRuntime.ReplClient;
using GlpRuntime.ResultCodec;
using GlpRuntime.SplitProtocol;

using RcExecutionStatus = GlpRuntime.ResultCodec.ExecutionStatus;

namespace GlpRuntime.EngineHost.Tests;

public class MultiClientServeTests
{
    private const string CounterSource =
        "procedure double_it(Integer?, Integer).\n" +
        "double_it(X, Y?) :- Y := X? * 2.\n";

    private static async Task LoadCounterAsync(ClientChannel client)
    {
        var response = await client.RoundTripAsync(
            RequestFrame.Text(client.NextRequestId(), RequestKind.LoadSource, CounterSource));
        Assert.Equal(ResponseKind.Ack, response.Kind);
    }

    /// <summary>Run double_it(input, Y) and assert THIS client's own doubled answer came back.</summary>
    private static async Task AssertDoubles(ClientChannel client, int input)
    {
        var response = await client.RoundTripAsync(
            RequestFrame.Text(client.NextRequestId(), RequestKind.RunGoal, $"double_it({input}, Y)"));
        Assert.Equal(ResponseKind.Result, response.Kind);
        var envelope = ResultEnvelopeCodec.Decode(response.Body);
        Assert.Equal(RcExecutionStatus.Success, envelope.Status);
        var binding = Assert.Single(envelope.ResolvedBindings);
        Assert.Equal("Y", binding.Key);
        Assert.Equal((input * 2).ToString(),
            Assert.IsType<ConstString>(Assert.IsType<ConstTerm>(binding.Value).Value).Value);
    }

    [Fact]
    public async Task ThreeClients_InterleavedGoals_RepliesIsolatedPerClient()
    {
        await using var host = new EngineHostFixture(EngineServeMode.MultiClient);
        await using var c1 = await host.ConnectAsync();
        await using var c2 = await host.ConnectAsync();
        await using var c3 = await host.ConnectAsync();

        // One engine, one loaded program (the sessions share the engine — US1
        // AS-3 semantics carry over); load once from the first client.
        await LoadCounterAsync(c1);

        // Interleave: each client drives its own goal batch concurrently with
        // DISTINCT inputs, so a cross-delivered reply would surface as a wrong
        // value (per-client request ids overlap — values are the isolation
        // oracle; the request-id echo check in RoundTripAsync guards ordering).
        async Task Drive(ClientChannel client, int baseValue)
        {
            for (int i = 0; i < 5; i++)
                await AssertDoubles(client, baseValue + i);
        }
        await Task.WhenAll(Drive(c1, 100), Drive(c2, 200), Drive(c3, 300));
    }

    [Fact]
    public async Task DisconnectOneClient_OthersKeepBeingServed()
    {
        await using var host = new EngineHostFixture(EngineServeMode.MultiClient);
        await using var c1 = await host.ConnectAsync();
        var c2 = await host.ConnectAsync();
        await using var c3 = await host.ConnectAsync();

        await LoadCounterAsync(c1);
        await AssertDoubles(c1, 1);
        await AssertDoubles(c2, 2);
        await AssertDoubles(c3, 3);

        // c2 leaves; its session closes — the others are undisturbed (US2 AS-1).
        await c2.DisposeAsync();

        await AssertDoubles(c1, 4);
        await AssertDoubles(c3, 5);
    }

    [Fact]
    public async Task ClientCrashMidGoal_PendingReplyDiscarded_ServeLoopSurvives()
    {
        await using var host = new EngineHostFixture(EngineServeMode.MultiClient);
        await using var survivor = await host.ConnectAsync();
        await LoadCounterAsync(survivor);

        // A raw client that fires a goal and crashes (abortive RST close)
        // without ever reading its reply: the reply must be discarded by the
        // closing session — never wedge the merge loop (spec edge case).
        using (var crasher = new TcpClient())
        {
            await crasher.ConnectAsync("127.0.0.1", host.Port);
            var frame = RequestResponseCodec.EncodeRequestFrame(
                RequestFrame.Text(1UL, RequestKind.RunGoal, "double_it(7, Y)"), 1U);
            var header = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(header, frame.Length);
            var stream = crasher.GetStream();
            await stream.WriteAsync(header);
            await stream.WriteAsync(frame);
            await stream.FlushAsync();
            crasher.LingerState = new LingerOption(true, 0); // RST on close — a crash, not a FIN
        } // dispose → abortive close while the goal may still be in flight

        // The merge loop keeps serving the surviving client, repeatedly.
        await AssertDoubles(survivor, 10);
        await AssertDoubles(survivor, 11);
    }

    [Fact]
    public async Task LegacyMode_SecondClient_IsStillRefusedLoudly()
    {
        // Default mode: the 061 FR-002 one-client contract must be unchanged.
        await using var host = new EngineHostFixture();
        await using var first = await host.ConnectAsync();

        var status = await first.RoundTripAsync(
            RequestFrame.Empty(first.NextRequestId(), RequestKind.Status));
        Assert.Equal(ResponseKind.Ack, status.Kind);

        await using var second = await host.ConnectAsync();
        var ex = await Record.ExceptionAsync(() => second.RoundTripAsync(
            RequestFrame.Empty(second.NextRequestId(), RequestKind.Status)));
        Assert.True(ex is SplitProtocolException or ClientTransportException,
            $"expected loud refusal, got {ex?.GetType().Name}: {ex?.Message}");

        // The first client keeps being served after the refusal.
        var statusAfter = await first.RoundTripAsync(
            RequestFrame.Empty(first.NextRequestId(), RequestKind.Status));
        Assert.Equal(ResponseKind.Ack, statusAfter.Kind);
    }
}
