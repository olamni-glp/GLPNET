using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// T031 — the <c>'_link_send'/3</c> body kernel (the LinkId-keyed sender face backing
/// the GLP <c>out_relay/3</c> wrapper; OQ-3 ruling). Drives the kernel directly with
/// hand-built heap cells (the GLP wrappers + compile pipeline arrive with T036/T040)
/// against the deterministic loopback transport: a ground payload sent by LinkId is
/// serialized to a ground frame on the wire (FR-010), sequenced per-link FIFO
/// (FR-018/053), and a non-ground / unknown-link send is surfaced as Abort rather than
/// placing a partial term on the wire.
/// </summary>
public class LinkSendKernelTests
{
    private const string Chan = "chan-send";

    private static Term LinkIdTerm() => new StructTerm("link_id", new Term[]
    {
        new ConstTerm("loopback"),
        new ConstTerm(Chan),
        new ConstTerm(1L),
    });

    private static LinkId ExpectedId() =>
        new(LinkScheme.Loopback, LinkAddress.Path(Chan), LinkNonce.Int(1));

    /// <summary>Establish a connector link (via the T030 setup kernel) and return the engine, runtime, and peer end.</summary>
    private static async Task<(GlpRuntimeEngine engine, LinkRuntime link, ILinkEndpoint peer)> SetupConnectorAsync()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var transport = new LoopbackTransport();
        link.Transports.Register(transport);

        var (inW, _) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
        var setupArgs = new List<object?>
        {
            LinkIdTerm(), new ConstTerm("connector"),
            new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        };

        var listenTask = transport.ListenAsync(LinkScheme.Loopback, LinkAddress.Path(Chan), LinkOptions.Default);
        var setup = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        Assert.Equal(BodyKernelResult.Success, setup(engine, setupArgs));
        var peer = await listenTask;
        return (engine, link, peer);
    }

    private static BodyKernelResult Send(GlpRuntimeEngine engine, Term msg, Term linkId, Term toPeer)
    {
        var kernel = engine.BodyKernels.Lookup(LinkKernels.LinkSendName, LinkKernels.LinkSendArity);
        Assert.NotNull(kernel);
        return kernel!(engine, new List<object?> { msg, linkId, toPeer });
    }

    /// <summary>Drain one whole message off the peer endpoint (reassembling fragments) and deserialize it.</summary>
    private static async Task<Term> RecvTermAsync(ILinkEndpoint peer)
    {
        var reassembler = new FrameReassembler();
        while (true)
        {
            var frame = await peer.RecvBytesAsync();
            Assert.NotNull(frame);
            var payload = reassembler.Accept(FrameCodec.ParseFrame(frame!));
            if (payload is not null)
                return new PayloadSerializer(string.Empty).DeserializeAgentMessagePayload(
                    payload, allocateImportedVar: _ => throw new Exception("unexpected variable on the wire"));
        }
    }

    [Fact]
    public async Task Send_GroundConst_ShipsGroundFrameToPeer()
    {
        var (engine, link, peer) = await SetupConnectorAsync();

        var result = Send(engine, new ConstTerm("hello"), LinkIdTerm(), new ConstTerm("peer"));
        Assert.Equal(BodyKernelResult.Success, result);

        var term = await RecvTermAsync(peer);
        Assert.Equal("hello", Assert.IsType<ConstTerm>(term).Value);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_GroundStruct_ResolvesAndShipsWholeTree()
    {
        var (engine, link, peer) = await SetupConnectorAsync();

        // A ground struct whose args are VarRefs into BOUND cells — the deep
        // ground-resolve must flatten it to a VarRef-free tree to serialize (the
        // serializer throws on any VarRef). add(1, 2).
        var (a1w, a1r) = engine.Heap.AllocateVariable();
        var (a2w, a2r) = engine.Heap.AllocateVariable();
        engine.Heap.BindVariable(a1w, new ConstTerm(1L));
        engine.Heap.BindVariable(a2w, new ConstTerm(2L));
        var msg = new StructTerm("add", new Term[] { new VarRef(a1r), new VarRef(a2r) });

        var result = Send(engine, msg, LinkIdTerm(), new ConstTerm("peer"));
        Assert.Equal(BodyKernelResult.Success, result);

        var term = await RecvTermAsync(peer);
        var s = Assert.IsType<StructTerm>(term);
        Assert.Equal("add", s.Functor);
        Assert.Equal(2, s.Args.Count);
        Assert.Equal(1L, Assert.IsType<ConstTerm>(s.Args[0]).Value);
        Assert.Equal(2L, Assert.IsType<ConstTerm>(s.Args[1]).Value);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_TwiceIsPerLinkFifo_MonotoneSequence()
    {
        var (engine, link, peer) = await SetupConnectorAsync();

        Assert.Equal(BodyKernelResult.Success, Send(engine, new ConstTerm("one"), LinkIdTerm(), new ConstTerm("peer")));
        Assert.Equal(BodyKernelResult.Success, Send(engine, new ConstTerm("two"), LinkIdTerm(), new ConstTerm("peer")));

        // The receiver's InboundOrdering reconstructs send order from the per-link
        // monotone sequence stamped by handle.Sequencer (seq 0 then 1).
        var ordered = await DrainOrderedAsync(peer, count: 2);
        Assert.Equal(new[] { "one", "two" }, ordered);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_UnknownLink_Aborts()
    {
        var (engine, link, peer) = await SetupConnectorAsync();

        // A ground LinkId that was never set up — "send before setup" is a caller bug.
        var unknown = new StructTerm("link_id", new Term[]
        {
            new ConstTerm("loopback"), new ConstTerm("never-opened"), new ConstTerm(9L),
        });
        var result = Send(engine, new ConstTerm("x"), unknown, new ConstTerm("peer"));
        Assert.Equal(BodyKernelResult.Abort, result);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_NonGroundMsg_Aborts_GroundGate()
    {
        var (engine, link, peer) = await SetupConnectorAsync();

        // An UNBOUND reader as the payload — the ground(Msg?) guard should have
        // excluded it; the kernel surfaces the gate violation rather than shipping.
        var (_, unboundR) = engine.Heap.AllocateVariable();
        var result = Send(engine, new VarRef(unboundR), LinkIdTerm(), new ConstTerm("peer"));
        Assert.Equal(BodyKernelResult.Abort, result);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Send_NonGroundToPeer_Aborts()
    {
        var (engine, link, peer) = await SetupConnectorAsync();

        var (_, unboundR) = engine.Heap.AllocateVariable();
        var result = Send(engine, new ConstTerm("hello"), LinkIdTerm(), new VarRef(unboundR));
        Assert.Equal(BodyKernelResult.Abort, result);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    // --- helper: drain N messages through the receiver's reassembler + ordering buffer ---

    private static async Task<List<string>> DrainOrderedAsync(ILinkEndpoint peer, int count)
    {
        var reassembler = new FrameReassembler();
        var ordering = new InboundOrdering();
        var got = new List<string>();
        while (got.Count < count)
        {
            var frame = await peer.RecvBytesAsync();
            Assert.NotNull(frame);
            var parsed = FrameCodec.ParseFrame(frame!);
            var payload = reassembler.Accept(parsed);
            if (payload is null) continue;
            foreach (var inOrder in ordering.Accept(parsed.MessageId, payload))
                got.Add(Decode(inOrder));
        }
        return got;
    }

    private static string Decode(byte[] payload) =>
        (string)Assert.IsType<ConstTerm>(
            new PayloadSerializer(string.Empty).DeserializeAgentMessagePayload(
                payload, allocateImportedVar: _ => throw new Exception("unexpected variable"))).Value!;
}
