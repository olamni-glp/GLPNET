using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using GlpRuntime.CrdtMsg.Bridge;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Headers;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Runtime;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Feature 050 US2 (T013/T014/T015) — the <c>"quic"</c> link's L5 wire payload is a genuine 041
/// crdtmsg envelope, encoded/decoded by <see cref="CrdtMsgPayloadCodec"/> through the 050
/// <see cref="IPayloadCodec"/> seam. The GLP program ships a structured <c>crdtmsg/7</c> ground term
/// (contract addendum A1, Option A); the codec transcribes it to a <see cref="Message"/> and back
/// LOSSLESSLY, the 025 <c>FrameCodec</c> frames the envelope bytes UNCHANGED, and malformed input
/// fails loud (FR-005/6/7). Rich-text is carried (not applied) — a rich-text edit op rides as opaque
/// 041 <c>OpCodec</c> bytes in a section and survives byte-for-byte incl. marks the link never reads
/// (addendum A3). The over-the-wire tests use a real <c>System.Net.Quic</c>/MsQuic loopback handshake
/// (skip-guarded on <see cref="QuicTransport.IsSupported"/>); T014 is a pure codec loud-fail unit.
/// </summary>
public class CrdtMsgOnLinkTests
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
        var req = new CertificateRequest("CN=GLP-Quick 050 US2 Test", ec, HashAlgorithmName.SHA256);
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

    // --- GLP ground-term builders (contract addendum A1) ---

    /// <summary>A GLP list term: [x0, x1, ...] as StructTerm(".", [h, tail]) ending in nil.</summary>
    private static Term List(IEnumerable<Term> items)
    {
        Term acc = new ConstTerm("nil");
        foreach (var t in items.Reverse())
            acc = new StructTerm(".", new Term[] { t, acc });
        return acc;
    }

    private static Term Atoms(params string[] xs) => List(xs.Select(x => (Term)new ConstTerm(x)));

    private static Term ByteList(byte[] bytes) => List(bytes.Select(b => (Term)new ConstTerm((long)b)));

    private static Term SectionTerm(long type, byte[] value) =>
        new StructTerm("section", new Term[] { new ConstTerm(type), ByteList(value) });

    /// <summary>crdtmsg(MsgId, From, To, Seq, policy(T,W,E), CrdtModel, Sections).</summary>
    private static Term CrdtMsgTerm(
        string msgId, string from, string to, long seq,
        Term policy, string crdtModel, IEnumerable<Term> sections) =>
        new StructTerm("crdtmsg", new Term[]
        {
            new ConstTerm(msgId), new ConstTerm(from), new ConstTerm(to), new ConstTerm(seq),
            policy, new ConstTerm(crdtModel), List(sections),
        });

    private static Term EmptyPolicy() =>
        new StructTerm("policy", new Term[] { new ConstTerm("nil"), new ConstTerm("nil"), new ConstTerm("nil") });

    /// <summary>A genuine 041 CRDT op whose opaque body stands in for a rich-text edit carrying an
    /// UNKNOWN formatting mark — the link carries it verbatim (never applies Fugue/Peritext).</summary>
    private static byte[] RichTextEditOpBytes()
    {
        byte[] editBody = System.Text.Encoding.UTF8.GetBytes("seq_insert@3 mark_add(wavy_underline/unknown)");
        var op = Op.Create(new Dot("alice", 1L), Array.Empty<Dot>(), editBody);
        return OpCodec.Encode(op);
    }

    /// <summary>A representative rich message: policy, an op-based rich-text op section (must-understand,
    /// carried), plus an unknown-ignorable section and a greasing section (skip path, FR-006).</summary>
    private static Term SampleRichTerm(out byte[] richOpBytes)
    {
        richOpBytes = RichTextEditOpBytes();
        return CrdtMsgTerm(
            "m-42", "alice", "@editors", 7,
            new StructTerm("policy", new Term[] { Atoms("bob", "carol"), Atoms("relay1"), Atoms("mallory") }),
            "op_based",
            new[]
            {
                SectionTerm(UnifiedHeader.OpSectionType, richOpBytes),   // 0x13 odd/must-understand (op)
                SectionTerm(0x40, new byte[] { 0x00, 0x7F, 0x80, 0xFF }), // even/ignorable, full byte range
                SectionTerm(TlvSection.GreaseTypeNumber, new byte[] { 0xDE, 0xAD }), // grease (skip path)
            });
    }

    // --- Real-quic connector harness (mirrors QuicLinkOneBindTests) with the crdtmsg codec wired ---

    private sealed record Wired(
        GlpRuntimeEngine Engine, LinkRuntime Link, ILinkEndpoint Peer, int InReader, int OutWriter);

    private static async Task<Wired> SetupQuicConnectorAsync(IPayloadCodec codec)
    {
        int port = FreeUdpPort();
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var (cert, pin) = MakeCert();
        link.Transports.Register(new QuicTransport(cert, pin));
        link.PayloadCodecs.Register(LinkScheme.Quic, codec); // the "quic" link speaks crdtmsg (T018 shape)

        var (inW, inR) = engine.Heap.AllocateVariable();
        var (outW, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
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
        return new Wired(engine, link, peer, inR, outW);
    }

    [Fact] // T013 — crdtmsg round-trip over a real quic link, incl. a rich-text edit op, lossless
    public async Task CrdtMsg_RoundTripsLosslessly_OverRealQuicLink_InclRichTextOp()
    {
        if (!QuicAvailable) return;
        var codec = new CrdtMsgPayloadCodec();
        var sent = SampleRichTerm(out _);
        var wired = await SetupQuicConnectorAsync(codec);
        try
        {
            // Peer encodes the crdtmsg envelope and ships it framed; the link's codec decodes it and
            // extends In = [decoded | In']. TryApplyNext applies the inbound on the runner thread.
            byte[] wire = codec.Encode(sent);
            foreach (var f in FrameCodec.Encode(wire, messageId: 0))
                await wired.Peer.SendBytesAsync(f);
            Assert.True(wired.Engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(5)));

            var cons = Assert.IsType<StructTerm>(wired.Engine.Heap.Dereference(new VarRef(wired.InReader)));
            Assert.Equal(".", cons.Functor);
            Term received = wired.Engine.Heap.Dereference(cons.Args[0]);
            AssertTermEqual(sent, received); // lossless incl. the op section + unknown-ignorable sections
        }
        finally
        {
            wired.Link.Pump.Dispose();
            await wired.Peer.DisposeAsync();
        }
    }

    [Fact] // T014 — malformed inputs are rejected loud-fail (bad version, unknown must-understand, truncation, trailing)
    public void CrdtMsg_MalformedInputs_RejectedLoudFail()
    {
        var codec = new CrdtMsgPayloadCodec();
        byte[] good = codec.Encode(SampleRichTerm(out _));

        // (a) bad codec-format version byte (TIER-2 hard gate)
        byte[] badVersion = (byte[])good.Clone();
        badVersion[0] = 0xFE;
        Assert.Throws<CrdtMsgException>(() => codec.Decode(badVersion));

        // (b) unknown MUST-UNDERSTAND (odd) section the codec does not understand
        byte[] mustUnderstand = codec.Encode(CrdtMsgTerm(
            "m-x", "a", "b", 0, EmptyPolicy(), "none",
            new[] { SectionTerm(0x99, new byte[] { 1, 2, 3 }) })); // 0x99 odd, not UnifiedHeader.OpSectionType
        Assert.Throws<CrdtMsgException>(() => codec.Decode(mustUnderstand));

        // (c) truncation
        byte[] truncated = good[..(good.Length - 3)];
        Assert.Throws<CrdtMsgException>(() => codec.Decode(truncated));

        // (d) trailing bytes after a complete message
        byte[] trailing = new byte[good.Length + 1];
        Array.Copy(good, trailing, good.Length);
        Assert.Throws<CrdtMsgException>(() => codec.Decode(trailing));

        // and a non-crdtmsg ground term is a program error on a "quic" link (fail-closed, FR-005)
        Assert.Throws<CrdtMsgException>(() => codec.Encode(new ConstTerm(10L)));
    }

    [Fact] // T015 — the L5 payload observed on the wire is a decodable crdtmsg envelope; zero ad-hoc strings
    public async Task WirePayload_IsCrdtMsgEnvelope_NotAdHocString()
    {
        if (!QuicAvailable) return;
        var codec = new CrdtMsgPayloadCodec();
        var sent = SampleRichTerm(out byte[] richOpBytes);
        var wired = await SetupQuicConnectorAsync(codec);
        try
        {
            // Bind the Out writer to [msg | _]: the egress drainer ships the ground crdtmsg term over the
            // real wire, encoded by the link's IPayloadCodec. Capture the framed bytes the peer receives.
            var heap = wired.Engine.Heap;
            var (_, tailR) = heap.AllocateVariable();
            var cons = new StructTerm(".", new Term[] { sent, new VarRef(tailR) });
            // Binding the Out writer fires the armed egress OnBind callback synchronously → ShipGround
            // encodes via the link's codec and writes the framed bytes to the endpoint. Any reactivated
            // goals from the bind are unrelated to egress (no goal suspends on Out here) — ignore them.
            _ = heap.BindVariable(wired.OutWriter, cons);

            byte[] payload = await ReassembleOneAsync(wired.Peer);

            // The observed L5 payload decodes as a well-formed crdtmsg envelope (SC-002) ...
            Message m = MessageCodec.Binary.Decode(payload);
            Assert.Equal(PayloadType.CrdtMessage, m.PayloadType);
            Assert.Equal("m-42", m.Header.MsgId);
            Assert.Equal(CrdtModel.OpBased, m.CrdtModel);
            Assert.Equal(richOpBytes, UnifiedHeader.OpBytes(m)); // the rich-text op crossed verbatim

            // ... and it is NOT the default ground-relay ad-hoc blob: that blob does not decode as an envelope.
            byte[] adHoc = DefaultPayloadCodec.Instance.Encode(new ConstTerm(10L));
            Assert.Throws<CrdtMsgException>(() => MessageCodec.Binary.Decode(adHoc));
        }
        finally
        {
            wired.Link.Pump.Dispose();
            await wired.Peer.DisposeAsync();
        }
    }

    /// <summary>Pull frames from the peer and reassemble exactly one message payload (single or fragmented).</summary>
    private static async Task<byte[]> ReassembleOneAsync(ILinkEndpoint peer)
    {
        var reassembler = new FrameReassembler();
        var ct = Timeout(5);
        while (true)
        {
            byte[]? frame = await peer.RecvBytesAsync(ct);
            Assert.NotNull(frame);
            byte[]? payload = reassembler.Accept(FrameCodec.ParseFrame(frame!));
            if (payload is not null)
                return payload;
        }
    }

    // --- structural ground-term equality (int/long normalized) ---
    private static void AssertTermEqual(Term expected, Term actual)
    {
        switch (expected)
        {
            case ConstTerm ce:
                var ca = Assert.IsType<ConstTerm>(actual);
                Assert.Equal(Norm(ce.Value), Norm(ca.Value));
                break;
            case StructTerm se:
                var sa = Assert.IsType<StructTerm>(actual);
                Assert.Equal(se.Functor, sa.Functor);
                Assert.Equal(se.Args.Count, sa.Args.Count);
                for (int i = 0; i < se.Args.Count; i++)
                    AssertTermEqual(se.Args[i], sa.Args[i]);
                break;
            default:
                Assert.Fail($"unexpected term kind {expected.GetType().Name}");
                break;
        }
    }

    private static object? Norm(object? v) => v switch { int i => (long)i, _ => v };
}
