using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// T035 — the <c>'_link_close'/2</c> system-predicate + the graceful stream-end <c>[]</c>
/// close (rulings-log "link_close — 9th base primitive"; FR-024). A close — abrupt
/// (<c>'_link_close'</c>, RST_STREAM-equiv, regardless of stream state) or graceful (the
/// program binds <c>Out = []</c>) — emits a terminal <c>closed(LinkId, Reason)</c> on every
/// monitor stream (an ordinary bound ground term, never a fourth verdict — FR-043), ends
/// those streams, tears the transport endpoint down, and runs distributed GC so the registry
/// returns to baseline (FR-024). The abrupt path carries the caller's reason (default
/// <c>abrupt</c>); the graceful path carries <c>eos</c>. These tests drive the kernel directly
/// (the GLP <c>link_close/1</c>+<c>/2</c> wrappers + compile pipeline land in T036). The data
/// path is left untouched — a close is signalled on the monitor, never on <c>In</c> (FR-044).
/// </summary>
public class LinkCloseKernelTests
{
    private const string Chan = "chan-close";
    private static readonly LinkScheme Rec = LinkScheme.Of("recording");

    private static Term LinkIdTerm() => new StructTerm("link_id", new Term[]
    {
        new ConstTerm("recording"), new ConstTerm(Chan), new ConstTerm(1L),
    });

    private static LinkId ExpectedId() =>
        new(Rec, LinkAddress.Path(Chan), LinkNonce.Int(1));

    /// <summary>A leaf that records its <see cref="CloseAsync"/> calls; recv parks until torn down.</summary>
    private sealed class RecordingEndpoint : ILinkEndpoint
    {
        public LinkId Id { get; }
        public int CloseCount;
#pragma warning disable CS0067 // OnFault is part of the seam; these tests exercise close, not faults.
        public event Action<LinkFaultSignal>? OnFault;
#pragma warning restore CS0067
        public RecordingEndpoint(LinkId id) => Id = id;

        public Task SendBytesAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default) => Task.CompletedTask;

