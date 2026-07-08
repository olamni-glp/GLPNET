// Npgsql (PGlite) backend for the colab op-WAL (048 bk-colab-yngenios-transport, T012).
//
// The concrete IColabOpDb over a PGlite/Postgres connection string — the buildkit-side plugin hands
// this the beacon-style home-store connection string and PgliteOpWal does the rest. Mirrors the
// Beacon.Host DeliveryStore.cs conventions: one NpgsqlConnection guarded by a lock, EnsureSchema is
// CREATE … IF NOT EXISTS only (additive + idempotent; the canonical DDL lives in the 048 contract
// `contracts/op-wal-schema.md` and the Python colab store migration — this mirrors it in lock-step).

using Npgsql;
using NpgsqlTypes;

namespace GlpRuntime.CrdtMsg.Store;

public sealed class NpgsqlColabOpDb : IColabOpDb, IDisposable
{
    /// <summary>
    /// The additive `colab_*` DDL — indicative DDL from contracts/op-wal-schema.md verbatim
    /// (every statement IF NOT EXISTS; re-runnable; no drop, no destructive rewrite).
    /// </summary>
    public const string SchemaDdl = @"
        -- The append-only op journal. One row per CRDT op. NEVER updated or deleted.
        CREATE TABLE IF NOT EXISTS colab_op (
            peer          TEXT        NOT NULL,
            counter       BIGINT      NOT NULL,
            box           TEXT        NOT NULL,
            message_id    TEXT        NOT NULL,
            op_type       TEXT        NOT NULL,
            msg_type      TEXT,
            from_host     TEXT        NOT NULL,
            to_host       TEXT,
            pred_hash     BYTEA,
            deps          JSONB       NOT NULL DEFAULT '[]',
            payload       JSONB       NOT NULL,
            macaroon_fp   TEXT,
            created       TIMESTAMPTZ NOT NULL DEFAULT now(),
            PRIMARY KEY (peer, counter)
        );
        CREATE INDEX IF NOT EXISTS colab_op_box_msg     ON colab_op (box, message_id);
        CREATE INDEX IF NOT EXISTS colab_op_box_created ON colab_op (box, created);

        -- Delta-Merkle anti-entropy summary per box (reconciliation cursor; derived, additive).
        CREATE TABLE IF NOT EXISTS colab_box_merkle (
            box         TEXT        NOT NULL,
            peer        TEXT        NOT NULL,
            merkle_root BYTEA       NOT NULL,
            updated     TIMESTAMPTZ NOT NULL DEFAULT now(),
            PRIMARY KEY (box, peer)
        );

        -- Pinned peer trust set + last mDNS-discovered address (membership; advisory addr).
        CREATE TABLE IF NOT EXISTS colab_peer (
            host_id     TEXT        NOT NULL PRIMARY KEY,
            spki_pin    BYTEA       NOT NULL,
            last_addr   TEXT,
            last_seen   TIMESTAMPTZ
        );

        -- Content-addressed artifact block index for stage-artifact.
        CREATE TABLE IF NOT EXISTS colab_artifact_block (
            content_hash TEXT        NOT NULL,
            block_no     INT         NOT NULL,
            block_hash   TEXT        NOT NULL,
            have         BOOLEAN     NOT NULL DEFAULT FALSE,
            PRIMARY KEY (content_hash, block_no)
        );

        -- Box root-of-trust: the box creator's published root public key (D7, FR-015).
        CREATE TABLE IF NOT EXISTS colab_box_root (
            box          TEXT        NOT NULL PRIMARY KEY,
            creator_host TEXT        NOT NULL,
            root_pubkey  BYTEA       NOT NULL
        );";

    private readonly NpgsqlConnection _conn;
    private readonly object _lock = new();

    public NpgsqlColabOpDb(string connectionString)
    {
        _conn = new NpgsqlConnection(connectionString);
        _conn.Open();
    }

    /// <inheritdoc />
    public void EnsureSchema()
    {
        lock (_lock)
        {
            using var cmd = new NpgsqlCommand(SchemaDdl, _conn);
            cmd.ExecuteNonQuery();
        }
    }

