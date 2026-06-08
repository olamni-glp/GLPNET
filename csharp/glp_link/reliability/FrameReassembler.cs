namespace GlpRuntime.Link.Reliability;

/// <summary>
/// Reassembles fragmented payloads (FR-022). Feed it each <see cref="ParsedFrame"/>
/// (already CRC-validated by <see cref="FrameCodec.ParseFrame"/>); a
/// <see cref="FrameKind.Whole"/> frame yields its payload immediately, while
/// fragments are buffered by <c>MessageId</c> until the set is complete.
/// </summary>
/// <remarks>
/// Hardened against malformed/adversarial fragment streams (FR-028): inconsistent
/// metadata across a message's fragments is rejected; the number of concurrently
/// in-flight partial messages and the total buffered bytes are both bounded, so a
/// peer cannot exhaust memory by opening many partial messages or never completing
/// them. Not thread-safe: one reassembler per link, driven by that link's single
/// receive loop.
/// </remarks>
public sealed class FrameReassembler
{
    private sealed class Partial
    {
        public required uint TotalLength;
        public required ushort FragCount;
        public readonly byte[]?[] Chunks;
        public int Received;
        public long BufferedBytes;
        public Partial(ushort fragCount) => Chunks = new byte[]?[fragCount];
    }

    private readonly int _maxInFlight;
    private readonly long _maxBufferedBytes;
    private readonly Dictionary<uint, Partial> _partials = new();
    private long _totalBuffered;

    /// <param name="maxInFlightMessages">Cap on concurrently incomplete messages.</param>
    /// <param name="maxBufferedBytes">Cap on total bytes held across all partials.</param>
    public FrameReassembler(int maxInFlightMessages = 64, long maxBufferedBytes = 64L * 1024 * 1024)
    {
        _maxInFlight = maxInFlightMessages;
        _maxBufferedBytes = maxBufferedBytes;
    }

    /// <summary>
    /// Accept one frame. Returns the complete payload when this frame finishes a
    /// message (or for a whole frame), otherwise <c>null</c>. Throws
    /// <see cref="FrameException"/> on inconsistent or over-bound input.
    /// </summary>
    public byte[]? Accept(in ParsedFrame frame)
    {
        if (frame.Kind == FrameKind.Whole)
        {
            if (frame.Chunk.Length != frame.TotalLength)
                throw new FrameException($"whole frame chunk {frame.Chunk.Length} != total {frame.TotalLength}");
            return frame.Chunk;
        }

        if (!_partials.TryGetValue(frame.MessageId, out var partial))
        {
            if (_partials.Count >= _maxInFlight)
                throw new FrameException($"too many in-flight partial messages (max {_maxInFlight})");
            if (_totalBuffered + frame.TotalLength > _maxBufferedBytes)
                throw new FrameException($"reassembly buffer would exceed {_maxBufferedBytes} bytes");
            partial = new Partial(frame.FragCount) { TotalLength = frame.TotalLength, FragCount = frame.FragCount };
            _partials[frame.MessageId] = partial;
        }
        else if (partial.TotalLength != frame.TotalLength || partial.FragCount != frame.FragCount)
        {
            throw new FrameException(
                $"fragment metadata mismatch for message {frame.MessageId}: " +
                $"({frame.TotalLength},{frame.FragCount}) vs ({partial.TotalLength},{partial.FragCount})");
        }

        if (partial.Chunks[frame.FragIndex] is null)
        {
            partial.Chunks[frame.FragIndex] = frame.Chunk;
            partial.Received++;
            partial.BufferedBytes += frame.Chunk.Length;
            _totalBuffered += frame.Chunk.Length;
        }
        // A duplicate fragment (same index re-delivered) is silently ignored here;
        // payload-level dedup is the T022 sublayer's job.

        if (partial.Received < partial.FragCount)
            return null;

        var payload = new byte[partial.TotalLength];
        int offset = 0;
        for (int i = 0; i < partial.FragCount; i++)
        {
            var chunk = partial.Chunks[i]!;
            Buffer.BlockCopy(chunk, 0, payload, offset, chunk.Length);
            offset += chunk.Length;
        }
        if (offset != partial.TotalLength)
            throw new FrameException($"reassembled {offset} bytes != advertised total {partial.TotalLength}");

        _partials.Remove(frame.MessageId);
        _totalBuffered -= partial.BufferedBytes;
        return payload;
    }

    /// <summary>Number of messages currently awaiting more fragments.</summary>
    public int InFlightCount => _partials.Count;
}
