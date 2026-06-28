using System.Net.Quic;

using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Transports;

/// <summary>
/// One established QUIC+WS link end (feature 036): a <see cref="QuicConnection"/> with one
/// bidirectional <see cref="QuicStream"/> carrying a <b>genuine RFC 6455 WebSocket link</b>
/// (one WS per stream — the carriage RFC 9220 standardizes), reusing spec 025's
/// <see cref="ILinkEndpoint"/> seam so the reliability sublayer (seq/dedup, reorder,
/// epoch/fence, backpressure) and ground-relay discipline ride for free (FR-018).
/// </summary>
/// <remarks>
/// SKELETON (FR-017). The frame I/O is delegated to <see cref="WebSocketOverQuic"/> (025
/// FrameCodec over the QuicStream); both land in US1 (T017). Send and recv run on different
/// threads (the egress drainer writes; the pump's recv loop reads), which a single
/// <see cref="QuicStream"/> supports for one concurrent reader + one writer — matching the
/// <see cref="TcpEndpoint"/> precedent.
/// </remarks>
internal sealed class QuicEndpoint : ILinkEndpoint
{
    private readonly QuicConnection? _connection;
    private readonly QuicStream? _stream;
    private readonly WebSocketOverQuic? _ws;

    public LinkId Id { get; }

#pragma warning disable CS0067 // OnFault is raised by the US1 frame I/O bodies (T017), not the skeleton.
    public event Action<LinkFaultSignal>? OnFault;
#pragma warning restore CS0067

    /// <summary>
    /// Construct an established endpoint over a live QUIC connection + bidi stream. The US1
    /// establishment paths (<see cref="QuicTransport.ListenAsync"/>/<see cref="QuicTransport.ConnectAsync"/>)
    /// pass the negotiated connection/stream; the skeleton accepts nulls so the type is
    /// constructable in tests before the handshake exists.
    /// </summary>
    internal QuicEndpoint(LinkId id, QuicConnection? connection = null, QuicStream? stream = null, WebSocketOverQuic? ws = null)
    {
        Id = id;
        _connection = connection;
        _stream = stream;
        _ws = ws;
    }

    public Task SendBytesAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default) =>
        // US1 T017: one self-delimiting frame ⇒ one RFC 6455 data frame over the QuicStream
        // (no masking on the TLS-encrypted QUIC stream); per-link FIFO reconstructed above by 025.
        throw new NotImplementedException("QuicEndpoint.SendBytesAsync — WS-over-QUIC frame I/O lands in US1 (T017).");

    public Task<byte[]?> RecvBytesAsync(CancellationToken ct = default) =>
        // US1 T017: reassemble one RFC 6455 message (FIN/continuation) from the QuicStream;
        // null on the peer's clean WS close (→ closed/eos upstream).
        throw new NotImplementedException("QuicEndpoint.RecvBytesAsync — WS-over-QUIC frame I/O lands in US1 (T017).");

    public Task CloseAsync() =>
        // US1 T017: send a RFC 6455 close frame, let in-flight frames drain, then close the stream.
        throw new NotImplementedException("QuicEndpoint.CloseAsync — graceful WS/QUIC teardown lands in US1 (T017).");

    public ValueTask DisposeAsync() =>
        // Best-effort disposal of stream + connection; full teardown semantics in US1 (T017).
        throw new NotImplementedException("QuicEndpoint.DisposeAsync — teardown lands in US1 (T017).");
}
