// PGlite-backed op-WAL (048 bk-colab-yngenios-transport, T012 — D1/K1: ONE journal in PGlite).
//
// 048 contracts/op-wal-schema.md: the CRDT operation journal is the SINGLE system of record — additive
// `colab_*` tables in a beacon-style per-host home store, written lock-step by this C# tier and the
// Python `colab/` tier (exactly the beacon_mailbox_delivery ↔ DeliveryStore.cs pattern). Invariants:
//   1. append-only  — `colab_op` rows are only ever INSERTed; status/delete are NEW ops, never UPDATE.
//   2. idempotent   — PK (peer, counter) + `INSERT … ON CONFLICT DO NOTHING` make a replayed op a no-op.
//   3. projection   — box state folds `colab_op` for a box; HEAD is rebuildable by replay (SC-008).
//   4. no dual-write — the op INSERT *is* the durable commit; there is no second outbox table (R4).
//
// The class sits behind the same IOpWal store seam as the file-backed OpWal (which remains alongside,
// unchanged); MerkleTree / AntiEntropy / Projection / MeshNode work over either. Durability is delegated
// to a pluggable IColabOpDb so unit tests run against an in-memory fake and the buildkit-side plugin
// hands NpgsqlColabOpDb a PGlite connection string.
//
// Row mapping (lock-step note): this tier persists the CANONICAL op bytes (OpCodec) base64-wrapped
// under the `op_b64` payload key — that is what guarantees byte-identical replay + Merkle equality
// (SC-008). The Python tier writes rich payload JSON without `op_b64`; such rows are reconstructed on
// recovery from the row columns (dot, deps, pred_hash, box) with the payload JSON text as the opaque
// op body. Cross-tier byte-parity of *Python-authored* ops is a later, gated integration concern.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Envelope;

namespace GlpRuntime.CrdtMsg.Store;

/// <summary>One `colab_op` row (op-wal-schema.md). `DepsJson`/`PayloadJson` are JSONB text.</summary>
public sealed record ColabOpRow(
    string Peer,
    long Counter,
    string Box,
    string MessageId,
    string OpType,
    string? MsgType,
    string FromHost,
    string? ToHost,
    byte[] PredHash,
    string DepsJson,
    string PayloadJson,
    string? MacaroonFp);

/// <summary>
/// Optional seam-level metadata for a `colab_op` row (the message-projection columns the Python tier
/// also reads). Engine-level opaque ops take the defaults; the plugin layer supplies real values.
/// `PayloadJson`, when given, must be a JSON object — it is merged with the canonical `op_b64` field.
/// </summary>
public sealed record ColabOpMeta(
    string? MessageId = null,
    string OpType = "send",
    string? MsgType = null,
    string? FromHost = null,
    string? ToHost = null,
    string? MacaroonFp = null,
    string? PayloadJson = null)
{
    public static readonly ColabOpMeta Default = new();
}

/// <summary>
/// The pluggable durability seam under <see cref="PgliteOpWal"/>: the real backend is
/// <see cref="NpgsqlColabOpDb"/> (PGlite over Npgsql); tests plug an in-memory fake.
/// </summary>
public interface IColabOpDb
{
    /// <summary>Apply the additive, idempotent `colab_*` DDL (CREATE … IF NOT EXISTS only).</summary>
    void EnsureSchema();

    /// <summary>`INSERT … ON CONFLICT (peer, counter) DO NOTHING`; true iff the row was newly inserted.</summary>
    bool InsertOp(ColabOpRow row);

    /// <summary>All op rows in deterministic commit order (created, then dot order).</summary>
    IReadOnlyList<ColabOpRow> LoadOps();

    /// <summary>Upsert the delta-Merkle reconciliation cursor for a (box, peer) frontier.</summary>
    void UpsertBoxMerkle(string box, string peer, byte[] merkleRoot);

    /// <summary>Publish a box's root-of-trust public key (insert-once; an existing root is never overwritten).</summary>
    void UpsertBoxRoot(string box, string creatorHost, byte[] rootPubkey);
}

/// <summary>
/// The PGlite-backed op-WAL: same store seam as the file WAL, durably committed by the `colab_op`
/// INSERT (the transactional-outbox-free commit point, R4). Idempotent by dot at BOTH layers — the
/// in-memory set and the (peer, counter) primary key — so a concurrent lock-step writer (the Python
/// tier) never double-applies.
/// </summary>
public sealed class PgliteOpWal : IOpWal
{
    private const string OpB64Key = "op_b64";

    private readonly IColabOpDb _db;
    private readonly Dictionary<Dot, Op> _byDot = new();
    private readonly List<Op> _commitOrder = new();

    private PgliteOpWal(IColabOpDb db) => _db = db;

    /// <summary>Open the op-WAL over <paramref name="db"/>: ensure the schema, then recover by replay.</summary>
    public static PgliteOpWal Open(IColabOpDb db)
    {
        var wal = new PgliteOpWal(db);
        db.EnsureSchema();
        wal.Recover();
        return wal;
    }

    /// <inheritdoc />
    public IReadOnlyList<Op> Ops => _commitOrder;

    /// <inheritdoc />
    public int Count => _byDot.Count;

    /// <inheritdoc />
    public bool Contains(Dot dot) => _byDot.ContainsKey(dot);

