// SC-003 — store convergence (feature 041-crdtmsg-mvp, T020).
//
// Two stores, randomized op order (+ duplicates + disjoint subsets reconciled by Merkle anti-entropy)
// → identical Merkle root, identical dot set, identical projected state.

using GlpRuntime.CrdtMsg.Store;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class StoreConvergenceTests
{
    private static void Shuffle<T>(IList<T> xs, int seed)
    {
        var rnd = new Random(seed);
        for (int i = xs.Count - 1; i > 0; i--)
        {
            int j = rnd.Next(i + 1);
            (xs[i], xs[j]) = (xs[j], xs[i]);
        }
    }

    [Fact]
    public void Same_opset_random_order_and_duplicates_converge()
    {
        var pool = LwwMap.Pool();
        int distinct = pool.Select(o => o.Id).Distinct().Count();

        using var da = new TempDir();
        using var db = new TempDir();
        var a = OpWal.Open(da.Path);
        var b = OpWal.Open(db.Path);

        var forA = new List<GlpRuntime.CrdtMsg.Crdt.Op>(pool);
        var forB = new List<GlpRuntime.CrdtMsg.Crdt.Op>(pool);
        Shuffle(forA, 1);
        Shuffle(forB, 2);

        // feed each op twice to exercise idempotence
        foreach (var op in forA) { a.Append(op); a.Append(op); }
        foreach (var op in forB) { b.Append(op); b.Append(op); }

        Assert.Equal(distinct, a.Count);
        Assert.Equal(distinct, b.Count);
        Assert.True(MerkleTree.Build(a.Ops).RootEquals(MerkleTree.Build(b.Ops)));
        Assert.Equal(LwwMap.StateOf(a), LwwMap.StateOf(b));
    }

    [Fact]
    public void Disjoint_subsets_reconcile_via_merkle_anti_entropy()
    {
        var pool = LwwMap.Pool();
        int distinct = pool.Select(o => o.Id).Distinct().Count();

        using var da = new TempDir();
        using var db = new TempDir();
        var a = OpWal.Open(da.Path);
        var b = OpWal.Open(db.Path);

        // overlapping-but-different halves
        var half1 = pool.Take(20).ToList();
        var half2 = pool.Skip(10).ToList();
        Shuffle(half1, 3);
        Shuffle(half2, 4);
        foreach (var op in half1) a.Append(op);
        foreach (var op in half2) b.Append(op);

        // before: roots differ (disjoint tails)
        Assert.False(MerkleTree.Build(a.Ops).RootEquals(MerkleTree.Build(b.Ops)));

        var (toA, toB) = AntiEntropy.Reconcile(a, b);
        Assert.True(toA > 0 || toB > 0);

        // after: converged op sets + state
        Assert.Equal(distinct, a.Count);
        Assert.Equal(distinct, b.Count);
        Assert.True(MerkleTree.Build(a.Ops).RootEquals(MerkleTree.Build(b.Ops)));
        Assert.Equal(a.DotSet(), b.DotSet());
        Assert.Equal(LwwMap.StateOf(a), LwwMap.StateOf(b));

        // reconcile is idempotent — a second pass moves nothing
        var again = AntiEntropy.Reconcile(a, b);
        Assert.Equal((0, 0), again);
    }

    [Fact]
    public void Append_is_idempotent_by_dot()
    {
        using var d = new TempDir();
        var a = OpWal.Open(d.Path);
        var op = LwwMap.Set("alice", 1, "k", "v");
        Assert.True(a.Append(op));
        Assert.False(a.Append(op));
        Assert.Equal(1, a.Count);
    }
}
