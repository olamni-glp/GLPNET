using System.Collections.Concurrent;
using Ynet.Transport.Dht;
using Ynet.Transport.HolePunch;

namespace Ynet.Transport.Capability;

/// <summary>
/// Where a node currently is. Deliberately NOT part of <see cref="NodeId"/>: an address is a
/// mutable, host-local, rebindable fact, and an id is not (FR-102-7). Rebinding this leaves the id
/// alone; that is the whole content of "address-independent".
/// </summary>
public readonly record struct NodeAddress(string Scheme, string Host, int Port)
{
    /// <summary>The YNET QUIC scheme — the only one this tier dials today.</summary>
    public const string QuicScheme = "ynet-quic";

    public static NodeAddress Quic(string host, int port) => new(QuicScheme, host, port);

    public override string ToString() => $"{Scheme}://{Host}:{Port}";

    /// <summary>Parse <c>scheme://host:port</c>. False on anything malformed — never a partial parse.</summary>
    public static bool TryParse(string? text, out NodeAddress address)
    {
        address = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var schemeEnd = text.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd <= 0) return false;
        var scheme = text[..schemeEnd];

        var rest = text[(schemeEnd + 3)..];
        var portSep = rest.LastIndexOf(':');
        if (portSep <= 0 || portSep == rest.Length - 1) return false;

        if (!int.TryParse(rest[(portSep + 1)..], out var port) || port is < 1 or > 65535) return false;

        address = new NodeAddress(scheme, rest[..portSep], port);
        return true;
    }
}

/// <summary>
/// <c>Resolve(NodeId) -> NodeAddress | Refused(reason)</c> — the surface `R-E4` refuses every
/// candidacy for want of (feature 102, FR-102-5/6).
/// </summary>
/// <remarks>
/// 🔴 <b>This is NOT <see cref="INodeEndpointResolver"/>.</b> That seam answers "open a channel to
/// this peer" and therefore conflates resolution with dialing: a caller could only learn *where* a
/// peer is by producing a wire side-effect, so it could not cache, publish, or refuse. This one
/// answers "where is this peer" and performs <b>no I/O of any kind</b>.
///
/// <para><b>Refusal is a valid answer</b> and refusals are DISTINCT — never an exception, never
/// null, and never a fabricated address (FR-017's rule, applied to addresses):</para>
/// <list type="bullet">
/// <item><see cref="RefusalReason.FurtherResolverRequired"/> — the id is not a self-certified key,
/// or this resolver does not serve that namespace at all.</item>
/// <item><see cref="RefusalReason.RecordNotFound"/> — a well-formed id with no binding here.</item>
/// <item><see cref="RefusalReason.Unreachable"/> — a binding exists but has expired or been
/// withdrawn. Distinct from "never knew it": the caller may retry one and not the other.</item>
/// </list>
/// </remarks>
public interface INodeAddressResolver
{
    /// <summary>Map an id to an address, or refuse distinctly. Side-effect free.</summary>
    Result<NodeAddress> Resolve(NodeId id);
}

/// <summary>
/// An operator-supplied id→address table — the pin table the fleet already exchanges by hand, given
/// an API (FR-102-5). Address-independent by construction: <see cref="Bind"/> replaces an address
/// under an unchanged id, which is what a host that moved, re-DHCP'd, or came back on a new port is.
/// Thread-safe; every mutation is explicit.
/// </summary>
public sealed class StaticNodeAddressResolver : INodeAddressResolver
{
    private readonly ConcurrentDictionary<NodeId, Binding> _bindings = new();
    private readonly Func<DateTimeOffset> _clock;

    private readonly record struct Binding(NodeAddress Address, DateTimeOffset? ExpiresAt);

    public StaticNodeAddressResolver(Func<DateTimeOffset>? clock = null)
        => _clock = clock ?? (() => DateTimeOffset.UtcNow);

    /// <summary>Bind (or REBIND) an id to an address. The id is untouched — that is the point.</summary>
    /// <param name="expiresAt">optional lease; after it, the id resolves
    /// <see cref="RefusalReason.Unreachable"/> rather than <see cref="RefusalReason.RecordNotFound"/>,
    /// because "it was here and the lease lapsed" is not "never heard of it".</param>
    public void Bind(NodeId id, NodeAddress address, DateTimeOffset? expiresAt = null)
        => _bindings[id] = new Binding(address, expiresAt);

    /// <summary>Withdraw a binding entirely. Subsequent resolves are <c>RecordNotFound</c>.</summary>
    public bool Withdraw(NodeId id) => _bindings.TryRemove(id, out _);

    public int Count => _bindings.Count;

    public Result<NodeAddress> Resolve(NodeId id)
    {
        // A non-key id is not this resolver's namespace to guess at (FR-017).
        if (!IsSelfCertifiedId(id))
            return Result<NodeAddress>.Refuse(RefusalReason.FurtherResolverRequired);

        if (!_bindings.TryGetValue(id, out var binding))
            return Result<NodeAddress>.Refuse(RefusalReason.RecordNotFound);

        if (binding.ExpiresAt is { } expiry && _clock() >= expiry)
            return Result<NodeAddress>.Refuse(RefusalReason.Unreachable);

        return Result<NodeAddress>.Success(binding.Address);
    }

