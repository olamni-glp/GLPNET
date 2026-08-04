// EngineServer — the TCP loopback listener + request loop (T009/T017;
// contracts/wire-protocol.md rules 1/2).
//
// Two serve modes (064 US2, FR-005):
//
//   SingleClient (DEFAULT — the 061 FR-002/DEF-A2 contract, preserved verbatim):
//   exactly one client is served at a time. The listener stays live while a
//   client is active so a SECOND connection can be accepted just far enough to
//   receive a loud PROTOCOL_ERROR frame and be closed (wire rule 1) — the
//   single-accept TcpTransport.ListenAsync leaf cannot express that refusal, so
//   this path owns its TcpListener directly and mirrors the leaf's stream
//   framing (4-byte big-endian length prefix around each FrameCodec frame,
//   TcpTransport.cs convention) for byte-compatibility with clients that
//   connect through TcpTransport.ConnectAsync. When the active client
//   disconnects, the engine — and everything loaded into it — survives; the
//   next accepted connection becomes the new active client (US1 AS-3).
//
//   MultiClient (064 US2 opt-in, supersedes the single-client rule when
//   selected — 061 behavior stays available via the default): accepts clients
//   continuously through the shipped TcpTransport.AcceptLoopAsync (T015), one
//   ClientSession per client (T016), merges every session's requests into ONE
//   serialized serve loop over the ONE RequestDispatcher (the engine stays
//   single-threaded; per-session FIFO is preserved, the merged order is a legal
//   interleaving — US2 AS-2), and routes each reply to exactly its issuing
//   session (RoutedReply key). A client crash/disconnect closes only its own
//   session — pending replies are discarded (ClientSession.CloseAsync), the
//   merge loop is never wedged (spec edge case).
//
// A31 merge-tree note (T017, recorded PARTIAL): the C# merge channel below is
// the host-side analogue of the shipped GLP control program's merge tree
// (programs/tests/typed/multi_client_control.glp — broadcast3/merge3). Wiring
// the REAL client channels into that GLP-level merge tree would need engine
// plumbing that does not exist today (injecting/appending to live GLP stream
// variables of a running computation from the host, and incrementally reading
// the merged reply stream back out) — new engine/language surface, which is
// Section-1.14 gated (propose-only, spec FR-011). Until proposed and approved,
// the per-session merge + routing lives here, host-side.

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;
using GlpRuntime.SplitProtocol;

namespace GlpRuntime.EngineHost;

/// <summary>Loud-fail exception for server-level refusals (port in use, bad --listen).</summary>
public sealed class EngineServerException : Exception
{
    public EngineServerException(string message) : base(message) { }
    public EngineServerException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Serve-mode selector (064 US2). <see cref="SingleClient"/> is the default —
/// the 061 FR-002 one-client contract with the loud second-client refusal,
/// unchanged. <see cref="MultiClient"/> opts into the 064 FR-005 concurrent
/// serve path (N sessions, per-session reply routing).
/// </summary>
public enum EngineServeMode
{
    SingleClient,
    MultiClient,
}

public sealed class EngineServer
{
    private readonly IPEndPoint _endpoint;
    private readonly RequestDispatcher _dispatcher;
    private readonly EngineServeMode _mode;
    private uint _messageId;
    private int _clientActive; // 0 = free, 1 = a client is being served (single-client mode)
    private ulong _nextSessionId; // multi-client mode session id source

    public EngineServer(
        IPEndPoint endpoint,
        RequestDispatcher dispatcher,
        EngineServeMode mode = EngineServeMode.SingleClient)
    {
        _endpoint = endpoint;
        _dispatcher = dispatcher;
        _mode = mode;
    }

