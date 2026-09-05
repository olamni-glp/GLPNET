using System.Security.Cryptography;
using SysPath = System.IO.Path;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace Ynet.Transport.Capability;

/// <summary>How a <see cref="NodeIdentity.LoadOrMint"/> call obtained the identity (FR-102-3).</summary>
/// <remarks>
/// A value other than <see cref="Loaded"/> means every holder of this lane's old pin now holds a
/// stale one. It is returned so the caller can publish that fact; it is never swallowed.
/// </remarks>
public enum IdentityOrigin
{
    /// <summary>The keystore already held this lane's key. The node id is unchanged.</summary>
    Loaded,

    /// <summary>First use on this host — a keypair was minted and written.</summary>
    Minted,

    /// <summary>🔴 The stored key was unreadable and a NEW one was minted. The node id CHANGED.</summary>
    RemintedCorrupt,
}

public sealed partial class NodeIdentity
{
    /// <summary>Default keystore root, overridden by <c>$YNET_NODE_KEYSTORE</c>.</summary>
    private const string KeystoreEnvVar = "YNET_NODE_KEYSTORE";

    /// <summary>
    /// Load this lane's persistent node identity, minting it on first use (FR-102-1..4).
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Why this exists.</b> <see cref="Generate()"/> mints a FRESH keypair on every call, so
    /// <c>nodeId = H(SPKI)</c> changes at every process start. That is correct for a test wanting two
    /// unrelated nodes and <b>wrong for anything another host pins, votes on, or addresses</b> — the
    /// same defect class found in <c>CreateDevCert</c> (five runs, five pins) and fixed for the
    /// federation certificate in <c>c2303104</c>. This is that fix for the node identity itself.
    ///
    /// <para><b>Concurrency (FR-102-2).</b> The key file is written <see cref="FileMode.CreateNew"/>;
    /// the loser of a first-use race <b>loads the winner's file</b>. Last-writer-wins is forbidden
    /// here — it silently forks one lane into two identities, which is two votes.</para>
    ///
    /// <para><b>No expiry.</b> Unlike a certificate, a raw keypair does not expire, so there is no
    /// scheduled rotation and no <c>recreated-expired</c> case. The ONLY way the id changes is
    /// <see cref="IdentityOrigin.RemintedCorrupt"/>, and that is reported.</para>
    ///
    /// <para>The algorithm is <b>not</b> re-selected on load: whatever was minted stays in force, so
    /// a host that fell back to P-256 (DEC-CRYPTO-1) does not silently change identity when the
    /// Ed25519 provider later appears.</para>
    /// </remarks>
    /// <param name="laneName">the stable per-lane identity, e.g. <c>shiras.glpnet</c></param>
    /// <param name="origin">how the identity was obtained — publish it when it is not
    /// <see cref="IdentityOrigin.Loaded"/></param>
    /// <param name="keystorePath">override; default <c>$YNET_NODE_KEYSTORE</c>, else
    /// <c>LocalApplicationData/glpnet/ynet</c></param>
    /// <param name="algorithm">algorithm used only when MINTING; ignored on load</param>
    public static NodeIdentity LoadOrMint(
        string laneName,
        out IdentityOrigin origin,
        string? keystorePath = null,
        SignatureAlgorithm algorithm = SignatureAlgorithm.Ed25519)
    {
        ArgumentException.ThrowIfNullOrEmpty(laneName);

        var dir = ResolveKeystoreDir(keystorePath);
        Directory.CreateDirectory(dir);

        // 🔴 REJECT, don't sanitise. Mapping every unsupported character to '_' is NOT injective:
        // `a/b` and `a?b` both become `a_b`, so two configured lanes silently share ONE key file and
        // therefore ONE node id — one signing identity answering for two lanes, which is the identity
        // fork this whole type exists to prevent, arriving through the front door. A name we cannot
        // represent losslessly is a configuration error, and it is said out loud.
        var path = SysPath.Combine(dir, RequireLaneStem(laneName) + ".nodekey");

        origin = IdentityOrigin.Minted;

        if (File.Exists(path))
        {
            // 🔴 A key we cannot READ is not a key that is WRONG. TryLoadPkcs8 now throws on a
            // storage-layer failure (locked file, permission, transient I/O) and returns false ONLY
            // for bytes it actually parsed and rejected. Before this split, a peer lane holding the
            // file open for one moment was enough to delete it and mint a new id — turning a
            // transient, self-healing condition into a PERMANENT identity change that invalidates
            // every pin in the fleet. Fail closed: refuse to run rather than rotate by accident.
            if (TryLoadPkcs8(path, out var loaded))
            {
                origin = IdentityOrigin.Loaded;
                return loaded!;
            }
            // Genuinely malformed key material. Mint a new one — but say so: the node id has CHANGED
            // and every peer holding the old pin is now wrong about this lane.
            origin = IdentityOrigin.RemintedCorrupt;
            File.Delete(path);
        }

        var minted = Generate(algorithm);
        var pkcs8 = minted.ExportPkcs8PrivateKey();

        // 🔴 WRITE-THEN-RENAME, not write-in-place. A crash midway through an in-place write leaves a
        // TRUNCATED .nodekey, which the next load reads as corrupt and re-mints — i.e. a power cut
        // during first use would change this lane's node id. The temp file is unique, so it never
        // collides; the move is atomic and NON-overwriting, so a first-use race still resolves to ONE
        // key with the loser loading the winner's (FR-102-2). Both properties at once, which
        // CreateNew-and-write and rename-over-the-top each give only half of.
        var temp = path + "." + Environment.ProcessId.ToString() + "-" + Guid.NewGuid().ToString("N")[..8] + ".tmp";

        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
            };
            // Set the mode AT CREATION: chmod-after-create leaves a window, however brief, in which a
            // file destined to hold a private key exists under the ambient umask.
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

