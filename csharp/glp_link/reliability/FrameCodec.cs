using System.Buffers.Binary;

namespace GlpRuntime.Link.Reliability;

/// <summary>The on-wire frame kind.</summary>
public enum FrameKind : byte
{
    /// <summary>A whole payload in one frame (fits under MTU).</summary>
    Whole = 0,

    /// <summary>One fragment of a payload split across several frames (FR-022).</summary>
    Fragment = 1,
}

/// <summary>A parsed, CRC-validated frame: its header fields plus its chunk bytes.</summary>
/// <param name="Kind">Whole or Fragment.</param>
/// <param name="MessageId">Groups the fragments of one logical payload.</param>
/// <param name="TotalLength">Full payload byte length (identical across a message's fragments).</param>
/// <param name="FragIndex">0-based fragment position (0 for <see cref="FrameKind.Whole"/>).</param>
/// <param name="FragCount">Number of fragments (1 for <see cref="FrameKind.Whole"/>).</param>
/// <param name="Chunk">This frame's payload slice (already CRC-verified).</param>
public readonly record struct ParsedFrame(
    FrameKind Kind, uint MessageId, uint TotalLength, ushort FragIndex, ushort FragCount, byte[] Chunk);

/// <summary>
/// The wire-format frame codec (T021, FR-022). Wraps the opaque
/// <c>PayloadSerializer</c> blob with what corpus 13 §6 flags as ABSENT today: a
/// version byte (forward-compat), an outer length + CRC-32 (the Dart path relied
/// on in-process object integrity), and fragmentation/reassembly for under-MTU
/// leaves (CoAP ~1 KB, BLE GATT 20 B). Big-endian throughout to match the
/// serializer's endian-independent TLV and so the Dart mirror is byte-identical
/// (FR-060/061).
/// </summary>
/// <remarks>
/// The <c>MessageId</c> is supplied by the caller (the link's outbound counter,
/// related to the T022 sequence number), NOT generated here — encoding is
/// therefore deterministic and replayable for the seeded loopback/parity tests.
/// </remarks>
public static class FrameCodec
{
    /// <summary>Current wire version. A frame with any other version byte is rejected.</summary>
    public const byte Version = 0x01;

    /// <summary>Fixed header size in bytes (see field layout in <see cref="Encode"/>).</summary>
    public const int HeaderSize = 22;

    /// <summary>
    /// Upper bound on a single payload (FR-028 deserializer hardening): a frame
    /// advertising a larger <c>TotalLength</c> is rejected before any allocation,
    /// so a forged length cannot exhaust memory.
    /// </summary>
    public const int MaxPayloadBytes = 64 * 1024 * 1024;

    // Header field offsets (big-endian):
    //   0      Version   (1)
    //   1      Kind      (1)
    //   2..5   MessageId (u32)
    //   6..9   TotalLen  (u32)
    //   10..11 FragIndex (u16)
    //   12..13 FragCount (u16)
    //   14..17 ChunkCrc  (u32)  CRC-32 of the chunk bytes
    //   18..21 ChunkLen  (u32)
    //   22..   chunk
    private const int OffKind = 1;
    private const int OffMessageId = 2;
    private const int OffTotalLen = 6;
    private const int OffFragIndex = 10;
    private const int OffFragCount = 12;
    private const int OffChunkCrc = 14;
    private const int OffChunkLen = 18;

    /// <summary>
    /// Encode <paramref name="payload"/> into one or more self-delimiting frames.
    /// A single <see cref="FrameKind.Whole"/> frame when it fits under
    /// <paramref name="maxFrameBytes"/> (or when that is <c>null</c>), otherwise
    /// <see cref="FrameKind.Fragment"/> frames each ≤ the MTU.
    /// </summary>
    /// <param name="payload">The opaque serialized payload.</param>
    /// <param name="messageId">Caller-supplied id grouping this payload's frames.</param>
    /// <param name="maxFrameBytes">MTU including header, or <c>null</c> for unbounded.</param>
    public static IReadOnlyList<byte[]> Encode(ReadOnlySpan<byte> payload, uint messageId, int? maxFrameBytes = null)
    {
        if (payload.Length > MaxPayloadBytes)
            throw new FrameException($"payload {payload.Length} exceeds MaxPayloadBytes {MaxPayloadBytes}");

        int total = payload.Length;

        bool fitsWhole = maxFrameBytes is null || total + HeaderSize <= maxFrameBytes;
        if (fitsWhole)
            return new[] { BuildFrame(FrameKind.Whole, messageId, (uint)total, 0, 1, payload) };

        int maxChunk = maxFrameBytes!.Value - HeaderSize;
        if (maxChunk <= 0)
            throw new FrameException($"maxFrameBytes {maxFrameBytes} too small for {HeaderSize}-byte header");

        long fragCountL = total == 0 ? 1 : (total + maxChunk - 1L) / maxChunk;
        if (fragCountL > ushort.MaxValue)
            throw new FrameException($"payload {total} needs {fragCountL} fragments under MTU {maxFrameBytes} (max {ushort.MaxValue})");
        ushort fragCount = (ushort)fragCountL;

        var frames = new List<byte[]>(fragCount);
        for (ushort i = 0; i < fragCount; i++)
        {
            int start = i * maxChunk;
            int len = Math.Min(maxChunk, total - start);
            frames.Add(BuildFrame(FrameKind.Fragment, messageId, (uint)total, i, fragCount, payload.Slice(start, len)));
        }
        return frames;
    }

