// ClientChannel — FrameCodec framing over the shipped TcpTransport leaf (T012;
// research D1: one framing stack on the wire, not two).
//
// One request in flight at a time (the protocol promises in-order answers for a
// single client — wire rule 2), so the channel is a simple send-then-await
// round-trip. Transport-level failure surfaces as ClientTransportException so
// the REPL loop can render it DISTINCTLY from a goal-level failure (FR-007).

using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.SplitProtocol;

namespace GlpRuntime.ReplClient;

/// <summary>Transport-level failure (lost engine) — never a goal failure (FR-007).</summary>
public sealed class ClientTransportException : Exception
{
    public ClientTransportException(string message) : base(message) { }
    public ClientTransportException(string message, Exception inner) : base(message, inner) { }
}

public sealed class ClientChannel : IAsyncDisposable
{
    private readonly ILinkEndpoint _endpoint;
    private ulong _requestId;
    private uint _messageId;

    private ClientChannel(ILinkEndpoint endpoint)
    {
        _endpoint = endpoint;
    }

    /// <summary>Dial the engine host over TCP loopback (retries until the listener is up, bounded by <paramref name="timeout"/>).</summary>
    public static async Task<ClientChannel> ConnectAsync(string host, int port, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
            var endpoint = await new TcpTransport().ConnectAsync(
                LinkScheme.Tcp,
                LinkAddress.Endpoint(host, port),
                LinkOptions.Default,
                cts.Token).ConfigureAwait(false);
            return new ClientChannel(endpoint);
        }
        catch (OperationCanceledException)
        {
            throw new ClientTransportException(
                $"could not reach an engine host at {host}:{port} within {timeout.TotalSeconds:0}s");
        }
    }

    /// <summary>Next client-monotonic request id (echoed by the engine).</summary>
    public ulong NextRequestId() => ++_requestId;

    /// <summary>
    /// Send one request and await its one terminal response. Any transport
    /// break (peer FIN, reset, mid-frame close) is a ClientTransportException.
    /// </summary>
    public async Task<ResponseFrame> RoundTripAsync(RequestFrame request, CancellationToken ct = default)
    {
        byte[] frame = RequestResponseCodec.EncodeRequestFrame(request, ++_messageId);
        try
        {
            await _endpoint.SendBytesAsync(frame, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or System.Net.Sockets.SocketException or ObjectDisposedException)
        {
            throw new ClientTransportException($"engine connection lost while sending: {ex.Message}", ex);
        }

        byte[]? responseBytes = await _endpoint.RecvBytesAsync(ct).ConfigureAwait(false);
        if (responseBytes is null)
            throw new ClientTransportException(
                "engine connection lost before a response arrived (the request has no terminal " +
                "response — treat it as not committed and re-submit; wire rule 8)");

        var response = RequestResponseCodec.DecodeResponseFrame(responseBytes);
        if (response.RequestId != request.RequestId)
            throw new SplitProtocolException(
                $"response echoes request_id {response.RequestId}, expected {request.RequestId} (wire rule 2)");
        return response;
    }

    public ValueTask DisposeAsync() => _endpoint.DisposeAsync();
}
