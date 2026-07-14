using Ynet.Transport.Capability;
using Ynet.Transport.Dht;
using Ynet.Transport.Exit;
using Ynet.Transport.Link;
using Ynet.Transport.Path;
using Ynet.Transport.Relay;
using Ynet.Transport.Seal;

namespace Ynet.Transport.Tests;

// ---- T010 / T005: node identity + pre-frame identity gate (FR-002, SC-001) ----
public class NodeIdentityTests
{
    [Fact]
    public void NodeId_is_self_certified_hash_of_pubkey()
    {
        using var id = NodeIdentity.Generate();
        Assert.Equal(id.NodeId, NodeIdentity.DeriveNodeId(id.PublicKeySpki));
    }

    [Fact]
    public void Matching_peer_identity_is_accepted_before_any_frame()
    {
        using var peer = NodeIdentity.Generate();
        var r = YnetLink.VerifyPeerIdentity(peer.NodeId, peer.PublicKeySpki);
        Assert.True(r.Ok);
    }

    [Fact]
    public void Mismatched_peer_identity_is_refused_identity_mismatch()
    {
        using var peer = NodeIdentity.Generate();
        using var impostor = NodeIdentity.Generate();
        var r = YnetLink.VerifyPeerIdentity(peer.NodeId, impostor.PublicKeySpki);
        Assert.False(r.Ok);
        Assert.Equal(RefusalReason.IdentityMismatch, r.Reason);
    }

    [Fact]
    public void Sign_verify_round_trips_and_rejects_tamper()
    {
        using var id = NodeIdentity.Generate();
        var data = new byte[] { 1, 2, 3, 4 };
        var sig = id.Sign(data);
        Assert.True(id.Verify(data, sig));
        data[0] ^= 0xFF;
        Assert.False(id.Verify(data, sig));
    }

    [Fact]
    public void Cutover_is_monotonic_active_cannot_go_back_to_migrating()
    {
        using var id = NodeIdentity.Generate(); // starts Active
        Assert.Throws<InvalidOperationException>(() => id.CompleteMigration());
    }
}

// ---- T012 / DEC-CRYPTO-1: Ed25519-primary identity + P-256 exceptional fallback (FR-002) ----
public class NodeIdentityAlgorithmTests
{
    [Fact]
    public void Generate_defaults_to_ed25519_primary()
    {
        using var id = NodeIdentity.Generate();
        Assert.Equal(SignatureAlgorithm.Ed25519, id.Algorithm);
    }

    [Fact]
    public void Ed25519_identity_sign_verify_round_trips_and_rejects_tamper()
    {
        using var id = NodeIdentity.Generate(SignatureAlgorithm.Ed25519);
        var data = "ed25519 payload"u8.ToArray();
        var sig = id.Sign(data);
        Assert.True(id.Verify(data, sig));
        data[0] ^= 0xFF;
        Assert.False(id.Verify(data, sig));
    }

    [Fact]
    public void P256_fallback_identity_still_works_and_is_algorithm_agnostic_verifiable()
    {
        using var id = NodeIdentity.Generate(SignatureAlgorithm.EcdsaP256);
        Assert.Equal(SignatureAlgorithm.EcdsaP256, id.Algorithm);
        var data = "p256 payload"u8.ToArray();
        var sig = id.Sign(data);
        // Verified through the same agnostic path used by self-cert records.
        Assert.True(NodeIdentity.VerifySpki(id.PublicKeySpki, data, sig));
    }

    [Fact]
    public void Both_algorithms_self_cert_a_reachability_record_over_one_verify_path()
    {
        foreach (var algo in new[] { SignatureAlgorithm.Ed25519, SignatureAlgorithm.EcdsaP256 })
        {
            using var owner = NodeIdentity.Generate(algo);
            var now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
            var rec = Ynet.Transport.Dht.SignedRecord.CreateReachability(owner, "addr"u8.ToArray(), now, TimeSpan.FromHours(1));
            Assert.True(rec.VerifySelfCertified(now));
            Assert.Equal(owner.NodeId, rec.SignerNodeId);
        }
    }

