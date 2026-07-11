using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using GlpRuntime.CrdtMsg.Bridge;
using GlpRuntime.CrdtMsg.Cap;
using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Headers;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Feature 050 US3 (T022/T023/T024) — macaroons gate link establishment and maintenance,
/// verify-before-act (FR-008/FR-009, contract <c>capability-gating.md</c>). The static macaroon rides
/// in the envelope capability slot (<see cref="CapabilitySlot"/> section <c>0x20</c>, additive-optional
/// v2 — research D-2 resolved: the 041 T048 slot is a TLV <b>section</b>, which the binary canonical
/// surface already carries verbatim, so no 041 codec change and no JSON stopgap is needed).
/// Establishment is gated in <c>LinkEstablish</c> BEFORE the transport endpoint is opened; inbound
/// gated actions (delivery) re-verify the slot in the link's payload codec. Refusals fail closed,
/// are recorded as <see cref="ProvenanceOutcome.Refused"/>, and never crash the run.
/// </summary>
public class MacaroonGateTests
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
        var req = new CertificateRequest("CN=GLP-Quick 050 US3 Test", ec, HashAlgorithmName.SHA256);
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

    // --- GLP ground-term builders (contract addendum A1, mirrors CrdtMsgOnLinkTests) ---

    private static Term List(IEnumerable<Term> items)
    {
        Term acc = new ConstTerm("nil");
        foreach (var t in items.Reverse())
            acc = new StructTerm(".", new Term[] { t, acc });
        return acc;
    }

    private static Term EmptyPolicy() =>
        new StructTerm("policy", new Term[] { new ConstTerm("nil"), new ConstTerm("nil"), new ConstTerm("nil") });

    private static Term SampleTerm() => new StructTerm("crdtmsg", new Term[]
    {
        new ConstTerm("m-1"), new ConstTerm("alice"), new ConstTerm("bob"), new ConstTerm(1L),
        EmptyPolicy(), new ConstTerm("none"),
        List(new Term[]
        {
            new StructTerm("section", new Term[] { new ConstTerm(0x40L), List(new Term[] { new ConstTerm(7L) }) }),
        }),
    });

    // --- macaroon material (041 idioms: CapabilityTests) ---

    private static readonly byte[] RootKey = RandomNumberGenerator.GetBytes(32);

    /// <summary>The 041 numeric-expiry idiom: valid while the gate's clock exceeds 100.</summary>
    private static Macaroon TimeCaveatedCap() =>
        Macaroon.Create(RootKey, "glpquick", "cap-link").AddCaveat(new Caveat("expires", ">", "100"));

    private static MacaroonLinkGate Gate(Macaroon? presented, long nowUnixSeconds = 150) =>
        new(RootKey, presented, () => nowUnixSeconds);

    // --- establishment harness: the genuine kernel path ('_link_setup') with the gate registered ---

    private sealed record Rig(
        GlpRuntimeEngine Engine, LinkRuntime Link, int InReader, int OutWriter, int Port);

    /// <summary>Build an engine with QuicTransport + the gate (and gated codec) registered, and run
    /// the '_link_setup' kernel as a connector. No listener is started — a REFUSED establishment
    /// must fail closed BEFORE any transport endpoint is opened, so none is needed.</summary>
    private static BodyKernelResult TryEstablish(MacaroonLinkGate gate, out Rig rig)
    {
        int port = FreeUdpPort();
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var (cert, pin) = MakeCert();
        link.Transports.Register(new QuicTransport(cert, pin));
        link.PayloadCodecs.Register(LinkScheme.Quic, new CrdtMsgPayloadCodec(gate));
        link.CapabilityGates.Register(LinkScheme.Quic, gate);

        var (inW, inR) = engine.Heap.AllocateVariable();
        var (outW, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
        var args = new List<object?>
        {
            QuicLinkIdTerm(port), new ConstTerm("connector"),
            new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        };
        var setup = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        rig = new Rig(engine, link, inR, outW, port);
        return setup(engine, args);
    }

    [Fact] // T022 — valid macaroon, all caveats satisfied → establishment proceeds (over a real handshake)
    public async Task ValidMacaroon_AllCaveatsSatisfied_EstablishmentProceeds()
    {
        if (!QuicAvailable) return;
        int port = FreeUdpPort();
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var (cert, pin) = MakeCert();
        var gate = Gate(TimeCaveatedCap()); // clock 150 > caveat 100 ⇒ satisfied
        link.Transports.Register(new QuicTransport(cert, pin));
        link.PayloadCodecs.Register(LinkScheme.Quic, new CrdtMsgPayloadCodec(gate));
        link.CapabilityGates.Register(LinkScheme.Quic, gate);

        var (inW, _) = engine.Heap.AllocateVariable();
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
        try
        {
            Assert.Equal(BodyKernelResult.Success, setup(engine, args));   // the action proceeds (SC-003)
            var peer = await listenTask;
            await peer.DisposeAsync();
        }
        finally
        {
            link.Pump.Dispose();
        }

        Assert.Empty(gate.Provenance.Refusals);
        Assert.Contains(gate.Provenance.Records, r => r.Outcome == ProvenanceOutcome.Applied);
    }

    public static TheoryData<string> RefusalCases => new()
    {
        "absent", "tampered", "expired", "unsatisfiable", "un-understood",
    };

    [Theory] // T023 — absent/tampered/expired/unsatisfiable/un-understood → fail closed, recorded, no crash
    [MemberData(nameof(RefusalCases))]
    public void InvalidMacaroon_EstablishmentFailsClosed_RefusalRecorded_NoCrash(string kind)
    {
        MacaroonLinkGate gate = kind switch
        {
            "absent" => Gate(presented: null),
            "tampered" => Gate(Tampered(TimeCaveatedCap())),
            "expired" => Gate(TimeCaveatedCap(), nowUnixSeconds: 50),      // 50 > 100 fails (041 idiom)
            "unsatisfiable" => Gate(Macaroon.Create(RootKey, "glpquick", "cap-x")
                .AddCaveat(new Caveat("action", "=", "never-this-action"))),
            "un-understood" => Gate(Macaroon.Create(RootKey, "glpquick", "cap-y")
                .AddCaveat(new Caveat("weird", "=", "x"))),
            _ => throw new InvalidOperationException(kind),
        };

        // Fails closed via the graceful Abort path — the call itself must not throw (no crash),
        // and no listener exists: a refused establishment never opens a transport endpoint.
        var result = TryEstablish(gate, out var rig);
        rig.Link.Pump.Dispose();

        Assert.Equal(BodyKernelResult.Abort, result);
        Assert.Single(gate.Provenance.Refusals);                            // distinct recorded outcome
        Assert.Equal(0, rig.Link.Links.Count);                              // nothing was wired
    }

    private static Macaroon Tampered(Macaroon m)
    {
        m.Signature[0] ^= 0xFF;
        return m;
    }

    [Fact] // capability carriage (T025/D-2): slot rides as section 0x20 on the BINARY canonical surface
    public void CapabilitySlot_RidesBinaryCanonicalSurface_AsSection0x20()
    {
        var gate = Gate(TimeCaveatedCap());
        var gated = new CrdtMsgPayloadCodec(gate);

        byte[] wire = gated.Encode(SampleTerm());
        Message m = MessageCodec.Binary.Decode(wire);                        // no 041 codec change needed
        Assert.True(CapabilitySlot.Present(m));
        Assert.Equal(2, m.SchemaVersion);                                    // v2 additive-optional
        Assert.Null(m.Header.CapabilitySlot);                                // header field stays null on the wire

        // The slot bytes rehydrate to the presented macaroon and verify against the root key.
        Macaroon carried = MacaroonCodec.Decode(CapabilitySlot.Extract(m)!);
        Assert.True(carried.Verify(RootKey,
            new Dictionary<string, string> { ["action"] = "deliver", ["peer"] = "alice", ["expires"] = "150" },
            new HashSet<string> { "action", "peer", "expires" }));

        // MacaroonCodec is consume-all-or-throw (loud-fail, FR-007 discipline).
        byte[] slot = CapabilitySlot.Extract(m)!;
        byte[] trailing = new byte[slot.Length + 1];
        Array.Copy(slot, trailing, slot.Length);
        Assert.Throws<CrdtMsgException>(() => MacaroonCodec.Decode(trailing));
        Assert.Throws<CrdtMsgException>(() => MacaroonCodec.Decode(slot[..(slot.Length - 2)]));
    }

    [Fact] // T024 — gated action mid-session with an invalid capability → refused + recorded, run graceful
    public async Task GatedActionMidSession_InvalidCapability_RefusedAndRecorded_RunStaysGraceful()
    {
        if (!QuicAvailable) return;

        int port = FreeUdpPort();
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var (cert, pin) = MakeCert();
        var gate = Gate(TimeCaveatedCap());                                  // receiver-side verify gate
        link.Transports.Register(new QuicTransport(cert, pin));
        link.PayloadCodecs.Register(LinkScheme.Quic, new CrdtMsgPayloadCodec(gate));
        link.CapabilityGates.Register(LinkScheme.Quic, gate);

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
            var sent = SampleTerm();

            // (a) NO capability slot: an ungated codec's envelope is a gated action with an ABSENT
            //     capability — verify-before-act refuses it, records the refusal, and delivers nothing.
            byte[] noSlot = new CrdtMsgPayloadCodec().Encode(sent);
            foreach (var f in FrameCodec.Encode(noSlot, messageId: 0))
                await peer.SendBytesAsync(f);

            // (b) WRONG ROOT KEY (tamper-equivalent): a macaroon minted from a different root fails
            //     the HMAC chain — refused + recorded, still no crash.
            byte[] wrongRoot = RandomNumberGenerator.GetBytes(32);
            var rogueGate = new MacaroonLinkGate(
                wrongRoot, Macaroon.Create(wrongRoot, "glpquick", "cap-rogue"), () => 150);
            byte[] badSlot = new CrdtMsgPayloadCodec(rogueGate).Encode(sent);
            foreach (var f in FrameCodec.Encode(badSlot, messageId: 1))
                await peer.SendBytesAsync(f);

            // (c) a VALID capability after the refusals: the pump survived and delivery proceeds —
            //     the run stayed graceful (FR-009: refusal is per-action, never a crash/teardown).
            byte[] good = new CrdtMsgPayloadCodec(Gate(TimeCaveatedCap())).Encode(sent);
            foreach (var f in FrameCodec.Encode(good, messageId: 2))
                await peer.SendBytesAsync(f);

            // Exactly one item becomes deliverable — the valid one. (Refused actions enqueue nothing.)
            Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(10)));
            var cons = Assert.IsType<StructTerm>(engine.Heap.Dereference(new VarRef(inR)));
            Assert.Equal(".", cons.Functor);
            Term received = engine.Heap.Dereference(cons.Args[0]);
            var msg = Assert.IsType<StructTerm>(received);
            Assert.Equal("crdtmsg", msg.Functor);                            // the valid message, slot stripped
            Assert.False(engine.InboundPump!.TryApplyNext(TimeSpan.FromMilliseconds(200)));

            // Both invalid gated actions were refused as DISTINCT recorded outcomes, never silent drops.
            Assert.Equal(2, gate.Provenance.Refusals.Count());
            Assert.Contains(gate.Provenance.Records, r => r.Outcome == ProvenanceOutcome.Applied);
        }
        finally
        {
            link.Pump.Dispose();
            await peer.DisposeAsync();
        }
    }
}
