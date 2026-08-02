// PgliteSnapshotStore — the PGLite-primary backend (T021; contracts/
// snapshot-store.md "Primary — PGLite").
//
// Additive `glpsnap_*` tables on the repo's single bridge-guarded cluster
// (`<repo>/.pgdb/`), reached exactly the way the 041 precedent does
// (csharp/glp_crdtmsg/store/{PgliteOpWal,NpgsqlColabOpDb}.cs — Constitution
// VI-b: no second cluster, no parallel bridge stack): a pluggable ISnapshotDb
// durability seam so unit tests run an in-memory fake, with NpgsqlSnapshotDb
// as the real Npgsql-over-PGLite end, its connection string supplied via
// GLP_SNAPSHOT_PG_CONN (the COLAB_PG_CONN convention).
//
// Durable-then-visible (FR-013): the single-row INSERT is the atomic commit
// point — a snapshot row either exists completely or not at all; there is no
// separate manifest to tear.

using Npgsql;
using NpgsqlTypes;

namespace GlpRuntime.EngineHost.Store;

/// <summary>
/// The pluggable durability seam under <see cref="PgliteSnapshotStore"/> (041
/// IColabOpDb pattern): NpgsqlSnapshotDb is the real backend; tests plug a fake.
/// </summary>
public interface ISnapshotDb
{
    /// <summary>Apply the additive, idempotent `glpsnap_*` DDL (CREATE … IF NOT EXISTS only).</summary>
    void EnsureSchema();

    /// <summary>Insert one snapshot row; false when (engine_identity, seq) already exists.</summary>
    bool Insert(string engineIdentity, long seq, long createdUtcMs, int formatVersion, byte[] blob);

    /// <summary>Highest seq for the identity, or null.</summary>
    long? MaxSeq(string engineIdentity);

    /// <summary>The blob at (identity, seq), or null.</summary>
    byte[]? BySeq(string engineIdentity, long seq);

    /// <summary>All rows for the identity (seq, created, size, format_version), ascending.</summary>
    IReadOnlyList<(long Seq, long CreatedUtcMs, long Size, int FormatVersion)> List(string engineIdentity);
}

public sealed class PgliteSnapshotStore : ISnapshotBackend
{
    private readonly ISnapshotDb _db;
    private readonly string _engineIdentity;

    public string Name => "pglite";

    private PgliteSnapshotStore(ISnapshotDb db, string engineIdentity)
    {
        _db = db;
        _engineIdentity = engineIdentity;
    }

    /// <summary>Open the store: ensure the additive schema, then serve.</summary>
    public static PgliteSnapshotStore Open(ISnapshotDb db, string engineIdentity)
    {
        db.EnsureSchema();
        return new PgliteSnapshotStore(db, engineIdentity);
    }

    public ulong MaxSeq() => (ulong)(_db.MaxSeq(_engineIdentity) ?? 0);

    public void Write(ulong seq, long createdUtcMs, int formatVersion, byte[] blob)
    {
        if (!_db.Insert(_engineIdentity, checked((long)seq), createdUtcMs, formatVersion, blob))
            throw new SnapshotStoreException(
                $"snapshot seq {seq} already exists for '{_engineIdentity}' in the PGLite store " +
                "(monotonic-seq violation)");
    }

    public (ulong Seq, byte[] Blob)? Latest()
    {
        var max = _db.MaxSeq(_engineIdentity);
        if (max is null) return null;
        var blob = _db.BySeq(_engineIdentity, max.Value)
            ?? throw new SnapshotStoreException(
                $"PGLite store lists seq {max} for '{_engineIdentity}' but the row vanished");
        return ((ulong)max.Value, blob);
    }

    public byte[]? BySeq(ulong seq) => _db.BySeq(_engineIdentity, checked((long)seq));

    public IReadOnlyList<SnapshotMeta> List() =>
        _db.List(_engineIdentity)
            .Select(r => new SnapshotMeta((ulong)r.Seq, r.CreatedUtcMs, r.Size, r.FormatVersion))
            .ToList();
}

