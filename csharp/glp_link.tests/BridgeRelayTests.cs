using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

using GlpQuick.Host;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// 064 US1 T012 (research D3, FR-004) — the Gleam-facing bridge acceptor on the QUIC-WS mesh
/// server: a loopback "Gleam" peer (the C# <see cref="TcpTransport"/> client is byte-identical
/// to the Gleam tcp leaf's dial path — 4-byte big-endian length-prefixed frames, parity proven
/// at the seam) dials <see cref="BridgeAcceptor"/> and its frames relay UNCHANGED through the
/// internal <see cref="Mesh"/> router to a mesh peer and back. The mesh routes over the
/// <see cref="ILinkEndpoint"/> seam, so a recording fake stands in for the QUIC leg here (the
/// genuine QUIC+WS leg is QuicMeshTests); the bridge must be a relay, not a translator.
/// </summary>
public class BridgeRelayTests
{
    private static CancellationToken Timeout10 => new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

    /// <summary>Bind:0 to grab an OS-assigned free loopback port, then release it for the bridge to reuse.</summary>
    private static int FreeTcpPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    /// <summary>A mesh peer at the seam: records every frame the mesh routes to it; recv parks.</summary>
    private sealed class FakeMeshPeer : ILinkEndpoint
    {
        private readonly Channel<byte[]> _sent = Channel.CreateUnbounded<byte[]>();
        public LinkId Id { get; }
#pragma warning disable CS0067 // OnFault is part of the seam; the relay scenario exercises routing, not faults.
        public event Action<LinkFaultSignal>? OnFault;
#pragma warning restore CS0067
        public FakeMeshPeer(string chan) =>
            Id = new LinkId(LinkScheme.Of("fake"), LinkAddress.Path(chan), LinkNonce.Int(1));

        public Task SendBytesAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
        {
            _sent.Writer.TryWrite(frame.ToArray());
            return Task.CompletedTask;
        }

        public Task<byte[]> NextRoutedAsync(CancellationToken ct) => _sent.Reader.ReadAsync(ct).AsTask();

        public bool NothingRouted => _sent.Reader.Count == 0;

        public async Task<byte[]?> RecvBytesAsync(CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            return null;
        }

        public Task CloseAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static int _msgNo;

    private static byte[] Env(string from, string to, string payload = "p") =>
        Encoding.UTF8.GetBytes(
            $"{{\"msg_id\":\"m{Interlocked.Increment(ref _msgNo)}\",\"from\":\"{from}\",\"to\":\"{to}\",\"seq\":null,\"payload\":\"{payload}\"}}");

    /// <summary>Register a mesh peer's announced id by routing one envelope from its link.</summary>
    private static Task Announce(Mesh mesh, FakeMeshPeer link, string id) =>
        mesh.RouteAsync(Env(id, "server"), fromSelf: false, srcLink: link, CancellationToken.None);

    private static Task<ILinkEndpoint> DialBridgeAsync(int port) =>
        new TcpTransport().ConnectAsync(LinkScheme.Tcp, LinkAddress.Endpoint("127.0.0.1", port), LinkOptions.Default, Timeout10);

    [Fact]
    public async Task bridge_relays_gleam_frames_to_mesh_peer_and_back_unchanged()
    {
        int port = FreeTcpPort();
        var mesh = new Mesh("server", TextWriter.Null);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var bridge = BridgeAcceptor.RunAsync(mesh, LinkAddress.Endpoint("127.0.0.1", port), new ClientCapacity(3), stop.Token);

        var quicPeer = new FakeMeshPeer("quic");
        await Announce(mesh, quicPeer, "quicpeer");

        var gleam = await DialBridgeAsync(port);
        try
        {
            // Gleam → mesh: the exact frame bytes arrive at the mesh peer (relay, not translation).
            var toMesh = Env("gleam1", "quicpeer", "hello-from-gleam");
            await gleam.SendBytesAsync(toMesh, Timeout10);
            Assert.Equal(toMesh, await quicPeer.NextRoutedAsync(Timeout10));

            // Mesh → Gleam: the reply routes to the bridge peer's announced id, bytes unchanged.
            var back = Env("quicpeer", "gleam1", "hello-from-mesh");
            await mesh.RouteAsync(back, fromSelf: false, srcLink: quicPeer, CancellationToken.None);
            Assert.Equal(back, await gleam.RecvBytesAsync(Timeout10));
        }
        finally
        {
            stop.Cancel();
            await bridge;
            await gleam.DisposeAsync();
        }
    }

