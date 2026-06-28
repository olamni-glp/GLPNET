using System.Net.Quic;

namespace GlpRuntime.Link.Transports;

/// <summary>
/// Brings up the WebSocket link on a negotiated bidi <see cref="QuicStream"/> via a <b>minimal
/// CONNECT-style bootstrap</b> on the stream (feature 036, FR-002). This is the MVP path: it does
/// NOT require RFC 9220 Extended-CONNECT-over-HTTP/3, which .NET has not shipped.
/// </summary>
/// <remarks>
/// SKELETON (FR-017). The bootstrap handshake + WS-link handoff land in US1 (T017).
///
/// Two paths, deliberately separated by this seam (Decision 3):
/// <list type="bullet">
///   <item><see cref="BootstrapAsync"/> — the minimal CONNECT-style exchange on a raw QUIC stream,
///   sufficient for our genuine RFC 6455 link between our own endpoints (MVP, US1).</item>
///   <item><see cref="ExtendedConnectAsync"/> — the RFC 9220 Extended-CONNECT-over-HTTP/3 bootstrap,
///   needed ONLY for third-party/browser interop. Isolated here so the MVP never blocks on the one
///   piece .NET lacks (FR-002); wired later, not in US1.</item>
/// </list>
/// </remarks>
internal static class ConnectBootstrap
{
    /// <summary>The ALPN protocol id for HTTP/3 (RFC 9114 / RFC 7301): the negotiated value MUST be h3.</summary>
    public const string AlpnH3 = "h3";

    /// <summary>
    /// Establish the WebSocket link over <paramref name="stream"/> using the minimal CONNECT-style
    /// bootstrap (US1 T017). The connector side initiates; the listener side accepts.
    /// </summary>
    /// <param name="stream">A negotiated bidirectional QUIC stream.</param>
    /// <param name="isConnector">True for the <c>client_connector</c> side; false for the listener.</param>
    /// <param name="ct">Cancels the bootstrap.</param>
    /// <returns>The established <see cref="WebSocketOverQuic"/> carrier.</returns>
    public static Task<WebSocketOverQuic> BootstrapAsync(QuicStream stream, bool isConnector, CancellationToken ct = default) =>
        throw new NotImplementedException(
            "ConnectBootstrap.BootstrapAsync — minimal CONNECT-style WS bootstrap lands in US1 (T017).");

    /// <summary>
    /// The RFC 9220 Extended-CONNECT-over-HTTP/3 bootstrap seam — isolated for later third-party/
    /// browser interop only (FR-002, Decision 3). Intentionally NOT part of the MVP: .NET has not
    /// shipped Extended-CONNECT-over-HTTP/3, so depending on it would block the genuine-WS MVP.
    /// </summary>
    public static Task<WebSocketOverQuic> ExtendedConnectAsync(QuicStream stream, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "RFC 9220 Extended-CONNECT-over-HTTP/3 is isolated behind this seam for later browser "
            + "interop only (FR-002). The MVP uses the minimal CONNECT-style bootstrap; do not block on this.");
}
