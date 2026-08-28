using Ynet.Transport.Capability;
using Ynet.Transport.HolePunch;
using Ynet.Transport.Tests.Integration;

namespace Ynet.Transport.Tests.Contract;

// ---- T017 / US2: hole-punch contract — punch within ≤5 s OR deterministic relay fallback; the
//      active path type (direct|relayed) is always surfaced (SC-002, FR-005/FR-018) ----
public class HolePunchTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static PunchOrchestrator Orchestrator(NatFabric fabric, NodeId host)
    {
        var (_, rv) = RendezvousHarness.SingleNode(host);
        return new PunchOrchestrator(fabric, rv, fabric, fabric, new SimClock());
    }

    // Publish the peer's candidates into the shared rendezvous (as the peer's own Establish would).
    private static PunchOrchestrator OrchestratorWithPeer(
        NatFabric fabric, NodeIdentity self, NodeIdentity peer, RendezvousMode mode)
    {
        var (_, rv) = RendezvousHarness.SingleNode(self.NodeId);
        rv.Publish(peer, fabric.Gather(peer.NodeId), mode, Now);
        return new PunchOrchestrator(fabric, rv, fabric, fabric, new SimClock());
    }

    [Fact]
    public void Two_cone_nats_punch_directly_and_surface_a_direct_path()
    {
        var fabric = new NatFabric();
        using var a = NodeIdentity.Generate();
        using var b = NodeIdentity.Generate();
        fabric.Register(a.NodeId, SimNat.EndpointIndependent);
        fabric.Register(b.NodeId, SimNat.EndpointIndependent);

        var outcome = OrchestratorWithPeer(fabric, a, b, RendezvousMode.DhtAddress)
            .Establish(a, b.NodeId, RendezvousMode.DhtAddress, Now);

        Assert.True(outcome.Ok);
        Assert.Equal(PathType.Direct, outcome.PathType); // path type surfaced (FR-005)
        Assert.Null(outcome.Refusal);
    }

    [Fact]
    public void A_symmetric_peer_falls_back_deterministically_to_a_relay_path()
    {
        var fabric = new NatFabric();
        using var a = NodeIdentity.Generate();
        using var b = NodeIdentity.Generate();
        fabric.Register(a.NodeId, SimNat.EndpointIndependent);
        fabric.Register(b.NodeId, SimNat.Symmetric, relayAdmitted: true); // not punchable, relay admitted

        var outcome = OrchestratorWithPeer(fabric, a, b, RendezvousMode.DhtAddress)
            .Establish(a, b.NodeId, RendezvousMode.DhtAddress, Now);

        Assert.True(outcome.Ok);
        Assert.Equal(PathType.Relayed, outcome.PathType); // relayed path surfaced, not a silent drop
    }

    [Fact]
    public void An_exhausted_budget_falls_back_to_a_relay_even_for_a_punchable_peer()
    {
        var fabric = new NatFabric();
        using var a = NodeIdentity.Generate();
        using var b = NodeIdentity.Generate();
        fabric.Register(a.NodeId, SimNat.EndpointIndependent);
        fabric.Register(b.NodeId, SimNat.EndpointIndependent, relayAdmitted: true);

        // Zero budget: the ≤5 s gate refuses the direct attempt and the fallback is taken deterministically.
        var outcome = OrchestratorWithPeer(fabric, a, b, RendezvousMode.DhtAddress)
            .Establish(a, b.NodeId, RendezvousMode.DhtAddress, Now, budget: TimeSpan.Zero);

        Assert.True(outcome.Ok);
        Assert.Equal(PathType.Relayed, outcome.PathType);
    }

    [Fact]
    public void No_direct_punch_and_no_admitted_relay_is_a_distinct_unreachable_refusal()
    {
        var fabric = new NatFabric();
        using var a = NodeIdentity.Generate();
        using var b = NodeIdentity.Generate();
        fabric.Register(a.NodeId, SimNat.EndpointIndependent);
        fabric.Register(b.NodeId, SimNat.Symmetric, relayAdmitted: false); // no path at all

        var outcome = OrchestratorWithPeer(fabric, a, b, RendezvousMode.DhtAddress)
            .Establish(a, b.NodeId, RendezvousMode.DhtAddress, Now);

        Assert.False(outcome.Ok);
        Assert.Equal(RefusalReason.Unreachable, outcome.Refusal); // FR-018 distinct, never silent
        Assert.Null(outcome.PathType);
    }

    [Fact]
    public void Hidden_service_rendezvous_also_coordinates_a_direct_punch()
    {
        var fabric = new NatFabric();
        using var a = NodeIdentity.Generate();
        using var b = NodeIdentity.Generate();
        fabric.Register(a.NodeId, SimNat.EndpointIndependent);
        fabric.Register(b.NodeId, SimNat.EndpointIndependent);

        var outcome = OrchestratorWithPeer(fabric, a, b, RendezvousMode.HiddenService)
            .Establish(a, b.NodeId, RendezvousMode.HiddenService, Now);

        Assert.True(outcome.Ok);
        Assert.Equal(PathType.Direct, outcome.PathType);
    }
}
