using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using GlpRuntime.CrdtMsg.Bridge;
using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Feature 050 US5 (T037/T038/T039) — the run concludes with GRACEFUL termination (FR-017/FR-018,
/// contract <c>mesh-test-harness.md</c>): drain in-flight → clean close on every link → orderly
/// teardown of the QUIC connection/stream so <see cref="ILinkEndpoint.RecvBytesAsync"/> returns
/// <c>null</c> at the peer, with zero crashes; an immediate re-run re-establishes on the same UDP
/// port with no leftover listener/connection (the port released); and a peer that vanishes mid-drain
/// still tears down gracefully with the fault reported on the monitor stream. Over a genuine
/// <see cref="QuicTransport"/> handshake (skip-guarded on <see cref="QuicTransport.IsSupported"/>).
/// </summary>
public class QuicTeardownTests
{
    private static bool QuicAvailable => QuicTransport.IsSupported;

    private static CancellationToken Timeout(int seconds = 20) =>
        new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

    private static int FreeUdpPort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    private static (X509Certificate2 cert, string pin) MakeCert()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=GLP-Quick 050 US5 Teardown", ec, HashAlgorithmName.SHA256);
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

    private static Term QuicLinkIdTerm(int port) => new StructTerm("link_id", new Term[]
    {
        new ConstTerm("quic"),
        new StructTerm("ep", new Term[] { new ConstTerm("127.0.0.1"), new ConstTerm((long)port) }),
        new ConstTerm(1L),
    });

    // The registry/reclaimer key is the GROUND LinkId parsed from the GLP term (nonce 1 — see
    // QuicLinkIdTerm), NOT the transport endpoint's internal per-connection nonce.
    private static LinkId ExpectedId(int port) =>
        new(LinkScheme.Quic, LinkAddress.Endpoint("127.0.0.1", port), LinkNonce.Int(1));

    private static Term List(IEnumerable<Term> items)
    {
        Term acc = new ConstTerm("nil");
        foreach (var t in items.Reverse())
            acc = new StructTerm(".", new Term[] { t, acc });
        return acc;
    }

    private static Term CrdtMsg(long seq) => new StructTerm("crdtmsg", new Term[]
    {
        new ConstTerm("ping"), new ConstTerm("a"), new ConstTerm("b"), new ConstTerm(seq),
        new StructTerm("policy", new Term[] { new ConstTerm("nil"), new ConstTerm("nil"), new ConstTerm("nil") }),
        new ConstTerm("none"),
        List(Array.Empty<Term>()),
    });

    private sealed record Wired(
        GlpRuntimeEngine Engine, LinkRuntime Link, ILinkEndpoint Peer, int InReader, int OutReader, int OutWriter);

    /// <summary>Establish a genuine quic connector link via the kernel path; keep the In reader + Out writer.</summary>
    private static async Task<Wired> SetupAsync(int port, QuicTransport transport)
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        link.Transports.Register(transport);
        link.PayloadCodecs.Register(LinkScheme.Quic, new CrdtMsgPayloadCodec());

        var (inW, inR) = engine.Heap.AllocateVariable();
        var (outW, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
        var args = new List<object?>
        {
            QuicLinkIdTerm(port), new ConstTerm("connector"),
            new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        };

        var listenTask = transport.ListenAsync(
            LinkScheme.Quic, LinkAddress.Endpoint("127.0.0.1", port), LinkOptions.Default, Timeout());
        var setup = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        Assert.Equal(BodyKernelResult.Success, setup(engine, args));
        var peer = await listenTask;
        return new Wired(engine, link, peer, inR, outR, outW);
    }

    [Fact] // T037 — graceful close: drain in-flight, close, peer sees null, teardown with no crash
    public async Task GracefulClose_DrainsThenClosesLink_PeerSeesNull_NoCrash()
    {
        if (!QuicAvailable) return;
        int port = FreeUdpPort();
        var (cert, pin) = MakeCert();
        var wired = await SetupAsync(port, new QuicTransport(cert, pin));
        var heap = wired.Engine.Heap;
        try
        {
            // In-flight: ship two envelopes, then bind Out = [] — the canonical graceful stream-end
            // close (LinkEstablish turns Out=[] into an ordered teardown with reason `eos`).
            var (_, t1R) = heap.AllocateVariable();
            var (w2, t2R) = heap.AllocateVariable();
            var head = new StructTerm(".", new Term[] { CrdtMsg(1), new VarRef(t1R) });
            _ = heap.BindVariable(wired.OutWriter, head);
            var second = new StructTerm(".", new Term[] { CrdtMsg(2), new VarRef(t2R) });
            _ = heap.BindVariable(TailWriter(heap, t1R), second);
            _ = heap.BindVariable(TailWriter(heap, t2R), new ConstTerm("nil")); // graceful close

            // The peer drains both in-flight envelopes, then reads null (our WS close crossed).
            int drained = 0;
            var reassembler = new FrameReassembler();
            while (true)
            {
                byte[]? frame = await wired.Peer.RecvBytesAsync(Timeout());
                if (frame is null) break;                       // graceful end-of-link
                if (reassembler.Accept(FrameCodec.ParseFrame(frame)) is not null)
                    drained++;
            }
            Assert.Equal(2, drained);                            // in-flight drained, none lost

            // Teardown ran distributed GC: the registry returned to baseline, no crash.
            Assert.True(wired.Link.Reclaimer.IsReclaimed(ExpectedId(port)));
            Assert.Equal(0, wired.Link.Links.Count);
        }
        finally
        {
            wired.Link.Pump.Dispose();
            await wired.Peer.DisposeAsync();
        }
    }

    [Fact] // T038 — re-run after teardown re-establishes on the same port (no leftover listener/conn)
    public async Task RerunAfterTeardown_ReestablishesOnSamePort_NoLeftovers()
    {
        if (!QuicAvailable) return;
        int port = FreeUdpPort();
        var (cert, pin) = MakeCert();

        // First run: establish, then abrupt link_close via the kernel → full teardown + GC.
        var first = await SetupAsync(port, new QuicTransport(cert, pin));
        try
        {
            var closeArgs = new List<object?> { QuicLinkIdTerm(port), new ConstTerm("abrupt") };
            var close = first.Engine.BodyKernels.Lookup(LinkKernels.LinkCloseName, LinkKernels.LinkCloseArity)!;
            Assert.Equal(BodyKernelResult.Success, close(first.Engine, closeArgs));
            Assert.True(first.Link.Reclaimer.IsReclaimed(ExpectedId(port)));
            Assert.Equal(0, first.Link.Links.Count);             // registry back to baseline
        }
        finally
        {
            first.Link.Pump.Dispose();
            await first.Peer.DisposeAsync();
        }

        // The UDP port released: a fresh listener binds it again with no "address in use".
        var second = await SetupAsync(port, new QuicTransport(cert, pin));
        try
        {
            Assert.Equal(1, second.Link.Links.Count);            // clean re-establishment, no leftovers
        }
        finally
        {
            second.Link.Pump.Dispose();
            await second.Peer.DisposeAsync();
        }
    }

    [Fact] // T039 — peer disappears mid-drain → fault reported via monitor stream, teardown completes
    public async Task PeerVanishesMidDrain_FaultReported_TeardownStillCompletes()
    {
        if (!QuicAvailable) return;
        int port = FreeUdpPort();
        var (cert, pin) = MakeCert();
        var wired = await SetupAsync(port, new QuicTransport(cert, pin));

        // Register an independent monitor cursor for the link (link_monitor/2 kernel path).
        var (monW, monR) = wired.Engine.Heap.AllocateVariable();
        var monArgs = new List<object?> { QuicLinkIdTerm(port), new VarRef(monW) };
        var monitor = wired.Engine.BodyKernels.Lookup(LinkKernels.LinkMonitorName, LinkKernels.LinkMonitorArity)!;
        Assert.Equal(BodyKernelResult.Success, monitor(wired.Engine, monArgs));

        var heap = wired.Engine.Heap;
        try
        {
            // The peer vanishes mid-run. Let the QUIC connection teardown propagate on loopback.
            await wired.Peer.DisposeAsync();
            await Task.Delay(750);

            // In-flight data still bound for the wire: shipping it into the dead connection fails at
            // the endpoint, which raises the out-of-band fault. The synchronous egress bind may
            // surface that transport exception to the binder — that is itself a loud signal, tolerate
            // it; the reliability contract under test is the MONITOR-stream fault fan-out (FR-016).
            var (_, tailR) = heap.AllocateVariable();
            var cons = new StructTerm(".", new Term[] { CrdtMsg(3), new VarRef(tailR) });
            try { _ = heap.BindVariable(wired.OutWriter, cons); }
            catch (Exception) { /* egress surfaced the transport failure synchronously — acceptable */ }

            // The fault fans out to the monitor stream — a bound ground fault term
            // (closed/tempFail/permFail), reported and never swallowed. The stream opens with an
            // `ok` baseline (ConstTerm), so walk past it to the fault struct term.
            Term? fault = null;
            var faultFunctors = new[] { "closed", "tempFail", "permFail" };
            for (int i = 0; i < 30 && fault is null; i++)
            {
                if (!wired.Engine.InboundPump!.TryApplyNext(TimeSpan.FromMilliseconds(500)))
                    continue;
                fault = FindFault(heap, monR, faultFunctors);
            }
            var faultStruct = Assert.IsType<StructTerm>(fault);
            Assert.Contains(faultStruct.Functor, faultFunctors);

            // Teardown still completes gracefully: an explicit close after the fault is a clean no-op
            // (idempotent GC), not a crash.
            var closeArgs = new List<object?> { QuicLinkIdTerm(port), new ConstTerm("abrupt") };
            var close = wired.Engine.BodyKernels.Lookup(LinkKernels.LinkCloseName, LinkKernels.LinkCloseArity)!;
            close(wired.Engine, closeArgs); // may succeed or abort-if-already-reclaimed; never throws
            Assert.True(wired.Link.Reclaimer.IsReclaimed(ExpectedId(port)));
        }
        finally
        {
            wired.Link.Pump.Dispose();
        }
    }

    /// <summary>Resolve the writer behind a freshly-allocated tail reader, for chaining stream conses.</summary>
    private static int TailWriter(HeapFCP heap, int tailReaderAddr) =>
        heap.TryWriterForReader(tailReaderAddr) ?? throw new InvalidOperationException("no paired tail writer");

    /// <summary>Walk the monitor stream (a <c>.</c>/2 spine, opening with an <c>ok</c> baseline)
    /// and return the first element whose functor names a fault, or null if none has arrived yet.</summary>
    private static Term? FindFault(HeapFCP heap, int streamReaderAddr, string[] faultFunctors)
    {
        Term cur = heap.Dereference(new VarRef(streamReaderAddr));
        while (cur is StructTerm cons && cons.Functor == "." && cons.Args.Count == 2)
        {
            Term head = heap.Dereference(cons.Args[0]);
            if (head is StructTerm s && faultFunctors.Contains(s.Functor))
                return s;
            cur = heap.Dereference(cons.Args[1]);
        }
        return null;
    }
}
