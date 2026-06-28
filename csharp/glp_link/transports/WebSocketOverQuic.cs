using System.Buffers.Binary;
using System.IO;

using GlpRuntime.Link.Reliability;

namespace GlpRuntime.Link.Transports;

/// <summary>
/// A <b>genuine RFC 6455 WebSocket link carried over one bidirectional QUIC stream</b>
/// (feature 036, FR-002) — one WS per stream, the exact carriage RFC 9220 standardizes. A
/// first-class WS-over-QUIC design, not a fallback. The stream is supplied as a
/// <see cref="Stream"/> so this layer is testable over any duplex stream and reused unchanged
/// over a real <c>QuicStream</c>.
/// </summary>
/// <remarks>
/// RFC 6455 opcodes (text 0x1 / binary 0x2 / close 0x8 / ping 0x9 / pong 0xA), FIN/continuation
/// reassembly, and 7/16/64-bit payload length. Frames we send are <b>unmasked</b> — the QUIC
/// stream is already TLS-encrypted, so masking (a browser cache-poisoning mitigation) is
/// unnecessary between our own endpoints (T017). On receive we still honour a mask bit if a peer
/// sets one, so the decoder is RFC-correct. Each <see cref="SendFrameAsync"/> is one opaque
/// application frame ⇒ one WS binary message ⇒ one peer <see cref="ReceiveFrameAsync"/>, so per-link
/// FIFO is reconstructed by spec 025's sublayer above (FR-018) exactly as for the TCP leaf.
/// </remarks>
internal sealed class WebSocketOverQuic
{
    private const byte Fin = 0x80;
    private const byte OpContinuation = 0x0;
    private const byte OpText = 0x1;
    private const byte OpBinary = 0x2;
    private const byte OpClose = 0x8;
    private const byte OpPing = 0x9;
    private const byte OpPong = 0xA;
    private const byte MaskBit = 0x80;

    private readonly Stream _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    internal WebSocketOverQuic(Stream stream) => _stream = stream;

    /// <summary>Send one opaque application frame as one RFC 6455 binary message (FIN=1, unmasked).</summary>
    public async Task SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
    {
        var header = BuildHeader(OpBinary, frame.Length);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(header, ct).ConfigureAwait(false);
            if (frame.Length > 0)
                await _stream.WriteAsync(frame, ct).ConfigureAwait(false);
            await _stream.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Receive one complete RFC 6455 data message (reassembling FIN/continuation), transparently
    /// answering ping with pong; returns <c>null</c> on a clean WebSocket close or end-of-stream.
    /// </summary>
    public async Task<byte[]?> ReceiveFrameAsync(CancellationToken ct = default)
    {
        using var message = new MemoryStream();

        while (true)
        {
            int b0 = await ReadByteOrEofAsync(ct).ConfigureAwait(false);
            if (b0 < 0)
                return null; // peer ended the stream at a frame boundary → graceful close (eos upstream)

            bool fin = (b0 & Fin) != 0;
            byte opcode = (byte)(b0 & 0x0F);

            int b1 = await ReadByteAsync(ct).ConfigureAwait(false);
            bool masked = (b1 & MaskBit) != 0;
            long len = await ReadPayloadLengthAsync((byte)(b1 & 0x7F), ct).ConfigureAwait(false);

            byte[] maskKey = Array.Empty<byte>();
            if (masked)
            {
                maskKey = new byte[4];
                await _stream.ReadExactlyAsync(maskKey, ct).ConfigureAwait(false);
            }

            var payload = new byte[len];
            if (len > 0)
                await _stream.ReadExactlyAsync(payload, ct).ConfigureAwait(false);
            if (masked)
                for (int i = 0; i < payload.Length; i++) payload[i] ^= maskKey[i & 3];

            switch (opcode)
            {
                case OpClose:
                    return null; // peer initiated WS close → end of stream

                case OpPing:
                    await SendControlAsync(OpPong, payload, ct).ConfigureAwait(false);
                    continue;

                case OpPong:
                    continue; // unsolicited/keepalive pong — ignore

                case OpText:
                case OpBinary:
                case OpContinuation:
                    message.Write(payload, 0, payload.Length);
                    if (fin)
                        return message.ToArray();
                    continue; // more continuation frames coming

                default:
                    throw new FrameException($"RFC 6455: unsupported opcode 0x{opcode:X}");
            }
        }
    }

    /// <summary>Send a RFC 6455 close frame (graceful WS teardown).</summary>
    public Task CloseAsync(CancellationToken ct = default) =>
        SendControlAsync(OpClose, ReadOnlyMemory<byte>.Empty, ct);

    private async Task SendControlAsync(byte opcode, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        var header = BuildHeader(opcode, payload.Length);
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(header, ct).ConfigureAwait(false);
            if (payload.Length > 0)
                await _stream.WriteAsync(payload, ct).ConfigureAwait(false);
            await _stream.FlushAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Build an unmasked RFC 6455 frame header (FIN=1) for the given opcode + payload length.</summary>
    private static byte[] BuildHeader(byte opcode, long length)
    {
        if (length <= 125)
            return new byte[] { (byte)(Fin | opcode), (byte)length };
        if (length <= ushort.MaxValue)
        {
            var h = new byte[4];
            h[0] = (byte)(Fin | opcode);
            h[1] = 126;
            BinaryPrimitives.WriteUInt16BigEndian(h.AsSpan(2), (ushort)length);
            return h;
        }
        var h8 = new byte[10];
        h8[0] = (byte)(Fin | opcode);
        h8[1] = 127;
        BinaryPrimitives.WriteUInt64BigEndian(h8.AsSpan(2), (ulong)length);
        return h8;
    }

    private async Task<long> ReadPayloadLengthAsync(byte len7, CancellationToken ct)
    {
        if (len7 < 126)
            return len7;
        if (len7 == 126)
        {
            var b = new byte[2];
            await _stream.ReadExactlyAsync(b, ct).ConfigureAwait(false);
            return BinaryPrimitives.ReadUInt16BigEndian(b);
        }
        var b8 = new byte[8];
        await _stream.ReadExactlyAsync(b8, ct).ConfigureAwait(false);
        long len = (long)BinaryPrimitives.ReadUInt64BigEndian(b8);
        if (len < 0)
            throw new FrameException("RFC 6455: payload length exceeds Int64 range");
        return len;
    }

    /// <summary>Read one byte; -1 at a clean end-of-stream (no byte available).</summary>
    private async Task<int> ReadByteOrEofAsync(CancellationToken ct)
    {
        var one = new byte[1];
        int n = await _stream.ReadAsync(one, ct).ConfigureAwait(false);
        return n == 0 ? -1 : one[0];
    }

    /// <summary>Read one byte; throws at end-of-stream (mid-frame truncation is a fault).</summary>
    private async Task<int> ReadByteAsync(CancellationToken ct)
    {
        var one = new byte[1];
        await _stream.ReadExactlyAsync(one, ct).ConfigureAwait(false);
        return one[0];
    }
}
