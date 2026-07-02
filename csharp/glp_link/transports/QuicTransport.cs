using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using GlpRuntime.Link.Seam;

// System.Net.Quic is [SupportedOSPlatform] windows/linux/macOS. We gate every entry on
// QuicTransport.IsSupported at runtime (FR-001), which is the sanctioned guard — so the CA1416
// "reachable on all platforms" advisory does not apply here.
#pragma warning disable CA1416

namespace GlpRuntime.Link.Transports;

/// <summary>
/// The genuine HTTP/3 (QUIC) + WebSocket transport leaf (feature 036) — a new
/// <see cref="ILinkTransport"/> alongside <see cref="LoopbackTransport"/> / <see cref="TcpTransport"/>,
/// reusing spec 025's seam, reliability sublayer, and ground-relay discipline for free (FR-018).
/// Selected by <see cref="LinkScheme.Quic"/>.
/// </summary>
/// <remarks>
/// Real QUIC only (FR-001): every path gates on <see cref="IsSupported"/> and refuses to claim a
/// handshake the platform cannot perform — no loopback/simulated fallback. Trust is the <b>shared
/// self-signed cert pinned by SPKI SHA-256</b>, validated <b>mutually</b> (both ends present the
/// shared cert and pin the peer's) — the validation callback NEVER returns <c>true</c> blindly; it
/// waives only the no-CA-chain + hostname-mismatch errors (FR-003/SC-005). ALPN is <c>h3</c>. The
/// WebSocket link is genuine RFC 6455 framing over one bidi <see cref="QuicStream"/>
/// (<see cref="WebSocketOverQuic"/>) brought up by the minimal CONNECT-style
/// <see cref="ConnectBootstrap"/>; the RFC 9220 Extended-CONNECT bootstrap stays isolated behind the
/// bootstrap seam (later browser interop only, Decision 3).
/// </remarks>
public sealed class QuicTransport : ILinkTransport
{
    private static readonly LinkScheme[] Schemes = { LinkScheme.Quic };

    // Both ends share this cert (a shared secret). The server presents it; the client presents it
    // too (mutual auth). Possession of the cert+key proves membership; the SPKI pin is the anchor.
    private readonly X509Certificate2 _sharedCert;
    private readonly string _expectedSpkiPin;

    /// <summary>
    /// Configure the leaf with the shared trust material (loaded from the cert dir by the host /
    /// adapter). <paramref name="sharedCert"/> must carry the private key (server + client present it);
    /// <paramref name="expectedSpkiPin"/> is the pinned <c>base64(SHA-256(SPKI))</c> value.
    /// </summary>
    public QuicTransport(X509Certificate2 sharedCert, string expectedSpkiPin)
    {
        _sharedCert = sharedCert ?? throw new ArgumentNullException(nameof(sharedCert));
        if (string.IsNullOrWhiteSpace(expectedSpkiPin))
            throw new ArgumentException("a non-empty SPKI pin is required", nameof(expectedSpkiPin));
        _expectedSpkiPin = expectedSpkiPin.Trim();
    }

    public IReadOnlyCollection<LinkScheme> SupportedSchemes => Schemes;

    /// <summary>True iff this host can perform a genuine QUIC handshake in BOTH roles (FR-001).</summary>
    public static bool IsSupported => QuicListener.IsSupported && QuicConnection.IsSupported;

    /// <summary>The SPKI SHA-256 pin of a cert: <c>base64(SHA-256(DER SubjectPublicKeyInfo))</c>.
    /// Identical to the Python tool's pin (cert.py) so either end validates the other.</summary>
    public static string SpkiPin(X509Certificate2 cert) =>
        Convert.ToBase64String(SHA256.HashData(cert.PublicKey.ExportSubjectPublicKeyInfo()));

