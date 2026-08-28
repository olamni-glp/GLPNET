using System.Diagnostics;
using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace Ynet.Transport.Capability;

/// <summary>
/// Pluggable node signer (FR-002). The spec fixes the node's public key AS its transport (TLS)
/// identity and nodeId = H(pubkey). The signing ALGORITHM is a seam (DEC-CRYPTO-1): the node
/// identity is <b>Ed25519-primary</b> (absorbing iroh/`noq`) with an <b>ECDsa/P-256 (BCL-native)
/// fallback in exceptional cases</b>; nodeId/self-cert machinery is algorithm-agnostic.
/// </summary>
public interface INodeSigner
{
    ReadOnlySpan<byte> PublicKeySpki { get; }
    byte[] Sign(ReadOnlySpan<byte> data);
    bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature);
}

/// <summary>The node-identity signature algorithm actually in force (DEC-CRYPTO-1).</summary>
public enum SignatureAlgorithm { Ed25519, EcdsaP256 }

/// <summary>Key lifecycle (data-model Node identity). Monotonic cutover: never active -> migrating.</summary>
public enum KeyState { Active, Migrating, Retired }

/// <summary>
/// A YNET node identity: a keypair whose public key is the transport identity, and a self-certifying
/// node id nodeId = SHA-256(SPKI). Ed25519-primary with an ECDsa/P-256 exceptional-case fallback
/// (DEC-CRYPTO-1), both behind <see cref="INodeSigner"/>. REAL + TESTED (T005/T012).
/// </summary>
public sealed class NodeIdentity : INodeSigner, IDisposable
{
    // Exactly one of these is non-null, selected by Algorithm.
    private readonly Ed25519PrivateKeyParameters? _edPriv;
    private readonly ECDsa? _ecKey;

    public NodeId NodeId { get; }
    public KeyState State { get; private set; }

    /// <summary>The algorithm actually in force — the "loud" signal a fallback occurred (DEC-CRYPTO-1).</summary>
    public SignatureAlgorithm Algorithm { get; }

    private NodeIdentity(SignatureAlgorithm algorithm, Ed25519PrivateKeyParameters? edPriv, ECDsa? ecKey, byte[] spki, KeyState state)
    {
        Algorithm = algorithm;
        _edPriv = edPriv;
        _ecKey = ecKey;
        State = state;
        PublicKeySpki = spki;
        NodeId = DeriveNodeId(spki);
    }

    public byte[] PublicKeySpki { get; }
    ReadOnlySpan<byte> INodeSigner.PublicKeySpki => PublicKeySpki;

    /// <summary>
    /// Generate a node identity using the primary algorithm (Ed25519). If the Ed25519 provider is
    /// unavailable/unusable on this host, degrade LOUDLY to ECDsa/P-256 (DEC-CRYPTO-1 exceptional
    /// case) — never silently. Inspect <see cref="Algorithm"/> to confirm which is in force.
    /// </summary>
    public static NodeIdentity Generate() => Generate(SignatureAlgorithm.Ed25519);

    /// <summary>Generate with an explicit algorithm (Ed25519 falls back to P-256 on provider failure).</summary>
    public static NodeIdentity Generate(SignatureAlgorithm algorithm)
    {
        if (algorithm == SignatureAlgorithm.Ed25519)
        {
            try
            {
                return NewEd25519();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(
                    $"YNET DEC-CRYPTO-1: Ed25519 provider unavailable ({ex.GetType().Name}: {ex.Message}); " +
                    "falling back to ECDsa/P-256 node identity.");
                return NewEcdsaP256();
            }
        }
        return NewEcdsaP256();
    }

    private static NodeIdentity NewEd25519()
    {
        var gen = new Ed25519KeyPairGenerator();
        gen.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var kp = gen.GenerateKeyPair();
        var priv = (Ed25519PrivateKeyParameters)kp.Private;
        var pub = (Ed25519PublicKeyParameters)kp.Public;
        var spki = SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(pub).GetDerEncoded();
        return new NodeIdentity(SignatureAlgorithm.Ed25519, priv, ecKey: null, spki, KeyState.Active);
    }

    private static NodeIdentity NewEcdsaP256()
    {
        var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new NodeIdentity(SignatureAlgorithm.EcdsaP256, edPriv: null, ec, ec.ExportSubjectPublicKeyInfo(), KeyState.Active);
    }

    /// <summary>nodeId = H(pubkey) — self-certification basis (FR-006/FR-017). Algorithm-agnostic.</summary>
    public static NodeId DeriveNodeId(ReadOnlySpan<byte> publicKeySpki)
    {
        Span<byte> digest = stackalloc byte[32];
        SHA256.HashData(publicKeySpki, digest);
        return new NodeId(Convert.ToHexStringLower(digest));
    }

    public byte[] Sign(ReadOnlySpan<byte> data)
    {
        if (Algorithm == SignatureAlgorithm.Ed25519)
        {
            var msg = data.ToArray();
            var signer = new Ed25519Signer();
            signer.Init(forSigning: true, _edPriv);
            signer.BlockUpdate(msg, 0, msg.Length);
            return signer.GenerateSignature();
        }
        return _ecKey!.SignData(data, HashAlgorithmName.SHA256);
    }

    /// <summary>Verify against THIS identity's own public key (delegates to the agnostic verifier).</summary>
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
        => VerifySpki(PublicKeySpki, data, signature);

    /// <summary>
    /// Algorithm-agnostic signature verification against an arbitrary SPKI public key (DEC-CRYPTO-1):
    /// dispatches on the key type parsed from the SPKI — Ed25519 (BouncyCastle) or EC/P-256 (BCL
    /// ECDsa, SHA-256). Used by self-cert record + peer-identity verification so Ed25519 and P-256
    /// identities interoperate on one path. Returns false on any parse/verify failure (fail-closed).
    /// </summary>
    public static bool VerifySpki(ReadOnlySpan<byte> publicKeySpki, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        var spki = publicKeySpki.ToArray();
        var msg = data.ToArray();
        var sig = signature.ToArray();

        Org.BouncyCastle.Crypto.AsymmetricKeyParameter? key = null;
        try { key = PublicKeyFactory.CreateKey(spki); }
        catch { key = null; }

        if (key is Ed25519PublicKeyParameters ed)
        {
            try
            {
                var verifier = new Ed25519Signer();
                verifier.Init(forSigning: false, ed);
                verifier.BlockUpdate(msg, 0, msg.Length);
                return verifier.VerifySignature(sig);
            }
            catch
            {
                return false;
            }
        }

        // EC / P-256 (fallback identities) — verify with the BCL ECDsa path.
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(spki, out _);
            return ecdsa.VerifyData(msg, sig, HashAlgorithmName.SHA256);
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

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

    public void Dispose() => _ecKey?.Dispose();
}