        public async Task<byte[]?> RecvBytesAsync(CancellationToken ct = default)
        {
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false); // parks until the pump is disposed
            return null;
        }

        public Task CloseAsync()
        {
            Interlocked.Increment(ref CloseCount);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingTransport : ILinkTransport
    {
        public IReadOnlyCollection<LinkScheme> SupportedSchemes { get; } = new[] { Rec };
        public Task<ILinkEndpoint> ListenAsync(LinkScheme scheme, LinkAddress local, LinkOptions opts, CancellationToken ct = default)
            => Task.FromResult<ILinkEndpoint>(new RecordingEndpoint(new LinkId(scheme, local, LinkNonce.Int(1))));
        public Task<ILinkEndpoint> ConnectAsync(LinkScheme scheme, LinkAddress remote, LinkOptions opts, CancellationToken ct = default)
            => Task.FromResult<ILinkEndpoint>(new RecordingEndpoint(new LinkId(scheme, remote, LinkNonce.Int(1))));
    }

    /// <summary>
    /// Establish a link over the recording transport; return the engine, link runtime, the
    /// endpoint, the establishment Faults READER, and the <c>Out</c> WRITER (so a test can
    /// bind <c>Out = []</c> to drive the graceful close).
    /// </summary>
    private static (GlpRuntimeEngine engine, LinkRuntime link, RecordingEndpoint endpoint, int faultsReader, int outWriter) Setup()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        link.Transports.Register(new RecordingTransport());

        var (inW, _) = engine.Heap.AllocateVariable();
        var (outW, outR) = engine.Heap.AllocateVariable();
        var (faultsW, faultsR) = engine.Heap.AllocateVariable();
        var args = new List<object?>
        {
            LinkIdTerm(), new ConstTerm("connector"),
            new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        };
        var setup = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        Assert.Equal(BodyKernelResult.Success, setup(engine, args));

        Assert.True(link.Links.TryGet(ExpectedId(), out var handle));
        return (engine, link, (RecordingEndpoint)handle.Endpoint, faultsR, outW);
    }

    /// <summary>Run '_link_monitor'/2 for the established link; return the Faults reader.</summary>
    private static int Monitor(GlpRuntimeEngine engine)
    {
        var (monW, monR) = engine.Heap.AllocateVariable();
        var args = new List<object?> { LinkIdTerm(), new VarRef(monW) };
        var kernel = engine.BodyKernels.Lookup(LinkKernels.LinkMonitorName, LinkKernels.LinkMonitorArity)!;
        Assert.Equal(BodyKernelResult.Success, kernel(engine, args));
        return monR;
    }

    /// <summary>Run '_link_close'/2 with a ground reason.</summary>
    private static BodyKernelResult Close(GlpRuntimeEngine engine, string reason)
    {
        var args = new List<object?> { LinkIdTerm(), new ConstTerm(reason) };
        var kernel = engine.BodyKernels.Lookup(LinkKernels.LinkCloseName, LinkKernels.LinkCloseArity)!;
        return kernel(engine, args);
    }

    private static StructTerm Cons(GlpRuntimeEngine engine, int reader) =>
        Assert.IsType<StructTerm>(engine.Heap.Dereference(new VarRef(reader)));

    private static int TailAddr(StructTerm cons) => Assert.IsType<VarRef>(cons.Args[1]).Addr;

    /// <summary>Assert <paramref name="reader"/> heads a <c>closed(link_id(...), reason)</c> term; return the tail addr.</summary>
    private static int AssertClosed(GlpRuntimeEngine engine, int reader, string reason)
    {
        var cons = Cons(engine, reader);
        Assert.Equal(".", cons.Functor);
        var closed = Assert.IsType<StructTerm>(engine.Heap.Dereference(cons.Args[0]));
        Assert.Equal("closed", closed.Functor);
        Assert.Equal("link_id", Assert.IsType<StructTerm>(engine.Heap.Dereference(closed.Args[0])).Functor);
        Assert.Equal(reason, Assert.IsType<ConstTerm>(engine.Heap.Dereference(closed.Args[1])).Value);
        return TailAddr(cons);
    }

    [Fact]
    public void Close_Abrupt_EmitsClosedTerminal_ThenEndsMonitorStream()
    {
        var (engine, link, _, _, _) = Setup();
        int monR = Monitor(engine);
        int afterOk = TailAddr(Cons(engine, monR)); // skip the `ok` baseline

        Assert.Equal(BodyKernelResult.Success, Close(engine, LinkTerms.AbruptReason));

        // [ok | [closed(link_id, "abrupt") | []]] — the close term, then end-of-stream.
        int afterClosed = AssertClosed(engine, afterOk, LinkTerms.AbruptReason);
        Assert.Equal("nil", Assert.IsType<ConstTerm>(engine.Heap.Dereference(new VarRef(afterClosed))).Value);

        link.Pump.Dispose();
    }

    [Fact]
    public void Close_Abrupt_RunsDistributedGC_RegistryReturnsToBaseline()
    {
        var (engine, link, _, _, _) = Setup();
        Assert.Equal(1, link.Links.Count);

        Assert.Equal(BodyKernelResult.Success, Close(engine, LinkTerms.AbruptReason));

        Assert.False(link.Links.TryGet(ExpectedId(), out _)); // registry entry reclaimed (FR-024)
        Assert.Equal(0, link.Links.Count);
        Assert.True(link.Reclaimer.IsReclaimed(ExpectedId()));

        link.Pump.Dispose();
    }

    [Fact]
    public void Close_FansClosedOutToEveryObserver_IndependentlyObservable()
    {
        var (engine, link, _, faultsR, _) = Setup();
        int monA = Monitor(engine);
        int monB = Monitor(engine);
        int aAfterOk = TailAddr(Cons(engine, monA));
        int bAfterOk = TailAddr(Cons(engine, monB));

        Assert.Equal(BodyKernelResult.Success, Close(engine, LinkTerms.AbruptReason));

        // The establishment Faults stream (no `ok` was pushed there) heads closed directly;
        // each link_monitor stream heads closed after its `ok` (FR-008).
        AssertClosed(engine, faultsR, LinkTerms.AbruptReason);
        AssertClosed(engine, aAfterOk, LinkTerms.AbruptReason);
        AssertClosed(engine, bAfterOk, LinkTerms.AbruptReason);

        link.Pump.Dispose();
    }

    [Fact]
    public void Close_TearsDownTransportEndpoint()
    {
        var (engine, link, endpoint, _, _) = Setup();

        Assert.Equal(BodyKernelResult.Success, Close(engine, LinkTerms.AbruptReason));

        Assert.True(endpoint.CloseCount >= 1); // transport torn down (RST-equiv per leaf)

        link.Pump.Dispose();
    }

    [Fact]
    public void Close_WithUserReason_EmitsThatReason()
    {
        var (engine, link, _, _, _) = Setup();
        int monR = Monitor(engine);
        int afterOk = TailAddr(Cons(engine, monR));

        Assert.Equal(BodyKernelResult.Success, Close(engine, "shutting down"));

        AssertClosed(engine, afterOk, "shutting down");

        link.Pump.Dispose();
    }

    [Fact]
    public void Close_OfUnestablishedLink_Aborts()
    {
        var engine = new GlpRuntimeEngine();
        LinkKernels.Install(engine);

        Assert.Equal(BodyKernelResult.Abort, Close(engine, LinkTerms.AbruptReason)); // close observes; it never creates
    }

    [Fact]
    public void Close_Twice_SecondAborts()
    {
        var (engine, link, _, _, _) = Setup();

        Assert.Equal(BodyKernelResult.Success, Close(engine, LinkTerms.AbruptReason));
        Assert.Equal(BodyKernelResult.Abort, Close(engine, LinkTerms.AbruptReason)); // registry entry already reclaimed

        link.Pump.Dispose();
    }

    [Fact]
    public void Close_Graceful_StreamEnd_EmitsEos_AndRunsGC()
    {
        var (engine, link, endpoint, _, outWriter) = Setup();
        int monR = Monitor(engine);
        int afterOk = TailAddr(Cons(engine, monR));

        // The program closes its sender: Out = [] (nil). The egress drainer's OnBind callback
        // runs the SAME teardown as the abrupt kernel, with reason `eos`.
        foreach (var act in engine.Heap.BindVariable(outWriter, new ConstTerm("nil")))
            engine.EnqueueReactivatedGoal(act);

        // Graceful close emits closed(link_id, eos) on the monitor and ends it ...
        int afterClosed = AssertClosed(engine, afterOk, LinkTerms.GracefulReason);
        Assert.Equal("nil", Assert.IsType<ConstTerm>(engine.Heap.Dereference(new VarRef(afterClosed))).Value);
        // ... runs distributed GC ...
        Assert.False(link.Links.TryGet(ExpectedId(), out _));
        Assert.True(link.Reclaimer.IsReclaimed(ExpectedId()));
        // ... and tears down the transport.
        Assert.True(endpoint.CloseCount >= 1);

        link.Pump.Dispose();
    }

    [Fact]
    public void Close_SuspendedWatcher_ReactivatedExactlyOnce()
    {
        var (engine, link, _, _, _) = Setup();
        int monR = Monitor(engine);

        // A watcher reads `ok`, recurses, and SUSPENDS on the unbound tail (no close yet).
        int tail = TailAddr(Cons(engine, monR));
        Assert.IsType<VarRef>(engine.Heap.Dereference(new VarRef(tail)));
        var record = new SuspensionRecord(goalId: 21, resumePC: 0);
        engine.Heap.SuspendOnReader(tail, record);
        Assert.True(record.Armed);
        Assert.True(engine.Gq.IsEmpty);

        Assert.Equal(BodyKernelResult.Success, Close(engine, LinkTerms.AbruptReason));

        // The terminal closed term reactivates the suspended watcher exactly once.
        Assert.Equal(1, engine.Gq.Length);
        Assert.False(record.Armed);
        AssertClosed(engine, tail, LinkTerms.AbruptReason);

        link.Pump.Dispose();
    }

    [Fact]
    public void Close_DoesNotTouchDataPath_InCursorUnmoved()
    {
        var (engine, link, _, _, _) = Setup();
        Assert.True(link.Links.TryGet(ExpectedId(), out var handle));
        int inCursorBefore = handle.InWriterAddr!.Value;

        // A close is signalled on the monitor stream, never on the In data stream (FR-044):
        // the In writer cursor is unmoved, so a goal suspended on In stays suspended.
        Assert.Equal(BodyKernelResult.Success, Close(engine, LinkTerms.AbruptReason));

        Assert.Equal(inCursorBefore, handle.InWriterAddr!.Value);

        link.Pump.Dispose();
    }
}
