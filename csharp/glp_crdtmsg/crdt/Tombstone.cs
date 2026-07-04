// Semantic tombstone + observed-remove set (feature 041-crdtmsg-mvp, T030).
//
// Contract C8 / data-model §4a: a tombstone is a first-class op {removed_id (dot), causal_context,
// reason} with OBSERVED-REMOVE semantics — an unobserved concurrent add survives. The rich-text
// seq_delete / mark_remove ops (RichTextDoc) are dot-targeted specializations of this (each element /
// mark has a unique dot, so targeting the dot is inherently observed-remove). The general OR-Set below
// demonstrates the value-keyed case the C8 test names: concurrent add + remove of the "same element".

namespace GlpRuntime.CrdtMsg.Crdt;

/// <summary>The general tombstone op payload shape (data-model §4a).</summary>
public sealed record Tombstone(Dot RemovedId, IReadOnlyList<Dot> CausalContext, string Reason);

/// <summary>
/// Observed-remove set keyed by value. Each add is tagged with the adding op's dot; a remove records the
/// set of add-dots it OBSERVED. A value is present iff it has an add-dot not covered by any remove — so a
/// concurrent add (a fresh dot the remove never saw) survives (C8). Grow-only maps of dots ⇒ join = union
/// ⇒ order-independent convergence.
/// </summary>
public sealed class ObservedRemoveSet<T> where T : notnull
{
    private readonly Dictionary<T, HashSet<Dot>> _adds = new();
    private readonly Dictionary<T, HashSet<Dot>> _removedObserved = new();

    public void Add(T value, Dot dot) => Bucket(_adds, value).Add(dot);

    /// <summary>Remove <paramref name="value"/>, observing the given add-dots (its causal context).</summary>
    public void Remove(T value, IEnumerable<Dot> observedAddDots)
    {
        var r = Bucket(_removedObserved, value);
        foreach (var d in observedAddDots) r.Add(d);
    }

    public bool Contains(T value)
    {
        if (!_adds.TryGetValue(value, out var adds)) return false;
        var removed = _removedObserved.TryGetValue(value, out var r) ? r : null;
        return adds.Any(d => removed is null || !removed.Contains(d));
    }

    /// <summary>The observed add-dots for a value — the causal context to attach to a remove.</summary>
    public IReadOnlyCollection<Dot> ObservedAdds(T value) =>
        _adds.TryGetValue(value, out var s) ? s : (IReadOnlyCollection<Dot>)Array.Empty<Dot>();

    public IEnumerable<T> Values => _adds.Keys.Where(Contains);

    /// <summary>Join (lattice merge) — union of adds and of observed-removes. Idempotent, commutative.</summary>
    public void Merge(ObservedRemoveSet<T> other)
    {
        foreach (var (v, dots) in other._adds) Bucket(_adds, v).UnionWith(dots);
        foreach (var (v, dots) in other._removedObserved) Bucket(_removedObserved, v).UnionWith(dots);
    }

    private static HashSet<Dot> Bucket(Dictionary<T, HashSet<Dot>> d, T key) =>
        d.TryGetValue(key, out var s) ? s : d[key] = new HashSet<Dot>();
}
