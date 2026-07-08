// PGlite-backed op-WAL (048 bk-colab-yngenios-transport, T012 — D1/K1: one journal in PGlite).
//
// PgliteOpWal is exercised through its pluggable IColabOpDb seam: the in-memory fake below stands in
// for a live PGlite (unavailable in unit CI); the buildkit-side plugin hands the real NpgsqlColabOpDb
// a home-store connection string. The suite mirrors the file-WAL discipline (StoreRebuildTests):
// idempotent dot apply, crash-reopen zero loss with identical projected state + Merkle root, loud
// corruption, plus the 048 op-wal-schema invariants (append-only INSERT-only journal, lock-step
// Python-authored rows, per-(box, peer) Merkle cursor, publish-once box root, IF-NOT-EXISTS-only DDL).
// An optional live-PGlite pass runs when COLAB_PG_CONN is set (skipped otherwise — repo convention).

using System.Text;
using System.Text.Json;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Store;

namespace GlpRuntime.CrdtMsg.Tests;

/// <summary>The IColabOpDb fake: dictionary semantics identical to the contract SQL (PK + upserts).</summary>
internal sealed class InMemoryColabOpDb : IColabOpDb
{
    private readonly List<ColabOpRow> _rows = new();               // insertion order == created order
    private readonly HashSet<(string, long)> _dots = new();
    public readonly Dictionary<(string Box, string Peer), byte[]> BoxMerkle = new();
    public readonly Dictionary<string, (string Host, byte[] Key)> BoxRoot = new();
    public int EnsureSchemaCalls;

    public IReadOnlyList<ColabOpRow> Rows => _rows;

    public void EnsureSchema() => EnsureSchemaCalls++;

    public bool InsertOp(ColabOpRow row)
    {
        if (!_dots.Add((row.Peer, row.Counter))) return false;    // ON CONFLICT (peer,counter) DO NOTHING
        _rows.Add(row);
        return true;
    }

    public IReadOnlyList<ColabOpRow> LoadOps() => _rows.ToList();

    public void UpsertBoxMerkle(string box, string peer, byte[] merkleRoot) =>
        BoxMerkle[(box, peer)] = merkleRoot;

    public void UpsertBoxRoot(string box, string creatorHost, byte[] rootPubkey)
    {
        if (!BoxRoot.ContainsKey(box)) BoxRoot[box] = (creatorHost, rootPubkey); // publish-once
    }
}

public sealed class PgliteOpWalTests
{
    [Fact]
    public void Open_ensures_the_schema_then_recovers()
    {
        var db = new InMemoryColabOpDb();
        var wal = PgliteOpWal.Open(db);
        Assert.Equal(1, db.EnsureSchemaCalls);
        Assert.Equal(0, wal.Count);
    }

    [Fact]
    public void Append_is_idempotent_by_dot_and_insert_only()
    {
        var db = new InMemoryColabOpDb();
        var wal = PgliteOpWal.Open(db);
        var op = LwwMap.Set("alice", 1, "k", "v");

        Assert.True(wal.Append(op));
        Assert.False(wal.Append(op));                 // replay of a captured op → no-op (FR-005)
        Assert.Equal(1, wal.Count);
        Assert.Single(db.Rows);                       // journal got exactly ONE INSERT, never an update
        Assert.True(wal.Contains(op.Id));
    }

    [Fact]
    public void Reopen_recovers_all_committed_ops_zero_loss()
    {
        var db = new InMemoryColabOpDb();
        string stateBefore, rootBefore;
        int n = LwwMap.Pool().Count;

        {
            var a = PgliteOpWal.Open(db);
            foreach (var op in LwwMap.Pool()) a.Append(op);
            stateBefore = LwwMap.StateOf(a);
            rootBefore = Convert.ToHexStringLower(MerkleTree.Build(a.Ops).Root);
        }

        // "crash": abandon the first instance, reopen over the same journal (rebuild-by-replay, SC-008)
        var b = PgliteOpWal.Open(db);
        Assert.Equal(n, b.Count);
        Assert.Equal(stateBefore, LwwMap.StateOf(b));
        Assert.Equal(rootBefore, Convert.ToHexStringLower(MerkleTree.Build(b.Ops).Root));
    }

