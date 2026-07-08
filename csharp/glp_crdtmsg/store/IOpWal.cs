// The op-WAL store seam (048 bk-colab-yngenios-transport, T012).
//
// Extracted from the concrete file-backed OpWal so a second durable backend (the PGlite-backed
// PgliteOpWal, 048 D1/K1 — one journal in PGlite as the system of record) is a drop-in behind the
// same seam. Everything above the store (Projection, MerkleTree/AntiEntropy, MeshNode) consumes
// THIS contract: a grow-only, dot-keyed, idempotent op set with a stable commit order.

using GlpRuntime.CrdtMsg.Crdt;

namespace GlpRuntime.CrdtMsg.Store;

/// <summary>
/// The append-only, dot-keyed op journal seam. Append is idempotent by dot (FR-005/FR-015: a
/// re-delivered/replayed op is a no-op); ops are never mutated or dropped; live state is a
/// rebuildable projection over <see cref="Ops"/> (048 FR-013, SC-008).
/// </summary>
public interface IOpWal
{
    /// <summary>Ops in commit order (append order); a rebuildable projection folds these.</summary>
    IReadOnlyList<Op> Ops { get; }

    /// <summary>Count of distinct committed ops.</summary>
    int Count { get; }

    /// <summary>True if this op's dot is already committed.</summary>
    bool Contains(Dot dot);

    /// <summary>The committed dot-set — the seed for Merkle anti-entropy.</summary>
    IReadOnlySet<Dot> DotSet();

    /// <summary>Durably append an op. Idempotent by dot (returns false if already present).</summary>
    bool Append(Op op);

    /// <summary>Merge a batch (anti-entropy delta) — idempotent; returns the count newly applied.</summary>
    int Merge(IEnumerable<Op> ops);
}
