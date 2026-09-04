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

        var dir = keystorePath
            ?? Environment.GetEnvironmentVariable(KeystoreEnvVar)
            ?? SysPath.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "glpnet", "ynet");
        Directory.CreateDirectory(dir);

        // The lane name is caller-supplied and lands in a path; keep it to a filesystem-safe stem.
        var stem = string.Concat(laneName.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_'));
        var path = SysPath.Combine(dir, stem + ".nodekey");

        origin = IdentityOrigin.Minted;

        if (File.Exists(path))
        {
            if (TryLoadPkcs8(path, out var loaded))
            {
                origin = IdentityOrigin.Loaded;
                return loaded!;
            }
            // Unreadable key material. Mint a new one — but say so: the node id has CHANGED and every
            // peer holding the old pin is now wrong about this lane.
            origin = IdentityOrigin.RemintedCorrupt;
            File.Delete(path);
        }

        var minted = Generate(algorithm);
        var pkcs8 = minted.ExportPkcs8PrivateKey();

        try
        {
            using var fs = new System.IO.FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite); // holds a private key
            fs.Write(pkcs8);
        }
        catch (IOException) when (File.Exists(path))
        {
            // Another process created it between File.Exists and here. THEIRS WINS — a second id for
            // one lane is the exact fork this method exists to prevent.
            if (TryLoadPkcs8(path, out var winner))
            {
                minted.Dispose();
                origin = IdentityOrigin.Loaded;
                return winner!;
            }
            throw; // the winner wrote something unreadable: surface it rather than fork the identity
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pkcs8);
        }

        return minted;
    }

    /// <summary>PKCS#8 DER of the private key — the on-disk form, algorithm-agnostic.</summary>
    private byte[] ExportPkcs8PrivateKey()
        => Algorithm == SignatureAlgorithm.Ed25519
            ? PrivateKeyInfoFactory.CreatePrivateKeyInfo(_edPriv).GetDerEncoded()
            : _ecKey!.ExportPkcs8PrivateKey();

    /// <summary>
    /// Rebuild an identity from PKCS#8 DER, dispatching on the parsed key type so an Ed25519 and a
    /// P-256 keystore load over one path. Returns false on ANY read/parse failure (fail-closed): the
    /// caller re-mints and reports it, rather than a half-initialised identity escaping.
    /// </summary>
    private static bool TryLoadPkcs8(string path, out NodeIdentity? identity)
    {
        identity = null;
        byte[] der;
        try { der = File.ReadAllBytes(path); }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }

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
