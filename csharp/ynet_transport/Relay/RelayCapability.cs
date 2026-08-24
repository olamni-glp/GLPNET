using System.Collections.Concurrent;
using Ynet.Transport.Capability;
using Ynet.Transport.Link;
using Ynet.Transport.Path;

namespace Ynet.Transport.Relay;

/// <summary>What a relay granted a transit request: the mechanism it will forward with, and — for
/// circuit-relay-v2 — the reservation voucher that gates every forwarded frame.</summary>
public readonly record struct TransitGrant(RelayMechanism Mechanism, ReservationVoucher? Voucher);

/// <summary>
/// Opens a relayed wire channel <c>dialer → relay → target</c>. The composition root implements this
/// alongside <see cref="INodeEndpointResolver"/> (the in-process fabric does; the QUIC endpoint
/// resolver swaps in behind the same seam) — the capability above it does not change.
/// </summary>
public interface IRelayChannelResolver
{
    /// <param name="proof">The 056 admission decision authorizing <paramref name="relay"/>; the relay
    /// re-enforces it on its own side (a dialer's claim is never trusted).</param>
    Result<IWireChannel> OpenRelayedChannel(
        NodeId dialer, NodeId relay, NodeId target, AdmissionProof proof, Guid circuitId);
}

/// <summary>
/// The US4 relay slice of the transport capability (T028–T033, FR-007/FR-008/FR-016): wires
/// <see cref="IYnetTransport.OfferRelay"/> onto the real relay mechanisms — replacing the honest seam
/// that stood in until T029/T030 landed.
///
/// One node plays BOTH relay roles through this slice:
/// <list type="bullet">
/// <item><b>As a client</b> — <see cref="Offer"/> enforces the 056 <see cref="AdmissionProof"/> and
/// admits a relay into this node's selectable set. Revocation removes it immediately and tears its
/// live paths down at the next frame boundary (research R3, invariant 4).</item>
/// <item><b>As a relay</b> — <see cref="AcceptTransit"/> gates third-party transit on the leaf hook
/// (FR-016, invariant 5) and on the proof admitting THIS node, then <see cref="StartTransit"/> pumps
/// ciphertext only.</item>
/// </list>
///
/// This tier ENFORCES admission and never decides it (FR-024): every gate consumes the 056 proof.
/// </summary>
public sealed class RelayCapability : IDisposable
{
    private readonly NodeId _self;
    private readonly CircuitRelayV2 _circuit;
    private readonly TorCellRelay _cells = new();
    private readonly ConcurrentDictionary<NodeId, AdmittedRelay> _admitted = new();
    private readonly ConcurrentDictionary<Guid, RelayTransit> _transits = new();

    /// <param name="self">This node's id — bound into vouchers it mints and checked against any
    /// proof offered for it as a relay.</param>
    /// <param name="leaf">The leaf-mode hook (FR-016); a fresh full-node hook by default. The
    /// capability's <c>SetMode</c> drives it, so 056's leaf policy binds here.</param>
    /// <param name="clock">Time source for reservation expiry; defaults to wall clock.</param>
    public RelayCapability(NodeId self, LeafMode? leaf = null, Func<DateTimeOffset>? clock = null)
    {
        _self = self;
        Leaf = leaf ?? new LeafMode();
        _circuit = new CircuitRelayV2(self, clock);
    }

    /// <summary>The leaf-never-relays enforcement hook (FR-016) this node's transit is gated on.</summary>
    public LeafMode Leaf { get; }

    // ---- as a client: admission of relays this node may select (FR-007/FR-008) ----

