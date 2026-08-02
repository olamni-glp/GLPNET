// T024 — snapshot store tests (US2, FR-012/FR-013):
//   - a torn write is never listed: kill-during-Write ⇒ Latest() == previous seq;
//   - seq is monotonic and SHARED across backends (max(both)+1);
//   - fallback engagement is reported loudly (US2/AS-4);
//   - PgliteSnapshotStore over the ISnapshotDb seam (041 in-memory-fake pattern);
//   - live-PGLite pass gated on GLP_SNAPSHOT_PG_CONN (repo env-gate convention).

using GlpRuntime.EngineHost.Snapshot;
using GlpRuntime.EngineHost.Store;

namespace GlpRuntime.EngineHost.Tests;

// ---------------------------------------------------------------- test doubles

/// <summary>In-memory ISnapshotBackend for composite-store tests.</summary>
internal sealed class FakeBackend : ISnapshotBackend
{
    private readonly SortedDictionary<ulong, (long Created, int Fv, byte[] Blob)> _rows = new();

    public string Name { get; }
    public bool FailWrites { get; set; }
    public bool FailReads { get; set; }

    public FakeBackend(string name) { Name = name; }

    public ulong MaxSeq()
    {
        if (FailReads) throw new InvalidOperationException($"{Name} backend down");
        return _rows.Count == 0 ? 0UL : _rows.Keys.Max();
    }

    public void Write(ulong seq, long createdUtcMs, int formatVersion, byte[] blob)
    {
        if (FailWrites) throw new InvalidOperationException($"{Name} backend down");
        if (_rows.ContainsKey(seq))
            throw new SnapshotStoreException($"seq {seq} already exists");
        _rows[seq] = (createdUtcMs, formatVersion, blob);
    }

    public (ulong Seq, byte[] Blob)? Latest()
    {
        if (FailReads) throw new InvalidOperationException($"{Name} backend down");
        if (_rows.Count == 0) return null;
        var top = _rows.Keys.Max();
        return (top, _rows[top].Blob);
    }

    public byte[]? BySeq(ulong seq)
    {
        if (FailReads) throw new InvalidOperationException($"{Name} backend down");
        return _rows.TryGetValue(seq, out var row) ? row.Blob : null;
    }

    public IReadOnlyList<SnapshotMeta> List()
    {
        if (FailReads) throw new InvalidOperationException($"{Name} backend down");
        return _rows.Select(kv => new SnapshotMeta(kv.Key, kv.Value.Created, kv.Value.Blob.LongLength, kv.Value.Fv))
            .ToList();
    }
}

/// <summary>In-memory ISnapshotDb (the 041 InMemoryColabOpDb pattern).</summary>
internal sealed class InMemorySnapshotDb : ISnapshotDb
{
    private readonly Dictionary<(string, long), (long Created, int Fv, byte[] Blob)> _rows = new();
    public int EnsureSchemaCalls { get; private set; }

    public void EnsureSchema() => EnsureSchemaCalls++;

    public bool Insert(string engineIdentity, long seq, long createdUtcMs, int formatVersion, byte[] blob)
    {
        if (_rows.ContainsKey((engineIdentity, seq))) return false;
        _rows[(engineIdentity, seq)] = (createdUtcMs, formatVersion, blob);
        return true;
    }

    public long? MaxSeq(string engineIdentity)
    {
        var seqs = _rows.Keys.Where(k => k.Item1 == engineIdentity).Select(k => k.Item2).ToList();
        return seqs.Count == 0 ? null : seqs.Max();
    }

    public byte[]? BySeq(string engineIdentity, long seq) =>
        _rows.TryGetValue((engineIdentity, seq), out var row) ? row.Blob : null;

    public IReadOnlyList<(long, long, long, int)> List(string engineIdentity) =>
        _rows.Where(kv => kv.Key.Item1 == engineIdentity)
            .OrderBy(kv => kv.Key.Item2)
            .Select(kv => (kv.Key.Item2, kv.Value.Created, kv.Value.Blob.LongLength, kv.Value.Fv))
            .ToList();
}

// ------------------------------------------------------------------- the tests