    [Fact]
    public void Concurrent_lockstep_writer_dedups_at_the_journal()
    {
        // two store instances over ONE journal — the C# plugin and the Python tier write lock-step
        var db = new InMemoryColabOpDb();
        var a = PgliteOpWal.Open(db);
        var b = PgliteOpWal.Open(db);
        var op = LwwMap.Set("alice", 1, "k", "v");

        Assert.True(a.Append(op));
        Assert.False(b.Append(op));                   // journal PK wins — not double-applied
        Assert.Single(db.Rows);
        Assert.True(b.Contains(op.Id));               // …but b's view still converges
    }

    [Fact]
    public void Boxed_op_roundtrips_with_row_metadata()
    {
        var db = new InMemoryColabOpDb();
        var a = PgliteOpWal.Open(db);
        var op = Op.Create(new Dot("alice", 1), Array.Empty<Dot>(), LwwMap.SetPayload("k", "v"), box: "inbox");
        var meta = new ColabOpMeta(
            MessageId: "msg-001", OpType: "send", MsgType: "task", FromHost: "hostA", ToHost: "hostB",
            MacaroonFp: "fp:abc", PayloadJson: /*lang=json,strict*/ "{\"title\":\"hello\"}");

        Assert.True(a.Append(op, meta));
        var row = Assert.Single(db.Rows);
        Assert.Equal(("alice", 1L, "inbox", "msg-001", "send", "task", "hostA", "hostB", "fp:abc"),
            (row.Peer, row.Counter, row.Box, row.MessageId, row.OpType, row.MsgType, row.FromHost, row.ToHost, row.MacaroonFp));
        using var payload = JsonDocument.Parse(row.PayloadJson);
        Assert.Equal("hello", payload.RootElement.GetProperty("title").GetString());
        Assert.True(payload.RootElement.TryGetProperty("op_b64", out _)); // canonical bytes travel too

        var b = PgliteOpWal.Open(db); // byte-identical recovery via op_b64
        Op back = Assert.Single(b.Ops);
        Assert.Equal(op.Id, back.Id);
        Assert.Equal("inbox", back.Box);
        Assert.Equal(op.Payload, back.Payload);
    }

    [Fact]
    public void Python_authored_row_without_op_b64_recovers_from_columns()
    {
        var db = new InMemoryColabOpDb();
        // what colab/ops.py writes: rich payload JSON, deps as dot strings, no op_b64
        db.InsertOp(new ColabOpRow(
            Peer: "hostB", Counter: 5, Box: "inbox", MessageId: "msg-9", OpType: "move_status",
            MsgType: null, FromHost: "hostB", ToHost: null, PredHash: new byte[32],
            DepsJson: /*lang=json,strict*/ "[\"hostA:3\"]",
            PayloadJson: /*lang=json,strict*/ "{\"status\":\"done\"}", MacaroonFp: null));

        var wal = PgliteOpWal.Open(db);
        Op op = Assert.Single(wal.Ops);
        Assert.Equal(new Dot("hostB", 5), op.Id);
        Assert.Equal("inbox", op.Box);
        Assert.Equal(new Dot("hostA", 3), Assert.Single(op.Deps));
        Assert.Equal("{\"status\":\"done\"}", Encoding.UTF8.GetString(op.Payload));
    }

    [Fact]
    public void Row_disagreeing_with_its_encoded_op_is_detected_loud()
    {
        var db = new InMemoryColabOpDb();
        var a = PgliteOpWal.Open(db);
        a.Append(LwwMap.Set("alice", 1, "k", "v"));

        // corrupt the journal: re-home the row's encoded op under a different dot
        var good = db.Rows[0];
        var forged = new InMemoryColabOpDb();
        forged.InsertOp(good with { Peer = "mallory", Counter = 99 });
        Assert.Throws<CrdtMsgException>(() => PgliteOpWal.Open(forged));
    }

    [Fact]
    public void Anti_entropy_reconciles_two_pglite_journals_via_the_seam()
    {
        var a = PgliteOpWal.Open(new InMemoryColabOpDb());
        var b = PgliteOpWal.Open(new InMemoryColabOpDb());
        var pool = LwwMap.Pool();
        foreach (var op in pool.Where((_, i) => i % 2 == 0)) a.Append(op);
        foreach (var op in pool.Where((_, i) => i % 2 == 1)) b.Append(op);

        AntiEntropy.Reconcile(a, b); // MerkleTree/Delta/Merge over IOpWal — the same code path as OpWal
        Assert.Equal(LwwMap.StateOf(a), LwwMap.StateOf(b));
        Assert.True(MerkleTree.Build(a.Ops).RootEquals(MerkleTree.Build(b.Ops)));
    }