    /// <summary>
    /// <c>offer_relay(relay, AdmissionProof) -> Admitted | Rejected(reason)</c>. Admits ONLY on a
    /// proof that 056 issued for this exact relay and has not revoked (invariant 4) — anything else
    /// is <see cref="RefusalReason.RelayNotAdmitted"/>.
    ///
    /// A revoking proof is the removal path: the relay leaves the selectable set immediately, and
    /// every live path through it tears down at the next frame boundary, surfacing
    /// <see cref="RefusalReason.AuthorizedButUnreachable"/> to the sender (R3).
    /// </summary>
    public Result<Unit> Offer(NodeId relay, AdmissionProof proof)
    {
        if (!AdmissionEnforcer.IsSelectable(relay, proof))
        {
            Withdraw(relay);
            return Result<Unit>.Refuse(RefusalReason.RelayNotAdmitted);
        }

        var mechanism = AdmissionEnforcer.MechanismFor(proof.TrafficClass);
        _admitted.AddOrUpdate(
            relay,
            _ => new AdmittedRelay(proof, mechanism),
            (_, existing) => existing.Rebind(proof, mechanism)); // re-offer keeps live paths up

        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>Is this relay currently selectable — admitted by 056 and not revoked (FR-007)?</summary>
    public bool IsAdmitted(NodeId relay) => _admitted.ContainsKey(relay);

    /// <summary>The mechanism bound for an admitted relay (from its proof's traffic class), else null.</summary>
    public RelayMechanism? MechanismFor(NodeId relay)
        => _admitted.TryGetValue(relay, out var a) ? a.Mechanism : null;

    /// <summary>The 056 proof currently admitting this relay, else null.</summary>
    public AdmissionProof? ProofFor(NodeId relay)
        => _admitted.TryGetValue(relay, out var a) ? a.Proof : null;

    /// <summary>Live paths this node holds through <paramref name="relay"/> (introspection, FR-023).</summary>
    public IReadOnlyCollection<RelayedPath> PathsThrough(NodeId relay)
        => _admitted.TryGetValue(relay, out var a) ? a.Paths.Values.ToList() : [];

    /// <summary>
    /// Track a relayed path so a later revocation can tear it down (R3). Fail-closed: a path
    /// registered against a relay that is (or becomes) un-admitted is torn down at once rather than
    /// left forwarding.
    /// </summary>
    internal RelayedPath RegisterPath(NodeId relay, NodeId target, Guid circuitId, IWireChannel dialerEnd)
    {
        var mechanism = MechanismFor(relay) ?? RelayMechanism.CircuitRelayV2;
        var path = new RelayedPath(circuitId, relay, target, mechanism, dialerEnd);

        if (!_admitted.TryGetValue(relay, out var admitted))
        {
            path.TearDown();
            return path;
        }

        admitted.Paths[circuitId] = path;
        if (!_admitted.ContainsKey(relay)) path.TearDown(); // revoked concurrently — never forward on
        return path;
    }

    private void Withdraw(NodeId relay)
    {
        if (!_admitted.TryRemove(relay, out var admitted)) return;
        foreach (var path in admitted.Paths.Values)
        {
            path.TearDown();
            if (_transits.TryRemove(path.CircuitId, out var transit)) transit.Revoke();
        }
    }

    // ---- as a relay: third-party transit this node forwards (FR-007/FR-016) ----

    /// <summary>
    /// Gate a third-party transit request. Refuses with
    /// <see cref="RefusalReason.LeafTransitRefused"/> in leaf mode (FR-016, invariant 5 — a leaf may
    /// still use relays for its OWN egress) and with <see cref="RefusalReason.RelayNotAdmitted"/>
    /// unless the 056 proof admits THIS node as the relay. The dialer's claim is re-enforced here, so
    /// a proof minted for another relay cannot be replayed at this one (confused-deputy defense).
    /// </summary>
    public Result<TransitGrant> AcceptTransit(NodeId dialer, AdmissionProof proof)
    {
        if (Leaf.EvaluateTransit(isThirdPartyTransit: true) is { } leafRefusal)
            return Result<TransitGrant>.Refuse(leafRefusal);

        if (!AdmissionEnforcer.IsSelectable(_self, proof))
            return Result<TransitGrant>.Refuse(RefusalReason.RelayNotAdmitted);

        var mechanism = AdmissionEnforcer.MechanismFor(proof.TrafficClass);
        if (mechanism != RelayMechanism.CircuitRelayV2)
            return Result<TransitGrant>.Success(new TransitGrant(mechanism, Voucher: null));

        var reserved = _circuit.Reserve(dialer, proof); // libp2p RESERVE precedes CONNECT
        return reserved.Ok
            ? Result<TransitGrant>.Success(new TransitGrant(mechanism, reserved.Value))
            : Result<TransitGrant>.Refuse(reserved.Reason);
    }

    /// <summary>
    /// Begin forwarding a granted circuit: a bidirectional, <b>ciphertext-only</b> pump between the
    /// dialer side and the target side. This node holds no session key for the traffic it carries
    /// (SC-004).
    /// </summary>
    internal RelayTransit StartTransit(
        Guid circuitId, IWireChannel upstream, IWireChannel downstream, TransitGrant grant)
    {
        var transit = new RelayTransit(circuitId, upstream, downstream, grant, _circuit, _cells);
        _transits[circuitId] = transit;
        transit.Start();
        return transit;
    }

    public void Dispose()
    {
        foreach (var transit in _transits.Values) transit.Dispose();
        _transits.Clear();
        foreach (var admitted in _admitted.Values)
            foreach (var path in admitted.Paths.Values) path.Dispose();
        _admitted.Clear();
    }

    private sealed class AdmittedRelay(AdmissionProof proof, RelayMechanism mechanism)
    {
        public AdmissionProof Proof { get; private set; } = proof;
        public RelayMechanism Mechanism { get; private set; } = mechanism;
        public ConcurrentDictionary<Guid, RelayedPath> Paths { get; } = new();

        public AdmittedRelay Rebind(AdmissionProof proof, RelayMechanism mechanism)
        {
            Proof = proof;
            Mechanism = mechanism;
            return this;
        }
    }
}

/// <summary>
/// One path this node holds THROUGH an admitted relay. Its <see cref="PathState"/> follows the
/// data-model lifecycle; <see cref="TearDown"/> is the revocation semantic (research R3): the
/// in-flight frame completes, then the circuit is closed, so the next send observes the distinct
/// <see cref="RefusalReason.AuthorizedButUnreachable"/> rather than a silent drop (FR-018).
/// </summary>
public sealed class RelayedPath : IDisposable
{
    private readonly IWireChannel _dialerEnd;
    private int _tornDown;

