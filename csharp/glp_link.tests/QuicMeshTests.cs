using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Feature 050 US4 (T029) — the multi-accept mesh server role (contract
/// <c>mesh-test-harness.md</c>): one <see cref="QuicTransport.CreateListenerAsync"/> bind on one UDP
/// port accepts MANY isolated client links (<see cref="QuicTransport.QuicListenerHandle"/>), each a
/// genuine per-client QUIC handshake + WS bootstrap on its own <c>QuicConnection</c> + bidi stream —
/// so one link's fault never touches a sibling (FR-013/SC-004 concurrency floor; 036 FR-005/006).
/// This validates the shipped host capability the US4 GLP mesh program drives; skip-guarded on
/// <see cref="QuicTransport.IsSupported"/>.
/// </summary>
public class QuicMeshTests
{
    private static bool QuicAvailable => QuicTransport.IsSupported;

    private static CancellationToken Timeout(int seconds = 20) =>
        new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

    private static int FreeUdpPort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    private static (X509Certificate2 cert, string pin) MakeCert(string cn)
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest($"CN={cn}", ec, HashAlgorithmName.SHA256);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, false));
        var now = DateTimeOffset.UtcNow;
        using var ephemeral = req.CreateSelfSigned(now.AddMinutes(-1), now.AddDays(1));
        var loaded = X509CertificateLoader.LoadPkcs12(
            ephemeral.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
        return (loaded, QuicTransport.SpkiPin(loaded));
    }

    [Fact] // T029 — N isolated client links from ONE UDP port; a sibling's fault never propagates
    public async Task MultiAccept_ManyIsolatedLinksFromOnePort_SiblingFaultIsolated()
    {
        if (!QuicAvailable) return;
        const int clients = 3;
        int port = FreeUdpPort();
        var (cert, pin) = MakeCert("GLP-Quick 050 US4 Mesh");
        var transport = new QuicTransport(cert, pin);
        var addr = LinkAddress.Endpoint("127.0.0.1", port);

        await using var listener = await transport.CreateListenerAsync(addr, LinkOptions.Default, Timeout());

        // Bring up N genuine client links against the ONE bound port — each accept is its own
        // per-client QUIC handshake + WS bootstrap (an isolated QuicConnection + bidi stream).
        var connectors = new ILinkEndpoint[clients];
        var accepted = new ILinkEndpoint[clients];
        for (int i = 0; i < clients; i++)
        {
            var acceptTask = listener.AcceptAsync(Timeout());
            connectors[i] = await transport.ConnectAsync(LinkScheme.Quic, addr, LinkOptions.Default, Timeout());
            accepted[i] = await acceptTask;
        }

        try
        {
            // Per-link isolation: distinct payloads cross distinct links, full-duplex, no cross-talk.
            for (int i = 0; i < clients; i++)
            {
                byte[] up = { (byte)(0x10 + i), 0xAA };
                byte[] down = { (byte)(0x20 + i), 0xBB };
                await connectors[i].SendBytesAsync(up, Timeout());
                Assert.Equal(up, await accepted[i].RecvBytesAsync(Timeout()));
                await accepted[i].SendBytesAsync(down, Timeout());
                Assert.Equal(down, await connectors[i].RecvBytesAsync(Timeout()));
            }

            // Kill client 0's link. Its accepted end observes end-of-link (null) ...
            await connectors[0].DisposeAsync();
            Assert.Null(await accepted[0].RecvBytesAsync(Timeout()));

            // ... and every SIBLING link keeps exchanging both directions, untouched (FR-013 isolation).
            for (int i = 1; i < clients; i++)
            {
                byte[] up = { (byte)(0x30 + i) };
                byte[] down = { (byte)(0x40 + i) };
                await connectors[i].SendBytesAsync(up, Timeout());
                Assert.Equal(up, await accepted[i].RecvBytesAsync(Timeout()));
                await accepted[i].SendBytesAsync(down, Timeout());
                Assert.Equal(down, await connectors[i].RecvBytesAsync(Timeout()));
            }
        }
        finally
        {
            for (int i = 0; i < clients; i++)
            {
                await connectors[i].DisposeAsync();
                await accepted[i].DisposeAsync();
            }
        }
    }
}
