using System.Text;
using Ynet.Transport.Capability;
using Ynet.Transport.Dht;

namespace Ynet.Transport.Tests.Contract;

// ---- T023 / US3: DHT discovery contract on the IYnetTransport surface (FR-006/FR-017, SC-003) ----
//
// dht_store(SignedRecord) -> Stored | Refusal ; dht_lookup(key) -> SignedRecord | NotFound |
// FurtherResolverRequired (contracts/transport-capability.md). Every refusal carries exactly one
// distinct reason (contract invariant 1); a signature-invalid record never round-trips; a
// human-memorable name is never fabricated — it is routed to the further resolver.
public class DhtRecordTests
{
    // Two co-hosted nodes joined into one curated overlay, driven purely through the 056-facing
    // IYnetTransport surface (no direct SKademliaNode access from the contract tier).
    private static (IYnetTransport Storer, IYnetTransport LookerUp) Pair(out NodeIdentity owner)
    {
        var fabric = new InProcessFabric(clock: () => DateTimeOffset.UnixEpoch);
        owner = NodeIdentity.Generate();
        using var other = NodeIdentity.Generate();
        var a = fabric.AttachNode(owner);
        var b = fabric.AttachNode(other);
        fabric.FormOverlay();
        return (a, b);
    }

    [Fact]
    public void A_signed_record_round_trips_store_then_lookup_across_nodes()
    {
        var (storer, lookerUp) = Pair(out var owner);
        using var _ = owner;
        var now = DateTimeOffset.UnixEpoch;

        var record = SignedRecord.CreateReachability(owner, "endpoint=1.2.3.4:40000"u8.ToArray(), now, TimeSpan.FromMinutes(10));
        var stored = storer.DhtStore(record);
        Assert.True(stored.Ok);

        var key = Encoding.ASCII.GetBytes(owner.NodeId.Value);
        var found = lookerUp.DhtLookup(key);

        Assert.True(found.Ok);
        Assert.Equal(owner.NodeId, found.Value!.SignerNodeId);
        Assert.True(found.Value!.VerifySelfCertified(now));
    }

    [Fact]
    public void A_signature_mismatched_record_is_refused_on_store_and_never_stored()
    {
        var (storer, lookerUp) = Pair(out var owner);
        using var _ = owner;
        var now = DateTimeOffset.UnixEpoch;

        var good = SignedRecord.CreateReachability(owner, "endpoint=ok"u8.ToArray(), now, TimeSpan.FromMinutes(10));
        var tampered = good with { Payload = "endpoint=EVIL"u8.ToArray() }; // signature no longer matches

        var stored = storer.DhtStore(tampered);
        Assert.False(stored.Ok);
        Assert.Equal(RefusalReason.RecordRejected, stored.Reason); // exactly one distinct reason

        // and nothing round-trips — the invalid write left no trace on any hop.
        var key = Encoding.ASCII.GetBytes(owner.NodeId.Value);
        var found = lookerUp.DhtLookup(key);
        Assert.False(found.Ok);
        Assert.Equal(RefusalReason.RecordNotFound, found.Reason);
    }

    [Fact]
    public void A_key_spoofed_record_stored_under_another_nodes_id_is_refused()
    {
        var (storer, _) = Pair(out var owner);
        using var __ = owner;
        using var victim = NodeIdentity.Generate();
        var now = DateTimeOffset.UnixEpoch;

        // Validly signed by the owner, but keyed under the VICTIM's node id — a self-cert spoof.
        var spoof = SignedRecord.Create(
            owner, Encoding.ASCII.GetBytes(victim.NodeId.Value),
            RecordKind.Reachability, "endpoint=evil"u8.ToArray(), now, TimeSpan.FromMinutes(10));

        var stored = storer.DhtStore(spoof);
        Assert.False(stored.Ok);
        Assert.Equal(RefusalReason.RecordRejected, stored.Reason);
    }

    [Fact]
    public void A_human_memorable_name_returns_further_resolver_required_and_fabricates_nothing()
    {
        var (_, lookerUp) = Pair(out var owner);
        using var __ = owner;

        // Not a self-certified key (nodeId = 64 lowercase hex): a petname / hostname the embedded
        // self-certified overlay cannot resolve without an external resolver (FR-017).
        var found = lookerUp.DhtLookup("alice.mesh"u8.ToArray());

        Assert.False(found.Ok);
        Assert.Null(found.Value);
        Assert.Equal(RefusalReason.FurtherResolverRequired, found.Reason);
    }

    [Fact]
    public void A_valid_key_with_no_record_is_not_found_distinct_from_a_naming_refusal()
    {
        var (_, lookerUp) = Pair(out var owner);
        using var __ = owner;

        // A syntactically valid self-certified key (an unrelated node id) with nothing stored under it.
        using var stranger = NodeIdentity.Generate();
        var found = lookerUp.DhtLookup(Encoding.ASCII.GetBytes(stranger.NodeId.Value));

        Assert.False(found.Ok);
        Assert.Equal(RefusalReason.RecordNotFound, found.Reason); // NOT FurtherResolverRequired
    }
}
