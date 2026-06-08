namespace GlpRuntime.Link.Reliability;

/// <summary>The fencing decision for a writer attempting to bind a global name.</summary>
public enum FenceVerdict
{
    /// <summary>This writer holds the highest epoch seen for the name; it may bind.</summary>
    Admit,

    /// <summary>
    /// A newer (higher-epoch) writer has already taken over this name; this writer
    /// is stale and is fenced out. The link layer surfaces <c>permFail</c> for it —
    /// never a silent overwrite, never a <c>StateError</c> (FR-047).
    /// </summary>
    Fenced,
}

/// <summary>
/// Split-brain defense via fencing tokens (FR-047, SC-011). Each link
/// establishment carries a monotonically increasing <c>epoch</c> (the fencing
/// token, from <see cref="EpochAllocator"/>). This registry — the "resource" in
/// the Kleppmann fencing model — remembers the highest epoch admitted per global
/// name and FENCES any later write that carries a lower epoch. So when a
/// partitioned writer resumes after a newer writer has taken over, its stale
/// (lower-epoch) binding is rejected rather than silently overwriting the live
/// one; exactly one writer wins.
/// </summary>
/// <remarks>
/// This is IN ADDITION TO global-name idempotency (the <c>mad_context</c> dedup
/// gate, T002): idempotency absorbs a re-delivered SAME binding; fencing rejects a
/// CONFLICTING stale one. In the real topology the stale writer always holds the
/// lower epoch (the higher epoch only exists after takeover, by which time the
/// stale end is partitioned away), so the highest-epoch-wins rule fences exactly
/// the stale writer. One registry per instance; not thread-safe.
/// </remarks>
public sealed class FencingRegistry
{
    private readonly Dictionary<string, ulong> _highestEpoch = new();

    /// <summary>
    /// Decide whether a writer carrying <paramref name="epoch"/> may bind
    /// <paramref name="globalName"/>. Admits the first writer and any writer whose
    /// epoch is ≥ the highest admitted so far (an equal epoch is the same/idempotent
    /// writer; a higher epoch is a legitimate takeover). Fences a lower epoch.
    /// </summary>
    public FenceVerdict Admit(string globalName, ulong epoch)
    {
        if (_highestEpoch.TryGetValue(globalName, out var highest) && epoch < highest)
            return FenceVerdict.Fenced;
        _highestEpoch[globalName] = epoch;
        return FenceVerdict.Admit;
    }

    /// <summary>The highest epoch admitted for a name, or <c>null</c> if none yet.</summary>
    public ulong? HighestEpochFor(string globalName) =>
        _highestEpoch.TryGetValue(globalName, out var hi) ? hi : null;

    /// <summary>
    /// Drop all fencing state for a name (distributed GC after the link's writers
    /// are reclaimed, T024). After this the name is fresh again.
    /// </summary>
    public void Forget(string globalName) => _highestEpoch.Remove(globalName);

    /// <summary>Number of global names currently tracked.</summary>
    public int TrackedCount => _highestEpoch.Count;
}

/// <summary>
/// Monotone source of per-establishment fencing epochs (FR-047). Each new link
/// establishment (or writer takeover) draws the next epoch; higher always means
/// newer. One allocator per instance covers all its outbound establishments.
/// </summary>
public sealed class EpochAllocator
{
    private ulong _next;

    public EpochAllocator(ulong start = 1) => _next = start;

    /// <summary>The next establishment epoch, advancing the counter.</summary>
    public ulong Next() => _next++;
}
