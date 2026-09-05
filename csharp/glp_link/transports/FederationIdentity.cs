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
/// <param name="Created">True iff this call MINTED a keypair — a first run, or an explicit
/// rotation. False iff it loaded material that already existed. It is NOT a "first run" flag on
/// its own: a rotating caller already knows it asked to rotate.</param>
/// <remarks>
/// 🔴 <b>THE KEY IS THE ONLY SOURCE OF TRUTH FOR THE PIN.</b> <c>Pin == base64(SHA-256(SPKI(key)))</c>,
/// so it is DERIVED on every read and never believed from disk. The <c>.fingerprint</c> file is an
/// operator-readable <b>cache</b>: it is refreshed when it drifts and reported when it was wrong,
/// but it is never trust material and a disagreement with it never refuses a start (ruling
/// <c>Q-glpnetshiras-48</c>). Storing a value that is computable from another value is what let two
/// non-atomic writes wedge a cold start; deriving removes the possibility rather than the window.
///
/// <para>🔴 <b>TWO IDENTITIES, ONE HOST — and they are not interchangeable.</b> Everything on this record
/// derives from the host's <b>TLS certificate</b>: it anchors the QUIC transport and is what an SPKI
/// pin pins. It is <b>not</b> the identity <c>YnetSession</c> authenticates a peer by — that is the
/// independent <c>NodeIdentity</c> Ed25519 keypair, whose <c>nodeId = H(pubkey)</c> is what a peer
/// resolves, votes on and files board ops under.
///
/// <para>Putting a certificate-derived id into <c>INodeAddressResolver</c> gets a genuine connection
/// refused with <c>IdentityMismatch</c>, and this SPKI cannot verify that lane's board signatures at
/// all. That is the fleet's recurring <i>id-class</i> defect (a value that verifies, is signed by its
/// rightful holder, and still names the wrong thing) at a FOURTH site — so the two are named apart
/// here rather than left to a reader's discipline.</para>
/// </remarks>
/// <summary>What the on-disk <c>.fingerprint</c> cache was holding when the identity was loaded.</summary>
/// <remarks>
/// 🔴 The cache is NOT trust material and disagreeing with it is not an error state. The pin is
/// <c>base64(SHA-256(SPKI))</c> of the key, so the key IS the pin — see the class remarks.
/// </remarks>
public enum PinCacheState
{
    /// <summary>The cache agreed with the key. Nothing to report.</summary>
    Fresh,

    /// <summary>No cache existed; it was written from the key. Normal on first read and mid-race.</summary>
    Rederived,

    /// <summary>🔴 The cache DISAGREED and was overwritten from the key. This host's published pin
    /// is not what the cache said — say so out loud and re-publish.</summary>
    Refreshed,
}

