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
    /// Create a reachability record whose DHT key is bound to the signer's node id (see
    /// <see cref="KeyIsBoundToSigner"/>).
    /// </summary>
    public static SignedRecord CreateReachability(
        NodeIdentity signer, byte[] payload, DateTimeOffset now, TimeSpan ttl)
    {
        return Create(signer, ReachabilityKey(signer.NodeId), RecordKind.Reachability, payload, now, ttl);
    }

    /// <summary>
    /// Create a key-to-record entry under the signer's OWN namespace (Q-olg15-02: bind every kind).
    /// The DHT key is <c>&lt;signerNodeId&gt;/&lt;name&gt;</c>, so no signer can publish under another
    /// signer's namespace. <paramref name="name"/> must be non-empty; it is otherwise unconstrained.
    /// </summary>
    public static SignedRecord CreateKeyToRecord(
        NodeIdentity signer, string name, byte[] payload, DateTimeOffset now, TimeSpan ttl)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("a key-to-record name must be non-empty", nameof(name));
        return Create(signer, KeyToRecordKey(signer.NodeId, name), RecordKind.KeyToRecord, payload, now, ttl);
    }

    /// <summary>The DHT key a reachability record signed by <paramref name="signer"/> must carry.</summary>
    public static byte[] ReachabilityKey(NodeId signer)
        => System.Text.Encoding.ASCII.GetBytes(signer.Value);

    /// <summary>The DHT key a key-to-record entry signed by <paramref name="signer"/> must carry.</summary>
    public static byte[] KeyToRecordKey(NodeId signer, string name)
        => System.Text.Encoding.UTF8.GetBytes(signer.Value + NamespaceSeparator + name);

    /// <summary>
    /// Separates the owning node id from the name in a key-to-record DHT key. A node id is 64 lowercase
    /// hex characters, so this byte can never occur inside one — the split is unambiguous.
    /// </summary>
    public const char NamespaceSeparator = '/';

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

        // EVERY kind must bind its DHT key to its signer, and an unbound kind is refused (Q-olg15-02).
        if (!KeyIsBoundToSigner()) return false;

        // Algorithm-agnostic verify (DEC-CRYPTO-1): the signer may be Ed25519 (primary) or P-256
        // (fallback); dispatch is by the SPKI key type, not hardcoded to ECDsa.
        return NodeIdentity.VerifySpki(
            SignerPublicKeySpki, CanonicalBytes(Key, Kind, Payload, ExpiresAt), Signature);
    }

    /// <summary>
    /// Is this record's DHT key one that its signer is entitled to write under?
    ///
    /// A valid signature proves only WHO wrote the record, never WHERE they may write it. Without a
    /// key binding, any node can mint a validly-signed record under a victim's key, and every store
    /// and lookup path accepts it (Q-olg15-02, engineer ruling 2026-09-05: "bind every kind, refuse
    /// unbound").
    ///
    /// | kind            | key the signer may write under        |
    /// |-----------------|---------------------------------------|
    /// | Reachability    | <c>&lt;nodeId&gt;</c> exactly         |
    /// | KeyToRecord     | <c>&lt;nodeId&gt;/&lt;name&gt;</c>, name non-empty |
    /// | (any other)     | none — refused                        |
    ///
    /// 🔴 The default arm is deliberately a REFUSAL, not a pass. A new <see cref="RecordKind"/> added
    /// without a binding rule here fails closed rather than inheriting the hole this method closed.
    /// </summary>
    public bool KeyIsBoundToSigner()
    {
        var owner = SignerNodeId.Value;
        return Kind switch
        {
            RecordKind.Reachability => Key.AsSpan().SequenceEqual(ReachabilityKey(SignerNodeId)),
            RecordKind.KeyToRecord  => KeyIsInNamespaceOf(owner),
            _ => false, // unbound kind — refuse (see remarks)
        };
    }

    /// <summary>Key is exactly <c>owner + separator + non-empty name</c>, compared over raw bytes.</summary>
    private bool KeyIsInNamespaceOf(string owner)
    {
        var prefix = System.Text.Encoding.UTF8.GetBytes(owner + NamespaceSeparator);
        // strictly longer than the prefix: an empty name is not a namespace member.
        if (Key.Length <= prefix.Length) return false;
        return Key.AsSpan(0, prefix.Length).SequenceEqual(prefix);
    }
}