    [Fact]
    public async Task bridge_over_capacity_peer_gets_the_notice_then_a_clean_close()
    {
        int port = FreeTcpPort();
        var mesh = new Mesh("server", TextWriter.Null);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        // Capacity 0: every bridge peer is deterministically over budget (shared --max-clients gate).
        var bridge = BridgeAcceptor.RunAsync(mesh, LinkAddress.Endpoint("127.0.0.1", port), new ClientCapacity(0), stop.Token);

        var prevErr = Console.Error;
        Console.SetError(TextWriter.Null); // the REJECT diagnostic is expected noise here
        ILinkEndpoint gleam;
        try { gleam = await DialBridgeAsync(port); }
        finally { Console.SetError(prevErr); }
        try
        {
            var notice = await gleam.RecvBytesAsync(Timeout10);
            Assert.NotNull(notice);
            Assert.Contains("over_capacity", Encoding.UTF8.GetString(notice!)); // explicit reject, not a stall
            Assert.Null(await gleam.RecvBytesAsync(Timeout10));                 // then the bridge closes the link
        }
        finally
        {
            stop.Cancel();
            await bridge;
            await gleam.DisposeAsync();
        }
    }

    /// <summary>
    /// A busy bridge port must fail LOUDLY (T040 cycle-2 `bind-failure-swallowed`):
    /// BRIDGE_READY is published only from the transport's post-bind seam, so a bind
    /// that never happens publishes nothing and the acceptor task faults instead of
    /// completing normally — the host cannot exit 0 advertising a bridge it never bound.
    /// </summary>
    [Fact]
    public async Task bridge_bind_failure_is_loud_no_ready_token_and_the_task_faults()
    {
        int port = FreeTcpPort();
        var occupant = new TcpListener(IPAddress.Loopback, port);
        occupant.Start(); // the port is already owned — the bridge's bind must refuse
        var mesh = new Mesh("server", TextWriter.Null);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var bound = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var prevErr = Console.Error;
        var err = new StringWriter();
        Console.SetError(err); // the ERR bridge_bind_failed diagnostic is the point of the test
        try
        {
            var acceptor = BridgeAcceptor.RunAsync(
                mesh, LinkAddress.Endpoint("127.0.0.1", port), new ClientCapacity(3), stop.Token, bound);

            // THE GATE, exactly as Program.RunMeshServerAsync awaits it: the bind
            // outcome must surface from this ONE task, with no second observation of
            // the acceptor Task (which faults only after its unwind — cycle-3
            // bind-gate-race, where the losing interleaving served bridgeless).
            var gate = await Assert.ThrowsAsync<SocketException>(() => bound.Task);
            Assert.Equal(SocketError.AddressAlreadyInUse, gate.SocketErrorCode);

            // …and the acceptor itself still fails loudly rather than returning normally.
            var ex = await Assert.ThrowsAsync<SocketException>(() => acceptor);
            Assert.Equal(SocketError.AddressAlreadyInUse, ex.SocketErrorCode);
            Assert.Contains("ERR bridge_bind_failed", err.ToString());
            Assert.DoesNotContain("BRIDGE_READY", err.ToString()); // no READY for a port nothing listens on
        }
        finally
        {
            Console.SetError(prevErr);
            occupant.Stop();
        }
    }

    [Fact]
    public async Task bridge_peer_disconnect_leaves_the_mesh_serving_later_peers()
    {
        int port = FreeTcpPort();
        var mesh = new Mesh("server", TextWriter.Null);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var bridge = BridgeAcceptor.RunAsync(mesh, LinkAddress.Endpoint("127.0.0.1", port), new ClientCapacity(3), stop.Token);

        var quicPeer = new FakeMeshPeer("quic");
        await Announce(mesh, quicPeer, "quicpeer");

        var prevErr = Console.Error;
        Console.SetError(TextWriter.Null); // CLIENT_DOWN / DROP diagnostics are expected noise here
        try
        {
            var g1 = await DialBridgeAsync(port);
            var first = Env("gleam1", "quicpeer");
            await g1.SendBytesAsync(first, Timeout10);
            Assert.Equal(first, await quicPeer.NextRoutedAsync(Timeout10)); // g1 was live and relaying
            await g1.DisposeAsync();                                        // Gleam peer crashes/leaves

            // The mesh survives the drop: a fresh bridge peer still relays both directions.
            var g2 = await DialBridgeAsync(port);
            var after = Env("gleam2", "quicpeer", "after-disconnect");
            await g2.SendBytesAsync(after, Timeout10);
            Assert.Equal(after, await quicPeer.NextRoutedAsync(Timeout10));

            var back = Env("quicpeer", "gleam2", "still-routing");
            await mesh.RouteAsync(back, fromSelf: false, srcLink: quicPeer, CancellationToken.None);
            Assert.Equal(back, await g2.RecvBytesAsync(Timeout10));
            await g2.DisposeAsync();
        }
        finally
        {
            Console.SetError(prevErr);
            stop.Cancel();
            await bridge;
        }
    }
}