public sealed record FederationIdentity(
    X509Certificate2 Cert, string Pin, string PfxPath, bool Created)
{
    /// <summary>How the <c>.fingerprint</c> cache stood when this identity was loaded.</summary>
    public PinCacheState PinCache { get; init; } = PinCacheState.Fresh;

    /// <summary>What a disagreeing cache was holding, for the operator's report. Null unless
    /// <see cref="PinCache"/> is <see cref="PinCacheState.Refreshed"/>.</summary>
    public string? StaleCachedPin { get; init; }

    /// <summary>Plain-language reading of a disagreement (interrupted rotation vs. a pin published
    /// for a key this host does not hold). Null unless the cache disagreed.</summary>
    public string? PinCacheDiagnosis { get; init; }

    /// <summary>True when the caller must RE-PUBLISH this host's pin — the cache was wrong.</summary>
    public bool RequiresRepublication => PinCache == PinCacheState.Refreshed;

    /// <summary>
    /// The same 32 bytes as <see cref="Pin"/>, in lowercase HEX rather than base64 — the fleet's
    /// <c>node_id</c>. Published because @gavriella-glpnet measured (2026-09-04T19:30Z) an operator
    /// writing a hex node id into a base64 pin field: **every correctly configured peer was refused,
    /// and the refusal presented as a pin mismatch — a configuration bug wearing a security event's
    /// clothes.** Exposing both encodings from ONE derivation is how that stops being possible.
    /// </summary>
    public string TlsNodeId => Convert.ToHexString(
        SHA256.HashData(Cert.PublicKey.ExportSubjectPublicKeyInfo())).ToLowerInvariant();

    /// <summary>
    /// 🔴 <b>Do not use as a YNET node id.</b> Retained only so existing callers fail LOUDLY at
    /// compile time rather than silently keep publishing a TLS hash into an id space that refuses
    /// it. See <see cref="TlsNodeId"/> and the class remarks.
    /// </summary>
    [Obsolete("This is the TLS CERTIFICATE's id, not the YNET node id. Use TlsNodeId for the "
        + "transport anchor, or NodeIdentity.NodeId for the identity YnetSession authenticates. "
        + "Entering this value into INodeAddressResolver refuses every genuine peer.", error: true)]
    public string NodeId => TlsNodeId;

    /// <summary>
    /// The full SubjectPublicKeyInfo, base64. **A pin is a hash and therefore cannot verify a
    /// signature** — an admitted peer could otherwise forge ops in another admitted peer's name,
    /// including the leadership tie-break, which is monotone and unfixable after a CRDT merge.
    /// Publish this beside the pin so an op's claimed author can actually be checked.
    /// </summary>
    public string TlsSpki => Convert.ToBase64String(Cert.PublicKey.ExportSubjectPublicKeyInfo());

    /// <summary>
    /// 🔴 <b>Cannot verify a lane's board signatures.</b> This is the TLS certificate's public key;
    /// ops are signed with the independent <c>NodeIdentity</c> keypair. Kept as a compile-time error
    /// so the substitution is caught at the call site instead of at a peer's refusal.
    /// </summary>
    [Obsolete("This is the TLS CERTIFICATE's SPKI, not the YNET signing key. Use TlsSpki for the "
        + "transport anchor, or NodeIdentity.PublicKeySpki to verify signatures.", error: true)]
    public string Spki => TlsSpki;

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
            // 🔴 NOT a temp fallback. This key is the anchor for an SPKI pin that other hosts hold
            // for years; parking it under the OS temp directory means a reap policy this fleet does
            // not control silently invalidates every exchanged pin, and mTLS then refuses every peer
            // — indistinguishable from a dead transport, with nothing in the code having changed.
            // An unusable environment is a configuration error and is reported as one.
            throw new InvalidOperationException(
                "no durable home for the federation identity: LocalApplicationData is empty on this "
                + "host (typical of a headless service account). Set $" + KeystoreEnvVar
                + " to an ABSOLUTE, persistent directory. Refusing a temporary-directory fallback: "
                + "every SPKI pin published from it would expire at the next temp sweep.");
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

        // A pfx with no sidecar yet is NOT an error: it is either a concurrent starter caught between
        // the two renames, or an interrupted first run. The pin is a pure function of the cert, so
        // re-deriving it invents nothing — the pfx is the authority. (A sidecar that DISAGREES with
        // the cert is a different animal entirely and is still refused, in Load.)
        if (!rotate && File.Exists(pfxPath))
            return LoadAfterEnsuringSidecar(pfxPath, fpPath);

        // A half-written pair (pfx gone, sidecar left behind) is corruption, not a first run: minting
        // over it would silently change a pin peers may already hold. Refuse unless asked to rotate.
        if (!rotate && File.Exists(fpPath))
            throw new InvalidOperationException(
                $"federation keystore is inconsistent: '{fpPath}' exists but '{pfxPath}' does not. "
                + "Refusing to mint a replacement identity, because that would silently change this "
                + "host's published SPKI pin. Remove the stale sidecar deliberately, or pass rotate: true.");

        return Create(dir, pfxPath, fpPath, commonName, rotate);
    }

    /// <summary>Load an identity pair, fail-closed on any missing/inconsistent material.</summary>
    public static FederationIdentity Load(string pfxPath, string fingerprintPath)
    {
        if (!File.Exists(pfxPath))
            throw new FileNotFoundException(
                $"federation identity missing: '{pfxPath}' — fail-closed, no ephemeral fallback "
                + "(an ephemeral cert would present a pin no peer holds).", pfxPath);
        // An ABSENT sidecar is a cold cache, not a missing secret: the pin is re-derived below. This
        // is also exactly the window a concurrent starter observes between the two atomic renames.

        var cert = X509CertificateLoader.LoadPkcs12(
            File.ReadAllBytes(pfxPath), null, X509KeyStorageFlags.Exportable);
        if (!cert.HasPrivateKey)
            throw new InvalidOperationException(
                $"federation identity '{pfxPath}' has no private key — it must sign its own handshake.");

        // An EXPIRED anchor is refused, not re-minted (converged with @gavriella-glpnet's independent
        // implementation, which re-minted). Re-minting is a rotation; a rotation invalidates every
        // peer's table; a rotation nobody asked for, arriving on a timer, is this feature's own
        // failure mode wearing a clock. Refusing turns it into one loud instruction instead.
        if (cert.NotAfter <= DateTime.Now)
            throw new InvalidOperationException(
                $"federation identity '{pfxPath}' EXPIRED at {cert.NotAfter:O}. Refusing to mint a "
                + "replacement automatically: that would change this host's published pin and refuse "
                + "every peer until all of them updated. Rotate deliberately (rotate: true), then "
                + "RE-PUBLISH the new pin to the fleet before restarting the service.");

        // 🔴 DERIVED, NEVER READ (ruling Q-glpnetshiras-48). The pin is base64(SHA-256(SPKI)) of the
        // key we just loaded, so it is a PURE FUNCTION of the key and the key is the only source of
        // truth. Reading it from a second file created a second truth that could drift from the
        // first, and it did: two files written non-atomically wedged a concurrent cold start in 2 of
        // 20 measured runs, refusing to boot on trust material that was never actually wrong.
        // Deriving removes the class of defect instead of narrowing its window.
        var computed = QuicTransport.SpkiPin(cert);

        var stored = File.Exists(fingerprintPath) ? File.ReadAllText(fingerprintPath).Trim() : "";
        if (string.Equals(computed, stored, StringComparison.Ordinal))
            return new FederationIdentity(cert, computed, pfxPath, Created: false);

        // The cache is cold or stale. Refresh it from the key and REPORT — never refuse. A stale
        // cache cannot make a correct key wrong; what it CAN do is mislead an operator reading the
        // file, so the drift is surfaced rather than silently repaired.
        var state = stored.Length == 0 ? PinCacheState.Rederived : PinCacheState.Refreshed;
        var diagnosis = state == PinCacheState.Refreshed
            ? DiagnoseMismatch(pfxPath, fingerprintPath)
            : null;

        WriteSidecar(fingerprintPath, computed, replace: true);

        return new FederationIdentity(cert, computed, pfxPath, Created: false)
        {
            PinCache = state,
            StaleCachedPin = state == PinCacheState.Refreshed ? stored : null,
            PinCacheDiagnosis = diagnosis,
        };
    }

    /// <summary>
    /// The two files are each replaced atomically but not as a pair, so a rotation killed between
    /// the renames leaves a new key beside an old pin. That state is REFUSED either way; this turns
    /// the refusal into an instruction, because "inconsistent" alone sends an operator hunting for a
    /// corruption that is really just an interrupted command.
    /// </summary>
    private static string DiagnoseMismatch(string pfxPath, string fingerprintPath)
    {
        try
        {
            return File.GetLastWriteTimeUtc(pfxPath) > File.GetLastWriteTimeUtc(fingerprintPath)
                ? "The key is NEWER than the pin file, which is what an INTERRUPTED ROTATION looks "
                  + "like: re-run with rotate: true to complete it, then re-publish the pin."
                : "The pin file is NEWER than the key, so a pin was published for a key this host "
                  + "does not hold — restore the matching keystore rather than rotating.";
        }
        catch (IOException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Mint and CLAIM an identity. The claim is a same-directory rename of a freshly created temp
    /// file, which is atomic and exclusive on both Windows and Unix — so two processes starting at
    /// once cannot each believe they minted the host's identity. Exactly one wins the rename; every
    /// loser discards its keypair and LOADS the winner's, and all of them return the same pin. A
    /// last-writer-wins WriteAllBytes would instead hand two callers two different pins and persist
    /// only one of them, which is the original defect wearing a race condition.
    /// </summary>
    private static FederationIdentity Create(
        string dir, string pfxPath, string fpPath, string commonName, bool rotate)
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

        // 🔴 File.Move(overwrite: false) IS NOT AN ATOMIC EXCLUSIVE CLAIM. On this runtime it is a
        // check-then-rename, so two concurrent callers can BOTH pass the existence check and both
        // rename — the second silently clobbering the first. Measured: 16 concurrent first-starts
        // returned TWO DISTINCT identities, i.e. two callers each believed they had minted the
        // host's identity and the file held only one of them. Every peer pinned from the loser's
        // return value would have been pinning a key this host does not hold.
        //
        // FileMode.CreateNew IS atomic and exclusive (O_EXCL on POSIX, CREATE_NEW on Windows), so
        // the claim is taken on a separate marker file and only the claim's winner writes the key.
        var claimPath = pfxPath + ".claim";
        if (!rotate && !TryClaim(claimPath))
            return AdoptTheWinner(pfxPath, fpPath, claimPath); // lost the race — adopt, never mint

        var tempPfx = pfxPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            WritePrivate(tempPfx, pfxBytes);
            // Safe to overwrite: the claim above guarantees this caller is the only writer. The
            // write-then-rename is kept for DURABILITY (no truncated key after a power cut), which
            // is a different property from exclusivity and needs its own mechanism.
            File.Move(tempPfx, pfxPath, overwrite: true);
        }
        catch
        {
            TryDelete(tempPfx);
            if (!rotate) TryDelete(claimPath); // release the claim so the next start can retry
            throw;
        }

        var cert = X509CertificateLoader.LoadPkcs12(pfxBytes, null, X509KeyStorageFlags.Exportable);
        var pin = QuicTransport.SpkiPin(cert);
        WriteSidecar(fpPath, pin, replace: true);
        // Released only now, so a loser that is still waiting sees the claim disappear ONLY after
        // the key is on disk and readable.
        TryDelete(claimPath);
        return new FederationIdentity(cert, pin, pfxPath, Created: true);
    }

    /// <summary>
    /// Take an exclusive claim, or return false. <see cref="FileMode.CreateNew"/> is the only
    /// portable primitive here that is genuinely atomic — it maps to <c>O_CREAT|O_EXCL</c> on POSIX
    /// and <c>CREATE_NEW</c> on Windows, both of which the kernel serialises. Everything built out
    /// of "does it exist? then act" has a window between the two halves.
    /// </summary>
    private static bool TryClaim(string claimPath)
    {
        try
        {
            using var _ = new FileStream(claimPath, new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
            });
            return true;
        }
        catch (IOException) { return false; }   // someone else holds it
        catch (UnauthorizedAccessException) { return false; }
    }

    /// <summary>
    /// A caller that lost the claim waits for the winner to publish, then adopts it. It must NEVER
    /// mint: minting here is precisely how one host ends up with two identities.
    /// </summary>
    private static FederationIdentity AdoptTheWinner(string pfxPath, string fpPath, string claimPath)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!File.Exists(pfxPath) && DateTime.UtcNow < deadline)
            Thread.Sleep(15);

        if (File.Exists(pfxPath))
            return LoadAfterEnsuringSidecar(pfxPath, fpPath);

        // The claim is held but no key ever appeared: the holder died between claiming and writing.
        // Refuse with an instruction rather than minting a second identity behind its back.
        throw new InvalidOperationException(
            $"federation identity '{pfxPath}' is CLAIMED but was never published: '{claimPath}' is "
            + "held and no key appeared within 30s. The process that claimed it died between taking "
            + "the claim and writing the key. Refusing to mint a replacement, because that is how "
            + $"one host acquires two identities. Delete '{claimPath}' and start again.");
    }

    /// <summary>
    /// Load a pfx that another process just claimed, writing the sidecar if that process has not
    /// got there yet. The pin is DERIVED FROM THE PFX, never invented: an absent sidecar means the
    /// publication step has not completed, not that the pin is unknown.
    /// </summary>
    private static FederationIdentity LoadAfterEnsuringSidecar(string pfxPath, string fpPath)
    {
        if (!File.Exists(fpPath))
        {
            using var claimed = X509CertificateLoader.LoadPkcs12(
                File.ReadAllBytes(pfxPath), null, X509KeyStorageFlags.Exportable);
            WriteSidecar(fpPath, QuicTransport.SpkiPin(claimed), replace: false);
        }
        return Load(pfxPath, fpPath);
    }

    /// <summary>Publish the pin by atomic rename too, so no reader ever sees a half-written pin.</summary>
    private static void WriteSidecar(string fpPath, string pin, bool replace)
    {
        var temp = fpPath + ".tmp-" + Guid.NewGuid().ToString("N");
        WritePrivate(temp, System.Text.Encoding.ASCII.GetBytes(pin + Environment.NewLine));
        try
        {
            File.Move(temp, fpPath, overwrite: replace);
        }
        catch (IOException) when (!replace && File.Exists(fpPath))
        {
            TryDelete(temp); // another process published the same pin first — nothing to do
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Create owner-only and write. The permission is applied AT CREATION, not after: a
    /// WriteAllBytes-then-chmod leaves the private key world-readable for the width of the write.
    /// <c>CreateNew</c> also refuses a pre-existing file, so a planted symlink cannot redirect the
    /// key material somewhere readable. On Unix that is <c>UnixCreateMode</c> 0600 passed to open(2);
    /// on Windows it is an explicit protected DACL granting only the current user, because the
    /// per-user LocalApplicationData ACL does NOT travel to a directory named by
    /// <see cref="KeystoreEnvVar"/>.
    /// </summary>
    private static void WritePrivate(string path, byte[] bytes)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

        using (var stream = new FileStream(path, options))
        {
            // The DACL goes on while the file is still EMPTY. Writing first and tightening after
            // would publish the private key under the inherited ACL for the width of the write —
            // brief, but a race is not made safe by being short.
            if (OperatingSystem.IsWindows())
                RestrictToCurrentUser(path);
            stream.Write(bytes, 0, bytes.Length);
            // 🔴 Flush to STABLE STORAGE before the caller renames this temp file into place and
            // reports the identity as created. Closing a stream only hands the bytes to the page
            // cache: after an abrupt power loss the rename can survive while the contents do not,
            // leaving a PFX that is present, zero-length or truncated, and unreadable — so the host
            // cannot reproduce the pin it already published. Durability first, then the name.
            stream.Flush(flushToDisk: true);
        }
    }

    /// <summary>Replace the file's DACL with a single full-control ACE for the current user and
    /// break inheritance — the Windows half of "owner-only", applied to the file we just created.</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void RestrictToCurrentUser(string path)
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var owner = identity.User
            // Fail rather than fall back to the inherited ACL: silently storing a private key under
            // whatever the parent directory permits is exactly the outcome this method exists to
            // prevent, and a caller cannot see that it happened.
            ?? throw new InvalidOperationException(
                "cannot restrict the federation keystore file: the current Windows identity has no "
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
}
