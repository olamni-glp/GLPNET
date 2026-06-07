using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// T032 — the receiver contract (<c>link_recv/3</c> + the per-link host ingress). The
/// GLP <c>link_recv/3</c> wrapper is pure composable stream-decons (no kernel; its
/// clause lands in T036 <c>link.glp</c>) — the net-new work is the host ingress that
/// fills the <c>In</c> stream as frames arrive, routes a redelivered frame through the
/// transport dedup gate as a VERIFIED NO-OP (FR-021), and reactivates a reader
/// suspended on the unarrived stream head EXACTLY ONCE (FR-017/FR-051). These tests
/// drive the T030 pump + T022 <see cref="InboundOrdering"/> ingress directly (the
/// full compile pipeline + a real suspended consumer goal arrive in T040), simulating
/// the suspended <c>link_recv</c> with a <see cref="SuspensionRecord"/> on the In-head
/// reader and observing reactivation on the engine goal queue.
/// </summary>
public class LinkRecvIngressTests
{
    private const string Chan = "chan-recv";

    private static LinkId ExpectedId() =>
        new(LinkScheme.Loopback, LinkAddress.Path(Chan), LinkNonce.Int(1));

    private static Term LinkIdTerm() => new StructTerm("link_id", new Term[]
    {
        new ConstTerm("loopback"), new ConstTerm(Chan), new ConstTerm(1L),
    });

    /// <summary>Establish a connector link and KEEP the In-stream reader (the cell a consumer's <c>link_recv</c> reads / suspends on).</summary>
    private static async Task<(GlpRuntimeEngine engine, LinkRuntime link, ILinkEndpoint peer, int inReader)>
        SetupAsync()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var transport = new LoopbackTransport();
        link.Transports.Register(transport);

        var (inW, inR) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
        var args = new List<object?>
        {
            LinkIdTerm(), new ConstTerm("connector"),
            new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        };

        var listenTask = transport.ListenAsync(LinkScheme.Loopback, LinkAddress.Path(Chan), LinkOptions.Default);
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

    [Fact]
    public async Task Ingress_SuspendedReader_ReactivatedExactlyOnceOnArrival()
    {
        var (engine, link, peer, inReader) = await SetupAsync();

        // A consumer's `link_recv` reads the In stream head; with nothing arrived yet
        // the head reader is UNBOUND, so the goal SUSPENDS (FR-017, three-valued: an
        // unarrived value is a suspend, never a fail). Simulate that suspended goal.
        Assert.IsType<VarRef>(engine.Heap.Dereference(new VarRef(inReader)));
        var record = new SuspensionRecord(goalId: 7, resumePC: 0);
        engine.Heap.SuspendOnReader(inReader, record);
        Assert.True(record.Armed);
        Assert.True(engine.Gq.IsEmpty);

        // The frame arrives: the ingress extends In = [world | In'] and reactivates the
        // suspended goal — exactly once (the record disarms on activation, FR-051).
        await PeerSendAsync(peer, new ConstTerm("world"), seq: 0);
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));

        Assert.Equal(1, engine.Gq.Length);          // reactivated once
        Assert.False(record.Armed);                  // disarmed → cannot fire again
        var inVal = engine.Heap.Dereference(new VarRef(inReader));
        var cons = Assert.IsType<StructTerm>(inVal);
        Assert.Equal(".", cons.Functor);
        Assert.Equal("world", Assert.IsType<ConstTerm>(engine.Heap.Dereference(cons.Args[0])).Value);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Ingress_DuplicateFrame_IsVerifiedNoOp_NoReBind_NoSecondReactivation()
    {
        var (engine, link, peer, inReader) = await SetupAsync();
        var record = new SuspensionRecord(goalId: 7, resumePC: 0);
        engine.Heap.SuspendOnReader(inReader, record);

        // First delivery of seq 0: extends In, reactivates once.
        await PeerSendAsync(peer, new ConstTerm("once"), seq: 0);
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));
        Assert.Equal(1, engine.Gq.Length);
        var afterFirst = engine.Heap.Dereference(new VarRef(inReader));
        var cons = Assert.IsType<StructTerm>(afterFirst);
        var tailBefore = cons.Args[1];

        // Re-delivery of the SAME seq 0 (at-least-once transport): the per-link dedup
        // (InboundOrdering high-water) absorbs it BEFORE the heap — no inbox item, so
        // TryApplyNext finds nothing (verified no-op: no throw, no re-bind, no second
        // reactivation). FR-021/SC-008.
        await PeerSendAsync(peer, new ConstTerm("once"), seq: 0);
        Assert.False(engine.InboundPump!.TryApplyNext(TimeSpan.FromMilliseconds(300)));

        Assert.Equal(1, engine.Gq.Length);           // still exactly one reactivation
        var afterDup = engine.Heap.Dereference(new VarRef(inReader));
        var consAfter = Assert.IsType<StructTerm>(afterDup);
        Assert.Same(tailBefore, consAfter.Args[1]);  // head cell unchanged — no re-bind

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Ingress_TwoFrames_ExtendInStreamInArrivalOrder()
    {
        var (engine, link, peer, inReader) = await SetupAsync();

        await PeerSendAsync(peer, new ConstTerm("a"), seq: 0);
        await PeerSendAsync(peer, new ConstTerm("b"), seq: 1);
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));

        // In = [a, b | In''] — per-link FIFO preserved (FR-018).
        var first = Assert.IsType<StructTerm>(engine.Heap.Dereference(new VarRef(inReader)));
        Assert.Equal("a", Assert.IsType<ConstTerm>(engine.Heap.Dereference(first.Args[0])).Value);
        var second = Assert.IsType<StructTerm>(engine.Heap.Dereference(first.Args[1]));
        Assert.Equal("b", Assert.IsType<ConstTerm>(engine.Heap.Dereference(second.Args[0])).Value);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Ingress_ReorderedFrames_ReconstructInOrder()
    {
        var (engine, link, peer, inReader) = await SetupAsync();

        // Out-of-order on the wire (seq 1 before seq 0): the reorder buffer holds 1
        // until 0 fills the gap, then drains both in order — the stream still reads a,b.
        await PeerSendAsync(peer, new ConstTerm("b"), seq: 1);
        await PeerSendAsync(peer, new ConstTerm("a"), seq: 0);
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));

        var first = Assert.IsType<StructTerm>(engine.Heap.Dereference(new VarRef(inReader)));
        Assert.Equal("a", Assert.IsType<ConstTerm>(engine.Heap.Dereference(first.Args[0])).Value);
        var second = Assert.IsType<StructTerm>(engine.Heap.Dereference(first.Args[1]));
        Assert.Equal("b", Assert.IsType<ConstTerm>(engine.Heap.Dereference(second.Args[0])).Value);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }
}
