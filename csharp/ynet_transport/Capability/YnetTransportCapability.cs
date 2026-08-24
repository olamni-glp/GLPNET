using System.Collections.Concurrent;
using Ynet.Transport.Link;

namespace Ynet.Transport.Capability;

/// <summary>
/// Resolves a peer <see cref="NodeId"/> to an open wire channel (T015 resolver seam). Full
/// NodeId→address resolution needs the DHT/rendezvous of US2/US3 (T019/T020); until those land the
/// REAL <see cref="InProcessFabric"/> (CapabilityRegistration.cs) implements this for co-hosted
/// nodes, and the QUIC endpoint resolver swaps in behind the same seam (P3/P4) — the capability
/// code above it does not change.
/// </summary>
public interface INodeEndpointResolver
{
    /// <summary>Open a duplex wire channel to <paramref name="peer"/>, or refuse distinctly
    /// (<see cref="RefusalReason.Unreachable"/> when no path to the peer exists — FR-018).</summary>
    Result<IWireChannel> OpenChannel(NodeId peer);
}

/// <summary>
/// The REAL <see cref="IYnetTransport"/> implementation (T014/T015, US1 AS1–2/AS4): the single
/// first-class capability a 056 stub resolves and drives. `connect`/`send`/`receive`/`close` ride
/// live <see cref="YnetSession"/>s (identity-verified handshake + per-direction AES-256-GCM seal)
/// over channels obtained from the <see cref="INodeEndpointResolver"/> seam. Contains NO
/// service-embed, macaroon-minting, admission-deciding, or durable-mailbox logic (FR-004/FR-024) —
/// 056 binds its policy to this surface from the other side of the tier boundary.
///
/// Honest seams (Constitution II): the DHT discovery slice (US3) is REAL when a <see
/// cref="Dht.DhtCapability"/> is attached (T025 — wired onto the embedded S-Kademlia node), and the
/// relay slice (US4) is REAL when a <see cref="Relay.RelayCapability"/> is attached (T029/T030 —
/// circuit-relay-v2 + Tor-style cell forward). An unconfigured node throws a clear
/// NotSupportedException for the slice it does not host — it never pretends to succeed.
/// </summary>
public sealed class YnetTransportCapability : IYnetTransport, IDisposable
{
    private readonly NodeIdentity _self;
    private readonly INodeEndpointResolver _resolver;
    private readonly Dht.DhtCapability? _dht;
    private readonly Relay.RelayCapability? _relay;
    private readonly ConcurrentDictionary<Guid, (YnetSession Session, RoutingSelection Selection)> _sessions = new();
    private readonly BlockingCollection<LinkHandle> _acceptedLinks = new(new ConcurrentQueue<LinkHandle>());
    private volatile NodeMode _mode = NodeMode.Full;

    /// <param name="dht">The discovery slice over this node's S-Kademlia participant (US3). When
    /// absent, the node exposes no DHT and the discovery operations refuse honestly.</param>
    /// <param name="relay">The relay slice (US4): enforces the 056 admission decision and forwards
    /// ciphertext-only. When absent, the node offers no relay mechanism and refuses honestly.</param>
    public YnetTransportCapability(
        NodeIdentity self,
        INodeEndpointResolver resolver,
        Dht.DhtCapability? dht = null,
        Relay.RelayCapability? relay = null)
    {
        _self = self;
        _resolver = resolver;
        _dht = dht;
        _relay = relay;
    }

    /// <summary>This node's relay slice (composition-root wiring for the transit side of US4).</summary>
    internal Relay.RelayCapability? RelaySlice => _relay;

    /// <summary>This node's self-certified identity (nodeId = H(pubkey), FR-002).</summary>
    public NodeId NodeId => _self.NodeId;

    /// <summary>Current mode; leaf transit-refusal binds at the forward hook (US4/US8, FR-016).</summary>
    public NodeMode Mode => _mode;

    internal int LiveSessionCount => _sessions.Count;

    // --- core link (US1/US2) ---

    public Result<LinkHandle> Connect(NodeId peer, RoutingSelection selection)
    {
        var channel = _resolver.OpenChannel(peer);
        if (!channel.Ok)
            return Result<LinkHandle>.Refuse(channel.Reason); // zero wire side-effects (invariant 2)

        var session = YnetSession.Connect(channel.Value!, _self, peer, selection);
        if (!session.Ok)
        {
            channel.Value!.Dispose();
            return Result<LinkHandle>.Refuse(session.Reason);
        }

        _sessions[session.Value!.Handle.Id] = (session.Value!, selection);
        return Result<LinkHandle>.Success(session.Value!.Handle);
    }

    public Result<Ack> Send(LinkHandle link, ReadOnlyMemory<byte> frame)
        => _sessions.TryGetValue(link.Id, out var s)
            ? s.Session.Send(frame)
            : Result<Ack>.Refuse(RefusalReason.AuthorizedButUnreachable);

    public Result<ReadOnlyMemory<byte>> Receive(LinkHandle link)
        => _sessions.TryGetValue(link.Id, out var s)
            ? s.Session.Receive()
            : Result<ReadOnlyMemory<byte>>.Refuse(RefusalReason.AuthorizedButUnreachable);

    public void Close(LinkHandle link)
    {
        if (_sessions.TryRemove(link.Id, out var s))
        {
            s.Session.Close(); // graceful close-after-collect (050 discipline)
            s.Session.Dispose();
        }
    }

