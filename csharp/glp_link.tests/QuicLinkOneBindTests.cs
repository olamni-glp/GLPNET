using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Feature 050 US1 (T007/T008) — a genuine QUIC link established through the 025 <c>'_link_setup'</c>
/// KERNEL PATH (not the raw transport directly), over which one writer→reader bind crosses the real
/// wire and reactivates a suspended reader EXACTLY ONCE (SC-001/FR-004). The handshake is a real
/// <c>System.Net.Quic</c>/MsQuic handshake over loopback UDP (real datagrams, TLS 1.3, ALPN h3 — a
/// genuine-QUIC endpoint, not a loopback shim; analyze note A2). Skip-guarded on
/// <see cref="QuicTransport.IsSupported"/> (FR-001). The two-physical-host LAN run (T043) is the
/// real-wire acceptance proof; this is the hermetic same-host verification.
/// </summary>
public class QuicLinkOneBindTests
{
    private static bool QuicAvailable => QuicTransport.IsSupported;

    private static CancellationToken Timeout(int seconds = 15) =>
        new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

    private static int FreeUdpPort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    private static (X509Certificate2 cert, string pin) MakeCert()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=GLP-Quick 050 Test", ec, HashAlgorithmName.SHA256);
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

    /// <summary>link_id("quic", ep("127.0.0.1", Port), 1) — the ground LinkId the GLP wrapper passes.</summary>
    private static Term QuicLinkIdTerm(int port) => new StructTerm("link_id", new Term[]
    {
        new ConstTerm("quic"),
        new StructTerm("ep", new Term[] { new ConstTerm("127.0.0.1"), new ConstTerm((long)port) }),
        new ConstTerm(1L),
    });

    /// <summary>
    /// Establish a connector QUIC link via the <c>'_link_setup'</c> kernel against a parked genuine
    /// QUIC listener; keep the In-stream reader (the cell a consumer's <c>link_recv</c> suspends on).
    /// </summary>
    private static async Task<(GlpRuntimeEngine engine, LinkRuntime link, ILinkEndpoint peer, int inReader)>
        SetupQuicConnectorAsync()
    {
        int port = FreeUdpPort();
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var (cert, pin) = MakeCert();
        link.Transports.Register(new QuicTransport(cert, pin));

        var (inW, inR) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
        var args = new List<object?>
        {
            QuicLinkIdTerm(port), new ConstTerm("connector"),
            new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        };

        // Park the genuine QUIC listener (the "other host"). The connector kernel BLOCKS the test
        // thread on the real handshake (LinkSetupKernel.Establish → ConnectAsync.GetResult); the
        // parked listener accepts on the thread pool, so the two rendezvous with no self-deadlock.
        var listenTask = link.Transports.Select(LinkScheme.Quic)
            .ListenAsync(LinkScheme.Quic, LinkAddress.Endpoint("127.0.0.1", port), LinkOptions.Default, Timeout());
        var setup = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        Assert.Equal(BodyKernelResult.Success, setup(engine, args));
        var peer = await listenTask;
        return (engine, link, peer, inR);
    }

    /// <summary>Ship one ground term from the peer at the given per-link sequence number.</summary>
    private static async Task PeerSendAsync(ILinkEndpoint peer, Term term, uint seq)
    {
        var payload = new PayloadSerializer("peer").SerializeAgentMessage(term);
        foreach (var f in FrameCodec.Encode(payload, messageId: seq))
            await peer.SendBytesAsync(f);
    }

    [Fact] // T007
    public async Task QuicKernelPath_SuspendedReader_ReactivatedExactlyOnceOverTheRealWire()
    {
        if (!QuicAvailable) return; // FR-001 gate — no QUIC on this host, nothing to verify
        var (engine, link, peer, inReader) = await SetupQuicConnectorAsync();
        try
        {
            // Nothing arrived yet ⇒ the In head reader is UNBOUND, so a consumer's link_recv
            // SUSPENDS (three-valued: an unarrived value is a suspend, never a fail). Simulate it.
            Assert.IsType<VarRef>(engine.Heap.Dereference(new VarRef(inReader)));
            var record = new SuspensionRecord(goalId: 7, resumePC: 0);
            engine.Heap.SuspendOnReader(inReader, record);
            Assert.True(record.Armed);
            Assert.True(engine.Gq.IsEmpty);

            // One writer→reader bind crosses the genuine QUIC wire; the ingress extends
            // In = [world | In'] and reactivates the suspended reader EXACTLY ONCE (SC-001/FR-051).
            await PeerSendAsync(peer, new ConstTerm("world"), seq: 0);
            Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(5)));

            Assert.Equal(1, engine.Gq.Length);   // reactivated once
            Assert.False(record.Armed);          // disarmed → cannot fire again
            var cons = Assert.IsType<StructTerm>(engine.Heap.Dereference(new VarRef(inReader)));
            Assert.Equal(".", cons.Functor);
            Assert.Equal("world", Assert.IsType<ConstTerm>(engine.Heap.Dereference(cons.Args[0])).Value);

            // A duplicate of the same seq is absorbed by the per-link dedup — no SECOND
            // reactivation (FR-016), so exactly-once holds.
            await PeerSendAsync(peer, new ConstTerm("world"), seq: 0);
            Assert.False(engine.InboundPump!.TryApplyNext(TimeSpan.FromMilliseconds(300)));
            Assert.Equal(1, engine.Gq.Length);
        }
        finally
        {
            link.Pump.Dispose();
            await peer.DisposeAsync();
        }
    }

    [Fact] // T008 (part A — all hosts): a quic link is served ONLY by the QUIC leaf, no downgrade
    public void QuicScheme_ServedOnlyByQuicLeaf_NoTcpLoopbackFallback()
    {
        var (cert, pin) = MakeCert();
        var registry = new TransportRegistry();
        registry.Register(new TcpTransport());
        registry.Register(new LoopbackTransport());
        var quic = new QuicTransport(cert, pin);
        registry.Register(quic);
        Assert.Same(quic, registry.Select(LinkScheme.Quic));
    }

    [Fact] // T008 (part B — unsupported hosts only): loud fault, never a silent downgrade (FR-002)
    public async Task QuicUnsupportedHost_ListenAndConnect_ThrowLoud_NoFallback()
    {
        if (QuicAvailable) return; // meaningful only where the platform lacks QUIC
        var (cert, pin) = MakeCert();
        var transport = new QuicTransport(cert, pin);
        var addr = LinkAddress.Endpoint("127.0.0.1", FreeUdpPort());
        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => transport.ListenAsync(LinkScheme.Quic, addr, LinkOptions.Default, Timeout(5)));
        await Assert.ThrowsAsync<PlatformNotSupportedException>(
            () => transport.ConnectAsync(LinkScheme.Quic, addr, LinkOptions.Default, Timeout(5)));
    }
}
