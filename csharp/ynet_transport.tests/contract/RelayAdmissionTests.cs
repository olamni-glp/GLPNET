using System.Security.Cryptography;
using Ynet.Transport.Capability;
using Ynet.Transport.Link;
using Ynet.Transport.Relay;

namespace Ynet.Transport.Tests.Contract;

// ---- T028 / US4: relay admission + ciphertext-only forwarding contract (FR-007/FR-008/FR-016,
//      SC-004) ----
//
// offer_relay(relay_node_id, AdmissionProof) -> Admitted | Rejected(reason)
// (contracts/transport-capability.md). Invariant 4: admits ONLY on a valid 056 AdmissionProof, and
// revocation removes it. Invariant 5: a leaf refuses third-party transit. Invariant 1: every refusal
// carries exactly one distinct reason. This tier ENFORCES the 056 decision and never decides it
// (FR-024) — every gate below consumes a proof it was handed.
public class RelayAdmissionTests
{
    private static AdmissionProof Admit(NodeId relay, string trafficClass = "mesh")
        => new(relay, Admitted: true, trafficClass, Revoked: false);

    private static (InProcessFabric Fabric, YnetTransportCapability Node) Fabric(out NodeIdentity id)
    {
        var fabric = new InProcessFabric(clock: () => DateTimeOffset.UnixEpoch);
        id = NodeIdentity.Generate();
        return (fabric, fabric.AttachNode(id));
    }

    [Fact]
    public void An_admitted_relay_is_accepted_by_offer_relay()
    {
        var (fabric, a) = Fabric(out var aId);
        using var _ = aId;
        using var relayId = NodeIdentity.Generate();
        fabric.AttachNode(relayId);

        var offered = a.OfferRelay(relayId.NodeId, Admit(relayId.NodeId));

        Assert.True(offered.Ok);
        Assert.Equal(RefusalReason.None, offered.Reason);
    }

    [Fact]
    public void A_relay_056_has_not_admitted_is_rejected_and_never_selectable()
    {
        var (fabric, a) = Fabric(out var aId);
        using var _ = aId;
        using var relayId = NodeIdentity.Generate();
        using var targetId = NodeIdentity.Generate();
        fabric.AttachNode(relayId);
        fabric.AttachNode(targetId);

        // 056 decided NOT to admit: the proof says so, and this tier enforces that verdict.
        var notAdmitted = new AdmissionProof(relayId.NodeId, Admitted: false, "mesh", Revoked: false);

        var offered = a.OfferRelay(relayId.NodeId, notAdmitted);
        Assert.False(offered.Ok);
        Assert.Equal(RefusalReason.RelayNotAdmitted, offered.Reason); // exactly one distinct reason

        // ...and it is never selected for a path (invariant 2: refused before any wire side-effect).
        var connected = a.ConnectViaRelay(relayId.NodeId, targetId.NodeId, RoutingSelection.SafeDefault);
        Assert.False(connected.Ok);
        Assert.Equal(RefusalReason.RelayNotAdmitted, connected.Reason);
    }

    [Fact]
    public void A_revoked_relay_is_never_selected_even_after_it_was_admitted()
    {
        var (fabric, a) = Fabric(out var aId);
        using var _ = aId;
        using var relayId = NodeIdentity.Generate();
        using var targetId = NodeIdentity.Generate();
        fabric.AttachNode(relayId);
        fabric.AttachNode(targetId);

        Assert.True(a.OfferRelay(relayId.NodeId, Admit(relayId.NodeId)).Ok); // admitted first

        // 056 revokes: the removal path (invariant 4).
        var revoked = new AdmissionProof(relayId.NodeId, Admitted: true, "mesh", Revoked: true);
        var offered = a.OfferRelay(relayId.NodeId, revoked);

        Assert.False(offered.Ok);
        Assert.Equal(RefusalReason.RelayNotAdmitted, offered.Reason);

        var connected = a.ConnectViaRelay(relayId.NodeId, targetId.NodeId, RoutingSelection.SafeDefault);
        Assert.False(connected.Ok);
        Assert.Equal(RefusalReason.RelayNotAdmitted, connected.Reason);
    }

