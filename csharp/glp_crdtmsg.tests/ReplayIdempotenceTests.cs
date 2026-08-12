// Feature 064 — durable listener service box, T009 (contracts/message-log-and-replay.md).
//
// The service-box boot replay is a pure READ of the op journal: ServiceWal hands `Ops` to the
// pump's ReplaySource, and replayed items never reach the delivery observer, so nothing about
// replay may mutate the log. These tests pin that at the IOpWal seam for BOTH backends the
// composer can open (file OpWal fallback, PgliteOpWal primary):
//   * replay reads never append — enumerating Ops leaves the journal untouched;
//   * a second restart is byte-identical (SC-002's double-restart check, journal-level);
//   * crash-replay overlap dedups by dot — re-appending a recovered op (a crash between the
//     durable append and the program observing it) is a no-op (FR-005 idempotence).

using System.Text;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Store;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class ReplayIdempotenceTests
{
    private const string Peer = "service-box"; // ServiceWal's dot peer name

    private static Op ServiceOp(long counter) =>
        Op.Create(new Dot(Peer, counter), Array.Empty<Dot>(), Encoding.UTF8.GetBytes($"msg-{counter}"));

    private static byte[][] Snapshot(IOpWal wal) =>
        wal.Ops.Select(OpCodec.Encode).ToArray();

    [Fact]
    public void Replay_reads_never_append_on_the_file_wal()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"replay-idem-{Guid.NewGuid():N}");
        try
        {
            var wal = OpWal.Open(dir);
            for (long i = 1; i <= 3; i++) Assert.True(wal.Append(ServiceOp(i)));

            // Boot replay = enumerate Ops (twice, as two consecutive boots would).
            for (int pass = 0; pass < 2; pass++)
                Assert.Equal(new[] { "msg-1", "msg-2", "msg-3" },
                    wal.Ops.Select(o => Encoding.UTF8.GetString(o.Payload)).ToArray());

            Assert.Equal(3, wal.Count); // reads appended nothing

            // Restart, replay, restart again: the journal is byte-identical each time.
            var restart1 = OpWal.Open(dir);
            byte[][] afterFirst = Snapshot(restart1);
            _ = restart1.Ops.ToList();                       // the replay read
            var restart2 = OpWal.Open(dir);
            Assert.Equal(afterFirst, Snapshot(restart2));    // SC-002 double-restart byte-identity
            Assert.Equal(3, restart2.Count);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Replay_reads_never_append_on_the_pglite_wal()
    {
        var db = new InMemoryColabOpDb();
        var wal = PgliteOpWal.Open(db);
        for (long i = 1; i <= 3; i++) Assert.True(wal.Append(ServiceOp(i)));
        int rowsBefore = db.Rows.Count;

        for (int pass = 0; pass < 2; pass++)
            _ = wal.Ops.ToList(); // the replay read

        Assert.Equal(rowsBefore, db.Rows.Count); // journal got no new INSERT from reading
        Assert.Equal(3, wal.Count);

        // Restart against the same cluster: recovery is a read too.
        var restarted = PgliteOpWal.Open(db);
        Assert.Equal(Snapshot(wal), Snapshot(restarted));
        Assert.Equal(rowsBefore, db.Rows.Count);
    }

    [Fact]
    public void Crash_replay_overlap_dedups_by_dot_on_the_file_wal()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"replay-dedup-{Guid.NewGuid():N}");
        try
        {
            var wal = OpWal.Open(dir);
            for (long i = 1; i <= 3; i++) Assert.True(wal.Append(ServiceOp(i)));

            // Crash between the durable append and the program observing msg-3, then restart:
            // recovery replays the log AND the retry path re-appends the captured op.
            var restarted = OpWal.Open(dir);
            byte[][] recovered = Snapshot(restarted);
            Assert.False(restarted.Append(ServiceOp(3))); // same dot ⇒ no-op (FR-005)
            Assert.Equal(3, restarted.Count);
            Assert.Equal(recovered, Snapshot(restarted)); // journal unchanged, order intact
            Assert.True(restarted.Contains(new Dot(Peer, 3)));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Crash_replay_overlap_dedups_by_dot_on_the_pglite_wal()
    {
        var db = new InMemoryColabOpDb();
        var wal = PgliteOpWal.Open(db);
        for (long i = 1; i <= 3; i++) Assert.True(wal.Append(ServiceOp(i)));

        var restarted = PgliteOpWal.Open(db);
        Assert.False(restarted.Append(ServiceOp(2)));  // redelivered captured op ⇒ no-op
        Assert.Equal(3, restarted.Count);
        Assert.Equal(3, db.Rows.Count);                // INSERT-only journal: still one row per dot
    }
}
