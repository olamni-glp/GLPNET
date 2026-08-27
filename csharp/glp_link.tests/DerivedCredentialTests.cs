using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using GlpQuick.Host;

using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Feature 067 acceptance seam (contracts/join-seam-contract.md): trunk-signed derived
/// credentials accepted alongside the exact trunk pin; expiry / revocation / non-trunk material
/// refused with the contract tokens; revocation reload per accept; single-redemption replay
/// refusal. Validator + tracker rules are covered directly (deterministic, no network); the
/// handshake-level matrix runs a real QUIC handshake and is platform-gated like
/// <see cref="QuicTransportTests"/>.
/// </summary>
public class DerivedCredentialTests
{
    private static bool QuicAvailable => QuicTransport.IsSupported;

    private static CancellationToken Timeout(int seconds = 15) =>
        new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;

    private static int FreeUdpPort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    /// <summary>A fixed-now clock so window checks are deterministic (no sleeping).</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>The trunk: a Decision-5-profile self-signed EC cert with its private key.</summary>
    private static X509Certificate2 MakeTrunk()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=GLP-Quick Shared Cert", ec, HashAlgorithmName.SHA256);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        var now = DateTimeOffset.UtcNow;
        using var ephemeral = req.CreateSelfSigned(now.AddMinutes(-1), now.AddDays(30));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    /// <summary>A device cert signed by the trunk key (mirrors cert.py derive_device_cert).</summary>
    private static X509Certificate2 MintDerived(X509Certificate2 trunk, DateTimeOffset notBefore, DateTimeOffset notAfter,
        string label = "test-device")
    {
        using var deviceKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest($"CN=glp-quick device {label}", deviceKey, HashAlgorithmName.SHA256);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        using var trunkKey = trunk.GetECDsaPrivateKey()!;
        var serial = new byte[8];
        RandomNumberGenerator.Fill(serial);
        var signed = req.Create(trunk.SubjectName, X509SignatureGenerator.CreateForECDsa(trunkKey), notBefore, notAfter, serial);
        using var withKey = signed.CopyWithPrivateKey(deviceKey);
        return X509CertificateLoader.LoadPkcs12(withKey.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    /// <summary>A self-signed impostor — NOT trunk-signed (the cert_mismatch path).</summary>
    private static X509Certificate2 MakeSelfSigned()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=glp-quick device impostor", ec, HashAlgorithmName.SHA256);
        var now = DateTimeOffset.UtcNow;
        using var ephemeral = req.CreateSelfSigned(now.AddMinutes(-1), now.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
    }

    private static string WriteRevocations(string dir, params string[] fingerprints)
    {
        var path = Path.Combine(dir, "revoked.jsonl");
        File.WriteAllLines(path, fingerprints.Select(fp => $"{{\"fingerprint\": \"{fp}\"}}"));
        return path;
    }

    // ---------------------------------------------------------------- validator rules (no network)

    [Fact]
    public void ValidDerived_Accepted()
    {
        using var trunk = MakeTrunk();
        var now = DateTimeOffset.UtcNow;
        using var derived = MintDerived(trunk, now.AddMinutes(-1), now.AddDays(30));
        var v = new DerivedCredentialValidator(trunk, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "revoked.jsonl"));
        Assert.True(v.Validate(derived).IsAccepted);
    }

    [Fact]
    public void Expired_Refused_CertExpired()
    {
        using var trunk = MakeTrunk();
        var now = DateTimeOffset.UtcNow;
        using var derived = MintDerived(trunk, now.AddDays(-30), now.AddDays(-1));
        var v = new DerivedCredentialValidator(trunk, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "revoked.jsonl"));
        var res = v.Validate(derived);
        Assert.Equal(DerivedVerdict.Expired, res.Verdict);
        Assert.Equal("cert_expired", res.Token);
    }

