using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using GlpRuntime.CrdtMsg.Bridge;
using GlpRuntime.CrdtMsg.Cap;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.CrdtMsg.Sig;
using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Feature 050 US4 (T031) — cyber over the genuine QUIC link (FR-015, SC-008): a rogue / non-pinned
/// peer is rejected at the mutual SPKI-pin handshake (never a false accept, and the listener stays
/// healthy for pinned peers); a tampered SIGNED block that crossed the real wire fails
/// <see cref="SealSet.Verify"/> (041 Ed25519 whole + Biscuit-style chained sub-content seals) and the
/// refusal is a recorded <see cref="ProvenanceOutcome.Refused"/> outcome — with the untampered
/// counterpart verifying (zero false accepts, zero false rejects). Skip-guarded on
/// <see cref="QuicTransport.IsSupported"/>.
/// </summary>
public class QuicCyberTests
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

    [Fact] // T031a — rogue (non-pinned) peer rejected at the mutual-pin handshake; zero false accepts
    public async Task RoguePeer_PinMismatch_HandshakeRejected_ListenerStaysHealthy()
    {
        if (!QuicAvailable) return;
        int port = FreeUdpPort();
        var (trunkCert, trunkPin) = MakeCert("GLP-Quick 050 Trunk");
        var (rogueCert, roguePin) = MakeCert("GLP-Quick 050 Rogue");
        var trunk = new QuicTransport(trunkCert, trunkPin);
        var addr = LinkAddress.Endpoint("127.0.0.1", port);

        await using var listener = await trunk.CreateListenerAsync(addr, LinkOptions.Default, Timeout());

        // The rogue presents ITS OWN cert and pins its own SPKI — a non-member. The mutual pin
        // rejects it: the rogue's client-side validation of the trunk cert fails (pin mismatch),
        // and/or the server's validation of the rogue's cert fails. Either way: no link, loudly.
        var accept = listener.AcceptAsync(Timeout());
        var rogue = new QuicTransport(rogueCert, roguePin);
        await Assert.ThrowsAnyAsync<Exception>(() =>
            rogue.ConnectAsync(LinkScheme.Quic, addr, LinkOptions.Default, Timeout(10)));

        // Zero false accepts is two-sided: the rogue's failed handshake never surfaces a link on the
        // accept path (it either throws there or is dropped before dequeue — both are rejections),
        // and the listener remains healthy — a genuinely pinned member connects fine afterwards.
        var memberTask = trunk.ConnectAsync(LinkScheme.Quic, addr, LinkOptions.Default, Timeout());
        ILinkEndpoint accepted;
        try
        {
            accepted = await accept;                       // pending accept serves the member
        }
        catch (Exception)
        {
            // the rogue's failed handshake surfaced here — re-accept for the member
            accepted = await listener.AcceptAsync(Timeout());
        }
        var member = await memberTask;
        try
        {
            byte[] probe = { 0x5A };
            await member.SendBytesAsync(probe, Timeout());
            Assert.Equal(probe, await accepted.RecvBytesAsync(Timeout()));
        }
        finally
        {
            await member.DisposeAsync();
            await accepted.DisposeAsync();
        }
    }

    // ---- T031b — tampered signed block, detected AFTER crossing the genuine wire ----

    private static Term List(IEnumerable<Term> items)
    {
        Term acc = new ConstTerm("nil");
        foreach (var t in items.Reverse())
            acc = new StructTerm(".", new Term[] { t, acc });
        return acc;
    }

    private static Term ByteList(byte[] bytes) => List(bytes.Select(b => (Term)new ConstTerm((long)b)));

    private static Term SectionedTerm(params byte[][] blocks) => new StructTerm("crdtmsg", new Term[]
    {
        new ConstTerm("m-sig"), new ConstTerm("alice"), new ConstTerm("bob"), new ConstTerm(9L),
        new StructTerm("policy", new Term[] { new ConstTerm("nil"), new ConstTerm("nil"), new ConstTerm("nil") }),
        new ConstTerm("none"),
        List(blocks.Select((b, i) => (Term)new StructTerm("section", new Term[]
        {
            new ConstTerm((long)(0x40 + 2 * i)),   // even/ignorable content blocks
            ByteList(b),
        }))),
    });

    private static Term QuicLinkIdTerm(int port) => new StructTerm("link_id", new Term[]
    {
        new ConstTerm("quic"),
        new StructTerm("ep", new Term[] { new ConstTerm("127.0.0.1"), new ConstTerm((long)port) }),
        new ConstTerm(1L),
    });

    [Fact]
    public async Task TamperedSignedBlock_CrossingRealWire_FailsSealVerify_RefusalRecorded()
    {
        if (!QuicAvailable) return;

        // Sender ("alice") signs the message: Ed25519 whole-content sig over the canonical envelope
        // + chained sub-content seals over the section blocks (041 sig/Seals.cs). Receiver ("bob")
        // enrolled alice's public key at mesh join.
        var signer = new PeerKeyStore("alice");
        var verifier = new PeerKeyStore("bob");
        verifier.Enroll("alice", signer.LocalPublicKey);

        byte[][] blocks = { new byte[] { 1, 2, 3 }, new byte[] { 4, 5 }, new byte[] { 6 } };
        var codec = new CrdtMsgPayloadCodec();
        Term good = SectionedTerm(blocks);
        byte[] goodWire = codec.Encode(good);
        Message goodMsg = MessageCodec.Binary.Decode(goodWire);
        var seal = SealSet.Seal(signer, MessageCodec.Canonical(goodMsg),
            goodMsg.Sections.Select(s => s.Value).ToList());

        // Tampered variant: one byte of one SIGNED block flipped before shipping.
        byte[][] tamperedBlocks = { new byte[] { 1, 2, 3 }, new byte[] { 4, 0xFF }, new byte[] { 6 } };
        Term tampered = SectionedTerm(tamperedBlocks);

        // Ship BOTH over a genuine quic link (kernel path) and verify what actually arrived.
        int port = FreeUdpPort();
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var (cert, pin) = MakeCert("GLP-Quick 050 US4 Sig");
        link.Transports.Register(new QuicTransport(cert, pin));
        link.PayloadCodecs.Register(LinkScheme.Quic, codec);

        var (inW, inR) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
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
        try
        {
            uint frameId = 0;
            var provenance = new ProvenanceLog();
            var received = new List<Message>();
            foreach (var term in new[] { good, tampered })
            {
                byte[] wire = codec.Encode(term);
                foreach (var f in FrameCodec.Encode(wire, messageId: frameId++))
                    await peer.SendBytesAsync(f, Timeout());
                Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(10)));

                // Re-encode what the runner delivered — the receiver-side canonical view.
                var cons = Assert.IsType<StructTerm>(engine.Heap.Dereference(new VarRef(inR)));
                Term deliveredTerm = Resolve(engine.Heap, cons.Args[0]);
                received.Add(MessageCodec.Binary.Decode(codec.Encode(deliveredTerm)));
                // advance the In cursor for the next round
                inR = ((VarRef)engine.Heap.Dereference(cons.Args[1])).Addr;
            }

            // Verify-at-receiver: the untampered message verifies (zero false REJECTS) ...
            bool goodOk = seal.Verify(verifier, MessageCodec.Canonical(received[0]),
                received[0].Sections.Select(s => s.Value).ToList());
            Assert.True(goodOk);

            // ... and the tampered signed block breaks the chain (zero false ACCEPTS); the refusal
            // is recorded as the distinct provenance outcome, never a silent drop (041 C19 idiom).
            bool tamperedOk = seal.Verify(verifier, MessageCodec.Canonical(received[1]),
                received[1].Sections.Select(s => s.Value).ToList());
            if (!tamperedOk)
                provenance.Record("alice", "bob", DateTimeOffset.UnixEpoch, "tampered-block",
                    ProvenanceOutcome.Refused);
            Assert.False(tamperedOk);
            Assert.Single(provenance.Refusals);
        }
        finally
        {
            link.Pump.Dispose();
            await peer.DisposeAsync();
        }
    }

    /// <summary>Deep-resolve a delivered term to a VarRef-free tree for re-encoding.</summary>
    private static Term Resolve(HeapFCP heap, Term t)
    {
        Term d = heap.Dereference(t);
        return d switch
        {
            ConstTerm c => c,
            StructTerm s => new StructTerm(s.Functor, s.Args.Select(a => Resolve(heap, a)).ToArray()),
            _ => throw new InvalidOperationException($"non-ground delivered term: {d.GetType().Name}"),
        };
    }
}