    /// <summary>
    /// Inbound rendezvous for the hosting composition root / fabric (NOT part of the 056-facing
    /// <see cref="IYnetTransport"/> contract): accept a dialer's handshake on
    /// <paramref name="channel"/> and, when the pre-frame identity gate passes, track the live
    /// session and surface its handle via <see cref="TryAcceptLink"/>. A refused handshake tears
    /// the channel down — the dialer observes its own distinct refusal.
    /// </summary>
    internal void AcceptInbound(IWireChannel channel)
    {
        var accepted = YnetSession.Accept(channel, _self, RoutingSelection.SafeDefault);
        if (!accepted.Ok)
        {
            channel.Dispose();
            return;
        }
        _sessions[accepted.Value!.Handle.Id] = (accepted.Value!, RoutingSelection.SafeDefault);
        _acceptedLinks.Add(accepted.Value!.Handle);
    }

    /// <summary>Block up to <paramref name="timeout"/> for the next accepted inbound link.</summary>
    public bool TryAcceptLink(TimeSpan timeout, out LinkHandle link)
        => _acceptedLinks.TryTake(out link, timeout);

    // --- discovery (US3) — REAL over the embedded S-Kademlia node (T025) ---

    public Result<Unit> DhtStore(Dht.SignedRecord record)
        => _dht is { } dht
            ? dht.Store(record)
            : throw new NotSupportedException(
                "no DHT overlay attached to this node — construct with a Dht.DhtCapability (051 T025).");

    public Result<Dht.SignedRecord> DhtLookup(ReadOnlyMemory<byte> key)
        => _dht is { } dht
            ? dht.Lookup(key)
            : throw new NotSupportedException(
                "no DHT overlay attached to this node — construct with a Dht.DhtCapability (051 T025).");

    // --- relay mechanism (US4) — REAL over the attached relay slice (T029/T030) ---

    /// <summary>
    /// <c>offer_relay(relay, AdmissionProof) -> Admitted | Rejected(reason)</c>. Enforces the 056
    /// decision, never decides it (FR-007/FR-024): admits only a relay 056 issued this exact proof
    /// for and has not revoked. A revoking proof removes the relay and tears its live paths down at
    /// the next frame boundary (invariant 4).
    /// </summary>
    public Result<Unit> OfferRelay(NodeId relay, AdmissionProof proof)
        => _relay is { } slice
            ? slice.Offer(relay, proof)
            : throw new NotSupportedException(
                "no relay slice attached to this node — construct with a Relay.RelayCapability (051 T029/T030).");

    /// <summary>
    /// Establish a RELAYED path to <paramref name="target"/> through an ADMITTED
    /// <paramref name="relay"/> (US4). The end-to-end <see cref="YnetSession"/> seal is negotiated
    /// with the target, so the relay forwards ciphertext it holds no key for (SC-004).
    ///
    /// NOT part of the 056-facing <see cref="IYnetTransport"/> contract: this is the composition-root
    /// entry for the relay mechanism, and the surface US2's automatic direct-then-relay fallback
    /// (FR-018) binds to when its selection lands. A relay 056 has not admitted — or has revoked — is
    /// refused with <see cref="RefusalReason.RelayNotAdmitted"/> before any wire side-effect
    /// (invariant 2).
    /// </summary>
    public Result<LinkHandle> ConnectViaRelay(NodeId relay, NodeId target, RoutingSelection selection)
    {
        if (_relay is not { } slice)
            throw new NotSupportedException(
                "no relay slice attached to this node — construct with a Relay.RelayCapability (051 T029/T030).");

        // Enforce first: an un-admitted / revoked relay is never selected (FR-007, invariant 4).
        if (slice.ProofFor(relay) is not { } proof)
            return Result<LinkHandle>.Refuse(RefusalReason.RelayNotAdmitted);

        if (_resolver is not Relay.IRelayChannelResolver relayResolver)
            throw new NotSupportedException(
                "this node's endpoint resolver cannot open a relayed channel — the composition root " +
                "must implement Relay.IRelayChannelResolver (051 T029/T030).");

        var circuitId = Guid.NewGuid();
        var channel = relayResolver.OpenRelayedChannel(_self.NodeId, relay, target, proof, circuitId);
        if (!channel.Ok)
            return Result<LinkHandle>.Refuse(channel.Reason); // relay refused transit — zero side-effects

        var path = slice.RegisterPath(relay, target, circuitId, channel.Value!);

        var session = YnetSession.Connect(channel.Value!, _self, target, selection, PathType.Relayed);
        if (!session.Ok)
        {
            path.TearDown();
            channel.Value!.Dispose();
            return Result<LinkHandle>.Refuse(session.Reason);
        }

        path.Established();
        _sessions[session.Value!.Handle.Id] = (session.Value!, selection);
        return Result<LinkHandle>.Success(session.Value!.Handle);
    }

    // --- leaf mode (US8) ---

    /// <summary>Set the node mode and bind it to the relay transit hook, so a leaf refuses
    /// third-party transit while still using relays for its own egress (FR-016).</summary>
    public void SetMode(NodeMode mode)
    {
        _mode = mode;
        _relay?.Leaf.SetMode(mode);
    }

    // --- introspection (FR-023) ---

    public PathInfo PathInfo(LinkHandle link)
        => _sessions.TryGetValue(link.Id, out var s)
            ? s.Session.Info(s.Selection)
            : new PathInfo(link.PathType, RoutingMode.Normal, AnonymityLevel: 0, RelayHops: 0);

    public void Dispose()
    {
        foreach (var (_, s) in _sessions)
        {
            s.Session.Close();
            s.Session.Dispose();
        }
        _sessions.Clear();
        _relay?.Dispose(); // tears down live relayed circuits
        _acceptedLinks.Dispose();
    }
}
