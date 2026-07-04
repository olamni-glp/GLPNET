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

/// <summary>Append-only provenance log. Every routed action (incl. every refusal) appends exactly one row.</summary>
public sealed class ProvenanceLog
{
    private readonly List<ProvenanceRecord> _records = new();

    public IReadOnlyList<ProvenanceRecord> Records => _records;

    public ProvenanceRecord Record(string peer, string target, DateTimeOffset at, string sha256Hex, ProvenanceOutcome outcome)
    {
        var rec = new ProvenanceRecord(peer, target, at, sha256Hex, outcome);
        _records.Add(rec);
        return rec;
    }

    /// <summary>All recorded refusals — provably never silent (SC-006).</summary>
    public IEnumerable<ProvenanceRecord> Refusals => _records.Where(r => r.Outcome == ProvenanceOutcome.Refused);
}
