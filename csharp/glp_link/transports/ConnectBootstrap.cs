using System.IO;
using System.Text;

using GlpRuntime.Link.Reliability;

namespace GlpRuntime.Link.Transports;

/// <summary>
/// Brings up the WebSocket link on a negotiated bidi QUIC stream via a <b>minimal CONNECT-style
/// bootstrap</b> on the stream (feature 036, FR-002). The MVP path: it does NOT require RFC 9220
/// Extended-CONNECT-over-HTTP/3, which .NET has not shipped.
/// </summary>
/// <remarks>
/// A one-line request/response on the raw stream before WS framing begins — analogous to the RFC
/// 6455 HTTP Upgrade, reduced to what two cooperating GLP-Quick endpoints need (no HTTP/3 Extended
/// CONNECT, no browser handshake). The RFC 9220 path is isolated behind
/// <see cref="ExtendedConnectAsync"/> for later third-party/browser interop only (Decision 3), so
/// the genuine-WS MVP never blocks on the one piece .NET lacks.
/// </remarks>
internal static class ConnectBootstrap
{
    /// <summary>The ALPN protocol id for HTTP/3 (RFC 9114 / RFC 7301): the negotiated value MUST be h3.</summary>
    public const string AlpnH3 = "h3";

    private const string Request = "GLPQUICK/1 CONNECT glp-link\r\n";
    private const string Accepted = "GLPQUICK/1 101 SWITCHING\r\n";
    private const int MaxLineBytes = 256; // a bootstrap line is tiny; cap to reject a runaway peer (FR-022 spirit)

    /// <summary>
    /// Establish the WebSocket link over <paramref name="stream"/> using the minimal CONNECT-style
    /// bootstrap. The connector initiates the request; the listener accepts and replies.
    /// </summary>
    /// <param name="stream">A negotiated bidirectional QUIC stream (or any duplex stream, for tests).</param>
    /// <param name="isConnector">True for the client_connector side; false for the listener.</param>
    public static async Task<WebSocketOverQuic> BootstrapAsync(Stream stream, bool isConnector, CancellationToken ct = default)
    {
        if (isConnector)
        {
            await WriteLineAsync(stream, Request, ct).ConfigureAwait(false);
            var reply = await ReadLineAsync(stream, ct).ConfigureAwait(false);
            if (reply != Accepted.TrimEnd('\r', '\n'))
                throw new FrameException($"WS bootstrap rejected by listener: '{reply}'");
        }
        else
        {
            var req = await ReadLineAsync(stream, ct).ConfigureAwait(false);
            if (req != Request.TrimEnd('\r', '\n'))
                throw new FrameException($"WS bootstrap: unexpected request line '{req}'");
            await WriteLineAsync(stream, Accepted, ct).ConfigureAwait(false);
        }
        return new WebSocketOverQuic(stream);
    }

    /// <summary>
    /// The RFC 9220 Extended-CONNECT-over-HTTP/3 bootstrap seam — isolated for later third-party/
    /// browser interop only (FR-002, Decision 3). Intentionally NOT part of the MVP.
    /// </summary>
    public static Task<WebSocketOverQuic> ExtendedConnectAsync(Stream stream, CancellationToken ct = default) =>
        throw new NotSupportedException(
            "RFC 9220 Extended-CONNECT-over-HTTP/3 is isolated behind this seam for later browser "
            + "interop only (FR-002). The MVP uses the minimal CONNECT-style bootstrap; do not block on this.");

    private static async Task WriteLineAsync(Stream stream, string line, CancellationToken ct)
    {
        var bytes = Encoding.ASCII.GetBytes(line);
        await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Read a CRLF/LF-terminated ASCII line (bootstrap only), capped to reject a runaway peer.</summary>
    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken ct)
    {
        var buf = new List<byte>(MaxLineBytes);
        var one = new byte[1];
        while (true)
        {
            int n = await stream.ReadAsync(one, ct).ConfigureAwait(false);
            if (n == 0)
                throw new FrameException("WS bootstrap: stream closed before the line terminated");
            if (one[0] == (byte)'\n')
                break;
            if (one[0] != (byte)'\r')
                buf.Add(one[0]);
            if (buf.Count > MaxLineBytes)
                throw new FrameException("WS bootstrap: line exceeded the bound");
        }
        return Encoding.ASCII.GetString(buf.ToArray());
    }
}
