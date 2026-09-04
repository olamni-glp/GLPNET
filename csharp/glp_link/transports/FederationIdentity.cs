// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// FederationIdentity — the PER-HOST, PERSISTED QUIC trust anchor (Q-GLPNETA21-01, severity critical).
//
// WHY THIS EXISTS. Ruling Q-GLPNETG27-04 authorises four-host federation on "CreateDevCert material
// and the four SPKI pins exchanged over the existing coop channel". Measured on ARIELLAS
// 2026-09-04T17:35Z: five consecutive runs of the same probe on one unchanged host produced FIVE
// DIFFERENT PINS. Root cause read in the source, not inferred — QuicLinkTransport.CreateDevCert
// calls ECDsa.Create(nistP256) on every invocation and its local is literally named `ephemeral`;
// there is no load-from-disk anywhere. An exchanged pin table is therefore invalid for every host
// simultaneously at the next process restart, and mTLS then refuses EVERY peer.
//
// CreateDevCert is not the defect. It is honestly named and correct for its purpose (a per-test
// throwaway). The defect was adopting a TEST helper as the fleet's trust anchor. This type is the
// non-throwaway sibling: load-or-create, so the pin a host publishes on Monday is the pin it
// presents on Friday.
//
// HOW IT DIFFERS FROM SharedCertMaterial (feature 050, FR-010/FR-011), which also persists a cert:
// that one loads ONE SHARED credential which BOTH ends present — every holder has the same pin, so
// it is a MEMBERSHIP token and cannot distinguish peer from peer. Federation pins are keyed
// per-peer (peer-name -> pin), so each host needs its OWN durable keypair. Both are persisted; only
// this one is an IDENTITY. The consistency discipline below is deliberately SharedCertMaterial's,
// reused rather than reinvented: cert and pin sidecar must agree or the load is refused.
//
// FAIL-CLOSED, with one deliberate exception. Missing/unreadable/private-key-less/self-inconsistent
// material is a loud failure — never a silent regeneration, because silently minting a new keypair
// is exactly the failure this type exists to prevent (it would look like success and break every
// peer). The single exception is FIRST RUN: no keystore at all is not corruption, so a keypair is
// generated and persisted, and the caller is told which happened via <see cref="Created"/>.
//
// ROTATION IS EXPLICIT AND NEVER IMPLICIT. Rotating changes the pin and invalidates every peer's
// table, so it cannot be a side effect of a clock or a near-expiry heuristic; the caller must pass
// rotate: true. For the same reason the generated lifetime is long (10 years): an expiry-driven
// regeneration would be an implicit rotation wearing a different hat.

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GlpRuntime.Link.Transports;

