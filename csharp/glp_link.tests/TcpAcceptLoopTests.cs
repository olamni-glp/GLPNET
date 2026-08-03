using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// T015 (feature 064 US2) — <see cref="TcpTransport.AcceptLoopAsync"/>: one bound
/// listener accepts clients continuously until stopped, none dropped. Each accept
/// is an independent bilateral link (D-9 — never a fan-out hub); cancellation ends
/// the enumeration cleanly and releases the port; one client disconnecting leaves
/// the other links (and further accepts) unaffected. The single-accept
/// <see cref="TcpTransport.ListenAsync"/> path is untouched (TcpTransportTests).
/// </summary>
public class TcpAcceptLoopTests
{
    private static CancellationToken Timeout10 => new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

    /// <summary>Bind:0 to grab an OS-assigned free loopback port, then release it for the loop to reuse.</summary>
    private static int FreeTcpPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    /// <summary>Pump the accept loop into a channel so tests await accepted endpoints one by one.</summary>
    private static (ChannelReader<ILinkEndpoint> Accepted, Task Loop) StartLoop(
        TcpTransport transport, LinkAddress addr, CancellationToken ct)
    {
        var accepted = Channel.CreateUnbounded<ILinkEndpoint>();
        var loop = Task.Run(async () =>
        {
            await foreach (var ep in transport.AcceptLoopAsync(LinkScheme.Tcp, addr, LinkOptions.Default, ct))
                accepted.Writer.TryWrite(ep);
            accepted.Writer.Complete();
        });
        return (accepted.Reader, loop);
    }

    /// <summary>
    /// Each client sends one distinct probe byte; recv on each server end pairs the
    /// (unordered) accepted endpoints with their clients by probe.
    /// </summary>
    private static async Task<Dictionary<byte, ILinkEndpoint>> PairByProbeAsync(
        IReadOnlyList<ILinkEndpoint> clients, IReadOnlyList<ILinkEndpoint> servers)
    {
        for (int i = 0; i < clients.Count; i++)
            await clients[i].SendBytesAsync(new[] { (byte)i }, Timeout10);

        var byProbe = new Dictionary<byte, ILinkEndpoint>();
        foreach (var s in servers)
        {
            var probe = await s.RecvBytesAsync(Timeout10);
            Assert.NotNull(probe);
            byProbe[Assert.Single(probe!)] = s;
        }
        return byProbe;
    }

    [Fact]
    public async Task Three_concurrent_connects_all_accepted()
    {
        int port = FreeTcpPort();
        var transport = new TcpTransport();
        var addr = LinkAddress.Endpoint("127.0.0.1", port);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var (accepted, loop) = StartLoop(transport, addr, stop.Token);

        var connects = new[]
        {
            transport.ConnectAsync(LinkScheme.Tcp, addr, LinkOptions.Default, Timeout10),
            transport.ConnectAsync(LinkScheme.Tcp, addr, LinkOptions.Default, Timeout10),
            transport.ConnectAsync(LinkScheme.Tcp, addr, LinkOptions.Default, Timeout10),
        };
        var clients = await Task.WhenAll(connects);
        var servers = new List<ILinkEndpoint>();
        for (int i = 0; i < 3; i++)
            servers.Add(await accepted.ReadAsync(Timeout10)); // all three accepted, none dropped

        try
        {
            // Every client round-trips with exactly one server end (three independent links).
            var byProbe = await PairByProbeAsync(clients, servers);
            Assert.Equal(3, byProbe.Count);
            for (int i = 0; i < 3; i++)
            {
                await byProbe[(byte)i].SendBytesAsync(new byte[] { 0xA0, (byte)i }, Timeout10);
                Assert.Equal(new byte[] { 0xA0, (byte)i }, await clients[i].RecvBytesAsync(Timeout10));
            }
        }
        finally
        {
            stop.Cancel();
            await loop;
            foreach (var ep in servers.Concat(clients))
                await ep.DisposeAsync();
        }
    }

    [Fact]
    public async Task Stop_terminates_cleanly_and_releases_the_port()
    {
        int port = FreeTcpPort();
        var transport = new TcpTransport();
        var addr = LinkAddress.Endpoint("127.0.0.1", port);
        using var stop = new CancellationTokenSource();
        var (accepted, loop) = StartLoop(transport, addr, stop.Token);

        var client = await transport.ConnectAsync(LinkScheme.Tcp, addr, LinkOptions.Default, Timeout10);
        var server = await accepted.ReadAsync(Timeout10); // loop is live before we stop it

        stop.Cancel();
        await loop; // clean termination — the enumeration ends without throwing
        Assert.False(accepted.TryRead(out _)); // completed, nothing dangling

        // The already-established link outlives the stopped listener…
        await client.SendBytesAsync(new byte[] { 5 }, Timeout10);
        Assert.Equal(new byte[] { 5 }, await server.RecvBytesAsync(Timeout10));

        // …and the port is released for re-use (single-accept path binds it again).
        var relisten = transport.ListenAsync(LinkScheme.Tcp, addr, LinkOptions.Default, Timeout10);
        var reclient = await transport.ConnectAsync(LinkScheme.Tcp, addr, LinkOptions.Default, Timeout10);
        var reserver = await relisten;

        foreach (var ep in new[] { client, server, reclient, reserver })
            await ep.DisposeAsync();
    }

    [Fact]
    public async Task Disconnect_one_client_does_not_affect_others_or_further_accepts()
    {
        int port = FreeTcpPort();
        var transport = new TcpTransport();
        var addr = LinkAddress.Endpoint("127.0.0.1", port);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var (accepted, loop) = StartLoop(transport, addr, stop.Token);

        var clients = new List<ILinkEndpoint>();
        var servers = new List<ILinkEndpoint>();
        for (int i = 0; i < 3; i++)
        {
            clients.Add(await transport.ConnectAsync(LinkScheme.Tcp, addr, LinkOptions.Default, Timeout10));
            servers.Add(await accepted.ReadAsync(Timeout10));
        }

        try
        {
            var byProbe = await PairByProbeAsync(clients, servers);

            // Client 0 disconnects: its server end sees end-of-stream…
            await clients[0].DisposeAsync();
            Assert.Null(await byProbe[0].RecvBytesAsync(Timeout10));

            // …while the other two links keep round-tripping, unaffected.
            for (byte i = 1; i < 3; i++)
            {
                await clients[i].SendBytesAsync(new byte[] { 0xB0, i }, Timeout10);
                Assert.Equal(new byte[] { 0xB0, i }, await byProbe[i].RecvBytesAsync(Timeout10));
            }

            // And the loop still accepts a fresh client after the disconnect.
            var late = await transport.ConnectAsync(LinkScheme.Tcp, addr, LinkOptions.Default, Timeout10);
            var lateServer = await accepted.ReadAsync(Timeout10);
            await late.SendBytesAsync(new byte[] { 0xC4 }, Timeout10);
            Assert.Equal(new byte[] { 0xC4 }, await lateServer.RecvBytesAsync(Timeout10));
            clients.Add(late);
            servers.Add(lateServer);
        }
        finally
        {
            stop.Cancel();
            await loop;
            foreach (var ep in servers.Concat(clients.Skip(1)))
                await ep.DisposeAsync();
        }
    }
}
