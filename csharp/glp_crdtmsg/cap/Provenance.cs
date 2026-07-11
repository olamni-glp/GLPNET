// Durable provenance records incl. refusals (feature 041-crdtmsg-mvp, T042).
//
// Contract C19 / FR-035 / SC-006: 100% of operations INCLUDING refusals produce a durable
//   {peer, target, timestamps, sha256, outcome ∈ closed-enum} record keyed to authenticated identity.
//   A refusal is a distinct, recorded outcome — NEVER a silent drop. Timestamps are supplied by the
//   caller (deterministic + testable); the log is append-only.

namespace GlpRuntime.CrdtMsg.Cap;

/// <summary>The closed outcome enum (data-model §10). Refusals/drops are first-class, recorded values.</summary>
public enum ProvenanceOutcome
{
    Applied,
    Refused,        // capability/signature verification failed (fail-closed)
    DroppedNoRoute,
    Malformed,
    OverCapacity,
}

public sealed record ProvenanceRecord(
    string Peer,
    string Target,
    DateTimeOffset Timestamp,
    string Sha256Hex,
    ProvenanceOutcome Outcome);

/// <summary>Append-only provenance log. Every routed action (incl. every refusal) appends exactly one
/// row. Thread-safe (feature 050 review-fix): in a mesh, multiple per-link receive-loop threads call
/// <see cref="Record"/> on the one shared log (via <c>MacaroonLinkGate.VerifyInbound</c>), so the
/// backing list is guarded by a lock and readers return a snapshot — otherwise concurrent
/// <c>List.Add</c>/enumeration could lose or corrupt the "exactly one row per action" guarantee.</summary>
public sealed class ProvenanceLog
{
    private readonly List<ProvenanceRecord> _records = new();
    private readonly object _gate = new();

    /// <summary>A point-in-time snapshot of the recorded rows (safe to enumerate off-thread).</summary>
    public IReadOnlyList<ProvenanceRecord> Records
    {
        get { lock (_gate) return _records.ToArray(); }
    }

    public ProvenanceRecord Record(string peer, string target, DateTimeOffset at, string sha256Hex, ProvenanceOutcome outcome)
    {
        var rec = new ProvenanceRecord(peer, target, at, sha256Hex, outcome);
        lock (_gate) _records.Add(rec);
        return rec;
    }

    /// <summary>All recorded refusals — provably never silent (SC-006). Snapshot under the lock.</summary>
    public IEnumerable<ProvenanceRecord> Refusals
    {
        get { lock (_gate) return _records.Where(r => r.Outcome == ProvenanceOutcome.Refused).ToArray(); }
    }
}
