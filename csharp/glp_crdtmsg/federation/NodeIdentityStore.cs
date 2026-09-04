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
// same SPKI QuicLinkTransport.SpkiPin() hashes.
//
// THE SAME 32 BYTES — IN TWO DIFFERENT ENCODINGS, AND THAT DIFFERENCE WAS LOAD-BEARING.
// DeriveNodeId emits lowercase HEX; QuicTransport.SpkiPin emits BASE64. An earlier revision of this
// comment claimed the two "do not need reconciling, only naming consistently" and the console duly
// assigned the hex node id straight into `Pin`. Every correctly-configured peer was then refused by
// the TLS callback before federation could start — an admission failure produced entirely by an
// encoding mismatch, presenting as a pin mismatch, i.e. as a security event.
//
// So the conversion is EXPLICIT and lives here, next to the derivation it must agree with. Nothing
// downstream may assign a node id to a pin field or vice versa; it calls PinFromNodeId.

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

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        // FIRST-RUN MINTING IS A CROSS-PROCESS RACE, and it was unguarded.
        //
        // `serve`, `post` and `identity` can all start together on a fresh host — the runbook has
        // the operator do exactly that. An unlocked exists-then-write let two processes each mint a
        // DIFFERENT certificate, each return its own, and only one survive on disk. The loser then
        // signed operations with a key no peer could verify, and this host's effective node identity
        // differed between two commands run seconds apart. Both symptoms present as someone else's
        // bug: forged-attribution refusals, and a peer that will not admit you.
        //
        // The lock file is a separate path, so it can be taken exclusively without conflicting with
        // the key write itself.
        string lockPath = _path + ".lock";
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using var gate = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite,
                                                FileShare.None);

                // Re-check UNDER the lock: the process we queued behind may have just minted it.
                if (File.Exists(_path))
                    return X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(_path), password: null,
                        X509KeyStorageFlags.Exportable);

                var cert = Mint(commonName);

                // Write to a temp file and move it into place, so a reader never sees a partial key.
                string tmp = _path + ".tmp";
                File.WriteAllBytes(tmp, cert.Export(X509ContentType.Pkcs12));
                File.Move(tmp, _path, overwrite: false);
                RestrictToOwner(_path);
                return cert;
            }
            catch (IOException) when (attempt < 100)
            {
                Thread.Sleep(20);   // another process is minting; it will be there when we get in
            }
        }
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

    /// <summary>
    /// The transport's SPKI pin for a node id: the SAME 32 bytes, re-encoded from hex to base64.
    /// <para>
    /// This is the whole of the reconciliation between <see cref="DeriveNodeId"/> (hex, the operator
    /// surface) and <c>QuicTransport.SpkiPin</c> (base64, the TLS callback). Assigning one to the
    /// other refuses every correct peer.
    /// </para>
    /// </summary>
    /// <exception cref="FormatException">The node id is not 32 bytes of hexadecimal.</exception>
    public static string PinFromNodeId(string nodeId)
    {
        string s = (nodeId ?? "").Trim();
        if (s.Length != 64)
            throw new FormatException(
                $"node id must be 64 hex characters (SHA-256 of the SPKI); got {s.Length} — a pin is not a node id");
        return Convert.ToBase64String(Convert.FromHexString(s));
    }

    /// <summary>The inverse, for reading a pin back onto the operator surface.</summary>
    public static string NodeIdFromPin(string pin) =>
        Convert.ToHexStringLower(Convert.FromBase64String(pin));

    /// <summary>True when a string is a well-formed node id (64 hex characters).</summary>
    public static bool IsNodeId(string? s) =>
        s is { Length: 64 } && s.All(Uri.IsHexDigit);

    /// <summary>
    /// The base64 SubjectPublicKeyInfo of an identity — what a PEER needs in order to VERIFY this
    /// host's operation signatures. The pin is a hash and cannot verify a signature; this can.
    /// It is safe to publish: it is a public key.
    /// </summary>
    public static string ExportSpki(X509Certificate2 cert) =>
        Convert.ToBase64String(cert.PublicKey.ExportSubjectPublicKeyInfo());

    /// <summary>
    /// The node id implied by a published SPKI. Lets a configured public key be CHECKED against the
    /// node id and pin it claims to belong to, so a wrong key cannot be installed quietly.
    /// </summary>
    public static string NodeIdFromSpki(string spkiBase64) =>
        Convert.ToHexStringLower(SHA256.HashData(Convert.FromBase64String(spkiBase64)));

    /// <summary>
    /// Restrict the key to its owner, and FAIL CLOSED if that cannot be done.
    /// <para>
    /// The PFX is written unencrypted. On Unix it may be created group- or world-readable, so a
    /// failure to tighten permissions leaves a federation PRIVATE KEY exposed. Swallowing that and
    /// reporting a successful identity creation is the worst combination available: the operator is
    /// told everything is fine, and the key that authenticates this host to the estate is readable.
    /// </para>
    /// <para>
    /// So the file is REMOVED and the failure raised. A missing identity is recoverable in one
    /// command; a leaked one is not recoverable at all.
    /// </para>
    /// </summary>
    private static void RestrictToOwner(string path)
    {
        if (OperatingSystem.IsWindows()) return;   // NTFS inherits owner-only ACLs from the profile

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception ex)
        {
            try { File.Delete(path); } catch { /* the raise below is what matters */ }
            throw new IOException(
                $"could not restrict '{path}' to owner-only permissions, so the federation private "
                + "key would be left readable by others. The key has been removed rather than "
                + "reported as successfully created.", ex);
        }

        // VERIFY, do not assume. SetUnixFileMode can succeed on a filesystem that does not honour
        // it (a mounted share, some container overlays), and an unenforced permission is not one.
        var mode = File.GetUnixFileMode(path);
        if ((mode & (UnixFileMode.GroupRead | UnixFileMode.OtherRead
                     | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) != 0)
        {
            try { File.Delete(path); } catch { }
            throw new IOException(
                $"'{path}' still reports mode {mode} after owner-only permissions were applied — the "
                + "filesystem does not enforce them. The federation private key has been removed.");
        }
    }
}
