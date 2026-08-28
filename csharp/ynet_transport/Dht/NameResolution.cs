using Ynet.Transport.Capability;

namespace Ynet.Transport.Dht;

/// <summary>How a lookup key relates to what the embedded self-certified overlay can resolve.</summary>
public enum KeyClass
{
    /// <summary>A self-certified DHT key (node id = H(pubkey)); the S-Kademlia overlay serves it.</summary>
    SelfCertifiedRecord,

    /// <summary>A human-memorable name (petname / hostname / label); NOT self-certifying — needs a
    /// further (external) resolver. The transport fabricates nothing for it (FR-017).</summary>
    HumanMemorableName,
}

/// <summary>
/// Name resolution boundary for the transport tier (T026, FR-017). The embedded S-Kademlia overlay
/// resolves ONLY self-certified <c>key→record</c> entries — a key that IS a node id (nodeId =
/// H(pubkey), the 64-lowercase-hex form the overlay stores reachability records under). A
/// human-memorable, decentralized name (a petname / hostname / readable label) is unsolved in the
/// whole external corpus (spec cycle-2 §6; mstack R9 tie): the transport returns
/// <see cref="RefusalReason.FurtherResolverRequired"/> and MUST NOT fabricate a resolution.
///
/// This classifier is deliberately conservative: only a syntactically valid self-certified key is
/// treated as DHT-resolvable, so any non-key naming request is routed to the further resolver rather
/// than silently mis-resolved.
/// </summary>
public static class NameResolution
{
    /// <summary>SHA-256 node id rendered as lowercase hex (see <see cref="NodeIdentity.DeriveNodeId"/>).</summary>
    private const int NodeIdHexLength = DhtId.Bytes * 2; // 32 bytes -> 64 hex chars

    /// <summary>Classify a lookup key: a self-certified DHT key vs a human-memorable name (FR-017).</summary>
    public static KeyClass Classify(ReadOnlySpan<byte> key)
        => IsSelfCertifiedKey(key) ? KeyClass.SelfCertifiedRecord : KeyClass.HumanMemorableName;

    /// <summary>
    /// True iff <paramref name="key"/> is a self-certified DHT key — the ASCII of a node id
    /// (exactly 64 lowercase-hex chars). Everything else is a human-memorable name (FR-017), which
    /// the self-certified overlay cannot resolve without fabricating.
    /// </summary>
    public static bool IsSelfCertifiedKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != NodeIdHexLength) return false;
        foreach (var b in key)
        {
            bool isHexDigit = (b >= (byte)'0' && b <= (byte)'9') || (b >= (byte)'a' && b <= (byte)'f');
            if (!isHexDigit) return false;
        }
        return true;
    }
}
