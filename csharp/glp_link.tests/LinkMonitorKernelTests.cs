using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Seam;
using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// T034 — the <c>'_link_monitor'/2</c> system-predicate + the per-link fault-monitor
/// stream (FR-008/043-046). Faults surface as ORDINARY BOUND GROUND TERMS
/// (<c>ok</c>/<c>closed</c>/<c>tempFail</c>/<c>permFail</c>) on a per-link stream read
/// with existing guards — never a fourth verdict (FR-043), never a logical Fail
/// (FR-044). These tests drive the kernel directly (the GLP <c>link_monitor/2</c>
/// wrapper + compile pipeline land in T036) against a controllable fault-injecting
/// endpoint, asserting: the kernel registers an independent observer cursor and pushes
/// the healthy <c>ok</c> baseline; an out-of-band transport fault is delivered as a
/// bound term via the pump on the runner thread; one fault fans out to EVERY observer
/// (the establishment <c>Faults</c> stream and each <c>link_monitor</c> stream — FR-008);
/// a watcher suspended on the monitor stream reactivates on a fault (SC-010); and the
/// data path is untouched (an unmonitored goal stays suspended — FR-044).
/// </summary>
public class LinkMonitorKernelTests
{
    private const string Chan = "chan-mon";
    private static readonly LinkScheme Faultable = LinkScheme.Of("faultable");

    private static Term LinkIdTerm() => new StructTerm("link_id", new Term[]
    {
        new ConstTerm("faultable"), new ConstTerm(Chan), new ConstTerm(1L),
    });

    private static LinkId ExpectedId() =>
        new(Faultable, LinkAddress.Path(Chan), LinkNonce.Int(1));

    /// <summary>A leaf whose only job is to be driven into faults on demand; recv parks until torn down.</summary>
    private sealed class FaultableEndpoint : ILinkEndpoint
    {
        public LinkId Id { get; }
        public event Action<LinkFaultSignal>? OnFault;
        public FaultableEndpoint(LinkId id) => Id = id;

        public Task SendBytesAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default) => Task.CompletedTask;

