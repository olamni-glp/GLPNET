// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Persisted node identity (feature 102, T007).
//
// Contract federation-config.md G4 / data-model I-1..I-4 / FR-007.
//
// THE GAP THIS CLOSES. QuicLinkTransport.CreateDevCert() mints a FRESH certificate on every call.
// A pin taken from a probe run is therefore EPHEMERAL — stale before it reaches the peer you sent
// it to. A federation identity that changes per process is not an identity. So the key is minted
// once and loaded thereafter, and the node id derived from it is what gets published.
//
// nodeId = SHA-256(SPKI), the same derivation Ynet.Transport.Capability.NodeIdentity uses, and the
// same SPKI QuicLinkTransport.SpkiPin() hashes. The transport pin and the node id are THE SAME
// VALUE UNDER TWO NAMES — they do not need reconciling by a mapping table, only naming consistently.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>
/// Mints once, loads thereafter. The node id survives restarts, which is the whole point.
/// </summary>
public sealed class NodeIdentityStore
{
    private readonly string _path;

    public NodeIdentityStore(string path) => _path = path;

    /// <summary>The default per-host key path, beside the config and outside the repo.</summary>
    public static string DefaultPath() =>
        Path.Combine(Path.GetDirectoryName(FederationConfig.DefaultPath())!, "node.key");

    /// <summary>True if an identity has already been minted on this host.</summary>
    public bool Exists => File.Exists(_path);

    /// <summary>
    /// Load the persisted identity, or mint and persist one on first use.
    /// </summary>
    /// <param name="commonName">Subject CN — cosmetic; identity is the key, not the name.</param>
    public X509Certificate2 LoadOrMint(string commonName)
    {
        if (File.Exists(_path))
            return X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(_path), password: null,
                X509KeyStorageFlags.Exportable);

        var cert = Mint(commonName);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllBytes(_path, cert.Export(X509ContentType.Pkcs12));
        RestrictToOwner(_path);
        return cert;
    }

    private static X509Certificate2 Mint(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            new OidCollection { new Oid("1.3.6.1.5.5.7.3.1"), new Oid("1.3.6.1.5.5.7.3.2") }, critical: false));
        var now = DateTimeOffset.UtcNow;
        return req.CreateSelfSigned(now.AddDays(-1), now.AddYears(5));
    }

    /// <summary>
    /// nodeId = SHA-256(SubjectPublicKeyInfo), lowercase hex. Independent of every address the host
    /// answers on — which is what makes a two-NIC host count as ONE participant (FR-007 / SC-006).
    /// </summary>
    public static string DeriveNodeId(X509Certificate2 cert) =>
        Convert.ToHexStringLower(SHA256.HashData(cert.PublicKey.ExportSubjectPublicKeyInfo()));

    private static void RestrictToOwner(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // Best effort. Failing to tighten permissions must not prevent the identity from
            // existing — but it is never reported as success either; the caller surfaces the path.
        }
    }
}
