using System.Text;
using System.Text.Json;
using Ynet.Transport.Capability;
using Ynet.Transport.Dht;

namespace Ynet.Transport.HolePunch;

/// <summary>How two peers find each other's candidates before a punch (FR-005; clarify §5.3).</summary>
public enum RendezvousMode
{
    /// <summary>Standard: publish/lookup a self-certified reachability record in the embedded DHT.</summary>
    DhtAddress,
    /// <summary>Internet circuits: an indirection via a rendezvous point (hidden-service style),
    /// so neither side reveals its own address to the DHT — only to the introduction point.</summary>
    HiddenService,
}

/// <summary>A peer's advertised ICE candidates (the punch coordination payload).</summary>
public readonly record struct ReachabilityAdvert(NodeId Node, IReadOnlyList<Candidate> Candidates)
{
    public byte[] Encode()
    {
        var dto = new Dto(Node.Value, Candidates.Select(c => new CandDto((int)c.Type, c.Address, c.Port)).ToArray());
        return JsonSerializer.SerializeToUtf8Bytes(dto);
    }

    public static ReachabilityAdvert Decode(NodeId node, byte[] payload)
    {
        var dto = JsonSerializer.Deserialize<Dto>(payload)!;
        var cands = dto.C.Select(c => new Candidate((CandidateType)c.T, c.A, c.P)).ToList();
        return new ReachabilityAdvert(node, cands);
    }

    private sealed record Dto(string N, CandDto[] C);
    private sealed record CandDto(int T, string A, int P);
}

/// <summary>
/// Rendezvous coordination (T020, FR-005): publishes and resolves the candidate set two peers need
/// to attempt a hole punch. Standard <see cref="RendezvousMode.DhtAddress"/> stores a self-certified
/// reachability record in the embedded S-Kademlia DHT (verifiable independent of the serving hop);
/// <see cref="RendezvousMode.HiddenService"/> registers an ephemeral rendezvous cookie so an
/// internet-circuit peer is reached without exposing its address in the DHT (Veilid private-route /
/// Tor-hidden-service style). REAL + TESTED over the in-process DHT; the transport for the
/// hidden-service introduction hop is a documented seam.
/// </summary>
public sealed class RendezvousService
{
    private static readonly TimeSpan AdvertTtl = TimeSpan.FromMinutes(10);

    private readonly SKademliaNode _dht;
    // Hidden-service cookies: rendezvous-point registration (address kept OUT of the DHT).
    private readonly Dictionary<string, ReachabilityAdvert> _rendezvousPoint = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public RendezvousService(SKademliaNode dht) => _dht = dht;

    /// <summary>Publish this node's candidates so a peer can find them (mode-specific).</summary>
    public void Publish(NodeIdentity self, IReadOnlyList<Candidate> candidates, RendezvousMode mode, DateTimeOffset now)
    {
        var advert = new ReachabilityAdvert(self.NodeId, candidates);
        switch (mode)
        {
            case RendezvousMode.DhtAddress:
                var record = SignedRecord.CreateReachability(self, advert.Encode(), now, AdvertTtl);
                _dht.Store(record, now);
                break;
            case RendezvousMode.HiddenService:
                // Register at a rendezvous point under the node-id cookie; address never hits the DHT.
                lock (_gate) _rendezvousPoint[Cookie(self.NodeId)] = advert;
                break;
        }
    }

    /// <summary>Resolve a peer's candidates for a punch, or null if it is not (yet) reachable.</summary>
    public ReachabilityAdvert? Resolve(NodeId peer, RendezvousMode mode, DateTimeOffset now)
    {
        switch (mode)
        {
            case RendezvousMode.DhtAddress:
                var key = Encoding.ASCII.GetBytes(peer.Value);
                var rec = _dht.Lookup(key, now);
                if (rec is null || rec.Kind != RecordKind.Reachability) return null;
                // Self-cert already checked in the DHT; bind the advert to the verified signer.
                if (rec.SignerNodeId != peer) return null;
                return ReachabilityAdvert.Decode(peer, rec.Payload);
            case RendezvousMode.HiddenService:
                lock (_gate)
                    return _rendezvousPoint.TryGetValue(Cookie(peer), out var a) ? a : (ReachabilityAdvert?)null;
            default:
                return null;
        }
    }

    private static string Cookie(NodeId n) => "rp:" + n.Value;
}
