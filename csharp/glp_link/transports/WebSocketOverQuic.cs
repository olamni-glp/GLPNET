using System.Net.Quic;

namespace GlpRuntime.Link.Transports;

/// <summary>
/// A <b>genuine RFC 6455 WebSocket link carried over one bidirectional <see cref="QuicStream"/></b>
/// (feature 036, FR-002) — one WS per stream, the exact carriage RFC 9220 standardizes. This is a
/// first-class WS-over-QUIC design, not a fallback (QUIC/HTTP-3 is de-facto dominant).
/// </summary>
/// <remarks>
/// SKELETON (FR-017). Behaviour lands in US1 (T017): RFC 6455 opcodes (text/binary/close/ping/pong),
/// FIN/continuation reassembly, and varint payload length, with <b>no masking</b> (the QUIC stream is
/// already TLS-encrypted). Opaque application frames are carried via spec 025's <c>FrameCodec</c>
/// (version + CRC32 + fragment) reused unchanged — this layer only adds the RFC 6455 envelope, not a
/// second reliability scheme (FR-018). The link is brought up by the minimal CONNECT-style
/// <see cref="ConnectBootstrap"/>; the RFC 9220 Extended-CONNECT bootstrap stays isolated behind that
/// seam (later browser interop only, Decision 3).
/// </remarks>
internal sealed class WebSocketOverQuic
{
    private readonly QuicStream _stream;

    /// <summary>Wrap one negotiated bidi QUIC stream as the WebSocket link carrier.</summary>
    internal WebSocketOverQuic(QuicStream stream) => _stream = stream;

    /// <summary>
    /// Send one opaque application frame as one RFC 6455 data message over the QUIC stream
    /// (US1 T017). The frame is the already-self-delimiting 025 <c>FrameCodec</c> blob.
    /// </summary>
    public Task SendFrameAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default) =>
        throw new NotImplementedException("WebSocketOverQuic.SendFrameAsync — RFC 6455 framing lands in US1 (T017).");

    /// <summary>
    /// Receive one complete RFC 6455 message (reassembling FIN/continuation) from the QUIC stream,
    /// or <c>null</c> on a clean WebSocket close (US1 T017).
    /// </summary>
    public Task<byte[]?> ReceiveFrameAsync(CancellationToken ct = default) =>
        throw new NotImplementedException("WebSocketOverQuic.ReceiveFrameAsync — RFC 6455 framing lands in US1 (T017).");

    /// <summary>Send a RFC 6455 close frame (graceful WS teardown) — US1 T017.</summary>
    public Task CloseAsync(CancellationToken ct = default) =>
        throw new NotImplementedException("WebSocketOverQuic.CloseAsync — RFC 6455 close lands in US1 (T017).");
}
