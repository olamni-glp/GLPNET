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
    /// Verify self-certification: the signature validates against the embedded public key. Rejects
    /// tampered records regardless of the serving DHT hop (FR-006 / SC-003).
    /// </summary>
    public bool VerifySelfCertified()
    {
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
