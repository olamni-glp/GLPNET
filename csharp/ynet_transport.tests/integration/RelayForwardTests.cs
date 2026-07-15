using System.Collections.Concurrent;
using System.Security.Cryptography;
using Ynet.Transport.Capability;
using Ynet.Transport.Link;
using Ynet.Transport.Path;
using Ynet.Transport.Relay;

namespace Ynet.Transport.Tests.Integration;

// ---- T033 / US4 (SC-004): the relay-forward slice end-to-end over the real InProcessFabric — a
//      co-hosted relay node really carries traffic between two other nodes through the production
//      mechanisms (circuit-relay-v2 voucher gate for mesh; Tor-style fixed-size cells for
//      internet/critical). The properties under test: a relayed path carries traffic end-to-end,
//      revocation mid-path tears the live circuit down to a distinct authorized_but_unreachable, and
//      the relay only ever moves ciphertext it cannot read. ----
public class RelayForwardTests
{
    private static AdmissionProof Admit(NodeId relay, string trafficClass = "mesh")
        => new(relay, Admitted: true, trafficClass, Revoked: false);

    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(5);

    [Fact]
    public void A_relayed_path_carries_traffic_end_to_end_through_an_admitted_relay()
    {
        var fabric = new InProcessFabric(clock: () => DateTimeOffset.UnixEpoch);
        using var aId = NodeIdentity.Generate();
        using var bId = NodeIdentity.Generate();
        using var relayId = NodeIdentity.Generate();
        var a = fabric.AttachNode(aId);
        var b = fabric.AttachNode(bId);
        fabric.AttachNode(relayId);

        Assert.True(a.OfferRelay(relayId.NodeId, Admit(relayId.NodeId)).Ok);

        var link = a.ConnectViaRelay(relayId.NodeId, bId.NodeId, RoutingSelection.SafeDefault);
        Assert.True(link.Ok);
        Assert.Equal(PathType.Relayed, link.Value.PathType);
        Assert.Equal(bId.NodeId, link.Value.Peer); // the relay did not become the peer

        // FR-023 auditability: the path reports its forwarding hop honestly.
        var info = a.PathInfo(link.Value);
        Assert.Equal(PathType.Relayed, info.PathType);
        Assert.Equal(1, info.RelayHops);

        var payload = "hello, carried over a relay"u8.ToArray();
        Assert.True(a.Send(link.Value, payload).Ok);

        Assert.True(b.TryAcceptLink(Settle, out var bLink));
        var received = b.Receive(bLink);
        Assert.True(received.Ok);
        Assert.Equal(payload, received.Value!.ToArray());
    }

    [Fact]
    public void An_internet_class_circuit_rides_tor_cells_end_to_end()
    {
        var fabric = new InProcessFabric(clock: () => DateTimeOffset.UnixEpoch);
        using var aId = NodeIdentity.Generate();
        using var bId = NodeIdentity.Generate();
        using var relayId = NodeIdentity.Generate();
        var a = fabric.AttachNode(aId);
        var b = fabric.AttachNode(bId);
        fabric.AttachNode(relayId);

        // internet/critical default to the Tor-style cell relay (clarify §5.2).
        Assert.True(a.OfferRelay(relayId.NodeId, Admit(relayId.NodeId, "internet")).Ok);
        Assert.Equal(RelayMechanism.TorCell, a.RelaySlice!.MechanismFor(relayId.NodeId));

        var link = a.ConnectViaRelay(relayId.NodeId, bId.NodeId, RoutingSelection.SafeDefault);
        Assert.True(link.Ok);

        // A frame larger than one cell fragments across cells and reassembles at the far endpoint.
        var payload = RandomNumberGenerator.GetBytes(TorCellRelay.MaxPayload * 2 + 41);
        Assert.True(a.Send(link.Value, payload).Ok);

        Assert.True(b.TryAcceptLink(Settle, out var bLink));
        var received = b.Receive(bLink);
        Assert.True(received.Ok);
        Assert.Equal(payload, received.Value!.ToArray());
    }