    [Fact]
    public void Cross_algorithm_signature_does_not_verify_under_a_foreign_key()
    {
        using var ed = NodeIdentity.Generate(SignatureAlgorithm.Ed25519);
        using var ec = NodeIdentity.Generate(SignatureAlgorithm.EcdsaP256);
        var data = "x"u8.ToArray();
        var edSig = ed.Sign(data);
        // ed's signature must not verify against ec's P-256 SPKI.
        Assert.False(NodeIdentity.VerifySpki(ec.PublicKeySpki, data, edSig));
    }
}

// ---- T008 / T013: AES-256-GCM seal with H2 atomic nonce + H3 HKDF (FR-003) ----
public class SessionSealTests
{
    private static SessionSeal NewSeal()
        => SessionSeal.Derive(new byte[32], "ynet-test"u8, salt: 0xA1B2C3D4);

    [Fact]
    public void Seal_open_round_trips()
    {
        using var a = NewSeal();
        using var b = NewSeal();
        var pt = "hello ynet"u8.ToArray();
        var sealed_ = a.Seal(pt);
        Assert.Equal(pt, b.Open(sealed_));
    }

    [Fact]
    public void Tampered_ciphertext_fails_authentication_no_silent_downgrade()
    {
        using var a = NewSeal();
        using var b = NewSeal();
        var sealed_ = a.Seal("secret"u8);
        sealed_[^1] ^= 0xFF; // flip a tag byte
        Assert.Null(b.Open(sealed_));
    }

    [Fact]
    public void H2_nonces_are_unique_and_monotonic_across_sends()
    {
        using var a = NewSeal();
        var n1 = a.Seal("a"u8).AsSpan(0, 12).ToArray();
        var n2 = a.Seal("b"u8).AsSpan(0, 12).ToArray();
        var n3 = a.Seal("c"u8).AsSpan(0, 12).ToArray();
        Assert.NotEqual(n1, n2);
        Assert.NotEqual(n2, n3);
        Assert.NotEqual(n1, n3);
    }
}

// ---- T024: self-certified DHT record (FR-006, SC-003) ----
public class SignedRecordTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    [Fact]
    public void Signed_reachability_record_verifies_self_certified()
    {
        using var owner = NodeIdentity.Generate();
        var rec = SignedRecord.CreateReachability(owner, "addr"u8.ToArray(), Now, TimeSpan.FromHours(1));
        Assert.True(rec.VerifySelfCertified());
        Assert.Equal(owner.NodeId, rec.SignerNodeId);
    }

    [Fact]
    public void Tampered_record_is_rejected_regardless_of_serving_hop()
    {
        using var owner = NodeIdentity.Generate();
        var rec = SignedRecord.CreateReachability(owner, "addr"u8.ToArray(), Now, TimeSpan.FromHours(1));
        var tampered = rec with { Payload = "evil"u8.ToArray() };
        Assert.False(tampered.VerifySelfCertified());
    }

    // codexreview: a validly-signed reachability record under ANOTHER node's key must be rejected.
    [Fact]
    public void Reachability_record_spoofing_a_victim_key_is_rejected()
    {
        using var victim = NodeIdentity.Generate();
        using var attacker = NodeIdentity.Generate();
        var victimKey = System.Text.Encoding.ASCII.GetBytes(victim.NodeId.Value);
        // attacker signs a record stored under the victim's DHT key with the attacker's own key
        var spoof = SignedRecord.Create(attacker, victimKey, RecordKind.Reachability, "evil-addr"u8.ToArray(), Now, TimeSpan.FromHours(1));
        Assert.False(spoof.VerifySelfCertified()); // key != H(signer pubkey) -> rejected
    }

    // codexreview: an expired record must not verify when a clock is supplied.
    [Fact]
    public void Expired_record_is_rejected_when_clock_supplied()
    {
        using var owner = NodeIdentity.Generate();
        var rec = SignedRecord.CreateReachability(owner, "addr"u8.ToArray(), Now, TimeSpan.FromHours(1));
        Assert.True(rec.VerifySelfCertified(Now + TimeSpan.FromMinutes(30)));
        Assert.False(rec.VerifySelfCertified(Now + TimeSpan.FromHours(2)));
    }
}

