// EngineServer — the one-accept TCP loopback listener + request loop (T009;
// contracts/wire-protocol.md rules 1/2).
//
// Exactly one client is served at a time (FR-002/DEF-A2). The listener stays
// live while a client is active so a SECOND connection can be accepted just far
// enough to receive a loud PROTOCOL_ERROR frame and be closed (wire rule 1) —
// the shipped TcpTransport leaf is one-accept-per-listen and cannot express
// that refusal, so the server owns its TcpListener directly and mirrors the
// leaf's stream framing (4-byte big-endian length prefix around each FrameCodec
// frame, TcpTransport.cs convention) for byte-compatibility with clients that
// connect through TcpTransport.ConnectAsync.
//
// When the active client disconnects, the engine — and everything loaded into
// it — survives; the next accepted connection becomes the new active client
// (US1 AS-3).

using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

using GlpRuntime.Link.Reliability;
using GlpRuntime.SplitProtocol;

namespace GlpRuntime.EngineHost;

/// <summary>Loud-fail exception for server-level refusals (port in use, bad --listen).</summary>
public sealed class EngineServerException : Exception
{
    public EngineServerException(string message) : base(message) { }
    public EngineServerException(string message, Exception inner) : base(message, inner) { }
}

public sealed class EngineServer
{
    private readonly IPEndPoint _endpoint;
    private readonly RequestDispatcher _dispatcher;
    private uint _messageId;
    private int _clientActive; // 0 = free, 1 = a client is being served

    public EngineServer(IPEndPoint endpoint, RequestDispatcher dispatcher)
    {
        _endpoint = endpoint;
        _dispatcher = dispatcher;
    }

    /// <summary>
    /// Bind and serve until SHUTDOWN (wire rule 6) or cancellation. A busy port
    /// is refused loudly at bind time — never queued silently (spec edge case).
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        var listener = new TcpListener(_endpoint);
        try
        {
            listener.Start();
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            throw new EngineServerException(
                $"cannot listen on {_endpoint}: another engine host already owns the endpoint " +
                "(one engine per endpoint — start this instance on a different port)", ex);
        }