    [Fact]
    public void A_proof_issued_for_another_relay_never_authorizes_this_one()
    {
        var (fabric, a) = Fabric(out var aId);
        using var _ = aId;
        using var admittedRelay = NodeIdentity.Generate();
        using var maliciousRelay = NodeIdentity.Generate();
        fabric.AttachNode(admittedRelay);
        fabric.AttachNode(maliciousRelay);

        // A valid proof for admitted relay X, replayed to authorize a different relay M
        // (confused-deputy): the proof→target binding defeats it (FR-007/FR-008, SC-004).
        var proofForX = Admit(admittedRelay.NodeId);

        var offered = a.OfferRelay(maliciousRelay.NodeId, proofForX);

        Assert.False(offered.Ok);
        Assert.Equal(RefusalReason.RelayNotAdmitted, offered.Reason);
    }

    [Fact]
    public void A_relay_reserves_a_circuit_only_against_a_proof_admitting_itself()
    {
        using var relayId = NodeIdentity.Generate();
        using var dialerId = NodeIdentity.Generate();
        using var otherRelayId = NodeIdentity.Generate();
        var relay = new CircuitRelayV2(relayId.NodeId, clock: () => DateTimeOffset.UnixEpoch);

        Assert.True(relay.Reserve(dialerId.NodeId, Admit(relayId.NodeId)).Ok);

        // A proof admitting a DIFFERENT relay does not reserve a circuit here...
        var forOther = relay.Reserve(dialerId.NodeId, Admit(otherRelayId.NodeId));
        Assert.False(forOther.Ok);
        Assert.Equal(RefusalReason.RelayNotAdmitted, forOther.Reason);

        // ...nor does a revoked one.
        var revoked = relay.Reserve(dialerId.NodeId, Admit(relayId.NodeId) with { Revoked = true });
        Assert.False(revoked.Ok);
        Assert.Equal(RefusalReason.RelayNotAdmitted, revoked.Reason);
    }

    [Fact]
    public void A_voucher_minted_by_one_relay_never_gates_a_circuit_at_another()
    {
        using var relayAId = NodeIdentity.Generate();
        using var relayBId = NodeIdentity.Generate();
        using var dialerId = NodeIdentity.Generate();
        var clock = () => DateTimeOffset.UnixEpoch;
        var relayA = new CircuitRelayV2(relayAId.NodeId, clock);
        var relayB = new CircuitRelayV2(relayBId.NodeId, clock);

        var voucherFromA = relayA.Reserve(dialerId.NodeId, Admit(relayAId.NodeId)).Value;

        // Transplanted onto relay B: refused (bound to A, and MAC'd under A's secret).
        Assert.Equal(RefusalReason.RelayNotAdmitted, relayB.VerifyVoucher(voucherFromA));
        Assert.Null(relayA.VerifyVoucher(voucherFromA)); // still gates at its own relay
    }

    [Fact]
    public void A_tampered_voucher_is_refused_and_forwards_nothing()
    {
        using var relayId = NodeIdentity.Generate();
        using var dialerId = NodeIdentity.Generate();
        var relay = new CircuitRelayV2(relayId.NodeId, clock: () => DateTimeOffset.UnixEpoch);
        var voucher = relay.Reserve(dialerId.NodeId, Admit(relayId.NodeId)).Value;

        // Extend the reservation past its expiry — the MAC no longer authenticates the tuple.
        var forged = voucher with { ExpiresAt = voucher.ExpiresAt + TimeSpan.FromDays(365) };
        Assert.Equal(RefusalReason.RelayNotAdmitted, relay.VerifyVoucher(forged));

        var (downstream, farEnd) = InProcessDuplexChannel.CreatePair();
        var forwarded = relay.Forward(forged, downstream, "payload"u8.ToArray());

        Assert.False(forwarded.Ok);
        Assert.Equal(RefusalReason.RelayNotAdmitted, forwarded.Reason);

        // Zero side-effects on the refusal path (invariant 2): nothing reached the next hop.
        downstream.Close();
        Assert.Null(farEnd.ReadFrame());
    }

    [Fact]
    public void An_expired_reservation_stops_gating_the_circuit()
    {
        using var relayId = NodeIdentity.Generate();
        using var dialerId = NodeIdentity.Generate();
        var now = DateTimeOffset.UnixEpoch;
        var relay = new CircuitRelayV2(relayId.NodeId, clock: () => now);

        var voucher = relay.Reserve(dialerId.NodeId, Admit(relayId.NodeId)).Value;
        Assert.Null(relay.VerifyVoucher(voucher)); // live now

        now = voucher.ExpiresAt; // reservation elapsed
        Assert.Equal(RefusalReason.RelayNotAdmitted, relay.VerifyVoucher(voucher));
    }

