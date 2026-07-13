using System.IO;

using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Feature 050 P1 fix (2026-07-13, codexreview 20260713T072512Z): the capability gate must run
/// verify-before-act on the PATH-B establishment kernels too. Previously the gate lived only inside
/// <see cref="LinkEstablish.WireEstablishedLink"/>, which path-A ('_link_setup') reaches with a LAZY
/// endpoint factory (gate genuinely before open) — but '_link_request' opened the QUIC connection and
/// shipped the request token BEFORE that core, and '_link_accept' adopted an already-accepted pending
/// connection, so a refused peer still established a transport connection / exchanged the handshake.
/// These tests pin that the gate now runs FIRST on both path-B kernels.
/// </summary>
public class PathBGateOrderingTests
{
    private const string Chan = "chan-gate";

    private static Term LinkIdTerm() => new StructTerm("link_id", new Term[]
    {
        new ConstTerm("loopback"), new ConstTerm(Chan), new ConstTerm(1L),
    });

    /// <summary>A gate that always refuses and counts how many times it was consulted.</summary>
    private sealed class CountingRefusingGate : ICapabilityGate
    {
        public int Calls;
        public bool GateEstablish(LinkId id) { Calls++; return false; }
    }

    /// <summary>A gate whose evaluation THROWS — mirrors a LazyMacaroonLinkGate whose first use loads
    /// missing/invalid trust material (StaticMacaroonMaterial.LoadFromRepo throws).</summary>
    private sealed class ThrowingGate : ICapabilityGate
    {
        public bool GateEstablish(LinkId id) => throw new FileNotFoundException("lazy trust material missing");
    }

    /// <summary>A transport that records whether a connect/listen was attempted and refuses to
    /// perform one — so a test fails FAST (never hangs) if the gate is bypassed on a refusal.</summary>
    private sealed class ConnectRecordingTransport : ILinkTransport
    {
        public int ConnectCalls;
        public int ListenCalls;
        public IReadOnlyCollection<LinkScheme> SupportedSchemes { get; } = new[] { LinkScheme.Loopback };

        public Task<ILinkEndpoint> ConnectAsync(LinkScheme scheme, LinkAddress remote, LinkOptions opts, CancellationToken ct = default)
        {
            ConnectCalls++;
            throw new InvalidOperationException("verify-before-act violated: the gate must refuse BEFORE ConnectAsync (path-B)");
        }

        public Task<ILinkEndpoint> ListenAsync(LinkScheme scheme, LinkAddress local, LinkOptions opts, CancellationToken ct = default)
        {
            ListenCalls++;
            throw new InvalidOperationException("no listen expected in this test");
        }
    }

    [Fact] // '_link_request' (connector): a refused capability must abort BEFORE any QUIC connect / token ship.
    public void Request_RefusedCapability_GatesBeforeConnect_NoTransportOpened()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var transport = new ConnectRecordingTransport();
        var gate = new CountingRefusingGate();
        link.Transports.Register(transport);
        link.CapabilityGates.Register(LinkScheme.Loopback, gate);

        var (inW, _) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
        var request = engine.BodyKernels.Lookup(LinkKernels.LinkRequestName, LinkKernels.LinkRequestArity)!;

        var result = request(engine, new List<object?>
        {
            LinkIdTerm(), new ConstTerm("serverB"), new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        });

        Assert.Equal(BodyKernelResult.Abort, result);   // fails closed
        Assert.Equal(0, transport.ConnectCalls);         // gate ran BEFORE opening the connection (the P1)
        Assert.Equal(1, gate.Calls);                     // consulted exactly once (no double-record)
        Assert.Equal(0, link.Links.Count);               // nothing wired
    }

    [Fact] // '_link_accept': the gate must run BEFORE the pending-connection adopt.
    public void Accept_RefusedCapability_GatesBeforePendingAdopt()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        link.Transports.Register(new ConnectRecordingTransport());
        var gate = new CountingRefusingGate();
        link.CapabilityGates.Register(LinkScheme.Loopback, gate);

        var (inW, _) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
        var accept = engine.BodyKernels.Lookup(LinkKernels.LinkAcceptName, LinkKernels.LinkAcceptArity)!;

        // No pending connection is parked. PRE-fix the gate lived inside WireEstablishedLink, reached
        // only AFTER Pending.Remove — so with no pending the kernel aborted with the gate NEVER
        // consulted (Calls == 0). POST-fix the gate runs first: a refused peer fails closed on the
        // capability and the gate is consulted exactly once.
        var result = accept(engine, new List<object?>
        {
            LinkIdTerm(), new ConstTerm("requester"), new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        });

        Assert.Equal(BodyKernelResult.Abort, result);
        Assert.Equal(1, gate.Calls);          // consulted BEFORE the pending-adopt check (the P1)
        Assert.Equal(0, link.Links.Count);
    }

    [Fact] // path-A ('_link_setup'): a gate that THROWS on evaluation (lazy material missing) fails closed GRACEFULLY.
    public void Setup_GateEvaluationThrows_FailsClosedGracefully()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        link.Transports.Register(new ConnectRecordingTransport());
        link.CapabilityGates.Register(LinkScheme.Loopback, new ThrowingGate());

        var (inW, _) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
        var setup = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;

        BodyKernelResult result = default;
        var ex = Record.Exception(() => result = setup(engine, new List<object?>
        {
            LinkIdTerm(), new ConstTerm("connector"), new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        }));

        Assert.Null(ex);                              // no uncaught exception escapes the kernel (P2, review#3)
        Assert.Equal(BodyKernelResult.Abort, result); // fails closed gracefully
        Assert.Equal(0, link.Links.Count);
    }

    [Fact] // path-B ('_link_request'): same graceful fail-closed, and still no connect is attempted.
    public void Request_GateEvaluationThrows_FailsClosedGracefully_NoConnect()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var transport = new ConnectRecordingTransport();
        link.Transports.Register(transport);
        link.CapabilityGates.Register(LinkScheme.Loopback, new ThrowingGate());

        var (inW, _) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
        var request = engine.BodyKernels.Lookup(LinkKernels.LinkRequestName, LinkKernels.LinkRequestArity)!;

        BodyKernelResult result = default;
        var ex = Record.Exception(() => result = request(engine, new List<object?>
        {
            LinkIdTerm(), new ConstTerm("serverB"), new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        }));

        Assert.Null(ex);                              // graceful, not a crash
        Assert.Equal(BodyKernelResult.Abort, result);
        Assert.Equal(0, transport.ConnectCalls);      // gate threw before any connect
    }
}