    /// <inheritdoc />
    public IReadOnlySet<Dot> DotSet() => new HashSet<Dot>(_byDot.Keys);

    /// <inheritdoc />
    public bool Append(Op op) => Append(op, ColabOpMeta.Default);

    /// <summary>
    /// Durably append an op with its seam-level row metadata. The `colab_op` INSERT is the durable
    /// commit point; `ON CONFLICT DO NOTHING` + the in-memory dot set make a replay a no-op (FR-005).
    /// Returns false when the dot was already committed (here or by the lock-step Python writer).
    /// </summary>
    public bool Append(Op op, ColabOpMeta meta)
    {
        if (_byDot.ContainsKey(op.Id)) return false;

        bool inserted = _db.InsertOp(ToRow(op, meta));

        // Reflect the op in-memory either way: if the DB already held the dot (a concurrent lock-step
        // writer), the op set still converges — but only a genuinely-new insert counts as applied.
        _byDot[op.Id] = op;
        _commitOrder.Add(op);
        return inserted;
    }

    /// <inheritdoc />
    public int Merge(IEnumerable<Op> ops)
    {
        int applied = 0;
        foreach (var op in ops)
            if (Append(op)) applied++;
        return applied;
    }

    /// <summary>Ops belonging to one box (the per-box projection input, 048 C1).</summary>
    public IReadOnlyList<Op> OpsForBox(string box) => _commitOrder.Where(o => o.Box == box).ToList();

    /// <summary>
    /// Publish the current delta-Merkle root for a (box, peer) frontier into `colab_box_merkle` —
    /// the anti-entropy reconciliation cursor (derived, additive).
    /// </summary>
    public byte[] PublishBoxMerkle(string box, string peer)
    {
        byte[] root = MerkleTree.Build(OpsForBox(box)).Root;
        _db.UpsertBoxMerkle(box, peer, root);
        return root;
    }

    /// <summary>Publish the box creator's root PUBLIC key into `colab_box_root` (D7/FR-015; publish-once).</summary>
    public void PublishBoxRoot(string box, string creatorHost, byte[] rootPubkey) =>
        _db.UpsertBoxRoot(box, creatorHost, rootPubkey);

    // ---------------------------------------------------------------- row mapping

    private static ColabOpRow ToRow(Op op, ColabOpMeta meta)
    {
        string depsJson = JsonSerializer.Serialize(op.Deps.Select(d => d.ToString()));
        return new ColabOpRow(
            Peer: op.Id.PeerName,
            Counter: op.Id.Counter,
            Box: op.Box,
            MessageId: meta.MessageId ?? op.Id.ToString(),
            OpType: meta.OpType,
            MsgType: meta.MsgType,
            FromHost: meta.FromHost ?? op.Id.PeerName,
            ToHost: meta.ToHost,
            PredHash: op.PredHash,
            DepsJson: depsJson,
            PayloadJson: BuildPayloadJson(op, meta.PayloadJson),
            MacaroonFp: meta.MacaroonFp);
    }

    /// <summary>Canonical op bytes (base64 `op_b64`) merged over any caller-supplied payload fields.</summary>
    private static string BuildPayloadJson(Op op, string? fieldsJson)
    {
        JsonObject payload;
        if (fieldsJson is null)
            payload = new JsonObject();
        else
            payload = JsonNode.Parse(fieldsJson) as JsonObject
                ?? throw new CrdtMsgException("colab_op payload fields must be a JSON object");
        payload[OpB64Key] = Convert.ToBase64String(OpCodec.Encode(op));
        return payload.ToJsonString();
    }

    private void Recover()
    {
        foreach (var row in _db.LoadOps())
        {
            Op op = RowToOp(row);
            if (_byDot.TryAdd(op.Id, op))
                _commitOrder.Add(op);
        }
    }

    /// <summary>
    /// Reconstruct an op from its row. A C#-written row carries the canonical bytes (`op_b64`) —
    /// decoded byte-identically and cross-checked against the row's dot (loud-fail on corruption, the
    /// OpWal recovery discipline). A Python-written row (no `op_b64`) is reconstructed from the row
    /// columns with the payload JSON text as the opaque op body.
    /// </summary>
    private static Op RowToOp(ColabOpRow row)
    {
        var dot = new Dot(row.Peer, row.Counter);

        string? opB64 = null;
        if (JsonNode.Parse(row.PayloadJson) is JsonObject payload &&
            payload.TryGetPropertyValue(OpB64Key, out var b64Node) && b64Node is not null)
            opB64 = b64Node.GetValue<string>();

        if (opB64 is not null)
        {
            Op op = OpCodec.Decode(Convert.FromBase64String(opB64));
            if (!op.Id.Equals(dot) || op.Box != row.Box)
                throw new CrdtMsgException(
                    $"colab_op corruption: row ({dot}, box '{row.Box}') disagrees with its encoded op ({op.Id}, box '{op.Box}')");
            return op;
        }

        // Lock-step Python-authored row: rebuild the op shape from the columns.
        var deps = (JsonSerializer.Deserialize<List<string>>(row.DepsJson) ?? new List<string>())
            .Select(Dot.Parse).ToList();
        return new Op(dot, deps, row.PredHash, Encoding.UTF8.GetBytes(row.PayloadJson), row.Box);
    }
}
