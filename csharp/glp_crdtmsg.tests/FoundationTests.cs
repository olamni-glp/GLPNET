// Foundational-layer tests (feature 041-crdtmsg-mvp) — validate T007 (model) + T009 (Dot/DVV/hash-chain)
// + T008 (in-memory link fabric). These are the invariants the store/crdt/route layers build on.

using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.CrdtMsg.Route;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class FoundationTests
{
    // --- T009: Dot ---

    [Fact]
    public void Dot_canonical_form_and_order()
    {
        Assert.Equal("alice:7", new Dot("alice", 7).ToString());
        // ordinal peer, then counter
        Assert.True(new Dot("alice", 9).CompareTo(new Dot("bob", 1)) < 0);
        Assert.True(new Dot("bob", 1).CompareTo(new Dot("bob", 2)) < 0);
        Assert.Equal(0, new Dot("bob", 2).CompareTo(new Dot("bob", 2)));
    }

    [Fact]
    public void Dot_canonical_bytes_are_stable_and_distinct()
    {
        Assert.Equal(new Dot("a", 1).CanonicalBytes(), new Dot("a", 1).CanonicalBytes());
        Assert.NotEqual(new Dot("a", 1).CanonicalBytes(), new Dot("a", 2).CanonicalBytes());
        Assert.NotEqual(new Dot("a", 1).CanonicalBytes(), new Dot("b", 1).CanonicalBytes());
    }

    // --- T009: VersionVector (idempotence / dominance / join) ---

    [Fact]
    public void VersionVector_dominance_is_idempotent()
    {
        var vv = new VersionVector().With(new Dot("a", 3));
        Assert.True(vv.Contains(new Dot("a", 1)));
        Assert.True(vv.Contains(new Dot("a", 3)));
        Assert.False(vv.Contains(new Dot("a", 4)));
        // applying a dominated dot again does not regress the max
        Assert.Equal(3, vv.With(new Dot("a", 2))["a"]);
    }

    [Fact]
    public void VersionVector_join_is_pointwise_max()
    {
        var a = new VersionVector().With(new Dot("x", 5)).With(new Dot("y", 2));
        var b = new VersionVector().With(new Dot("y", 9)).With(new Dot("z", 1));
        var j = a.Join(b);
        Assert.Equal(5, j["x"]);
        Assert.Equal(9, j["y"]);
        Assert.Equal(1, j["z"]);
    }

    // --- T009: HashChain (deterministic, order-independent over deps, dep-sensitive) ---

    [Fact]
    public void HashChain_is_deterministic_and_dep_order_independent()
    {
        var self = new Dot("a", 4);
        var deps1 = new[] { new Dot("a", 3), new Dot("b", 1) };
        var deps2 = new[] { new Dot("b", 1), new Dot("a", 3) }; // reordered
        Assert.Equal(HashChain.PredHash(self, deps1), HashChain.PredHash(self, deps2));
        Assert.Equal(32, HashChain.PredHash(self, deps1).Length);
    }

    [Fact]
    public void HashChain_changes_with_deps_or_self()
    {
        var self = new Dot("a", 4);
        var h0 = HashChain.PredHash(self, new[] { new Dot("a", 3) });
        var hMoreDeps = HashChain.PredHash(self, new[] { new Dot("a", 3), new Dot("c", 1) });
        var hOtherSelf = HashChain.PredHash(new Dot("a", 5), new[] { new Dot("a", 3) });
        Assert.NotEqual(h0, hMoreDeps);
        Assert.NotEqual(h0, hOtherSelf);
    }

    // --- T007: abstract model shape ---

    [Fact]
    public void Message_model_constructs_with_registry_payload_type()
    {
        var header = new Header("m-1", "alice", "bob", 0, RoutingPolicy.Empty);
        var msg = new Message(
            SchemaVersion: 1,
            PayloadType: GlpRuntime.WireRegistry.PayloadType.CrdtMessage,
            Header: header,
            Sections: new[] { new Section(0x12, new byte[] { 1, 2, 3 }) },
            CrdtModel: CrdtModel.OpBased);

        Assert.Equal(0x12, msg.PayloadType);
        Assert.Equal(CrdtModel.OpBased, msg.CrdtModel);
        Assert.Null(msg.Header.CapabilitySlot); // v2 additive slot absent by default
        Assert.Equal(3, msg.Sections[0].Value.Length);
    }

    // --- T008: in-memory link fabric (verbatim byte transfer, membership) ---

    [Fact]
    public async Task InMemoryFabric_delivers_bytes_verbatim_and_reports_membership()
    {
        var fabric = new InMemoryLinkFabric();
        var a = fabric.Connect("A");
        var b = fabric.Connect("B");

        Assert.Contains("A", a.Members);
        Assert.Contains("B", a.Members);

        byte[] payload = { 9, 8, 7, 6 };
        await a.SendAsync("B", payload);
        var inbound = await b.Inbound.ReadAsync();

        Assert.Equal("A", inbound.FromPeer);
        Assert.Equal(payload, inbound.Bytes);
    }

    [Fact]
    public async Task InMemoryFabric_unknown_destination_is_not_a_silent_drop()
    {
        var fabric = new InMemoryLinkFabric();
        var a = fabric.Connect("A");
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await a.SendAsync("ghost", new byte[] { 1 }));
    }
}
