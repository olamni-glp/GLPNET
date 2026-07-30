// ISnapshotStore — the durable store contract + the two-backend composition
// (T020/T021; contracts/snapshot-store.md).
//
// Backend rules (FR-012/FR-013):
//   - monotonic seq per engine identity, SHARED across backends: next = max(both)+1;
//   - durable-then-visible: a snapshot is listable only when blob AND manifest
//     are both durably written — a torn write is never returned;
//   - Latest() = highest COMPLETE seq across both backends, preferring the
//     primary on a tie;
//   - engaging the fallback is reported LOUDLY to the requester and the log
//     (US2/AS-4) — never a silent degradation.

using GlpRuntime.EngineHost.Snapshot;

namespace GlpRuntime.EngineHost.Store;

/// <summary>Loud-fail exception for store-level errors that are not backend unavailability.</summary>
public sealed class SnapshotStoreException : Exception
{
    public SnapshotStoreException(string message) : base(message) { }
    public SnapshotStoreException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>One listed snapshot (complete writes only).</summary>
public sealed record SnapshotMeta(ulong Seq, long CreatedUtcMs, long Size, int FormatVersion);

/// <summary>
/// One storage backend (primary PGLite or fallback file). All operations are
/// scoped to one engine identity; a throwing backend is treated as UNAVAILABLE
/// by the composing <see cref="SnapshotStore"/> (fallback engagement), except
/// for <see cref="SnapshotStoreException"/> which is a real error and propagates.
/// </summary>
public interface ISnapshotBackend
{
    /// <summary>Backend name for degradation reports ("pglite" / "file").</summary>
    string Name { get; }

    /// <summary>Highest complete seq, or 0 when no snapshot exists.</summary>
    ulong MaxSeq();

    /// <summary>Durably write one complete snapshot (durable-then-visible).</summary>
    void Write(ulong seq, long createdUtcMs, int formatVersion, byte[] blob);

    /// <summary>Highest complete snapshot, or null.</summary>
    (ulong Seq, byte[] Blob)? Latest();

    /// <summary>Exact complete snapshot, or null.</summary>
    byte[]? BySeq(ulong seq);

    /// <summary>All complete snapshots, ascending by seq.</summary>
    IReadOnlyList<SnapshotMeta> List();
}

/// <summary>
/// The contract-level store: primary (PGLite, optional) + fallback (file),
/// shared monotonic seq namespace, loud fallback engagement.
/// </summary>
public sealed class SnapshotStore
{
    private readonly ISnapshotBackend? _primary;
    private readonly ISnapshotBackend _fallback;
    private readonly Action<string> _report;

    /// <param name="primary">The PGLite backend, or null when no primary is configured
    /// (which is itself a loud-degradation condition on every write).</param>
    /// <param name="fallback">The file backend — always present.</param>
    /// <param name="report">Loud-degradation sink: the message goes to the log AND is
    /// surfaced to the requester by the dispatcher (US2/AS-4).</param>
    public SnapshotStore(ISnapshotBackend? primary, ISnapshotBackend fallback, Action<string> report)
    {
        _primary = primary;
        _fallback = fallback;
        _report = report;
    }

    /// <summary>Messages reported during the most recent Write (for the requester's ACK).</summary>
    public IReadOnlyList<string> LastWriteDegradations => _lastWriteDegradations;
    private readonly List<string> _lastWriteDegradations = new();

    /// <summary>
    /// The seq the NEXT write will carry: max complete seq across both backends
    /// plus one (FR-012). The caller encodes this seq into the blob header, then
    /// calls <see cref="Write"/> with the same value.
    /// </summary>
    public ulong NextSeq()
    {
        ulong primaryMax = 0;
        if (_primary != null)
        {
            try { primaryMax = _primary.MaxSeq(); }
            catch (SnapshotStoreException) { throw; }
            catch (Exception ex)
            {
                // Loud at RESERVATION time, not just at write: seqs minted while the
                // primary is unreachable may sit below seqs the primary already
                // holds — the fork is detected loudly on the next read (Latest).
                primaryMax = 0;
                _report($"primary snapshot store '{_primary.Name}' unavailable while reserving " +
                        $"the next seq ({ex.Message}) — seq derives from the file fallback only");
            }
        }
        ulong fallbackMax = _fallback.MaxSeq();
        return Math.Max(primaryMax, fallbackMax) + 1;
    }

