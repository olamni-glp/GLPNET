using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.Runtime;
using Xunit;

namespace GlpRuntime.Link.Tests;

/// <summary>T030 foundational infra: term mapping, transport selection, idempotent registry.</summary>
public class LinkInfraTests
{
    // ---- LinkTerms round-trip ----

    [Fact]
    public void LinkId_RoundTrip_PathEndpoint_IntNonce()
    {
        var id = new LinkId(LinkScheme.Ws, LinkAddress.Path("hostB/path"), LinkNonce.Int(7));
        var term = LinkTerms.ToTerm(id);
        Assert.Equal(id, LinkTerms.ParseLinkId(term));
    }

    [Fact]
    public void LinkId_RoundTrip_EpEndpoint_StringNonce()
    {
        var id = new LinkId(LinkScheme.Mqtt, LinkAddress.Endpoint("broker", 1883), LinkNonce.Str("n1"));
        var term = LinkTerms.ToTerm(id);
        var parsed = LinkTerms.ParseLinkId(term);
        Assert.Equal(id, parsed);
        Assert.Equal(1883, parsed.Endpoint.Port);
    }

    [Fact]
    public void ParseLinkId_FromHandWrittenTerm()
    {
        // link_id("ws", ep("h", 9001), 1)
        var term = new StructTerm("link_id", new Term[]
        {
            new ConstTerm("ws"),
            new StructTerm("ep", new Term[] { new ConstTerm("h"), new ConstTerm(9001L) }),
            new ConstTerm(1L),
        });
        var id = LinkTerms.ParseLinkId(term);
        Assert.Equal(LinkScheme.Ws, id.Scheme);
        Assert.Equal("h", id.Endpoint.Host);
        Assert.Equal(9001, id.Endpoint.Port);
        Assert.True(id.Nonce.IsInteger);
        Assert.Equal(1L, id.Nonce.IntValue);
    }

    [Fact]
    public void ParseLinkId_RejectsMalformed()
    {
        Assert.Throws<ArgumentException>(() => LinkTerms.ParseLinkId(new ConstTerm("nope")));
        Assert.Throws<ArgumentException>(() => LinkTerms.ParseLinkId(
            new StructTerm("link_id", new Term[] { new ConstTerm("ws") }))); // wrong arity
        Assert.Throws<ArgumentException>(() => LinkTerms.ParseLinkId(
            new StructTerm("link_id", new Term[] { new VarRef(3), new ConstTerm("h"), new ConstTerm(1L) }))); // unresolved
    }

    [Fact]
    public void ParseRole()
    {
        Assert.Equal(LinkRole.Listener, LinkTerms.ParseRole(new ConstTerm("listener")));
        Assert.Equal(LinkRole.Connector, LinkTerms.ParseRole(new ConstTerm("connector")));
        Assert.Throws<ArgumentException>(() => LinkTerms.ParseRole(new ConstTerm("bystander")));
    }

    [Fact]
    public void FaultTerms_HaveExpectedShape()
    {
        var id = new LinkId(LinkScheme.Loopback, LinkAddress.Path("c"), LinkNonce.Int(1));
        Assert.Equal("ok", Assert.IsType<ConstTerm>(LinkTerms.Ok()).Value);

        var closed = Assert.IsType<StructTerm>(LinkTerms.Closed(id, LinkTerms.GracefulReason));
        Assert.Equal("closed", closed.Functor);
        Assert.Equal(2, closed.Args.Count);
        Assert.Equal("eos", Assert.IsType<ConstTerm>(closed.Args[1]).Value);

        Assert.Equal("tempFail", Assert.IsType<StructTerm>(LinkTerms.TempFail(id, "silence")).Functor);
        Assert.Equal("permFail", Assert.IsType<StructTerm>(LinkTerms.PermFail(id, "gave up")).Functor);
    }

    [Fact]
    public void FromSignal_MapsKinds()
    {
        var id = new LinkId(LinkScheme.Loopback, LinkAddress.Path("c"), LinkNonce.Int(1));
        Assert.Equal("closed", ((StructTerm)LinkTerms.FromSignal(new LinkFaultSignal(id, LinkFaultKind.Closed, "x"))).Functor);
        Assert.Equal("tempFail", ((StructTerm)LinkTerms.FromSignal(new LinkFaultSignal(id, LinkFaultKind.Transient, "x"))).Functor);
        Assert.Equal("permFail", ((StructTerm)LinkTerms.FromSignal(new LinkFaultSignal(id, LinkFaultKind.Permanent, "x"))).Functor);
    }

    // ---- TransportRegistry ----

    [Fact]
    public void TransportRegistry_SelectsByScheme()
    {
        var reg = new TransportRegistry();
        var loop = new LoopbackTransport();
        reg.Register(loop);
        Assert.Same(loop, reg.Select(LinkScheme.Loopback));
        Assert.Throws<KeyNotFoundException>(() => reg.Select(LinkScheme.Ws));
        Assert.False(reg.TrySelect(LinkScheme.Mqtt, out _));
    }

    // ---- LinkRegistry idempotency (FR-007) ----

    [Fact]
    public async Task LinkRegistry_IdempotentAtIdentity()
    {
        var t = new LoopbackTransport();
        var listen = t.ListenAsync(LinkScheme.Loopback, LinkAddress.Path("c"), LinkOptions.Default);
        var ep = await t.ConnectAsync(LinkScheme.Loopback, LinkAddress.Path("c"), LinkOptions.Default);
        await listen;

        var reg = new LinkRegistry();
        var id = ep.Id;
        int establishCount = 0;
        LinkHandle Establish() { establishCount++; return new LinkHandle(id, ep, LinkOptions.Default); }

        var h1 = reg.GetOrEstablish(id, Establish);
        var h2 = reg.GetOrEstablish(id, Establish); // same identity → reuse, no re-establish
        Assert.Same(h1, h2);
        Assert.Equal(1, establishCount);
        Assert.Equal(1, reg.Count);

        Assert.True(reg.Remove(id)); // GC
        Assert.Equal(0, reg.Count);
    }

    [Fact]
    public void LinkHandle_DerivesWindowFromOptions()
    {
        var id = new LinkId(LinkScheme.Loopback, LinkAddress.Path("c"), LinkNonce.Int(1));
        var handle = new LinkHandle(id, new StubEndpoint(id), LinkOptions.Default with { BackpressureWindow = 4 });
        Assert.Equal(4, handle.Window.Capacity);
    }

    private sealed class StubEndpoint : ILinkEndpoint
    {
        public StubEndpoint(LinkId id) => Id = id;
        public LinkId Id { get; }
        public event Action<LinkFaultSignal>? OnFault { add { } remove { } }
        public Task SendBytesAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default) => Task.CompletedTask;
        public Task<byte[]?> RecvBytesAsync(CancellationToken ct = default) => Task.FromResult<byte[]?>(null);
        public Task CloseAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
