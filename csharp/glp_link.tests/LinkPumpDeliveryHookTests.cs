using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Feature 064 US2 (T006) — the additive host-owned delivery observer
/// (<see cref="LinkPump.OnDelivered"/>, contracts/message-log-and-replay.md). It fires on the
/// runner thread ONCE per data term actually delivered to the program, in receipt order,
/// BEFORE the heap bind that lets the program observe it (FR-004's durability-precedes-action
/// guarantee), and NEVER for graceful-close or fault items. Unset (the default) leaves ingress
/// byte-identical to pre-064 — the other ingress suites are the regression evidence for that.
/// </summary>
public class LinkPumpDeliveryHookTests
{
    private const string Chan = "chan-hook";

    private static Term LinkIdTerm() => new StructTerm("link_id", new Term[]
    {
        new ConstTerm("loopback"), new ConstTerm(Chan), new ConstTerm(1L),
    });

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

    private static async Task PeerSendAsync(ILinkEndpoint peer, Term term, uint seq)
    {
        var payload = new PayloadSerializer("peer").SerializeAgentMessage(term);
        foreach (var f in FrameCodec.Encode(payload, messageId: seq))
            await peer.SendBytesAsync(f);
    }

    // T006a — one observation per delivered term, in receipt order, carrying the link id.
    [Fact]
    public async Task OnDelivered_FiresOncePerTerm_InReceiptOrder()
    {
        var (engine, link, peer, _) = await SetupAsync();
        var seen = new List<string>();
        var ids = new List<LinkId>();
        link.Pump.OnDelivered = (id, term) =>
        {
            ids.Add(id);
            seen.Add(((ConstTerm)term).Value?.ToString() ?? "<null>");
        };

        await PeerSendAsync(peer, new ConstTerm("alpha"), seq: 0);
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));
        await PeerSendAsync(peer, new ConstTerm("beta"), seq: 1);
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));

        Assert.Equal(new[] { "alpha", "beta" }, seen);
        Assert.All(ids, id => Assert.Equal(LinkScheme.Loopback, id.Scheme));

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    // T006b — a duplicate frame is a verified no-op at the dedup gate, so the observer does
    // NOT see it twice: the WAL's exactly-once property (SC-002) rests on this.
    [Fact]
    public async Task OnDelivered_DuplicateFrame_ObservedOnlyOnce()
    {
        var (engine, link, peer, _) = await SetupAsync();
        int count = 0;
        link.Pump.OnDelivered = (_, _) => count++;

        await PeerSendAsync(peer, new ConstTerm("once"), seq: 0);
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));
        await PeerSendAsync(peer, new ConstTerm("once"), seq: 0); // redelivery of the same seq
        engine.InboundPump!.TryApplyNext(TimeSpan.FromMilliseconds(500));

        Assert.Equal(1, count);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    // T006c — graceful close is not a message: the observer must not fire for it (nor for faults).
    [Fact]
    public async Task OnDelivered_NotFiredForGracefulClose()
    {
        var (engine, link, peer, _) = await SetupAsync();
        int count = 0;
        link.Pump.OnDelivered = (_, _) => count++;

        await PeerSendAsync(peer, new ConstTerm("last"), seq: 0);
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));
        await peer.CloseAsync();                                  // peer FIN → close item
        engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3));

        Assert.Equal(1, count); // the data term only — never the close

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    // T006d — an observer that throws is loud but never breaks ingress (availability over
    // durability; the contract's both-backends-fail policy).
    [Fact]
    public async Task OnDelivered_ThrowingObserver_DoesNotBreakIngress()
    {
        var (engine, link, peer, inReader) = await SetupAsync();
        link.Pump.OnDelivered = (_, _) => throw new InvalidOperationException("wal down");

        await PeerSendAsync(peer, new ConstTerm("survives"), seq: 0);
        Assert.True(engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));

        var cons = Assert.IsType<StructTerm>(engine.Heap.Dereference(new VarRef(inReader)));
        Assert.Equal("survives", Assert.IsType<ConstTerm>(engine.Heap.Dereference(cons.Args[0])).Value);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }
}
