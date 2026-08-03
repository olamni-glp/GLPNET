// T013 — engine server tests (US1): single-accept round-trip, second-client
// loud refusal (wire rule 1), compile-error keeps serving (FR-006, US1 AS-4),
// client-exit + reconnect keeps the loaded program (US1 AS-3).

using System.Net;

using GlpRuntime.Engine;
using GlpRuntime.EngineHost;
using GlpRuntime.EngineHost.Store;
using GlpRuntime.ReplClient;
using GlpRuntime.ResultCodec;
using GlpRuntime.SplitProtocol;

using RcExecutionStatus = GlpRuntime.ResultCodec.ExecutionStatus;

namespace GlpRuntime.EngineHost.Tests;

/// <summary>One live engine host on an ephemeral loopback port, per test.</summary>
public sealed class EngineHostFixture : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _serverTask;

    public int Port { get; }
    public RequestDispatcher Dispatcher { get; }

    /// <summary>Per-fixture file store root (T022 composition; deleted on dispose).</summary>
    public string StoreDir { get; }

    public EngineHostFixture(EngineServeMode mode = EngineServeMode.SingleClient)
    {
        var rootSelfGlp = GlpRuntime.EngineHost.Program.ResolveRootSelfGlpPath();
        var engine = new GlpEngine(rootSelfGlp);
        var session = new EngineSession("engine-test");
        session.TransitionTo(EngineState.Serving);
        StoreDir = Path.Combine(Path.GetTempPath(), $"glpsnap-test-{Guid.NewGuid():N}");
        var store = new SnapshotStore(
            primary: null,
            fallback: new FileSnapshotStore(StoreDir, "engine-test"),
            report: _ => { });
        Dispatcher = new RequestDispatcher(
            engine, session, new Quiescence(engine), store,
            linkRuntime: null, rootSelfSource: File.ReadAllText(rootSelfGlp));

        Port = FreePort();
        var server = new EngineServer(new IPEndPoint(IPAddress.Loopback, Port), Dispatcher, mode);
        _serverTask = Task.Run(() => server.RunAsync(_cts.Token));
    }

    public Task<ClientChannel> ConnectAsync() =>
        ClientChannel.ConnectAsync("127.0.0.1", Port, TimeSpan.FromSeconds(10));

    private static int FreePort()
    {
        var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _serverTask.WaitAsync(TimeSpan.FromSeconds(10)); }
        catch (OperationCanceledException) { }
        _cts.Dispose();
        try { Directory.Delete(StoreDir, recursive: true); } catch (IOException) { }
    }
}

public class EngineServerTests
{
    private const string CounterSource =
        "procedure double_it(Integer?, Integer).\n" +
        "double_it(X, Y?) :- Y := X? * 2.\n";

    [Fact]
    public async Task SingleClient_LoadThenGoal_RoundTrips()
    {
        await using var host = new EngineHostFixture();
        await using var client = await host.ConnectAsync();

        var loadResponse = await client.RoundTripAsync(
            RequestFrame.Text(client.NextRequestId(), RequestKind.LoadSource, CounterSource));
        Assert.Equal(ResponseKind.Ack, loadResponse.Kind);

        var goalResponse = await client.RoundTripAsync(
            RequestFrame.Text(client.NextRequestId(), RequestKind.RunGoal, "double_it(21, Y)"));
        Assert.Equal(ResponseKind.Result, goalResponse.Kind);

        var envelope = ResultEnvelopeCodec.Decode(goalResponse.Body);
        Assert.Equal(RcExecutionStatus.Success, envelope.Status);
        var binding = Assert.Single(envelope.ResolvedBindings);
        Assert.Equal("Y", binding.Key);
        var value = Assert.IsType<ConstTerm>(binding.Value);
        Assert.Equal("42", Assert.IsType<ConstString>(value.Value).Value);
    }

