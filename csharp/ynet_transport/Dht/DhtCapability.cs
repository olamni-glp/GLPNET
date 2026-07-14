using Ynet.Transport.Capability;

namespace Ynet.Transport.Dht;

/// <summary>
/// The discovery slice of the transport capability (T025, US3, FR-006/FR-017): wires
/// <see cref="IYnetTransport.DhtStore"/> / <see cref="IYnetTransport.DhtLookup"/> onto the embedded
/// <see cref="SKademliaNode"/> — replacing the honest seam that stood in until T019/T024 landed.
///
/// Both operations reject a record that does not self-certify (bad signature, key-spoof, or expiry)
/// REGARDLESS of the serving DHT hop (FR-006 / SC-003): storage never propagates an unverifiable
/// record, and a lookup verifies at the requesting node so a malicious hop cannot inject one. A
/// lookup for a human-memorable name is routed to <see cref="RefusalReason.FurtherResolverRequired"/>
/// via <see cref="NameResolution"/> — the transport fabricates no naming answer (FR-017).
/// </summary>
public sealed class DhtCapability
{
    private readonly SKademliaNode _node;
    private readonly Func<DateTimeOffset> _clock;

    /// <param name="node">This node's S-Kademlia participant in the curated overlay.</param>
    /// <param name="clock">Time source for TTL/expiry checks; defaults to wall clock.</param>
    public DhtCapability(SKademliaNode node, Func<DateTimeOffset>? clock = null)
    {
        _node = node ?? throw new ArgumentNullException(nameof(node));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>The underlying overlay node (for bootstrap/join by the composition root).</summary>
    public SKademliaNode Node => _node;

    /// <summary>
    /// Store a self-certified record at the k closest overlay nodes to its key (FR-006). Refuses a
    /// record that fails self-certification (tamper / key-spoof / expiry) with
    /// <see cref="RefusalReason.RecordRejected"/> — no partial write, no propagation.
    /// </summary>
    public Result<Unit> Store(SignedRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        return _node.Store(record, _clock())
            ? Result<Unit>.Success(Unit.Value)
            : Result<Unit>.Refuse(RefusalReason.RecordRejected);
    }

    /// <summary>
    /// Iterative self-certified lookup (FR-006/FR-017). A human-memorable name → <see
    /// cref="RefusalReason.FurtherResolverRequired"/> (fabricate nothing); a valid self-certified key
    /// with no live record → <see cref="RefusalReason.RecordNotFound"/>; otherwise the verified
    /// record. The returned record is verified at THIS node, so it is trustworthy independent of the
    /// serving hop (SC-003).
    /// </summary>
    public Result<SignedRecord> Lookup(ReadOnlyMemory<byte> key)
    {
        var k = key.ToArray();

        if (NameResolution.Classify(k) == KeyClass.HumanMemorableName)
            return Result<SignedRecord>.Refuse(RefusalReason.FurtherResolverRequired);

        var found = _node.Lookup(k, _clock());
        return found is null
            ? Result<SignedRecord>.Refuse(RefusalReason.RecordNotFound)
            : Result<SignedRecord>.Success(found);
    }
}
