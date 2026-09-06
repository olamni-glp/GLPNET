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
    /// The SPKI pin of the CURRENT shared generation (gen-3, installed 2026-08-10 by feature 069).
    /// <para>
    /// Compiled in, never read from a file or an environment variable (feature 109, FR-002,
    /// engineer ruling G-03). A configuration-driven trust list that ships empty admits everybody;
    /// a constant has no empty state to fail open into.
    /// </para>
    /// <para>
    /// <b>Rotating this is a code change, and deliberately so.</b> Until every host takes the new
    /// build, hosts on different builds refuse each other. That is the correct cost for shared
    /// SPKI-pinned material: a rotation should be simultaneous and reviewed, not ambient.
    /// </para>
    /// </summary>
    public const string CurrentPin = "jKMVqlvEL0evFBPw4TWIlEln3TBbXT1u1t072Zp1AlY=";

    /// <summary>
    /// SPKI pins that must NEVER be trusted again by any peer (feature 109, FR-001/FR-003).
    /// <para>
    /// <c>0LOm…</c> is gen-1. Its private key was committed in <c>94fbe87d</c> ("release:
    /// v2026.07.09.1") and is reachable from <c>origin/main</c>, <c>origin/develop</c> and 10+
    /// origin branches on the PUBLIC remote — anyone who has cloned this repository holds it.
    /// </para>
    /// <para>
    /// This list exists for the MESSAGE, not for the coverage: a denylist can only refuse what
    /// somebody already enumerated. <see cref="CurrentPin"/> is what actually closes the door on
    /// generations nobody has looked at — which, on 2026-09-06, was every generation for 25 days.
    /// </para>
    /// </summary>
    private static readonly string[] RevokedPins =
    [
        "0LOmLNM0HYv79Rkoasuu6L4MKGRyg7axgJufbZBcyTo=",
    ];

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

        // ---- feature 109: the loader validated CONSISTENCY above; now it validates IDENTITY. ----
        AssertPinIsTrusted(pin, fpPath);

        return (cert, pin);
    }

    /// <summary>
    /// Refuse trust material that is REVOKED, or that is not the CURRENT generation
    /// (feature 109, FR-001/FR-003/FR-004/FR-005/FR-006).
    /// <para>
    /// Pure and side-effect free: it takes the already-parsed pin and throws or returns. It is
    /// separated from <see cref="Load"/> precisely so it can be tested directly — proving the
    /// revoked branch through <see cref="Load"/> would require gen-1's private key, which is the
    /// one thing this feature must never reintroduce into the repository.
    /// </para>
    /// <para>
    /// <b>Why this is needed at all:</b> every check in <see cref="Load"/> above this point passes
    /// for a coherent-but-revoked generation, because a restored pfx and its restored fingerprint
    /// agree with each other perfectly. <see cref="Load"/> validated INTERNAL CONSISTENCY; it had
    /// no notion of IDENTITY. That gap is how a host served a publicly-published private key for
    /// two days with nothing firing.
    /// </para>
    /// </summary>
    /// <param name="pin">The parsed SPKI pin (already trimmed by the caller — FR-008).</param>
    /// <param name="fpPath">Path quoted in the message so the operator knows which file to fix.</param>
    public static void AssertPinIsTrusted(string pin, string fpPath)
    {
        // Revoked is tested FIRST: "this key is public" is more urgent and more specific than
        // "this is not the current generation", and the operator must be shown the worse one.
        if (Array.Exists(RevokedPins, revoked => string.Equals(revoked, pin, StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"shared QUIC trust material is REVOKED: SPKI pin '{pin}' ({fpPath}) is on the "
                + "never-trust list — its private key is published in this repository's PUBLIC git "
                + "history and anyone who has cloned the repo holds it (feature 109, FR-001/FR-003). "
                + "REMEDY: obtain the current material from a peer host that already has it — do NOT "
                + "restore it from git history, which is what put this key here.");

        if (!string.Equals(CurrentPin, pin, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"shared QUIC trust material is NOT THE CURRENT GENERATION: SPKI pin '{pin}' "
                + $"({fpPath}) != expected '{CurrentPin}' (feature 109, FR-004). The pin is not on "
                + "the known-revoked list, so this is unrecognised rather than known-bad — treated "
                + "as untrusted either way, because a generation nobody has vetted is not a "
                + "generation to establish links on. REMEDY: obtain the current material from a peer "
                + "host — do NOT restore it from git history.");
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
