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
/// Feature 050 US4 (T030) — reliability over the genuine QUIC link (FR-016, contract
/// <c>mesh-test-harness.md</c>): duplicate suppression (frame <c>msg_id</c> + the per-link
/// <see cref="InboundOrdering"/> sequence high-water), exactly-once remote reader reactivation
/// (a redelivered frame extends <c>In</c> zero further times), and fault reporting via the 025
/// monitor stream (a send to a vanished peer surfaces <c>tempFail(LinkId, Reason)</c> on the
/// establishment <c>Faults</c> stream — never swallowed). Skip-guarded on
/// <see cref="QuicTransport.IsSupported"/>; the wire payload is the post-US2 crdtmsg envelope.
/// </summary>
public class QuicReliabilityTests
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
        var req = new CertificateRequest("CN=GLP-Quick 050 US4 Reliability", ec, HashAlgorithmName.SHA256);
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

    private static Term List(IEnumerable<Term> items)
    {
        Term acc = new ConstTerm("nil");
        foreach (var t in items.Reverse())
            acc = new StructTerm(".", new Term[] { t, acc });
        return acc;
    }

    /// <summary>A minimal well-formed crdtmsg/7 ground term (contract addendum A1).</summary>
    private static Term CrdtMsg(string msgId, long seq) => new StructTerm("crdtmsg", new Term[]
    {
        new ConstTerm(msgId), new ConstTerm("alice"), new ConstTerm("bob"), new ConstTerm(seq),
        new StructTerm("policy", new Term[] { new ConstTerm("nil"), new ConstTerm("nil"), new ConstTerm("nil") }),
        new ConstTerm("none"),
        List(Array.Empty<Term>()),
    });

    private sealed record Wired(
        GlpRuntimeEngine Engine, LinkRuntime Link, ILinkEndpoint Peer, int InReader, int OutWriter, int FaultsReader);

    /// <summary>Kernel-path quic connector (mirrors CrdtMsgOnLinkTests) keeping the Faults reader.</summary>
    private static async Task<Wired> SetupAsync()
    {
        int port = FreeUdpPort();
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var (cert, pin) = MakeCert();
        link.Transports.Register(new QuicTransport(cert, pin));
        link.PayloadCodecs.Register(LinkScheme.Quic, new CrdtMsgPayloadCodec());

        var (inW, inR) = engine.Heap.AllocateVariable();
        var (outW, outR) = engine.Heap.AllocateVariable();
        var (faultsW, faultsR) = engine.Heap.AllocateVariable();
        var args = new List<object?>
        {
            QuicLinkIdTerm(port), new ConstTerm("connector"),
            new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        };

        var listenTask = link.Transports.Select(LinkScheme.Quic)
            .ListenAsync(LinkScheme.Quic, LinkAddress.Endpoint("127.0.0.1", port), LinkOptions.Default, Timeout());
        var setup = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        Assert.Equal(BodyKernelResult.Success, setup(engine, args));
        var peer = await listenTask;
        return new Wired(engine, link, peer, inR, outW, faultsR);
    }

    [Fact] // T030a — duplicate suppression + exactly-once reader reactivation over the real wire
    public async Task DuplicateFrames_SuppressedByMsgIdSeq_ReaderExtendsExactlyOnce()
    {
        if (!QuicAvailable) return;
        var codec = new CrdtMsgPayloadCodec();
        var wired = await SetupAsync();
        try
        {
            // Ship the SAME framed message (msg_id/seq 0) twice — at-least-once redelivery.
            byte[] wire = codec.Encode(CrdtMsg("m-dup", 1));
            for (int round = 0; round < 2; round++)
                foreach (var f in FrameCodec.Encode(wire, messageId: 0))
                    await wired.Peer.SendBytesAsync(f, Timeout());

            // Exactly ONE In-stream extension: the duplicate is an idempotent no-op at the
            // transport-level dedup (InboundOrdering high-water), so the reader reactivates once.
            Assert.True(wired.Engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(10)));
            var cons = Assert.IsType<StructTerm>(wired.Engine.Heap.Dereference(new VarRef(wired.InReader)));
            Assert.Equal(".", cons.Functor);
            Assert.False(wired.Engine.InboundPump!.TryApplyNext(TimeSpan.FromMilliseconds(300))); // no 2nd delivery

            // The tail is still an unbound stream hole (exactly-once: no duplicate cons behind it).
            Assert.IsType<VarRef>(wired.Engine.Heap.Dereference(cons.Args[1]));

            // The link stays healthy: the NEXT message id delivers normally.
            byte[] wire2 = codec.Encode(CrdtMsg("m-next", 2));
            foreach (var f in FrameCodec.Encode(wire2, messageId: 1))
                await wired.Peer.SendBytesAsync(f, Timeout());
            Assert.True(wired.Engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(10)));
        }
        finally
        {
            wired.Link.Pump.Dispose();
            await wired.Peer.DisposeAsync();
        }
    }

    [Fact] // T030b — fault reporting: a send to a vanished peer surfaces tempFail on the Faults stream
    public async Task SendToVanishedPeer_ReportsTempFailOnMonitorStream_NeverSwallowed()
    {
        if (!QuicAvailable) return;
        var wired = await SetupAsync();
        try
        {
            // Peer vanishes. Our recv loop observes the close (graceful null → In ends with nil).
            await wired.Peer.DisposeAsync();
            Assert.True(wired.Engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(10)));

            // Let the QUIC CONNECTION_CLOSE finish propagating on loopback so the egress write
            // below fails deterministically (the WS close frame precedes the connection teardown).
            await Task.Delay(750);

            // A message shipped into the vanished link: egress send fails, the endpoint raises the
            // out-of-band fault signal, and the pump fans tempFail(LinkId, Reason) onto the
            // establishment Faults stream (FR-016 — reported, never swallowed). The egress bind
            // itself may surface the transport exception to the binder; that exception is the
            // caller's loud signal, not the reliability contract under test — tolerate either.
            var heap = wired.Engine.Heap;
            var (_, tailR) = heap.AllocateVariable();
            var cons = new StructTerm(".", new Term[] { CrdtMsg("m-lost", 3), new VarRef(tailR) });
            try
            {
                _ = heap.BindVariable(wired.OutWriter, cons);
            }
            catch (Exception)
            {
                // the synchronous egress path surfaced the transport failure — acceptable and loud
            }

            // The fault term arrives on the runner thread via the pump.
            Term? fault = null;
            for (int i = 0; i < 20 && fault is null; i++)
            {
                if (!wired.Engine.InboundPump!.TryApplyNext(TimeSpan.FromMilliseconds(500)))
                    continue;
                var f = heap.Dereference(new VarRef(wired.FaultsReader));
                if (f is StructTerm s && s.Functor == "." )
                    fault = heap.Dereference(s.Args[0]);
            }

            var faultStruct = Assert.IsType<StructTerm>(fault);
            Assert.Equal("tempFail", faultStruct.Functor);   // Transient → tempFail(LinkId, Reason)
            Assert.Equal(2, faultStruct.Args.Count);
        }
        finally
        {
            wired.Link.Pump.Dispose();
        }
    }
}