public class SnapshotStoreTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), $"glpsnap-t024-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static SnapshotBlob BlobFor(ulong seq) =>
        new(SnapshotBlob.FormatVersion1, "engine-test", 1_000, seq,
            SnapshotSection.All.ToDictionary(t => t, _ => Array.Empty<byte>()));

    private static void WriteVia(SnapshotStore store, ulong seq)
    {
        var blob = BlobFor(seq);
        store.Write(blob, blob.Encode(), seq);
    }

    // -------------------------------------------------- torn writes (FR-013)

    [Fact]
    public void TornWrite_IsNeverListed_LatestStaysAtPreviousSeq()
    {
        var file = new FileSnapshotStore(_dir, "engine-test");
        file.Write(1, 1_000, 1, new byte[] { 1, 2, 3 });

        // Simulate a crash at ANY point during the seq-2 write: the blob file
        // exists (temp or renamed) but the manifest was never updated.
        var engineDir = Directory.GetDirectories(_dir).Single();
        File.WriteAllBytes(Path.Combine(engineDir, "snap-2.gsnp.tmp"), new byte[] { 9 });
        File.WriteAllBytes(Path.Combine(engineDir, "snap-3.gsnp"), new byte[] { 9, 9 });

        Assert.Equal(1UL, file.MaxSeq());
        Assert.Single(file.List());
        var latest = file.Latest();
        Assert.Equal(1UL, latest!.Value.Seq);
        Assert.Equal(new byte[] { 1, 2, 3 }, latest.Value.Blob);
        Assert.Null(file.BySeq(3)); // orphan blob without a manifest entry is not a snapshot
    }

    [Fact]
    public void FileStore_DuplicateSeq_FailsLoudly()
    {
        var file = new FileSnapshotStore(_dir, "engine-test");
        file.Write(1, 1_000, 1, new byte[] { 1 });
        Assert.Throws<SnapshotStoreException>(() => file.Write(1, 2_000, 1, new byte[] { 2 }));
    }

    // ------------------------------------- shared monotonic seq across backends

    [Fact]
    public void NextSeq_IsMaxOfBothBackendsPlusOne()
    {
        var primary = new FakeBackend("pglite");
        var fallback = new FakeBackend("file");
        primary.Write(5, 1_000, 1, new byte[] { 1 });
        fallback.Write(3, 1_000, 1, new byte[] { 2 });

        var store = new SnapshotStore(primary, fallback, _ => { });
        Assert.Equal(6UL, store.NextSeq());

        WriteVia(store, 6);
        Assert.Equal(6UL, primary.MaxSeq()); // primary took the write
        Assert.Equal(7UL, store.NextSeq());
    }

    [Fact]
    public void Latest_TakesMaxAcrossBackends_PrimaryPreferredOnTie()
    {
        var primary = new FakeBackend("pglite");
        var fallback = new FakeBackend("file");
        primary.Write(2, 1_000, 1, new byte[] { 0xAA });
        fallback.Write(2, 1_000, 1, new byte[] { 0xBB });
        fallback.Write(4, 1_000, 1, new byte[] { 0xCC });

        var store = new SnapshotStore(primary, fallback, _ => { });
        Assert.Equal(4UL, store.Latest()!.Value.Seq);          // max across both
        Assert.Equal(new byte[] { 0xCC }, store.Latest()!.Value.Blob);

        var tied = new SnapshotStore(primary, new FakeBackend("file2"), _ => { });
        var t = tied.Latest()!.Value;
        Assert.Equal(2UL, t.Seq);
        Assert.Equal(new byte[] { 0xAA }, t.Blob);             // primary on a tie
    }

    // ------------------------------------------- loud fallback engagement (AS-4)

    [Fact]
    public void PrimaryUnavailable_FallbackTakesWrite_DegradationReportedLoudly()
    {
        var primary = new FakeBackend("pglite") { FailWrites = true };
        var fallback = new FakeBackend("file");
        var reports = new List<string>();
        var store = new SnapshotStore(primary, fallback, reports.Add);

        WriteVia(store, 1);

        Assert.Equal(1UL, fallback.MaxSeq());                  // fallback received it
        Assert.NotEmpty(store.LastWriteDegradations);          // surfaced to the requester
        Assert.Contains(reports, r => r.Contains("unavailable"));
    }

    [Fact]
    public void NoPrimaryConfigured_EveryWriteReportsTheDegradation()
    {
        var fallback = new FakeBackend("file");
        var reports = new List<string>();
        var store = new SnapshotStore(null, fallback, reports.Add);

        WriteVia(store, 1);

        Assert.Equal(1UL, fallback.MaxSeq());
        Assert.Contains(store.LastWriteDegradations, m => m.Contains("no primary"));
        Assert.NotEmpty(reports);
    }

    [Fact]
    public void BothBackendsDown_WriteFailsLoudly()
    {
        var primary = new FakeBackend("pglite") { FailWrites = true };
        var fallback = new FakeBackend("file") { FailWrites = true };
        var store = new SnapshotStore(primary, fallback, _ => { });

        Assert.Throws<SnapshotStoreException>(() => WriteVia(store, 1));
    }

    // --------------------------------------------- PGLite backend over the seam

    [Fact]
    public void PgliteBackend_WriteListLatest_OverTheDbSeam()
    {
        var db = new InMemorySnapshotDb();
        var store = PgliteSnapshotStore.Open(db, "engine-test");
        Assert.Equal(1, db.EnsureSchemaCalls);

        Assert.Equal(0UL, store.MaxSeq());
        store.Write(1, 1_000, 1, new byte[] { 1 });
        store.Write(2, 2_000, 1, new byte[] { 2, 2 });

        Assert.Equal(2UL, store.MaxSeq());
        Assert.Equal(new byte[] { 2, 2 }, store.Latest()!.Value.Blob);
        Assert.Equal(new byte[] { 1 }, store.BySeq(1));
        Assert.Equal(2, store.List().Count);

        // Identity scoping: another engine's rows are invisible.
        var other = PgliteSnapshotStore.Open(db, "engine-other");
        Assert.Equal(0UL, other.MaxSeq());
    }

    [Fact]
    public void PgliteBackend_DuplicateSeq_FailsLoudly()
    {
        var store = PgliteSnapshotStore.Open(new InMemorySnapshotDb(), "engine-test");
        store.Write(1, 1_000, 1, new byte[] { 1 });
        Assert.Throws<SnapshotStoreException>(() => store.Write(1, 2_000, 1, new byte[] { 2 }));
    }

    [Fact]
    public void SchemaDdl_IsAdditiveAndIdempotentOnly()
    {
        string ddl = NpgsqlSnapshotDb.SchemaDdl;
        Assert.Contains("CREATE TABLE IF NOT EXISTS glpsnap_snapshot", ddl);
        Assert.Contains("CREATE INDEX IF NOT EXISTS glpsnap_snapshot_identity", ddl);
        Assert.DoesNotContain("DROP ", ddl, StringComparison.OrdinalIgnoreCase);   // additive only
        Assert.DoesNotContain("ALTER ", ddl, StringComparison.OrdinalIgnoreCase);  // (Constitution VI-a)
        Assert.DoesNotContain("DELETE ", ddl, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The live-PGLite pass: runs only when GLP_SNAPSHOT_PG_CONN points at a
    /// reachable PGlite/Postgres (skipped otherwise — the repo's environment-gate
    /// convention, same as glp_crdtmsg's COLAB_PG_CONN test).
    /// </summary>
    [Fact]
    public void LivePglite_RoundTrip_WhenAvailable()
    {
        string? conn = Environment.GetEnvironmentVariable("GLP_SNAPSHOT_PG_CONN");
        if (string.IsNullOrWhiteSpace(conn)) return; // no live PGLite — nothing to verify here

        using var db = new NpgsqlSnapshotDb(conn);
        var identity = $"engine-live-{Guid.NewGuid():N}";
        var store = PgliteSnapshotStore.Open(db, identity);

        store.Write(1, 1_000, 1, new byte[] { 4, 2 });
        Assert.Equal(1UL, store.MaxSeq());
        Assert.Equal(new byte[] { 4, 2 }, store.Latest()!.Value.Blob);
        Assert.Throws<SnapshotStoreException>(() => store.Write(1, 2_000, 1, new byte[] { 9 }));
    }
}
