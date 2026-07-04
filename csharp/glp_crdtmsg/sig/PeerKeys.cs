// Per-peer Ed25519 identity (feature 041-crdtmsg-mvp, T039).
//
// Contract C16/C17 (capability-signature.md) / FR-019:
//   Per-peer identity comes from an enrolled Ed25519 key bound to the peer-name at mesh join — NEVER
//   from the SPKI-pin shared cert (that is layer-0 MEMBERSHIP only, C16). This store holds the local
//   signing key and the enrolled public keys of peers, and does Ed25519 sign/verify (NSec / libsodium).

using NSec.Cryptography;

namespace GlpRuntime.CrdtMsg.Sig;

public sealed class PeerKeyStore
{
    private static readonly SignatureAlgorithm Ed = SignatureAlgorithm.Ed25519;

    private readonly Key _local;
    private readonly Dictionary<string, PublicKey> _peers = new();

    public string LocalPeer { get; }

    public PeerKeyStore(string localPeer)
    {
        LocalPeer = localPeer;
        _local = Key.Create(Ed, new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        _peers[localPeer] = _local.PublicKey; // a peer can verify its own signatures
    }

    /// <summary>This peer's public key, as raw 32 bytes, to hand to peers at mesh join.</summary>
    public byte[] LocalPublicKey => _local.PublicKey.Export(KeyBlobFormat.RawPublicKey);

    /// <summary>Enrol a peer's public key, bound to its authenticated peer-name (mesh join).</summary>
    public void Enroll(string peerName, byte[] rawPublicKey) =>
        _peers[peerName] = PublicKey.Import(Ed, rawPublicKey, KeyBlobFormat.RawPublicKey);

    /// <summary>Ed25519-sign with the local key.</summary>
    public byte[] Sign(ReadOnlySpan<byte> data) => Ed.Sign(_local, data);

    /// <summary>Verify an Ed25519 signature against an enrolled peer's key (unknown peer ⇒ false).</summary>
    public bool Verify(string peerName, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature) =>
        _peers.TryGetValue(peerName, out var pk) && Ed.Verify(pk, data, signature);

    public bool IsEnrolled(string peerName) => _peers.ContainsKey(peerName);
}
