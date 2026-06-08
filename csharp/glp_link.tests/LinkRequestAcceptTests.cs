using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// T033 — the path-B establishment handshake (Option A, ratified 2026-06-07):
/// <c>request_link</c> (connector, kernel <c>'_link_request'</c>), the EXPLICIT
/// <c>request_listener</c> (kernel <c>'_link_listen'</c>), and <c>accept_link</c>
/// (kernel <c>'_link_accept'</c>). The headline property is FR-002 / risk R-5: the
/// request/accept path yields a link INDISTINGUISHABLE from a listen/connect one,
/// because all three converge on the shared <see cref="LinkEstablish.WireEstablishedLink"/>
/// core and the one T030 registry. Driven cross-instance (two engines over a shared
/// loopback): the requester runs on a background task, the rendezvous-listener +
/// acceptor on the test thread; the GLP wrappers + a real <c>=?=</c> match arrive in
/// T036/T043. Kernel args are passed directly (no compile pipeline yet).
/// </summary>
public class LinkRequestAcceptTests
{
    private const string Chan = "chan-ra";

    private static LinkId ExpectedId() =>
        new(LinkScheme.Loopback, LinkAddress.Path(Chan), LinkNonce.Int(1));

    private static Term LinkIdTerm() => new StructTerm("link_id", new Term[]
    {
        new ConstTerm("loopback"), new ConstTerm(Chan), new ConstTerm(1L),
    });

    private readonly record struct ReqEnds(GlpRuntimeEngine Engine, LinkRuntime Link, int InReader, int OutWriter);

    /// <summary>Run the requester (connector + in-band token + establish) on its own engine/thread.</summary>
    private static Task<ReqEnds> StartRequesterAsync(LoopbackTransport transport) => Task.Run(() =>
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        link.Transports.Register(transport);

        var (inW, inR) = engine.Heap.AllocateVariable();
        var (outW, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
        var args = new List<object?>
        {
            LinkIdTerm(), new ConstTerm("serverB"),
            new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        };
        var kernel = engine.BodyKernels.Lookup(LinkKernels.LinkRequestName, LinkKernels.LinkRequestArity)!;
        Assert.Equal(BodyKernelResult.Success, kernel(engine, args));
        return new ReqEnds(engine, link, inR, outW);
    });

    private static Term DerefHead(GlpRuntimeEngine e, int streamReader)
    {
        var cons = Assert.IsType<StructTerm>(e.Heap.Dereference(new VarRef(streamReader)));
        Assert.Equal(".", cons.Functor);
        return e.Heap.Dereference(cons.Args[0]);
    }

    [Fact]
    public async Task RequestAccept_ConvergesOnRegistry_EquivalentLink_BidirectionalData()
    {
        var transport = new LoopbackTransport();

        // 1) Requester begins (connects, ships request token, establishes its data link).
        var reqTask = StartRequesterAsync(transport);

        // 2) Listener side (engine B) — rendezvous + surface the request, then accept.
        var engineB = new GlpRuntimeEngine();
        var linkB = LinkKernels.Install(engineB);
        linkB.Transports.Register(transport);

        var (reqW, reqR) = engineB.Heap.AllocateVariable();
        var listen = engineB.BodyKernels.Lookup(LinkKernels.LinkListenName, LinkKernels.LinkListenArity)!;
        Assert.Equal(BodyKernelResult.Success, listen(engineB, new List<object?>
        {
            new ConstTerm("loopback"), new ConstTerm(Chan), new VarRef(reqW),
        }));

        // request_listener surfaced request(LinkId, FromPeer) on Requests (the explicit,
        // visible producer — the whole point of Option A).
        var token = Assert.IsType<StructTerm>(DerefHead(engineB, reqR));
        Assert.Equal("request", token.Functor);
        var (tokId, tokFrom) = LinkTerms.ParseRequestToken(token);
        Assert.Equal(ExpectedId(), tokId);
        Assert.Equal("requester", Assert.IsType<ConstTerm>(engineB.Heap.Dereference(tokFrom)).Value);

        // accept_link: the GLP =?= match is implied; the kernel adopts the pending conn.
        var (inWB, inRB) = engineB.Heap.AllocateVariable();
        var (outWB, outRB) = engineB.Heap.AllocateVariable();
        var (faultsWB, _) = engineB.Heap.AllocateVariable();
        var accept = engineB.BodyKernels.Lookup(LinkKernels.LinkAcceptName, LinkKernels.LinkAcceptArity)!;
        Assert.Equal(BodyKernelResult.Success, accept(engineB, new List<object?>
        {
            LinkIdTerm(), tokFrom, new VarRef(inWB), new VarRef(outRB), new VarRef(faultsWB),
        }));

        var req = await reqTask;

        // 3) FR-002 convergence: BOTH ends registered the SAME ground LinkId — an
        //    established link indistinguishable from the listen/connect path.
        Assert.True(req.Link.Links.TryGet(ExpectedId(), out _));
        Assert.True(linkB.Links.TryGet(ExpectedId(), out _));
        Assert.Empty(linkB.Pending); // the pending conn was adopted, not leaked

        // 4) Data flows both ways over the handshook link (symmetric, FR-003). Both
        //    engines are now idle on their own threads; drive each pump on this thread.
        // requester → acceptor
        var (_, aTailR) = req.Engine.Heap.AllocateVariable();
        req.Engine.Heap.BindVariable(req.OutWriter, new StructTerm(".", new Term[] { new ConstTerm("a2b"), new VarRef(aTailR) }));
        Assert.True(engineB.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));
        Assert.Equal("a2b", Assert.IsType<ConstTerm>(DerefHead(engineB, inRB)).Value);

        // acceptor → requester
        var (_, bTailR) = engineB.Heap.AllocateVariable();
        engineB.Heap.BindVariable(outWB, new StructTerm(".", new Term[] { new ConstTerm("b2a"), new VarRef(bTailR) }));
        Assert.True(req.Engine.InboundPump!.TryApplyNext(TimeSpan.FromSeconds(3)));
        Assert.Equal("b2a", Assert.IsType<ConstTerm>(DerefHead(req.Engine, req.InReader)).Value);

        req.Link.Pump.Dispose();
        linkB.Pump.Dispose();
    }

    [Fact]
    public void Accept_WithoutPendingRequest_Aborts()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        link.Transports.Register(new LoopbackTransport());

        var (inW, _) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
        var accept = engine.BodyKernels.Lookup(LinkKernels.LinkAcceptName, LinkKernels.LinkAcceptArity)!;

        // "accept before listen": no request_listener has parked a pending connection.
        var result = accept(engine, new List<object?>
        {
            LinkIdTerm(), new ConstTerm("requester"), new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        });
        Assert.Equal(BodyKernelResult.Abort, result);
    }

    [Fact]
    public void Request_NonGroundToPeer_Aborts()
    {
        var engine = new GlpRuntimeEngine();
        var link = LinkKernels.Install(engine);
        link.Transports.Register(new LoopbackTransport());

        var (inW, _) = engine.Heap.AllocateVariable();
        var (_, outR) = engine.Heap.AllocateVariable();
        var (faultsW, _) = engine.Heap.AllocateVariable();
        var (_, unboundR) = engine.Heap.AllocateVariable();
        var request = engine.BodyKernels.Lookup(LinkKernels.LinkRequestName, LinkKernels.LinkRequestArity)!;

        var result = request(engine, new List<object?>
        {
            LinkIdTerm(), new VarRef(unboundR), new VarRef(inW), new VarRef(outR), new VarRef(faultsW),
        });
        Assert.Equal(BodyKernelResult.Abort, result);
    }
}
