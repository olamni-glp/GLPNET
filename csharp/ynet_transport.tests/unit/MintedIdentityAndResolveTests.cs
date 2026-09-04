using SysPath = System.IO.Path;
using Ynet.Transport.Capability;
using Ynet.Transport.Dht;
using Ynet.Transport.HolePunch;

namespace Ynet.Transport.Tests;

/// <summary>
/// Feature 102 (ruling Q-glpnetshiras-39): a lane identity that is MINTED ONCE and survives a
/// reboot, and a <c>Resolve</c> that maps an address-independent id to an address — with
/// <c>Refused</c> a first-class answer.
/// </summary>
public sealed class MintedLaneIdentityTests : IDisposable
{
    private readonly string _dir = SysPath.Combine(
        SysPath.GetTempPath(), "ynet-keystore-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { /* best effort */ }
    }

    // A1 — the whole point: three loads, ONE id. Generate() cannot pass this test.
    [Fact]
    public void Identity_is_minted_once_and_loaded_thereafter()
    {
        using var first = NodeIdentity.LoadOrMint("shiras.glpnet", out var o1, _dir);
        using var second = NodeIdentity.LoadOrMint("shiras.glpnet", out var o2, _dir);
        using var third = NodeIdentity.LoadOrMint("shiras.glpnet", out var o3, _dir);

        Assert.Equal(IdentityOrigin.Minted, o1);
        Assert.Equal(IdentityOrigin.Loaded, o2);
        Assert.Equal(IdentityOrigin.Loaded, o3);
        Assert.Equal(first.NodeId, second.NodeId);
        Assert.Equal(second.NodeId, third.NodeId);
    }

    // Positive control: Generate() is STILL ephemeral. If this ever fails, the two APIs have been
    // conflated and the "two unrelated test nodes" use case is silently broken.
    [Fact]
    public void Generate_remains_ephemeral_a_fresh_id_every_call()
    {
        using var a = NodeIdentity.Generate();
        using var b = NodeIdentity.Generate();
        Assert.NotEqual(a.NodeId, b.NodeId);
    }

    // A2 — the id follows the KEY, not the host: a different keystore is a different lane.
    [Fact]
    public void Different_lanes_in_one_keystore_get_different_ids()
    {
        using var glpnet = NodeIdentity.LoadOrMint("shiras.glpnet", out _, _dir);
        using var yngcor = NodeIdentity.LoadOrMint("shiras.yngcor", out _, _dir);
        Assert.NotEqual(glpnet.NodeId, yngcor.NodeId);
    }

    // A5 — a signature made BEFORE the reload verifies AFTER it: the same private key came back,
    // not merely the same 32 bytes of id.
    [Fact]
    public void Signature_made_before_a_reload_verifies_after_it()
    {
        var payload = System.Text.Encoding.UTF8.GetBytes("era-102 board op");
        byte[] signature;
        byte[] spki;

        using (var before = NodeIdentity.LoadOrMint("shiras.glpnet", out _, _dir))
        {
            signature = before.Sign(payload);
            spki = before.PublicKeySpki;
        }

        using var after = NodeIdentity.LoadOrMint("shiras.glpnet", out var origin, _dir);
        Assert.Equal(IdentityOrigin.Loaded, origin);
        Assert.True(after.Verify(payload, signature));
        Assert.Equal(spki, after.PublicKeySpki);
        Assert.True(NodeIdentity.VerifySpki(after.PublicKeySpki, payload, signature));
    }

    // The Ed25519 primary survives the round trip as Ed25519 — a reload must not silently
    // re-select the algorithm (a P-256 host would change identity when a provider appeared).
    [Fact]
    public void Algorithm_in_force_survives_the_round_trip()
    {
        using var minted = NodeIdentity.LoadOrMint("shiras.glpnet", out _, _dir);
        using var loaded = NodeIdentity.LoadOrMint("shiras.glpnet", out _, _dir);
        Assert.Equal(SignatureAlgorithm.Ed25519, minted.Algorithm);
        Assert.Equal(minted.Algorithm, loaded.Algorithm);
    }

