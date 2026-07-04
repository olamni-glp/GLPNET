// Op semantics (feature 041-crdtmsg-mvp, T026): idempotence, observed-remove tombstone, ground-term +
// acyclic rejection (FR-015/030/031/023), and the delivery reliability window (T031).

using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class OpSemanticsTests
{
    [Fact]
    public void Apply_is_idempotent_by_dot()
    {
        var doc = new RichTextDoc();
        var op = doc.MakeInsertAt(new Dot("a", 1), 0, "x");
        doc.Apply(op);
        doc.Apply(op); // duplicate delivery
        Assert.Equal("x", doc.Text());
    }

    [Fact]
    public void Observed_remove_concurrent_add_survives()
    {
        // C8: replica1 adds "e"@d1; replica2 adds "e"@d2 then removes "e" observing only {d2}.
        var r1 = new ObservedRemoveSet<string>();
        r1.Add("e", new Dot("r1", 1));

        var r2 = new ObservedRemoveSet<string>();
        var d2 = new Dot("r2", 1);
        r2.Add("e", d2);
        r2.Remove("e", new[] { d2 }); // observes only its own add

        r1.Merge(r2);
        r2.Merge(r1);

        // the unobserved concurrent add (d1) keeps "e" alive on both
        Assert.True(r1.Contains("e"));
        Assert.True(r2.Contains("e"));
    }

    [Fact]
    public void Non_ground_op_payload_is_a_transport_fault()
    {
        // a seq_insert whose parent position carries an unbound VarRef → non-ground wire value (FR-023)
        var body = new StructTerm("seq_insert", new Term[]
        {
            new VarRef(new GlobalVarId("x", 1)),
            new ConstTerm(new ConstAtom("r")),
            new ConstTerm(new ConstString("a")),
        });
        var w = new ByteWriter();
        TermCodec.EncodeTerm(w, body);
        var op = new Op(new Dot("a", 1), Array.Empty<Dot>(), new byte[32], w.TakeBytes());

        var doc = new RichTextDoc();
        Assert.Throws<NonGroundTermException>(() => doc.Apply(op));
    }

    [Fact]
    public void Ground_acyclic_term_passes_the_guard()
    {
        var body = new StructTerm("seq_insert", new Term[]
        {
            new ConstTerm(new ConstAtom("root")),
            new ConstTerm(new ConstAtom("r")),
            new ConstTerm(new ConstString("a")),
        });
        TermGuards.ValidateOpPayload(body); // no throw
    }

    [Fact]
    public void Delivery_window_reorders_dedups_and_releases_in_order()
    {
        var w = new DeliveryWindow<string>();
        Assert.Equal(new[] { "a" }, w.Offer(0, "a"));
        Assert.Empty(w.Offer(2, "c"));                 // out of order → buffered
        Assert.Equal(new[] { "b", "c" }, w.Offer(1, "b")); // gap filled → releases b then c
        Assert.Empty(w.Offer(0, "a"));                 // duplicate suppressed
    }

    [Fact]
    public void Delivery_window_single_winner_fencing()
    {
        var w = new DeliveryWindow<string>();
        Assert.Equal(new[] { "a" }, w.Offer(0, "a", epoch: 5)); // adopt epoch 5
        Assert.Empty(w.Offer(1, "b", epoch: 3));                // fenced: stale epoch
        Assert.Equal(new[] { "b" }, w.Offer(1, "b", epoch: 5)); // current winner accepted
    }

    [Fact]
    public void Delivery_window_rejects_beyond_bounded_reorder()
    {
        var w = new DeliveryWindow<string>();
        Assert.Empty(w.Offer(DeliveryWindow<string>.WindowSize, "far")); // >= window ⇒ not accepted
    }
}
