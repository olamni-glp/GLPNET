using System.Security.Cryptography;
using Ynet.Transport.Capability;

namespace Ynet.Transport.Dht;

/// <summary>
/// A routable contact in the embedded S-Kademlia overlay: a self-certified node id (nodeId =
/// H(pubkey), the "S" secure-node-id property) plus the endpoint another node reaches it at.
/// </summary>
public readonly record struct NodeContact(NodeId Id, string Endpoint)
{
    /// <summary>Fixed-width (256-bit) key-space id for XOR routing, derived from the node id.</summary>
    public byte[] KademliaId => DhtId.Of(System.Text.Encoding.ASCII.GetBytes(Id.Value));
}

/// <summary>256-bit key-space helpers (XOR metric over SHA-256 of the raw key/id bytes).</summary>
public static class DhtId
{
    public const int Bytes = 32;

    public static byte[] Of(ReadOnlySpan<byte> raw)
    {
        Span<byte> h = stackalloc byte[Bytes];
        SHA256.HashData(raw, h);
        return h.ToArray();
    }

    /// <summary>XOR distance a⊕b, compared big-endian (smaller = closer). Both must be 32 bytes.</summary>
    public static int CompareDistance(byte[] target, byte[] a, byte[] b)
    {
        for (int i = 0; i < Bytes; i++)
        {
            int da = target[i] ^ a[i];
            int db = target[i] ^ b[i];
            if (da != db) return da < db ? -1 : 1;
        }
        return 0;
    }
}

/// <summary>
/// One node of the embedded S-Kademlia DHT (T019, FR-006/FR-008): iterative XOR-metric store/lookup
/// of <see cref="SignedRecord"/>s over a curated overlay (NEVER a public DHT). REAL + TESTED: the
/// routing/lookup/store logic runs over an in-process node network via the <c>resolve</c> seam (the
/// physical UDP RPC swaps in behind the same seam — the algorithm above it does not change).
///
/// Security properties this provides (the "S" in S-Kademlia): (1) self-certified node ids
/// (nodeId = H(pubkey)) so a node cannot claim an arbitrary id; (2) records are self-certified and
/// **verified at the requesting node regardless of which hop served them** — a tampered or
/// wrong-signer record is discarded, never trusted because a peer returned it (FR-006 / SC-003).
/// Disjoint-path lookup hardening (full S-Kademlia) is a documented extension point, not faked here.
/// </summary>
public sealed class SKademliaNode
{
    /// <summary>k — replication / closeness parameter (Kademlia default 20, sized down for the curated overlay).</summary>
    public const int K = 8;
    /// <summary>α — lookup parallelism.</summary>
    public const int Alpha = 3;

    private readonly NodeContact _self;
    private readonly Func<NodeId, SKademliaNode?> _resolve;
    private readonly List<NodeContact> _routing = new();      // known contacts (k-closest table)
    private readonly Dictionary<string, SignedRecord> _store = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    /// <param name="resolve">Overlay seam: map a contact's node id to its live node (UDP RPC in prod).</param>
    public SKademliaNode(NodeId self, string endpoint, Func<NodeId, SKademliaNode?> resolve)
    {
        _self = new NodeContact(self, endpoint);
        _resolve = resolve;
    }

    public NodeContact Self => _self;

    /// <summary>Learn a bootstrap contact and seed the routing table from it (iterative self-lookup).</summary>
    public void Join(NodeContact bootstrap)
    {
        AddContact(bootstrap);
        _ = FindNode(_self.KademliaId); // populate the table with our own neighbourhood
    }

    /// <summary>
    /// Store a self-certified record at the k closest nodes to its key (FR-006). Refuses to store or
    /// propagate a record that does not self-certify (a node never serves a record it could not itself
    /// verify). Returns false on an invalid record.
    /// </summary>
    public bool Store(SignedRecord record, DateTimeOffset now)
    {
        if (!record.VerifySelfCertified(now)) return false;

        var targets = FindNode(DhtId.Of(record.Key));
        var closest = targets.Count > 0 ? targets : new List<NodeContact> { _self };

        foreach (var c in closest)
        {
            var node = c.Id == _self.Id ? this : _resolve(c.Id);
            node?.AcceptStore(record, now);
        }
        // Always keep a local copy at the initiator too (curated overlay: origin is authoritative).
        AcceptStore(record, now);
        return true;
    }

