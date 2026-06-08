using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;

using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Transports;

/// <summary>
/// The FIRST real cross-process transport (feature 025 runtime-integration): a raw-TCP
/// leaf over IPv4 localhost (127.0.0.1). The <c>server_listener</c> end binds and waits
/// (<see cref="ListenAsync"/>); the <c>client_connector</c> end dials when its goal
/// activates (<see cref="ConnectAsync"/>); the one persistent duplex socket carries the
/// bilateral <c>Link(In, Out)</c> — both ends ship frames whenever they want (FR-003/005).
/// Selected by <see cref="LinkScheme.Tcp"/> from <c>link_id("tcp", ep("127.0.0.1", Port), Nonce)</c>.
/// </summary>
/// <remarks>
/// Iteration 1 is plain TCP on the loopback IP (same host) — no TLS, which FR-029 only
/// requires for INTER-host links; the secure + web leaves (ws/wss/https, T060/T061) are the
/// next iteration. Frames are made self-delimiting over the byte stream by a 4-byte
/// big-endian length prefix (the FrameCodec frame is carried opaquely), so one
/// <see cref="ILinkEndpoint.SendBytesAsync"/> ⇒ one peer <see cref="ILinkEndpoint.RecvBytesAsync"/>
/// (per-link FIFO is then reconstructed above by the reliability sublayer). P2P to the
/// immediate peer; no broker.
/// </remarks>
public sealed class TcpTransport : ILinkTransport
{
    private static readonly LinkScheme[] Schemes = { LinkScheme.Tcp };

    public IReadOnlyCollection<LinkScheme> SupportedSchemes => Schemes;

    public async Task<ILinkEndpoint> ListenAsync(LinkScheme scheme, LinkAddress local, LinkOptions opts, CancellationToken ct = default)
    {
        Require(scheme);
        int port = RequirePort(local, "listen");
        var listener = new TcpListener(ParseIp(local.Host), port);
        listener.Start();
        try
        {
            var client = await listener.AcceptTcpClientAsync(ct).ConfigureAwait(false);
            Configure(client);
            return new TcpEndpoint(new LinkId(LinkScheme.Tcp, local, LinkNonce.Int(port)), client);
        }
        finally
        {
            // One link per listen for the base MVP (a multi-accept loop is a transport-leaf
            // concern, Phase 6); stop the listener so the port is released for re-use.
            listener.Stop();
        }
    }

    public async Task<ILinkEndpoint> ConnectAsync(LinkScheme scheme, LinkAddress remote, LinkOptions opts, CancellationToken ct = default)
    {
        Require(scheme);
        int port = RequirePort(remote, "connect");
        var client = new TcpClient();
        await client.ConnectAsync(ParseIp(remote.Host), port, ct).ConfigureAwait(false);
        Configure(client);
        return new TcpEndpoint(new LinkId(LinkScheme.Tcp, remote, LinkNonce.Int(port)), client);
    }

    private static void Require(LinkScheme scheme)
    {
        if (scheme != LinkScheme.Tcp)
            throw new ArgumentException($"TcpTransport does not serve scheme '{scheme}'", nameof(scheme));
    }

    private static int RequirePort(LinkAddress addr, string op) =>
        addr.Port ?? throw new ArgumentException($"tcp {op} requires an ep(Host, Port) endpoint; got '{addr}'");

    // IPv4 localhost is the supported case (Gabi: "127.0.0.1 is probably the best localhost").
    private static IPAddress ParseIp(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ? IPAddress.Loopback : IPAddress.Parse(host);

    private static void Configure(TcpClient c) => c.NoDelay = true; // ship each frame promptly (disable Nagle)
}

/// <summary>
/// One end of a raw-TCP link: a duplex socket carrying length-prefixed frames. Send and
/// recv run on different threads (the egress drainer writes on the runner thread; the pump's
/// background recv loop reads), which a <see cref="NetworkStream"/> supports for one
/// concurrent reader + one writer; concurrent sends are serialized by a lock.
/// </summary>
internal sealed class TcpEndpoint : ILinkEndpoint
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _closed;

    public LinkId Id { get; }

    public event Action<LinkFaultSignal>? OnFault;

    internal TcpEndpoint(LinkId id, TcpClient client)
    {
        Id = id;
        _client = client;
        _stream = client.GetStream();
    }

    public async Task SendBytesAsync(ReadOnlyMemory<byte> frame, CancellationToken ct = default)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, frame.Length);
        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(header, ct).ConfigureAwait(false);
            await _stream.WriteAsync(frame, ct).ConfigureAwait(false);
            await _stream.FlushAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            // A send failure on a not-yet-closed link is a real transport fault.
            if (Volatile.Read(ref _closed) == 0)
                OnFault?.Invoke(new LinkFaultSignal(Id, LinkFaultKind.Transient, $"tcp send failed: {ex.Message}"));
            throw;
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async Task<byte[]?> RecvBytesAsync(CancellationToken ct = default)
    {
        try
        {
            var header = await ReadExactlyAsync(4, ct).ConfigureAwait(false);
            if (header is null)
                return null; // peer FIN before a new frame → graceful close (→ closed/eos upstream)

            int len = BinaryPrimitives.ReadInt32BigEndian(header);
            if (len < 0)
            {
                OnFault?.Invoke(new LinkFaultSignal(Id, LinkFaultKind.Permanent, "tcp: negative frame length"));
                return null;
            }
            if (len == 0)
                return Array.Empty<byte>();

            var body = await ReadExactlyAsync(len, ct).ConfigureAwait(false);
            if (body is null)
            {
                OnFault?.Invoke(new LinkFaultSignal(Id, LinkFaultKind.Transient, "tcp: peer closed mid-frame"));
                return null;
            }
            return body;
        }
        catch (OperationCanceledException)
        {
            throw; // pump disposed / link torn down — normal shutdown
        }
        catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException)
        {
            // An error on a link WE closed is the expected end of our own teardown — not a fault.
            if (Volatile.Read(ref _closed) == 0)
                OnFault?.Invoke(new LinkFaultSignal(Id, LinkFaultKind.Transient, $"tcp recv failed: {ex.Message}"));
            return null;
        }
    }

    /// <summary>Read exactly <paramref name="count"/> bytes; null if the peer FINs before any byte of the chunk.</summary>
    private async Task<byte[]?> ReadExactlyAsync(int count, CancellationToken ct)
    {
        var buf = new byte[count];
        int off = 0;
        while (off < count)
        {
            int n = await _stream.ReadAsync(buf.AsMemory(off, count - off), ct).ConfigureAwait(false);
            if (n == 0)
            {
                if (off == 0)
                    return null;                                  // clean boundary → graceful close
                throw new IOException("tcp: peer closed mid-frame");
            }
            off += n;
        }
        return buf;
    }

    public Task CloseAsync()
    {
        if (Interlocked.Exchange(ref _closed, 1) == 0)
        {
            // Graceful: send FIN so the peer drains buffered frames then reads null. We keep the
            // socket readable so our own recv loop finishes on the peer's FIN; full disposal is
            // DisposeAsync (or process exit).
            try { _client.Client.Shutdown(SocketShutdown.Send); }
            catch (Exception ex) when (ex is SocketException or ObjectDisposedException) { /* already gone */ }
        }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        try { _stream.Dispose(); } catch { /* best-effort */ }
        try { _client.Dispose(); } catch { /* best-effort */ }
        _sendLock.Dispose();
    }
}
