// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Attribution admission (feature 102, codex round-2 finding `reject-unverifiable-operation-attribution`).
//
// Contract federation-wire.md W3 / FR-009, FR-013.
//
// WHAT WAS WRONG. An admitted sender chose `Origin`, `OpId.PeerName` and `Term.HostId`
// INDEPENDENTLY — including blank, and including three mutually contradictory values — and the op
// was folded and persisted with no check at all. FR-009 requires an operation to arrive "with its
// originating participant CORRECTLY attributed", and FR-013 requires a term to carry "the
// ORIGINATING participant's identity". Accepting whatever the sender typed satisfies neither: it
// permits forged claim attribution, and — because Term.HostId is the leadership tie-break — forged
// leadership identity, which is monotone and therefore unfixable after the fact.
//
// TWO INDEPENDENT GATES, and they answer different questions.
//
//   CONSISTENCY (always applied, needs nothing configured). The three identity fields must agree
//   and be non-blank. This is cheap, total, and catches the malformed and the careless.
//
//   ORIGIN SIGNATURE (applied when the origin's public key is known). The op is signed by the
//   private half of the ORIGIN's federation identity, over the canonical bytes excluding the
//   signature itself. This is what stops one ADMITTED peer forging an op in ANOTHER admitted peer's
//   name — which the consistency gate alone cannot do, and which mutual TLS alone cannot do either,
//   because a CRDT mesh legitimately relays third-party operations.
//
// WHY THE PIN IS NOT ENOUGH TO VERIFY. The pin is base64(SHA-256(SPKI)) — a HASH. You cannot verify
// a signature against a hash of a key. So a peer's SPKI is carried in configuration alongside the
// pin, and is self-checking: SHA-256 of the configured SPKI MUST equal the configured pin, or the
// entry is refused. A wrong key therefore cannot be installed quietly.
//
// UNKNOWN-KEY POSTURE IS DECLARED, NOT ASSUMED. An op whose origin has no configured public key is
// reported as `UnverifiedOrigin` — a THIRD outcome, never silently merged into "valid" and never
// silently merged into "forged". The caller decides; the surface reports which.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace GlpRuntime.CrdtMsg.Federation;

/// <summary>The disposition of an operation's attribution. FOUR outcomes, all distinguishable.</summary>
public enum AttributionVerdict
{
    /// <summary>Fields agree AND an origin signature verified against the configured key.</summary>
    Verified,

    /// <summary>Fields agree, but the origin's public key is not configured — unproven, not refused.</summary>
    UnverifiedOrigin,

    /// <summary>The identity fields disagree, or one is blank. A fault (FR-009).</summary>
    Inconsistent,

    /// <summary>An origin key IS configured and the signature does not verify against it. A forgery.</summary>
    SignatureInvalid,
}

/// <summary>The verdict plus the reason an operator needs. "Rejected" is not a reason.</summary>
public readonly record struct AttributionResult(AttributionVerdict Verdict, string Reason)
{
    /// <summary>True when the operation may be folded under the configured strictness.</summary>
    public bool Acceptable(bool requireSignature) =>
        Verdict == AttributionVerdict.Verified
        || (!requireSignature && Verdict == AttributionVerdict.UnverifiedOrigin);
}

/// <summary>Signs and checks the attribution of a federated operation.</summary>
public static class OpAttribution
{
    /// <summary>
    /// The bytes an origin signature covers: the canonical form with the signature field ABSENT.
    /// Signing over the form that contains the signature is not possible, and signing over a
    /// DIFFERENT serialisation than the one that crosses the wire is how signature checks come to
    /// pass on bytes nobody transmitted.
    /// </summary>
    public static byte[] SignableBytes(FederationOp op) =>
        Encoding.UTF8.GetBytes(op.ToSignableJson());