    /// <summary>
    /// Bind and serve until SHUTDOWN (wire rule 6) or cancellation. A busy port
    /// is refused loudly at bind time — never queued silently (spec edge case).
    /// </summary>
    public async Task RunAsync(CancellationToken ct = default)
    {
        if (_mode == EngineServeMode.MultiClient)
        {
            await RunMultiClientAsync(ct).ConfigureAwait(false);
            return;
        }

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

    // ================= multi-client serve path (064 US2, T017) =================

    /// <summary>One session's request as it enters the merged serve stream.</summary>
    private readonly record struct MergedRequest(ClientSession Session, RequestFrame Request);

    /// <summary>
    /// Multi-client mode: accept clients continuously via the shipped
    /// TcpTransport.AcceptLoopAsync (T015), spin up one ClientSession + pumps per
    /// client, and serve every session through the ONE merged serve loop until
    /// SHUTDOWN (any client may issue it — wire rule 6) or cancellation.
    /// </summary>
    private async Task RunMultiClientAsync(CancellationToken ct)
    {
        using var shutdownCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sessions = new ConcurrentDictionary<ulong, ClientSession>();
        // The host-side merge tree (see the A31 note in the file header): every
        // session's forwarder writes here; the ONE serve loop reads (SingleReader).
        var merged = Channel.CreateUnbounded<MergedRequest>(
            new UnboundedChannelOptions { SingleReader = true });
        var serveTask = Task.Run(
            () => MergeServeLoopAsync(merged.Reader, shutdownCts), CancellationToken.None);
        var pumps = new ConcurrentBag<Task>();

        try
        {
            await foreach (var conn in new TcpTransport().AcceptLoopAsync(
                LinkScheme.Tcp,
                LinkAddress.Endpoint(_endpoint.Address.ToString(), _endpoint.Port),
                LinkOptions.Default,
                shutdownCts.Token).ConfigureAwait(false))
            {
                // Register the session with the merge tree: sessions map + a recv
                // pump (connection → session inbound) + a forwarder (session
                // inbound → merged stream). Pumps are fully self-contained — they
                // never throw out (the teardown WhenAll must not observe faults).
                var session = new ClientSession(++_nextSessionId, conn);
                sessions[session.SessionId] = session;
                pumps.Add(Task.Run(
                    () => RecvPumpAsync(session, sessions, shutdownCts.Token), CancellationToken.None));
                pumps.Add(Task.Run(
                    () => ForwardInboundAsync(session, merged.Writer), CancellationToken.None));
            }
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            // Same loud bind-time refusal contract as the single-client path.
            throw new EngineServerException(
                $"cannot listen on {_endpoint}: another engine host already owns the endpoint " +
                "(one engine per endpoint — start this instance on a different port)", ex);
        }
        finally
        {
            shutdownCts.Cancel();
            merged.Writer.TryComplete();
            // Drain the serve loop first — the SHUTDOWN ACK (delivered inline by
            // the loop) is already flushed by the time the loop returns.
            await serveTask.ConfigureAwait(false);
            foreach (var session in sessions.Values)
            {
                try { await session.DisposeAsync().ConfigureAwait(false); }
                catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException) { }
            }
            await Task.WhenAll(pumps).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Per-session recv pump: connection frames → decoded requests → the
    /// session's inbound channel. A malformed frame gets a session-level
    /// PROTOCOL_ERROR straight back on THIS connection (request id undecodable →
    /// 0) and the session keeps serving (FR-006). Disconnect/crash (null frame
    /// or transport fault) closes ONLY this session: pending replies are
    /// discarded by ClientSession.CloseAsync, the merge loop never wedges.
    /// </summary>
    private async Task RecvPumpAsync(
        ClientSession session, ConcurrentDictionary<ulong, ClientSession> sessions, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var frame = await session.Connection.RecvBytesAsync(ct).ConfigureAwait(false);
                if (frame is null)
                    break; // clean disconnect or transport fault — this session only

                RequestFrame request;
                try
                {
                    request = RequestResponseCodec.DecodeRequestFrame(frame);
                }
                catch (SplitProtocolException ex)
                {
                    await SendDirectAsync(session,
                        ResponseFrame.Text(0UL, ResponseKind.ProtocolError, ex.Message))
                        .ConfigureAwait(false);
                    continue;
                }
                if (!session.InboundWriter.TryWrite(request))
                    break; // draining/closed — no new requests accepted
            }
        }
        catch (OperationCanceledException)
        {
            // host shutting down
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            // an endpoint that throws instead of returning null — same disconnect path
        }
        finally
        {
            sessions.TryRemove(session.SessionId, out _);
            try
            {
                int discarded = await session.CloseAsync().ConfigureAwait(false);
                if (discarded > 0)
                {
                    Console.WriteLine(
                        $"glp_engine_host: session {session.SessionId} closed with " +
                        $"{discarded} pending repl{(discarded == 1 ? "y" : "ies")} discarded");
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // the transport is already gone — nothing owed to it
            }
            // The session left `sessions` above, so RunMultiClientAsync's teardown
            // loop can no longer dispose it: release the transport's handles HERE
            // (socket, stream, send lock) or every departed client leaks them.
            // Ordering is safe — CloseAsync already ran, so no reply can still be
            // queued for delivery; a delivery in flight fails with
            // ObjectDisposedException, which DeliverRoutedRepliesAsync absorbs.
            try
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                // the transport is already gone — nothing owed to it
            }
        }
    }