    /// <summary>RPC target: accept a record into local storage IFF it self-certifies (reject-at-hop).</summary>
    internal bool AcceptStore(SignedRecord record, DateTimeOffset now)
    {
        if (!record.VerifySelfCertified(now)) return false; // never store what we can't verify
        lock (_gate) _store[KeyString(record.Key)] = record;
        return true;
    }

    /// <summary>
    /// Iterative value lookup (FR-006). Returns the record only if it self-certifies AT THIS node —
    /// a record served by any hop whose signature/self-cert fails is discarded and the search
    /// continues, so a lookup result is trustworthy independent of the serving hop (SC-003).
    /// </summary>
    public SignedRecord? Lookup(byte[] key, DateTimeOffset now)
    {
        var target = DhtId.Of(key);
        var ks = KeyString(key);

        // Local hit first.
        if (TryLocal(ks, now, out var localHit)) return localHit;

        var queried = new HashSet<NodeId> { _self.Id };
        var shortlist = ClosestKnown(target, K);

        while (true)
        {
            var batch = shortlist.Where(c => !queried.Contains(c.Id)).Take(Alpha).ToList();
            if (batch.Count == 0) return null;

            var learned = new List<NodeContact>();
            foreach (var c in batch)
            {
                queried.Add(c.Id);
                var node = _resolve(c.Id);
                if (node is null) continue;

                if (node.AnswerFindValue(ks, now) is { } candidate)
                {
                    // Verify HERE regardless of the serving hop; a bad record does not end the search.
                    if (candidate.VerifySelfCertified(now)) return candidate;
                }
                learned.AddRange(node.AnswerFindNode(target));
            }

            foreach (var c in learned) AddContact(c);
            var merged = shortlist.Concat(learned)
                .GroupBy(c => c.Id).Select(g => g.First())
                .OrderBy(c => c.KademliaId, new DistanceComparer(target))
                .Take(K).ToList();

            // Converged: no unqueried contact closer than what we already probed.
            if (merged.All(c => queried.Contains(c.Id))) return null;
            shortlist = merged;
        }
    }

    /// <summary>Iterative node lookup — the k closest contacts to <paramref name="target"/> we can find.</summary>
    public List<NodeContact> FindNode(byte[] target)
    {
        var queried = new HashSet<NodeId> { _self.Id };
        var shortlist = ClosestKnown(target, K);

        while (true)
        {
            var batch = shortlist.Where(c => !queried.Contains(c.Id)).Take(Alpha).ToList();
            if (batch.Count == 0) break;

            var learned = new List<NodeContact>();
            foreach (var c in batch)
            {
                queried.Add(c.Id);
                var node = _resolve(c.Id);
                if (node is null) continue;
                learned.AddRange(node.AnswerFindNode(target));
            }
            foreach (var c in learned) AddContact(c);

            var merged = shortlist.Concat(learned)
                .GroupBy(c => c.Id).Select(g => g.First())
                .OrderBy(c => c.KademliaId, new DistanceComparer(target))
                .Take(K).ToList();
            if (merged.All(c => queried.Contains(c.Id))) { shortlist = merged; break; }
            shortlist = merged;
        }
        return shortlist;
    }

    // ---- RPC-answer surface (in prod these are served over the wire) ----

    internal List<NodeContact> AnswerFindNode(byte[] target) => ClosestKnown(target, K);

    internal SignedRecord? AnswerFindValue(string keyString, DateTimeOffset now)
        => TryLocal(keyString, now, out var r) ? r : null;

    private bool TryLocal(string keyString, DateTimeOffset now, out SignedRecord? record)
    {
        lock (_gate)
        {
            if (_store.TryGetValue(keyString, out var r) && r.VerifySelfCertified(now))
            {
                record = r;
                return true;
            }
        }
        record = null;
        return false;
    }

    private void AddContact(NodeContact c)
    {
        if (c.Id == _self.Id) return;
        lock (_gate)
        {
            if (_routing.Any(x => x.Id == c.Id)) return;
            _routing.Add(c);
        }
    }

    private List<NodeContact> ClosestKnown(byte[] target, int count)
    {
        lock (_gate)
            return _routing.OrderBy(c => c.KademliaId, new DistanceComparer(target)).Take(count).ToList();
    }

    private static string KeyString(byte[] key) => Convert.ToHexString(key);

    private sealed class DistanceComparer(byte[] target) : IComparer<byte[]>
    {
        public int Compare(byte[]? x, byte[]? y) => DhtId.CompareDistance(target, x!, y!);
    }
}
