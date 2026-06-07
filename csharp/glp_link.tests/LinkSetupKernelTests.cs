using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// T030 — the <c>'_link_setup'/5</c> body kernel + the Option-B inbound pump
/// (FR-001/002/003/004/007). Drives the kernel directly with hand-built heap cells
/// (the GLP <c>link_setup/4</c> wrapper + compile pipeline arrive with T036/T040),
/// exercising the full establishment wiring against the deterministic loopback
/// transport: setup → egress (program writes <c>Out</c> → ground frame on the wire)
/// → ingress (peer frame → pump extends <c>In</c> on the runner thread).
/// </summary>
public class LinkSetupKernelTests
{
    private const string Chan = "chan-setup";

    // link_id("loopback", "chan-setup", 1) — the ground LinkId term the wrapper passes.
    private static Term LinkIdTerm() => new StructTerm("link_id", new Term[]
    {
        new ConstTerm("loopback"),
        new ConstTerm(Chan),
        new ConstTerm(1L),
    });

    private static LinkId ExpectedId() =>
        new(LinkScheme.Loopback, LinkAddress.Path(Chan), LinkNonce.Int(1));

    /// <summary>Establish a connector link via the kernel; return the engine, link runtime, the peer (listener) end, and the three cell addresses.</summary>
    private static async Task<(GlpRuntimeEngine engine, LinkRuntime link, ILinkEndpoint peer,
        int inW, int outW, int outR, int faultsW)> SetupConnectorAsync()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var transport = new LoopbackTransport();
        link.Transports.Register(transport);

        // The three output holes the wrapper's head allocates: In (writer the host
        // extends), Out (writer the program fills; we pass its reader Out?), Faults
        // (writer the host extends).
        var (inW, _) = engine.Heap.AllocateVariable();
        var (outW, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();

        var args = new List<object?>
        {
            LinkIdTerm(),
            new ConstTerm("connector"),
            new VarRef(inW),
            new VarRef(outR),
            new VarRef(faultsW),
        };

        // Park a listener (the "other process"), then run the connector kernel which
        // rendezvouses with it synchronously.
        var listenTask = transport.ListenAsync(LinkScheme.Loopback, LinkAddress.Path(Chan), LinkOptions.Default);
        var kernel = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity);
        Assert.NotNull(kernel);
        var result = kernel!(engine, args);
        Assert.Equal(BodyKernelResult.Success, result);

        var peer = await listenTask;
        return (engine, link, peer, inW, outW, outR, faultsW);
    }

    [Fact]
    public async Task Setup_RegistersHandle_WiresCursors_InstallsPump()
    {
        var (engine, link, peer, inW, outW, outR, faultsW) = await SetupConnectorAsync();

        Assert.True(link.Links.TryGet(ExpectedId(), out var handle));
        Assert.Equal(inW, handle.InWriterAddr);
        Assert.Equal(outR, handle.OutReaderAddr);
        Assert.Equal(faultsW, handle.FaultsWriterAddr);
        Assert.Same(link.Pump, engine.InboundPump);

        link.Pump.Dispose();
        await peer.DisposeAsync();
        _ = outW; // (the egress writer is exercised in the egress test)
    }

    [Fact]
    public async Task Setup_IsIdempotentAtIdentity_ReSetupSurfaced()
    {
        var (engine, link, peer, _, _, _, _) = await SetupConnectorAsync();
        Assert.Equal(1, link.Links.Count);

        // A second setup with the SAME ground LinkId but fresh holes is the FR-007
        // re-setup cell-aliasing case (rulings-log open) — the kernel surfaces it.
        var (inW2, _) = engine.Heap.AllocateVariable();
        var (_, outR2) = engine.Heap.AllocateVariable();
        var (faultsW2, _) = engine.Heap.AllocateVariable();
        var args = new List<object?>
        {
            LinkIdTerm(), new ConstTerm("connector"),
            new VarRef(inW2), new VarRef(outR2), new VarRef(faultsW2),
        };
        var kernel = engine.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        var result = kernel(engine, args);
        Assert.Equal(BodyKernelResult.Abort, result);   // surfaced, not silently reused
        Assert.Equal(1, link.Links.Count);                // no duplicate handle

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Egress_ProgramWritesOut_GroundFrameOnTheWire()
    {
        var (engine, link, peer, _, outW, _, _) = await SetupConnectorAsync();

        // The program binds Out = [hello | Out'] (the head-cons of link_send/3). The
        // armed egress drainer ships the ground head.
        var (_, outTailR) = engine.Heap.AllocateVariable();
        var cons = new StructTerm(".", new Term[] { new ConstTerm("hello"), new VarRef(outTailR) });
        engine.Heap.BindVariable(outW, cons);

        var frame = await peer.RecvBytesAsync();
        Assert.NotNull(frame);
        var parsed = FrameCodec.ParseFrame(frame!);
        var payload = new FrameReassembler().Accept(parsed);
        Assert.NotNull(payload);
        var term = new PayloadSerializer(string.Empty).DeserializeAgentMessagePayload(
            payload!, allocateImportedVar: _ => throw new Exception("unexpected variable on the wire"));
        var c = Assert.IsType<ConstTerm>(term);
        Assert.Equal("hello", c.Value);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Ingress_PeerFrame_PumpExtendsInStreamOnRunnerThread()
    {
        var (engine, link, peer, inW, _, _, _) = await SetupConnectorAsync();

        // The peer ships a ground term; the receiver's InboundOrdering expects seq 0.
        var payload = new PayloadSerializer("peer").SerializeAgentMessage(new ConstTerm("world"));
        foreach (var f in FrameCodec.Encode(payload, messageId: 0u))
            await peer.SendBytesAsync(f);

        Assert.NotNull(engine.InboundPump);
        bool applied = engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3));
        Assert.True(applied);

        // In was bound to [world | In']; the ingress cursor advanced to a fresh writer.
        var inVal = engine.Heap.Dereference(new VarRef(inW));
        var inCons = Assert.IsType<StructTerm>(inVal);
        Assert.Equal(".", inCons.Functor);
        var head = Assert.IsType<ConstTerm>(engine.Heap.Dereference(inCons.Args[0]));
        Assert.Equal("world", head.Value);

        Assert.True(link.Links.TryGet(ExpectedId(), out var handle));
        Assert.NotNull(handle.InWriterAddr);
        Assert.NotEqual(inW, handle.InWriterAddr);   // cursor advanced past the bound cell

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }
}
