using System.Text;
using Ynet.Transport.Capability;
using Ynet.Transport.Dht;

namespace Ynet.Transport.Tests.Unit;

// ---- T019 / US2 foundation: embedded S-Kademlia store/lookup (FR-006, curated overlay) ----
public class SKademliaTests
{
    // A small in-process overlay: node ids map to live nodes via the resolve seam (UDP RPC in prod).
    private static Dictionary<NodeId, SKademliaNode> Overlay(int count, out List<SKademliaNode> nodes)
    {
        var map = new Dictionary<NodeId, SKademliaNode>();
        SKademliaNode? Resolve(NodeId id) => map.TryGetValue(id, out var n) ? n : null;
        nodes = new List<SKademliaNode>();
        for (int i = 0; i < count; i++)
        {
            var id = new NodeId($"kad-node-{i}");
            var node = new SKademliaNode(id, $"sim://{id.Value}", Resolve);
            map[id] = node;
            nodes.Add(node);
        }
        // Fully connect through node0 as bootstrap (star): every node knows node0; node0 knows every node.
        foreach (var n in nodes.Skip(1)) n.Join(nodes[0].Self);
        foreach (var n in nodes.Skip(1)) nodes[0].Join(n.Self);
        return map;
    }

    [Fact]
    public void A_stored_reachability_record_is_found_by_an_unrelated_node()
    {
        _ = Overlay(6, out var nodes);
        var now = DateTimeOffset.UnixEpoch;
        using var owner = NodeIdentity.Generate();

        var record = SignedRecord.CreateReachability(owner, "endpoint=1.2.3.4:40000"u8.ToArray(), now, TimeSpan.FromMinutes(10));
        Assert.True(nodes[0].Store(record, now));

        // an unrelated node looks it up by the owner's key
        var key = Encoding.ASCII.GetBytes(owner.NodeId.Value);
        var found = nodes[5].Lookup(key, now);

        Assert.NotNull(found);
        Assert.Equal(owner.NodeId, found!.SignerNodeId);
        Assert.True(found.VerifySelfCertified(now));
    }

    [Fact]
    public void A_reachability_record_keyed_under_someone_elses_node_id_is_refused()
    {
        _ = Overlay(4, out var nodes);
        var now = DateTimeOffset.UnixEpoch;
        using var owner = NodeIdentity.Generate();
        using var victim = NodeIdentity.Generate();

        // A validly-signed record, but the DHT key claims the VICTIM's node id — a spoof (S-Kademlia
        // secure-node-id: the key of a reachability record MUST be the signer's own H(pubkey)).
        var spoof = SignedRecord.Create(
            owner,
            Encoding.ASCII.GetBytes(victim.NodeId.Value), // wrong key
            RecordKind.Reachability,
            "endpoint=evil"u8.ToArray(), now, TimeSpan.FromMinutes(10));

        Assert.False(spoof.VerifySelfCertified(now));   // record itself does not self-certify
        Assert.False(nodes[0].Store(spoof, now));       // and the DHT refuses to store/serve it
        Assert.Null(nodes[3].Lookup(Encoding.ASCII.GetBytes(victim.NodeId.Value), now));
    }

    [Fact]
    public void A_tampered_record_is_rejected_regardless_of_the_serving_hop()
    {
        _ = Overlay(4, out var nodes);
        var now = DateTimeOffset.UnixEpoch;
        using var owner = NodeIdentity.Generate();

        var good = SignedRecord.CreateReachability(owner, "endpoint=ok"u8.ToArray(), now, TimeSpan.FromMinutes(10));
        // Flip a payload byte AFTER signing — signature no longer matches the canonical bytes.
        var tampered = good with { Payload = "endpoint=XX"u8.ToArray() };

        Assert.False(tampered.VerifySelfCertified(now));
        Assert.False(nodes[0].Store(tampered, now)); // a node never stores what it cannot verify
    }

    [Fact]
    public void An_expired_record_does_not_resolve()
    {
        _ = Overlay(4, out var nodes);
        var t0 = DateTimeOffset.UnixEpoch;
        using var owner = NodeIdentity.Generate();

        var record = SignedRecord.CreateReachability(owner, "endpoint=1"u8.ToArray(), t0, TimeSpan.FromMinutes(5));
        Assert.True(nodes[0].Store(record, t0));

        var key = Encoding.ASCII.GetBytes(owner.NodeId.Value);
        Assert.NotNull(nodes[3].Lookup(key, t0));                              // live now
        Assert.Null(nodes[3].Lookup(key, t0 + TimeSpan.FromMinutes(6)));       // expired later
    }
}
