// Real QUIC adapter behind the link-transport seam (048 bk-colab-yngenios-transport, T013 —
// the deferred 041 T052, now required in MVP per 048 D3 / contracts/peer-link.md).
//
// One System.Net.Quic (MsQuic) connection per pinned peer, multiplexed PER BOX: every box rides its
// own QUIC stream, so one box's ops never head-of-line-block another's (peer-link.md "per-box
// multiplexing"). Trust is SPKI public-key pinning PER PEER (FR-008): a dialed peer must present the
// cert whose base64(SHA-256(SPKI)) pin we hold for it; an accepted peer must present a pin in our
// pinned set AND its hello-claimed name must map to that same pin — an un-pinned peer's link is
// REFUSED (US4 AS-2). Dev trust material is a self-signed cert (CreateDevCert) + SpkiPin, reusing the
// shipped glp_link QuicTransport pin discipline (REUSE; the pin is layer-0 MEMBERSHIP, not identity —
// FR-019).
//
// ⚠ CONTRACT DEVIATION (flagged): peer-link.md asks for presence via RFC 9221 unreliable DATAGRAM
// frames. System.Net.Quic through .NET 10 does NOT expose the datagram extension (verified against
// the net10.0 ref assembly — no Datagram members), so presence pings ride a short-lived
// UNIDIRECTIONAL stream instead: still advisory, still best-effort (failures swallowed), same
// no-second-socket property. The seam (`SendPresencePingAsync`/`LastSeen`) is shaped so a real
// datagram implementation drops in when the runtime exposes one.
//
// Connection migration (peer-link.md): MsQuic performs QUIC path migration below this adapter; the
// adapter keys ALL state by authenticated peer-name — never by remote address — so an address change
// mid-link disturbs nothing here (migration tolerance by construction). The multi-host migration
// acceptance run is the gated real-LAN tier (D3), not a loopback unit test.
//
// Wire shape (this adapter's private protocol, all integers 4-byte LE, strings len-prefixed UTF-8):
//   control stream (bidi, opened by the dialer): [0x00][hello: local peer]  →  reply [remote peer]
//   box stream     (uni, one per (peer, box)):   [0x01][box][frame][frame]…  frame = [len][bytes]
//   presence ping  (uni, short-lived):           [0x02][8-byte unix-ms]

using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Quic;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Channels;

using GlpRuntime.CrdtMsg.Crdt;
using GlpRuntime.CrdtMsg.Envelope;

// Gated on IsSupported at every entry (the glp_link QuicTransport convention) — the CA1416
// "reachable on all platforms" advisory does not apply.
#pragma warning disable CA1416

namespace GlpRuntime.CrdtMsg.Route;

public sealed class QuicLinkTransport : IBoxLinkTransport, IAsyncDisposable
{
    private const byte KindControl = 0x00, KindBox = 0x01, KindPresence = 0x02;
    private const int MaxFrameBytes = 64 * 1024 * 1024; // the glp_link 64 MiB frame guard

    // System.Net.Quic defaults BOTH inbound stream budgets to 0 (nothing acceptable) — both roles
    // must grant credit: bidi for the control hello, uni for the per-box lanes + presence pings.
    private const int MaxInboundBidi = 16;
    private const int MaxInboundUni = 512;
    private static readonly SslApplicationProtocol Alpn = new("glp-colab");

    private readonly X509Certificate2 _cert;
    private readonly IReadOnlyDictionary<string, string> _peerPins; // peer-name → base64(SHA-256(SPKI))
    private readonly Channel<LinkInbound> _inbound = Channel.CreateUnbounded<LinkInbound>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly ConcurrentDictionary<string, QuicConnection> _conns = new();
    private readonly ConcurrentDictionary<(string Peer, string Box), Lane> _lanes = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSeen = new();
    private readonly CancellationTokenSource _life = new();
    private QuicListener? _listener;
    private Task? _acceptLoop;