    [Fact]
    public void Revocation_mid_path_tears_the_live_circuit_down_to_authorized_but_unreachable()
    {
        var fabric = new InProcessFabric(clock: () => DateTimeOffset.UnixEpoch);
        using var aId = NodeIdentity.Generate();
        using var bId = NodeIdentity.Generate();
        using var relayId = NodeIdentity.Generate();
        var a = fabric.AttachNode(aId);
        var b = fabric.AttachNode(bId);
        fabric.AttachNode(relayId);

        Assert.True(a.OfferRelay(relayId.NodeId, Admit(relayId.NodeId)).Ok);
        var link = a.ConnectViaRelay(relayId.NodeId, bId.NodeId, RoutingSelection.SafeDefault);
        Assert.True(link.Ok);

        Assert.True(a.Send(link.Value, "before revocation"u8.ToArray()).Ok);
        Assert.True(b.TryAcceptLink(Settle, out var bLink));
        Assert.True(b.Receive(bLink).Ok);

        var live = Assert.Single(a.RelaySlice!.PathsThrough(relayId.NodeId));
        Assert.Equal(PathPhase.Live, live.State.Phase);

        // 056 revokes the relay mid-path (research R3): new selection is blocked immediately and the
        // live path drains to unreachable.
        var revoked = a.OfferRelay(relayId.NodeId, Admit(relayId.NodeId) with { Revoked = true });
        Assert.False(revoked.Ok);
        Assert.Equal(RefusalReason.RelayNotAdmitted, revoked.Reason);

        Assert.Equal(PathPhase.Unreachable, live.State.Phase);
        Assert.False(a.RelaySlice!.IsAdmitted(relayId.NodeId));

        // The sender observes a distinct refusal — never a silent drop (FR-018).
        var afterRevocation = a.Send(link.Value, "after revocation"u8.ToArray());
        Assert.False(afterRevocation.Ok);
        Assert.Equal(RefusalReason.AuthorizedButUnreachable, afterRevocation.Reason);

        // ...and the relay is not selectable for a fresh path either.
        var reconnect = a.ConnectViaRelay(relayId.NodeId, bId.NodeId, RoutingSelection.SafeDefault);
        Assert.False(reconnect.Ok);
        Assert.Equal(RefusalReason.RelayNotAdmitted, reconnect.Reason);
    }

    [Fact]
    public void A_revoked_circuit_tears_down_at_the_far_end_too()
    {
        var fabric = new InProcessFabric(clock: () => DateTimeOffset.UnixEpoch);
        using var aId = NodeIdentity.Generate();
        using var bId = NodeIdentity.Generate();
        using var relayId = NodeIdentity.Generate();
        var a = fabric.AttachNode(aId);
        var b = fabric.AttachNode(bId);
        fabric.AttachNode(relayId);

        Assert.True(a.OfferRelay(relayId.NodeId, Admit(relayId.NodeId)).Ok);
        var link = a.ConnectViaRelay(relayId.NodeId, bId.NodeId, RoutingSelection.SafeDefault);
        Assert.True(link.Ok);
        Assert.True(a.Send(link.Value, "first"u8.ToArray()).Ok);
        Assert.True(b.TryAcceptLink(Settle, out var bLink));
        Assert.True(b.Receive(bLink).Ok);

        a.OfferRelay(relayId.NodeId, Admit(relayId.NodeId) with { Revoked = true });

        // The teardown cascades through the relay: B's next receive is a distinct refusal, so the
        // far end re-paths rather than hanging on a dead circuit (R3).
        var received = b.Receive(bLink);
        Assert.False(received.Ok);
        Assert.Equal(RefusalReason.AuthorizedButUnreachable, received.Reason);
    }

    [Fact]
    public async Task A_relay_forwards_only_ciphertext_it_cannot_read()
    {
        using var aId = NodeIdentity.Generate();
        using var bId = NodeIdentity.Generate();
        using var relayId = NodeIdentity.Generate();

        // The production relay slice + pump, with the relay's egress wire tapped so the test can see
        // exactly what crossed it.
        var relaySlice = new RelayCapability(relayId.NodeId, clock: () => DateTimeOffset.UnixEpoch);
        var grant = relaySlice.AcceptTransit(aId.NodeId, Admit(relayId.NodeId));
        Assert.True(grant.Ok);

        var (dialerEnd, relayUpstream) = InProcessDuplexChannel.CreatePair();
        var (relayDownstream, targetEnd) = InProcessDuplexChannel.CreatePair();
        var tap = new TappedChannel(relayDownstream);

        relaySlice.StartTransit(Guid.NewGuid(), relayUpstream, tap, grant.Value);

        // The handshake — and therefore the seal — is end-to-end between A and B THROUGH the relay.
        var accepting = Task.Run(() => YnetSession.Accept(targetEnd, bId, RoutingSelection.SafeDefault));
        var aSession = YnetSession.Connect(dialerEnd, aId, bId.NodeId, RoutingSelection.SafeDefault, PathType.Relayed);
        Assert.True(aSession.Ok);
        var bSession = await accepting;
        Assert.True(bSession.Ok);

        using var aLive = aSession.Value!;
        using var bLive = bSession.Value!;

        var secret = "MEET-AT-DAWN-BY-THE-OLD-BRIDGE"u8.ToArray();
        Assert.True(aLive.Send(secret).Ok);

        var received = bLive.Receive();
        Assert.True(received.Ok);
        Assert.Equal(secret, received.Value!.ToArray()); // the endpoints do read it...

        // ...but nothing the relay forwarded contains the plaintext: it moved ciphertext only, and it
        // holds no session key for the circuit it carried (SC-004).
        var forwarded = tap.Forwarded;
        Assert.NotEmpty(forwarded);
        Assert.All(forwarded, frame => Assert.False(Contains(frame, secret)));
    }

