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

    /// <summary>An empty sidecar is corruption, not an absent one — fail closed.</summary>
    [Fact]
    public void EmptyFingerprint_IsRefused()
    {
        FederationIdentity.LoadOrCreate("host-a", _dir);
        File.WriteAllText(Path.Combine(_dir, "host-a.fingerprint"), "   ");

        Assert.Throws<InvalidOperationException>(() => FederationIdentity.LoadOrCreate("host-a", _dir));
    }

    /// <summary>A pfx without its sidecar is refused rather than re-derived — the pin file is the
    /// published artifact, and inventing it hides that the publication step never happened.</summary>
    [Fact]
    public void MissingFingerprintSidecar_IsRefused()
    {
        FederationIdentity.LoadOrCreate("host-a", _dir);
        File.Delete(Path.Combine(_dir, "host-a.fingerprint"));

        Assert.Throws<FileNotFoundException>(() => FederationIdentity.LoadOrCreate("host-a", _dir));
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
