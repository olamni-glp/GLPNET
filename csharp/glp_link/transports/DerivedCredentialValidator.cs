using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace GlpRuntime.Link.Transports;

/// <summary>How a presented (non-trunk) certificate fared against the derived-identity rules.</summary>
public enum DerivedVerdict
{
    /// <summary>Trunk-signed, inside its validity window, not revoked — accept (067 R-002).</summary>
    Accepted,

    /// <summary>Not signed by the trunk key — the existing <c>cert_mismatch</c> semantics.</summary>
    NotSigned,

    /// <summary>Outside the validity window beyond the declared skew bound — <c>cert_expired</c>.</summary>
    Expired,

    /// <summary>SPKI fingerprint is in the revocation set (or the set is unreadable — fail-closed)
    /// — <c>cert_revoked</c>.</summary>
    Revoked,
}

/// <summary>One validation outcome: the verdict, its <c>ERR</c> token, and a human detail.</summary>
public readonly record struct DerivedValidation(DerivedVerdict Verdict, string Token, string Detail)
{
    public bool IsAccepted => Verdict == DerivedVerdict.Accepted;
}

/// <summary>
/// Derived-credential acceptance rules for the 067 join seam
/// (<c>specs/067-qr-link-provisioning/contracts/join-seam-contract.md</c>): a presented peer
/// certificate that is not the trunk itself is accepted iff (a) its signature verifies against the
/// trunk certificate's public key — a raw signature check, NOT name-based X509 chain building —
/// (b) <c>now</c> is inside its validity window (±90 s skew bound), and (c) its SPKI fingerprint is
/// absent from the revocation set (<c>glpquick-cert/provision/revoked.jsonl</c>, append-only).
/// </summary>
/// <remarks>
/// Revocation set: loaded at construction; reloaded when the file's mtime/length changes, checked
/// on every <see cref="Validate"/> (i.e. per accept) — enforcement latency ≤ 60 s with margin
/// (FR-009). A missing file is an empty set (nothing ever revoked) — NOT an error; an unreadable
/// or corrupt file fail-closes the derived path (<c>cert_revoked</c> refusals naming
/// <c>revocation_list_unreadable</c>) while the trunk-identity path stays untouched.
/// </remarks>
public sealed class DerivedCredentialValidator
{
    /// <summary>Declared clock-skew tolerance for the validity-window check (join-seam contract §2b).</summary>
    public static readonly TimeSpan DefaultClockSkew = TimeSpan.FromSeconds(90);

    private readonly X509Certificate2 _trunkCert;
    private readonly string _revokedPath;
    private readonly TimeSpan _skew;
    private readonly TimeProvider _clock;

    private readonly object _revocationLock = new();
    private HashSet<string> _revoked = new(StringComparer.Ordinal);
    private bool _unreadable;
    private DateTime _lastMtimeUtc;
    private long _lastLength = -1;
    private bool _fileSeen;

    public DerivedCredentialValidator(X509Certificate2 trunkCert, string revokedJsonlPath,
        TimeSpan? clockSkew = null, TimeProvider? clock = null)
    {
        _trunkCert = trunkCert ?? throw new ArgumentNullException(nameof(trunkCert));
        ArgumentException.ThrowIfNullOrWhiteSpace(revokedJsonlPath);
        _revokedPath = revokedJsonlPath;
        _skew = clockSkew ?? DefaultClockSkew;
        _clock = clock ?? TimeProvider.System;
        RefreshRevocations();
    }