    private sealed record Lane(QuicStream Stream, SemaphoreSlim Gate);

    /// <summary>
    /// <paramref name="peerPins"/> is the SPKI pinning hook (peer-link.md admission gate): the
    /// per-peer trust set — in production the `colab_peer.spki_pin` rows; in dev/tests an explicit
    /// map. A peer absent from it can never link.
    /// </summary>
    public QuicLinkTransport(string localPeer, X509Certificate2 cert, IReadOnlyDictionary<string, string> peerPins)
    {
        LocalPeer = localPeer;
        _cert = cert ?? throw new ArgumentNullException(nameof(cert));
        _peerPins = peerPins ?? throw new ArgumentNullException(nameof(peerPins));
    }

    /// <summary>True iff this host can perform a genuine QUIC handshake in both roles.</summary>
    public static bool IsSupported => QuicListener.IsSupported && QuicConnection.IsSupported;

    /// <summary>The SPKI SHA-256 pin of a cert — delegated to the shipped glp_link discipline (REUSE).</summary>
    public static string SpkiPin(X509Certificate2 cert) => GlpRuntime.Link.Transports.QuicTransport.SpkiPin(cert);

    /// <summary>
    /// The FEDERATION trust anchor: a dev cert loaded from a per-host keystore, generated only on
    /// first run - so this host's SPKI pin is STABLE ACROSS REBOOTS.
    /// <para>
    /// CONVERGED 2026-09-05: this lane and @gavriella-glpnet independently built the same fix in
    /// the same hours. The BODY is now one implementation - the shared
    /// <see cref="GlpRuntime.Link.Transports.FederationIdentity"/> in glp_link, beside the SpkiPin
    /// discipline both callers already delegate to - while THIS signature and its origin contract
    /// are kept verbatim so no existing caller or test changes. Two implementations of one trust
    /// anchor is the fork this method exists to prevent, at the source level instead of the file level.
    /// </para>
    /// </summary>
    /// <remarks>
    /// 🔴 WHY THIS EXISTS, and why <see cref="CreateDevCert"/> must NOT be used for federation.
    /// <c>CreateDevCert</c> mints a fresh P-256 keypair on every call (its local is named
    /// <c>ephemeral</c> and means it). That is correct for a test, and fatal as a fleet trust anchor:
    /// @ariellas-glpnet ran the probe five times on one host, changing nothing, and got FIVE
    /// DIFFERENT PINS (2026-09-04T17:45Z). A pin table exchanged before a reboot is invalid for every
    /// host the moment they come back, and mTLS then refuses EVERY peer — which is indistinguishable
    /// from a dead transport, and would re-open a question two probes have already settled.
    /// The defect was never in <c>CreateDevCert</c>; it was adopting a TEST helper as the anchor.
    ///
    /// Concurrency: the claim is an atomic same-directory RENAME, so if two lanes start together the
    /// loser loads the winner's file rather than overwriting it — both end up on ONE pin, which is the
    /// whole point. Never "last writer wins" here: that silently re-forks the identity.
    ///
    /// 🔴 ONE BEHAVIOURAL CHANGE FROM THE PRE-CONVERGENCE VERSION, STATED RATHER THAN SLIPPED IN.
    /// That version re-minted a keypair when the stored anchor was within a day of expiry and reported
    /// it as <c>"recreated-expired"</c>. The converged implementation REFUSES instead: an expired
    /// anchor throws with an instruction, because re-minting is a rotation, a rotation invalidates
    /// every peer's table, and a rotation nobody asked for is the failure this whole feature exists to
    /// prevent — arriving on a timer instead of on a restart. The anchor's life is 10 years, so the
    /// branch is close to unreachable either way; the difference is what happens when it IS reached.
    /// <c>"recreated-expired"</c> therefore no longer occurs. @gavriella-glpnet: say so if you want the
    /// re-mint back and it goes back — this is a converged decision, not a silent override.
    /// </remarks>
    /// <param name="commonName">the stable per-host identity, e.g. the host name</param>
    /// <param name="origin">"loaded" | "created" — for the caller to log/publish</param>
    /// <param name="keystorePath">override; default $GLPNET_FEDERATION_KEYSTORE, else LocalAppData/glpnet/federation</param>
    public static X509Certificate2 LoadOrCreateDevCert(string commonName, out string origin, string? keystorePath = null)
    {
        var identity = GlpRuntime.Link.Transports.FederationIdentity.LoadOrCreate(commonName, keystorePath);
        origin = identity.Created ? "created" : "loaded";
        return identity.Cert;
    }

