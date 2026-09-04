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
/// The federation private key could not be protected, so it was REMOVED rather than used.
/// <para>
/// A distinct type, deliberately: as a plain IOException it was caught by the minting retry loop,
/// whose next iteration took the existing-file fast path and returned the unprotected key as a
/// success. A security refusal and lock contention must not be indistinguishable to a catch clause.
/// </para>
/// </summary>
public sealed class InsecureKeyPermissionsException : IOException
{
    public InsecureKeyPermissionsException(string message, Exception? inner = null)
        : base(message, inner) { }
}

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
        {
            // VERIFY BEFORE LOADING. Checking permissions only on the mint path meant a key that
            // became world-readable afterwards — or was written by an older build before this check
            // existed — was loaded and used without complaint on every subsequent run.
            AssertOwnerOnly(_path);
            return X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(_path), password: null,
                X509KeyStorageFlags.Exportable);
        }

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
                //
                // AND RE-VERIFY ITS PERMISSIONS. Loading here without the check was a way around the
                // security refusal: if the process we waited for wrote the key and THEN failed to
                // harden it, this waiter picked up the exposed private key and reported success —
                // bypassing the very refusal the other process had just raised.
                if (File.Exists(_path))
                {
                    AssertOwnerOnly(_path);
                    return X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(_path), password: null,
                        X509KeyStorageFlags.Exportable);
                }

                var cert = Mint(commonName);

                // Write to a temp file and move it into place, so a reader never sees a partial key.
                string tmp = _path + ".tmp";
                File.WriteAllBytes(tmp, cert.Export(X509ContentType.Pkcs12));
                File.Move(tmp, _path, overwrite: false);
                RestrictToOwner(_path);
                return cert;
            }
            catch (InsecureKeyPermissionsException)
            {
                // NEVER retried. This exception is a SECURITY refusal, not lock contention — and
                // the retry loop's next iteration would take the existing-file fast path and hand
                // back the very unprotected key this refusal exists to withhold. Distinguishing the
                // two by TYPE is the whole fix; catching IOException caught both.
                throw;
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
    /// <summary>
    /// Verify a key's Windows ACL grants no one but its owner and the system.
    /// <para>
    /// The default path sits under the user profile and inherits owner-only ACLs — but
    /// <c>identity_path</c> can name ANY directory, and an unencrypted PFX in a shared one inherits
    /// that directory's permissions while startup reports success. "The usual location is safe" is
    /// not a check; this is.
    /// </para>
    /// </summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void AssertOwnerOnlyWindows(string path)
    {
        System.Security.AccessControl.AuthorizationRuleCollection rules;
        System.Security.Principal.IdentityReference? owner;
        try
        {
            var security = new FileInfo(path).GetAccessControl();
            owner = security.GetOwner(typeof(System.Security.Principal.SecurityIdentifier));
            rules = security.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier));
        }
        catch (Exception ex)
        {
            throw new InsecureKeyPermissionsException(
                $"could not read the ACL of '{path}', so it cannot be confirmed private. Refusing to "
                + "use a federation private key of unknown protection.", ex);
        }

        // Identities that may legitimately hold the key: its owner, the account we are running as,
        // SYSTEM, and the Administrators group.
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (owner is not null) allowed.Add(owner.Value);
        using (var me = System.Security.Principal.WindowsIdentity.GetCurrent())
        {
            if (me.User is not null) allowed.Add(me.User.Value);
        }
        allowed.Add(new System.Security.Principal.SecurityIdentifier(
            System.Security.Principal.WellKnownSidType.LocalSystemSid, null).Value);
        allowed.Add(new System.Security.Principal.SecurityIdentifier(
            System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid, null).Value);

        foreach (System.Security.AccessControl.FileSystemAccessRule rule in rules)
        {
            if (rule.AccessControlType != System.Security.AccessControl.AccessControlType.Allow) continue;
            if (allowed.Contains(rule.IdentityReference.Value)) continue;

            // Any READ of an unencrypted PFX is a disclosure of the private key.
            if ((rule.FileSystemRights & System.Security.AccessControl.FileSystemRights.Read) == 0) continue;

            // WARN, DO NOT REFUSE — unless the operator has asked for strict enforcement.
            //
            // Measured before shipping this: an ordinary key under %TEMP% trips it, because Windows
            // ACLs routinely grant read to groups the owner belongs to. Deleting a working key on
            // that basis would be a FALSE POSITIVE THAT BREAKS THE DAEMON — strictly worse than the
            // exposure it guards against, and the opposite of the fleet's own rule that a missing
            // prerequisite makes a daemon UNHEALTHY, never UNSTARTABLE.
            //
            // So the finding is REPORTED and remains visible, and `require_owner_only_key` turns it
            // into a refusal for deployments that want one. Unmeasured is not the same as safe, and
            // this says which it is.
            LastKeyPermissionWarning =
                $"'{path}' grants read access to '{rule.IdentityReference.Value}'. The federation "
                + "private key may be readable by another principal. Set require_owner_only_key to "
                + "refuse rather than warn.";

            if (!RequireOwnerOnlyKey) return;

            try { File.Delete(path); } catch { }
            throw new InsecureKeyPermissionsException(LastKeyPermissionWarning);
        }
    }

    /// <summary>
    /// When true, a key readable by another principal is REFUSED rather than reported. Off by
    /// default because the Windows check false-positives on ordinary group ACLs, and a guard that
    /// deletes a working key is worse than the exposure it prevents.
    /// </summary>
    public static bool RequireOwnerOnlyKey { get; set; }

    /// <summary>
    /// The most recent key-permission concern, or null. Surfaced so an unrefused finding is still
    /// VISIBLE — a warning nobody can read is the same as no check at all.
    /// </summary>
    public static string? LastKeyPermissionWarning { get; private set; }

    private static void RestrictToOwner(string path)
    {
        if (!OperatingSystem.IsWindows())   // NTFS inherits owner-only ACLs from the profile
        {
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch (Exception ex)
            {
                try { File.Delete(path); } catch { /* the raise below is what matters */ }
                throw new InsecureKeyPermissionsException(
                    $"could not restrict '{path}' to owner-only permissions, so the federation private "
                    + "key would be left readable by others. The key has been removed rather than "
                    + "reported as successfully created.", ex);
            }
        }

        // VERIFIED THROUGH THE SAME PLACE the load path verifies, on every OS. Returning early on
        // Windows skipped the check entirely, which also made the refusal unreachable in a test on
        // the machine that has to ship it.
        AssertOwnerOnly(path);
    }

    /// <summary>
    /// Verify — do not assume — that a key on disk is owner-only, and REMOVE it if it is not.
    /// <para>
    /// <c>SetUnixFileMode</c> can succeed on a filesystem that does not honour it (a mounted share,
    /// some container overlays), and an unenforced permission is not a permission. This runs on the
    /// LOAD path too, so a key that became readable after it was minted — or was written by an older
    /// build before this check existed — is caught rather than used silently on every later run.
    /// </para>
    /// </summary>
    /// <summary>
    /// Overrides the permission verdict, so the refusal path is reachable in a test.
    /// <para>
    /// The real check is Unix-only and returns immediately on Windows, so on this estate's hosts a
    /// test written against it cannot distinguish "refuses an insecure key" from "never runs here".
    /// A mutation removing the refusal SURVIVED for exactly that reason. Injecting the verdict is
    /// the only way to assert the behaviour on the machine that has to ship it.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <see cref="AsyncLocal{T}"/>, not a plain static. xUnit runs test CLASSES in parallel, so a
    /// plain static leaked one test's override into another's <c>LoadOrMint</c> — two tests failed
    /// intermittently and neither had a defect. A shared mutable test seam is a race in the test
    /// harness, which is the one place a flaky failure is most likely to be dismissed as noise.
    /// </remarks>
    private static readonly AsyncLocal<Func<string, bool>?> _permissionOverride = new();

    internal static Func<string, bool>? PermissionsAreInsecureOverride
    {
        get => _permissionOverride.Value;
        set => _permissionOverride.Value = value;
    }

    private static void AssertOwnerOnly(string path)
    {
        if (PermissionsAreInsecureOverride is { } probe)
        {
            if (!probe(path)) return;
            try { File.Delete(path); } catch { }
            throw new InsecureKeyPermissionsException(
                $"'{path}' is readable by others — the federation private key has been removed "
                + "rather than used.");
        }

        if (OperatingSystem.IsWindows())
        {
            AssertOwnerOnlyWindows(path);
            return;
        }

        UnixFileMode mode;
        try { mode = File.GetUnixFileMode(path); }
        catch (Exception ex)
        {
            throw new InsecureKeyPermissionsException(
                $"could not read the permissions of '{path}', so it cannot be confirmed private. "
                + "Refusing to load a federation private key of unknown protection.", ex);
        }

        if ((mode & (UnixFileMode.GroupRead | UnixFileMode.OtherRead
                     | UnixFileMode.GroupWrite | UnixFileMode.OtherWrite)) == 0)
            return;

        try { File.Delete(path); } catch { }
        throw new InsecureKeyPermissionsException(
            $"'{path}' reports mode {mode} — the federation private key is readable by others. "
            + "It has been removed rather than used.");
    }
}