    private static byte[] BuildFrame(FrameKind kind, uint messageId, uint totalLen, ushort fragIndex, ushort fragCount, ReadOnlySpan<byte> chunk)
    {
        var frame = new byte[HeaderSize + chunk.Length];
        var h = frame.AsSpan();
        h[0] = Version;
        h[OffKind] = (byte)kind;
        BinaryPrimitives.WriteUInt32BigEndian(h.Slice(OffMessageId), messageId);
        BinaryPrimitives.WriteUInt32BigEndian(h.Slice(OffTotalLen), totalLen);
        BinaryPrimitives.WriteUInt16BigEndian(h.Slice(OffFragIndex), fragIndex);
        BinaryPrimitives.WriteUInt16BigEndian(h.Slice(OffFragCount), fragCount);
        BinaryPrimitives.WriteUInt32BigEndian(h.Slice(OffChunkCrc), Crc32.Compute(chunk));
        BinaryPrimitives.WriteUInt32BigEndian(h.Slice(OffChunkLen), (uint)chunk.Length);
        chunk.CopyTo(h.Slice(HeaderSize));
        return frame;
    }

    /// <summary>
    /// Parse and validate one frame: version byte, CRC-32 over the chunk, and
    /// header/length consistency. Throws <see cref="FrameException"/> on any
    /// mismatch (FR-022 bad-version/bad-CRC rejected) — never a silent mis-decode.
    /// </summary>
    public static ParsedFrame ParseFrame(ReadOnlySpan<byte> frame)
    {
        if (frame.Length < HeaderSize)
            throw new FrameException($"frame {frame.Length} shorter than {HeaderSize}-byte header");

        byte version = frame[0];
        if (version != Version)
            throw new FrameException($"unsupported wire version 0x{version:X2} (expected 0x{Version:X2})");

        byte kindByte = frame[OffKind];
        if (kindByte is not ((byte)FrameKind.Whole or (byte)FrameKind.Fragment))
            throw new FrameException($"unknown frame kind {kindByte}");
        var kind = (FrameKind)kindByte;

        uint messageId = BinaryPrimitives.ReadUInt32BigEndian(frame.Slice(OffMessageId));
        uint totalLen = BinaryPrimitives.ReadUInt32BigEndian(frame.Slice(OffTotalLen));
        ushort fragIndex = BinaryPrimitives.ReadUInt16BigEndian(frame.Slice(OffFragIndex));
        ushort fragCount = BinaryPrimitives.ReadUInt16BigEndian(frame.Slice(OffFragCount));
        uint chunkCrc = BinaryPrimitives.ReadUInt32BigEndian(frame.Slice(OffChunkCrc));
        uint chunkLen = BinaryPrimitives.ReadUInt32BigEndian(frame.Slice(OffChunkLen));

        if (totalLen > MaxPayloadBytes)
            throw new FrameException($"advertised total length {totalLen} exceeds MaxPayloadBytes {MaxPayloadBytes}");
        if (fragCount == 0)
            throw new FrameException("fragment count 0");
        if (fragIndex >= fragCount)
            throw new FrameException($"fragment index {fragIndex} out of range for count {fragCount}");
        if (kind == FrameKind.Whole && fragCount != 1)
            throw new FrameException($"whole frame with fragment count {fragCount}");

        long expectedFrameLen = HeaderSize + (long)chunkLen;
        if (frame.Length != expectedFrameLen)
            throw new FrameException($"frame length {frame.Length} != header+chunk {expectedFrameLen}");

        var chunk = frame.Slice(HeaderSize, (int)chunkLen).ToArray();
        uint actualCrc = Crc32.Compute(chunk);
        if (actualCrc != chunkCrc)
            throw new FrameException($"CRC mismatch: got 0x{actualCrc:X8}, frame says 0x{chunkCrc:X8}");

        return new ParsedFrame(kind, messageId, totalLen, fragIndex, fragCount, chunk);
    }
}