/// <summary>
/// The real Npgsql (PGLite) end of the seam — mirrors NpgsqlColabOpDb: one
/// connection guarded by a lock, additive IF-NOT-EXISTS DDL only.
/// </summary>
public sealed class NpgsqlSnapshotDb : ISnapshotDb, IDisposable
{
    /// <summary>Additive `glpsnap_*` DDL (Constitution VI-a: no drop, no alter, no delete).</summary>
    public const string SchemaDdl = @"
        -- 061 engine snapshots. One row per complete snapshot. NEVER updated or deleted
        -- by the engine; the INSERT is the durable-then-visible commit point (FR-013).
        CREATE TABLE IF NOT EXISTS glpsnap_snapshot (
            engine_identity TEXT        NOT NULL,
            seq             BIGINT      NOT NULL,
            created_utc_ms  BIGINT      NOT NULL,
            format_version  INT         NOT NULL,
            size            BIGINT      NOT NULL,
            blob            BYTEA       NOT NULL,
            created         TIMESTAMPTZ NOT NULL DEFAULT now(),
            PRIMARY KEY (engine_identity, seq)
        );
        CREATE INDEX IF NOT EXISTS glpsnap_snapshot_identity ON glpsnap_snapshot (engine_identity, seq DESC);";

    private readonly NpgsqlConnection _conn;
    private readonly object _lock = new();

    public NpgsqlSnapshotDb(string connectionString)
    {
        _conn = new NpgsqlConnection(connectionString);
        _conn.Open();
    }

    public void EnsureSchema()
    {
        lock (_lock)
        {
            using var cmd = new NpgsqlCommand(SchemaDdl, _conn);
            cmd.ExecuteNonQuery();
        }
    }

    public bool Insert(string engineIdentity, long seq, long createdUtcMs, int formatVersion, byte[] blob)
    {
        lock (_lock)
        {
            using var cmd = new NpgsqlCommand(
                "INSERT INTO glpsnap_snapshot (engine_identity, seq, created_utc_ms, format_version, size, blob)" +
                " VALUES (@id, @seq, @created, @fv, @size, @blob)" +
                " ON CONFLICT (engine_identity, seq) DO NOTHING", _conn);
            cmd.Parameters.AddWithValue("id", engineIdentity);
            cmd.Parameters.AddWithValue("seq", seq);
            cmd.Parameters.AddWithValue("created", createdUtcMs);
            cmd.Parameters.AddWithValue("fv", formatVersion);
            cmd.Parameters.AddWithValue("size", blob.LongLength);
            cmd.Parameters.AddWithValue("blob", NpgsqlDbType.Bytea, blob);
            return cmd.ExecuteNonQuery() == 1;
        }
    }

    public long? MaxSeq(string engineIdentity)
    {
        lock (_lock)
        {
            using var cmd = new NpgsqlCommand(
                "SELECT max(seq) FROM glpsnap_snapshot WHERE engine_identity = @id", _conn);
            cmd.Parameters.AddWithValue("id", engineIdentity);
            var result = cmd.ExecuteScalar();
            return result is long l ? l : (long?)null;
        }
    }

    public byte[]? BySeq(string engineIdentity, long seq)
    {
        lock (_lock)
        {
            using var cmd = new NpgsqlCommand(
                "SELECT blob FROM glpsnap_snapshot WHERE engine_identity = @id AND seq = @seq", _conn);
            cmd.Parameters.AddWithValue("id", engineIdentity);
            cmd.Parameters.AddWithValue("seq", seq);
            var result = cmd.ExecuteScalar();
            return result as byte[];
        }
    }

    public IReadOnlyList<(long, long, long, int)> List(string engineIdentity)
    {
        lock (_lock)
        {
            using var cmd = new NpgsqlCommand(
                "SELECT seq, created_utc_ms, size, format_version FROM glpsnap_snapshot" +
                " WHERE engine_identity = @id ORDER BY seq", _conn);
            cmd.Parameters.AddWithValue("id", engineIdentity);
            using var rd = cmd.ExecuteReader();
            var rows = new List<(long, long, long, int)>();
            while (rd.Read())
                rows.Add((rd.GetInt64(0), rd.GetInt64(1), rd.GetInt64(2), rd.GetInt32(3)));
            return rows;
        }
    }

    public void Dispose() => _conn.Dispose();
}