// ---- T031 / T051: relay admission + leaf policy (FR-007/008/016) ----
public class RelayPolicyTests
{
    [Fact]
    public void Only_admitted_non_revoked_relays_are_selectable()
    {
        var r = new NodeId("r");
        Assert.True(AdmissionEnforcer.IsSelectable(r, new AdmissionProof(r, Admitted: true, "mesh", Revoked: false)));
        Assert.False(AdmissionEnforcer.IsSelectable(r, new AdmissionProof(r, Admitted: false, "mesh", Revoked: false)));
        Assert.False(AdmissionEnforcer.IsSelectable(r, new AdmissionProof(r, Admitted: true, "mesh", Revoked: true)));
    }

    // codexreview: a valid proof issued for relay X must not authorize a different relay M.
    [Fact]
    public void Proof_for_one_relay_does_not_authorize_another()
    {
        var proofForX = new AdmissionProof(new("X"), Admitted: true, "mesh", Revoked: false);
        Assert.True(AdmissionEnforcer.IsSelectable(new("X"), proofForX));
        Assert.False(AdmissionEnforcer.IsSelectable(new("M"), proofForX)); // confused-deputy blocked
    }

    [Theory]
    [InlineData("mesh", RelayMechanism.CircuitRelayV2)]
    [InlineData("internet", RelayMechanism.TorCell)]
    [InlineData("critical", RelayMechanism.TorCell)]
    public void Traffic_class_maps_to_relay_mechanism(string cls, RelayMechanism expected)
        => Assert.Equal(expected, AdmissionEnforcer.MechanismFor(cls));

    [Fact]
    public void Leaf_refuses_third_party_transit_but_allows_own_traffic()
    {
        var leaf = new LeafMode();
        leaf.SetMode(NodeMode.Leaf);
        Assert.Equal(RefusalReason.LeafTransitRefused, leaf.EvaluateTransit(isThirdPartyTransit: true));
        Assert.Null(leaf.EvaluateTransit(isThirdPartyTransit: false));
        Assert.Equal((true, false), leaf.Asymmetry());
    }

    [Fact]
    public void Full_node_forwards_third_party_transit()
    {
        var full = new LeafMode(); // Full by default
        Assert.Null(full.EvaluateTransit(isThirdPartyTransit: true));
        Assert.Equal((true, true), full.Asymmetry());
    }
}

// ---- T038 / T037: routing-mode fail-closed + mix-trust (FR-011/010a) ----
public class RoutingAndMixTrustTests
{
    [Fact]
    public void Unspecified_selection_resolves_to_safe_default()
    {
        var sel = RoutingModeResolver.Resolve(null);
        Assert.Equal(RoutingMode.Sealed, sel.Mode);
        Assert.Equal(ExitKind.Internal, sel.Exit);
    }

    [Fact]
    public void Sealed_request_with_no_sealed_path_fails_closed_never_downgrades()
    {
        var sealedSel = new RoutingSelection(RoutingMode.Sealed, 2, ExitKind.Internal);
        Assert.Equal(RefusalReason.SealUnavailable, RoutingModeResolver.Honor(sealedSel, sealedPathAvailable: false));
        Assert.Null(RoutingModeResolver.Honor(sealedSel, sealedPathAvailable: true));
    }

    [Fact]
    public void Mix_trust_prefers_pocw_stake_when_available()
    {
        var sel = new MixTrustSelector(pocwSignalAvailable: true);
        var cands = new[]
        {
            new RelayCandidate(new("low"), PocwStake: 1, LoopixSemiTrusted: true),
            new RelayCandidate(new("high"), PocwStake: 9, LoopixSemiTrusted: false),
        };
        var hops = sel.Select(cands, hopCount: 1);
        Assert.Equal(new NodeId("high"), hops![0]);
        Assert.Equal(MixTrustSource.Pocw057, sel.ActiveSource);
    }

    [Fact]
    public void Mix_trust_falls_back_to_loopix_when_pocw_absent()
    {
        var sel = new MixTrustSelector(pocwSignalAvailable: false);
        var cands = new[]
        {
            new RelayCandidate(new("a"), PocwStake: 9, LoopixSemiTrusted: false), // high stake but pocw off
            new RelayCandidate(new("b"), PocwStake: 0, LoopixSemiTrusted: true),
        };
        var hops = sel.Select(cands, hopCount: 1);
        Assert.Equal(new NodeId("b"), hops![0]);
        Assert.Equal(MixTrustSource.LoopixFallback, sel.ActiveSource);
    }