    /// <summary>Base64 RSA PKCS#1 v1.5 / SHA-256 signature over <see cref="SignableBytes"/>.</summary>
    public static string Sign(FederationOp op, X509Certificate2 identity)
    {
        using var rsa = identity.GetRSAPrivateKey();
        if (rsa is not null)
            return Convert.ToBase64String(rsa.SignData(SignableBytes(op),
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

        using var ec = identity.GetECDsaPrivateKey()
            ?? throw new InvalidOperationException("federation identity exposes no usable private key");
        return Convert.ToBase64String(ec.SignData(SignableBytes(op), HashAlgorithmName.SHA256));
    }

    /// <summary>Verify a signature against a base64 SubjectPublicKeyInfo.</summary>
    public static bool VerifySignature(FederationOp op, string spkiBase64, string signatureBase64)
    {
        byte[] spki, sig;
        try
        {
            spki = Convert.FromBase64String(spkiBase64);
            sig = Convert.FromBase64String(signatureBase64);
        }
        catch (FormatException) { return false; }

        byte[] data = SignableBytes(op);

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(spki, out _);
            return rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (CryptographicException) { /* not an RSA key — try the other family */ }

        try
        {
            using var ec = ECDsa.Create();
            ec.ImportSubjectPublicKeyInfo(spki, out _);
            return ec.VerifyData(data, sig, HashAlgorithmName.SHA256);
        }
        catch (CryptographicException) { return false; }
    }

    /// <summary>
    /// Check one operation's attribution.
    /// </summary>
    /// <param name="op">The operation as received.</param>
    /// <param name="spkiForOrigin">
    /// The origin's base64 SPKI, or null when this host has not been given it. Null yields
    /// <see cref="AttributionVerdict.UnverifiedOrigin"/> — declared, never assumed either way.
    /// </param>
    public static AttributionResult Check(FederationOp op, string? spkiForOrigin)
    {
        if (string.IsNullOrWhiteSpace(op.Origin))
            return new(AttributionVerdict.Inconsistent,
                "origin is blank — an operation with no originating participant cannot be attributed (FR-009)");

        if (string.IsNullOrWhiteSpace(op.OpId.PeerName))
            return new(AttributionVerdict.Inconsistent,
                "op_id.peer is blank — the dot's peer half IS the originating participant");

        if (!string.Equals(op.OpId.PeerName, op.Origin, StringComparison.Ordinal))
            return new(AttributionVerdict.Inconsistent,
                $"op_id.peer '{op.OpId.PeerName}' disagrees with origin '{op.Origin}' — two different claims about who made this operation");

        if (op.Term is { } term)
        {
            if (string.IsNullOrWhiteSpace(term.HostId))
                return new(AttributionVerdict.Inconsistent,
                    "term carries no host id — FR-013 requires the originating participant's identity in the term");

            if (!string.Equals(term.HostId, op.Origin, StringComparison.Ordinal))
                return new(AttributionVerdict.Inconsistent,
                    $"term.host '{term.HostId}' disagrees with origin '{op.Origin}' — a forged leadership identity is monotone and cannot be undone");
        }

        if (spkiForOrigin is null)
            return new(AttributionVerdict.UnverifiedOrigin,
                $"no public key configured for origin '{op.Origin}' — attribution is self-declared, not proven");

        if (string.IsNullOrWhiteSpace(op.Signature))
            return new(AttributionVerdict.SignatureInvalid,
                $"origin '{op.Origin}' has a configured key but the operation carries no signature");

        return VerifySignature(op, spkiForOrigin, op.Signature!)
            ? new(AttributionVerdict.Verified, "origin signature verified against the configured key")
            : new(AttributionVerdict.SignatureInvalid,
                $"signature does not verify against the configured key for origin '{op.Origin}' — forged attribution");
    }
}

/// <summary>An operation was refused on attribution grounds. Named, so it is never a generic error.</summary>
public sealed class AttributionRefusedException : InvalidOperationException
{
    public AttributionRefusedException(AttributionResult result)
        : base($"attribution refused ({result.Verdict}): {result.Reason}") => Result = result;

    public AttributionResult Result { get; }
}
