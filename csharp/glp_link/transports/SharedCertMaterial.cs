using System.Security.Cryptography.X509Certificates;

namespace GlpRuntime.Link.Transports;

/// <summary>
/// Loads the <b>permanent shared</b> QUIC trust material (feature 050, FR-010/FR-011) from the
/// <c>glpquick-cert/</c> directory: the shared self-signed certificate (with its private key, so
/// both ends present it for mutual auth) and its pinned <c>base64(SHA-256(SPKI))</c> fingerprint.
/// <para>
/// <b>Fail-closed</b> (contract <c>transport-registration.md</c> "Cert/pin loader"): any missing,
/// unreadable, private-key-less, or self-inconsistent material is a loud failure — NEVER a degraded
/// no-pin mode. The cert is a permanent credential (FR-010): the loader applies no time-boxed
/// carve-out; trust is the SPKI pin, not expiry/CA/hostname (FR-011).
/// </para>
/// </summary>
public static class SharedCertMaterial
{
    /// <summary>The repo-root directory holding the shared trust artifacts.</summary>
    public const string CertDirName = "glpquick-cert";

    /// <summary>The PKCS#12 bundle (cert + private key) both ends present.</summary>
    public const string PfxFileName = "glpquick.pfx";

    /// <summary>The pinned <c>base64(SHA-256(SPKI))</c> value the peer is validated against.</summary>
    public const string FingerprintFileName = "glpquick.fingerprint";

    /// <summary>
    /// Load <c>(cert, pin)</c> from <paramref name="certDir"/>. The cert MUST carry its private key
    /// and the fingerprint file's pin MUST equal the cert's own SPKI pin (a swapped or corrupt pair
    /// is a trust-config error — refused, not tolerated). Throws on any missing / unreadable /
    /// inconsistent material (fail-closed).
    /// </summary>
    public static (X509Certificate2 cert, string pin) Load(string certDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certDir);
        var pfxPath = Path.Combine(certDir, PfxFileName);
        var fpPath = Path.Combine(certDir, FingerprintFileName);

        if (!File.Exists(pfxPath))
            throw new FileNotFoundException(
                $"shared QUIC cert missing: '{pfxPath}' — fail-closed, no degraded no-pin mode (FR-010).", pfxPath);
        if (!File.Exists(fpPath))
            throw new FileNotFoundException(
                $"shared QUIC SPKI pin missing: '{fpPath}' — fail-closed (FR-011).", fpPath);

        var cert = X509CertificateLoader.LoadPkcs12(
            File.ReadAllBytes(pfxPath), null, X509KeyStorageFlags.Exportable);
        if (!cert.HasPrivateKey)
            throw new InvalidOperationException(
                $"shared QUIC cert '{pfxPath}' has no private key — both ends present it for mutual auth (FR-011).");

        var pin = File.ReadAllText(fpPath).Trim();
        if (pin.Length == 0)
            throw new InvalidOperationException(
                $"shared QUIC SPKI pin file '{fpPath}' is empty — fail-closed (FR-011).");

        var computed = QuicTransport.SpkiPin(cert);
        if (!string.Equals(computed, pin, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"shared QUIC trust material is inconsistent: cert SPKI pin '{computed}' != fingerprint file '{pin}' "
                + $"({pfxPath} vs {fpPath}) — fail-closed, refuse a mismatched cert/pin pair (FR-011).");

        return (cert, pin);
    }

    /// <summary>
    /// Resolve the <c>glpquick-cert/</c> directory by walking up from
    /// <see cref="AppContext.BaseDirectory"/> until an ancestor holds
    /// <c>glpquick-cert/glpquick.pfx</c> — the same idiom the REPL uses to find
    /// <c>programs/self.glp</c>. Throws (fail-closed) if no such ancestor exists.
    /// </summary>
    public static string ResolveCertDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, CertDirName, PfxFileName);
            if (File.Exists(candidate))
                return Path.GetFullPath(Path.Combine(dir.FullName, CertDirName));
            dir = dir.Parent;
        }
        throw new InvalidOperationException(
            $"could not locate {CertDirName}/{PfxFileName} by walking up from AppContext.BaseDirectory = "
            + $"{AppContext.BaseDirectory}. Run from within a glpnet checkout whose root holds {CertDirName}/ (FR-010).");
    }

    /// <summary>Resolve the repo <c>glpquick-cert/</c> then load — the composition-root call.</summary>
    public static (X509Certificate2 cert, string pin) LoadFromRepo() => Load(ResolveCertDir());
}
