namespace GlpRuntime.Link.Reliability;

/// <summary>
/// Per-link inbound FIFO reconstruction + transport-level dedup (FR-020/023/053).
/// A single sequence number DETECTS disorder but does not RESTORE it, so a reorder
/// buffer is required (architecture-context.md §4.2): out-of-order frames are held
/// until the gap fills, then released in sequence order. A frame at or below the
/// delivered high-water mark, or one already buffered, is an idempotent no-op —
/// the transport-level half of at-least-once redelivery (FR-021/FR-027), upstream
/// of the global-name dedup gate in <c>mad_context</c>.
/// </summary>
/// <remarks>
/// One instance per link per direction, driven by that link's single receive loop
/// (not thread-safe). The reorder buffer is bounded (FR-028): a peer cannot force
/// unbounded memory by withholding one early frame while flooding later ones.
/// </remarks>
public sealed class InboundOrdering
{
    private uint _nextExpected;
    private readonly SortedDictionary<uint, byte[]> _buffer = new();
    private readonly int _maxBufferedFrames;

    /// <param name="start">The first expected sequence number (must match the peer's sequencer start).</param>
    /// <param name="maxBufferedFrames">Cap on out-of-order frames held awaiting a gap.</param>
    public InboundOrdering(uint start = 0, int maxBufferedFrames = 256)
    {
        _nextExpected = start;
        _maxBufferedFrames = maxBufferedFrames;
    }

    /// <summary>The next in-order sequence number awaited.</summary>
    public uint NextExpected => _nextExpected;

    /// <summary>Out-of-order frames currently buffered awaiting a gap.</summary>
    public int BufferedCount => _buffer.Count;

    /// <summary>
    /// Accept one sequenced payload. Returns the payloads now deliverable IN ORDER
    /// (empty when the input was a duplicate/old frame, or a future frame buffered
    /// awaiting the gap). Throws <see cref="FrameException"/> if buffering would
    /// exceed the bound.
    /// </summary>
    public IReadOnlyList<byte[]> Accept(uint seq, byte[] payload)
    {
        // Already delivered (seq < high-water) → idempotent no-op.
        if (SeqLess(seq, _nextExpected))
            return Array.Empty<byte[]>();

        if (seq == _nextExpected)
        {
            var ready = new List<byte[]> { payload };
            _nextExpected++;
            // Drain any contiguous run that was buffered ahead of this gap.
            while (_buffer.TryGetValue(_nextExpected, out var next))
            {
                ready.Add(next);
                _buffer.Remove(_nextExpected);
                _nextExpected++;
            }
            return ready;
        }

        // Future frame (seq > nextExpected): buffer it, deduping a re-delivered future.
        if (_buffer.ContainsKey(seq))
            return Array.Empty<byte[]>();
        if (_buffer.Count >= _maxBufferedFrames)
            throw new FrameException($"reorder buffer full (max {_maxBufferedFrames}); missing seq {_nextExpected}");
        _buffer[seq] = payload;
        return Array.Empty<byte[]>();
    }

    // Sequence comparison. Plain unsigned compare (no wraparound within a session;
    // a reconnect creates a fresh InboundOrdering, see LinkSequencer remarks).
    private static bool SeqLess(uint a, uint b) => a < b;
}