    [Fact]
    public async Task SecondClient_IsRefusedLoudly_WhileFirstIsActive()
    {
        await using var host = new EngineHostFixture();
        await using var first = await host.ConnectAsync();

        // Prove the first client is the active one before the second connects.
        var status = await first.RoundTripAsync(
            RequestFrame.Empty(first.NextRequestId(), RequestKind.Status));
        Assert.Equal(ResponseKind.Ack, status.Kind);

        // The second connection gets a loud PROTOCOL_ERROR then close (wire rule 1)
        // — the refusal frame arrives unprompted, so a bare STATUS round-trip
        // observes it as its "response" (request_id 0 ≠ ours → protocol exception)
        // or the closed socket (transport exception). Either is loud; never silence.
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

    [Fact]
    public async Task CompileError_ReturnsStructuredResult_AndEngineKeepsServing()
    {
        await using var host = new EngineHostFixture();
        await using var client = await host.ConnectAsync();

        // SRSW violation: reader X? twice, no writer — must be rejected.
        var badSource =
            "procedure p(Integer?, Integer).\n" +
            "p(X?, X?).\n";
        var response = await client.RoundTripAsync(
            RequestFrame.Text(client.NextRequestId(), RequestKind.LoadSource, badSource));

        Assert.Equal(ResponseKind.Result, response.Kind);
        var envelope = ResultEnvelopeCodec.Decode(response.Body);
        Assert.Equal(RcExecutionStatus.Failed, envelope.Status);
        Assert.False(string.IsNullOrEmpty(envelope.Error));

        // US1 AS-4: the engine remains usable for subsequent requests.
        var loadResponse = await client.RoundTripAsync(
            RequestFrame.Text(client.NextRequestId(), RequestKind.LoadSource, CounterSource));
        Assert.Equal(ResponseKind.Ack, loadResponse.Kind);
    }

    [Fact]
    public async Task ClientExit_ThenReconnect_KeepsLoadedProgram()
    {
        await using var host = new EngineHostFixture();

        await using (var first = await host.ConnectAsync())
        {
            var loadResponse = await first.RoundTripAsync(
                RequestFrame.Text(first.NextRequestId(), RequestKind.LoadSource, CounterSource));
            Assert.Equal(ResponseKind.Ack, loadResponse.Kind);
        } // client exits; engine must survive (US1 AS-3)

        await using var second = await host.ConnectAsync();
        ResponseFrame goalResponse = null!;
        // The server may need a beat to notice the disconnect and free the
        // one-client slot; retry until the reconnect is the active client.
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                goalResponse = await second.RoundTripAsync(
                    RequestFrame.Text(second.NextRequestId(), RequestKind.RunGoal, "double_it(5, Y)"));
                break;
            }
            catch (Exception) when (attempt < 20)
            {
                await Task.Delay(100);
                // reconnect with a fresh channel — the refused socket is gone
                var retry = await host.ConnectAsync();
                goalResponse = await retry.RoundTripAsync(
                    RequestFrame.Text(retry.NextRequestId(), RequestKind.RunGoal, "double_it(5, Y)"));
                await retry.DisposeAsync();
                break;
            }
        }

        Assert.Equal(ResponseKind.Result, goalResponse.Kind);
        var envelope = ResultEnvelopeCodec.Decode(goalResponse.Body);
        Assert.Equal(RcExecutionStatus.Success, envelope.Status);
        var binding = Assert.Single(envelope.ResolvedBindings);
        Assert.Equal("10",
            Assert.IsType<ConstString>(Assert.IsType<ConstTerm>(binding.Value).Value).Value);
    }

    [Fact]
    public async Task SuspendedGoal_ReportsSuspendedStatus()
    {
        await using var host = new EngineHostFixture();
        await using var client = await host.ConnectAsync();

        await client.RoundTripAsync(
            RequestFrame.Text(client.NextRequestId(), RequestKind.LoadSource, CounterSource));

        // X stays unbound → the guard suspends (US1 AS-2).
        var goalResponse = await client.RoundTripAsync(
            RequestFrame.Text(client.NextRequestId(), RequestKind.RunGoal, "double_it(X?, Y)"));
        Assert.Equal(ResponseKind.Result, goalResponse.Kind);
        var envelope = ResultEnvelopeCodec.Decode(goalResponse.Body);
        Assert.Equal(RcExecutionStatus.Suspended, envelope.Status);
    }
}
