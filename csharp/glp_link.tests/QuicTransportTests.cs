using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// The genuine HTTP/3 (QUIC) + WebSocket transport leaf (feature 036). These run a <b>real</b>
/// <c>System.Net.Quic</c>/MsQuic handshake between two in-process endpoints over loopback UDP
/// (real datagrams, real TLS 1.3, real ALPN h3) carrying a genuine RFC 6455 WebSocket link — the
/// same-host verification of US1 (SC-001/SC-005). Each test is bounded by a token so a wiring bug
/// fails fast instead of hanging. Skipped automatically where the platform lacks QUIC (FR-001 gate).
/// </summary>
public class QuicTransportTests
{
    private static bool QuicAvailable => QuicTransport.IsSupported;

    private static CancellationToken Timeout(int seconds = 15) =>
        new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

    /// <summary>An OS-assigned free UDP port for the loopback link.</summary>
    private static int FreeUdpPort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    /// <summary>
    /// A shared self-signed cert + its SPKI pin to the Decision-5 profile, round-tripped through PFX
    /// so the private key is in a form msquic/SChannel accepts on every platform.
    /// </summary>
    private static (X509Certificate2 cert, string pin) MakeSharedCert()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=GLP-Quick Shared Cert", ec, HashAlgorithmName.SHA256);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, false));
        var now = DateTimeOffset.UtcNow;
        using var ephemeral = req.CreateSelfSigned(now.AddMinutes(-1), now.AddDays(1));
        var loaded = X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
        return (loaded, QuicTransport.SpkiPin(loaded));
    }

    private static async Task<(ILinkEndpoint server, ILinkEndpoint client)> PairAsync()
    {
        int port = FreeUdpPort();
        var (cert, pin) = MakeSharedCert();
        var transport = new QuicTransport(cert, pin);
        var addr = LinkAddress.Endpoint("127.0.0.1", port);
        var listen = transport.ListenAsync(LinkScheme.Quic, addr, LinkOptions.Default, Timeout());
        var connect = transport.ConnectAsync(LinkScheme.Quic, addr, LinkOptions.Default, Timeout());
        await Task.WhenAll(listen, connect);
        return (listen.Result, connect.Result);
    }

    [Fact]
    public async Task RoundTrip_BothDirections()
    {
        if (!QuicAvailable) return; // platform lacks QUIC (FR-001 gate) — nothing to verify here
        var (server, client) = await PairAsync();
        try
        {
            await client.SendBytesAsync(new byte[] { 1, 2, 3, 4 }, Timeout());
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, await server.RecvBytesAsync(Timeout()));

            await server.SendBytesAsync(new byte[] { 9, 8, 7 }, Timeout()); // role-independent (FR-003/008a)
            Assert.Equal(new byte[] { 9, 8, 7 }, await client.RecvBytesAsync(Timeout()));
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
        if (!QuicAvailable) return; // platform lacks QUIC (FR-001 gate) — nothing to verify here
        var (server, client) = await PairAsync();
        try
        {
            for (int i = 0; i < 8; i++)
                await client.SendBytesAsync(new byte[] { (byte)i, 0xAA }, Timeout());
            for (int i = 0; i < 8; i++)
                Assert.Equal(new byte[] { (byte)i, 0xAA }, await server.RecvBytesAsync(Timeout()));
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
        if (!QuicAvailable) return; // platform lacks QUIC (FR-001 gate) — nothing to verify here
        var (server, client) = await PairAsync();
        try
        {
            var payload = new byte[100_000]; // > 65535 → exercises the 64-bit RFC 6455 length path
            for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i % 251);
            await client.SendBytesAsync(payload, Timeout());
            Assert.Equal(payload, await server.RecvBytesAsync(Timeout()));
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
        if (!QuicAvailable) return; // platform lacks QUIC (FR-001 gate) — nothing to verify here
        var (server, client) = await PairAsync();
        try
        {
            await client.SendBytesAsync(new byte[] { 42 }, Timeout());
            Assert.Equal(new byte[] { 42 }, await server.RecvBytesAsync(Timeout())); // buffered frame drains first
            await client.CloseAsync();                                               // WS close
            Assert.Null(await server.RecvBytesAsync(Timeout()));                      // peer sees end-of-stream
        }
        finally
        {
            await server.DisposeAsync();
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task CertPinMismatch_HandshakeRejected()
    {
        if (!QuicAvailable) return; // platform lacks QUIC (FR-001 gate) — nothing to verify here
        int port = FreeUdpPort();
        var (certA, pinA) = MakeSharedCert();
        var serverT = new QuicTransport(certA, pinA);
        var clientT = new QuicTransport(certA, "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ="); // wrong pin
        var addr = LinkAddress.Endpoint("127.0.0.1", port);

        var listenCt = Timeout(8);
        var listen = serverT.ListenAsync(LinkScheme.Quic, addr, LinkOptions.Default, listenCt);
        // The client must REFUSE the server cert (pin mismatch) — handshake fails, no half-open link.
        await Assert.ThrowsAnyAsync<Exception>(async () =>
            await clientT.ConnectAsync(LinkScheme.Quic, addr, LinkOptions.Default, Timeout(8)));
        // Observe the listen task so its eventual fault/cancel is not unobserved.
        try { await listen; } catch { /* expected: no valid client ever completed */ }
    }
}
