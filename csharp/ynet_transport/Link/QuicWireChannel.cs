using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

// System.Net.Quic is [SupportedOSPlatform] windows/linux/macOS; every entry gates on IsSupported at
// runtime (FR-001), the sanctioned guard — so the CA1416 "reachable on all platforms" advisory does
// not apply here (harvested discipline from csharp/glp_link QuicTransport).
#pragma warning disable CA1416

namespace Ynet.Transport.Link;

/// <summary>
/// A REAL MsQuic <see cref="IWireChannel"/> (US1 / T011) harvested from csharp/glp_link's
/// <c>QuicTransport</c>: a genuine QUIC connection + one bidirectional <see cref="QuicStream"/>,
/// carrying length-prefixed frames, bridged to the synchronous <see cref="IWireChannel"/> the
/// <see cref="YnetSession"/> rides. Real QUIC only (FR-001): every path gates on
/// <see cref="IsSupported"/> and refuses rather than simulating a handshake (Constitution II).
///
/// Trust layering (honest, documented): the QUIC/TLS layer provides transport confidentiality with an
/// ephemeral self-signed cert; the YNET <b>node identity</b> is verified <b>app-layer</b> by
/// <see cref="YnetSession"/>'s signed-ECDH handshake (FR-002) — so the TLS cert is intentionally not
/// the identity anchor. Binding the cert SPKI to the node key (glp_link's pin model, P-256 path) is a
/// tracked refinement, not required for correctness because the app-layer handshake is authenticated.
/// </summary>
public sealed class QuicWireChannel : IWireChannel
{
    private static readonly SslApplicationProtocol Alpn = new("ynet");

    private readonly QuicConnection _connection;
    private readonly QuicStream _stream;
    private readonly X509Certificate2? _cert;
    private readonly BlockingCollection<byte[]> _inbound = new(new ConcurrentQueue<byte[]>());
    private readonly CancellationTokenSource _cts = new();
    private readonly object _writeLock = new();
    private readonly Task _recvLoop;
    private int _closed;

    private QuicWireChannel(QuicConnection connection, QuicStream stream, X509Certificate2? cert)
    {
        _connection = connection;
        _stream = stream;
        _cert = cert;
        _recvLoop = Task.Run(ReceiveLoopAsync);
    }

    /// <summary>True iff this host can perform a genuine QUIC handshake in BOTH roles (FR-001).</summary>
    public static bool IsSupported => QuicListener.IsSupported && QuicConnection.IsSupported;

    // ---- server role ----

    /// <summary>Bind a QUIC listener on loopback (port 0 = pick a free port; read LocalEndPoint).</summary>
    public static async Task<QuicListener> BindListenerAsync(int port = 0, CancellationToken ct = default)
    {
        RequireSupported("listen");
        var cert = MakeEphemeralCert();
        return await QuicListener.ListenAsync(new QuicListenerOptions
        {
            ListenEndPoint = new IPEndPoint(IPAddress.Loopback, port),
            ApplicationProtocols = new List<SslApplicationProtocol> { Alpn },
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                IdleTimeout = TimeSpan.FromMinutes(30),
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ServerCertificate = cert,
                    ApplicationProtocols = new List<SslApplicationProtocol> { Alpn },
                    // TLS layer = transport confidentiality only; YNET identity is verified app-layer
                    // by YnetSession (FR-002). Do not pin/require a client cert here.
                    RemoteCertificateValidationCallback = (_, _, _, _) => true,
                },
            }),
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Accept one client: a genuine QUIC handshake + its first inbound bidi stream.</summary>
    public static async Task<QuicWireChannel> AcceptAsync(QuicListener listener, CancellationToken ct = default)
    {
        var connection = await listener.AcceptConnectionAsync(ct).ConfigureAwait(false);
        var stream = await connection.AcceptInboundStreamAsync(ct).ConfigureAwait(false);
        return new QuicWireChannel(connection, stream, cert: null);
    }

    // ---- client role ----

    /// <summary>Dial a peer's QUIC endpoint on loopback and open the bidirectional stream.</summary>
    public static async Task<QuicWireChannel> ConnectAsync(int port, CancellationToken ct = default)
    {
        RequireSupported("connect");
        var connection = await QuicConnection.ConnectAsync(new QuicClientConnectionOptions
        {
            RemoteEndPoint = new IPEndPoint(IPAddress.Loopback, port),
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            IdleTimeout = TimeSpan.FromMinutes(30),
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = new List<SslApplicationProtocol> { Alpn },
                TargetHost = "ynet", // cosmetic; identity trust is app-layer (YnetSession)
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            },
        }, ct).ConfigureAwait(false);
        var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct).ConfigureAwait(false);
        return new QuicWireChannel(connection, stream, cert: null);
    }

    // ---- IWireChannel (sync bridge) ----

    public void WriteFrame(ReadOnlySpan<byte> frame)
    {
        if (Volatile.Read(ref _closed) != 0) throw new IOException("quic wire channel closed");
        var buf = new byte[4 + frame.Length];
        BinaryPrimitives.WriteUInt32BigEndian(buf, (uint)frame.Length);
        frame.CopyTo(buf.AsSpan(4));
        lock (_writeLock)
        {
            try
            {
                _stream.WriteAsync(buf, _cts.Token).AsTask().GetAwaiter().GetResult();
                _stream.FlushAsync(_cts.Token).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is QuicException or IOException or ObjectDisposedException or OperationCanceledException)
            {
                throw new IOException("quic wire write failed", ex);
            }
        }
    }

    public byte[]? ReadFrame()
    {
        try { return _inbound.Take(); }
        catch (InvalidOperationException) { return null; } // completed + drained
    }

    private async Task ReceiveLoopAsync()
    {
        var header = new byte[4];
        try
        {
            while (!_cts.IsCancellationRequested)
            {
                await _stream.ReadExactlyAsync(header, _cts.Token).ConfigureAwait(false);
                uint len = BinaryPrimitives.ReadUInt32BigEndian(header);
                var payload = new byte[len];
                if (len > 0)
                    await _stream.ReadExactlyAsync(payload, _cts.Token).ConfigureAwait(false);
                _inbound.Add(payload);
            }
        }
        catch (Exception ex) when (ex is EndOfStreamException or QuicException or IOException
                                   or ObjectDisposedException or OperationCanceledException)
        {
            // Peer completed writes / link torn down — normal end of the read side.
        }
        finally
        {
            _inbound.CompleteAdding();
        }
    }

    public void Close()
    {
        if (Interlocked.Exchange(ref _closed, 1) != 0) return;
        try { _stream.CompleteWrites(); } catch (Exception ex) when (ex is QuicException or ObjectDisposedException) { }
        _cts.Cancel();
    }

    public void Dispose()
    {
        Close();
        try { _recvLoop.Wait(TimeSpan.FromSeconds(2)); } catch { /* best-effort */ }
        try { _stream.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        try { _connection.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        _cert?.Dispose();
        _cts.Dispose();
    }

    private static X509Certificate2 MakeEphemeralCert()
    {
        // Harvested Windows-safe pattern (glp_link tests): create self-signed, then re-import from
        // PFX so the private key is usable by the platform TLS stack (Schannel).
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=ynet-node", ec, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;
        using var ephemeral = req.CreateSelfSigned(now.AddMinutes(-5), now.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(
            ephemeral.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    private static void RequireSupported(string op)
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException(
                $"QUIC {op} unavailable: QuicListener.IsSupported={QuicListener.IsSupported}, " +
                $"QuicConnection.IsSupported={QuicConnection.IsSupported}. Real QUIC only — no simulated " +
                "fallback (FR-001).");
    }
}