    [Fact]
    public void NotYetValid_Refused_CertExpired()
    {
        using var trunk = MakeTrunk();
        var now = DateTimeOffset.UtcNow;
        using var derived = MintDerived(trunk, now.AddHours(1), now.AddDays(30));
        var v = new DerivedCredentialValidator(trunk, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "revoked.jsonl"));
        var res = v.Validate(derived);
        Assert.Equal(DerivedVerdict.Expired, res.Verdict);
        Assert.Equal("cert_expired", res.Token);
        Assert.Contains("not yet valid", res.Detail);
    }

    [Fact]
    public void SkewBound_90s_IsTolerated_BeyondIsNot()
    {
        using var trunk = MakeTrunk();
        var nb = DateTimeOffset.UtcNow;
        using var derived = MintDerived(trunk, nb, nb.AddDays(30));
        var tmp = Directory.CreateTempSubdirectory().FullName;
        // 60 s before NotBefore: inside the ±90 s bound → accepted.
        var early = new DerivedCredentialValidator(trunk, Path.Combine(tmp, "revoked.jsonl"),
            clock: new FixedClock(nb.AddSeconds(-60)));
        Assert.True(early.Validate(derived).IsAccepted);
        // 120 s before NotBefore: beyond the bound → cert_expired.
        var tooEarly = new DerivedCredentialValidator(trunk, Path.Combine(tmp, "revoked.jsonl"),
            clock: new FixedClock(nb.AddSeconds(-120)));
        Assert.Equal("cert_expired", tooEarly.Validate(derived).Token);
    }

    [Fact]
    public void SelfSigned_Refused_CertMismatch()
    {
        using var trunk = MakeTrunk();
        using var impostor = MakeSelfSigned();
        var v = new DerivedCredentialValidator(trunk, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "revoked.jsonl"));
        var res = v.Validate(impostor);
        Assert.Equal(DerivedVerdict.NotSigned, res.Verdict);
        Assert.Equal("cert_mismatch", res.Token);
    }

    [Fact]
    public void Revoked_Refused_CertRevoked()
    {
        using var trunk = MakeTrunk();
        var now = DateTimeOffset.UtcNow;
        using var derived = MintDerived(trunk, now.AddMinutes(-1), now.AddDays(30));
        var tmp = Directory.CreateTempSubdirectory().FullName;
        var path = WriteRevocations(tmp, QuicTransport.SpkiPin(derived));
        var v = new DerivedCredentialValidator(trunk, path);
        var res = v.Validate(derived);
        Assert.Equal(DerivedVerdict.Revoked, res.Verdict);
        Assert.Equal("cert_revoked", res.Token);
    }

    [Fact]
    public void RevokeMidListen_NextValidateRefuses()
    {
        // T021: the revocation appended AFTER the validator went live is enforced on the very next
        // accept (per-accept mtime reload) — well inside the ≤ 60 s bound (FR-009).
        using var trunk = MakeTrunk();
        var now = DateTimeOffset.UtcNow;
        using var derived = MintDerived(trunk, now.AddMinutes(-1), now.AddDays(30));
        var tmp = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(tmp, "revoked.jsonl");
        var v = new DerivedCredentialValidator(trunk, path);
        Assert.True(v.Validate(derived).IsAccepted); // live, nothing revoked

        WriteRevocations(tmp, QuicTransport.SpkiPin(derived)); // operator revokes mid-listen
        Assert.Equal("cert_revoked", v.Validate(derived).Token);
    }

    [Fact]
    public void ReprovisionAfterRevoke_NewCredentialAccepted()
    {
        using var trunk = MakeTrunk();
        var now = DateTimeOffset.UtcNow;
        using var revokedCred = MintDerived(trunk, now.AddMinutes(-1), now.AddDays(30), "old");
        using var freshCred = MintDerived(trunk, now.AddMinutes(-1), now.AddDays(30), "new");
        var tmp = Directory.CreateTempSubdirectory().FullName;
        var v = new DerivedCredentialValidator(trunk, WriteRevocations(tmp, QuicTransport.SpkiPin(revokedCred)));
        Assert.Equal("cert_revoked", v.Validate(revokedCred).Token);
        Assert.True(v.Validate(freshCred).IsAccepted); // the re-provisioned device is unaffected
    }

    [Fact]
    public void CorruptRevocationFile_DerivedFailsClosed_ThenRecovers()
    {
        using var trunk = MakeTrunk();
        var now = DateTimeOffset.UtcNow;
        using var derived = MintDerived(trunk, now.AddMinutes(-1), now.AddDays(30));
        var tmp = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(tmp, "revoked.jsonl");
        File.WriteAllText(path, "this is not json\n");
        var v = new DerivedCredentialValidator(trunk, path);
        var res = v.Validate(derived);
        Assert.Equal(DerivedVerdict.Revoked, res.Verdict);
        Assert.Contains("revocation_list_unreadable", res.Detail);

        File.WriteAllText(path, ""); // operator repairs the file → derived path reopens
        Assert.True(v.Validate(derived).IsAccepted);
    }

    [Fact]
    public void MissingRevocationFile_IsEmptySet_NotAnError()
    {
        using var trunk = MakeTrunk();
        var now = DateTimeOffset.UtcNow;
        using var derived = MintDerived(trunk, now.AddMinutes(-1), now.AddDays(30));
        var v = new DerivedCredentialValidator(trunk, Path.Combine(Directory.CreateTempSubdirectory().FullName, "revoked.jsonl"));
        Assert.True(v.Validate(derived).IsAccepted);
    }

    // ---------------------------------------------------------------- single-redemption tracker (T020)

    [Fact]
    public void Tracker_TrunkIdentity_Exempt()
    {
        var t = new RedemptionTracker("TRUNKPIN=");
        Assert.Equal(AdmitOutcome.NotDerived, t.Admit("TRUNKPIN=", new object()));
        Assert.Equal(AdmitOutcome.NotDerived, t.Admit(null, new object()));
    }

    [Fact]
    public void Tracker_FirstJoin_Then_ReplayRefused()
    {
        var t = new RedemptionTracker("TRUNKPIN=");
        var linkA = new object();
        var linkB = new object();
        Assert.Equal(AdmitOutcome.FirstJoin, t.Admit("DERIVED=", linkA));
        // A second live connection under the same credential from a different link → replay.
        Assert.Equal(AdmitOutcome.Replayed, t.Admit("DERIVED=", linkB));
    }

    [Fact]
    public void Tracker_RejoinAfterDrop_Admitted_NoSecondRedeemEvent()
    {
        var t = new RedemptionTracker("TRUNKPIN=");
        var linkA = new object();
        var linkB = new object();
        Assert.Equal(AdmitOutcome.FirstJoin, t.Admit("DERIVED=", linkA));
        t.Release("DERIVED=", linkA); // client dropped
        Assert.Equal(AdmitOutcome.Rejoin, t.Admit("DERIVED=", linkB)); // TTL-valid reconnect is fine
    }

    // ---------------------------------------------------------------- handshake matrix (real QUIC)

    private static async Task<(string? serverToken, Exception? clientError, ILinkEndpoint? server, ILinkEndpoint? client)>
        HandshakeAsync(QuicTransport serverT, QuicTransport clientT)
    {
        int port = FreeUdpPort();
        var addr = LinkAddress.Endpoint("127.0.0.1", port);
        var listen = serverT.ListenAsync(LinkScheme.Quic, addr, LinkOptions.Default, Timeout(8));
        Exception? clientError = null;
        ILinkEndpoint? client = null;
        try { client = await clientT.ConnectAsync(LinkScheme.Quic, addr, LinkOptions.Default, Timeout(8)); }
        catch (Exception ex) { clientError = ex; }
        ILinkEndpoint? server = null;
        try { server = await listen; } catch { /* refused handshakes fault the listen — expected */ }
        return (serverT.LastRefusalToken, clientError, server, client);
    }

    [Fact]
    public async Task Handshake_TrunkClient_StillAccepted_WithValidatorConfigured()
    {
        if (!QuicAvailable) return; // platform lacks QUIC (FR-001 gate)
        using var trunk = MakeTrunk();
        var pin = QuicTransport.SpkiPin(trunk);
        var validator = new DerivedCredentialValidator(trunk, Path.Combine(Directory.CreateTempSubdirectory().FullName, "revoked.jsonl"));
        var (token, err, server, client) = await HandshakeAsync(
            new QuicTransport(trunk, pin, validator), new QuicTransport(trunk, pin));
        Assert.Null(err);         // FR-012: existing trunk endpoints unchanged
        Assert.Null(token);
        Assert.NotNull(server);
        Assert.NotNull(client);
        await server!.DisposeAsync();
        await client!.DisposeAsync();
    }

    [Fact]
    public async Task Handshake_ValidDerivedClient_Accepted_PeerPinReported()
    {
        if (!QuicAvailable) return; // platform lacks QUIC (FR-001 gate)
        using var trunk = MakeTrunk();
        var pin = QuicTransport.SpkiPin(trunk);
        var now = DateTimeOffset.UtcNow;
        using var derived = MintDerived(trunk, now.AddMinutes(-1), now.AddDays(30));
        var validator = new DerivedCredentialValidator(trunk, Path.Combine(Directory.CreateTempSubdirectory().FullName, "revoked.jsonl"));
        var (token, err, server, client) = await HandshakeAsync(
            new QuicTransport(trunk, pin, validator), new QuicTransport(derived, pin));
        Assert.Null(err);
        Assert.Null(token);
        Assert.NotNull(server);
        // The accept seam sees the DERIVED identity — the input to redemption/replay tracking.
        Assert.Equal(QuicTransport.SpkiPin(derived), Assert.IsAssignableFrom<IPeerCertEndpoint>(server).RemoteSpkiPin);
        await server!.DisposeAsync();
        await client!.DisposeAsync();
    }

    [Fact]
    public async Task Handshake_SelfSignedClient_Refused_CertMismatch()
    {
        if (!QuicAvailable) return; // platform lacks QUIC (FR-001 gate)
        using var trunk = MakeTrunk();
        var pin = QuicTransport.SpkiPin(trunk);
        using var impostor = MakeSelfSigned();
        var validator = new DerivedCredentialValidator(trunk, Path.Combine(Directory.CreateTempSubdirectory().FullName, "revoked.jsonl"));
        var (token, err, server, _) = await HandshakeAsync(
            new QuicTransport(trunk, pin, validator), new QuicTransport(impostor, pin));
        Assert.NotNull(err);      // no half-open link on refusal
        Assert.Null(server);
        Assert.Equal("cert_mismatch", token);
    }

    [Fact]
    public async Task Handshake_ExpiredDerivedClient_Refused_CertExpired()
    {
        if (!QuicAvailable) return; // platform lacks QUIC (FR-001 gate)
        using var trunk = MakeTrunk();
        var pin = QuicTransport.SpkiPin(trunk);
        var now = DateTimeOffset.UtcNow;
        using var expired = MintDerived(trunk, now.AddDays(-30), now.AddDays(-1));
        var validator = new DerivedCredentialValidator(trunk, Path.Combine(Directory.CreateTempSubdirectory().FullName, "revoked.jsonl"));
        var (token, err, server, _) = await HandshakeAsync(
            new QuicTransport(trunk, pin, validator), new QuicTransport(expired, pin));
        Assert.NotNull(err);
        Assert.Null(server);
        Assert.Equal("cert_expired", token);
    }

    [Fact]
    public async Task Handshake_RevokedDerivedClient_Refused_CertRevoked_TrunkStillAccepted()
    {
        if (!QuicAvailable) return; // platform lacks QUIC (FR-001 gate)
        using var trunk = MakeTrunk();
        var pin = QuicTransport.SpkiPin(trunk);
        var now = DateTimeOffset.UtcNow;
        using var derived = MintDerived(trunk, now.AddMinutes(-1), now.AddDays(30));
        var tmp = Directory.CreateTempSubdirectory().FullName;
        var validator = new DerivedCredentialValidator(trunk, WriteRevocations(tmp, QuicTransport.SpkiPin(derived)));

        var (token, err, server, _) = await HandshakeAsync(
            new QuicTransport(trunk, pin, validator), new QuicTransport(derived, pin));
        Assert.NotNull(err);
        Assert.Null(server);
        Assert.Equal("cert_revoked", token);

        // The trunk-identity path is untouched by the revocation state (contract: trunk unaffected).
        var (token2, err2, server2, client2) = await HandshakeAsync(
            new QuicTransport(trunk, pin, validator), new QuicTransport(trunk, pin));
        Assert.Null(err2);
        Assert.Null(token2);
        await server2!.DisposeAsync();
        await client2!.DisposeAsync();
    }

    [Fact]
    public async Task Handshake_CorruptRevocationFile_DerivedFailsClosed_TrunkStillAccepted()
    {
        if (!QuicAvailable) return; // platform lacks QUIC (FR-001 gate)
        using var trunk = MakeTrunk();
        var pin = QuicTransport.SpkiPin(trunk);
        var now = DateTimeOffset.UtcNow;
        using var derived = MintDerived(trunk, now.AddMinutes(-1), now.AddDays(30));
        var tmp = Directory.CreateTempSubdirectory().FullName;
        var path = Path.Combine(tmp, "revoked.jsonl");
        File.WriteAllText(path, "{not-json\n");
        var validator = new DerivedCredentialValidator(trunk, path);

        var (token, err, server, _) = await HandshakeAsync(
            new QuicTransport(trunk, pin, validator), new QuicTransport(derived, pin));
        Assert.NotNull(err);
        Assert.Null(server);
        Assert.Equal("cert_revoked", token); // revocation_list_unreadable variant

        var (token2, err2, server2, client2) = await HandshakeAsync(
            new QuicTransport(trunk, pin, validator), new QuicTransport(trunk, pin));
        Assert.Null(err2);
        await server2!.DisposeAsync();
        await client2!.DisposeAsync();
    }
}