        // The accept loop keeps accepting WHILE a client is served so a second
        // connection can be refused loudly (wire rule 1) instead of queueing
        // silently in the TCP backlog until the first client leaves.
        using var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        Task servingTask = Task.CompletedTask;
        try
        {
            while (!shutdownCts.IsCancellationRequested && !_dispatcher.ShutdownRequested)
            {
                TcpClient tcp;
                try
                {
                    tcp = await listener.AcceptTcpClientAsync(shutdownCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                tcp.NoDelay = true;

                if (Interlocked.CompareExchange(ref _clientActive, 1, 0) == 0)
                {
                    // One serving task at a time (the CompareExchange gate), so the
                    // engine stays single-threaded and requests are answered
                    // strictly in order (wire rule 2).
                    servingTask = Task.Run(async () =>
                    {
                        try
                        {
                            await ServeClientAsync(tcp, shutdownCts.Token).ConfigureAwait(false);
                        }
                        finally
                        {
                            Volatile.Write(ref _clientActive, 0);
                            tcp.Dispose();
                            if (_dispatcher.ShutdownRequested)
                                shutdownCts.Cancel(); // wake the accept loop (wire rule 6)
                        }
                    }, CancellationToken.None);
                }
                else
                {
                    // Wire rule 1: refuse the second client loudly, keep serving.
                    _ = Task.Run(() => RefuseSecondClientAsync(tcp), CancellationToken.None);
                }
            }
            await servingTask.ConfigureAwait(false); // flush the final response (e.g. SHUTDOWN ACK)
        }
        finally
        {
            listener.Stop();
        }
    }

    private async Task ServeClientAsync(TcpClient tcp, CancellationToken ct)
    {
        var stream = tcp.GetStream();
        while (!ct.IsCancellationRequested && !_dispatcher.ShutdownRequested)
        {
            byte[]? frame;
            try
            {
                frame = await ReadFrameAsync(stream, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return; // host shutting down
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                return; // client vanished mid-frame — engine survives (US1 AS-3)
            }
            if (frame is null)
                return; // clean client disconnect — engine survives (US1 AS-3)

            ResponseFrame response;
            ulong requestId = 0;
            try
            {
                var request = RequestResponseCodec.DecodeRequestFrame(frame);
                requestId = request.RequestId;
                response = await _dispatcher.DispatchAsync(request).ConfigureAwait(false);
            }
            catch (SplitProtocolException ex)
            {
                // Malformed frame → structured PROTOCOL_ERROR; engine keeps
                // serving (FR-006, wire rule 3).
                response = ResponseFrame.Text(requestId, ResponseKind.ProtocolError, ex.Message);
            }

            var bytes = RequestResponseCodec.EncodeResponseFrame(
                response, Interlocked.Increment(ref _messageId));
            try
            {
                await WriteFrameAsync(stream, bytes, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                return; // client vanished before the response — crash boundary is the client's (wire rule 8)
            }
        }
    }

    private async Task RefuseSecondClientAsync(TcpClient tcp)
    {
        try
        {
            var refusal = ResponseFrame.Text(0UL, ResponseKind.ProtocolError,
                "engine already serves one client (FR-002: one engine, one client)");
            // Interlocked: this task races the serving task's increment (both emit
            // frames concurrently) — duplicate message ids would break the framing
            // stack's dedup contract.
            var bytes = RequestResponseCodec.EncodeResponseFrame(
                refusal, Interlocked.Increment(ref _messageId));
            await WriteFrameAsync(tcp.GetStream(), bytes, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            // The refused client is already gone — nothing owed to it.
        }
        finally
        {
            tcp.Dispose();
        }
    }

    // ---- stream framing (TcpTransport.cs convention: 4-byte BE length + frame) ----

    private static async Task<byte[]?> ReadFrameAsync(NetworkStream stream, CancellationToken ct)
    {
        var header = await ReadExactlyAsync(stream, 4, ct).ConfigureAwait(false);
        if (header is null)
            return null;

        int len = BinaryPrimitives.ReadInt32BigEndian(header);
        if (len < 0)
            throw new IOException($"negative frame length {len}");
        // Bound the allocation BEFORE trusting the header: an unauthenticated
        // 4-byte header must not command a 2 GB buffer (codexreview
        // 20260730T070051Z unbounded-frame-allocation). The bound matches the
        // framing stack's own contract — FrameCodec refuses payloads above
        // MaxPayloadBytes on encode; the small slack covers the FrameCodec frame
        // header around a max-size payload (the length prefix wraps the whole
        // frame, not the bare payload — cycle-3 off-by-header note).
        if (len > FrameCodec.MaxPayloadBytes + 1024)
            throw new IOException(
                $"frame length {len} exceeds FrameCodec.MaxPayloadBytes {FrameCodec.MaxPayloadBytes} (+1 KiB frame-header slack)");
        if (len == 0)
            return Array.Empty<byte>();

        var body = await ReadExactlyAsync(stream, len, ct).ConfigureAwait(false);
        if (body is null)
            throw new IOException("peer closed mid-frame");
        return body;
    }

    private static async Task WriteFrameAsync(NetworkStream stream, byte[] frame, CancellationToken ct)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, frame.Length);
        await stream.WriteAsync(header, ct).ConfigureAwait(false);
        await stream.WriteAsync(frame, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task<byte[]?> ReadExactlyAsync(NetworkStream stream, int count, CancellationToken ct)
    {
        var buf = new byte[count];
        int off = 0;
        while (off < count)
        {
            int n = await stream.ReadAsync(buf.AsMemory(off, count - off), ct).ConfigureAwait(false);
            if (n == 0)
            {
                if (off == 0)
                    return null; // clean boundary → graceful close
                throw new IOException("peer closed mid-frame");
            }
            off += n;
        }
        return buf;
    }
}