    public async Task<ILinkEndpoint> ListenAsync(LinkScheme scheme, LinkAddress local, LinkOptions opts, CancellationToken ct = default)
    {
        Require(scheme);
        RequireQuicSupported("listen");
        int port = RequirePort(local, "listen");

        var listener = await QuicListener.ListenAsync(BuildListenerOptions(local, port), ct).ConfigureAwait(false);
        try
        {
            return await AcceptOneAsync(listener, local, ct).ConfigureAwait(false);
        }
        finally
        {
            // One link per listen for the seam contract (the multi-accept mesh server uses
            // CreateListenerAsync, US2 T025); dispose the listener so the UDP port releases.
            await listener.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Bind one QUIC listener and hand back a <see cref="QuicListenerHandle"/> that accepts MANY
    /// isolated client links from the same UDP port (US2 T025) — each its own
    /// <see cref="QuicConnection"/> + bidi stream + WS link, so one link's fault never touches a
    /// sibling (FR-005/FR-006/SC-004). The single-link <see cref="ListenAsync"/> is the seam form;
    /// this is the server-role form.
    /// </summary>
    public async Task<QuicListenerHandle> CreateListenerAsync(LinkAddress local, LinkOptions opts, CancellationToken ct = default)
    {
        RequireQuicSupported("listen");
        int port = RequirePort(local, "listen");
        var listener = await QuicListener.ListenAsync(BuildListenerOptions(local, port), ct).ConfigureAwait(false);
        return new QuicListenerHandle(listener, local);
    }

    private QuicListenerOptions BuildListenerOptions(LinkAddress local, int port) => new()
    {
        ListenEndPoint = new IPEndPoint(ParseBindIp(local.Host), port),
        ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
        ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
        {
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            IdleTimeout = TimeSpan.FromHours(2), // a chat link is idle between messages — don't auto-close it
            ServerAuthenticationOptions = new SslServerAuthenticationOptions
            {
                ServerCertificate = _sharedCert,
                ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                ClientCertificateRequired = true, // mutual: validate the peer's cert by pin too
                RemoteCertificateValidationCallback = PinValidationCallback,
            },
        }),
    };

    private static async Task<ILinkEndpoint> AcceptOneAsync(QuicListener listener, LinkAddress local, CancellationToken ct)
    {
        var connection = await listener.AcceptConnectionAsync(ct).ConfigureAwait(false);
        var stream = await connection.AcceptInboundStreamAsync(ct).ConfigureAwait(false);
        var ws = await ConnectBootstrap.BootstrapAsync(stream, isConnector: false, ct).ConfigureAwait(false);
        var nonce = LinkNonce.Str(connection.RemoteEndPoint?.ToString() ?? Guid.NewGuid().ToString());
        return new QuicEndpoint(new LinkId(LinkScheme.Quic, local, nonce), connection, stream, ws);
    }

    /// <summary>A bound QUIC listener that accepts many isolated client links (US2 T025).</summary>
    public sealed class QuicListenerHandle : IAsyncDisposable
    {
        private readonly QuicListener _listener;
        private readonly LinkAddress _local;

        internal QuicListenerHandle(QuicListener listener, LinkAddress local)
        {
            _listener = listener;
            _local = local;
        }

        /// <summary>Accept the next client: a genuine per-client QUIC handshake + WS bootstrap.</summary>
        public Task<ILinkEndpoint> AcceptAsync(CancellationToken ct = default) =>
            AcceptOneAsync(_listener, _local, ct);

        public ValueTask DisposeAsync() => _listener.DisposeAsync();
    }

    public async Task<ILinkEndpoint> ConnectAsync(LinkScheme scheme, LinkAddress remote, LinkOptions opts, CancellationToken ct = default)
    {
        Require(scheme);
        RequireQuicSupported("connect");
        int port = RequirePort(remote, "connect");
        var endpoint = new IPEndPoint(ResolveHost(remote.Host), port);

        var clientOptions = new QuicClientConnectionOptions
        {
            RemoteEndPoint = endpoint,
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            IdleTimeout = TimeSpan.FromHours(2), // a chat link is idle between messages — don't auto-close it
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = new List<SslApplicationProtocol> { SslApplicationProtocol.Http3 },
                TargetHost = "glp-quick", // cosmetic; trust is the pin, name mismatch is waived
                RemoteCertificateValidationCallback = PinValidationCallback,
                ClientCertificates = new X509CertificateCollection { _sharedCert }, // present the shared cert (mutual)
            },
        };

        var connection = await QuicConnection.ConnectAsync(clientOptions, ct).ConfigureAwait(false);
        var stream = await connection.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct).ConfigureAwait(false);
        // The outbound stream is not realized on the wire until first write — the bootstrap request
        // (connector writes first) opens it and unblocks the listener's AcceptInboundStreamAsync.
        var ws = await ConnectBootstrap.BootstrapAsync(stream, isConnector: true, ct).ConfigureAwait(false);
        return new QuicEndpoint(new LinkId(LinkScheme.Quic, remote, LinkNonce.Int(port)), connection, stream, ws);
    }

    /// <summary>
    /// The shared-cert SPKI pin check (FR-003). Waives ONLY the no-CA-chain + hostname-mismatch
    /// errors (trust is the pin, not the name); never blanket-accepts. A pin mismatch ⇒ reject ⇒
    /// the handshake fails cleanly (the <c>cert_mismatch</c> path, FR-019).
    /// </summary>
    private bool PinValidationCallback(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors errors)
    {
        if (certificate is null)
            return false; // no peer cert — cannot pin
        const SslPolicyErrors waived = SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch;
        if ((errors & ~waived) != SslPolicyErrors.None)
            return false; // e.g. RemoteCertificateNotAvailable — a real failure, not waivable
        var presented = certificate as X509Certificate2 ?? X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(SpkiPin(presented)),
            System.Text.Encoding.ASCII.GetBytes(_expectedSpkiPin));
    }

    private static void Require(LinkScheme scheme)
    {
        if (scheme != LinkScheme.Quic)
            throw new ArgumentException($"QuicTransport does not serve scheme '{scheme}'", nameof(scheme));
    }

    private static int RequirePort(LinkAddress addr, string op) =>
        addr.Port ?? throw new ArgumentException($"quic {op} requires an ep(Host, Port) endpoint; got '{addr}'");

    /// <summary>Refuse — clearly, not silently — when the platform cannot do real QUIC (FR-001/FR-019).</summary>
    private static void RequireQuicSupported(string op)
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException(
                $"QUIC {op} unavailable: QuicListener.IsSupported={QuicListener.IsSupported}, "
                + $"QuicConnection.IsSupported={QuicConnection.IsSupported}. A real handshake requires msquic "
                + "in the .NET runtime. Real QUIC only — no loopback/simulated fallback (FR-001).");
    }

    private static IPAddress ParseBindIp(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ? IPAddress.Loopback
        : string.Equals(host, "any", StringComparison.OrdinalIgnoreCase) ? IPAddress.Any
        : IPAddress.TryParse(host, out var ip) ? ip
        : IPAddress.Any; // a non-IP bind host (machine name) → bind all interfaces

    private static IPAddress ResolveHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            return IPAddress.Loopback;
        if (IPAddress.TryParse(host, out var ip))
            return ip;
        // Machine-name addressing (FR-004 "IP vs machine-name"): resolve to an IPv4 address.
        var addrs = Dns.GetHostAddresses(host);
        foreach (var a in addrs)
            if (a.AddressFamily == AddressFamily.InterNetwork)
                return a;
        return addrs.Length > 0 ? addrs[0] : throw new ArgumentException($"cannot resolve host '{host}'");
    }
}
