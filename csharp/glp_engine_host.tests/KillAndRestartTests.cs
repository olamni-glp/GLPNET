// T034 — the FR-033 kill-and-restart correctness test (US4 independent test,
// SC-002): a program streams committed results to a peer over an established
// tcp link; the engine is killed mid-stream; the supervisor restarts it from
// the latest snapshot (the restore path); the link re-establishes through the
// rewire path; and the peer-observable committed result stream is EXACTLY the
// stream of an uninterrupted run — no committed value lost, duplicated, or
// reordered.
//
// At-most-once crash boundary exercised for real (FR-032): the value 99 is
// produced AFTER the snapshot and shipped to the peer before the kill —
// committed-to-transport, discarded from engine state on restore — so it
// appears EXACTLY ONCE at the peer and is never re-executed. The client's
// in-flight view is exercised too: a request against the killed engine
// surfaces as a transport failure (distinct from a goal failure, FR-007) and
// the client re-submits after the restart.
//
// Topology per the MVP supervision contract: the supervisor is the engine's
// ONE wire client, so the interactive phases run unsupervised (test = client)
// and the supervisor phase owns the restart-from-latest; it is stopped (child
// left running) before the client resumes. Determinism: every peer command is
// sent exactly once and each phase gates on the OBSERVED peer stream, never on
// sleeps alone.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

using GlpRuntime.Link.Reliability;
using GlpRuntime.Multiagent;
using GlpRuntime.ReplClient;
using GlpRuntime.Runtime;
using GlpRuntime.SplitProtocol;

using SupervisorService = GlpRuntime.Supervisor.Supervisor;

namespace GlpRuntime.EngineHost.Tests;

public class KillAndRestartTests : IDisposable
{
    private readonly string _storeRoot =
        Path.Combine(Path.GetTempPath(), $"glpsnap-t034-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_storeRoot, recursive: true); } catch (IOException) { }
    }

    // ------------------------------------------------------------ test harness

