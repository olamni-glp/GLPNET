using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using GlpRuntime.Link.Primitives;
using GlpRuntime.Link.Seam;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.Link.Tests;

/// <summary>
/// Feature 050 US1 (T006) — registering the genuine <see cref="QuicTransport"/> makes the 025
/// <see cref="TransportRegistry"/> select it for <see cref="LinkScheme.Quic"/> (contract
/// <c>transport-registration.md</c> G1); an unregistered scheme throws; and a quic link NEVER
/// silently falls back to tcp/loopback by construction (G2/FR-002). Also covers the T005
/// fail-closed cert/pin loader (<see cref="SharedCertMaterial"/>).
/// </summary>
public class QuicRegistrationTests
{
    /// <summary>
    /// The gen-3 SPKI pin, written down INDEPENDENTLY of <see cref="SharedCertMaterial.CurrentPin"/>
    /// (feature 109; codexreview 2026-09-07 [P2]). Comparing the production constant against itself
    /// is tautological — it was, and setting <c>CurrentPin</c> to an arbitrary wrong non-revoked
    /// value left nine tests green. Measured on ARIELLAS 2026-09-06 from the gen-3 material dated
    /// 2026-08-10 (feature 069's rotation). If a rotation changes the production constant, this must
    /// be changed too, deliberately, in the same commit — that friction is the point.
    /// </summary>
    internal const string ExpectedGen3Pin = "jKMVqlvEL0evFBPw4TWIlEln3TBbXT1u1t072Zp1AlY=";