    /// <summary>
    /// The same anchor as <see cref="LoadOrCreateDevCert"/>, returning the whole identity rather than
    /// only the certificate: the pin, the <c>node_id</c> and the SPKI, all derived from one keypair.
    /// Prefer this where a pin is PUBLISHED - a pin and a node id are the same 32 bytes in different
    /// encodings, and a pin is a hash so it cannot verify a signature (@gavriella-glpnet, 19:30Z).
    /// </summary>
    public static GlpRuntime.Link.Transports.FederationIdentity LoadFederationIdentity(
        string commonName, string? keystoreDir = null, bool rotate = false) =>
        GlpRuntime.Link.Transports.FederationIdentity.LoadOrCreate(commonName, keystoreDir, rotate);

    /// <summary>
    /// An EPHEMERAL self-signed ECDSA P-256 dev cert (server+client EKU), PFX round-tripped so the
    /// private key is in a form MsQuic/SChannel accepts.
    /// </summary>
    /// <remarks>
    /// 🔴 A FRESH KEYPAIR ON EVERY CALL, so the SPKI pin CHANGES EVERY TIME. Correct for a test that
    /// wants two unrelated identities; WRONG as a federation trust anchor —
    /// use <see cref="LoadOrCreateDevCert"/> for anything whose pin another host holds.
    /// </remarks>
    public static X509Certificate2 CreateDevCert(string commonName)
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest($"CN={commonName}", ec, HashAlgorithmName.SHA256);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, false));
        var now = DateTimeOffset.UtcNow;
        using var ephemeral = req.CreateSelfSigned(now.AddMinutes(-1), now.AddDays(30));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx), null,
            X509KeyStorageFlags.Exportable);
    }

    // ---------------------------------------------------------------- ILinkTransport surface

    public string LocalPeer { get; }

    /// <summary>Layer-0 membership: the peers currently linked (pin-admitted), plus this endpoint.</summary>
    public IReadOnlyCollection<string> Members => _conns.Keys.Append(LocalPeer).ToArray();

    public ChannelReader<LinkInbound> Inbound => _inbound.Reader;

    /// <summary>
    /// A stable identity for the CURRENT connection to <paramref name="peer"/>, or null if there is
    /// none. Changes whenever the underlying QuicConnection is replaced, which is precisely the
    /// event that must invalidate a cached capability declaration.
    /// </summary>
    public string? SessionIdOf(string peer) =>
        _conns.TryGetValue(peer, out var c)
            ? System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(c).ToString("x8")
            : null;

    /// <summary>Advisory liveness: when a presence ping from <paramref name="peer"/> last arrived.</summary>
    public DateTimeOffset? LastSeen(string peer) => _lastSeen.TryGetValue(peer, out var t) ? t : null;

    public ValueTask SendAsync(string toPeer, ReadOnlyMemory<byte> frameBytes, CancellationToken ct = default) =>
        SendAsync(toPeer, Op.DefaultBox, frameBytes, ct);

    public async ValueTask SendAsync(string toPeer, string box, ReadOnlyMemory<byte> frameBytes, CancellationToken ct = default)
    {
        if (frameBytes.Length > MaxFrameBytes)
            throw new CrdtMsgException($"frame of {frameBytes.Length} bytes exceeds the {MaxFrameBytes} guard");
        Lane lane = await GetLaneAsync(toPeer, box, ct).ConfigureAwait(false);
        await lane.Gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, frameBytes.Length);
            await lane.Stream.WriteAsync(header, ct).ConfigureAwait(false);
            await lane.Stream.WriteAsync(frameBytes, ct).ConfigureAwait(false);
            await lane.Stream.FlushAsync(ct).ConfigureAwait(false);
        }
        catch (QuicException)
        {
            // the lane's stream is gone (link churn) — evict it so the next send opens a fresh one
            _lanes.TryRemove(new KeyValuePair<(string, string), Lane>((toPeer, box), lane));
            throw;
        }
        finally
        {
            lane.Gate.Release();
        }
    }

    /// <summary>
    /// Best-effort advisory presence ping (peer-link.md liveness — see the flagged RFC 9221 deviation
    /// in the header). Never throws; a lost/failed ping is exactly as meaningful as a lost datagram.
    /// </summary>
    public async ValueTask SendPresencePingAsync(string toPeer, CancellationToken ct = default)
    {
        if (!_conns.TryGetValue(toPeer, out var conn)) return; // no link — nothing to advise
        try
        {
            var ping = await conn.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, ct).ConfigureAwait(false);
            await using (ping.ConfigureAwait(false))
            {
                var buf = new byte[9];
                buf[0] = KindPresence;
                BinaryPrimitives.WriteInt64LittleEndian(buf.AsSpan(1), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                await ping.WriteAsync(buf, ct).ConfigureAwait(false);
                ping.CompleteWrites();
            }
        }
        catch
        {
            // advisory, not a delivery guarantee — swallow (the unreliable-datagram semantic)
        }
    }

    // ---------------------------------------------------------------- link establishment

    /// <summary>Bind the QUIC listener and start admitting pinned peers (server role).</summary>
    public async Task ListenAsync(IPEndPoint bind, CancellationToken ct = default)
    {
        RequireSupported("listen");
        if (_listener is not null) throw new InvalidOperationException("already listening");
        _listener = await QuicListener.ListenAsync(new QuicListenerOptions
        {
            ListenEndPoint = bind,
            ApplicationProtocols = new List<SslApplicationProtocol> { Alpn },
            ConnectionOptionsCallback = (_, _, _) => ValueTask.FromResult(new QuicServerConnectionOptions
            {
                DefaultStreamErrorCode = 0,
                DefaultCloseErrorCode = 0,
                IdleTimeout = TimeSpan.FromHours(2),
                MaxInboundBidirectionalStreams = MaxInboundBidi,
                MaxInboundUnidirectionalStreams = MaxInboundUni,
                ServerAuthenticationOptions = new SslServerAuthenticationOptions
                {
                    ServerCertificate = _cert,
                    ApplicationProtocols = new List<SslApplicationProtocol> { Alpn },
                    ClientCertificateRequired = true, // mutual: the dialer's cert is pin-checked too
                    RemoteCertificateValidationCallback = (_, c, _, e) => PinAdmits(c, e, expectedPin: null),
                },
            }),
        }, ct).ConfigureAwait(false);
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_life.Token), CancellationToken.None);
    }

    /// <summary>The listener's bound endpoint (for OS-assigned ports in tests).</summary>
    public IPEndPoint? ListenEndPoint => _listener?.LocalEndPoint;

    /// <summary>
    /// Dial a pinned peer and complete the hello handshake. Refuses (never links) when
    /// <paramref name="peerName"/> is not in the pin set, when the presented cert does not match its
    /// pin, or when the far end's hello does not claim the dialed name.
    /// </summary>
    public async Task ConnectPeerAsync(string peerName, IPEndPoint remote, CancellationToken ct = default)
    {
        RequireSupported("connect");
        if (!_peerPins.TryGetValue(peerName, out var expectedPin))
            throw new InvalidOperationException($"peer '{peerName}' is not SPKI-pinned — link refused (FR-008)");

        var conn = await QuicConnection.ConnectAsync(new QuicClientConnectionOptions
        {
            RemoteEndPoint = remote,
            DefaultStreamErrorCode = 0,
            DefaultCloseErrorCode = 0,
            IdleTimeout = TimeSpan.FromHours(2),
            MaxInboundBidirectionalStreams = MaxInboundBidi,
            MaxInboundUnidirectionalStreams = MaxInboundUni,
            ClientAuthenticationOptions = new SslClientAuthenticationOptions
            {
                ApplicationProtocols = new List<SslApplicationProtocol> { Alpn },
                TargetHost = "glp-colab", // cosmetic; trust is the per-peer pin, name mismatch is waived
                RemoteCertificateValidationCallback = (_, c, _, e) => PinAdmits(c, e, expectedPin),
                ClientCertificates = new X509CertificateCollection { _cert },
            },
        }, ct).ConfigureAwait(false);

        try
        {
            var control = await conn.OpenOutboundStreamAsync(QuicStreamType.Bidirectional, ct).ConfigureAwait(false);
            await using (control.ConfigureAwait(false))
            {
                await control.WriteAsync(new[] { KindControl }, ct).ConfigureAwait(false);
                await WriteStringAsync(control, LocalPeer, ct).ConfigureAwait(false);
                await control.FlushAsync(ct).ConfigureAwait(false);
                string remoteName = await ReadStringAsync(control, ct).ConfigureAwait(false);
                if (remoteName != peerName)
                    throw new CrdtMsgException($"dialed '{peerName}' but the far end claims '{remoteName}' — link refused");
                control.CompleteWrites();
            }
            Register(peerName, conn);
        }
        catch
        {
            await conn.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            QuicConnection conn;
            try { conn = await _listener!.AcceptConnectionAsync(ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
            catch (QuicException) { continue; } // one failed handshake never stops admission
            _ = Task.Run(() => HandleAcceptedAsync(conn, ct), CancellationToken.None);
        }
    }

    private async Task HandleAcceptedAsync(QuicConnection conn, CancellationToken ct)
    {
        try
        {
            var control = await conn.AcceptInboundStreamAsync(ct).ConfigureAwait(false);
            await using (control.ConfigureAwait(false))
            {
                byte kind = await ReadByteAsync(control, ct).ConfigureAwait(false);
                if (kind != KindControl)
                    throw new CrdtMsgException($"first stream must be the control hello; got kind 0x{kind:X2}");
                string claimed = await ReadStringAsync(control, ct).ConfigureAwait(false);

                // Admission (FR-008): the TLS layer admitted SOME pinned cert; now bind the claimed
                // name to it — the presented cert's pin must be exactly the claimed peer's pin.
                string presentedPin = SpkiPin(RemoteCert(conn));
                if (!_peerPins.TryGetValue(claimed, out var expected) || !FixedTimeEquals(presentedPin, expected))
                    throw new CrdtMsgException($"hello claims '{claimed}' but the presented cert does not match its pin — link refused");

                await WriteStringAsync(control, LocalPeer, ct).ConfigureAwait(false);
                await control.FlushAsync(ct).ConfigureAwait(false);
                control.CompleteWrites();
                Register(claimed, conn);
            }
        }
        catch
        {
            await conn.DisposeAsync().ConfigureAwait(false); // refused/failed link — never registered
        }
    }

    /// <summary>Key the connection by authenticated peer-NAME (never address — migration tolerance) and start its pump.</summary>
    private void Register(string peer, QuicConnection conn)
    {
        _conns[peer] = conn;
        _lastSeen[peer] = DateTimeOffset.UtcNow;
        _ = Task.Run(() => PumpConnectionAsync(peer, conn, _life.Token), CancellationToken.None);
    }

    // ---------------------------------------------------------------- inbound pumps

    private async Task PumpConnectionAsync(string peer, QuicConnection conn, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var stream = await conn.AcceptInboundStreamAsync(ct).ConfigureAwait(false);
                _ = Task.Run(() => PumpStreamAsync(peer, stream, ct), CancellationToken.None);
            }
        }
        catch
        {
            // connection closed/faulted — the peer drops out of Members; anti-entropy re-converges on relink
            _conns.TryRemove(new KeyValuePair<string, QuicConnection>(peer, conn));
        }
    }

    private async Task PumpStreamAsync(string peer, QuicStream stream, CancellationToken ct)
    {
        try
        {
            await using (stream.ConfigureAwait(false))
            {
                byte kind = await ReadByteAsync(stream, ct).ConfigureAwait(false);
                switch (kind)
                {
                    case KindBox:
                        string box = await ReadStringAsync(stream, ct).ConfigureAwait(false);
                        while (await ReadBlockAsync(stream, ct).ConfigureAwait(false) is { } frame)
                            await _inbound.Writer.WriteAsync(new LinkInbound(peer, frame, box), ct).ConfigureAwait(false);
                        break;
                    case KindPresence:
                        _ = await ReadBlockRawAsync(stream, 8, ct).ConfigureAwait(false); // ts payload — advisory
                        _lastSeen[peer] = DateTimeOffset.UtcNow;
                        break;
                    default:
                        throw new CrdtMsgException($"unknown stream kind 0x{kind:X2} from '{peer}'");
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (QuicException) { /* stream/conn torn down — box streams are re-opened on next send */ }
        catch (CrdtMsgException) { /* malformed stream — drop the stream, never the link */ }
    }

    // ---------------------------------------------------------------- lanes (per-box mux)

    private async ValueTask<Lane> GetLaneAsync(string peer, string box, CancellationToken ct)
    {
        if (_lanes.TryGetValue((peer, box), out var lane)) return lane;
        if (!_conns.TryGetValue(peer, out var conn))
            throw new InvalidOperationException($"no link to peer '{peer}'"); // fabric parity: loud, never silent
        var stream = await conn.OpenOutboundStreamAsync(QuicStreamType.Unidirectional, ct).ConfigureAwait(false);
        await stream.WriteAsync(new[] { KindBox }, ct).ConfigureAwait(false);
        await WriteStringAsync(stream, box, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
        var fresh = new Lane(stream, new SemaphoreSlim(1, 1));
        if (_lanes.TryAdd((peer, box), fresh)) return fresh;
        await stream.DisposeAsync().ConfigureAwait(false); // lost a benign race — use the winner
        return _lanes[(peer, box)];
    }

    // ---------------------------------------------------------------- pin + wire helpers

    /// <summary>
    /// The SPKI pin admission gate. With <paramref name="expectedPin"/> (dialer role) the presented
    /// cert must match that exact peer's pin; without it (listener role) it must match SOME pinned
    /// peer — the hello then binds name→pin exactly. Waives only chain + name-mismatch errors
    /// (self-signed dev cert; trust is the pin) — never blanket-accepts (the glp_link discipline).
    /// </summary>
    private bool PinAdmits(X509Certificate? certificate, SslPolicyErrors errors, string? expectedPin)
    {
        if (certificate is null) return false;
        const SslPolicyErrors waived =
            SslPolicyErrors.RemoteCertificateChainErrors | SslPolicyErrors.RemoteCertificateNameMismatch;
        if ((errors & ~waived) != SslPolicyErrors.None) return false;
        var presented = certificate as X509Certificate2
            ?? X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        string pin = SpkiPin(presented);
        return expectedPin is not null
            ? FixedTimeEquals(pin, expectedPin)
            : _peerPins.Values.Any(p => FixedTimeEquals(pin, p));
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

    private static X509Certificate2 RemoteCert(QuicConnection conn) =>
        conn.RemoteCertificate as X509Certificate2
        ?? (conn.RemoteCertificate is { } c
            ? X509CertificateLoader.LoadCertificate(c.GetRawCertData())
            : throw new CrdtMsgException("peer presented no certificate — cannot pin"));

    private static async ValueTask WriteStringAsync(Stream s, string value, CancellationToken ct)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(value);
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, utf8.Length);
        await s.WriteAsync(header, ct).ConfigureAwait(false);
        await s.WriteAsync(utf8, ct).ConfigureAwait(false);
    }

    private static async ValueTask<string> ReadStringAsync(Stream s, CancellationToken ct) =>
        Encoding.UTF8.GetString(await ReadBlockAsync(s, ct).ConfigureAwait(false)
            ?? throw new CrdtMsgException("stream ended before its string field"));

    private static async ValueTask<byte> ReadByteAsync(Stream s, CancellationToken ct)
    {
        var one = await ReadBlockRawAsync(s, 1, ct).ConfigureAwait(false)
            ?? throw new CrdtMsgException("stream ended before its kind byte");
        return one[0];
    }

    /// <summary>A [len][bytes] block; null on a clean EOF at a block boundary; loud on truncation.</summary>
    private static async ValueTask<byte[]?> ReadBlockAsync(Stream s, CancellationToken ct)
    {
        byte[]? header = await ReadBlockRawAsync(s, 4, ct).ConfigureAwait(false);
        if (header is null) return null;
        int len = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (len < 0 || len > MaxFrameBytes)
            throw new CrdtMsgException($"frame length {len} outside [0, {MaxFrameBytes}]");
        return await ReadBlockRawAsync(s, len, ct).ConfigureAwait(false)
            ?? throw new CrdtMsgException("stream ended mid-frame (truncated)");
    }

    /// <summary>Exactly <paramref name="count"/> bytes; null on a clean EOF before the first byte.</summary>
    private static async ValueTask<byte[]?> ReadBlockRawAsync(Stream s, int count, CancellationToken ct)
    {
        var buf = new byte[count];
        int got = 0;
        while (got < count)
        {
            int n = await s.ReadAsync(buf.AsMemory(got, count - got), ct).ConfigureAwait(false);
            if (n == 0)
            {
                if (got == 0) return null;
                throw new CrdtMsgException($"stream ended after {got}/{count} bytes (truncated)");
            }
            got += n;
        }
        return buf;
    }

    // ---------------------------------------------------------------- lifecycle

    private static void RequireSupported(string op)
    {
        if (!IsSupported)
            throw new PlatformNotSupportedException(
                $"QUIC {op} unavailable: QuicListener.IsSupported={QuicListener.IsSupported}, "
                + $"QuicConnection.IsSupported={QuicConnection.IsSupported}. Real QUIC only — no fallback (D3).");
    }

    /// <summary>Orderly close: stop pumps, close every lane/link/listener, complete the inbound channel.</summary>
    public async ValueTask DisposeAsync()
    {
        _life.Cancel();
        foreach (var lane in _lanes.Values)
        {
            try { lane.Stream.CompleteWrites(); } catch { /* already gone */ }
            try { await lane.Stream.DisposeAsync().ConfigureAwait(false); } catch { /* already gone */ }
        }
        _lanes.Clear();
        foreach (var conn in _conns.Values)
        {
            try { await conn.CloseAsync(0).ConfigureAwait(false); } catch { /* already gone */ }
            try { await conn.DisposeAsync().ConfigureAwait(false); } catch { /* already gone */ }
        }
        _conns.Clear();
        if (_listener is not null)
        {
            try { await _listener.DisposeAsync().ConfigureAwait(false); } catch { /* already gone */ }
        }
        if (_acceptLoop is not null)
        {
            try { await _acceptLoop.ConfigureAwait(false); } catch { /* cancellation */ }
        }
        _inbound.Writer.TryComplete();
        _life.Dispose();
    }
}
