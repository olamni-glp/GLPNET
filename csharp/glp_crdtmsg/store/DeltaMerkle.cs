// Delta-state CRDT + Merkle anti-entropy (feature 041-crdtmsg-mvp, T024).
//
// Contract C10 / research R8: two stores converge by exchanging only join-irreducible DELTA mutators
// (the ops a peer lacks), discovered via MERKLE comparison — no causal-broadcast assumption. The op
// set is a grow-only, dot-keyed join-semilattice: join = set union (idempotent by dot). A shared Merkle
// root over the (dot-sorted) op set is the convergence WITNESS — equal roots ⇒ identical op sets ⇒
// (with the deterministic Projection order) identical state.

using System.Security.Cryptography;
using GlpRuntime.CrdtMsg.Crdt;

namespace GlpRuntime.CrdtMsg.Store;

/// <summary>A Merkle tree over the op set: leaves = SHA-256(op bytes), dot-sorted; root witnesses the set.</summary>
public sealed class MerkleTree
{
    /// <summary>32-byte Merkle root over the (dot-sorted) op set. Equal roots ⇒ equal op sets.</summary>
    public byte[] Root { get; }

    /// <summary>The op dots covered, in sorted order.</summary>
    public IReadOnlyList<Dot> Dots { get; }

    private MerkleTree(byte[] root, IReadOnlyList<Dot> dots)
    {
        Root = root;
        Dots = dots;
    }

    public static MerkleTree Build(IReadOnlyCollection<Op> ops)
    {
        var sorted = ops.OrderBy(o => o.Id).ToList();
        var dots = sorted.Select(o => o.Id).ToList();

        var level = sorted.Select(o => SHA256.HashData(OpCodec.Encode(o))).ToList();
        if (level.Count == 0)
            return new MerkleTree(SHA256.HashData(Array.Empty<byte>()), dots);

        while (level.Count > 1)
        {
            var next = new List<byte[]>((level.Count + 1) / 2);
            for (int i = 0; i < level.Count; i += 2)
            {
                if (i + 1 < level.Count)
                    next.Add(SHA256.HashData(Concat(level[i], level[i + 1])));
                else
                    next.Add(level[i]); // odd node promoted
            }
            level = next;
        }
        return new MerkleTree(level[0], dots);
    }

    public bool RootEquals(MerkleTree other) => Root.AsSpan().SequenceEqual(other.Root);

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var c = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, c, 0, a.Length);
        Buffer.BlockCopy(b, 0, c, a.Length, b.Length);
        return c;
    }
}

/// <summary>Merkle-guided anti-entropy: compute the delta a peer lacks, then reconcile two stores.</summary>
public static class AntiEntropy
{
    /// <summary>The join-irreducible delta: ops in <paramref name="source"/> whose dot the peer lacks.</summary>
    public static IReadOnlyList<Op> Delta(IOpWal source, IReadOnlySet<Dot> peerDots) =>
        source.Ops.Where(o => !peerDots.Contains(o.Id)).ToList();

    /// <summary>
    /// Bidirectionally reconcile two stores by exchanging only the ops each lacks (discovered by
    /// comparing dot sets — the Merkle-root inequality is the trigger). Idempotent; converges the op
    /// sets so both Merkle roots match. Returns (appliedToA, appliedToB).
    /// </summary>
    public static (int toA, int toB) Reconcile(IOpWal a, IOpWal b)
    {
        if (MerkleTree.Build(a.Ops).RootEquals(MerkleTree.Build(b.Ops)))
            return (0, 0); // already converged — no delta to exchange

        var aDots = a.DotSet();
        var bDots = b.DotSet();
        int toA = a.Merge(Delta(b, aDots));
        int toB = b.Merge(Delta(a, bDots));
        return (toA, toB);
    }
}
