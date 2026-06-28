using System.Net.Quic;

using GlpRuntime.Link.Seam;

// System.Net.Quic is [SupportedOSPlatform] windows/linux/macOS; runtime-gated by
// QuicTransport.IsSupported (FR-001) — the CA1416 platform advisory does not apply.
#pragma warning disable CA1416

namespace GlpRuntime.Link.Transports;

/// <summary>
/// One established QUIC+WS link end (feature 036): a live <see cref="QuicConnection"/> with one
/// bidirectional <see cref="QuicStream"/> carrying a <b>genuine RFC 6455 WebSocket link</b>
/// (<see cref="WebSocketOverQuic"/>), reusing spec 025's <see cref="ILinkEndpoint"/> seam so the
/// reliability sublayer (seq/dedup, reorder, epoch/fence, backpressure) + ground-relay ride for
/// free (FR-018).
/// </summary>
/// <remarks>
/// One <see cref="SendBytesAsync"/> ⇒ one RFC 6455 binary message ⇒ one peer
/// <see cref="RecvBytesAsync"/> (self-delimiting), matching the <see cref="TcpEndpoint"/> precedent.
/// Send and recv run on different threads (one concurrent reader + one writer on the stream).
/// </remarks>
internal sealed class QuicEndpoint : ILinkEndpoint
{
    private readonly QuicConnection _connection;
    private readonly QuicStream _stream;
    private readonly WebSocketOverQuic _ws;
    private int _closed;

    public LinkId Id { get; }

    public event Action<LinkFaultSignal>? OnFault;

    internal QuicEndpoint(LinkId id, QuicConnection connection, QuicStream stream, WebSocketOverQuic ws)
    {
        Id = id;
        _connection = connection;
        _stream = stream;
        _ws = ws;
    }

    public async Task SendBytesAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
    {
        try
        {
            await _ws.SendFrameAsync(frame, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is QuicException or IOException or ObjectDisposedException)
        {
            if (Volatile.Read(ref _closed) == 0)
                OnFault?.Invoke(new LinkFaultSignal(Id, LinkFaultKind.Transient, $"quic/ws send failed: {ex.Message}"));
            throw;
        }
    }

    public async Task<byte[]?> RecvBytesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _ws.ReceiveFrameAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw; // pump disposed / link torn down — normal shutdown
        }
        catch (Exception ex) when (ex is QuicException or IOException or ObjectDisposedException or EndOfStreamException)
        {
            // An error on a link WE closed is the expected end of our own teardown — not a fault.
            if (Volatile.Read(ref _closed) == 0)
                OnFault?.Invoke(new LinkFaultSignal(Id, LinkFaultKind.Transient, $"quic/ws recv failed: {ex.Message}"));
            return null;
        }
    }

    public async Task CloseAsync()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0)
            return;
        // Graceful: send a WS close frame and complete our send direction so the peer drains then
        // reads null. The recv side stays open until the peer's close arrives.
        try { await _ws.CloseAsync().ConfigureAwait(false); }
        catch (Exception ex) when (ex is QuicException or IOException or ObjectDisposedException) { /* already gone */ }
        try { _stream.CompleteWrites(); }
        catch (Exception ex) when (ex is QuicException or ObjectDisposedException) { /* already gone */ }
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        try { await _stream.DisposeAsync().ConfigureAwait(false); } catch { /* best-effort */ }
        try { await _connection.DisposeAsync().ConfigureAwait(false); } catch { /* best-effort */ }
    }
}
