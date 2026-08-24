using Ynet.Transport.Capability;

namespace Ynet.Transport.Tests.Integration;

// ---- T015 / T016 / US1 AS4: first-class capability resolution + full link over the fabric ----
//
// A minimal 056-side stub: it knows ONLY the capability-type token and the IYnetTransport surface
// (contracts/transport-capability.md) — no embed/macaroon/admission logic leaks into this tier
// (FR-004/FR-024). It resolves the capability first-class and drives it.
file sealed class Stub056Resolver
{
    private readonly CapabilityRegistration _registration;
    public Stub056Resolver(CapabilityRegistration registration) => _registration = registration;

    public IYnetTransport ResolveOrThrow(string capabilityType)
        => _registration.Resolve(capabilityType)
           ?? throw new InvalidOperationException($"no capability exposed under '{capabilityType}'");
}

public class DirectLinkTests
{
    private static readonly TimeSpan AcceptTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public void A_056_stub_resolves_the_capability_first_class_under_both_network_tokens()
    {
        var fabric = new InProcessFabric();
        using var id = NodeIdentity.Generate();
        using var node = fabric.AttachNode(id);
        var stub = new Stub056Resolver(CapabilityRegistration.ForNode(node));

        var viaUdp = stub.ResolveOrThrow(CapabilityTypes.Udp);
        var viaSocket = stub.ResolveOrThrow(CapabilityTypes.Socket);

        // one first-class capability, not two shims (FR-004)
        Assert.Same(node, viaUdp);
        Assert.Same(viaUdp, viaSocket);
    }

    [Fact]
    public void Full_connect_send_receive_close_between_two_in_process_nodes_via_the_capability()
    {
        var fabric = new InProcessFabric();
        using var idA = NodeIdentity.Generate();
        using var idB = NodeIdentity.Generate();
        using var nodeA = fabric.AttachNode(idA);
        using var nodeB = fabric.AttachNode(idB);

        // the 056 stub sees only the resolved IYnetTransport surface
        IYnetTransport a = new Stub056Resolver(CapabilityRegistration.ForNode(nodeA))
            .ResolveOrThrow(CapabilityTypes.Udp);

        var link = a.Connect(idB.NodeId, RoutingSelection.SafeDefault);
        Assert.True(link.Ok);
        Assert.Equal(idB.NodeId, link.Value.Peer); // identity verified pre-frame (FR-002)

        Assert.True(nodeB.TryAcceptLink(AcceptTimeout, out var inbound));
        Assert.Equal(idA.NodeId, inbound.Peer); // listener learned the dialer's verified identity

        // dialer -> listener over the sealed session
        var payload = "hello through the first-class capability"u8.ToArray();
        Assert.True(a.Send(link.Value, payload).Ok);
        var got = nodeB.Receive(inbound);
        Assert.True(got.Ok);
        Assert.Equal(payload, got.Value.ToArray());

        // listener -> dialer (other per-direction key)
        var reply = "ack"u8.ToArray();
        Assert.True(nodeB.Send(inbound, reply).Ok);
        var back = a.Receive(link.Value);
        Assert.True(back.Ok);
        Assert.Equal(reply, back.Value.ToArray());

        // introspection (FR-023): a direct in-process path, sealed safe default
        var info = a.PathInfo(link.Value);
        Assert.Equal(PathType.Direct, info.PathType);
        Assert.Equal(RoutingMode.Sealed, info.Mode);

        // graceful close: the handle is gone, a late send refuses distinctly (no silent drop)
        a.Close(link.Value);
        var late = a.Send(link.Value, "late"u8.ToArray());
        Assert.False(late.Ok);
        Assert.Equal(RefusalReason.AuthorizedButUnreachable, late.Reason);
    }

    [Fact]
    public void Connect_to_an_unknown_node_refuses_unreachable_with_zero_side_effects()
    {
        var fabric = new InProcessFabric();
        using var id = NodeIdentity.Generate();
        using var node = fabric.AttachNode(id);
        using var stranger = NodeIdentity.Generate(); // never attached

        var r = node.Connect(stranger.NodeId, RoutingSelection.SafeDefault);

        Assert.False(r.Ok);
        Assert.Equal(RefusalReason.Unreachable, r.Reason); // FR-018 distinct reason
        Assert.Equal(0, node.LiveSessionCount);            // invariant 2: nothing tracked, no packet
    }

    [Fact]
    public void Operations_on_an_unknown_handle_refuse_distinctly()
    {
        var fabric = new InProcessFabric();
        using var id = NodeIdentity.Generate();
        using var node = fabric.AttachNode(id);
        var bogus = new LinkHandle(Guid.NewGuid(), new NodeId("nobody"), PathType.Direct);

        Assert.Equal(RefusalReason.AuthorizedButUnreachable, node.Send(bogus, "x"u8.ToArray()).Reason);
        Assert.Equal(RefusalReason.AuthorizedButUnreachable, node.Receive(bogus).Reason);
        node.Close(bogus); // closing an unknown handle is a safe no-op
    }

    [Fact]
    public void Ambiguous_capability_re_registration_throws()
    {
        var fabric = new InProcessFabric();
        using var idA = NodeIdentity.Generate();
        using var idB = NodeIdentity.Generate();
        using var nodeA = fabric.AttachNode(idA);
        using var nodeB = fabric.AttachNode(idB);

        var reg = CapabilityRegistration.ForNode(nodeA);
        reg.Register(CapabilityTypes.Udp, nodeA); // same instance: no-op
        Assert.Throws<InvalidOperationException>(() => reg.Register(CapabilityTypes.Udp, nodeB));
    }
}