    private static string EngineBinary()
    {
        var repoRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repoRoot != null && !File.Exists(Path.Combine(repoRoot.FullName, "programs", "self.glp")))
            repoRoot = repoRoot.Parent;
        Assert.NotNull(repoRoot);
        var exe = Path.Combine(repoRoot!.FullName,
            "csharp", "glp_engine_host", "bin", "Debug", "net10.0", "glp_engine_host.exe");
        Assert.True(File.Exists(exe), $"engine binary not built: {exe}");
        return exe;
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, string what)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }
        Assert.Fail($"timed out waiting for: {what}");
    }

    /// <summary>The engine-side program: each peer `emit(N)` command produces N on the link Out stream.</summary>
    private static string StreamerSource(int peerPort) =>
        "-mode(system).\n" +
        "\n" +
        "procedure start.\n" +
        "start :-\n" +
        $"    client_connector(link_id(\"tcp\", ep(\"127.0.0.1\", {peerPort}), 1), Link, _),\n" +
        "    run_link(Link?).\n" +
        "\n" +
        "procedure run_link(Link(_, _)?).\n" +
        "run_link(ch(In, Out?)) :- serve(In?, Out).\n" +
        "\n" +
        "procedure serve(Stream(_)?, Stream(_)).\n" +
        "serve([emit(N)|In], [N?|Out?]) :- ground(N?) | serve(In?, Out).\n" +
        "serve([], []).\n";

    /// <summary>
    /// The raw test peer: the tcp LISTENER end of the link. Accepts each engine
    /// connection in turn (pre-crash, then the rewired one), decodes every shipped
    /// frame into the observed value stream, and sends `emit(N)` commands (each
    /// exactly once) with per-connection message ids from 0 — the fresh
    /// reliability state a freshly (re-)established handle expects.
    /// </summary>
    private sealed class TcpPeer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _acceptLoop;
        private readonly object _lock = new();
        private readonly List<long> _values = new();
        private NetworkStream? _current;
        private uint _sendSeq;
        private int _connections;

        public TcpPeer(int port)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _acceptLoop = Task.Run(AcceptLoopAsync);
        }

        public long[] Values { get { lock (_lock) return _values.ToArray(); } }

        public int Connections { get { lock (_lock) return _connections; } }

        public bool HasLiveConnection { get { lock (_lock) return _current is not null; } }

        public async Task SendEmitAsync(int n)
        {
            NetworkStream stream;
            uint seq;
            lock (_lock)
            {
                stream = _current ?? throw new InvalidOperationException("peer has no live connection");
                seq = _sendSeq++;
            }
            var payload = new PayloadSerializer("peer").SerializeAgentMessage(
                new StructTerm("emit", new Term[] { new ConstTerm((long)n) }));
            foreach (var frame in FrameCodec.Encode(payload, messageId: seq))
            {
                var header = new byte[4];
                BinaryPrimitives.WriteInt32BigEndian(header, frame.Length);
                await stream.WriteAsync(header);
                await stream.WriteAsync(frame);
            }
            await stream.FlushAsync();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(_cts.Token);
                }
                catch (Exception)
                {
                    return; // listener stopped — peer disposed
                }
                var stream = client.GetStream();
                lock (_lock)
                {
                    _current = stream;
                    _sendSeq = 0; // fresh handle on the engine side expects ids from 0
                    _connections++;
                }
                try { await RecvLoopAsync(stream); }
                catch (Exception) { /* connection died with the engine — expected on the kill */ }
                finally
                {
                    lock (_lock)
                        if (ReferenceEquals(_current, stream)) _current = null;
                    client.Dispose();
                }
            }
        }

        private async Task RecvLoopAsync(NetworkStream stream)
        {
            while (true)
            {
                var header = await ReadExactlyAsync(stream, 4);
                if (header is null) return;
                int len = BinaryPrimitives.ReadInt32BigEndian(header);
                var body = await ReadExactlyAsync(stream, len);
                if (body is null) return;

                var parsed = FrameCodec.ParseFrame(body);
                var payload = new FrameReassembler().Accept(parsed);
                if (payload is null) continue;
                var term = new PayloadSerializer(string.Empty).DeserializeAgentMessagePayload(
                    payload, allocateImportedVar: _ => throw new Exception("unexpected variable on the wire"));
                if (term is ConstTerm { Value: long v })
                    lock (_lock) _values.Add(v);
                else if (term is ConstTerm { Value: int iv })
                    lock (_lock) _values.Add(iv);
                else
                    throw new Exception($"peer received unexpected term {term}");
            }
        }

        private async Task<byte[]?> ReadExactlyAsync(NetworkStream stream, int count)
        {
            var buf = new byte[count];
            int off = 0;
            while (off < count)
            {
                int n = await stream.ReadAsync(buf.AsMemory(off, count - off), _cts.Token);
                if (n == 0) return null;
                off += n;
            }
            return buf;
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            try { await _acceptLoop.WaitAsync(TimeSpan.FromSeconds(5)); } catch (TimeoutException) { }
            _cts.Dispose();
        }
    }

    private static async Task<ClientChannel> ConnectClientAsync(int port, TimeSpan budget)
    {
        var deadline = DateTimeOffset.UtcNow + budget;
        while (true)
        {
            try
            {
                var channel = await ClientChannel.ConnectAsync("127.0.0.1", port, TimeSpan.FromSeconds(5));
                var status = await channel.RoundTripAsync(
                    RequestFrame.Empty(channel.NextRequestId(), RequestKind.Status));
                if (status.Kind == ResponseKind.Ack)
                    return channel;
                await channel.DisposeAsync();
            }
            catch (Exception) when (DateTimeOffset.UtcNow < deadline)
            {
                // engine not listening yet / one-client slot still held — retry
            }
            await Task.Delay(100);
        }
    }

    /// <summary>Drive the engine's request-thread drain (pump service + scheduler) with a trivial goal.</summary>
    private static async Task KickAsync(ClientChannel client)
    {
        var response = await client.RoundTripAsync(
            RequestFrame.Text(client.NextRequestId(), RequestKind.RunGoal, "Z := 1 + 1"));
        Assert.Equal(ResponseKind.Result, response.Kind);
    }

    /// <summary>Send one command (exactly once) and kick until the peer observes the value.</summary>
    private static async Task EmitAndAwaitAsync(TcpPeer peer, ClientChannel client, int n)
    {
        await peer.SendEmitAsync(n);
        await WaitUntilAsync(async () =>
        {
            await KickAsync(client);
            return peer.Values.Contains(n);
        }, TimeSpan.FromSeconds(30), $"peer to observe {n}");
    }

    // ------------------------------------------------------------- the FR-033 test

    [Fact]
    public async Task KillAndRestart_PeerObservableCommittedStream_EqualsUninterruptedRun()
    {
        int peerPort = FreePort();
        int enginePort = FreePort();
        var engineBinary = EngineBinary();

        await using var peer = new TcpPeer(peerPort);

        // ---- phase 1: unsupervised engine; establish the link; commit 1..3 ----
        using var engine1 = Process.Start(new ProcessStartInfo
        {
            FileName = engineBinary,
            Arguments = $"--listen 127.0.0.1:{enginePort} --store \"{_storeRoot}\"",
            UseShellExecute = false,
        })!;
        var client = await ConnectClientAsync(enginePort, TimeSpan.FromSeconds(30));
        try
        {
            var loaded = await client.RoundTripAsync(RequestFrame.Text(
                client.NextRequestId(), RequestKind.LoadSource, StreamerSource(peerPort)));
            Assert.Equal(ResponseKind.Ack, loaded.Kind);

            var started = await client.RoundTripAsync(RequestFrame.Text(
                client.NextRequestId(), RequestKind.RunGoal, "start"));
            Assert.Equal(ResponseKind.Result, started.Kind);
            await WaitUntilAsync(() => Task.FromResult(peer.Connections == 1),
                TimeSpan.FromSeconds(10), "engine to connect to the peer");

            foreach (var n in new[] { 1, 2, 3 })
                await EmitAndAwaitAsync(peer, client, n);

            // ---- snapshot: 1..3 are the committed-in-snapshot prefix ----
            var ack = await client.RoundTripAsync(
                RequestFrame.Empty(client.NextRequestId(), RequestKind.Snapshot));
            Assert.Equal(ResponseKind.Ack, ack.Kind);
            Assert.Contains("snapshot seq=1", ack.BodyText());

            // ---- post-snapshot work: 99 ships to the peer, then the kill discards
            // it from engine state — committed-to-transport, exactly-once (FR-032).
            await EmitAndAwaitAsync(peer, client, 99);
        }
        finally
        {
            // ---- kill mid-stream ----
            engine1.Kill(entireProcessTree: true);
            engine1.WaitForExit(10_000);
        }

        // The client observes the crash boundary as a TRANSPORT failure, distinct
        // from any goal failure (FR-007) — its cue to re-submit after the restart.
        await Assert.ThrowsAnyAsync<Exception>(async () => await KickAsync(client));
        await client.DisposeAsync();

        // ---- phase 2: the supervisor restarts from the latest snapshot; the
        // rewire path re-establishes the link (the peer sees connection #2) ----
        using var supervisor = new SupervisorService(new GlpRuntime.Supervisor.SupervisorConfig
        {
            EngineBinary = engineBinary,
            Listen = $"127.0.0.1:{enginePort}",
            StoreRoot = _storeRoot,
            PingInterval = TimeSpan.FromMilliseconds(200),
            PingTimeout = TimeSpan.FromMilliseconds(800),
            StartupBudget = TimeSpan.FromSeconds(30),
            BackoffInitial = TimeSpan.FromMilliseconds(50),
            BackoffMultiplier = 2.0,
            BackoffMax = TimeSpan.FromSeconds(1),
        });
        await supervisor.StartAsync(CancellationToken.None);
        await WaitUntilAsync(
            () => Task.FromResult(supervisor.Log.ReadStatus()?.EngineState == "healthy"),
            TimeSpan.FromSeconds(30), "supervised engine healthy");
        Assert.Equal(1UL, supervisor.Log.ReadStatus()!.LastSnapshotSeq); // restored from latest
        await WaitUntilAsync(
            () => Task.FromResult(peer.Connections == 2 && peer.HasLiveConnection),
            TimeSpan.FromSeconds(30), "link re-established to the peer (connection #2)");

        // Free the one-client slot (the supervisor holds it, FR-002) WITHOUT
        // killing the engine, so the client can resume interactively.
        await supervisor.StopAsync(CancellationToken.None);

        // ---- phase 3: the client re-submits; the restored producer resumes ----
        client = await ConnectClientAsync(enginePort, TimeSpan.FromSeconds(30));
        try
        {
            // Every restored link is adopted; none failed (US4 STATUS surface).
            var status = await client.RoundTripAsync(
                RequestFrame.Empty(client.NextRequestId(), RequestKind.Status));
            Assert.Contains("pending_link_rewires=0", status.BodyText());
            Assert.Contains("failed_link_rewires=0", status.BodyText());

            foreach (var n in new[] { 4, 5, 6 })
                await EmitAndAwaitAsync(peer, client, n);
        }
        finally
        {
            await client.DisposeAsync();
        }

        // ---- FR-033/SC-002: the peer-observable committed stream is EXACTLY the
        // uninterrupted run's stream for the same command sequence — the snapshot
        // prefix (1,2,3), the post-snapshot committed-to-transport value (99,
        // exactly once), and the resumed suffix (4,5,6). No loss, no duplication,
        // no reordering across the crash boundary.
        Assert.Equal(new long[] { 1, 2, 3, 99, 4, 5, 6 }, peer.Values);
    }
}