    [Fact]
    public void A_sealed_payload_is_undecryptable_at_the_relay()
    {
        // The two ENDPOINTS' seal. The relay is not a party to it and holds no key material —
        // exactly the SC-004 property: a relay forwards what it cannot read.
        var endpointSecret = RandomNumberGenerator.GetBytes(32);
        using var senderSeal = SessionSeal.Derive(endpointSecret, "ynet-link"u8, salt: 7, SessionSeal.Direction.Initiator);
        using var peerOpener = SessionSeal.Derive(endpointSecret, "ynet-link"u8, salt: 7, SessionSeal.Direction.Initiator);

        var plaintext = "the payload a relay must never read"u8.ToArray();
        var sealedFrame = senderSeal.Seal(plaintext);

        using var relayId = NodeIdentity.Generate();
        using var dialerId = NodeIdentity.Generate();
        var relay = new CircuitRelayV2(relayId.NodeId, clock: () => DateTimeOffset.UnixEpoch);
        var voucher = relay.Reserve(dialerId.NodeId, Admit(relayId.NodeId)).Value;

        var (downstream, farEnd) = InProcessDuplexChannel.CreatePair();
        Assert.True(relay.Forward(voucher, downstream, sealedFrame).Ok);

        var onTheWire = farEnd.ReadFrame()!;

        Assert.Equal(sealedFrame, onTheWire);                          // forwarded verbatim
        Assert.False(Contains(onTheWire, plaintext));                  // ...and it is ciphertext
        Assert.NotEqual(plaintext, onTheWire);

        // Any key the relay could hold opens nothing — it never saw the endpoints' shared secret.
        using var relayHeldKey = SessionSeal.Derive(
            RandomNumberGenerator.GetBytes(32), "ynet-link"u8, salt: 7, SessionSeal.Direction.Initiator);
        Assert.Null(relayHeldKey.Open(onTheWire));

        // Only the intended endpoint opens it.
        Assert.Equal(plaintext, peerOpener.Open(onTheWire));
    }

    [Fact]
    public void A_leaf_refuses_third_party_transit_but_still_uses_relays_for_its_own_egress()
    {
        using var leafId = NodeIdentity.Generate();
        using var dialerId = NodeIdentity.Generate();
        var leafRelay = new RelayCapability(leafId.NodeId, clock: () => DateTimeOffset.UnixEpoch);

        // As a FULL node it would carry third-party transit for an admitted proof...
        Assert.True(leafRelay.AcceptTransit(dialerId.NodeId, Admit(leafId.NodeId)).Ok);

        leafRelay.Leaf.SetMode(NodeMode.Leaf);

        // ...but a leaf never forwards for others (FR-016, invariant 5).
        var transit = leafRelay.AcceptTransit(dialerId.NodeId, Admit(leafId.NodeId));
        Assert.False(transit.Ok);
        Assert.Equal(RefusalReason.LeafTransitRefused, transit.Reason);

        // The asymmetry is explicit: a leaf still uses relays for its OWN egress (US8 AS3).
        var (usesRelaysForSelf, relaysForOthers) = leafRelay.Leaf.Asymmetry();
        Assert.True(usesRelaysForSelf);
        Assert.False(relaysForOthers);
    }

    [Fact]
    public void The_traffic_class_binds_the_mechanism_a_relay_forwards_with()
    {
        using var relayId = NodeIdentity.Generate();
        using var dialerId = NodeIdentity.Generate();
        var relay = new RelayCapability(relayId.NodeId, clock: () => DateTimeOffset.UnixEpoch);

        // mesh -> circuit-relay-v2 (voucher-gated); internet/critical -> Tor-style cells (clarify §5.2).
        var mesh = relay.AcceptTransit(dialerId.NodeId, Admit(relayId.NodeId, "mesh"));
        Assert.True(mesh.Ok);
        Assert.Equal(RelayMechanism.CircuitRelayV2, mesh.Value.Mechanism);
        Assert.NotNull(mesh.Value.Voucher);

        foreach (var trafficClass in new[] { "internet", "critical" })
        {
            var cells = relay.AcceptTransit(dialerId.NodeId, Admit(relayId.NodeId, trafficClass));
            Assert.True(cells.Ok);
            Assert.Equal(RelayMechanism.TorCell, cells.Value.Mechanism);
        }
    }

    private static bool Contains(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
        => haystack.IndexOf(needle) >= 0;
}
