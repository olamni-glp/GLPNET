using System.Collections.Concurrent;
using Ynet.Transport.Capability;
using Ynet.Transport.Dht;
using Ynet.Transport.HolePunch;
using Ynet.Transport.Link;

namespace Ynet.Transport.Tests.Integration;

/// <summary>NAT behaviour class the simulation models (research R1).</summary>
public enum SimNat
{
    /// <summary>Endpoint-independent mapping (full/restricted cone) — the punchable class.</summary>
    EndpointIndependent,
    /// <summary>Per-destination mapping — the advertised srflx does not admit the peer; not punchable.</summary>
    Symmetric,
}

/// <summary>A controllable monotonic clock for the ≤5 s punch budget (T021).</summary>
public sealed class SimClock : IPunchClock
{
    public TimeSpan Now { get; set; } = TimeSpan.Zero;
    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>
/// A REAL, deterministic NAT-traversal simulation backing every hole-punch seam (candidate
/// gathering, the DCUtR punch probe, and the relay fallback). The traversal LOGIC under test
/// (IceDcutrAgent, PunchOrchestrator, Rendezvous, SKademlia) is production code; this fabric only
/// stands in for the UDP/STUN sockets — modelling the one fact that decides a punch: whether each
/// side's NAT admits an outbound-first peer packet (endpoint-independent) or not (symmetric).
///
/// On a successful punch or relay it mints a real <see cref="InProcessDuplexChannel"/> pair, returns
/// the initiator's end, and stashes the peer's end so a test can run a genuine YnetSession over it
/// (proving zero pending-frame loss on the relay path).
/// </summary>
public sealed class NatFabric : ICandidateGatherer, IPunchProbe, IRelayFallback
{
    private readonly Dictionary<NodeId, SimNat> _nat = new();
    private readonly Dictionary<NodeId, bool> _relayAdmitted = new();
    private readonly ConcurrentDictionary<NodeId, ConcurrentQueue<IWireChannel>> _peerEnds = new();
    private readonly Func<bool> _punchWindowLands;

    /// <param name="punchWindowLands">Models the coordinated-open timing window (default: always lands).
    /// A seeded probability lets the ≥90% SC test exercise occasional misses → relay fallback.</param>
    public NatFabric(Func<bool>? punchWindowLands = null)
        => _punchWindowLands = punchWindowLands ?? (() => true);

    public void Register(NodeId id, SimNat nat, bool relayAdmitted = true)
    {
        _nat[id] = nat;
        _relayAdmitted[id] = relayAdmitted;
    }

    // --- ICandidateGatherer: one server-reflexive candidate carrying the owner's node id ---
    public IReadOnlyList<Candidate> Gather(NodeId self)
        => new[] { new Candidate(CandidateType.ServerReflexive, self.Value, 40000) };

    // --- IPunchProbe: a coordinated open lands iff BOTH ends are endpoint-independent + window lands ---
    public PunchedPath? TryOpen(CandidatePair pair, TimeSpan fireAt)
    {
        var localId = new NodeId(pair.Local.Address);
        var peerId = new NodeId(pair.Remote.Address);
        bool bothCone = Class(localId) == SimNat.EndpointIndependent
                        && Class(peerId) == SimNat.EndpointIndependent;
        if (!bothCone || !_punchWindowLands()) return null;

        var (initEnd, peerEnd) = InProcessDuplexChannel.CreatePair();
        Stash(peerId, peerEnd);
        return new PunchedPath(pair, initEnd);
    }

    // --- IRelayFallback: opens only through an admitted relay (else Unreachable, FR-018) ---
    public RelayResult Open(NodeId peer)
    {
        if (!_relayAdmitted.GetValueOrDefault(peer)) return RelayResult.None;
        var (initEnd, peerEnd) = InProcessDuplexChannel.CreatePair();
        Stash(peer, peerEnd);
        return RelayResult.Relayed(initEnd);
    }

    /// <summary>Take the peer-side channel end minted by the last punch/relay toward <paramref name="peer"/>.</summary>
    public IWireChannel TakePeerEnd(NodeId peer)
        => _peerEnds.TryGetValue(peer, out var q) && q.TryDequeue(out var end)
            ? end
            : throw new InvalidOperationException($"no peer channel end for {peer}");

    private SimNat Class(NodeId id)
        => _nat.TryGetValue(id, out var c) ? c : SimNat.Symmetric; // unknown ⇒ not punchable

    private void Stash(NodeId peer, IWireChannel end)
        => _peerEnds.GetOrAdd(peer, _ => new ConcurrentQueue<IWireChannel>()).Enqueue(end);
}

/// <summary>Test helper: a single in-process S-Kademlia node hosting a shared rendezvous service.</summary>
public static class RendezvousHarness
{
    public static (SKademliaNode dht, RendezvousService rv) SingleNode(NodeId host)
    {
        var dht = new SKademliaNode(host, "sim://" + host.Value, _ => null);
        return (dht, new RendezvousService(dht));
    }
}