    /// <summary>An id is resolvable here only if it is syntactically a self-certified node id.</summary>
    internal static bool IsSelfCertifiedId(NodeId id)
        => NameResolution.IsSelfCertifiedKey(System.Text.Encoding.ASCII.GetBytes(id.Value ?? string.Empty));
}

/// <summary>
/// Resolves an id from the self-certified reachability record the peer itself published to the
/// embedded S-Kademlia overlay (FR-102-12). The record is verified before its address is believed —
/// a lookup result is trusted for its signature, never for the hop that served it.
/// </summary>
public sealed class DhtNodeAddressResolver : INodeAddressResolver
{
    private readonly DhtCapability _dht;
    private readonly Func<DateTimeOffset> _clock;

    public DhtNodeAddressResolver(DhtCapability dht, Func<DateTimeOffset>? clock = null)
    {
        ArgumentNullException.ThrowIfNull(dht);
        _dht = dht;
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    public Result<NodeAddress> Resolve(NodeId id)
    {
        if (!StaticNodeAddressResolver.IsSelfCertifiedId(id))
            return Result<NodeAddress>.Refuse(RefusalReason.FurtherResolverRequired);

        var found = _dht.Lookup(System.Text.Encoding.ASCII.GetBytes(id.Value));
        if (!found.Ok)
            return Result<NodeAddress>.Refuse(found.Reason);

        var record = found.Value!;

        // Believe the signature, not the hop — and reject a replayed expired record.
        if (record.Kind != RecordKind.Reachability || !record.VerifySelfCertified(_clock()))
            return Result<NodeAddress>.Refuse(RefusalReason.RecordRejected);

        // The signer must be the node asked for; a validly-signed record filed under someone else's
        // id is a spoof, and VerifySelfCertified already refuses it — this is the belt to that brace.
        if (record.SignerNodeId != id)
            return Result<NodeAddress>.Refuse(RefusalReason.RecordRejected);

        ReachabilityAdvert advert;
        try { advert = ReachabilityAdvert.Decode(id, record.Payload); }
        catch { return Result<NodeAddress>.Refuse(RefusalReason.RecordRejected); }

        // Highest-priority routable candidate wins (RFC 8445 ordering, already computed by Candidate).
        var best = advert.Candidates
            .Where(c => c.Port is > 0 and <= 65535 && !string.IsNullOrWhiteSpace(c.Address))
            .OrderByDescending(c => c.Priority())
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(best.Address)
            ? Result<NodeAddress>.Refuse(RefusalReason.Unreachable) // a record with no usable candidate
            : Result<NodeAddress>.Success(NodeAddress.Quic(best.Address, best.Port));
    }
}

/// <summary>
/// Tries resolvers in order and returns the FIRST success, or the MOST SPECIFIC refusal (FR-102-8).
/// </summary>
/// <remarks>
/// 🔴 The refusal-merge rule is the load-bearing part. Specificity, most specific first:
/// <c>Unreachable</c> (someone knew this node and its path lapsed) &gt; <c>RecordRejected</c>
/// (someone answered and the answer failed verification — a security-relevant event that must not be
/// masked by a later "not found") &gt; <c>RecordNotFound</c> (well-formed id, nobody has it) &gt;
/// <c>FurtherResolverRequired</c> (nobody even serves this namespace).
///
/// <para>Collapsing these — the obvious "return the last reason" — would tell a caller that a peer
/// whose lease just lapsed is indistinguishable from a name nothing can resolve, and it would hide a
/// rejected (tampered) record behind an empty resolver. The caller retries one and escalates
/// another.</para>
/// </remarks>
public sealed class ChainedNodeAddressResolver : INodeAddressResolver
{
    private readonly IReadOnlyList<INodeAddressResolver> _links;

    public ChainedNodeAddressResolver(params INodeAddressResolver[] links)
    {
        ArgumentNullException.ThrowIfNull(links);
        if (links.Length == 0)
            throw new ArgumentException("a resolver chain needs at least one link", nameof(links));
        if (links.Any(l => l is null))
            throw new ArgumentException("a resolver chain holds no null links", nameof(links));
        _links = links;
    }

    /// <summary>Most specific first. Index = specificity; lower wins the merge.</summary>
    private static int Specificity(RefusalReason reason) => reason switch
    {
        RefusalReason.Unreachable => 0,
        RefusalReason.RecordRejected => 1,
        RefusalReason.RecordNotFound => 2,
        RefusalReason.FurtherResolverRequired => 3,
        _ => 4,
    };

    public Result<NodeAddress> Resolve(NodeId id)
    {
        var best = RefusalReason.FurtherResolverRequired;
        var bestRank = int.MaxValue;

        foreach (var link in _links)
        {
            var result = link.Resolve(id);
            if (result.Ok) return result;

            var rank = Specificity(result.Reason);
            if (rank < bestRank)
            {
                bestRank = rank;
                best = result.Reason;
            }
        }

        return Result<NodeAddress>.Refuse(best);
    }
}