    [Fact]
    public void Mix_trust_fabricates_nothing_when_no_provider_available()
    {
        var sel = new MixTrustSelector(pocwSignalAvailable: false);
        var cands = new[] { new RelayCandidate(new("a"), PocwStake: 0, LoopixSemiTrusted: false) };
        Assert.Null(sel.Select(cands, hopCount: 1));
    }

    // codexreview: an undersized hop set must fail closed, never silently weaken anonymity.
    [Fact]
    public void Mix_trust_fails_closed_when_fewer_hops_than_requested()
    {
        var sel = new MixTrustSelector(pocwSignalAvailable: false);
        var cands = new[] { new RelayCandidate(new("a"), PocwStake: 0, LoopixSemiTrusted: true) };
        Assert.Null(sel.Select(cands, hopCount: 3)); // only 1 eligible, 3 requested -> null
    }
}

// ---- T043: exit-abuse policy (FR-012/013) ----
public class ExitAbusePolicyTests
{
    private static ExitAbusePolicy Policy() => new(
        allowList: new[] { "good.example" },
        denyList: new[] { "bad.example" },
        blockedClasses: new[] { EgressClass.Smtp },
        perCallerVolumeCap: 1000);

    [Fact]
    public void Denied_destination_is_refused() =>
        Assert.Equal(RefusalReason.EgressDenied,
            Policy().Evaluate(new EgressRequest(new("c"), "bad.example", EgressClass.Http, 1)));

    [Fact]
    public void Default_deny_when_not_on_allow_list() =>
        Assert.Equal(RefusalReason.EgressDenied,
            Policy().Evaluate(new EgressRequest(new("c"), "unknown.example", EgressClass.Http, 1)));

    [Fact]
    public void Allowed_destination_passes() =>
        Assert.Null(Policy().Evaluate(new EgressRequest(new("c"), "good.example", EgressClass.Http, 1)));

    [Fact]
    public void Blocked_class_is_refused() =>
        Assert.Equal(RefusalReason.EgressDenied,
            Policy().Evaluate(new EgressRequest(new("c"), "good.example", EgressClass.Smtp, 1)));

    // codexreview: an empty allow list must be default-deny, never allow-all.
    [Fact]
    public void Empty_allow_list_denies_everything()
    {
        var deny = new ExitAbusePolicy(allowList: Array.Empty<string>(), denyList: Array.Empty<string>(),
            blockedClasses: Array.Empty<EgressClass>(), perCallerVolumeCap: 0);
        Assert.Equal(RefusalReason.EgressDenied,
            deny.Evaluate(new EgressRequest(new("c"), "anything.example", EgressClass.Http, 1)));
    }

    [Fact]
    public void Volume_cap_is_enforced_per_caller()
    {
        var p = Policy();
        Assert.Null(p.Evaluate(new EgressRequest(new("c"), "good.example", EgressClass.Http, 900)));
        Assert.Equal(RefusalReason.EgressDenied,
            p.Evaluate(new EgressRequest(new("c"), "good.example", EgressClass.Http, 200))); // 1100 > 1000
    }
}

// ---- T032 / data-model: path state machine (FR-018, research R3) ----
public class PathStateTests
{
    [Fact]
    public void Establishing_to_live_then_teardown_to_unreachable()
    {
        var p = new PathState();
        p.Established(PathType.Direct);
        Assert.Equal(PathPhase.Live, p.Phase);
        p.BeginTearDown();
        p.TearDownComplete();
        Assert.Equal(PathPhase.Unreachable, p.Phase);
    }

    [Fact]
    public void No_path_marks_unreachable_from_establishing()
    {
        var p = new PathState();
        p.MarkUnreachableFromEstablishing();
        Assert.Equal(PathPhase.Unreachable, p.Phase);
    }

    [Fact]
    public void Illegal_transition_throws()
    {
        var p = new PathState();
        Assert.Throws<InvalidOperationException>(() => p.BeginTearDown()); // not Live yet
    }
}
