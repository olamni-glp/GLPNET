// SC-012 — Fugue maximal non-interleaving (feature 041-crdtmsg-mvp, T027).
//
// Two peers type contiguous runs concurrently; ops delivered in randomized order → converged text with
// ZERO interleaving anomaly (each run remains a contiguous substring), and both replicas converge.

using GlpRuntime.CrdtMsg.Crdt;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class FugueTests
{
    // type `text` into `doc` starting at visible index `start`, one element per char; return the ops.
    private static List<Op> Type(RichTextDoc doc, string peer, long baseCtr, int start, string text)
    {
        var ops = new List<Op>();
        for (int i = 0; i < text.Length; i++)
        {
            var op = doc.MakeInsertAt(new Dot(peer, baseCtr + i), start + i, text[i].ToString());
            doc.Apply(op);
            ops.Add(op);
        }
        return ops;
    }

    private static void Shuffle<T>(IList<T> xs, int seed)
    {
        var rnd = new Random(seed);
        for (int i = xs.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (xs[i], xs[j]) = (xs[j], xs[i]); }
    }

    [Fact]
    public void Concurrent_runs_at_start_do_not_interleave_and_converge()
    {
        // both authors start from an empty document
        var da = new RichTextDoc();
        var db = new RichTextDoc();
        var opsA = Type(da, "A", 0, 0, "HELLO");
        var opsB = Type(db, "B", 0, 0, "WORLD");

        var all = opsA.Concat(opsB).ToList();
        var order1 = new List<Op>(all); Shuffle(order1, 11);
        var order2 = new List<Op>(all); Shuffle(order2, 22);

        string t1 = RichTextDoc.Rebuild(order1).Text();
        string t2 = RichTextDoc.Rebuild(order2).Text();

        Assert.Equal(t1, t2);                       // convergence regardless of delivery order
        Assert.Equal(10, t1.Length);
        Assert.Contains("HELLO", t1);               // run stays contiguous — no interleaving
        Assert.Contains("WORLD", t1);
    }

    [Fact]
    public void Concurrent_runs_in_the_middle_do_not_interleave()
    {
        // shared prefix "A"+"Z" both replicas already have
        var seed = new RichTextDoc();
        var shared = new List<Op>();
        shared.Add(seed.MakeInsertAt(new Dot("seed", 0), 0, "A")); seed.Apply(shared[0]);
        shared.Add(seed.MakeInsertAt(new Dot("seed", 1), 1, "Z")); seed.Apply(shared[1]);

        var d1 = RichTextDoc.Rebuild(shared);
        var d2 = RichTextDoc.Rebuild(shared);

        var ops1 = Type(d1, "u1", 0, 1, "123"); // between A and Z
        var ops2 = Type(d2, "u2", 0, 1, "456"); // concurrently between A and Z

        var all = shared.Concat(ops1).Concat(ops2).ToList();
        Shuffle(all, 33);
        string text = RichTextDoc.Rebuild(all).Text();

        Assert.Equal(8, text.Length);               // A + 123 + 456 + Z
        Assert.StartsWith("A", text);
        Assert.EndsWith("Z", text);
        Assert.Contains("123", text);               // neither run interleaves
        Assert.Contains("456", text);
    }

    [Fact]
    public void Delete_is_observed_and_convergent()
    {
        var d = new RichTextDoc();
        var ops = Type(d, "a", 0, 0, "abcd");
        // delete 'b' (element index 1)
        Dot bElem = d.Elements()[1].Id;
        var del = d.MakeDelete(new Dot("a", 100), bElem);
        d.Apply(del);

        var all = ops.Append(del).ToList();
        Assert.Equal("acd", RichTextDoc.Rebuild(all).Text());
        // delete tolerant of reordering (delete before insert in the fold input)
        all.Reverse();
        Assert.Equal("acd", RichTextDoc.Rebuild(all).Text());
    }
}
