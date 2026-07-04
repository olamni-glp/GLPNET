// DVV dot + hash-chain primitives (feature 041-crdtmsg-mvp, T009).
//
// Contract C7 (crdt-store-richtext.md) / data-model §4 / research R9:
//   Every op has op_id = a DVV **dot** (peer_name, counter): the stable op identity AND the
//   store<->message seam (op_id, distinct from msg_id). Ops carry a pred_hash — a hash of their
//   causal predecessor(s) — a day-one hash-chain that preserves the blocklace/Byzantine upgrade
//   path without redesigning identity. This is foundational: store (§9), crdt (§4), and sig (§8)
//   all consume it.
//
// peer_name is the AUTHENTICATED peer name (BB-HDR-4), never a cert / SPKI pin (C16 / FR-019).

using System.Security.Cryptography;
using System.Text;

namespace GlpRuntime.CrdtMsg.Crdt;

/// <summary>
/// A dotted-version-vector dot: <c>(peer_name, counter)</c>. The stable identity of a single op
/// and the store key (distinct from a message's <c>msg_id</c>). Value type; canonical string form
/// is <c>peer:counter</c>. Total order = (peer_name ordinal, then counter) — a deterministic
/// tiebreak for convergence, NOT causal order (causality is carried by <c>deps</c>).
/// </summary>
public readonly record struct Dot(string PeerName, long Counter) : IComparable<Dot>
{
    /// <summary>Canonical wire/text form <c>peer:counter</c>.</summary>
    public override string ToString() => $"{PeerName}:{Counter}";

    /// <summary>Deterministic tiebreak order for last-writer-style convergence decisions.</summary>
    public int CompareTo(Dot other)
    {
        int c = string.CompareOrdinal(PeerName, other.PeerName);
        return c != 0 ? c : Counter.CompareTo(other.Counter);
    }

    /// <summary>Deterministic bytes for hashing/signing (length-prefixed peer + 8-byte LE counter).</summary>
    public byte[] CanonicalBytes()
    {
        byte[] peer = Encoding.UTF8.GetBytes(PeerName);
        var buf = new byte[4 + peer.Length + 8];
        BinaryPrimitivesLE.WriteInt32(buf, 0, peer.Length);
        Array.Copy(peer, 0, buf, 4, peer.Length);
        BinaryPrimitivesLE.WriteInt64(buf, 4 + peer.Length, Counter);
        return buf;
    }
}

/// <summary>
/// A dotted version vector = per-peer highest contiguously-known counter. Causal context (<c>deps</c>)
/// for ops and the seed for Merkle/delta reconciliation (T024). Immutable-by-copy small map.
/// </summary>
public sealed class VersionVector
{
    private readonly Dictionary<string, long> _max;

    public VersionVector() => _max = new Dictionary<string, long>();
    private VersionVector(Dictionary<string, long> max) => _max = max;

    /// <summary>Highest counter seen for <paramref name="peer"/> (0 if none).</summary>
    public long this[string peer] => _max.TryGetValue(peer, out var v) ? v : 0;

    /// <summary>True if the dot is already dominated by this vector (already-seen ⇒ idempotent).</summary>
    public bool Contains(Dot dot) => this[dot.PeerName] >= dot.Counter;

    /// <summary>A copy advanced to include <paramref name="dot"/> (max-merge on its peer).</summary>
    public VersionVector With(Dot dot)
    {
        var next = new Dictionary<string, long>(_max);
        long cur = this[dot.PeerName];
        if (dot.Counter > cur) next[dot.PeerName] = dot.Counter;
        return new VersionVector(next);
    }

    /// <summary>Pointwise max-merge of two vectors (join — the CRDT lattice join on contexts).</summary>
    public VersionVector Join(VersionVector other)
    {
        var next = new Dictionary<string, long>(_max);
        foreach (var (peer, ctr) in other._max)
            if (!next.TryGetValue(peer, out var cur) || ctr > cur) next[peer] = ctr;
        return new VersionVector(next);
    }

    /// <summary>Peers present, in ordinal order (deterministic).</summary>
    public IEnumerable<string> Peers => _max.Keys.OrderBy(k => k, StringComparer.Ordinal);
}

/// <summary>
/// Day-one op hash-chain (C7). <c>pred_hash</c> = SHA-256 over the sorted canonical bytes of the
/// causal predecessor dots followed by this op's own dot. Deterministic (predecessors are ordered),
/// so two replicas computing the chain for the same op agree byte-for-byte.
/// </summary>
public static class HashChain
{
    /// <summary>32-byte SHA-256 pred-hash binding <paramref name="self"/> to its <paramref name="deps"/>.</summary>
    public static byte[] PredHash(Dot self, IReadOnlyCollection<Dot> deps)
    {
        var ordered = deps.OrderBy(d => d).ToList();
        using var sha = SHA256.Create();
        using var ms = new MemoryStream();
        foreach (var d in ordered)
        {
            byte[] cb = d.CanonicalBytes();
            ms.Write(cb, 0, cb.Length);
        }
        byte[] sb = self.CanonicalBytes();
        ms.Write(sb, 0, sb.Length);
        return sha.ComputeHash(ms.ToArray());
    }

    /// <summary>Lowercase hex of a hash (for stable text keys / provenance).</summary>
    public static string ToHex(byte[] hash) => Convert.ToHexStringLower(hash);
}

/// <summary>Minimal little-endian primitive writers (the term payload is LE — endianness layering law).</summary>
internal static class BinaryPrimitivesLE
{
    public static void WriteInt32(byte[] buf, int off, int v)
    {
        buf[off] = (byte)v;
        buf[off + 1] = (byte)(v >> 8);
        buf[off + 2] = (byte)(v >> 16);
        buf[off + 3] = (byte)(v >> 24);
    }

    public static void WriteInt64(byte[] buf, int off, long v)
    {
        for (int i = 0; i < 8; i++) buf[off + i] = (byte)(v >> (8 * i));
    }
}
