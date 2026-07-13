using System.Security.Cryptography;

namespace Ynet.Transport.Capability;

/// <summary>
/// Pluggable node signer (FR-002). The spec fixes the node's public key AS its transport (TLS)
/// identity and nodeId = H(pubkey). The signing ALGORITHM is a seam: this reference impl uses
/// ECDsa/P-256 (BCL-native, no third-party dep). Production uses Ed25519 (absorbing iroh/`noq`) —
/// swap the implementation behind this interface; nodeId/self-cert machinery is algorithm-agnostic.
/// </summary>
public interface INodeSigner
{
    ReadOnlySpan<byte> PublicKeySpki { get; }
    byte[] Sign(ReadOnlySpan<byte> data);
    bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature);
}

/// <summary>Key lifecycle (data-model Node identity). Monotonic cutover: never active -> migrating.</summary>
public enum KeyState { Active, Migrating, Retired }

/// <summary>
/// A YNET node identity: an ECDsa/P-256 keypair whose public key is the transport identity, and a
/// self-certifying node id nodeId = SHA-256(SPKI). REAL + TESTED (T005).
/// </summary>
public sealed class NodeIdentity : INodeSigner, IDisposable
{
    private readonly ECDsa _key;

    public NodeId NodeId { get; }
    public KeyState State { get; private set; }

    private NodeIdentity(ECDsa key, KeyState state)
    {
        _key = key;
        State = state;
        PublicKeySpki = key.ExportSubjectPublicKeyInfo();
        NodeId = DeriveNodeId(PublicKeySpki);
    }

    public byte[] PublicKeySpki { get; }
    ReadOnlySpan<byte> INodeSigner.PublicKeySpki => PublicKeySpki;

    public static NodeIdentity Generate() => new(ECDsa.Create(ECCurve.NamedCurves.nistP256), KeyState.Active);

    /// <summary>nodeId = H(pubkey) — self-certification basis (FR-006/FR-017).</summary>
    public static NodeId DeriveNodeId(ReadOnlySpan<byte> publicKeySpki)
    {
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(publicKeySpki, digest);
        return new NodeId(Convert.ToHexStringLower(digest));
    }

    public byte[] Sign(ReadOnlySpan<byte> data) => _key.SignData(data, HashAlgorithmName.SHA256);

    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
        => _key.VerifyData(data, signature, HashAlgorithmName.SHA256);

    /// <summary>
    /// Cutover to per-node-only identity (research R4 / FR-020): only Migrating -> Active is legal,
    /// authorized by an operator-signed record verified by the caller. Never Active -> Migrating.
    /// </summary>
    public void CompleteMigration()
    {
        if (State != KeyState.Migrating)
            throw new InvalidOperationException("cutover is monotonic: only Migrating -> Active is permitted (FR-020).");
        State = KeyState.Active;
    }

    /// <summary>Verify a peer's presented identity == its handshake key (FR-002, pre-frame).</summary>
    public static bool PeerIdentityMatches(NodeId claimed, ReadOnlySpan<byte> handshakePublicKeySpki)
        => claimed == DeriveNodeId(handshakePublicKeySpki);

    public void Dispose() => _key.Dispose();
}
