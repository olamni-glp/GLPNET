using System.Text;
using Ynet.Transport.Capability;
using Ynet.Transport.Dht;

namespace Ynet.Transport.Tests.Integration;

// ---- T027 / US3 (SC-003): N-node embedded S-Kademlia — a self-certified record stored by one node
//      is discoverable by iterative lookup from an unrelated node, and a tampered / spoofed record is
//      rejected regardless of the serving hop. Driven end-to-end through the IYnetTransport surface
//      over the real InProcessFabric overlay (the QUIC/UDP RPC swaps in behind the same seam). ----
public class DhtDiscoveryTests
{
    private const int N = 12; // > SKademliaNode.K so lookups route rather than always hit locally.

    private static (InProcessFabric Fabric, List<IYnetTransport> Nodes, List<NodeIdentity> Ids) Overlay()
    {
        var fabric = new InProcessFabric(clock: () => DateTimeOffset.UnixEpoch);
        var ids = new List<NodeIdentity>();
        var nodes = new List<IYnetTransport>();
        for (int i = 0; i < N; i++)
        {
            var id = NodeIdentity.Generate();
            ids.Add(id);
            nodes.Add(fabric.AttachNode(id));
        }
        fabric.FormOverlay();
        return (fabric, nodes, ids);
    }

    [Fact]
    public void A_record_stored_at_one_node_is_found_by_every_other_node()
    {
        var (_, nodes, ids) = Overlay();
        try
        {
            var now = DateTimeOffset.UnixEpoch;
            var owner = ids[0];
            var record = SignedRecord.CreateReachability(owner, "endpoint=10.0.0.1:443"u8.ToArray(), now, TimeSpan.FromMinutes(30));

            Assert.True(nodes[3].DhtStore(record).Ok); // stored from an arbitrary node

            var key = Encoding.ASCII.GetBytes(owner.NodeId.Value);
            for (int i = 0; i < N; i++)
            {
                var found = nodes[i].DhtLookup(key);
                Assert.True(found.Ok, $"node {i} could not resolve the record");
                Assert.Equal(owner.NodeId, found.Value!.SignerNodeId);
                Assert.True(found.Value!.VerifySelfCertified(now));
            }
        }
        finally { foreach (var id in ids) id.Dispose(); }
    }

    [Fact]
    public void Many_distinct_records_are_each_resolvable_from_an_unrelated_node()
    {
        var (_, nodes, ids) = Overlay();
        try
        {
            var now = DateTimeOffset.UnixEpoch;
            // Each node publishes its own reachability record; a single unrelated node resolves them all.
            for (int i = 0; i < N; i++)
            {
                var rec = SignedRecord.CreateReachability(ids[i], Encoding.ASCII.GetBytes($"endpoint=node-{i}"), now, TimeSpan.FromMinutes(30));
                Assert.True(nodes[i].DhtStore(rec).Ok);
            }

            var resolver = nodes[N - 1];
            for (int i = 0; i < N; i++)
            {
                var found = resolver.DhtLookup(Encoding.ASCII.GetBytes(ids[i].NodeId.Value));
                Assert.True(found.Ok, $"could not resolve node {i}'s record");
                Assert.Equal(ids[i].NodeId, found.Value!.SignerNodeId);
            }
        }
        finally { foreach (var id in ids) id.Dispose(); }
    }

    [Fact]
    public void A_tampered_record_is_rejected_and_never_shadows_the_authentic_one()
    {
        var (_, nodes, ids) = Overlay();
        try
        {
            var now = DateTimeOffset.UnixEpoch;
            var owner = ids[0];
            var key = Encoding.ASCII.GetBytes(owner.NodeId.Value);

            var authentic = SignedRecord.CreateReachability(owner, "endpoint=authentic"u8.ToArray(), now, TimeSpan.FromMinutes(30));
            Assert.True(nodes[2].DhtStore(authentic).Ok);

            // An attacker tries to overwrite the same key with a payload-tampered variant.
            var tampered = authentic with { Payload = "endpoint=attacker"u8.ToArray() };
            var rejected = nodes[5].DhtStore(tampered);
            Assert.False(rejected.Ok);
            Assert.Equal(RefusalReason.RecordRejected, rejected.Reason);

            // Every node still resolves ONLY the authentic payload — no hop serves the tampered one.
            for (int i = 0; i < N; i++)
            {
                var found = nodes[i].DhtLookup(key);
                Assert.True(found.Ok);
                Assert.Equal("endpoint=authentic"u8.ToArray(), found.Value!.Payload);
            }
        }
        finally { foreach (var id in ids) id.Dispose(); }
    }

    [Fact]
    public void An_expired_record_stops_resolving_after_its_ttl()
    {
        // A dedicated fabric whose clock we advance past the record TTL.
        var t0 = DateTimeOffset.UnixEpoch;
        var clockNow = t0;
        var fabric = new InProcessFabric(clock: () => clockNow);
        var ids = new List<NodeIdentity>();
        var nodes = new List<IYnetTransport>();
        for (int i = 0; i < N; i++)
        {
            var id = NodeIdentity.Generate();
            ids.Add(id);
            nodes.Add(fabric.AttachNode(id));
        }
        fabric.FormOverlay();
        try
        {
            var owner = ids[0];
            var key = Encoding.ASCII.GetBytes(owner.NodeId.Value);
            var record = SignedRecord.CreateReachability(owner, "endpoint=ttl"u8.ToArray(), t0, TimeSpan.FromMinutes(5));
            Assert.True(nodes[4].DhtStore(record).Ok);

            Assert.True(nodes[9].DhtLookup(key).Ok); // live now

            clockNow = t0 + TimeSpan.FromMinutes(6); // TTL elapsed
            var afterExpiry = nodes[9].DhtLookup(key);
            Assert.False(afterExpiry.Ok);
            Assert.Equal(RefusalReason.RecordNotFound, afterExpiry.Reason);
        }
        finally { foreach (var id in ids) id.Dispose(); }
    }
}