            using (var fs = new System.IO.FileStream(temp, options))
            {
                // The DACL goes on while the file is still EMPTY — the Windows half of the
                // UnixCreateMode above. Writing first and tightening after would publish the private
                // key under the directory's INHERITED ACL for the width of the write. That window is
                // brief, but a race is not made safe by being short, and under an explicit
                // keystorePath or $YNET_NODE_KEYSTORE the directory may be one anybody can read.
                if (OperatingSystem.IsWindows())
                    RestrictToCurrentUser(temp);
                fs.Write(pkcs8);
                fs.Flush(flushToDisk: true); // the rename must not beat the bytes to disk
            }

            File.Move(temp, path, overwrite: false);
        }
        catch (IOException) when (File.Exists(path))
        {
            // Another process got there first. THEIRS WINS — a second id for one lane is the exact
            // fork this method exists to prevent.
            TryDelete(temp);
            if (TryLoadPkcs8(path, out var winner))
            {
                minted.Dispose();
                origin = IdentityOrigin.Loaded;
                return winner!;
            }
            throw; // the winner wrote something unreadable: surface it rather than fork the identity
        }
        catch
        {
            TryDelete(temp); // never leave key material in a stray temp file
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs8);
        }

        return minted;
    }

    /// <summary>
    /// The durable directory this lane's key lives in. Order: explicit argument, then
    /// <c>$YNET_NODE_KEYSTORE</c>, then <c>LocalApplicationData/glpnet/ynet</c>.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>Never returns a relative path, and never falls back to a temporary one.</b> On a
    /// headless Unix service <c>LocalApplicationData</c> can be empty; <c>Path.Combine</c> then
    /// yields the RELATIVE <c>glpnet/ynet</c>, so the same lane gets a different identity per
    /// working directory and a repo clean deletes it. A temp-directory fallback is worse still: it
    /// is reaped on a schedule the fleet does not control, so every published pin expires without
    /// anyone touching the code. Both are refused with an actionable message instead.
    /// </remarks>
    internal static string ResolveKeystoreDir(string? keystorePath = null)
    {
        // An EXPLICIT location — the argument or the env var — is the caller's deliberate choice and
        // is honoured as given, including a temporary one: that is the seam tests and deployments
        // use. What is refused below is only the DEFAULT silently degrading into somewhere
        // undurable, because nobody chose that and nobody can see it happen.
        var chosen = keystorePath;
        if (string.IsNullOrWhiteSpace(chosen))
            chosen = Environment.GetEnvironmentVariable(KeystoreEnvVar);
        if (!string.IsNullOrWhiteSpace(chosen))
            return SysPath.GetFullPath(chosen.Trim());

        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(baseDir))
            throw new InvalidOperationException(
                "no durable home for the YNET node key: LocalApplicationData is empty on this "
                + "host (typical of a headless service account). Set $" + KeystoreEnvVar
                + " to an ABSOLUTE, persistent directory. Refusing to fall back to a relative path: "
                + "'glpnet/ynet' resolves against the WORKING DIRECTORY, so the lane would carry a "
                + "different identity per launch directory and a repo clean would delete it.");

        var full = SysPath.GetFullPath(SysPath.Combine(baseDir, "glpnet", "ynet"));
        if (IsUnderTemp(full))
            throw new InvalidOperationException(
                "refusing to place the YNET node key under a temporary directory by DEFAULT ("
                + full + "): LocalApplicationData resolves inside temp on this host. Temp is reaped "
                + "on a policy this fleet does not control, so the lane's node id would change with "
                + "no code change and every published pin would go stale. Set $" + KeystoreEnvVar
                + " to a persistent directory to choose this deliberately.");
        return full;
    }

    private static bool IsUnderTemp(string full)
    {
        var temp = SysPath.GetFullPath(SysPath.GetTempPath())
            .TrimEnd(SysPath.DirectorySeparatorChar, SysPath.AltDirectorySeparatorChar);
        if (temp.Length == 0) return false;
        var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return full.Equals(temp, cmp)
            || full.StartsWith(temp + SysPath.DirectorySeparatorChar, cmp);
    }

    /// <summary>
    /// The filename stem for a lane, or a refusal. Accepts only characters that round-trip to
    /// exactly one file: letters, digits, <c>-</c>, <c>_</c>, <c>.</c>. See the call site for why a
    /// lossy substitution is not an acceptable alternative.
    /// </summary>
    internal static string RequireLaneStem(string laneName)
    {
        foreach (var c in laneName)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_' or '.') continue;
            throw new ArgumentException(
                "lane name '" + laneName + "' contains " + Describe(c) + ", which cannot appear in a "
                + "keystore filename. Two lane names that differ only in such characters would map "
                + "to ONE key file and therefore ONE node id — a single signing identity answering "
                + "for both lanes. Use only letters, digits, '-', '_' and '.' (e.g. 'shiras.glpnet').",
                nameof(laneName));
        }
        // '.' and '..' name directories, not files; and a leading '.' hides the key on Unix.
        if (laneName is "." or "..")
            throw new ArgumentException(
                "lane name '" + laneName + "' is a directory reference, not a name.", nameof(laneName));
        return laneName;
    }

    private static string Describe(char c)
        => char.IsControl(c) || char.IsWhiteSpace(c)
            ? "the character U+" + ((int)c).ToString("X4")
            : "'" + c + "'";

    /// <summary>Replace the file's DACL with a single full-control ACE for the current user and
    /// break inheritance — the Windows half of "owner-only", mirroring the federation keystore.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RestrictToCurrentUser(string path)
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var owner = identity.User
            // Fail rather than fall back to the inherited ACL: silently storing a private key under
            // whatever the parent directory permits is exactly what this method prevents, and the
            // caller cannot see that it happened.
            ?? throw new InvalidOperationException(
                "cannot restrict the YNET node keystore file: the current Windows identity has no "
                + "user SID to grant it to. Refusing to write private key material under an "
                + "inherited ACL.");

        var security = new System.Security.AccessControl.FileSecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
            owner,
            System.Security.AccessControl.FileSystemRights.FullControl,
            System.Security.AccessControl.AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { /* best effort: a stray temp is bad, a throw here would be worse */ }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>PKCS#8 DER of the private key — the on-disk form, algorithm-agnostic.</summary>
    private byte[] ExportPkcs8PrivateKey()
        => Algorithm == SignatureAlgorithm.Ed25519
            ? PrivateKeyInfoFactory.CreatePrivateKeyInfo(_edPriv).GetDerEncoded()
            : _ecKey!.ExportPkcs8PrivateKey();

    /// <summary>
    /// Rebuild an identity from PKCS#8 DER, dispatching on the parsed key type so an Ed25519 and a
    /// P-256 keystore load over one path. Returns false ONLY for bytes that were read and rejected;
    /// a storage-layer failure THROWS, because the caller treats false as "re-mint" and a transient
    /// I/O error must never rotate this lane's node id.
    /// </summary>
    private static bool TryLoadPkcs8(string path, out NodeIdentity? identity)
    {
        identity = null;
        // 🔴 A storage failure is NOT a corrupt key. Propagating it is the whole point: the caller's
        // false-branch DELETES the file and mints a new id, so swallowing a transient lock or a
        // permission error here would convert it into a permanent, fleet-visible identity change.
        var der = File.ReadAllBytes(path);

        if (der.Length == 0) return false;

        try
        {
            var key = PrivateKeyFactory.CreateKey(der);
            if (key is Ed25519PrivateKeyParameters ed)
            {
                var spki = SubjectPublicKeyInfoFactory
                    .CreateSubjectPublicKeyInfo(ed.GeneratePublicKey()).GetDerEncoded();
                identity = new NodeIdentity(SignatureAlgorithm.Ed25519, ed, ecKey: null, spki, KeyState.Active);
                return true;
            }

            var ec = ECDsa.Create();
            try
            {
                ec.ImportPkcs8PrivateKey(der, out _);
                identity = new NodeIdentity(
                    SignatureAlgorithm.EcdsaP256, edPriv: null, ec, ec.ExportSubjectPublicKeyInfo(), KeyState.Active);
                return true;
            }
            catch
            {
                ec.Dispose();
                return false;
            }
        }
        catch
        {
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(der);
        }
    }
}