        public async Task<byte[]?> RecvBytesAsync(CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false); // parks until the pump is disposed
            return null;
        }

        public Task CloseAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        /// <summary>Inject an out-of-band transport fault (the seam's <see cref="OnFault"/> path).</summary>
        public void Raise(LinkFaultKind kind, string reason) =>
            OnFault?.Invoke(new LinkFaultSignal(Id, kind, reason));
    }

    private sealed class FaultableTransport : ILinkTransport
    {
        public IReadOnlyCollection<LinkScheme> SupportedSchemes { get; } = new[] { Faultable };
        public Task<ILinkEndpoint> ListenAsync(LinkScheme scheme, LinkAddress local, LinkOptions opts, CancellationToken ct = default)
            => Task.FromResult<ILinkEndpoint>(new FaultableEndpoint(new LinkId(scheme, local, LinkNonce.Int(1))));
        public Task<ILinkEndpoint> ConnectAsync(LinkScheme scheme, LinkAddress remote, LinkOptions opts, CancellationToken ct = default)
            => Task.FromResult<ILinkEndpoint>(new FaultableEndpoint(new LinkId(scheme, remote, LinkNonce.Int(1))));
    }

    /// <summary>Establish a link over the faultable transport; return the engine, link runtime, the endpoint, and the establishment Faults READER.</summary>
    private static (GlpRuntimeEngine engine, LinkRuntime link, FaultableEndpoint endpoint, int faultsReader) Setup()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        link.Transports.Register(new FaultableTransport());

        var (inW, _) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
        var (faultsW, faultsR) = engine.Heap.AllocateVariable();
        var args = new List<object?>
        {
            LinkIdTerm(), new ConstTerm("connector"),
            new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        };
        var setup = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        Assert.Equal(BodyKernelResult.Success, setup(engine, args));

        Assert.True(link.Links.TryGet(ExpectedId(), out var handle));
        return (engine, link, (FaultableEndpoint)handle.Endpoint, faultsR);
    }

    /// <summary>Run '_link_monitor'/2 for the established link; return the Faults reader the program would read.</summary>
    private static int Monitor(GlpRuntimeEngine engine)
    {
        var (monW, monR) = engine.Heap.AllocateVariable();
        var args = new List<object?> { LinkIdTerm(), new VarRef(monW) };
        var kernel = engine.BodyKernels.Lookup(LinkKernels.LinkMonitorName, LinkKernels.LinkMonitorArity);
        Assert.NotNull(kernel);
        Assert.Equal(BodyKernelResult.Success, kernel!(engine, args));
        return monR;
    }

    private static StructTerm Cons(GlpRuntimeEngine engine, int reader) =>
        Assert.IsType<StructTerm>(engine.Heap.Dereference(new VarRef(reader)));

    private static string HeadConst(GlpRuntimeEngine engine, StructTerm cons) =>
        (string)Assert.IsType<ConstTerm>(engine.Heap.Dereference(cons.Args[0])).Value!;

    private static int TailAddr(StructTerm cons) => Assert.IsType<VarRef>(cons.Args[1]).Addr;

    [Fact]
    public void Monitor_RegistersObserverCursor_PushesOkBaseline()
    {
        var (engine, link, _, _) = Setup();
        Assert.True(link.Links.TryGet(ExpectedId(), out var handle));
        Assert.Single(handle.MonitorCursors); // the establishment Faults stream

        int monR = Monitor(engine);

        // A fresh, independent observer cursor was registered (FR-008) ...
        Assert.Equal(2, handle.MonitorCursors.Count);
        // ... and the healthy `ok` baseline is immediately on the monitor stream.
        var cons = Cons(engine, monR);
        Assert.Equal(".", cons.Functor);
        Assert.Equal("ok", HeadConst(engine, cons));

        link.Pump.Dispose();
    }

    [Fact]
    public void Monitor_TransportFault_DeliveredAsBoundTerm_NotVerdict()
    {
        var (engine, link, endpoint, _) = Setup();
        int monR = Monitor(engine);
        int afterOk = TailAddr(Cons(engine, monR)); // the cell the watcher reads next

        // An unrecoverable transport fault arrives out-of-band; the pump fans it onto
        // the monitor stream on the runner thread as a bound permFail term.
        endpoint.Raise(LinkFaultKind.Permanent, "connection refused");
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));

        // The stream extended: [ok | [permFail(LinkId, "connection refused") | _]].
        var faultCons = Cons(engine, afterOk);
        Assert.Equal(".", faultCons.Functor);
        var fault = Assert.IsType<StructTerm>(engine.Heap.Dereference(faultCons.Args[0]));
        Assert.Equal("permFail", fault.Functor);
        var idTerm = Assert.IsType<StructTerm>(engine.Heap.Dereference(fault.Args[0]));
        Assert.Equal("link_id", idTerm.Functor);
        Assert.Equal("connection refused", Assert.IsType<ConstTerm>(engine.Heap.Dereference(fault.Args[1])).Value);

        link.Pump.Dispose();
    }

    [Fact]
    public void Monitor_FaultFansOutToEveryObserver_IndependentlyObservable()
    {
        var (engine, link, endpoint, faultsR) = Setup();
        int monA = Monitor(engine);
        int monB = Monitor(engine);

        // Skip the `ok` baseline on the two link_monitor streams.
        int aAfterOk = TailAddr(Cons(engine, monA));
        int bAfterOk = TailAddr(Cons(engine, monB));

        // One transient fault → one inbox item → fanned out to ALL THREE observers
        // (the establishment Faults stream + both link_monitor streams) in one apply.
        endpoint.Raise(LinkFaultKind.Transient, "reset");
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));

        foreach (var reader in new[] { aAfterOk, bAfterOk, faultsR })
        {
            var cons = Cons(engine, reader); // [tempFail(LinkId, "reset") | _]
            var f = Assert.IsType<StructTerm>(engine.Heap.Dereference(cons.Args[0]));
            Assert.Equal("tempFail", f.Functor);
            Assert.Equal("reset", Assert.IsType<ConstTerm>(engine.Heap.Dereference(f.Args[1])).Value);
        }

        link.Pump.Dispose();
    }

    [Fact]
    public void Monitor_SuspendedWatcher_ReactivatedOnFault()
    {
        var (engine, link, endpoint, _) = Setup();
        int monR = Monitor(engine);

        // A watcher reads `ok`, recurses, and SUSPENDS on the unbound tail (no fault yet).
        int tail = TailAddr(Cons(engine, monR));
        Assert.IsType<VarRef>(engine.Heap.Dereference(new VarRef(tail)));
        var record = new SuspensionRecord(goalId: 11, resumePC: 0);
        engine.Heap.SuspendOnReader(tail, record);
        Assert.True(record.Armed);
        Assert.True(engine.Gq.IsEmpty);

        // The fault arrives: the bound term reactivates the suspended watcher exactly once
        // (SC-010 — a fault-guarded clause becomes reducible).
        endpoint.Raise(LinkFaultKind.Permanent, "gave up");
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));

        Assert.Equal(1, engine.Gq.Length);
        Assert.False(record.Armed);
        var faultCons = Cons(engine, tail); // [permFail(...) | _]
        Assert.Equal("permFail",
            Assert.IsType<StructTerm>(engine.Heap.Dereference(faultCons.Args[0])).Functor);

        link.Pump.Dispose();
    }

    [Fact]
    public void Monitor_FaultDoesNotTouchDataPath_UnmonitoredGoalStaysSuspended()
    {
        var (engine, link, endpoint, _) = Setup();
        Assert.True(link.Links.TryGet(ExpectedId(), out var handle));
        int inCursorBefore = handle.InWriterAddr!.Value;

        // No link_monitor here: only the (unread) establishment Faults cursor exists.
        // A disconnect arrives → it must NOT extend the In data stream (FR-008) and must
        // NOT be a logical fail (FR-044): the In writer cursor is unmoved.
        endpoint.Raise(LinkFaultKind.Closed, "peer closed");
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));

        Assert.Equal(inCursorBefore, handle.InWriterAddr!.Value); // data path untouched (In cursor unmoved)

        link.Pump.Dispose();
    }

    [Fact]
    public void Monitor_OfUnestablishedLink_Aborts()
    {
        var engine = new GlpRuntimeEngine();
        LinkKernels.Install(engine);
        var (monW, _) = engine.Heap.AllocateVariable();
        var args = new List<object?> { LinkIdTerm(), new VarRef(monW) };
        var kernel = engine.BodyKernels.Lookup(LinkKernels.LinkMonitorName, LinkKernels.LinkMonitorArity)!;
        Assert.Equal(BodyKernelResult.Abort, kernel(engine, args)); // monitor observes; it never creates
    }
}