    [Fact]
    public void A_leaf_relay_refuses_to_forward_third_party_transit()
    {
        var fabric = new InProcessFabric(clock: () => DateTimeOffset.UnixEpoch);
        using var aId = NodeIdentity.Generate();
        using var bId = NodeIdentity.Generate();
        using var relayId = NodeIdentity.Generate();
        var a = fabric.AttachNode(aId);
        fabric.AttachNode(bId);
        var relay = fabric.AttachNode(relayId);

        // 056's leaf policy binds at the transit hook (FR-016, invariant 5).
        relay.SetMode(NodeMode.Leaf);

        Assert.True(a.OfferRelay(relayId.NodeId, Admit(relayId.NodeId)).Ok); // admission is unaffected...

        // ...but the leaf will not carry traffic for others, whatever 056 admitted it for.
        var link = a.ConnectViaRelay(relayId.NodeId, bId.NodeId, RoutingSelection.SafeDefault);
        Assert.False(link.Ok);
        Assert.Equal(RefusalReason.LeafTransitRefused, link.Reason);
    }

    [Fact]
    public void A_leaf_still_uses_an_admitted_relay_for_its_own_egress()
    {
        var fabric = new InProcessFabric(clock: () => DateTimeOffset.UnixEpoch);
        using var aId = NodeIdentity.Generate();
        using var bId = NodeIdentity.Generate();
        using var relayId = NodeIdentity.Generate();
        var a = fabric.AttachNode(aId);
        var b = fabric.AttachNode(bId);
        fabric.AttachNode(relayId);

        a.SetMode(NodeMode.Leaf); // the DIALER is a leaf; the relay is a full node

        Assert.True(a.OfferRelay(relayId.NodeId, Admit(relayId.NodeId)).Ok);
        var link = a.ConnectViaRelay(relayId.NodeId, bId.NodeId, RoutingSelection.SafeDefault);

        Assert.True(link.Ok); // own-originated traffic is unaffected by leaf mode (US8 AS3)
        Assert.True(a.Send(link.Value, "leaf egress"u8.ToArray()).Ok);
        Assert.True(b.TryAcceptLink(Settle, out var bLink));
        Assert.True(b.Receive(bLink).Ok);
    }

    [Fact]
    public void Tor_cells_are_fixed_size_and_reassemble_a_fragmented_frame()
    {
        var circuitId = Guid.NewGuid();
        var frame = RandomNumberGenerator.GetBytes(TorCellRelay.MaxPayload * 3 + 17);

        var cells = TorCellRelay.Encode(circuitId, frame);

        // Every cell is exactly one width: the payload's LENGTH is padded away, not just its content.
        Assert.True(cells.Count >= 4);
        Assert.All(cells, cell => Assert.Equal(TorCellRelay.CellSize, cell.Length));

        // A one-byte frame is indistinguishable on the wire from a full one.
        Assert.All(TorCellRelay.Encode(circuitId, "x"u8), cell => Assert.Equal(TorCellRelay.CellSize, cell.Length));

        var reassembler = new TorCellRelay.Reassembler();
        byte[]? rebuilt = null;
        foreach (var cell in cells) rebuilt = reassembler.Accept(cell) ?? rebuilt;
        Assert.Equal(frame, rebuilt);

        // The circuit id is the ONLY field the relay reads — its routing demux.
        Assert.True(TorCellRelay.TryPeekCircuit(cells[0], out var seen));
        Assert.Equal(circuitId, seen);
    }

    private static bool Contains(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
        => haystack.IndexOf(needle) >= 0;

    /// <summary>Records every frame written through it, so a test can inspect exactly what crossed
    /// the relay's egress wire.</summary>
    private sealed class TappedChannel(IWireChannel inner) : IWireChannel
    {
        private readonly ConcurrentQueue<byte[]> _written = new();

        public IReadOnlyList<byte[]> Forwarded => _written.ToList();

        public void WriteFrame(ReadOnlySpan<byte> frame)
        {
            _written.Enqueue(frame.ToArray());
            inner.WriteFrame(frame);
        }

        public byte[]? ReadFrame() => inner.ReadFrame();
        public void Close() => inner.Close();
        public void Dispose() => inner.Dispose();
    }
}
