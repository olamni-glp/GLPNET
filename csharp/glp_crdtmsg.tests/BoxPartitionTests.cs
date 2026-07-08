// Box partitioning (048 bk-colab-yngenios-transport, T011 — net-new C1).
//
// The box is the unit of subscription/convergence (048 data-model §1). These prove: (1) the `Box`
// discriminator on Op is BACK-COMPATIBLE — a default-box op keeps its exact pre-048 byte encoding and
// pre-048 bytes decode unchanged; (2) a non-default box round-trips through the codec and the file
// WAL; (3) the per-box projection folds ONLY that box's ops (op-wal-schema "projection, not
// storage"); (4) the box participates in op identity bytes, so per-box Merkle frontiers differ.

using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Store;
using GlpRuntime.ResultCodec;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class BoxPartitionTests
{
    /// <summary>The exact pre-048 op layout (dot, deps, pred_hash, payload — NO box field).</summary>
    private static byte[] LegacyEncode(Op op)
    {
        var w = new ByteWriter();
        w.WriteString(op.Id.PeerName);
        VarInt.WriteU64(w, (ulong)op.Id.Counter);
        VarInt.WriteU64(w, (ulong)op.Deps.Count);
        foreach (var d in op.Deps)
        {
            w.WriteString(d.PeerName);
            VarInt.WriteU64(w, (ulong)d.Counter);
        }
        VarInt.WriteU64(w, (ulong)op.PredHash.Length);
        w.WriteBytes(op.PredHash);
        VarInt.WriteU64(w, (ulong)op.Payload.Length);
        w.WriteBytes(op.Payload);
        return w.TakeBytes();
    }

    [Fact]
    public void Default_box_op_keeps_the_pre048_byte_encoding()
    {
        var op = LwwMap.Set("alice", 1, "k", "v"); // no box argument anywhere — a pre-048 call site
        Assert.Equal(Op.DefaultBox, op.Box);
        Assert.Equal(LegacyEncode(op), OpCodec.Encode(op)); // byte-identical (goldens/back-compat)
    }

    [Fact]
    public void Pre048_bytes_decode_to_the_default_box()
    {
        var op = LwwMap.Set("alice", 7, "k", "v");
        Op decoded = OpCodec.Decode(LegacyEncode(op)); // bytes with no box field at all
        Assert.Equal(Op.DefaultBox, decoded.Box);
        Assert.Equal(op.Id, decoded.Id);
        Assert.Equal(op.PredHash, decoded.PredHash);
        Assert.Equal(op.Payload, decoded.Payload);
    }

    [Fact]
    public void Boxed_op_roundtrips_through_the_codec()
    {
        var op = Op.Create(new Dot("alice", 2), Array.Empty<Dot>(), LwwMap.SetPayload("k", "v"), box: "inbox");
        Op back = OpCodec.Decode(OpCodec.Encode(op));
        Assert.Equal("inbox", back.Box);
        Assert.Equal(op.Id, back.Id);
        Assert.Equal(op.Payload, back.Payload);
    }

    [Fact]
    public void Trailing_bytes_after_the_box_field_stay_loud()
    {
        var op = Op.Create(new Dot("alice", 3), Array.Empty<Dot>(), LwwMap.SetPayload("k", "v"), box: "inbox");
        byte[] bytes = OpCodec.Encode(op);
        byte[] garbled = bytes.Append((byte)0xFF).ToArray();
        Assert.Throws<CrdtMsgException>(() => OpCodec.Decode(garbled));
    }

    [Fact]
    public void Boxed_op_survives_the_file_wal_roundtrip()
    {
        using var d = new TempDir();
        var op = Op.Create(new Dot("alice", 4), Array.Empty<Dot>(), LwwMap.SetPayload("k", "v"), box: "wip");
        OpWal.Open(d.Path).Append(op);
        var reopened = OpWal.Open(d.Path); // crash/restart — recovery goes through OpCodec
        Assert.Equal("wip", Assert.Single(reopened.Ops).Box);
    }

    [Fact]
    public void Per_box_projection_folds_only_that_boxes_ops()
    {
        // same key written in two boxes — box states must NOT bleed into each other (C1)
        var ops = new List<Op>
        {
            Op.Create(new Dot("alice", 1), Array.Empty<Dot>(), LwwMap.SetPayload("k", "inbox-value"), box: "inbox"),
            Op.Create(new Dot("alice", 2), Array.Empty<Dot>(), LwwMap.SetPayload("k", "wip-value"), box: "wip"),
        };
        var projection = LwwMap.Projection();
        var inbox = projection.RebuildBox(ops, "inbox");
        var wip = projection.RebuildBox(ops, "wip");
        Assert.Equal("inbox-value", inbox["k"].Val);
        Assert.Equal("wip-value", wip["k"].Val);
        Assert.Empty(projection.RebuildBox(ops, "archive")); // an untouched box projects empty
    }

    [Fact]
    public void Partition_by_box_is_deterministic_and_complete()
    {
        var ops = new List<Op>
        {
            Op.Create(new Dot("alice", 1), Array.Empty<Dot>(), LwwMap.SetPayload("a", "1"), box: "inbox"),
            Op.Create(new Dot("bob", 1), Array.Empty<Dot>(), LwwMap.SetPayload("b", "2"), box: "inbox"),
            Op.Create(new Dot("alice", 2), Array.Empty<Dot>(), LwwMap.SetPayload("c", "3")), // default box
        };
        Assert.Equal(new[] { "default", "inbox" }, Projection<object>.Boxes(ops));
        var parts = Projection<object>.PartitionByBox(ops);
        Assert.Equal(2, parts["inbox"].Count);
        Assert.Single(parts["default"]);
        Assert.Equal(ops.Count, parts.Values.Sum(p => p.Count)); // complete, no op dropped
    }

    [Fact]
    public void Box_participates_in_the_merkle_frontier()
    {
        // identical dot+payload in different boxes ⇒ different op bytes ⇒ different Merkle roots
        var inboxOp = Op.Create(new Dot("alice", 1), Array.Empty<Dot>(), LwwMap.SetPayload("k", "v"), box: "inbox");
        var wipOp = Op.Create(new Dot("alice", 1), Array.Empty<Dot>(), LwwMap.SetPayload("k", "v"), box: "wip");
        Assert.False(MerkleTree.Build(new[] { inboxOp }).RootEquals(MerkleTree.Build(new[] { wipOp })));
    }
}
