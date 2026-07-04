// SC-004 — crash-rebuild zero-loss (feature 041-crdtmsg-mvp, T021).
//
// Reopen the WAL over the same directory (a simulated crash/restart): every committed op is recovered,
// the projected state and Merkle root are identical, an orphan temp (interrupted, never-acked write) is
// discarded, and a corrupted committed op is detected loudly (never silently accepted).

using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Store;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class StoreRebuildTests
{
    [Fact]
    public void Reopen_recovers_all_committed_ops_zero_loss()
    {
        using var d = new TempDir();
        string rootBefore;
        string stateBefore;
        int n = LwwMap.Pool().Count;

        {
            var a = OpWal.Open(d.Path);
            foreach (var op in LwwMap.Pool()) a.Append(op);
            stateBefore = LwwMap.StateOf(a);
            rootBefore = Convert.ToHexStringLower(MerkleTree.Build(a.Ops).Root);
        }

        // "crash": abandon the first instance, reopen the same directory
        var b = OpWal.Open(d.Path);
        Assert.Equal(n, b.Count);
        Assert.Equal(stateBefore, LwwMap.StateOf(b));
        Assert.Equal(rootBefore, Convert.ToHexStringLower(MerkleTree.Build(b.Ops).Root));
    }

    [Fact]
    public void Orphan_temp_from_interrupted_write_is_discarded()
    {
        using var d = new TempDir();
        var a = OpWal.Open(d.Path);
        foreach (var op in LwwMap.Pool()) a.Append(op);
        int committed = a.Count;

        // simulate an interrupted, never-committed write: a leftover .op.tmp
        string orphan = Path.Combine(d.Path, "ops", "999999999999.op.tmp");
        File.WriteAllBytes(orphan, new byte[] { 1, 2, 3, 4 });

        var b = OpWal.Open(d.Path);
        Assert.Equal(committed, b.Count);
        Assert.False(File.Exists(orphan)); // recovery cleaned it up
    }

    [Fact]
    public void Corrupted_committed_op_is_detected_loud()
    {
        using var d = new TempDir();
        var a = OpWal.Open(d.Path);
        a.Append(LwwMap.Set("alice", 1, "k", "v"));
        a.Append(LwwMap.Set("bob", 1, "k2", "v2"));

        // flip a byte in a committed op file → self-hash no longer matches
        string opFile = Directory.EnumerateFiles(Path.Combine(d.Path, "ops"), "*.op").First();
        byte[] bytes = File.ReadAllBytes(opFile);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(opFile, bytes);

        Assert.Throws<CrdtMsgException>(() => OpWal.Open(d.Path));
    }
}
