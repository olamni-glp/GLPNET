using System.Net;
using System.Net.Sockets;

using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// The ZeroMQ (NetMQ) transport leaf (feature 062 US3, T020) — bilateral P2P over a
/// PAIR socket pair on IPv4 localhost. A <see cref="ZmqTransport.ListenAsync"/> (Bind)
/// and a <see cref="ZmqTransport.ConnectAsync"/> (Connect) on the same port form one
/// duplex link; each Send ⇒ one peer Recv (FR-003/005/018), empty frames round-trip,
/// and CloseAsync gives the peer a graceful null. Every recv is bounded by a
/// CancellationToken so a wiring bug fails fast instead of hanging.
/// </summary>
public class ZmqTransportTests
{
    private static CancellationToken Timeout10 => new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

    /// <summary>Bind:0 to grab an OS-assigned free loopback port, then release it for the zmq link.</summary>
    private static int FreeTcpPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static (ILinkEndpoint server, ILinkEndpoint client) Pair()
    {
        int port = FreeTcpPort();
        var t = new ZmqTransport();
        var addr = LinkAddress.Endpoint("127.0.0.1", port);
        // Listen/Connect complete synchronously (zmq Bind/Connect are non-blocking; connect is lazy).
        var server = t.ListenAsync(LinkScheme.Zmq, addr, LinkOptions.Default).GetAwaiter().GetResult();
        var client = t.ConnectAsync(LinkScheme.Zmq, addr, LinkOptions.Default).GetAwaiter().GetResult();
        return (server, client);
    }

    [Fact]
    public async Task RoundTrip_BothDirections()
    {
        var (server, client) = Pair();
        try
        {
            await client.SendBytesAsync(new byte[] { 1, 2, 3, 4 }, Timeout10);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, await server.RecvBytesAsync(Timeout10));

            await server.SendBytesAsync(new byte[] { 9, 8, 7 }, Timeout10); // role-independent (FR-003)
            Assert.Equal(new byte[] { 9, 8, 7 }, await client.RecvBytesAsync(Timeout10));
        }
        finally
        {
            await server.DisposeAsync();
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task Fifo_MultipleFrames_InOrder()
    {
        var (server, client) = Pair();
        try
        {
            for (int i = 0; i < 8; i++)
                await client.SendBytesAsync(new byte[] { (byte)i, 0xAA }, Timeout10);
            for (int i = 0; i < 8; i++)
                Assert.Equal(new byte[] { (byte)i, 0xAA }, await server.RecvBytesAsync(Timeout10));
        }
        finally
        {
            await server.DisposeAsync();
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task EmptyFrame_RoundTrips()
    {
        // The control-tag scheme keeps an empty payload distinct from EOS.
        var (server, client) = Pair();
        try
        {
            await client.SendBytesAsync(Array.Empty<byte>(), Timeout10);
            Assert.Equal(Array.Empty<byte>(), await server.RecvBytesAsync(Timeout10));
        }
        finally
        {
            await server.DisposeAsync();
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task LargeFrame_ExactlyPreserved()
    {
        var (server, client) = Pair();
        try
        {
            var payload = new byte[100_000];
            for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i % 251);
            await client.SendBytesAsync(payload, Timeout10);
            Assert.Equal(payload, await server.RecvBytesAsync(Timeout10));
        }
        finally
        {
            await server.DisposeAsync();
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task GracefulClose_PeerRecvReturnsNull()
    {
        var (server, client) = Pair();
        try
        {
            await client.SendBytesAsync(new byte[] { 42 }, Timeout10);
            Assert.Equal(new byte[] { 42 }, await server.RecvBytesAsync(Timeout10)); // buffered frame drains first
            await client.CloseAsync();                                               // EOS
            Assert.Null(await server.RecvBytesAsync(Timeout10));                      // peer sees end-of-stream
        }
        finally
        {
            await server.DisposeAsync();
            await client.DisposeAsync();
        }
    }

    [Fact]
    public void Registry_selects_zmq_transport_by_scheme()
    {
        var reg = new TransportRegistry();
        var zmq = new ZmqTransport();
        reg.Register(zmq);
        Assert.Same(zmq, reg.Select(LinkScheme.Zmq));
        Assert.Contains(LinkScheme.Zmq, reg.Schemes);
    }

    [Fact]
    public void WrongScheme_Rejected()
    {
        var t = new ZmqTransport();
        Assert.Throws<ArgumentException>(() =>
            { _ = t.ConnectAsync(LinkScheme.Tcp, LinkAddress.Endpoint("127.0.0.1", 5599), LinkOptions.Default); });
    }
}
