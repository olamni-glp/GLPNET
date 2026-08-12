using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Feature 064 US3 (T011) — the QUIC connector retries a dial that lands before the listener has
/// bound, until the caller's CancellationToken cancels: exact parity with
/// <see cref="TcpTransport"/> (FR-008, contracts/quic-connect-retry.md). Before 064,
/// <c>QuicTransport.ConnectAsync</c> was single-shot, so a peer dialing a service box that was
/// still restarting failed instantly. Skip-guarded on <see cref="QuicTransport.IsSupported"/>,
/// matching the other QUIC suites.
/// </summary>
public class QuicConnectRetryTests
{
    private static bool QuicAvailable => QuicTransport.IsSupported;

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

    // T011a / US3 scenario 1 / SC-003 — the dial STARTS before anything is listening and still
    // succeeds once the listener arms inside the budget, with no dialer-side special handling.
    [Fact]
    public async Task Connect_BeforeListenerArms_RetriesAndSucceeds()
    {
        if (!QuicAvailable) return;
        int port = FreeUdpPort();
        var (cert, pin) = MakeCert("retry-parity");
        var transport = new QuicTransport(cert, pin);
        var addr = new LinkAddress("127.0.0.1", port);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Dial first — nothing is listening yet (pre-064 this threw immediately).
        var connectTask = transport.ConnectAsync(LinkScheme.Quic, addr, LinkOptions.Default, cts.Token);

        // Arm the listener a beat later, well inside the budget: the service-box restart window.
        await Task.Delay(1500, cts.Token);
        var listenTask = transport.ListenAsync(LinkScheme.Quic, addr, LinkOptions.Default, cts.Token);

        var listener = await listenTask;
        var connector = await connectTask;

        Assert.NotNull(connector);
        Assert.NotNull(listener);
        await connector.CloseAsync();
        await listener.CloseAsync();
        await connector.DisposeAsync();
        await listener.DisposeAsync();
    }

    // T011b / US3 scenario 2 — a listener that never arms fails ONLY when the budget is exhausted
    // (never an instant hard failure while budget remains), surfacing as cancellation the kernel
    // already reports through its existing establishment-failure path.
    [Fact]
    public async Task Connect_ListenerNeverArms_FailsOnlyAtBudgetExhaustion()
    {
        if (!QuicAvailable) return;
        int port = FreeUdpPort();
        var (cert, pin) = MakeCert("retry-budget");
        var transport = new QuicTransport(cert, pin);
        var addr = new LinkAddress("127.0.0.1", port);
        var budget = TimeSpan.FromSeconds(3);
        using var cts = new CancellationTokenSource(budget);

        var sw = Stopwatch.StartNew();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await transport.ConnectAsync(LinkScheme.Quic, addr, LinkOptions.Default, cts.Token));
        sw.Stop();

        // The point of the test: it kept retrying for the whole budget instead of failing at once.
        Assert.True(sw.Elapsed >= budget - TimeSpan.FromMilliseconds(500),
            $"connect gave up after {sw.Elapsed} — expected to retry for the full {budget} budget");
    }
}