    /// <summary>An ephemeral shared self-signed cert + its SPKI pin (hermetic; no repo dependency).</summary>
    private static (X509Certificate2 cert, string pin) MakeCert()
    {
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest("CN=GLP-Quick 050 Test", ec, HashAlgorithmName.SHA256);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, false));
        var now = DateTimeOffset.UtcNow;
        using var ephemeral = req.CreateSelfSigned(now.AddMinutes(-1), now.AddDays(1));
        var loaded = X509CertificateLoader.LoadPkcs12(
            ephemeral.Export(X509ContentType.Pfx), null, X509KeyStorageFlags.Exportable);
        return (loaded, QuicTransport.SpkiPin(loaded));
    }

    // ---- T006: registration → Select ----

    [Fact]
    public void Register_Quic_SelectQuic_ReturnsIt()
    {
        var (cert, pin) = MakeCert();
        var quic = new QuicTransport(cert, pin);
        var registry = new TransportRegistry();
        registry.Register(quic);

        Assert.Same(quic, registry.Select(LinkScheme.Quic));
        Assert.True(registry.TrySelect(LinkScheme.Quic, out var got));
        Assert.Same(quic, got);
    }

    [Fact]
    public void Select_UnregisteredScheme_Throws()
    {
        var registry = new TransportRegistry();
        registry.Register(new LoopbackTransport());
        // quic is NOT registered → loud KeyNotFound, never a silent substitute (FR-002/G2).
        Assert.Throws<KeyNotFoundException>(() => registry.Select(LinkScheme.Quic));
        Assert.False(registry.TrySelect(LinkScheme.Quic, out _));
    }

    [Fact]
    public void Quic_NeverFallsBackToTcpOrLoopback_ByConstruction()
    {
        var (cert, pin) = MakeCert();
        var quic = new QuicTransport(cert, pin);
        var registry = new TransportRegistry();
        // Register the composition-root set (tcp + loopback + quic) and confirm the quic scheme
        // resolves to the QUIC leaf — not tcp, not loopback. There is no downgrade path.
        registry.Register(new TcpTransport());
        registry.Register(new LoopbackTransport());
        registry.Register(quic);

        Assert.Same(quic, registry.Select(LinkScheme.Quic));
        Assert.NotSame(registry.Select(LinkScheme.Tcp), registry.Select(LinkScheme.Quic));
        Assert.NotSame(registry.Select(LinkScheme.Loopback), registry.Select(LinkScheme.Quic));
        // The QUIC leaf serves ONLY the quic scheme — it can never be selected for another.
        Assert.Equal(new[] { LinkScheme.Quic }, quic.SupportedSchemes.ToArray());
    }

    // ---- T005: the fail-closed cert/pin loader ----

    [Fact]
    public void Loader_ValidMaterial_LoadsCertAndMatchingPin()
    {
        // G-05: Load(dir) is the EXPLICIT-directory entry point and applies the revoked list
        // only, so a freshly generated cert loads exactly as it always did. This test is restored
        // to its original form; era 109 did not change what "valid explicit material" means.
        var (cert, pin) = MakeCert();
        var dir = FreshDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, SharedCertMaterial.PfxFileName), cert.Export(X509ContentType.Pfx));
            File.WriteAllText(Path.Combine(dir, SharedCertMaterial.FingerprintFileName), pin + "\n");

            var (loaded, loadedPin) = SharedCertMaterial.Load(dir);
            Assert.True(loaded.HasPrivateKey);
            Assert.Equal(pin, loadedPin);
            Assert.Equal(pin, QuicTransport.SpkiPin(loaded));
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Feature 109 SC-002 at integration level: the repo's REAL current material loads, with its
    /// private key and self-consistent pin. This is the accepted-generation coverage that
    /// <see cref="Loader_ValidMaterial_LoadsCertAndMatchingPin"/> used to provide with a synthetic
    /// cert — and it is stronger, because it exercises the material links actually use.
    /// Skipped rather than failed where the material is absent: a host without it is a provisioning
    /// state, not a code defect, and the loader's own missing-material tests already cover that.
    /// </summary>
    [Fact]
    public void Loader_RealCurrentMaterial_LoadsAndIsAccepted()
    {
        try { SharedCertMaterial.ResolveCertDir(); }
        catch (Exception) { return; }   // material not provisioned on this host — see doc comment

        // LoadFromRepo() is the SHARED path, so this exercises the generation assertion for real
        // (G-05). It is also the ONE non-tautological check on CurrentPin available here: the pin
        // comes off disk, not from the constant, so a wrong constant fails this test.
        var (loaded, loadedPin) = SharedCertMaterial.LoadFromRepo();

        Assert.True(loaded.HasPrivateKey);
        Assert.Equal(loadedPin, QuicTransport.SpkiPin(loaded));
        Assert.Equal(SharedCertMaterial.CurrentPin, loadedPin);
        Assert.Equal(ExpectedGen3Pin, loadedPin);   // independently specified — see the constant
    }

    [Fact]
    public void Loader_MissingPfx_FailsClosed()
    {
        var dir = FreshDir();
        try { Assert.Throws<FileNotFoundException>(() => SharedCertMaterial.Load(dir)); }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Loader_MissingFingerprint_FailsClosed()
    {
        var (cert, _) = MakeCert();
        var dir = FreshDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, SharedCertMaterial.PfxFileName), cert.Export(X509ContentType.Pfx));
            Assert.Throws<FileNotFoundException>(() => SharedCertMaterial.Load(dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Loader_MismatchedPin_FailsClosed()
    {
        var (cert, _) = MakeCert();
        var dir = FreshDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, SharedCertMaterial.PfxFileName), cert.Export(X509ContentType.Pfx));
            File.WriteAllText(Path.Combine(dir, SharedCertMaterial.FingerprintFileName),
                "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ="); // a wrong pin
            var ex = Assert.Throws<InvalidOperationException>(() => SharedCertMaterial.Load(dir));
            Assert.Contains("inconsistent", ex.Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Loader_EmptyFingerprint_FailsClosed()
    {
        var (cert, _) = MakeCert();
        var dir = FreshDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, SharedCertMaterial.PfxFileName), cert.Export(X509ContentType.Pfx));
            File.WriteAllText(Path.Combine(dir, SharedCertMaterial.FingerprintFileName), "   \n");
            Assert.Throws<InvalidOperationException>(() => SharedCertMaterial.Load(dir));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Loader_RealRepoCert_LoadsAndPinMatches()
    {
        // The permanent shared glpquick-cert/ (FR-010) — present in a real checkout; skip if the
        // untracked cert dir is absent so a cert-less CI does not hard-fail here.
        string dir;
        try { dir = SharedCertMaterial.ResolveCertDir(); }
        catch (InvalidOperationException) { return; }

        var (cert, pin) = SharedCertMaterial.Load(dir);
        Assert.True(cert.HasPrivateKey);
        Assert.Equal(pin, QuicTransport.SpkiPin(cert));
    }

    private static string FreshDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "glp050_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
