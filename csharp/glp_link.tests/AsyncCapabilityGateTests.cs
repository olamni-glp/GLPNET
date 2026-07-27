using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Feature 058 T034 (contract <c>s4-policy-service.md</c> §A) — the ADDITIVE async-capable
/// capability-gate variant beside the sync one. <see cref="ICapabilityGate.GateEstablishAsync"/> is a
/// default-interface-method delegating to <see cref="ICapabilityGate.GateEstablish"/>, so sync-only
/// implementors compile and behave unchanged; the canonical establish core
/// (<see cref="LinkEstablish.CapabilityRefusal"/>) now PREFERS the async variant, so an
/// implementation that overrides it (e.g. one consulting the network-callable policy service) gets
/// its async decision path on every establishment route (path-A setup and both path-B kernels).
/// </summary>
public class AsyncCapabilityGateTests
{
    private const string Chan = "chan-async-gate";

    private static Term LinkIdTerm() => new StructTerm("link_id", new Term[]
    {
        new ConstTerm("loopback"), new ConstTerm(Chan), new ConstTerm(1L),
    });

    /// <summary>Overrides ONLY the async variant to refuse (after a genuine yield); the sync path
    /// would ALLOW — so an establish that aborts proves the async variant was preferred.</summary>
    private sealed class AsyncRefusingGate : ICapabilityGate
    {
        public int AsyncCalls;
        public int SyncCalls;
        public bool GateEstablish(LinkId id) { SyncCalls++; return true; }
        public async ValueTask<bool> GateEstablishAsync(LinkId id, CancellationToken ct = default)
        {
            AsyncCalls++;
            await Task.Yield();   // genuinely asynchronous decision
            return false;
        }
    }

    /// <summary>Overrides ONLY the async variant to allow; the sync path would REFUSE — so an
    /// establish that succeeds proves the async variant was preferred.</summary>
    private sealed class AsyncAllowingGate : ICapabilityGate
    {
        public int AsyncCalls;
        public int SyncCalls;
        public bool GateEstablish(LinkId id) { SyncCalls++; return false; }
        public async ValueTask<bool> GateEstablishAsync(LinkId id, CancellationToken ct = default)
        {
            AsyncCalls++;
            await Task.Yield();
            return true;
        }
    }

    /// <summary>An async gate whose decision FAULTS — must fail closed gracefully like a sync throw.</summary>
    private sealed class AsyncThrowingGate : ICapabilityGate
    {
        public bool GateEstablish(LinkId id) => true;
        public async ValueTask<bool> GateEstablishAsync(LinkId id, CancellationToken ct = default)
        {
            await Task.Yield();
            throw new InvalidOperationException("policy service unreachable");
        }
    }

    /// <summary>A pre-058 sync-only implementor: relies on the default-interface-method bridge.</summary>
    private sealed class SyncOnlyGate : ICapabilityGate
    {
        private readonly bool _answer;
        public int Calls;
        public SyncOnlyGate(bool answer) => _answer = answer;
        public bool GateEstablish(LinkId id) { Calls++; return _answer; }
    }

    /// <summary>A transport that fails a test FAST if establish is reached after a refusal.</summary>
    private sealed class ConnectRecordingTransport : ILinkTransport
    {
        public int ConnectCalls;
        public IReadOnlyCollection<LinkScheme> SupportedSchemes { get; } = new[] { LinkScheme.Loopback };

        public Task<ILinkEndpoint> ConnectAsync(LinkScheme scheme, LinkAddress remote, LinkOptions opts, CancellationToken ct = default)
        {
            ConnectCalls++;
            throw new InvalidOperationException("verify-before-act violated: the gate must refuse BEFORE the transport opens");
        }

        public Task<ILinkEndpoint> ListenAsync(LinkScheme scheme, LinkAddress local, LinkOptions opts, CancellationToken ct = default)
            => throw new InvalidOperationException("no listen expected in this test");
    }

    private static (GlpRuntimeEngine engine, LinkRuntime link, List<object?> args) Prepare(
        ILinkTransport transport, ICapabilityGate gate)
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        link.Transports.Register(transport);
        link.CapabilityGates.Register(LinkScheme.Loopback, gate);

