// Delivery over the shipped reliability substrate (feature 041-crdtmsg-mvp, T031).
//
// Contract C7 / research: op delivery rides a reliability window aligned with the shipped glp_link
// reliability sublayer — monotone per-link seq, bounded-reorder buffering (window N=8), idempotent
// inbound (duplicates suppressed), and single-winner fencing (a superseded/fenced sender is rejected).
// Final idempotence is also guaranteed downstream by the store (Append is idempotent by dot) — this
// window handles ORDERING/dedup at the link boundary; the store handles set-idempotence.

namespace GlpRuntime.CrdtMsg.Crdt;

/// <summary>Per-link ordered-delivery window with bounded reorder + fencing.</summary>
public sealed class DeliveryWindow<T>
{
    /// <summary>Reliability window (matches the shipped N=8, FrameCodec reliability).</summary>
    public const int WindowSize = 8;

    private readonly SortedDictionary<long, T> _buffer = new();
    private long _nextExpected;
    private long _epoch;

    public DeliveryWindow(long startSeq = 0) => _nextExpected = startSeq;

    /// <summary>Current fencing epoch (single-winner token).</summary>
    public long Epoch => _epoch;

    /// <summary>
    /// Offer an inbound (seq, item) at fencing <paramref name="epoch"/>. Returns the items now
    /// deliverable IN ORDER (releasing any buffered contiguous successors). A duplicate (seq already
    /// delivered), an out-of-window seq, or a fenced (stale-epoch) offer yields an empty result.
    /// A higher epoch adopts the new single winner.
    /// </summary>
    public IReadOnlyList<T> Offer(long seq, T item, long epoch = 0)
    {
        if (epoch < _epoch) return Array.Empty<T>();      // fenced: superseded sender
        if (epoch > _epoch) _epoch = epoch;               // new single winner

        if (seq < _nextExpected) return Array.Empty<T>();                 // duplicate / already delivered
        if (seq >= _nextExpected + WindowSize) return Array.Empty<T>();   // beyond bounded reorder window
        _buffer[seq] = item;                                             // idempotent buffer (dedupe by seq)

        var released = new List<T>();
        while (_buffer.TryGetValue(_nextExpected, out var next))
        {
            released.Add(next);
            _buffer.Remove(_nextExpected);
            _nextExpected++;
        }
        return released;
    }
}
