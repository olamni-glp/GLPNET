// SC-009 — end-to-end demonstrator (feature 041-crdtmsg-mvp, T046).
//
// Single host, two clients over the in-memory link fabric (the QUIC/WS adapter is a drop-in replacement).
// Peer A composes ONE rich-text message carrying a seq-insert + a mark-add, routes it to peer B; the CRDT
// document converges on both peers and each op is durable in each store's op-WAL.

using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Crdt.RichText;
using GlpRuntime.CrdtMsg.Route;
using GlpRuntime.CrdtMsg.Store;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class EndToEndTests
{
    [Fact]
    public async Task RichText_op_converges_over_the_link_and_is_durable_on_both_peers()
    {
        var fabric = new InMemoryLinkFabric();
        using var da = new TempDir();
        using var db = new TempDir();
        var a = new MeshNode(fabric.Connect("A"), OpWal.Open(da.Path));
        var b = new MeshNode(fabric.Connect("B"), OpWal.Open(db.Path));

        // A types 'H', then marks it bold
        var insert = a.Doc.MakeInsertAt(new Dot("A", 1), 0, "H");
        await a.SendOpAsync(insert, "B");

        Dot hId = a.Doc.Elements()[0].Id;
        var mark = a.Doc.MakeMarkAdd(new Dot("A", 2), "bold",
            new Anchor(hId, AnchorSide.Before), new Anchor(hId, AnchorSide.After), "true");
        await a.SendOpAsync(mark, "B");

        await b.PumpAsync(2);

        // converged text + formatting on both peers
        Assert.Equal("H", a.Doc.Text());
        Assert.Equal("H", b.Doc.Text());
        Assert.Equal("bold", b.Doc.ActiveMarks().Single().Type);

        // durable in each store's op-WAL (SC-009)
        Assert.Equal(2, a.Store.Count);
        Assert.Equal(2, b.Store.Count);
    }

    [Fact]
    public async Task Redelivery_is_idempotent_at_the_store_boundary()
    {
        var fabric = new InMemoryLinkFabric();
        using var da = new TempDir();
        using var db = new TempDir();
        var a = new MeshNode(fabric.Connect("A"), OpWal.Open(da.Path));
        var b = new MeshNode(fabric.Connect("B"), OpWal.Open(db.Path));

        var insert = a.Doc.MakeInsertAt(new Dot("A", 1), 0, "H");
        await a.SendOpAsync(insert, "B");
        await b.PumpAsync(1);
        await a.SendOpAsync(insert, "B"); // re-send the same op (fresh message)
        await b.PumpAsync(1);

        Assert.Equal(1, b.Store.Count); // idempotent by dot at the store boundary
        Assert.Equal("H", b.Doc.Text());
    }
}
