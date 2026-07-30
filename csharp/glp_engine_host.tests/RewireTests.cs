// T033 — the DEF-E1 re-wire path (US4): adopt restored pre-bound cells,
// idempotent re-adoption, cursor-position resume (egress re-arms at the first
// UNSHIPPED tail — committed work is never re-shipped, FR-032), the normal
// establish path keeps its guards, the 0x09 definition round-trips through
// capture→restore with the recorded role, snapshots defer while a rewire is
// pending, and mid-restore the dispatcher answers only STATUS/PING (wire rule 4).

using GlpRuntime.Engine;
using GlpRuntime.EngineHost.Snapshot;
using GlpRuntime.EngineHost.Store;
using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;
using GlpRuntime.SplitProtocol;

namespace GlpRuntime.EngineHost.Tests;

public class RewireTests : IDisposable
{
    private readonly string _rootSelfGlp = Program.ResolveRootSelfGlpPath();
    private readonly string _storeDir =
        Path.Combine(Path.GetTempPath(), $"glpsnap-t033-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_storeDir, recursive: true); } catch (IOException) { }
    }

    private static LinkId LoopbackId(string chan) =>
        new(LinkScheme.Loopback, LinkAddress.Path(chan), LinkNonce.Int(7));

    /// <summary>
    /// Build the post-restore heap shape: In/Out/Faults cells present, the Out
    /// stream already bound two elements deep ([a, b | T]) — exactly the committed
    /// work a snapshot carries (each element was SHIPPED at bind time pre-crash).
    /// </summary>
    private static (GlpRuntimeEngine Engine, LinkRuntime Link, LoopbackTransport Transport,
        int InW, int OutW, int OutR, int FaultsW, int UnshippedTailW)
        RestoredState()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        var transport = new LoopbackTransport();
        link.Transports.Register(transport);

        var (inW, _) = engine.Heap.AllocateVariable();
        var (outW, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();

        // Pre-bound Out chain: outW = [a | T1], T1 = [b | T2], T2 unbound.
        var (t1w, t1r) = engine.Heap.AllocateVariable();
        engine.Heap.BindVariable(outW, new StructTerm(".", new Term[] { new ConstTerm("a"), new VarRef(t1r) }));
        var (t2w, t2r) = engine.Heap.AllocateVariable();
        engine.Heap.BindVariable(t1w, new StructTerm(".", new Term[] { new ConstTerm("b"), new VarRef(t2r) }));

        return (engine, link, transport, inW, outW, outR, faultsW, t2w);
    }

    private static Term DecodeShippedTerm(byte[] frame)
    {
        var parsed = FrameCodec.ParseFrame(frame);
        var payload = new FrameReassembler().Accept(parsed);
        Assert.NotNull(payload);
        return new PayloadSerializer(string.Empty).DeserializeAgentMessagePayload(
            payload!, allocateImportedVar: _ => throw new Exception("unexpected variable on the wire"));
    }

    // ------------------------------------ adopt pre-bound cells + cursor resume