    /// <summary>
    /// Per-session merge-tree leaf: forwards the session's inbound requests into
    /// the merged serve stream in FIFO order. Ends when the session drains/closes
    /// (inbound completed) or the serve loop is gone (merged writer completed).
    /// </summary>
    private static async Task ForwardInboundAsync(ClientSession session, ChannelWriter<MergedRequest> merged)
    {
        await foreach (var request in session.Inbound.ReadAllAsync().ConfigureAwait(false))
        {
            if (!merged.TryWrite(new MergedRequest(session, request)))
                return; // serve loop ended (shutdown) — session teardown follows
        }
    }

    /// <summary>
    /// THE serve loop: reads the merged request stream and dispatches strictly
    /// serially through the one RequestDispatcher (the engine stays
    /// single-threaded; wire rule 2 holds per session). Each reply is routed to
    /// exactly its issuing session via the RoutedReply key and delivered inline —
    /// a closed session's reply is discarded (TryRouteReply false), never blocked
    /// on. SHUTDOWN from any client ends the loop after its ACK is flushed.
    /// </summary>
    private async Task MergeServeLoopAsync(
        ChannelReader<MergedRequest> merged, CancellationTokenSource shutdownCts)
    {
        await foreach (var (session, request) in merged.ReadAllAsync().ConfigureAwait(false))
        {
            ResponseFrame response;
            try
            {
                response = await _dispatcher.DispatchAsync(request).ConfigureAwait(false);
            }
            catch (SplitProtocolException ex)
            {
                response = ResponseFrame.Text(request.RequestId, ResponseKind.ProtocolError, ex.Message);
            }

            if (session.TryRouteReply(new RoutedReply(session.SessionId, request.RequestId, response)))
                await DeliverRoutedRepliesAsync(session).ConfigureAwait(false);
            // else: session closed — the reply is discarded, the loop moves on.

            if (_dispatcher.ShutdownRequested)
            {
                shutdownCts.Cancel(); // wake the accept loop (wire rule 6)
                return;
            }
        }
    }

    /// <summary>
    /// Deliver every reply queued on the session's outbound channel to its
    /// connection (delivery is inline so a terminal ACK — e.g. SHUTDOWN's — is
    /// flushed before the loop advances; this drain races CloseAsync's drain on
    /// the recv-pump thread, which is why the channel is multi-reader —
    /// ClientSession._outbound). A transport failure mid-delivery closes ONLY this session
    /// (remaining replies are discarded by CloseAsync) and never propagates.
    /// </summary>
    private async Task DeliverRoutedRepliesAsync(ClientSession session)
    {
        while (session.Outbound.TryRead(out var reply))
        {
            var bytes = RequestResponseCodec.EncodeResponseFrame(
                reply.ResultEnvelope, Interlocked.Increment(ref _messageId));
            try
            {
                await session.Connection.SendBytesAsync(bytes, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
            {
                await session.CloseAsync().ConfigureAwait(false);
                return; // crash boundary is the client's (wire rule 8) — others unaffected
            }
        }
    }

    /// <summary>
    /// Session-level direct send (multi-client analogue of the single-client
    /// refusal write): used for pre-decode protocol errors that never enter the
    /// merge stream. Concurrent with serve-loop sends — the TCP endpoint
    /// serializes writers. Failures mean the client is gone; nothing is owed.
    /// </summary>
    private async Task SendDirectAsync(ClientSession session, ResponseFrame response)
    {
        var bytes = RequestResponseCodec.EncodeResponseFrame(
            response, Interlocked.Increment(ref _messageId));
        try
        {
            await session.Connection.SendBytesAsync(bytes, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
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
