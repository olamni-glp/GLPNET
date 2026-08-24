using Ynet.Transport.Capability;
using Ynet.Transport.HolePunch;
using Ynet.Transport.Link;

namespace Ynet.Transport.Tests.Integration;

// ---- T022 / US2: NAT traversal end-to-end (SC-002) — cone→direct ≥90% within 5 s; symmetric→relay
//      fallback with ZERO pending-frame loss ----
public class NatTraversalTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    private static PunchOutcome Establish(
        NatFabric fabric, NodeIdentity self, NodeIdentity peer, RendezvousMode mode, TimeSpan? budget = null)
    {
        var (_, rv) = RendezvousHarness.SingleNode(self.NodeId);
        rv.Publish(peer, fabric.Gather(peer.NodeId), mode, Now); // peer advertised its candidates
        var orch = new PunchOrchestrator(fabric, rv, fabric, fabric, new SimClock());
        return orch.Establish(self, peer.NodeId, mode, Now, budget: budget);
    }

    [Fact]
    public void Cone_to_cone_punches_directly_in_at_least_90_percent_of_trials()
    {
        // Model the coordinated-open timing window: ~97% of first attempts land (iroh/libp2p field
        // envelope, research R1). A seeded PRNG keeps the statistical assertion reproducible.
        var rng = new Random(Seed: 20260714);
        var fabric = new NatFabric(punchWindowLands: () => rng.NextDouble() < 0.97);

        const int trials = 200;
        int direct = 0, relayed = 0, unreachable = 0;
        for (int i = 0; i < trials; i++)
        {
            using var a = NodeIdentity.Generate();
            using var b = NodeIdentity.Generate();
            fabric.Register(a.NodeId, SimNat.EndpointIndependent);
            fabric.Register(b.NodeId, SimNat.EndpointIndependent, relayAdmitted: true);

            var outcome = Establish(fabric, a, b, RendezvousMode.DhtAddress);
            switch (outcome.PathType)
            {
                case PathType.Direct: direct++; break;
                case PathType.Relayed: relayed++; break;
                default: unreachable++; break;
            }
        }

        Assert.Equal(0, unreachable);                 // a relay is always admitted → never unreachable
        Assert.Equal(trials, direct + relayed);       // every trial resolved to a surfaced path type
        Assert.True(direct >= trials * 0.90, $"direct punch rate {direct}/{trials} < 90%");
    }

    [Fact]
    public void Symmetric_peer_relays_with_zero_pending_frame_loss()
    {
        var fabric = new NatFabric();
        using var a = NodeIdentity.Generate();
        using var b = NodeIdentity.Generate();
        fabric.Register(a.NodeId, SimNat.EndpointIndependent);
        fabric.Register(b.NodeId, SimNat.Symmetric, relayAdmitted: true);

        var outcome = Establish(fabric, a, b, RendezvousMode.DhtAddress);
        Assert.Equal(PathType.Relayed, outcome.PathType);

        // Drive a REAL YnetSession over the relayed channel and prove every queued frame is delivered
        // in order (zero pending-frame loss on fallback, SC-002 / US2 AS2).
        var channelA = (IWireChannel)outcome.Channel!;
        var channelB = fabric.TakePeerEnd(b.NodeId);

        var acceptTask = System.Threading.Tasks.Task.Run(
            () => YnetSession.Accept(channelB, b, RoutingSelection.SafeDefault));
        var dial = YnetSession.Connect(channelA, a, b.NodeId, RoutingSelection.SafeDefault);
        var accepted = acceptTask.GetAwaiter().GetResult();
        Assert.True(dial.Ok);
        Assert.True(accepted.Ok);

        var pending = new[]
        {
            "frame-1"u8.ToArray(), "frame-2"u8.ToArray(), "frame-3"u8.ToArray(),
            "frame-4"u8.ToArray(), "frame-5"u8.ToArray(),
        };
        foreach (var f in pending) Assert.True(dial.Value!.Send(f).Ok);

        for (int i = 0; i < pending.Length; i++)
        {
            var got = accepted.Value!.Receive();
            Assert.True(got.Ok);
            Assert.Equal(pending[i], got.Value.ToArray()); // in order, none lost
        }

        dial.Value!.Close();
        accepted.Value!.Close();
        dial.Value.Dispose();
        accepted.Value.Dispose();
    }

    [Fact]
    public void Symmetric_peer_without_an_admitted_relay_is_unreachable_not_dropped()
    {
        var fabric = new NatFabric();
        using var a = NodeIdentity.Generate();
        using var b = NodeIdentity.Generate();
        fabric.Register(a.NodeId, SimNat.EndpointIndependent);
        fabric.Register(b.NodeId, SimNat.Symmetric, relayAdmitted: false);

        var outcome = Establish(fabric, a, b, RendezvousMode.DhtAddress);

        Assert.False(outcome.Ok);
        Assert.Equal(RefusalReason.Unreachable, outcome.Refusal);
    }
}