    internal RelayedPath(Guid circuitId, NodeId relay, NodeId target, RelayMechanism mechanism, IWireChannel dialerEnd)
    {
        CircuitId = circuitId;
        Relay = relay;
        Target = target;
        Mechanism = mechanism;
        _dialerEnd = dialerEnd;
    }

    public Guid CircuitId { get; }
    public NodeId Relay { get; }
    public NodeId Target { get; }
    public RelayMechanism Mechanism { get; }
    public PathState State { get; } = new();

    /// <summary>The relayed session established over this circuit (Establishing → Live).</summary>
    internal void Established() => State.Established(PathType.Relayed);

    /// <summary>Revoked / closed: drain to the next frame boundary, then the path is unreachable.</summary>
    public void TearDown()
    {
        if (Interlocked.Exchange(ref _tornDown, 1) != 0) return; // idempotent

        switch (State.Phase)
        {
            case PathPhase.Live:
                State.BeginTearDown();
                State.TearDownComplete();
                break;
            case PathPhase.Establishing:
                State.MarkUnreachableFromEstablishing();
                break;
        }

        _dialerEnd.Close();
    }

    public void Dispose() => TearDown();
}

/// <summary>
/// The relay's forwarding loop for one circuit: reads a frame from one side and forwards it to the
/// other through the granted mechanism, in both directions. It never opens a payload — the frame is
/// opaque bytes and this node holds no session key (SC-004). A refusal from the mechanism (expired
/// reservation, closed downstream, malformed cell) stops the loop and closes the circuit, so the
/// endpoints observe a distinct refusal instead of a silent drop (FR-018).
/// </summary>
internal sealed class RelayTransit : IDisposable
{
    private readonly Guid _circuitId;
    private readonly IWireChannel _upstream;
    private readonly IWireChannel _downstream;
    private readonly TransitGrant _grant;
    private readonly CircuitRelayV2 _circuit;
    private readonly TorCellRelay _cells;
    private volatile bool _revoked;

    internal RelayTransit(
        Guid circuitId, IWireChannel upstream, IWireChannel downstream,
        TransitGrant grant, CircuitRelayV2 circuit, TorCellRelay cells)
    {
        _circuitId = circuitId;
        _upstream = upstream;
        _downstream = downstream;
        _grant = grant;
        _circuit = circuit;
        _cells = cells;
    }

    internal Guid CircuitId => _circuitId;

    internal void Start()
    {
        _ = Task.Run(() => Pump(_upstream, _downstream));
        _ = Task.Run(() => Pump(_downstream, _upstream));
    }

    /// <summary>Revocation: stop at the next frame boundary (R3), then close the circuit.</summary>
    internal void Revoke()
    {
        _revoked = true;
        _upstream.Close();
        _downstream.Close();
    }

    private void Pump(IWireChannel from, IWireChannel to)
    {
        try
        {
            while (!_revoked)
            {
                var frame = from.ReadFrame(); // opaque: never opened here
                if (frame is null) break;     // peer closed and drained
                if (_revoked) break;          // next frame boundary

                var forwarded = _grant.Mechanism == RelayMechanism.TorCell
                    ? _cells.Forward(to, frame)
                    : _circuit.Forward(_grant.Voucher!.Value, to, frame);

                if (!forwarded.Ok) break; // distinct refusal — stop, never silently drop
            }
        }
        catch (ObjectDisposedException) { /* circuit disposed under us — teardown is in progress */ }
        finally
        {
            to.Close(); // the far side observes AuthorizedButUnreachable
        }
    }

    public void Dispose() => Revoke();
}
