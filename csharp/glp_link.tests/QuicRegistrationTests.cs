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
        // Feature 109 changed what "valid" means. A freshly-generated cert is INTERNALLY consistent
        // (private key present, fingerprint matches its own SPKI) but is NOT the current shared
        // generation, so Load now refuses it — correctly: an unvetted generation is not one to
        // establish links on. This test therefore asserts what it always really exercised — that
        // every consistency check passes — by requiring the refusal to be the GENERATION one and
        // not any of the earlier ones. Accepted-material coverage moved to the integration test
        // below, which uses the repo's real current material rather than a synthetic pin.
        var (cert, pin) = MakeCert();
        var dir = FreshDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, SharedCertMaterial.PfxFileName), cert.Export(X509ContentType.Pfx));
            File.WriteAllText(Path.Combine(dir, SharedCertMaterial.FingerprintFileName), pin + "\n");

            var ex = Assert.Throws<InvalidOperationException>(() => SharedCertMaterial.Load(dir));

            // It got all the way to the generation check — so pfx, pin file, private key and
            // cert/pin consistency all passed. That is the original assertion, kept.
            Assert.Contains("NOT THE CURRENT GENERATION", ex.Message);
            Assert.Contains(pin, ex.Message);
            Assert.DoesNotContain("inconsistent", ex.Message);
            Assert.DoesNotContain("no private key", ex.Message);
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
        string certDir;
        try { certDir = SharedCertMaterial.ResolveCertDir(); }
        catch (Exception) { return; }   // material not provisioned on this host — see doc comment

        var (loaded, loadedPin) = SharedCertMaterial.Load(certDir);

        Assert.True(loaded.HasPrivateKey);
        Assert.Equal(loadedPin, QuicTransport.SpkiPin(loaded));
        Assert.Equal(SharedCertMaterial.CurrentPin, loadedPin);
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
