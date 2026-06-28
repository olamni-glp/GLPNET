using System.Net.Quic;

using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Transports;

/// <summary>
/// The genuine HTTP/3 (QUIC) + WebSocket transport leaf (feature 036) — a new
/// <see cref="ILinkTransport"/> alongside <see cref="LoopbackTransport"/> /
/// <see cref="TcpTransport"/>, reusing spec 025's seam, reliability sublayer, and
/// ground-relay discipline for free (FR-018). Selected by <see cref="LinkScheme.Quic"/>.
/// </summary>
/// <remarks>
/// SKELETON (FR-017 skeleton-before-behaviour). This file fixes the shape; the real
/// on-wire QUIC handshake (<c>System.Net.Quic</c> / MsQuic, GA in .NET 9+,
/// cross-platform) with the shared-cert <b>SPKI SHA-256 pin</b> via
/// <c>RemoteCertificateValidationCallback</c> lands in US1 (T016). The WebSocket link
/// — <b>genuine RFC 6455 framing over one bidi <c>QuicStream</c></b> (025 FrameCodec),
/// brought up by the minimal CONNECT-style <see cref="ConnectBootstrap"/> — lands in
/// US1 (T017). The RFC 9220 Extended-CONNECT-over-HTTP/3 bootstrap (the only piece
/// .NET has not shipped) stays isolated behind the bootstrap seam for later browser
/// interop only (FR-002).
///
/// Real QUIC only (FR-001): every path gates on <see cref="IsSupported"/> and refuses
/// to claim a handshake the platform cannot perform — no loopback/simulated fallback.
/// </remarks>
public sealed class QuicTransport : ILinkTransport
{
    private static readonly LinkScheme[] Schemes = { LinkScheme.Quic };

    public IReadOnlyCollection<LinkScheme> SupportedSchemes => Schemes;

    /// <summary>
    /// True iff this host can perform a genuine QUIC handshake in BOTH roles
    /// (<see cref="QuicListener.IsSupported"/> for the server, and
    /// <see cref="QuicConnection.IsSupported"/> for the client). Gated before any
    /// endpoint claims a real handshake (FR-001). The residual probe T013a asserts
    /// this is <c>true</c> on the actual demo host before US1 QUIC code runs.
    /// </summary>
    public static bool IsSupported => QuicListener.IsSupported && QuicConnection.IsSupported;

    public Task<ILinkEndpoint> ListenAsync(LinkScheme scheme, LinkAddress local, LinkOptions opts, CancellationToken ct = default)
    {
        Require(scheme);
        RequireQuicSupported("listen");
        // US1 T016/T017: bind a QuicListener (ALPN h3, shared-cert TLS), accept a real
        // connection, open one bidi QuicStream, and bring up the RFC 6455 WS link on it.
        throw new NotImplementedException(
            "QuicTransport.ListenAsync — real QUIC+WS server path lands in US1 (T016/T017).");
    }

    public Task<ILinkEndpoint> ConnectAsync(LinkScheme scheme, LinkAddress remote, LinkOptions opts, CancellationToken ct = default)
    {
        Require(scheme);
        RequireQuicSupported("connect");
        // US1 T016/T017: open a QuicConnection (ALPN h3), pin the shared cert by SPKI
        // SHA-256 in the validation callback (never return true), then bootstrap the WS link.
        throw new NotImplementedException(
            "QuicTransport.ConnectAsync — real QUIC+WS client path lands in US1 (T016/T017).");
    }

    private static void Require(LinkScheme scheme)
    {
        if (scheme != LinkScheme.Quic)
            throw new ArgumentException($"QuicTransport does not serve scheme '{scheme}'", nameof(scheme));
    }

    /// <summary>
    /// Refuse — clearly, not silently — when the platform cannot do real QUIC (FR-001/FR-019).
    /// This is the <c>alpn_version_mismatch</c>/unsupported-stack class of failure surfaced up-front.
    /// </summary>
    private static void RequireQuicSupported(string op)
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException(
                $"QUIC {op} unavailable: QuicListener.IsSupported={QuicListener.IsSupported}, "
                + $"QuicConnection.IsSupported={QuicConnection.IsSupported}. A real handshake requires "
                + "msquic in the .NET runtime (Win11/Server2022+, Linux libmsquic 2.2+, macOS partial). "
                + "Real QUIC only — no loopback/simulated fallback (FR-001).");
    }
}