    /// <summary>
    /// Durably write one snapshot. The blob's encoded seq must equal
    /// <paramref name="seq"/> (the dispatcher obtained it from <see cref="NextSeq"/>
    /// just before capture — single-threaded, so no interleaving). Primary first;
    /// on unavailability the fallback receives the snapshot and the degradation
    /// is reported loudly (US2/AS-4). Both backends down ⇒ SnapshotStoreException.
    /// </summary>
    public void Write(SnapshotBlob blob, byte[] encoded, ulong seq)
    {
        if (blob.Seq != seq)
            throw new SnapshotStoreException(
                $"blob seq {blob.Seq} does not match the reserved seq {seq}");

        _lastWriteDegradations.Clear();

        if (_primary is null)
        {
            Degrade("no primary snapshot store is configured (GLP_SNAPSHOT_PG_CONN unset) — " +
                    "writing to the file fallback");
        }
        else
        {
            try
            {
                _primary.Write(seq, blob.CreatedUtcMs, blob.FormatVersion, encoded);
                return;
            }
            catch (SnapshotStoreException)
            {
                throw; // a real store error (e.g. seq collision), not unavailability
            }
            catch (Exception ex)
            {
                Degrade($"primary snapshot store '{_primary.Name}' unavailable ({ex.Message}) — " +
                        "writing to the file fallback");
            }
        }

        try
        {
            _fallback.Write(seq, blob.CreatedUtcMs, blob.FormatVersion, encoded);
        }
        catch (SnapshotStoreException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new SnapshotStoreException(
                $"both snapshot backends failed — fallback '{_fallback.Name}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Latest complete snapshot across both backends (primary preferred on a
    /// tie). Refuses LOUDLY on a forked seq namespace — a higher-seq snapshot
    /// that is OLDER by creation time than the other backend's latest means seqs
    /// were minted during a primary outage below seqs the primary already held;
    /// silently picking either side could resurrect stale state (codexreview
    /// 20260730T070051Z seq-regression-stale-restore). The operator reconciles.
    /// </summary>
    public (ulong Seq, byte[] Blob)? Latest()
    {
        IReadOnlyList<SnapshotMeta>? primaryList = null;
        (ulong, byte[])? primary = null;
        if (_primary != null)
        {
            try
            {
                primaryList = _primary.List();
                primary = _primary.Latest();
            }
            catch (SnapshotStoreException) { throw; }
            catch (Exception ex)
            {
                _report($"primary snapshot store '{_primary.Name}' unavailable on read ({ex.Message}) — " +
                        "consulting the file fallback only");
            }
        }
        var fallback = _fallback.Latest();

        if (primary is null) return fallback;
        if (fallback is null) return primary;

        var pTop = primaryList?.Count > 0 ? primaryList[^1] : null;
        var fTop = _fallback.List() is { Count: > 0 } fl ? fl[^1] : null;
        if (pTop != null && fTop != null &&
            ((pTop.Seq > fTop.Seq && pTop.CreatedUtcMs < fTop.CreatedUtcMs) ||
             (fTop.Seq > pTop.Seq && fTop.CreatedUtcMs < pTop.CreatedUtcMs)))
        {
            throw new SnapshotStoreException(
                $"snapshot seq namespace FORKED: primary latest seq={pTop.Seq} " +
                $"(created {pTop.CreatedUtcMs}) vs fallback latest seq={fTop.Seq} " +
                $"(created {fTop.CreatedUtcMs}) — the higher seq is the older snapshot. " +
                "Refusing to pick a side silently; reconcile the stores (likely seqs " +
                "minted on the fallback during a primary outage).");
        }

        return fallback.Value.Seq > primary.Value.Item1 ? fallback : primary;
    }

    /// <summary>Exact complete snapshot by seq (primary first, then fallback).</summary>
    public byte[]? BySeq(ulong seq)
    {
        if (_primary != null)
        {
            try
            {
                var blob = _primary.BySeq(seq);
                if (blob != null) return blob;
            }
            catch (SnapshotStoreException) { throw; }
            catch (Exception ex)
            {
                _report($"primary snapshot store '{_primary.Name}' unavailable on read ({ex.Message}) — " +
                        "consulting the file fallback only");
            }
        }
        return _fallback.BySeq(seq);
    }

    /// <summary>All complete snapshots across both backends, deduplicated by seq, ascending.</summary>
    public IReadOnlyList<SnapshotMeta> List()
    {
        var bySeq = new SortedDictionary<ulong, SnapshotMeta>();
        foreach (var m in _fallback.List())
            bySeq[m.Seq] = m;
        if (_primary != null)
        {
            try
            {
                foreach (var m in _primary.List())
                    bySeq[m.Seq] = m; // primary preferred on a tie
            }
            catch (SnapshotStoreException) { throw; }
            catch (Exception ex)
            {
                _report($"primary snapshot store '{_primary.Name}' unavailable on list ({ex.Message})");
            }
        }
        return bySeq.Values.ToList();
    }

    private void Degrade(string message)
    {
        _lastWriteDegradations.Add(message);
        _report(message);
    }
}
