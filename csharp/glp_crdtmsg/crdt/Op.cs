// CRDT op — the store↔message seam unit (feature 041-crdtmsg-mvp).
//
// Contract C7/C10 / data-model §4: every op carries op_id = DVV dot (peer_name, counter), causal
// `deps`, a `pred_hash` (day-one hash-chain), and an opaque ground-term-encoded op body (`Payload`).
// The STORE is payload-agnostic — it keys ops by dot (idempotent, FR-015) and replays them; the op
// BODY semantics (map-set / seq-insert / seq-delete / mark-add / mark-remove / tombstone, ground-term
// + acyclic validation, CycleGuard) are layered on at T029 (US3). This minimal shared shape is what
// US2's store needs now (op_id is the store key, distinct from msg_id — T025).

using GlpRuntime.ResultCodec;
using GlpRuntime.CrdtMsg.Envelope;

namespace GlpRuntime.CrdtMsg.Crdt;

/// <summary>An op: stable dot identity + causal deps + pred-hash + opaque op-body bytes.</summary>
public sealed record Op(Dot Id, IReadOnlyList<Dot> Deps, byte[] PredHash, byte[] Payload)
{
    /// <summary>Build an op, computing its pred_hash from (id, deps) — the day-one hash-chain (C7).</summary>
    public static Op Create(Dot id, IReadOnlyList<Dot> deps, byte[] payload) =>
        new(id, deps, HashChain.PredHash(id, deps), payload);
}

/// <summary>Deterministic op serialization (WAL persistence + wire). Reuses the 038 byte conventions.</summary>
public static class OpCodec
{
    public static byte[] Encode(Op op)
    {
        var w = new ByteWriter();
        WriteDot(w, op.Id);
        VarInt.WriteU64(w, (ulong)op.Deps.Count);
        foreach (var d in op.Deps) WriteDot(w, d);
        VarInt.WriteU64(w, (ulong)op.PredHash.Length);
        w.WriteBytes(op.PredHash);
        VarInt.WriteU64(w, (ulong)op.Payload.Length);
        w.WriteBytes(op.Payload);
        return w.TakeBytes();
    }

    public static Op Decode(byte[] bytes)
    {
        var r = new ByteReader(bytes);
        Dot id = ReadDot(r);
        ulong depCount = VarInt.ReadU64(r);
        if (depCount > (ulong)r.Length)
            throw new CrdtMsgException($"op dep count {depCount} exceeds input size");
        var deps = new List<Dot>((int)depCount);
        for (ulong i = 0; i < depCount; i++) deps.Add(ReadDot(r));
        byte[] predHash = ReadLenBytes(r, "pred_hash");
        byte[] payload = ReadLenBytes(r, "payload");
        if (!r.AtEnd)
            throw new CrdtMsgException($"{r.Length - r.Position} trailing byte(s) after op");
        return new Op(id, deps, predHash, payload);
    }

    private static void WriteDot(ByteWriter w, Dot d)
    {
        w.WriteString(d.PeerName);
        VarInt.WriteU64(w, (ulong)d.Counter);
    }

    private static Dot ReadDot(ByteReader r)
    {
        string peer = r.ReadString();
        long counter = (long)VarInt.ReadU64(r);
        return new Dot(peer, counter);
    }

    private static byte[] ReadLenBytes(ByteReader r, string field)
    {
        ulong len = VarInt.ReadU64(r);
        if (len > (ulong)(r.Length - r.Position))
            throw new CrdtMsgException($"op {field} length {len} exceeds remaining bytes");
        return r.ReadBytes((int)len);
    }
}
