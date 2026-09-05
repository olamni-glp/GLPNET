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

    // ---- Q-olg15-02 CLOSED (was DEFECT PROBE, shiras-qhstate 20260905T0240Z ACK-COMPLIANCE) ----
    // The probe below measured the SAME spoof as the Reachability test above, under the OTHER record
    // kind. It used to assert that the spoof SUCCEEDED, because VerifySelfCertified's signer<->key
    // binding was guarded by `Kind == RecordKind.Reachability` and KeyToRecord carried no binding.
    //
    // Engineer ruling Q-olg15-02 (2026-09-05): bind every kind, refuse unbound. The asserts below are
    // the INVERSION the probe's own comment called for. Measured first: KeyToRecord had ZERO
    // production producers, so no live record legitimately used a non-signer key.
    [Fact]
    public void A_KeyToRecord_keyed_under_someone_elses_node_id_is_refused()
    {
        _ = Overlay(4, out var nodes);
        var now = DateTimeOffset.UnixEpoch;
        using var owner = NodeIdentity.Generate();
        using var victim = NodeIdentity.Generate();

        var spoof = SignedRecord.Create(
            owner,
            Encoding.ASCII.GetBytes(victim.NodeId.Value), // the VICTIM's key
            RecordKind.KeyToRecord,
            "endpoint=evil"u8.ToArray(), now, TimeSpan.FromMinutes(10));

        Assert.False(spoof.VerifySelfCertified(now));  // the binding now covers this kind
        Assert.False(nodes[0].Store(spoof, now));      // and the DHT refuses to store it
        Assert.Null(nodes[3].Lookup(Encoding.ASCII.GetBytes(victim.NodeId.Value), now));
    }

    // POSITIVE CONTROL: the fix must not make KeyToRecord unusable. A signer writing in its OWN
    // namespace still stores, still serves, and still verifies end to end.
    [Fact]
    public void A_KeyToRecord_in_the_signers_own_namespace_stores_and_serves()
    {
        _ = Overlay(4, out var nodes);
        var now = DateTimeOffset.UnixEpoch;
        using var owner = NodeIdentity.Generate();

        var rec = SignedRecord.CreateKeyToRecord(owner, "svc/oracle", "endpoint=ok"u8.ToArray(), now, TimeSpan.FromMinutes(10));

        Assert.True(rec.VerifySelfCertified(now));
        Assert.True(nodes[0].Store(rec, now));

        var served = nodes[3].Lookup(SignedRecord.KeyToRecordKey(owner.NodeId, "svc/oracle"), now);
        Assert.NotNull(served);
        Assert.Equal(owner.NodeId, served!.SignerNodeId);
    }

    // A victim's namespace PREFIX is not enough: the attacker must own the node id that prefixes the
    // key. Signing with the attacker's key while writing under the victim's prefix is still refused.
    [Fact]
    public void A_KeyToRecord_inside_a_victims_namespace_is_refused()
    {
        var now = DateTimeOffset.UnixEpoch;
        using var attacker = NodeIdentity.Generate();
        using var victim = NodeIdentity.Generate();

        var spoof = SignedRecord.Create(
            attacker,
            SignedRecord.KeyToRecordKey(victim.NodeId, "svc/oracle"), // victim's namespace
            RecordKind.KeyToRecord,
            "endpoint=evil"u8.ToArray(), now, TimeSpan.FromMinutes(10));

        Assert.False(spoof.VerifySelfCertified(now));
    }

    // The bare node id is the REACHABILITY key, not a KeyToRecord key. An empty name is not a
    // namespace member, so a KeyToRecord may not squat on its owner's own reachability slot.
    [Fact]
    public void A_KeyToRecord_may_not_squat_on_the_reachability_key()
    {
        var now = DateTimeOffset.UnixEpoch;
        using var owner = NodeIdentity.Generate();

        var bare = SignedRecord.Create(
            owner, SignedRecord.ReachabilityKey(owner.NodeId), RecordKind.KeyToRecord,
            "x"u8.ToArray(), now, TimeSpan.FromMinutes(10));
        Assert.False(bare.VerifySelfCertified(now));

        var emptyName = SignedRecord.Create(
            owner, Encoding.UTF8.GetBytes(owner.NodeId.Value + "/"), RecordKind.KeyToRecord,
            "x"u8.ToArray(), now, TimeSpan.FromMinutes(10));
        Assert.False(emptyName.VerifySelfCertified(now));
    }

    // A prefix that merely STARTS with the owner's id is not the owner's namespace either — the
    // separator must be present, or `<id>evil` would pass a naive StartsWith check.
    [Fact]
    public void A_KeyToRecord_key_that_only_starts_with_the_owner_id_is_refused()
    {
        var now = DateTimeOffset.UnixEpoch;
        using var owner = NodeIdentity.Generate();

        var noSeparator = SignedRecord.Create(
            owner, Encoding.UTF8.GetBytes(owner.NodeId.Value + "evil"), RecordKind.KeyToRecord,
            "x"u8.ToArray(), now, TimeSpan.FromMinutes(10));
        Assert.False(noSeparator.VerifySelfCertified(now));
    }

    // CreateKeyToRecord refuses an empty name at the source rather than minting a record that
    // VerifySelfCertified would silently reject later.
    [Fact]
    public void CreateKeyToRecord_refuses_an_empty_name()
    {
        using var owner = NodeIdentity.Generate();
        Assert.Throws<ArgumentException>(() => SignedRecord.CreateKeyToRecord(
            owner, "", "x"u8.ToArray(), DateTimeOffset.UnixEpoch, TimeSpan.FromMinutes(10)));
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
