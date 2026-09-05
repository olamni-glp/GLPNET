// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Regression tests for Q-GLPNETA21-01 — "five runs on one unchanged host, five different pins".
//
// The load-bearing test is StabilityAcrossCalls: it is the exact measurement that FAILED against
// CreateDevCert, expressed as an assertion, so the defect cannot come back unnoticed. The rest fix
// the fail-closed edges, because the dangerous failure here is not a crash — it is a SILENT
// remint, which looks like success and then refuses every peer.

using System.Security.Cryptography.X509Certificates;

using GlpRuntime.Link.Transports;

namespace GlpRuntime.Link.Tests;

public sealed class FederationIdentityTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "glpnet-fedid-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    /// <summary>THE regression: five consecutive loads on one unchanged host must yield ONE pin.</summary>
    [Fact]
    public void StabilityAcrossCalls_FivePinsAreOnePin()
    {
        var pins = Enumerable.Range(0, 5)
            .Select(_ => FederationIdentity.LoadOrCreate("host-a", _dir).Pin)
            .Distinct()
            .ToArray();

        Assert.Single(pins);
        Assert.False(string.IsNullOrWhiteSpace(pins[0]));
    }

    /// <summary>The first call mints; every later call loads. The caller can tell which.</summary>
    [Fact]
    public void FirstCallMints_SubsequentCallsLoad()
    {
        var first = FederationIdentity.LoadOrCreate("host-a", _dir);
        var second = FederationIdentity.LoadOrCreate("host-a", _dir);

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Pin, second.Pin);
    }

    /// <summary>The persisted pin is the cert's own SPKI pin, by the shipped glp_link discipline.</summary>
    [Fact]
    public void PersistedPin_EqualsTheCertsOwnSpkiPin()
    {
        var identity = FederationIdentity.LoadOrCreate("host-a", _dir);

        Assert.Equal(QuicTransport.SpkiPin(identity.Cert), identity.Pin);
        Assert.Equal(
            identity.Pin,
            File.ReadAllText(Path.Combine(_dir, "host-a.fingerprint")).Trim());
    }

    /// <summary>Mutual auth presents the private key, so a keyless load is useless — and refused.</summary>
    [Fact]
    public void MintedIdentity_CarriesItsPrivateKey()
    {
        Assert.True(FederationIdentity.LoadOrCreate("host-a", _dir).Cert.HasPrivateKey);
    }

    /// <summary>Named identities do not collide: broker, guardian and oracle each get their own.</summary>
    [Fact]
    public void DistinctNames_GetDistinctIdentities()
    {
        var broker = FederationIdentity.LoadOrCreate("yng-broker", _dir);
        var guardian = FederationIdentity.LoadOrCreate("yng-guardian", _dir);

        Assert.NotEqual(broker.Pin, guardian.Pin);
        Assert.True(File.Exists(Path.Combine(_dir, "yng-broker.pfx")));
        Assert.True(File.Exists(Path.Combine(_dir, "yng-guardian.pfx")));
    }

    /// <summary>
    /// A sidecar that disagrees with the cert is REFUSED, not silently believed. Publishing one pin
    /// and presenting another is how a pin table goes dead without anyone noticing.
    /// </summary>
    [Fact]
    public void MismatchedFingerprint_IsRefused()
    {
        FederationIdentity.LoadOrCreate("host-a", _dir);
        File.WriteAllText(Path.Combine(_dir, "host-a.fingerprint"), "not-the-real-pin");

        var ex = Assert.Throws<InvalidOperationException>(
            () => FederationIdentity.LoadOrCreate("host-a", _dir));
        Assert.Contains("inconsistent", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The pfx and its sidecar are each replaced atomically but not as a PAIR, so a rotation killed
    /// between the two renames leaves a new key beside an old pin. That state must be refused — and
    /// the refusal must say which of the two it is, because "inconsistent" alone sends an operator
    /// hunting for corruption that is really an interrupted command.
    /// </summary>
    [Fact]
    public void InterruptedRotation_IsRefusedWithAnActionableDiagnosis()
    {
        var original = FederationIdentity.LoadOrCreate("host-a", _dir);
        var fpPath = Path.Combine(_dir, "host-a.fingerprint");
        var pfxPath = Path.Combine(_dir, "host-a.pfx");

        // exactly the surviving state of "rotate, then die before the sidecar rename"
        File.WriteAllText(fpPath, original.Pin);
        File.SetLastWriteTimeUtc(fpPath, DateTime.UtcNow.AddMinutes(-5));
        FederationIdentity.LoadOrCreate("host-a", _dir, rotate: true);
        File.WriteAllText(fpPath, original.Pin);
        File.SetLastWriteTimeUtc(fpPath, File.GetLastWriteTimeUtc(pfxPath).AddMinutes(-5));

        var ex = Assert.Throws<InvalidOperationException>(
            () => FederationIdentity.LoadOrCreate("host-a", _dir));
        Assert.Contains("INTERRUPTED ROTATION", ex.Message, StringComparison.Ordinal);
        Assert.Contains("rotate: true", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>The other direction: a pin published for a key this host does not hold must NOT be
    /// diagnosed as a rotation to finish — completing one would compound the error.</summary>
    [Fact]
    public void PinNewerThanKey_IsDiagnosedAsAPublishedPinForAKeyWeLack()
    {
        FederationIdentity.LoadOrCreate("host-a", _dir);
        var fpPath = Path.Combine(_dir, "host-a.fingerprint");
        File.WriteAllText(fpPath, "SomeOtherHostsPin=");
        File.SetLastWriteTimeUtc(fpPath, DateTime.UtcNow.AddHours(1));

        var ex = Assert.Throws<InvalidOperationException>(
            () => FederationIdentity.LoadOrCreate("host-a", _dir));
        Assert.Contains("does not hold", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("INTERRUPTED ROTATION", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>An empty sidecar is corruption, not an absent one — fail closed.</summary>
    [Fact]
    public void EmptyFingerprint_IsRefused()
    {
        FederationIdentity.LoadOrCreate("host-a", _dir);
        File.WriteAllText(Path.Combine(_dir, "host-a.fingerprint"), "   ");

        Assert.Throws<InvalidOperationException>(() => FederationIdentity.LoadOrCreate("host-a", _dir));
    }

    /// <summary>
    /// A pfx whose sidecar is absent is REPAIRED, not refused and not reminted: the pin is a pure
    /// function of the cert, so re-deriving it invents nothing, whereas minting would change the
    /// host's published identity. This is also the window a concurrent starter observes between the
    /// two atomic renames. (A sidecar that DISAGREES is the dangerous case, and still throws.)
    /// </summary>
    [Fact]
    public void MissingFingerprintSidecar_IsRederivedFromTheCert_NotReminted()
    {
        var original = FederationIdentity.LoadOrCreate("host-a", _dir);
        File.Delete(Path.Combine(_dir, "host-a.fingerprint"));

        var repaired = FederationIdentity.LoadOrCreate("host-a", _dir);

        Assert.Equal(original.Pin, repaired.Pin);
        Assert.False(repaired.Created);
        Assert.Equal(
            original.Pin,
            File.ReadAllText(Path.Combine(_dir, "host-a.fingerprint")).Trim());
    }

    /// <summary>
    /// THE RACE (codex review, critical): several callers starting at once on a virgin keystore must
    /// converge on ONE identity. Exactly one wins the atomic rename; the losers must adopt the
    /// winner's pin rather than each returning the keypair it happened to mint.
    /// </summary>
    [Fact]
    public void ConcurrentFirstStart_ConvergesOnOneIdentity()
    {
        var results = new FederationIdentity[16];
        Parallel.For(0, results.Length, i => results[i] = FederationIdentity.LoadOrCreate("host-a", _dir));

        Assert.Single(results.Select(r => r.Pin).Distinct());
        Assert.Equal(1, results.Count(r => r.Created)); // exactly one minter, never two
        Assert.Equal(
            results[0].Pin,
            File.ReadAllText(Path.Combine(_dir, "host-a.fingerprint")).Trim());
    }

    /// <summary>
    /// No temp file may survive a completed call: a leftover .tmp-* holding an unclaimed private key
    /// is both litter and key material lying around outside the keystore's contract.
    /// </summary>
    [Fact]
    public void ConcurrentFirstStart_LeavesNoTempKeyMaterialBehind()
    {
        Parallel.For(0, 8, _ => FederationIdentity.LoadOrCreate("host-a", _dir));

        Assert.Empty(Directory.GetFiles(_dir, "*.tmp-*"));
        Assert.Equal(2, Directory.GetFiles(_dir).Length); // exactly the pfx and its sidecar
    }

    /// <summary>
    /// What a RESTART actually is, from the keystore's point of view: nothing in memory, only the
    /// bytes on disk. Loading straight from the files must reproduce the same pin — this is the
    /// in-process proxy for the cross-process evidence (five probe processes, one pin).
    /// </summary>
    [Fact]
    public void AfterRestart_TheDiskAloneReproducesTheSamePin()
    {
        var minted = FederationIdentity.LoadOrCreate("host-a", _dir);

        var fromDiskOnly = FederationIdentity.Load(
            Path.Combine(_dir, "host-a.pfx"), Path.Combine(_dir, "host-a.fingerprint"));

        Assert.Equal(minted.Pin, fromDiskOnly.Pin);
        Assert.True(fromDiskOnly.Cert.HasPrivateKey);
    }

    /// <summary>
    /// A sidecar whose pfx has vanished is half-written state. Minting over it would change this
    /// host's published pin silently — exactly the Q-GLPNETA21-01 failure — so it is refused.
    /// </summary>
    [Fact]
    public void OrphanedFingerprint_RefusesToSilentlyMint()
    {
        FederationIdentity.LoadOrCreate("host-a", _dir);
        File.Delete(Path.Combine(_dir, "host-a.pfx"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => FederationIdentity.LoadOrCreate("host-a", _dir));
        Assert.Contains("rotate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Rotation is available, explicit, and changes the pin — never a side effect.</summary>
    [Fact]
    public void ExplicitRotation_ChangesThePin_AndOnlyWhenAsked()
    {
        var before = FederationIdentity.LoadOrCreate("host-a", _dir);
        var rotated = FederationIdentity.LoadOrCreate("host-a", _dir, rotate: true);
        var after = FederationIdentity.LoadOrCreate("host-a", _dir);

        Assert.NotEqual(before.Pin, rotated.Pin);
        Assert.Equal(rotated.Pin, after.Pin); // the rotated identity is the persisted one now
    }

    /// <summary>
    /// Rotation must leave the pair CONSISTENT, never a new key beside the old pin. Both files are
    /// replaced by atomic rename, so a reader sees the old pair or the new one and never a mixture.
    /// </summary>
    [Fact]
    public void Rotation_LeavesNoMixedTrustMaterial()
    {
        FederationIdentity.LoadOrCreate("host-a", _dir);
        var rotated = FederationIdentity.LoadOrCreate("host-a", _dir, rotate: true);

        var reloaded = FederationIdentity.Load(
            Path.Combine(_dir, "host-a.pfx"), Path.Combine(_dir, "host-a.fingerprint"));

        Assert.Equal(rotated.Pin, reloaded.Pin);
        Assert.Equal(QuicTransport.SpkiPin(reloaded.Cert), reloaded.Pin);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp-*"));
    }

    /// <summary>
    /// A pin and a node id are THE SAME 32 BYTES in two encodings (@gavriella-glpnet, 19:30Z). They
    /// must therefore never be confused, and both must come from one derivation — an operator who
    /// pastes hex into a base64 pin field gets every valid peer refused, and the refusal looks like
    /// a security event rather than the configuration error it is.
    /// </summary>
    [Fact]
    public void NodeIdAndPin_AreTheSameBytesInTwoEncodings()
    {
        var identity = FederationIdentity.LoadOrCreate("host-a", _dir);

        Assert.Equal(32, Convert.FromHexString(identity.NodeId).Length);
        Assert.Equal(Convert.FromBase64String(identity.Pin), Convert.FromHexString(identity.NodeId));
        Assert.NotEqual(identity.Pin, identity.NodeId);                  // never interchangeable as strings
        Assert.Equal(identity.NodeId, identity.NodeId.ToLowerInvariant()); // ordinal tables need one case
    }

    /// <summary>
    /// The SPKI must be published too: a pin is a HASH and cannot verify a signature, so without it
    /// an admitted peer can forge ops in another admitted peer's name.
    /// </summary>
    [Fact]
    public void Spki_IsPublishedAndIsWhatThePinHashes()
    {
        var identity = FederationIdentity.LoadOrCreate("host-a", _dir);

        var spki = Convert.FromBase64String(identity.Spki);
        Assert.Equal(
            System.Security.Cryptography.SHA256.HashData(spki),
            Convert.FromBase64String(identity.Pin));
    }

    /// <summary>The env var is a real seam: it, not the user profile, decides where keys live.</summary>
    [Fact]
    public void KeystoreEnvVar_OverridesTheDefaultDirectory()
    {
        var previous = Environment.GetEnvironmentVariable(FederationIdentity.KeystoreEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(FederationIdentity.KeystoreEnvVar, _dir);
            Assert.Equal(Path.GetFullPath(_dir), FederationIdentity.ResolveKeystoreDir());
        }
        finally
        {
            Environment.SetEnvironmentVariable(FederationIdentity.KeystoreEnvVar, previous);
        }
    }

    /// <summary>The default lands outside every repo, so a clone or clean cannot destroy the pins.</summary>
    [Fact]
    public void DefaultKeystoreDir_IsOutsideTheRepo()
    {
        var previous = Environment.GetEnvironmentVariable(FederationIdentity.KeystoreEnvVar);
        try
        {
            Environment.SetEnvironmentVariable(FederationIdentity.KeystoreEnvVar, null);
            var dir = FederationIdentity.ResolveKeystoreDir();

            Assert.Contains("federation", dir, StringComparison.OrdinalIgnoreCase);
            // "outside the repo" measured against where this test is RUNNING FROM, not by matching
            // a name — the app dir is itself called glpnet, so a name match proves nothing.
            Assert.DoesNotContain(
                Path.GetFullPath(AppContext.BaseDirectory), dir, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                dir, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FederationIdentity.KeystoreEnvVar, previous);
        }
    }

    /// <summary>A persisted identity must still be usable trust material for the real transport.</summary>
    [Fact]
    public void PersistedIdentity_IsUsableAsQuicTrustMaterial()
    {
        var identity = FederationIdentity.LoadOrCreate("host-a", _dir);
        X509Certificate2 cert = identity.Cert;

        Assert.True(cert.NotAfter > DateTime.Now.AddYears(5)); // long-lived: no implicit rotation
        Assert.Equal("CN=host-a", cert.Subject);
        Assert.Contains(
            cert.Extensions.OfType<X509EnhancedKeyUsageExtension>()
                .SelectMany(e => e.EnhancedKeyUsages.Cast<System.Security.Cryptography.Oid>())
                .Select(o => o.Value),
            v => v == "1.3.6.1.5.5.7.3.1"); // server EKU — it must be able to LISTEN
    }
}