/// <summary>
/// A host's durable QUIC identity: an ECDSA P-256 self-signed cert persisted as PKCS#12 next to a
/// <c>.fingerprint</c> sidecar holding its <c>base64(SHA-256(SPKI))</c> pin. Load-or-create, so the
/// pin is STABLE across process restarts and reboots — the property a pin table exchanged between
/// hosts depends on.
/// </summary>
/// <param name="Cert">The identity certificate, private key included (mutual auth presents it).</param>
/// <param name="Pin">The pinned <c>base64(SHA-256(SPKI))</c> value peers admit this host by.</param>
/// <param name="PfxPath">Where the PKCS#12 bundle lives on this host.</param>
/// <param name="Created">True iff this call MINTED the identity (first run); false iff it loaded one.</param>
public sealed record FederationIdentity(
    X509Certificate2 Cert, string Pin, string PfxPath, bool Created)
{
    /// <summary>Env var overriding the keystore DIRECTORY (a deployment/test seam, not a default).</summary>
    public const string KeystoreEnvVar = "GLPNET_FEDERATION_KEYSTORE";

    /// <summary>Validity of a newly minted identity. Long on purpose — see the rotation note above.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(3650);

    /// <summary>
    /// The per-host keystore directory: <see cref="KeystoreEnvVar"/> if set, else
    /// <c>&lt;LocalApplicationData&gt;/glpnet/federation</c>. LocalApplicationData is deliberate — it is
    /// per-user, per-machine and NOT roamed, which is what a machine identity should be, and it is
    /// outside every repo so a clone, a clean or a branch switch cannot destroy the fleet's pins.
    /// </summary>
    public static string ResolveKeystoreDir()
    {
        var overridden = Environment.GetEnvironmentVariable(KeystoreEnvVar);
        if (!string.IsNullOrWhiteSpace(overridden))
            return Path.GetFullPath(overridden.Trim());
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Path.Combine(Path.GetTempPath(), "glpnet-home");
        return Path.Combine(baseDir, "glpnet", "federation");
    }

    /// <summary>
    /// Load this host's identity for <paramref name="commonName"/>, minting and persisting it only
    /// if no keystore entry exists yet. Existing-but-broken material is REFUSED, never replaced.
    /// </summary>
    /// <param name="commonName">Identity name — also the keystore file stem, so several named
    /// identities (broker, guardian, oracle) coexist on one host without colliding.</param>
    /// <param name="keystoreDir">Directory override; <c>null</c> resolves <see cref="ResolveKeystoreDir"/>.</param>
    /// <param name="rotate">When true, mint a NEW keypair and overwrite the stored one. This CHANGES
    /// the published pin and invalidates every peer's table — never pass it implicitly.</param>
    public static FederationIdentity LoadOrCreate(
        string commonName, string? keystoreDir = null, bool rotate = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commonName);
        if (commonName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException(
                $"identity name '{commonName}' is not usable as a keystore file name.", nameof(commonName));

        var dir = keystoreDir is null ? ResolveKeystoreDir() : Path.GetFullPath(keystoreDir);
        var pfxPath = Path.Combine(dir, commonName + ".pfx");
        var fpPath = Path.Combine(dir, commonName + ".fingerprint");

        if (!rotate && File.Exists(pfxPath))
            return Load(pfxPath, fpPath);

        // A half-written pair (pfx gone, sidecar left behind) is corruption, not a first run: minting
        // over it would silently change a pin peers may already hold. Refuse unless asked to rotate.
        if (!rotate && File.Exists(fpPath))
            throw new InvalidOperationException(
                $"federation keystore is inconsistent: '{fpPath}' exists but '{pfxPath}' does not. "
                + "Refusing to mint a replacement identity, because that would silently change this "
                + "host's published SPKI pin. Remove the stale sidecar deliberately, or pass rotate: true.");

        return Create(dir, pfxPath, fpPath, commonName);
    }

    /// <summary>Load an identity pair, fail-closed on any missing/inconsistent material.</summary>
    public static FederationIdentity Load(string pfxPath, string fingerprintPath)
    {
        if (!File.Exists(pfxPath))
            throw new FileNotFoundException(
                $"federation identity missing: '{pfxPath}' — fail-closed, no ephemeral fallback "
                + "(an ephemeral cert would present a pin no peer holds).", pfxPath);
        if (!File.Exists(fingerprintPath))
            throw new FileNotFoundException(
                $"federation SPKI pin missing: '{fingerprintPath}' — fail-closed.", fingerprintPath);

        var cert = X509CertificateLoader.LoadPkcs12(
            File.ReadAllBytes(pfxPath), null, X509KeyStorageFlags.Exportable);
        if (!cert.HasPrivateKey)
            throw new InvalidOperationException(
                $"federation identity '{pfxPath}' has no private key — it must sign its own handshake.");

        var stored = File.ReadAllText(fingerprintPath).Trim();
        if (stored.Length == 0)
            throw new InvalidOperationException(
                $"federation SPKI pin file '{fingerprintPath}' is empty — fail-closed.");

        var computed = QuicTransport.SpkiPin(cert);
        if (!string.Equals(computed, stored, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"federation trust material is inconsistent: cert SPKI pin '{computed}' != fingerprint "
                + $"file '{stored}' ({pfxPath} vs {fingerprintPath}). Refused: publishing one and "
                + "presenting the other is precisely how a pin table goes silently dead.");

        return new FederationIdentity(cert, computed, pfxPath, Created: false);
    }

    private static FederationIdentity Create(string dir, string pfxPath, string fpPath, string commonName)
    {
        Directory.CreateDirectory(dir);

        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var req = new CertificateRequest($"CN={commonName}", ec, HashAlgorithmName.SHA256);
        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, false));

        var now = DateTimeOffset.UtcNow;
        using var minted = req.CreateSelfSigned(now.AddMinutes(-1), now.Add(Lifetime));
        var pfxBytes = minted.Export(X509ContentType.Pfx);

        // Write the PFX first and the sidecar second: interrupted between the two, the next load
        // sees a pfx without a pin and says so, rather than a pin for a key that never existed.
        WritePrivate(pfxPath, pfxBytes);
        var cert = X509CertificateLoader.LoadPkcs12(pfxBytes, null, X509KeyStorageFlags.Exportable);
        var pin = QuicTransport.SpkiPin(cert);
        WritePrivate(fpPath, System.Text.Encoding.ASCII.GetBytes(pin + Environment.NewLine));

        return new FederationIdentity(cert, pin, pfxPath, Created: true);
    }

    /// <summary>Write owner-only. On Unix that is an explicit 0600; on Windows the per-user
    /// LocalApplicationData ACL already excludes other users and no extra ACL work is done.</summary>
    private static void WritePrivate(string path, byte[] bytes)
    {
        File.WriteAllBytes(path, bytes);
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
