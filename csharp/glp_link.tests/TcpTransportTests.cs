using System.Net;
using System.Net.Sockets;

using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// The raw-TCP transport (feature 025 runtime-integration) — the FIRST real cross-process
/// link, over IPv4 localhost (127.0.0.1). A <see cref="TcpTransport.ListenAsync"/> and a
/// <see cref="TcpTransport.ConnectAsync"/> on the same port rendezvous into one bilateral
/// duplex link; frames are length-prefixed so one Send ⇒ one peer Recv (FR-003/005/018).
/// These tests use real loopback sockets on an ephemeral free port; each is bounded by a
/// CancellationToken so a wiring bug fails fast instead of hanging.
/// </summary>
public class TcpTransportTests
{
    private static CancellationToken Timeout10 => new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

    /// <summary>Bind:0 to grab an OS-assigned free loopback port, then release it for the link to reuse.</summary>
    private static int FreeTcpPort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private static async Task<(ILinkEndpoint server, ILinkEndpoint client)> PairAsync()
    {
        int port = FreeTcpPort();
        var transport = new TcpTransport();
        var addr = LinkAddress.Endpoint("127.0.0.1", port);
        var listen = transport.ListenAsync(LinkScheme.Tcp, addr, LinkOptions.Default, Timeout10);
        var connect = transport.ConnectAsync(LinkScheme.Tcp, addr, LinkOptions.Default, Timeout10);
        await Task.WhenAll(listen, connect);
        return (listen.Result, connect.Result);
    }

    [Fact]
    public async Task RoundTrip_BothDirections()
    {
        var (server, client) = await PairAsync();
        try
        {
            await client.SendBytesAsync(new byte[] { 1, 2, 3, 4 }, Timeout10);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, await server.RecvBytesAsync(Timeout10));

            await server.SendBytesAsync(new byte[] { 9, 8, 7 }, Timeout10); // role-independent: server end can send too (FR-003)
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
        var (server, client) = await PairAsync();
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
    public async Task LargeFrame_ReassembledExactly()
    {
        var (server, client) = await PairAsync();
        try
        {
            var payload = new byte[100_000];
            for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i % 251);
            await client.SendBytesAsync(payload, Timeout10);
            Assert.Equal(payload, await server.RecvBytesAsync(Timeout10)); // spans many socket reads → exact reassembly
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
        var (server, client) = await PairAsync();
        try
        {
            await client.SendBytesAsync(new byte[] { 42 }, Timeout10);
            Assert.Equal(new byte[] { 42 }, await server.RecvBytesAsync(Timeout10)); // buffered frame drains first
            await client.CloseAsync();                                               // FIN
            Assert.Null(await server.RecvBytesAsync(Timeout10));                      // peer sees end-of-stream
        }
        finally
        {
            await server.DisposeAsync();
            await client.DisposeAsync();
        }
    }
}