    /// <inheritdoc />
    public bool InsertOp(ColabOpRow row)
    {
        lock (_lock)
        {
            using var cmd = new NpgsqlCommand(
                "INSERT INTO colab_op (peer, counter, box, message_id, op_type, msg_type," +
                " from_host, to_host, pred_hash, deps, payload, macaroon_fp)" +
                " VALUES (@peer, @counter, @box, @message_id, @op_type, @msg_type," +
                " @from_host, @to_host, @pred_hash, @deps, @payload, @macaroon_fp)" +
                " ON CONFLICT (peer, counter) DO NOTHING", _conn);
            cmd.Parameters.AddWithValue("peer", row.Peer);
            cmd.Parameters.AddWithValue("counter", row.Counter);
            cmd.Parameters.AddWithValue("box", row.Box);
            cmd.Parameters.AddWithValue("message_id", row.MessageId);
            cmd.Parameters.AddWithValue("op_type", row.OpType);
            cmd.Parameters.AddWithValue("msg_type", (object?)row.MsgType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("from_host", row.FromHost);
            cmd.Parameters.AddWithValue("to_host", (object?)row.ToHost ?? DBNull.Value);
            cmd.Parameters.AddWithValue("pred_hash", NpgsqlDbType.Bytea, row.PredHash);
            cmd.Parameters.AddWithValue("deps", NpgsqlDbType.Jsonb, row.DepsJson);
            cmd.Parameters.AddWithValue("payload", NpgsqlDbType.Jsonb, row.PayloadJson);
            cmd.Parameters.AddWithValue("macaroon_fp", (object?)row.MacaroonFp ?? DBNull.Value);
            return cmd.ExecuteNonQuery() == 1; // 0 ⇒ conflict ⇒ already committed (idempotent apply)
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ColabOpRow> LoadOps()
    {
        lock (_lock)
        {
            using var cmd = new NpgsqlCommand(
                "SELECT peer, counter, box, message_id, op_type, msg_type, from_host, to_host," +
                " pred_hash, deps::text, payload::text, macaroon_fp" +
                " FROM colab_op ORDER BY created, peer, counter", _conn);
            using var rd = cmd.ExecuteReader();
            var rows = new List<ColabOpRow>();
            while (rd.Read())
            {
                rows.Add(new ColabOpRow(
                    Peer: rd.GetString(0),
                    Counter: rd.GetInt64(1),
                    Box: rd.GetString(2),
                    MessageId: rd.GetString(3),
                    OpType: rd.GetString(4),
                    MsgType: rd.IsDBNull(5) ? null : rd.GetString(5),
                    FromHost: rd.GetString(6),
                    ToHost: rd.IsDBNull(7) ? null : rd.GetString(7),
                    PredHash: rd.IsDBNull(8) ? Array.Empty<byte>() : rd.GetFieldValue<byte[]>(8),
                    DepsJson: rd.GetString(9),
                    PayloadJson: rd.GetString(10),
                    MacaroonFp: rd.IsDBNull(11) ? null : rd.GetString(11)));
            }
            return rows;
        }
    }

    /// <inheritdoc />
    public void UpsertBoxMerkle(string box, string peer, byte[] merkleRoot)
    {
        lock (_lock)
        {
            using var cmd = new NpgsqlCommand(
                "INSERT INTO colab_box_merkle (box, peer, merkle_root) VALUES (@box, @peer, @root)" +
                " ON CONFLICT (box, peer) DO UPDATE SET merkle_root = EXCLUDED.merkle_root, updated = now()",
                _conn);
            cmd.Parameters.AddWithValue("box", box);
            cmd.Parameters.AddWithValue("peer", peer);
            cmd.Parameters.AddWithValue("root", NpgsqlDbType.Bytea, merkleRoot);
            cmd.ExecuteNonQuery();
        }
    }

    /// <inheritdoc />
    public void UpsertBoxRoot(string box, string creatorHost, byte[] rootPubkey)
    {
        lock (_lock)
        {
            // Publish-once: an existing root-of-trust is NEVER silently rotated (D7/FR-015).
            using var cmd = new NpgsqlCommand(
                "INSERT INTO colab_box_root (box, creator_host, root_pubkey) VALUES (@box, @host, @key)" +
                " ON CONFLICT (box) DO NOTHING", _conn);
            cmd.Parameters.AddWithValue("box", box);
            cmd.Parameters.AddWithValue("host", creatorHost);
            cmd.Parameters.AddWithValue("key", NpgsqlDbType.Bytea, rootPubkey);
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose() => _conn.Dispose();
}