        var (inW, _) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
        var args = new List<object?>
        {
            LinkIdTerm(), new ConstTerm("connector"),
            new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        };
        return (engine, link, args);
    }

    [Fact] // async refusal blocks establish — before any transport open, sync path never consulted.
    public void AsyncRefusal_BlocksEstablish_BeforeTransportOpen()
    {
        var transport = new ConnectRecordingTransport();
        var gate = new AsyncRefusingGate();
        var (engine, link, args) = Prepare(transport, gate);

        var setup = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        var result = setup(engine, args);

        Assert.Equal(BodyKernelResult.Abort, result);   // fails closed
        Assert.Equal(1, gate.AsyncCalls);                // the async variant decided
        Assert.Equal(0, gate.SyncCalls);                 // sync path (which would ALLOW) never consulted
        Assert.Equal(0, transport.ConnectCalls);         // verify-before-act: nothing opened
        Assert.Equal(0, link.Links.Count);
    }

    [Fact] // async acceptance allows establish end-to-end over the real loopback transport.
    public async Task AsyncAcceptance_AllowsEstablish()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var transport = new LoopbackTransport();
        var gate = new AsyncAllowingGate();
        link.Transports.Register(transport);
        link.CapabilityGates.Register(LinkScheme.Loopback, gate);

        var (inW, _) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();

        // Park a listener (the "other process"); the connector kernel rendezvouses with it.
        var listenTask = transport.ListenAsync(LinkScheme.Loopback, LinkAddress.Path(Chan), LinkOptions.Default);
        var setup = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        var result = setup(engine, new List<object?>
        {
            LinkIdTerm(), new ConstTerm("connector"),
            new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        });

        Assert.Equal(BodyKernelResult.Success, result);
        Assert.Equal(1, gate.AsyncCalls);                // async decided (sync would have REFUSED)
        Assert.Equal(0, gate.SyncCalls);
        Assert.Equal(1, link.Links.Count);

        var peer = await listenTask;
        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    [Fact] // an async gate that faults fails closed GRACEFULLY (Abort), never an uncaught crash.
    public void AsyncGateFaults_FailsClosedGracefully()
    {
        var transport = new ConnectRecordingTransport();
        var (engine, link, args) = Prepare(transport, new AsyncThrowingGate());

        var setup = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        BodyKernelResult result = default;
        var ex = Record.Exception(() => result = setup(engine, args));

        Assert.Null(ex);                                 // no uncaught exception escapes the kernel
        Assert.Equal(BodyKernelResult.Abort, result);    // fails closed
        Assert.Equal(0, transport.ConnectCalls);
        Assert.Equal(0, link.Links.Count);
    }

    [Fact] // a pre-058 sync-only implementor still refuses through the async-preferring core.
    public void SyncOnlyImplementor_Refusal_StillFailsClosed()
    {
        var transport = new ConnectRecordingTransport();
        var gate = new SyncOnlyGate(answer: false);
        var (engine, link, args) = Prepare(transport, gate);

        var setup = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        var result = setup(engine, args);

        Assert.Equal(BodyKernelResult.Abort, result);
        Assert.Equal(1, gate.Calls);                     // the DIM bridged to the sync decision, once
        Assert.Equal(0, transport.ConnectCalls);
        Assert.Equal(0, link.Links.Count);
    }

    [Fact] // a pre-058 sync-only implementor still allows establishment unchanged.
    public async Task SyncOnlyImplementor_Acceptance_StillEstablishes()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var transport = new LoopbackTransport();
        var gate = new SyncOnlyGate(answer: true);
        link.Transports.Register(transport);
        link.CapabilityGates.Register(LinkScheme.Loopback, gate);

        var (inW, _) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();

        var listenTask = transport.ListenAsync(LinkScheme.Loopback, LinkAddress.Path(Chan), LinkOptions.Default);
        var setup = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        var result = setup(engine, new List<object?>
        {
            LinkIdTerm(), new ConstTerm("connector"),
            new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        });

        Assert.Equal(BodyKernelResult.Success, result);
        Assert.Equal(1, gate.Calls);
        Assert.Equal(1, link.Links.Count);

        var peer = await listenTask;
        link.Pump.Dispose();
        await peer.DisposeAsync();
    }
}