    /// <summary>Evaluate a presented non-trunk certificate against rules (a)-(c). Never throws.</summary>
    public DerivedValidation Validate(X509Certificate2 presented)
    {
        ArgumentNullException.ThrowIfNull(presented);

        if (!IsSignedByTrunk(presented))
            return new DerivedValidation(DerivedVerdict.NotSigned, "cert_mismatch",
                "peer certificate is neither the pinned trunk nor trunk-signed");

        var now = _clock.GetUtcNow().UtcDateTime;
        var notBefore = presented.NotBefore.ToUniversalTime();
        var notAfter = presented.NotAfter.ToUniversalTime();
        if (now < notBefore - _skew)
            return new DerivedValidation(DerivedVerdict.Expired, "cert_expired",
                $"not yet valid: NotBefore={notBefore:O} now={now:O} (skew bound ±{_skew.TotalSeconds:0}s)");
        if (now > notAfter + _skew)
            return new DerivedValidation(DerivedVerdict.Expired, "cert_expired",
                $"expired: NotAfter={notAfter:O} now={now:O} (skew bound ±{_skew.TotalSeconds:0}s) — re-provision the device");

        RefreshRevocations();
        lock (_revocationLock)
        {
            if (_unreadable)
                return new DerivedValidation(DerivedVerdict.Revoked, "cert_revoked",
                    $"revocation_list_unreadable: '{_revokedPath}' is unreadable/corrupt — derived-path acceptance is fail-closed");
            if (_revoked.Contains(QuicTransport.SpkiPin(presented)))
                return new DerivedValidation(DerivedVerdict.Revoked, "cert_revoked",
                    "credential fingerprint is revoked");
        }
        return new DerivedValidation(DerivedVerdict.Accepted, "", "trunk-signed derived credential accepted");
    }

    /// <summary>
    /// Reload the revocation set if the file's mtime/length changed (or its existence flipped).
    /// Called per <see cref="Validate"/> and by the listener's ≥-every-10-s re-check (T019).
    /// </summary>
    public void RefreshRevocations()
    {
        lock (_revocationLock)
        {
            var info = new FileInfo(_revokedPath);
            if (!info.Exists)
            {
                // Missing file = nothing ever issued/revoked — an empty, READABLE set (contract).
                _fileSeen = false;
                _unreadable = false;
                if (_revoked.Count > 0) _revoked = new HashSet<string>(StringComparer.Ordinal);
                _lastLength = -1;
                return;
            }
            if (_fileSeen && info.LastWriteTimeUtc == _lastMtimeUtc && info.Length == _lastLength)
                return; // unchanged — keep the current set (or the fail-closed state)

            try
            {
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var line in File.ReadAllLines(_revokedPath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("fingerprint", out var fp)
                        && fp.ValueKind == JsonValueKind.String)
                        set.Add(fp.GetString()!);
                    else
                        throw new FormatException("revocation row lacks a string 'fingerprint'");
                }
                _revoked = set;
                _unreadable = false;
            }
            catch (Exception)
            {
                // Unreadable/corrupt ⇒ fail-closed for the DERIVED path only (trunk unaffected).
                _unreadable = true;
            }
            _fileSeen = true;
            _lastMtimeUtc = info.LastWriteTimeUtc;
            _lastLength = info.Length;
        }
    }

    /// <summary>
    /// Raw signature check of <paramref name="presented"/> against the trunk certificate's public
    /// key (join-seam contract §2a): parse the outer <c>Certificate ::= SEQUENCE { tbsCertificate,
    /// signatureAlgorithm, signatureValue }</c> and verify the signature over the encoded TBS bytes.
    /// No CA/hostname semantics, no chain building.
    /// </summary>
    private bool IsSignedByTrunk(X509Certificate2 presented)
    {
        byte[] tbs, signature;
        try
        {
            var reader = new AsnReader(presented.RawData, AsnEncodingRules.DER);
            var certSeq = reader.ReadSequence();
            tbs = certSeq.ReadEncodedValue().ToArray();   // the full DER-encoded TBSCertificate
            certSeq.ReadEncodedValue();                    // AlgorithmIdentifier (hash fixed: SHA-256)
            signature = certSeq.ReadBitString(out int unused);
            if (unused != 0) return false;
        }
        catch (AsnContentException)
        {
            return false; // not a parseable DER certificate — cannot be trunk-signed
        }

        try
        {
            using var ecdsa = _trunkCert.GetECDsaPublicKey();
            if (ecdsa is not null)
                return ecdsa.VerifyData(tbs, signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence);
            using var rsa = _trunkCert.GetRSAPublicKey();
            if (rsa is not null)
                return rsa.VerifyData(tbs, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException)
        {
            return false; // malformed signature material — a refusal, never a crash
        }
        return false; // trunk key type is neither EC nor RSA — nothing we can verify against
    }
}
