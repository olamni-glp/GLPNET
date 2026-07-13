using System.Security.Cryptography;
using Ynet.Transport.Capability;

namespace Ynet.Transport.Dht;

public enum RecordKind { Reachability, KeyToRecord }

/// <summary>
/// A self-certified DHT record (FR-006): payload signed by the record's own node key, so a lookup
/// result is verifiable independently of the DHT hop that served it. A record whose signature does
/// not match its claimed signer is rejected regardless of who returned it. REAL + TESTED (T024).
///
/// Embedded S-Kademlia is a curated overlay — records are never published to a public DHT.
/// </summary>
public sealed record SignedRecord(
    byte[] Key,
    RecordKind Kind,
    byte[] Payload,
    byte[] SignerPublicKeySpki,
    byte[] Signature,
    DateTimeOffset StoredAt,
    DateTimeOffset ExpiresAt)
{
    /// <summary>The signer's self-certified node id (must equal the DHT key for reachability records).</summary>
    public NodeId SignerNodeId => NodeIdentity.DeriveNodeId(SignerPublicKeySpki);

    private static byte[] CanonicalBytes(byte[] key, RecordKind kind, byte[] payload, DateTimeOffset expiresAt)
    {
        using var ms = new MemoryStream();
        ms.Write(key);
        ms.WriteByte((byte)kind);
        ms.Write(payload);
        Span<byte> exp = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(exp, expiresAt.ToUnixTimeSeconds());
        ms.Write(exp);
        return ms.ToArray();
    }

    /// <summary>Create + sign a record with the owner's identity.</summary>
    public static SignedRecord Create(
        NodeIdentity signer, byte[] key, RecordKind kind, byte[] payload, DateTimeOffset now, TimeSpan ttl)
    {
        var expires = now + ttl;
        var sig = signer.Sign(CanonicalBytes(key, kind, payload, expires));
        return new SignedRecord(key, kind, payload, signer.PublicKeySpki, sig, now, expires);
    }

    /// <summary>
    /// Create a reachability record whose DHT key is bound to the signer's node id (the only form
    /// that self-certifies against key-spoofing — see VerifySelfCertified).
    /// </summary>
    public static SignedRecord CreateReachability(
        NodeIdentity signer, byte[] payload, DateTimeOffset now, TimeSpan ttl)
    {
        var key = System.Text.Encoding.ASCII.GetBytes(signer.NodeId.Value);
        return Create(signer, key, RecordKind.Reachability, payload, now, ttl);
    }

    /// <summary>
    /// Verify self-certification: the signature validates against the embedded public key AND, for a
    /// reachability record, the DHT key equals the signer's H(pubkey) node id — so a malicious node
    /// cannot publish a validly-signed reachability record under ANOTHER node's key (codexreview
    /// finding, FR-006 / SC-003 / data-model). Rejects tampered records regardless of serving hop.
    /// </summary>
    public bool VerifySelfCertified() => VerifySelfCertified(now: null);

    /// <summary>
    /// As <see cref="VerifySelfCertified()"/>, but also rejects an expired record when a clock is
    /// supplied (codexreview finding — a replayed expired reachability record must not verify).
    /// ExpiresAt is inside the signed canonical bytes, so it is tamper-evident.
    /// </summary>
    public bool VerifySelfCertified(DateTimeOffset? now)
    {
        if (now is { } clock && clock >= ExpiresAt) return false; // expired — no replay

        // A reachability record's DHT key IS the signer's node id; anything else is a spoof attempt.
        if (Kind == RecordKind.Reachability &&
            !Key.AsSpan().SequenceEqual(System.Text.Encoding.ASCII.GetBytes(SignerNodeId.Value)))
        {
            return false;
        }
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(SignerPublicKeySpki, out _);
            return ecdsa.VerifyData(
                CanonicalBytes(Key, Kind, Payload, ExpiresAt), Signature, HashAlgorithmName.SHA256);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
