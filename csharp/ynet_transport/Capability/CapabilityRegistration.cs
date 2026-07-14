using System.Collections.Concurrent;
using Ynet.Transport.Dht;
using Ynet.Transport.Link;

namespace Ynet.Transport.Capability;

/// <summary>
/// The capability-type tokens under which the YNET transport is exposed (T015, FR-004/US1 AS4).
/// These mirror the qhstate 056 `CapabilityType` enum names (`Udp` = datagram authority, `Socket` =
/// stream authority) as plain strings so this tier keeps ZERO compile-time coupling to 056 — the
/// 056 embed resolves by token and adapts the resolved <see cref="IYnetTransport"/> to its own
/// `ICapability.Invoke` chokepoint on its side of the tier boundary (FR-024).
/// </summary>
public static class CapabilityTypes
{
    public const string Udp = "Udp";
    public const string Socket = "Socket";
}

/// <summary>
/// Exposes the YNET transport as a first-class resolvable capability (T015, FR-004): the
/// composition root registers ONE <see cref="IYnetTransport"/> per node under the capability-type
/// tokens, and a 056 stub resolves it — nothing else crosses the boundary. Mirrors the
/// glp_link <c>CapabilityGateRegistry</c> house pattern (instance registry owned by the composition
/// root; ambiguous re-registration throws). Contains NO embed/macaroon/admission/mailbox logic
/// (FR-024/SC-011) — enforcement of that is T056's tier-boundary self-check.
/// </summary>
public sealed class CapabilityRegistration
{
    private readonly Dictionary<string, IYnetTransport> _byType = new(StringComparer.Ordinal);

    /// <summary>Register the transport under a capability-type token; throws on an ambiguous
    /// re-registration to a different instance (same-instance re-registration is a no-op).</summary>
    public void Register(string capabilityType, IYnetTransport transport)
    {
        ArgumentException.ThrowIfNullOrEmpty(capabilityType);
        ArgumentNullException.ThrowIfNull(transport);
        if (_byType.TryGetValue(capabilityType, out var existing) && !ReferenceEquals(existing, transport))
            throw new InvalidOperationException(
                $"capability type '{capabilityType}' already exposes {existing.GetType().Name}");
        _byType[capabilityType] = transport;
    }

    /// <summary>Resolve the transport registered under a token, or null when none is exposed.</summary>
    public IYnetTransport? Resolve(string capabilityType)
        => _byType.TryGetValue(capabilityType, out var t) ? t : null;

    /// <summary>Expose one node's transport as first-class under BOTH network capability tokens
    /// (datagram + stream authority — the YNET link carries either traffic shape).</summary>
    public static CapabilityRegistration ForNode(IYnetTransport transport)
    {
        var reg = new CapabilityRegistration();
        reg.Register(CapabilityTypes.Udp, transport);
        reg.Register(CapabilityTypes.Socket, transport);
        return reg;
    }
}

/// <summary>
/// A REAL in-process node fabric (T015 resolver-seam backing): co-hosted nodes attach, and
/// <see cref="OpenChannel"/> resolves a peer <see cref="NodeId"/> to a live
/// <see cref="InProcessDuplexChannel"/> pair, running the listener's accept handshake on its end —
/// a genuine transport for two in-process nodes (the same fabric the T016 integration test rides).
/// The QUIC endpoint resolver (DHT/rendezvous-backed, US2/US3) swaps in behind the same
/// <see cref="INodeEndpointResolver"/> seam in P3/P4 without touching the capability above it.
/// </summary>
public sealed class InProcessFabric : INodeEndpointResolver
{
    private readonly ConcurrentDictionary<NodeId, YnetTransportCapability> _nodes = new();
    private readonly ConcurrentDictionary<NodeId, SKademliaNode> _dht = new();
    private readonly Func<DateTimeOffset> _clock;

    /// <param name="clock">Time source for DHT TTL/expiry checks; defaults to wall clock.</param>
    public InProcessFabric(Func<DateTimeOffset>? clock = null)
        => _clock = clock ?? (() => DateTimeOffset.UtcNow);

    /// <summary>
    /// Attach a node to the fabric, returning its first-class transport capability. Each node gets a
    /// live S-Kademlia participant in the shared curated overlay (the DHT resolve seam maps across
    /// the fabric's other nodes — UDP RPC swaps in behind it in prod), so the US3 discovery slice is
    /// REAL over co-hosted nodes. Call <see cref="FormOverlay"/> once all nodes are attached.
    /// </summary>
    public YnetTransportCapability AttachNode(NodeIdentity identity)
    {
        var kad = new SKademliaNode(
            identity.NodeId,
            $"inproc://{identity.NodeId.Value}",
            id => _dht.TryGetValue(id, out var n) ? n : null);
        var capability = new YnetTransportCapability(identity, this, new DhtCapability(kad, _clock));
        if (!_nodes.TryAdd(identity.NodeId, capability))
            throw new InvalidOperationException($"node {identity.NodeId} already attached");
        _dht[identity.NodeId] = kad;
        return capability;
    }

    /// <summary>
    /// Bootstrap every attached node into one S-Kademlia overlay through the first-attached node, so
    /// a record stored at any node is discoverable by iterative lookup from any other (SC-003). A
    /// no-op for a fabric with fewer than two nodes.
    /// </summary>
    public void FormOverlay()
    {
        var nodes = _dht.Values.ToList();
        if (nodes.Count < 2) return;
        var bootstrap = nodes[0];
        foreach (var n in nodes.Skip(1))
        {
            n.Join(bootstrap.Self);       // learner discovers the bootstrap's neighbourhood
            bootstrap.Join(n.Self);        // bootstrap learns the joiner (curated overlay, mutual)
        }
    }

    public Result<IWireChannel> OpenChannel(NodeId peer)
    {
        if (!_nodes.TryGetValue(peer, out var listener))
            return Result<IWireChannel>.Refuse(RefusalReason.Unreachable); // FR-018, no packet sent

        var (dialerEnd, listenerEnd) = InProcessDuplexChannel.CreatePair();
        _ = Task.Run(() => listener.AcceptInbound(listenerEnd));
        return Result<IWireChannel>.Success(dialerEnd);
    }
}