    // A P-256 fallback identity persists and reloads over the same path (DEC-CRYPTO-1).
    [Fact]
    public void P256_fallback_identity_also_persists_and_reloads()
    {
        using var minted = NodeIdentity.LoadOrMint(
            "shiras.fallback", out var o1, _dir, SignatureAlgorithm.EcdsaP256);
        using var loaded = NodeIdentity.LoadOrMint("shiras.fallback", out var o2, _dir);

        Assert.Equal(IdentityOrigin.Minted, o1);
        Assert.Equal(IdentityOrigin.Loaded, o2);
        Assert.Equal(SignatureAlgorithm.EcdsaP256, minted.Algorithm);
        Assert.Equal(SignatureAlgorithm.EcdsaP256, loaded.Algorithm);
        Assert.Equal(minted.NodeId, loaded.NodeId);
    }

    // A3 — corruption is REPORTED. A silently re-minted id would leave every peer holding a stale
    // pin with no way to know.
    [Fact]
    public void Corrupt_key_material_is_reminted_and_reported_never_silent()
    {
        using (var seed = NodeIdentity.LoadOrMint("shiras.glpnet", out _, _dir)) { Assert.NotNull(seed); }

        var keyFile = Directory.GetFiles(_dir, "*.nodekey").Single();
        File.WriteAllBytes(keyFile, new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        using var reminted = NodeIdentity.LoadOrMint("shiras.glpnet", out var origin, _dir);
        Assert.Equal(IdentityOrigin.RemintedCorrupt, origin);

        // and the replacement is durable — the next load is a plain Loaded, not another remint
        using var next = NodeIdentity.LoadOrMint("shiras.glpnet", out var o2, _dir);
        Assert.Equal(IdentityOrigin.Loaded, o2);
        Assert.Equal(reminted.NodeId, next.NodeId);
    }

    [Fact]
    public void Empty_key_file_is_treated_as_corrupt_not_as_a_valid_key()
    {
        using (var seed = NodeIdentity.LoadOrMint("shiras.glpnet", out _, _dir)) { Assert.NotNull(seed); }
        File.WriteAllBytes(Directory.GetFiles(_dir, "*.nodekey").Single(), Array.Empty<byte>());

        using var reminted = NodeIdentity.LoadOrMint("shiras.glpnet", out var origin, _dir);
        Assert.Equal(IdentityOrigin.RemintedCorrupt, origin);
        Assert.True(StaticNodeAddressResolverProbe.IsWellFormed(reminted.NodeId));
    }

    // The crash artifact a write-in-place would leave: a VALID PREFIX of a real key. It must read as
    // corrupt (and be re-minted loudly), never as a usable half-key.
    [Fact]
    public void Truncated_key_file_is_corrupt_not_a_usable_half_key()
    {
        using (var seed = NodeIdentity.LoadOrMint("shiras.glpnet", out _, _dir)) { Assert.NotNull(seed); }

        var keyFile = Directory.GetFiles(_dir, "*.nodekey").Single();
        var whole = File.ReadAllBytes(keyFile);
        Assert.True(whole.Length > 8);
        File.WriteAllBytes(keyFile, whole[..(whole.Length / 2)]);

        using var reminted = NodeIdentity.LoadOrMint("shiras.glpnet", out var origin, _dir);
        Assert.Equal(IdentityOrigin.RemintedCorrupt, origin);
        Assert.True(StaticNodeAddressResolverProbe.IsWellFormed(reminted.NodeId));
    }

    // Key material must never be left behind in a temp file the write-then-rename uses.
    [Fact]
    public void Minting_leaves_no_stray_temp_file_holding_key_material()
    {
        using var seed = NodeIdentity.LoadOrMint("shiras.glpnet", out _, _dir);
        Assert.Single(Directory.GetFiles(_dir));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    // A4 — it holds a private key.
    [Fact]
    public void Key_file_is_owner_only_on_posix()
    {
        if (OperatingSystem.IsWindows()) return; // POSIX modes do not apply

        using var seed = NodeIdentity.LoadOrMint("shiras.glpnet", out _, _dir);
        var mode = File.GetUnixFileMode(Directory.GetFiles(_dir, "*.nodekey").Single());
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    // FR-102-2 — concurrent first use mints ONE identity. Last-writer-wins would fork one lane
    // into two voters; the loser must load the winner's key.
    [Fact]
    public void Concurrent_first_use_yields_exactly_one_identity()
    {
        const int racers = 8;
        var ids = new NodeId[racers];
        var origins = new IdentityOrigin[racers];

        Parallel.For(0, racers, i =>
        {
            using var id = NodeIdentity.LoadOrMint("shiras.race", out var origin, _dir);
            ids[i] = id.NodeId;
            origins[i] = origin;
        });

        Assert.Single(ids.Distinct());
        Assert.Equal(1, origins.Count(o => o == IdentityOrigin.Minted));
        Assert.DoesNotContain(IdentityOrigin.RemintedCorrupt, origins);
    }

    // A lane name is caller-supplied and lands in a path: it must not escape the keystore dir.
    [Fact]
    public void Lane_name_cannot_traverse_out_of_the_keystore()
    {
        using var seed = NodeIdentity.LoadOrMint("../../etc/evil", out _, _dir);
        Assert.Single(Directory.GetFiles(_dir, "*.nodekey"));
        Assert.Empty(Directory.GetDirectories(_dir));
    }
}

/// <summary>Test-only reuse of the resolver's well-formedness rule.</summary>
internal static class StaticNodeAddressResolverProbe
{
    public static bool IsWellFormed(NodeId id)
        => NameResolution.IsSelfCertifiedKey(System.Text.Encoding.ASCII.GetBytes(id.Value));
}

/// <summary>
/// <c>Resolve(id) -> address | Refused(reason)</c> — the surface R-E4 refuses every ospark candidacy
/// for want of. Refusals must stay DISTINCT: a caller retries one and escalates another.
/// </summary>
public sealed class NodeAddressResolveTests
{
    private static NodeId WellFormedId() => NodeIdentity.Generate().NodeId;

    // A6 — a well-formed id nobody has bound.
    [Fact]
    public void Unknown_id_is_refused_record_not_found()
    {
        var resolver = new StaticNodeAddressResolver();
        var result = resolver.Resolve(WellFormedId());

        Assert.False(result.Ok);
        Assert.Equal(RefusalReason.RecordNotFound, result.Reason);
    }

    // A7 — a petname is not this namespace; the transport fabricates nothing (FR-017).
    [Fact]
    public void Non_key_name_is_refused_further_resolver_required()
    {
        var resolver = new StaticNodeAddressResolver();
        Assert.Equal(
            RefusalReason.FurtherResolverRequired,
            resolver.Resolve(new NodeId("shiras.glpnet")).Reason);
        Assert.Equal(
            RefusalReason.FurtherResolverRequired,
            resolver.Resolve(new NodeId("")).Reason);
    }

    [Fact]
    public void Bound_id_resolves_to_its_address()
    {
        var id = WellFormedId();
        var resolver = new StaticNodeAddressResolver();
        resolver.Bind(id, NodeAddress.Quic("192.168.0.108", 47890));

        var result = resolver.Resolve(id);
        Assert.True(result.Ok);
        Assert.Equal("ynet-quic://192.168.0.108:47890", result.Value.ToString());
    }

    // A9 — address-independence as a PROPERTY: rebinding changes the address, never the id.
    [Fact]
    public void Rebinding_changes_the_address_and_never_the_id()
    {
        var id = WellFormedId();
        var resolver = new StaticNodeAddressResolver();

        resolver.Bind(id, NodeAddress.Quic("192.168.0.108", 47890));
        resolver.Bind(id, NodeAddress.Quic("10.42.0.7", 51820)); // the host moved

        var result = resolver.Resolve(id);
        Assert.True(result.Ok);
        Assert.Equal(NodeAddress.Quic("10.42.0.7", 51820), result.Value);
        Assert.Equal(1, resolver.Count); // one node, not two
    }

    // A8 — "the lease lapsed" is NOT "never heard of it". Collapsing them would make a peer that
    // just went quiet indistinguishable from one that never existed.
    [Fact]
    public void Expired_binding_is_unreachable_distinct_from_not_found()
    {
        var now = DateTimeOffset.UnixEpoch;
        var id = WellFormedId();
        var resolver = new StaticNodeAddressResolver(() => now);
        resolver.Bind(id, NodeAddress.Quic("192.168.0.108", 47890), expiresAt: now.AddMinutes(10));

        Assert.True(resolver.Resolve(id).Ok);

        now = now.AddMinutes(11);
        var expired = resolver.Resolve(id);
        Assert.False(expired.Ok);
        Assert.Equal(RefusalReason.Unreachable, expired.Reason);
        Assert.NotEqual(RefusalReason.RecordNotFound, expired.Reason);
    }

    [Fact]
    public void Withdrawn_binding_returns_to_record_not_found()
    {
        var id = WellFormedId();
        var resolver = new StaticNodeAddressResolver();
        resolver.Bind(id, NodeAddress.Quic("192.168.0.108", 47890));

        Assert.True(resolver.Withdraw(id));
        Assert.Equal(RefusalReason.RecordNotFound, resolver.Resolve(id).Reason);
    }

    // A10 — the merge rule. "Not found in the pin table" must not be downgraded to "no resolver
    // serves this" just because a later link in the chain declined the namespace.
    [Fact]
    public void Chain_preserves_the_most_specific_refusal()
    {
        var id = WellFormedId();
        var known = new StaticNodeAddressResolver();          // well-formed id, no binding
        var declining = new NamespaceDecliningResolver();      // serves nothing

        var chain = new ChainedNodeAddressResolver(known, declining);
        Assert.Equal(RefusalReason.RecordNotFound, chain.Resolve(id).Reason);

        // order must not matter to the merge
        var reversed = new ChainedNodeAddressResolver(declining, known);
        Assert.Equal(RefusalReason.RecordNotFound, reversed.Resolve(id).Reason);
    }

    [Fact]
    public void Chain_prefers_unreachable_over_not_found_and_returns_a_hit_over_both()
    {
        var now = DateTimeOffset.UnixEpoch;
        var id = WellFormedId();

        var lapsed = new StaticNodeAddressResolver(() => now);
        lapsed.Bind(id, NodeAddress.Quic("10.0.0.1", 47890), expiresAt: now.AddMinutes(1));
        now = now.AddMinutes(2);

        var empty = new StaticNodeAddressResolver();
        Assert.Equal(
            RefusalReason.Unreachable,
            new ChainedNodeAddressResolver(empty, lapsed).Resolve(id).Reason);

        var fresh = new StaticNodeAddressResolver();
        fresh.Bind(id, NodeAddress.Quic("10.0.0.2", 47890));
        var hit = new ChainedNodeAddressResolver(empty, lapsed, fresh).Resolve(id);
        Assert.True(hit.Ok);
        Assert.Equal(NodeAddress.Quic("10.0.0.2", 47890), hit.Value);
    }

    // A tampered/rejected record is security-relevant and must not be masked by a later empty link.
    [Fact]
    public void Chain_does_not_mask_a_rejected_record_behind_a_not_found()
    {
        var id = WellFormedId();
        var rejecting = new FixedRefusalResolver(RefusalReason.RecordRejected);
        var empty = new StaticNodeAddressResolver();

        Assert.Equal(
            RefusalReason.RecordRejected,
            new ChainedNodeAddressResolver(rejecting, empty).Resolve(id).Reason);
    }

    [Fact]
    public void Empty_chain_is_rejected_at_construction()
        => Assert.Throws<ArgumentException>(() => new ChainedNodeAddressResolver());

    // A11 — a node with no resolver attached says so. It never throws, and never invents.
    [Fact]
    public void Capability_without_a_resolver_refuses_further_resolver_required()
    {
        var fabric = new InProcessFabric();
        using var self = NodeIdentity.Generate();
        using var node = new YnetTransportCapability(self, fabric);

        var result = node.Resolve(WellFormedId());
        Assert.False(result.Ok);
        Assert.Equal(RefusalReason.FurtherResolverRequired, result.Reason);
    }

    [Fact]
    public void Capability_with_a_resolver_answers_without_opening_a_channel()
    {
        var fabric = new CountingFabric();
        using var self = NodeIdentity.Generate();
        var peer = WellFormedId();

        var addresses = new StaticNodeAddressResolver();
        addresses.Bind(peer, NodeAddress.Quic("192.168.0.142", 47890));

        using var node = new YnetTransportCapability(self, fabric, addresses: addresses);

        var result = node.Resolve(peer);
        Assert.True(result.Ok);
        Assert.Equal(NodeAddress.Quic("192.168.0.142", 47890), result.Value);
        Assert.Equal(0, fabric.OpenChannelCalls); // FR-102-5: resolution performs NO wire I/O
    }

    // A12 — resolve from the peer's OWN self-certified reachability record.
    [Fact]
    public void Dht_resolve_returns_the_signers_own_advertised_address()
    {
        var now = DateTimeOffset.UnixEpoch;
        using var peer = NodeIdentity.Generate();
        var dht = SingleNodeDht(peer.NodeId, now);

        var advert = new ReachabilityAdvert(peer.NodeId, new[]
        {
            new Candidate(CandidateType.Relayed, "203.0.113.9", 3478),
            new Candidate(CandidateType.Host, "192.168.0.108", 47890), // highest priority
        });
        Assert.True(dht.Store(SignedRecord.CreateReachability(
            peer, advert.Encode(), now, TimeSpan.FromMinutes(10))).Ok);

        var resolver = new DhtNodeAddressResolver(dht, () => now);
        var result = resolver.Resolve(peer.NodeId);

        Assert.True(result.Ok);
        Assert.Equal(NodeAddress.Quic("192.168.0.108", 47890), result.Value);
    }

    [Fact]
    public void Dht_resolve_refuses_an_expired_record_rather_than_serving_a_stale_address()
    {
        var now = DateTimeOffset.UnixEpoch;
        using var peer = NodeIdentity.Generate();
        var dht = SingleNodeDht(peer.NodeId, now);

        var advert = new ReachabilityAdvert(
            peer.NodeId, new[] { new Candidate(CandidateType.Host, "192.168.0.108", 47890) });
        dht.Store(SignedRecord.CreateReachability(peer, advert.Encode(), now, TimeSpan.FromMinutes(5)));

        var later = now.AddMinutes(10);
        var result = new DhtNodeAddressResolver(dht, () => later).Resolve(peer.NodeId);

        Assert.False(result.Ok);
        Assert.NotEqual(RefusalReason.None, result.Reason);
    }

    [Fact]
    public void Dht_resolve_of_an_unpublished_id_is_refused_not_fabricated()
    {
        var now = DateTimeOffset.UnixEpoch;
        using var peer = NodeIdentity.Generate();
        var dht = SingleNodeDht(peer.NodeId, now);

        var result = new DhtNodeAddressResolver(dht, () => now).Resolve(WellFormedId());
        Assert.False(result.Ok);
    }

    private static DhtCapability SingleNodeDht(NodeId host, DateTimeOffset now)
    {
        var node = new SKademliaNode(host, "sim://" + host.Value, _ => null);
        return new DhtCapability(node, () => now);
    }

    [Theory]
    [InlineData("ynet-quic://192.168.0.108:47890", "ynet-quic", "192.168.0.108", 47890)]
    [InlineData("ynet-quic://[::1]:443", "ynet-quic", "[::1]", 443)]
    public void Address_round_trips_through_text(string text, string scheme, string host, int port)
    {
        Assert.True(NodeAddress.TryParse(text, out var parsed));
        Assert.Equal(new NodeAddress(scheme, host, port), parsed);
        Assert.Equal(text, parsed.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("192.168.0.108:47890")]     // no scheme
    [InlineData("ynet-quic://192.168.0.108")] // no port
    [InlineData("ynet-quic://host:0")]        // port out of range
    [InlineData("ynet-quic://host:70000")]
    [InlineData("ynet-quic://host:")]
    public void Malformed_address_text_is_refused_never_partially_parsed(string? text)
    {
        Assert.False(NodeAddress.TryParse(text, out var parsed));
        Assert.Equal(default, parsed);
    }

    private sealed class NamespaceDecliningResolver : INodeAddressResolver
    {
        public Result<NodeAddress> Resolve(NodeId id)
            => Result<NodeAddress>.Refuse(RefusalReason.FurtherResolverRequired);
    }

    private sealed class FixedRefusalResolver(RefusalReason reason) : INodeAddressResolver
    {
        public Result<NodeAddress> Resolve(NodeId id) => Result<NodeAddress>.Refuse(reason);
    }

    /// <summary>Counts dial attempts so a test can assert that resolution made none.</summary>
    private sealed class CountingFabric : INodeEndpointResolver
    {
        public int OpenChannelCalls { get; private set; }

        public Result<Ynet.Transport.Link.IWireChannel> OpenChannel(NodeId peer)
        {
            OpenChannelCalls++;
            return Result<Ynet.Transport.Link.IWireChannel>.Refuse(RefusalReason.Unreachable);
        }
    }
}
