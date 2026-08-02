using System.Threading.Channels;
using NetMQ;
using NetMQ.Sockets;
using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Transports;

/// <summary>
/// ZeroMQ (NetMQ) transport leaf (feature 062 US3, T020) — the <c>zmq-sender-base</c> +
/// <c>zmq-receiver-base</c> behind the <see cref="ILinkTransport"/> seam. Bilateral
/// point-to-point over a NetMQ <see cref="PairSocket"/> pair: the
/// <c>server_listener</c> end Binds (<see cref="ListenAsync"/>), the
/// <c>client_connector</c> end Connects (<see cref="ConnectAsync"/>); one PAIR link
/// carries the bilateral <c>Link(In, Out)</c>. Selected by <see cref="LinkScheme.Zmq"/>
/// from <c>link_id("zmq", ep(Host, Port), Nonce)</c>.
/// </summary>
/// <remarks>
/// This is the 1:1 base, mirroring <see cref="TcpTransport"/> (D-9 point-to-point; a
/// fan-out ROUTER/DEALER leaf is a separate concern). ZeroMQ <c>Connect</c> is lazy —
/// it tolerates connecting before the peer Binds — so no connect-retry loop is needed
/// (role-order independence, FR-004). Frames are opaque: each carries a 1-byte control
/// tag (<c>0x00</c> data, <c>0x01</c> eos) so empty payloads round-trip and
/// <see cref="ILinkEndpoint.CloseAsync"/> gives the peer a graceful <c>null</c> recv
/// (ZeroMQ has no transport FIN). Each socket is owned by a single
/// <see cref="NetMQPoller"/> thread; sends cross in via a thread-safe
/// <see cref="NetMQQueue{T}"/> and receives cross out via a channel — the socket is
/// never touched from two threads (NetMQ's threading rule).
/// </remarks>
public sealed class ZmqTransport : ILinkTransport
{
    private static readonly LinkScheme[] Schemes = { LinkScheme.Zmq };

    public IReadOnlyCollection<LinkScheme> SupportedSchemes => Schemes;

    public Task<ILinkEndpoint> ListenAsync(LinkScheme scheme, LinkAddress local, LinkOptions opts, CancellationToken ct = default)
    {
        Require(scheme);
        int port = RequirePort(local, "listen");
        ct.ThrowIfCancellationRequested();

        var socket = new PairSocket();
        socket.Bind($"tcp://{Ip(local.Host)}:{port}");
        return Task.FromResult<ILinkEndpoint>(
            new ZmqEndpoint(new LinkId(LinkScheme.Zmq, local, LinkNonce.Int(port)), socket));
    }

    public Task<ILinkEndpoint> ConnectAsync(LinkScheme scheme, LinkAddress remote, LinkOptions opts, CancellationToken ct = default)
    {
        Require(scheme);
        int port = RequirePort(remote, "connect");
        ct.ThrowIfCancellationRequested();

        var socket = new PairSocket();
        socket.Connect($"tcp://{Ip(remote.Host)}:{port}"); // lazy: no listener race
        return Task.FromResult<ILinkEndpoint>(
            new ZmqEndpoint(new LinkId(LinkScheme.Zmq, remote, LinkNonce.Int(port)), socket));
    }

    private static void Require(LinkScheme scheme)
    {
        if (scheme != LinkScheme.Zmq)
            throw new ArgumentException($"ZmqTransport does not serve scheme '{scheme}'", nameof(scheme));
    }

    private static int RequirePort(LinkAddress addr, string op) =>
        addr.Port ?? throw new ArgumentException($"zmq {op} requires an ep(Host, Port) endpoint; got '{addr}'");

    private static string Ip(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ? "127.0.0.1" : host;
}

/// <summary>
/// One end of a ZeroMQ PAIR link. A single <see cref="NetMQPoller"/> owns the socket;
/// <see cref="SendBytesAsync"/> and <see cref="CloseAsync"/> only enqueue into a
/// thread-safe <see cref="NetMQQueue{T}"/> (the poller drains it and calls the socket),
/// and <see cref="RecvBytesAsync"/> only reads a <see cref="Channel{T}"/> the poller
/// fills — so the socket itself is single-threaded.
/// </summary>
internal sealed class ZmqEndpoint : ILinkEndpoint
{
    private const byte TagData = 0x00;
    private const byte TagEos = 0x01;

    private readonly PairSocket _socket;
    private readonly NetMQPoller _poller;
    private readonly NetMQQueue<byte[]> _outbound;   // already tagged frames to send
    private readonly Channel<byte[]> _inbound;       // received payloads (tag stripped)
    private int _closed;

    public LinkId Id { get; }

    public event Action<LinkFaultSignal>? OnFault;

    internal ZmqEndpoint(LinkId id, PairSocket socket)
    {
        Id = id;
        _socket = socket;
        _outbound = new NetMQQueue<byte[]>();
        _inbound = Channel.CreateUnbounded<byte[]>(
            new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

        _socket.ReceiveReady += OnSocketReceiveReady;
        _outbound.ReceiveReady += OnOutboundReady;

        _poller = new NetMQPoller { _socket, _outbound };
        _poller.RunAsync();
    }

    private void OnOutboundReady(object? sender, NetMQQueueEventArgs<byte[]> e)
    {
        while (e.Queue.TryDequeue(out var frame, TimeSpan.Zero))
        {
            try
            {
                _socket.SendFrame(frame);
            }
            catch (Exception ex)
            {
                if (Volatile.Read(ref _closed) == 0)
                    OnFault?.Invoke(new LinkFaultSignal(Id, LinkFaultKind.Transient, $"zmq send failed: {ex.Message}"));
            }
        }
    }

    private void OnSocketReceiveReady(object? sender, NetMQSocketEventArgs e)
    {
        while (e.Socket.TryReceiveFrameBytes(out var bytes))
        {
            if (bytes.Length >= 1 && bytes[0] == TagEos)
            {
                _inbound.Writer.TryComplete(); // peer graceful close → drain then null
                continue;
            }
            // Strip the data tag; a bare tag byte denotes an empty payload.
            var payload = bytes.Length >= 1 ? bytes[1..] : Array.Empty<byte>();
            _inbound.Writer.TryWrite(payload);
        }
    }

    public Task SendBytesAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (Volatile.Read(ref _closed) != 0)
            throw new InvalidOperationException("zmq: send on a closed link");

        var tagged = new byte[frame.Length + 1];
        tagged[0] = TagData;
        frame.Span.CopyTo(tagged.AsSpan(1));
        _outbound.Enqueue(tagged);
        return Task.CompletedTask;
    }

    public async Task<byte[]?> RecvBytesAsync(CancellationToken ct = default)
    {
        // Drains buffered payloads first; returns null once the inbound channel is
        // completed (peer EOS) and empty.
        if (await _inbound.Reader.WaitToReadAsync(ct).ConfigureAwait(false)
            && _inbound.Reader.TryRead(out var payload))
            return payload;
        return null;
    }

    public Task CloseAsync()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0)
            _outbound.Enqueue(new[] { TagEos }); // FIFO after any pending data frames
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        // Let the EOS enqueued by CloseAsync flush before we tear the poller down.
        await Task.Delay(20).ConfigureAwait(false);
        try { if (_poller.IsRunning) _poller.Stop(); } catch { /* best-effort */ }
        try { _poller.Dispose(); } catch { /* best-effort */ }
        try { _socket.Dispose(); } catch { /* best-effort */ }
        try { _outbound.Dispose(); } catch { /* best-effort */ }
    }
}
