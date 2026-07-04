// Peritext formatting spans (feature 041-crdtmsg-mvp, T033).
//
// Contract C12 / research R7: marks {mark_id, type, start_anchor, end_anchor, value} anchor to STABLE
// positions — an anchor is (elem_id dot, side ∈ before/after), so it survives concurrent edits to the
// text. Marks of UNKNOWN type are preserved verbatim through convergence AND lossless transcode across
// all four surfaces (SC-013) — this store keeps every mark regardless of type; nothing is dropped for
// being unrecognised. Overlapping concurrent spans simply coexist (a set CRDT keyed by mark_id).

using GlpRuntime.CrdtMsg.Crdt;

namespace GlpRuntime.CrdtMsg.Crdt.RichText;

/// <summary>An anchor to a stable position: before/after a sequence element. elem_id = (dot, side).</summary>
public enum AnchorSide { Before, After }

public sealed record Anchor(Dot ElemId, AnchorSide Side);

/// <summary>A formatting mark. <c>Type</c> may be unknown to this receiver; it is kept verbatim.</summary>
public sealed record Mark(Dot MarkId, string Type, Anchor Start, Anchor End, string Value);

/// <summary>Observed-remove mark set keyed by mark_id (each add has a unique dot).</summary>
public sealed class MarkSet
{
    private readonly Dictionary<Dot, Mark> _marks = new();
    private readonly HashSet<Dot> _removed = new();

    /// <summary>Add a mark. Idempotent by mark_id.</summary>
    public bool Add(Mark mark)
    {
        if (_marks.ContainsKey(mark.MarkId)) return false;
        _marks[mark.MarkId] = mark;
        return true;
    }

    /// <summary>Observed-remove a mark by id (tolerant of remove-before-add).</summary>
    public bool Remove(Dot markId) => _removed.Add(markId);

    /// <summary>Active marks (added, not removed) — INCLUDING unknown types, verbatim.</summary>
    public IEnumerable<Mark> Active =>
        _marks.Values.Where(m => !_removed.Contains(m.MarkId)).OrderBy(m => m.MarkId);

    /// <summary>Active marks whose type this receiver does not recognise — kept, never dropped (SC-013).</summary>
    public IEnumerable<Mark> UnknownActive(IReadOnlySet<string> knownTypes) =>
        Active.Where(m => !knownTypes.Contains(m.Type));
}