    [Fact]
    public void Publish_box_merkle_maintains_the_per_box_cursor()
    {
        var db = new InMemoryColabOpDb();
        var wal = PgliteOpWal.Open(db);
        wal.Append(Op.Create(new Dot("alice", 1), Array.Empty<Dot>(), LwwMap.SetPayload("k", "v"), box: "inbox"));
        wal.Append(Op.Create(new Dot("alice", 2), Array.Empty<Dot>(), LwwMap.SetPayload("k", "w"), box: "wip"));

        byte[] inboxRoot = wal.PublishBoxMerkle("inbox", "alice");
        byte[] wipRoot = wal.PublishBoxMerkle("wip", "alice");
        Assert.Equal(inboxRoot, db.BoxMerkle[("inbox", "alice")]);
        Assert.Equal(wipRoot, db.BoxMerkle[("wip", "alice")]);
        Assert.NotEqual(inboxRoot, wipRoot); // per-box frontiers are independent (C1)

        // cursor is an upsert: appending more ops and re-publishing moves the root
        wal.Append(Op.Create(new Dot("bob", 1), Array.Empty<Dot>(), LwwMap.SetPayload("k2", "x"), box: "inbox"));
        Assert.NotEqual(inboxRoot, wal.PublishBoxMerkle("inbox", "alice"));
    }

    [Fact]
    public void Box_root_of_trust_is_publish_once()
    {
        var db = new InMemoryColabOpDb();
        var wal = PgliteOpWal.Open(db);
        wal.PublishBoxRoot("inbox", "hostA", new byte[] { 1, 2, 3 });
        wal.PublishBoxRoot("inbox", "mallory", new byte[] { 9, 9, 9 }); // never silently rotated
        Assert.Equal("hostA", db.BoxRoot["inbox"].Host);
        Assert.Equal(new byte[] { 1, 2, 3 }, db.BoxRoot["inbox"].Key);
    }

    [Fact]
    public void Schema_ddl_is_additive_and_idempotent_only()
    {
        string ddl = NpgsqlColabOpDb.SchemaDdl;
        foreach (var table in new[] { "colab_op", "colab_box_merkle", "colab_peer", "colab_artifact_block", "colab_box_root" })
            Assert.Contains($"CREATE TABLE IF NOT EXISTS {table}", ddl);
        Assert.Contains("CREATE INDEX IF NOT EXISTS colab_op_box_msg", ddl);
        Assert.Contains("CREATE INDEX IF NOT EXISTS colab_op_box_created", ddl);
        Assert.DoesNotContain("DROP ", ddl, StringComparison.OrdinalIgnoreCase);   // additive only
        Assert.DoesNotContain("ALTER ", ddl, StringComparison.OrdinalIgnoreCase);  // (Constitution II)
        Assert.DoesNotContain("DELETE ", ddl, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The live-PGlite pass: runs only when COLAB_PG_CONN points at a reachable PGlite/Postgres
    /// (skipped otherwise — the repo's environment-gate convention; a live PGlite is not assumed in
    /// unit CI). Proves the real NpgsqlColabOpDb end of the seam: idempotent EnsureSchema, insert,
    /// conflict-dedup, recovery.
    /// </summary>
    [Fact]
    public void Live_pglite_roundtrip_when_available()
    {
        string? conn = Environment.GetEnvironmentVariable("COLAB_PG_CONN");
        if (string.IsNullOrWhiteSpace(conn)) return; // no live PGlite — nothing to verify here

        using var db = new NpgsqlColabOpDb(conn);
        db.EnsureSchema();
        db.EnsureSchema(); // idempotent re-run

        var wal = PgliteOpWal.Open(db);
        var op = Op.Create(new Dot($"live-{Guid.NewGuid():N}", 1), Array.Empty<Dot>(),
            LwwMap.SetPayload("k", "v"), box: "inbox");
        Assert.True(wal.Append(op));
        Assert.False(wal.Append(op));

        var reopened = PgliteOpWal.Open(db);
        Assert.True(reopened.Contains(op.Id));
        wal.PublishBoxMerkle("inbox", op.Id.PeerName);
    }
}
