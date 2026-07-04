// SC-013 — Peritext unknown-mark preservation (feature 041-crdtmsg-mvp, T028).
//
// Marks (incl. UNKNOWN types) are preserved verbatim through convergence AND lossless transcode across
// all four surfaces; overlapping concurrent spans coexist.

using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Crdt.RichText;
using GlpRuntime.CrdtMsg.Model;
using GlpRuntime.WireRegistry;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class PeritextTests
{
    private static (RichTextDoc doc, List<Op> ops) Abc()
    {
        var doc = new RichTextDoc();
        var ops = new List<Op>();
        for (int i = 0; i < 3; i++)
        {
            var op = doc.MakeInsertAt(new Dot("t", i), i, "abc"[i].ToString());
            doc.Apply(op);
            ops.Add(op);
        }
        return (doc, ops);
    }

    [Fact]
    public void Unknown_mark_type_survives_convergence()
    {
        var (doc, ops) = Abc();
        var e = doc.Elements();
        var bold = doc.MakeMarkAdd(new Dot("t", 10), "bold",
            new Anchor(e[0].Id, AnchorSide.Before), new Anchor(e[1].Id, AnchorSide.After), "true");
        var blink = doc.MakeMarkAdd(new Dot("t", 11), "blink", // unknown type
            new Anchor(e[1].Id, AnchorSide.Before), new Anchor(e[2].Id, AnchorSide.After), "fast");
        doc.Apply(bold);
        doc.Apply(blink);

        var all = ops.Concat(new[] { bold, blink }).ToList();
        var rebuilt = RichTextDoc.Rebuild(Shuffled(all, 7));
        var types = rebuilt.ActiveMarks().Select(m => m.Type).ToHashSet();
        Assert.Contains("bold", types);
        Assert.Contains("blink", types); // unknown mark preserved verbatim, never dropped
    }

    [Fact]
    public void Overlapping_concurrent_spans_coexist()
    {
        var (seedDoc, seedOps) = Abc();
        var e = seedDoc.Elements();

        var d1 = RichTextDoc.Rebuild(seedOps);
        var d2 = RichTextDoc.Rebuild(seedOps);
        var bold = d1.MakeMarkAdd(new Dot("u1", 1), "bold",
            new Anchor(e[0].Id, AnchorSide.Before), new Anchor(e[1].Id, AnchorSide.After), "true");
        var italic = d2.MakeMarkAdd(new Dot("u2", 1), "italic", // overlapping range
            new Anchor(e[1].Id, AnchorSide.Before), new Anchor(e[2].Id, AnchorSide.After), "true");

        var all = seedOps.Concat(new[] { bold, italic }).ToList();
        var merged = RichTextDoc.Rebuild(Shuffled(all, 8));
        var types = merged.ActiveMarks().Select(m => m.Type).ToHashSet();
        Assert.Contains("bold", types);
        Assert.Contains("italic", types);
    }

    [Fact]
    public void Unknown_mark_survives_transcode_across_all_four_surfaces()
    {
        var (doc, _) = Abc();
        var e = doc.Elements();
        var blink = doc.MakeMarkAdd(new Dot("t", 11), "blink",
            new Anchor(e[0].Id, AnchorSide.Before), new Anchor(e[2].Id, AnchorSide.After), "fast");

        // carry the (unknown-typed) mark op as an opaque section inside a message
        byte[] opBytes = OpCodec.Encode(blink);
        var msg = new Message(1, PayloadType.CrdtMessage,
            new Header("m", "a", "b", 0, RoutingPolicy.Empty),
            new[] { new Section(0x40, opBytes) }, CrdtModel.OpBased);

        foreach (var surface in MessageCodec.Surfaces)
        {
            Message rt = surface.Decode(surface.Encode(msg));
            byte[] recovered = rt.Sections.Single(s => s.TypeNumber == 0x40).Value;
            Op recoveredOp = OpCodec.Decode(recovered);

            var target = new RichTextDoc();
            target.Apply(recoveredOp);
            var mark = target.ActiveMarks().Single();
            Assert.Equal("blink", mark.Type);   // unknown type survived the round trip on every surface
            Assert.Equal("fast", mark.Value);
        }
    }

    private static List<Op> Shuffled(List<Op> ops, int seed)
    {
        var xs = new List<Op>(ops);
        var rnd = new Random(seed);
        for (int i = xs.Count - 1; i > 0; i--) { int j = rnd.Next(i + 1); (xs[i], xs[j]) = (xs[j], xs[i]); }
        return xs;
    }
}
