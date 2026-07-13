// Static-macaroon root material loader — out-of-band alongside the cert (feature 050 US3, T028).
//
// Contract capability-gating.md "Root secret": the static-macaroon root key is distributed
// out-of-band alongside glpquick-cert/ (beacon static-macaroon model); it is not minted per-session.
// FAIL-CLOSED like SharedCertMaterial (whose ResolveCertDir walk-up this reuses): missing, empty,
// undecodable, or too-short key material is a loud startup error — never a degraded ungated mode.

using GlpRuntime.CrdtMsg.Cap;
using GlpRuntime.Link.Transports;

namespace GlpRuntime.CrdtMsg.Bridge;

/// <summary>Loads the shared static-macaroon root key from <c>glpquick-cert/</c> and mints this
/// endpoint's presented static macaroon (root macaroon, attenuable by tests/deployments).</summary>
public static class StaticMacaroonMaterial
{
    /// <summary>The out-of-band root-key file (base64, one line) beside the cert artifacts.</summary>
    public const string RootKeyFileName = "glpquick.macaroon.key";

    /// <summary>Minimum root-key strength: 32 bytes (HMAC-SHA256 key size).</summary>
    public const int MinKeyBytes = 32;

    /// <summary>The static macaroon's location/identifier (the beacon static credential).</summary>
    public const string Location = "glpquick";
    public const string Identifier = "glpnet-mesh";

    /// <summary>Load the root key from <paramref name="certDir"/>; fail-closed on any defect.</summary>
    public static byte[] LoadRootKey(string certDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(certDir);
        var path = Path.Combine(certDir, RootKeyFileName);
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"static-macaroon root key missing: '{path}' — fail-closed, no ungated mode (FR-008).", path);

        string b64 = File.ReadAllText(path).Trim();
        if (b64.Length == 0)
            throw new InvalidOperationException(
                $"static-macaroon root key file '{path}' is empty — fail-closed (FR-008).");

        byte[] key;
        try
        {
            key = Convert.FromBase64String(b64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"static-macaroon root key file '{path}' is not valid base64 — fail-closed (FR-008): {ex.Message}");
        }

        if (key.Length < MinKeyBytes)
            throw new InvalidOperationException(
                $"static-macaroon root key in '{path}' is {key.Length} bytes; require >= {MinKeyBytes} "
                + "(HMAC-SHA256 strength) — fail-closed (FR-008).");
        return key;
    }

    /// <summary>Resolve <c>glpquick-cert/</c> (the SharedCertMaterial walk-up) and load the root key
    /// + mint the presented static macaroon — the composition-root call (T028).</summary>
    public static (byte[] rootKey, Macaroon macaroon) LoadFromRepo()
    {
        byte[] rootKey = LoadRootKey(SharedCertMaterial.ResolveCertDir());
        return (rootKey, Macaroon.Create(rootKey, Location, Identifier));
    }
}