    [Fact]
    public async Task Adopt_PreBoundCells_ResumesEgressAtUnshippedTail_NoReShip()
    {
        var (engine, link, transport, inW, _, outR, faultsW, tailW) = RestoredState();
        var id = LoopbackId("chan-rewire-1");

        var listenTask = transport.ListenAsync(LinkScheme.Loopback, id.Endpoint, LinkOptions.Default);
        var handle = RewireHandle.Adopt(
            engine, link, id, LinkRole.Connector,
            () => transport.ConnectAsync(LinkScheme.Loopback, id.Endpoint, LinkOptions.Default)
                .GetAwaiter().GetResult(),
            inW, outR, faultsW, new[] { faultsW }, egressShippedCount: 2); // a, b shipped pre-crash
        var peer = await listenTask;

        // Registered + cursors at their RESTORED positions (US4 AS-2).
        Assert.True(link.Links.TryGet(id, out var registered));
        Assert.Same(handle, registered);
        Assert.Equal(LinkRole.Connector, handle.Role);
        Assert.Equal(inW, handle.InWriterAddr);
        Assert.Equal(outR, handle.OutReaderAddr);
        Assert.Equal(faultsW, handle.FaultsWriterAddr);
        Assert.Equal(new[] { faultsW }, handle.MonitorCursors);
        Assert.Same(link.Pump, engine.InboundPump);

        // FR-032 no-duplication: the committed elements a, b (bound pre-snapshot,
        // shipped pre-crash) are NOT re-shipped — the first frame the peer ever
        // receives is the first POST-restore element.
        var firstFrame = peer.RecvBytesAsync();
        var idle = await Task.WhenAny(firstFrame, Task.Delay(400));
        Assert.NotSame(firstFrame, idle); // nothing shipped from the pre-bound chain

        // The drain RESUMED: a post-restore bind at the unshipped tail ships.
        var (_, t3r) = engine.Heap.AllocateVariable();
        engine.Heap.BindVariable(tailW, new StructTerm(".", new Term[] { new ConstTerm("c"), new VarRef(t3r) }));
        var shipped = Assert.IsType<ConstTerm>(DecodeShippedTerm((await firstFrame)!));
        Assert.Equal("c", shipped.Value);
        Assert.Equal(3, handle.EgressShippedCount); // a, b (restored) + c (live)

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    [Fact]
    public async Task Adopt_BoundButUnshippedElement_IsReShipped_NoLoss()
    {
        // FR-032 no-loss: the snapshot says only ONE of the two bound elements was
        // handed to the transport (the second bind's synchronous ship threw when
        // the transport faulted pre-crash). The rewire must re-ship exactly the
        // unshipped bound element — never the shipped one, never nothing.
        var (engine, link, transport, inW, _, outR, faultsW, tailW) = RestoredState();
        var id = LoopbackId("chan-rewire-reship");

        var listenTask = transport.ListenAsync(LinkScheme.Loopback, id.Endpoint, LinkOptions.Default);
        var handle = RewireHandle.Adopt(
            engine, link, id, LinkRole.Connector,
            () => transport.ConnectAsync(LinkScheme.Loopback, id.Endpoint, LinkOptions.Default)
                .GetAwaiter().GetResult(),
            inW, outR, faultsW, new[] { faultsW }, egressShippedCount: 1); // only "a" was shipped
        var peer = await listenTask;

        // The peer receives the re-shipped "b" first...
        var frame1 = Assert.IsType<ConstTerm>(DecodeShippedTerm((await peer.RecvBytesAsync())!));
        Assert.Equal("b", frame1.Value);
        Assert.Equal(2, handle.EgressShippedCount);

        // ...and the drain is live at the tail: a fresh bind ships next.
        var (_, t3r) = engine.Heap.AllocateVariable();
        engine.Heap.BindVariable(tailW, new StructTerm(".", new Term[] { new ConstTerm("c"), new VarRef(t3r) }));
        var frame2 = Assert.IsType<ConstTerm>(DecodeShippedTerm((await peer.RecvBytesAsync())!));
        Assert.Equal("c", frame2.Value);
        Assert.Equal(3, handle.EgressShippedCount);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    // ------------------------------------------------- idempotent re-adoption

    [Fact]
    public async Task Adopt_IsIdempotentAtIdentity_NoDoubleWiring()
    {
        var (engine, link, transport, inW, _, outR, faultsW, _) = RestoredState();
        var id = LoopbackId("chan-rewire-2");

        var listenTask = transport.ListenAsync(LinkScheme.Loopback, id.Endpoint, LinkOptions.Default);
        var first = RewireHandle.Adopt(
            engine, link, id, LinkRole.Listener,
            () => transport.ConnectAsync(LinkScheme.Loopback, id.Endpoint, LinkOptions.Default)
                .GetAwaiter().GetResult(),
            inW, outR, faultsW, new[] { faultsW });
        var peer = await listenTask;

        // Re-adoption: same handle back, establish NOT re-run, cursors NOT re-added.
        var again = RewireHandle.Adopt(
            engine, link, id, LinkRole.Listener,
            () => throw new InvalidOperationException("establish must not run on re-adoption"),
            inW, outR, faultsW, new[] { faultsW });
        Assert.Same(first, again);
        Assert.Equal(1, link.Links.Count);
        Assert.Equal(new[] { faultsW }, first.MonitorCursors); // not duplicated

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    // ------------------------------- the normal establish path keeps its guards

    [Fact]
    public async Task NormalEstablishPath_StillRefusesReEstablishment_AfterAdopt()
    {
        var (engine, link, transport, inW, _, outR, faultsW, _) = RestoredState();
        var id = LoopbackId("chan-rewire-3");

        var listenTask = transport.ListenAsync(LinkScheme.Loopback, id.Endpoint, LinkOptions.Default);
        RewireHandle.Adopt(
            engine, link, id, LinkRole.Connector,
            () => transport.ConnectAsync(LinkScheme.Loopback, id.Endpoint, LinkOptions.Default)
                .GetAwaiter().GetResult(),
            inW, outR, faultsW, new[] { faultsW });
        var peer = await listenTask;

        // WireEstablishedLink's own guards are untouched (research.md D9): a
        // normal-path re-establishment of the adopted id aborts (FR-007 first-only),
        // even with fresh unbound holes.
        var (inW2, _) = engine.Heap.AllocateVariable();
        var (_, outR2) = engine.Heap.AllocateVariable();
        var (faultsW2, _) = engine.Heap.AllocateVariable();
        var result = LinkEstablish.WireEstablishedLink(
            engine, link, id, () => throw new InvalidOperationException("must not open a duplicate"),
            new VarRef(inW2), new VarRef(outR2), new VarRef(faultsW2), "test");
        Assert.Equal(BodyKernelResult.Abort, result);
        Assert.Equal(1, link.Links.Count);

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    // -------------------------------- 0x09 definition round-trip (role + cursors)

    [Fact]
    public async Task CaptureThenRestore_RoundTripsLinkDefinition_WithRole()
    {
        // A REAL kernel-established loopback link on a full GlpEngine (the kernel
        // stamps the role), with live cursor positions.
        var engine = new GlpEngine(_rootSelfGlp);
        var link = LinkKernels.Install(engine.Runtime);
        var transport = new LoopbackTransport();
        link.Transports.Register(transport);

        var (inW, _) = engine.Runtime.Heap.AllocateVariable();
        var (_, outR) = engine.Runtime.Heap.AllocateVariable();
        var (faultsW, _) = engine.Runtime.Heap.AllocateVariable();
        var listenTask = transport.ListenAsync(
            LinkScheme.Loopback, LinkAddress.Path("chan-rewire-4"), LinkOptions.Default);
        var kernel = engine.Runtime.BodyKernels.Lookup(LinkKernels.LinkSetupName, LinkKernels.LinkSetupArity)!;
        var result = kernel(engine.Runtime, new List<object?>
        {
            new StructTerm("link_id", new Term[]
            {
                new ConstTerm("loopback"), new ConstTerm("chan-rewire-4"), new ConstTerm(7L),
            }),
            new ConstTerm("connector"),
            new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        });
        Assert.Equal(BodyKernelResult.Success, result);
        var peer = await listenTask;

        var quiescence = new Quiescence(engine);
        var disarmed = quiescence.DisarmTimersForCapture()!;
        var blob = SnapshotCapture.Capture(
            engine, link, Array.Empty<LoadedUnit>(), File.ReadAllText(_rootSelfGlp),
            disarmed, "engine-test", 1);
        quiescence.RearmTimers(disarmed);

        var restored = SnapshotRestore.Restore(SnapshotBlob.Decode(blob.Encode()), _rootSelfGlp);
        var def = Assert.Single(restored.Links);
        Assert.Equal(new LinkId(LinkScheme.Loopback, LinkAddress.Path("chan-rewire-4"), LinkNonce.Int(7)), def.Id);
        Assert.Equal(LinkRole.Connector, def.Role); // the kernel-stamped role survived 0x09
        Assert.Equal(inW, def.InWriterAddr);
        Assert.Equal(outR, def.OutReaderAddr);
        Assert.Equal(faultsW, def.FaultsWriterAddr);
        Assert.Equal(new[] { faultsW }, def.MonitorCursors);
        Assert.Equal(0, def.EgressShippedCount); // nothing shipped before capture

        link.Pump.Dispose();
        await peer.DisposeAsync();
    }

    // ------------------------------------- snapshot defers while rewire pending

    [Fact]
    public async Task Snapshot_IsDeferred_WhileLinkRewireIsPending()
    {
        var engine = new GlpEngine(_rootSelfGlp);
        var link = LinkKernels.Install(engine.Runtime);
        link.Transports.Register(new LoopbackTransport());
        var session = new EngineSession("engine-test");
        session.TransitionTo(EngineState.Serving);
        var store = new SnapshotStore(
            null, new FileSnapshotStore(_storeDir, "engine-test"), _ => { });

        // A definition whose peer never appears: the rendezvous parks, Pending stays 1.
        var rewirer = new LinkRewirer(engine.Runtime, link);
        rewirer.Begin(new[]
        {
            new RestoredLinkDefinition(
                LoopbackId("chan-nobody-listens"), LinkRole.Connector,
                null, null, null, Array.Empty<int>(), 0),
        });
        var dispatcher = new RequestDispatcher(
            engine, session, new Quiescence(engine), store,
            link, File.ReadAllText(_rootSelfGlp), null, rewirer);

        // Quiescent engine, but the pending rewire defers the snapshot — a
        // not-yet-re-established link must never vanish from section 0x09.
        var response = await dispatcher.DispatchAsync(RequestFrame.Empty(1, RequestKind.Snapshot));
        Assert.Equal(ResponseKind.Deferred, response.Kind);
        Assert.Empty(store.List());

        var status = await dispatcher.DispatchAsync(RequestFrame.Empty(2, RequestKind.Status));
        Assert.Contains("pending_link_rewires=1", status.BodyText());

        link.Pump.Dispose();
    }

    // ------------------------------------ mid-restore: STATUS/PING only (rule 4)

    [Fact]
    public async Task MidRestore_OnlyStatusAndPingAnswered_RestElseEngineBusy()
    {
        var engine = new GlpEngine(_rootSelfGlp);
        var session = new EngineSession("engine-test");
        session.TransitionTo(EngineState.Restoring); // mid-restore
        var store = new SnapshotStore(
            null, new FileSnapshotStore(_storeDir, "engine-test"), _ => { });
        var dispatcher = new RequestDispatcher(
            engine, session, new Quiescence(engine), store, null, File.ReadAllText(_rootSelfGlp));

        // STATUS and PING are served (the supervisor's liveness view, wire rule 7).
        var status = await dispatcher.DispatchAsync(RequestFrame.Empty(1, RequestKind.Status));
        Assert.Equal(ResponseKind.Ack, status.Kind);
        Assert.Contains("state=restoring", status.BodyText());
        var ping = await dispatcher.DispatchAsync(RequestFrame.Empty(2, RequestKind.Ping));
        Assert.Equal(ResponseKind.Ack, ping.Kind);

        // Everything else: ENGINE_BUSY — never an answer from half-restored state.
        foreach (var kind in new[]
                 { RequestKind.LoadSource, RequestKind.RunGoal, RequestKind.Snapshot, RequestKind.Shutdown })
        {
            var busy = await dispatcher.DispatchAsync(RequestFrame.Empty(3, kind));
            Assert.Equal(ResponseKind.EngineBusy, busy.Kind);
        }
        Assert.Equal(EngineState.Restoring, session.State); // untouched by refusals
    }
}
