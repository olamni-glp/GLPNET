using System.Collections.Concurrent;
using Ynet.Transport.Capability;
using Ynet.Transport.Relay.Dsdv;

namespace Ynet.Transport.Relay;

/// <summary>How a directly-linked internet neighbour is actually reached (T031a, FR-021, D3). The kind
/// is what makes this an INTERNET overlay rather than olamnit's LAN mesh: a neighbour may sit behind a
/// NAT and be reachable only by hole-punch, or only through an admitted relay.</summary>
public enum InternetLinkKind
{
    /// <summary>A directly addressable peer (host / LAN / public endpoint) — the cheapest link.</summary>
    Direct,

    /// <summary>Reached by NAT hole-punch (ICE/DCUtR, US2). Works, but is more fragile and slower to
    /// establish than a direct link, so it costs more.</summary>
    HolePunched,

    /// <summary>Reached through a 056-admitted relay (US4). Always available where a punch fails, but
    /// adds a forwarding hop and the relay's load — the most expensive link, so DSDV prefers direct and
    /// hole-punched paths and falls back to relays exactly as FR-018 wants.</summary>
    Relayed,
}

/// <summary>A directly-linked internet neighbour: how it is reached, plus the measured signals the
/// layered cost model folds in. <paramref name="Quality"/> is a latency/loss penalty and
/// <paramref name="Load"/> an observed-contention signal — both fed to olamnit's
/// <see cref="LinkCostInputs"/> seam, which is where real measurements plug in with no routing-logic
/// change.</summary>
public readonly record struct InternetLink(InternetLinkKind Kind, uint Quality = 0, uint Load = 0);

/// <summary>
/// A route advertisement as it crosses the internet overlay: keyed by self-certified
/// <see cref="NodeId"/>, NOT by the DSDV core's compact <c>ushort</c>.
///
/// This is the crux of extending a LAN mesh to the internet. In olamnit's mesh a <c>ushort</c> IS the
/// node's identity, shared by every node. Here a node's index is <b>local to its own table</b> — node A's
/// index for X need not equal node B's — so an index on the wire would be meaningless (worse: silently
/// wrong). The wire therefore carries the canonical NodeId and each node translates on ingest.
/// </summary>
public readonly record struct InternetRouteAdvertisement(
    NodeId Origin, NodeId Dest, uint Cost, ulong Seq, NodeId Via);

/// <summary>
/// A bijective <see cref="NodeId"/> ↔ <c>ushort</c> registry — the id bridge between YNET's
/// self-certified 64-hex node ids and the DSDV core's compact node key (T031a).
///
/// <b>Deliberately a REGISTRY, never a hash.</b> Folding a 256-bit NodeId into 16 bits collides two
/// distinct nodes at ~256 nodes by the birthday bound, which would silently merge their routing-table
/// rows and blackhole one of them. Indices are assigned densely, first-come, and never reused within a
/// session, so the mapping stays stable for as long as the table refers to it.
///
/// Index <c>0</c> is reserved as "unassigned": the core's <c>TryNextHop</c> yields <c>0</c> on failure,
/// so keeping 0 out of circulation means a failed lookup can never alias a real node. Assignment is
/// bounded — see <see cref="MaxNodes"/>; exhaustion refuses distinctly (co #221) rather than wrapping.
/// Thread-safe.
/// </summary>
public sealed class NodeIndex
{
    /// <summary>Distinct nodes one table can index (1…65535; 0 is the reserved unassigned slot). This is
    /// a per-node-table bound inherited from the DSDV core's <c>ushort</c> key, not an overlay-wide cap
    /// on membership (co #221).</summary>
    public const int MaxNodes = ushort.MaxValue;

    private readonly ConcurrentDictionary<NodeId, ushort> _toIndex = new();
    private readonly ConcurrentDictionary<ushort, NodeId> _toNode = new();
    private readonly object _assign = new();
    private int _next = 1; // 0 reserved. int, not ushort: the counter must be able to hold the
                           // one-past-the-end value without wrapping back onto index 0.

    /// <summary>Distinct nodes indexed so far.</summary>
    public int Count => _toIndex.Count;

    /// <summary>Resolve <paramref name="node"/>'s index, assigning a fresh one if it is new. False only
    /// when the table is full (<see cref="MaxNodes"/>) — the caller refuses with
    /// <see cref="RefusalReason.RoutingCapacityExhausted"/>, never wraps onto another node's row.</summary>
    public bool TryGetOrAssign(NodeId node, out ushort index)
    {
        if (_toIndex.TryGetValue(node, out index)) return true;

        lock (_assign)
        {
            if (_toIndex.TryGetValue(node, out index)) return true; // raced, already assigned

            if (_toIndex.Count >= MaxNodes || _next > ushort.MaxValue) { index = 0; return false; }

            index = (ushort)_next++;
            _toIndex[node] = index;
            _toNode[index] = node;
            return true;
        }
    }

    /// <summary>
    /// Resolve several nodes at once, assigning fresh indices only if EVERY new node fits. Either all
    /// of them come back indexed, or none is newly assigned — so a caller that refuses on exhaustion
    /// leaves the registry exactly as it found it (contract invariant 2: no side effects on the refusal
    /// path). Assigning them one at a time would let the first consume the last free slot and the second
    /// fail, making capacity order-dependent and a refusal lossy.
    /// </summary>
    public bool TryGetOrAssignAll(IReadOnlyList<NodeId> nodes, out ushort[] indices)
    {
        indices = new ushort[nodes.Count];

        lock (_assign)
        {
            // Count the DISTINCT new arrivals first — the same node twice needs one slot, not two.
            var newcomers = new HashSet<NodeId>();
            foreach (var node in nodes)
                if (!_toIndex.ContainsKey(node)) newcomers.Add(node);

            if (_toIndex.Count + newcomers.Count > MaxNodes || _next + newcomers.Count - 1 > ushort.MaxValue)
            {
                indices = [];
                return false; // refuse before assigning anything
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                if (!TryGetOrAssign(nodes[i], out var idx)) { indices = []; return false; }
                indices[i] = idx;
            }
            return true;
        }
    }

    /// <summary>Look up an already-assigned index; false if this node was never indexed.</summary>
    public bool TryGetIndex(NodeId node, out ushort index) => _toIndex.TryGetValue(node, out index);

    /// <summary>Reverse the mapping; false for the reserved 0 or an unassigned index.</summary>
    public bool TryGetNode(ushort index, out NodeId node)
    {
        node = default;
        return index != 0 && _toNode.TryGetValue(index, out node);
    }
}

/// <summary>
/// Maps an internet link's <see cref="InternetLinkKind"/> onto the DSDV metric, through olamnit's own
/// <see cref="ILinkCostModel"/> seam (T031a, FR-021).
///
/// This is the whole point of the extension: olamnit built <see cref="ILinkCostModel"/> so "on hardware
/// the same seam is fed by real measurements with no routing-logic change". A NAT-piercing internet
/// overlay is exactly that case, so the extension plugs in HERE rather than forking the router — the
/// Bellman-Ford relaxation, sequence-number rules, split-horizon and metric ceiling all stay olamnit's
/// (FR-021 extend-not-duplicate).
///
/// The kind sets the <see cref="LinkCostInputs.Base"/> floor; measured latency/loss and contention ride
/// the existing <see cref="LinkCostInputs.Quality"/> / <see cref="LinkCostInputs.Load"/> signals. The
/// layering arithmetic itself is delegated to the injected model — this class deliberately does not
/// re-implement it.
/// </summary>
public sealed class InternetLinkCostModel : ILinkCostModel
{
    /// <summary>A directly addressable peer: the unit hop, identical to olamnit's LAN baseline.</summary>
    public const uint DirectBase = 1;

    /// <summary>A NAT-pierced link: real but more fragile/latent than a direct one.</summary>
    public const uint HolePunchedBase = 2;

    /// <summary>A relayed link: an extra forwarding hop plus the relay's load, so DSDV prefers direct
    /// and punched paths and uses relays as the fallback (FR-018).</summary>
    public const uint RelayedBase = 3;

    private readonly Func<ushort, InternetLink?> _linkFor;
    private readonly ILinkCostModel _layered;

    /// <param name="linkFor">Resolves a neighbour index to its link, or null when the neighbour is not a
    /// known internet link (then the injected model's own default applies).</param>
    /// <param name="layered">The layered additive metric — supply
    /// <c>Olamnit.Kernel.Mesh.LayeredLinkCostModel</c> once the shared DSDV package lands (DEC-DSDV-1).
    /// The extension does not re-implement it (FR-021/FR-022).</param>
    public InternetLinkCostModel(Func<ushort, InternetLink?> linkFor, ILinkCostModel layered)
    {
        _linkFor = linkFor ?? throw new ArgumentNullException(nameof(linkFor));
        _layered = layered ?? throw new ArgumentNullException(nameof(layered));
    }

    /// <summary>The base metric floor for a link kind. An unknown kind costs the most rather than the
    /// least: a metric must never become spuriously cheap through a gap in this map.</summary>
    public static uint BaseFor(InternetLinkKind kind) => kind switch
    {
        InternetLinkKind.Direct => DirectBase,
        InternetLinkKind.HolePunched => HolePunchedBase,
        InternetLinkKind.Relayed => RelayedBase,
        _ => RelayedBase, // fail expensive, never cheap
    };

    public uint CostFor(ushort neighbor, in LinkCostInputs inputs)
    {
        if (_linkFor(neighbor) is not { } link)
            return _layered.CostFor(neighbor, inputs); // not an internet link — the model's own default

        return _layered.CostFor(
            neighbor,
            new LinkCostInputs(Base: BaseFor(link.Kind), Quality: link.Quality, Load: link.Load));
    }
}

/// <summary>
/// **T031a / FR-021 / D3 — the routing substrate the BUILD-NEW overlay sits above.** Extends olamnit's
/// DSDV <c>DistanceVectorRouter</c> from LAN-only into the NAT-piercing internet overlay.
///
/// <b>It extends; it does not duplicate.</b> The DSDV algebra — Bellman-Ford relaxation, destination
/// sequence numbers, split-horizon + poison-reverse, the finite metric ceiling, aging — stays olamnit's,
/// consumed through <see cref="IDistanceVectorRouter"/> (FR-021/FR-022). ynet_transport ships no
/// implementation of that interface on purpose. What this class adds is exactly the three things a LAN
/// mesh does not have:
/// <list type="number">
/// <item><b>Identity</b> — a bijective <see cref="NodeIndex"/> between YNET's self-certified 64-hex
/// NodeId and the core's compact node key, and NodeId-keyed adverts on the wire (a node's index is local
/// to its own table, so an index on the wire would be meaningless).</item>
/// <item><b>Reachability</b> — <see cref="InternetLinkKind"/>: direct, NAT-hole-punched (US2), or via a
/// 056-admitted relay (US4), costed through olamnit's <see cref="ILinkCostModel"/> seam so relays are the
/// fallback rather than the default (FR-018).</item>
/// <item><b>Translation</b> — index ↔ NodeId around every core call, so the overlay above speaks only
/// NodeIds.</item>
/// </list>
///
/// <b>No network I/O, no seam to fake.</b> Like olamnit's router this is pure managed and caller-driven —
/// the caller drives advert exchange and drives the clock. The links it costs are established by the
/// EXISTING hole-punch (US2) and relay (US4) machinery; nothing here opens a socket, so all of it is
/// verifiable in-process (Constitution II — the honest seam is the DSDV core binding, not the wire).
///
/// <b>Inherited ceilings (co #221) — documented, not discovered in production:</b>
/// <list type="bullet">
/// <item>≤ 65535 distinct nodes per table (the core's <c>ushort</c> key) — exhaustion refuses with
/// <see cref="RefusalReason.RoutingCapacityExhausted"/>.</item>
/// <item>A path's total metric must stay under <see cref="RouteAdvertisement.MaxCost"/> (16) — the
/// RIP-style ceiling that bounds reconvergence. With <see cref="InternetLinkCostModel.RelayedBase"/> = 3
/// that is ~5 relayed hops; direct/punched paths reach further. Lifting either ceiling is an olamnit-side
/// change to the core, not something this extension may paper over.</item>
/// </list>
///
/// <b>Partial against FR-021 (honest):</b> FR-021 names the DSDV router AND the <i>durable</i>
/// <c>MeshRelayRoute</c>. This delivers the routing half. Olamnit's durability (exactly-once across a
/// relay kill) is bound to its <c>RouterEngine</c> journal, which lands with the same shared-package
/// decision (DEC-DSDV-1) — see the note on <see cref="TryRoute"/>.
/// </summary>
public sealed class DsdvInternetRoute
{
    private readonly NodeId _self;
    private readonly IDistanceVectorRouter _core;
    private readonly NodeIndex _index;
    private readonly ConcurrentDictionary<ushort, InternetLink> _links = new();

    /// <param name="self">This node's self-certified id.</param>
    /// <param name="core">The DSDV core — olamnit's <c>DistanceVectorRouter</c> once the shared package
    /// lands (DEC-DSDV-1). Construct it with an <see cref="InternetLinkCostModel"/> over
    /// <see cref="LinkFor"/> so link kinds actually reach the metric.</param>
    /// <param name="index">The id bridge; a fresh registry by default. Pass the same instance used to
    /// build the core's cost model.</param>
    public DsdvInternetRoute(NodeId self, IDistanceVectorRouter core, NodeIndex? index = null)
    {
        _self = self;
        _core = core ?? throw new ArgumentNullException(nameof(core));
        _index = index ?? new NodeIndex();

        // Self must be indexed: the core's table always carries a row for itself.
        if (!_index.TryGetOrAssign(self, out var selfIndex))
            throw new InvalidOperationException("node index exhausted before self could be assigned.");

        // The core keys its own row by its Self index. If that disagrees with the index this registry
        // assigned to `self`, every translation across this boundary is silently off-by-one-node —
        // routes would resolve to the wrong peer with no error anywhere. Fail loudly at construction
        // (found by dogfooding the extension against the real olamnit router).
        if (core.Self != selfIndex)
            throw new ArgumentException(
                $"the DSDV core's self index ({core.Self}) does not match the node index assigned to {self} " +
                $"({selfIndex}) — build the core with the index this registry assigns to self.",
                nameof(core));
    }

    /// <summary>This node's self-certified id.</summary>
    public NodeId Self => _self;

    /// <summary>The id bridge (shared with the core's cost model).</summary>
    public NodeIndex Index => _index;

    /// <summary>Resolve a neighbour index to its internet link — the lookup an
    /// <see cref="InternetLinkCostModel"/> is built over.</summary>
    public InternetLink? LinkFor(ushort neighborIndex)
        => _links.TryGetValue(neighborIndex, out var link) ? link : null;

    /// <summary>The internet link to a neighbour, or null if it is not a known direct link.</summary>
    public InternetLink? LinkTo(NodeId neighbor)
        => _index.TryGetIndex(neighbor, out var idx) ? LinkFor(idx) : null;

    /// <summary>
    /// Register (or re-cost) a directly-linked internet neighbour. The link kind reaches the metric
    /// through the cost model, so a relayed neighbour is costed above a punched one and a punched one
    /// above a direct one. Refuses with <see cref="RefusalReason.RoutingCapacityExhausted"/> when this
    /// table cannot index another node (co #221).
    /// </summary>
    public Result<Unit> AddNeighbor(NodeId neighbor, InternetLink link)
    {
        if (neighbor == _self)
            throw new ArgumentException("a node cannot be its own neighbour.", nameof(neighbor));

        if (!_index.TryGetOrAssign(neighbor, out var idx))
            return Result<Unit>.Refuse(RefusalReason.RoutingCapacityExhausted);

        _links[idx] = link;              // visible to the cost model BEFORE the core resolves the cost
        _core.SetLinkState(idx, up: true);
        return Result<Unit>.Success(Unit.Value);
    }

    /// <summary>
    /// Mark a neighbour's link up or down. Down poisons every route through it — the core's own
    /// withdrawal semantics — so the overlay re-paths instead of blackholing (FR-018). Returns whether
    /// the routing table changed (a triggered update: the caller re-emits <see cref="AdvertsFor"/>).
    ///
    /// A down link KEEPS its <see cref="InternetLink"/>: the kind is what it is whether the link is
    /// currently usable or not, and dropping it would let a later <c>SetLinkState(…, up: true)</c>
    /// re-admit the neighbour with no kind — the cost model would then fall through to the unit base and
    /// silently re-cost a relay as the CHEAPEST link, inverting FR-018. Down state lives in the core's
    /// table (it stops handing out the route); the kind lives here. Use <see cref="ForgetNeighbor"/> to
    /// drop a link for good.
    /// </summary>
    public Result<bool> SetLinkState(NodeId neighbor, bool up)
    {
        if (neighbor == _self)
            throw new ArgumentException("a node cannot be its own neighbour.", nameof(neighbor));

        if (!_index.TryGetIndex(neighbor, out var idx))
            return Result<bool>.Refuse(RefusalReason.Unreachable); // never was a link

        if (!_links.ContainsKey(idx))
            return Result<bool>.Refuse(RefusalReason.Unreachable); // indexed, but never one of our links

        return Result<bool>.Success(_core.SetLinkState(idx, up));
    }

    /// <summary>
    /// Drop a neighbour's link for good: withdraw it from the core AND forget its kind. Distinct from
    /// <see cref="SetLinkState"/> with <c>up: false</c>, which is a transient outage the link can
    /// recover from with its kind intact.
    /// </summary>
    public Result<bool> ForgetNeighbor(NodeId neighbor)
    {
        if (neighbor == _self)
            throw new ArgumentException("a node cannot be its own neighbour.", nameof(neighbor));

        if (!_index.TryGetIndex(neighbor, out var idx) || !_links.TryRemove(idx, out _))
            return Result<bool>.Refuse(RefusalReason.Unreachable);

        return Result<bool>.Success(_core.SetLinkState(idx, up: false));
    }

    /// <summary>
    /// The lowest-cost next hop toward <paramref name="dest"/>, as a NodeId. Refuses with
    /// <see cref="RefusalReason.Unreachable"/> when there is no route (FR-018) — never a silent drop,
    /// never a fabricated hop.
    /// </summary>
    public Result<NodeId> TryNextHop(NodeId dest)
    {
        if (dest == _self) return Result<NodeId>.Success(_self);

        if (!_index.TryGetIndex(dest, out var destIdx))
            return Result<NodeId>.Refuse(RefusalReason.Unreachable); // never heard of this node

        if (!_core.TryNextHop(destIdx, out var hopIdx))
            return Result<NodeId>.Refuse(RefusalReason.Unreachable); // no live route

        return Result<NodeId>.Success(NodeFor(hopIdx));
    }

    /// <summary>
    /// The routing decision the overlay above consumes: the next hop toward <paramref name="dest"/> AND
    /// how to reach it, so the caller knows whether to dial direct, punch, or ride an admitted relay
    /// (US4). This is the seam olamnit's <c>MeshRelayRoute</c> binds to — its durable exactly-once
    /// forwarding rides <c>RouterEngine</c> and lands with the shared package (DEC-DSDV-1); the routing
    /// decision itself is real here and now.
    /// </summary>
    public Result<(NodeId NextHop, InternetLink Link)> TryRoute(NodeId dest)
    {
        var hop = TryNextHop(dest);
        if (!hop.Ok) return Result<(NodeId, InternetLink)>.Refuse(hop.Reason);

        if (hop.Value == _self)
            return Result<(NodeId, InternetLink)>.Success((_self, new InternetLink(InternetLinkKind.Direct)));

        return LinkTo(hop.Value) is { } link
            ? Result<(NodeId, InternetLink)>.Success((hop.Value, link))
            // A next hop that is not one of our direct links means the table and the link set have
            // diverged — refuse rather than guess a reachability we cannot back up.
            : Result<(NodeId, InternetLink)>.Refuse(RefusalReason.Unreachable);
    }

    /// <summary>
    /// Apply an inbound advertisement from the overlay. NodeId-keyed on the wire, translated into this
    /// node's local index space here. Returns whether the table changed (a triggered update). Refuses an
    /// advert arriving over a link we do not have (the core rejects it anyway — this is the honest,
    /// distinct reason) or when the table cannot index the destination (co #221).
    /// </summary>
    public Result<bool> Ingest(InternetRouteAdvertisement advert)
    {
        // The neighbour test is the LINK SET, not the index. Being indexed only means we have heard of a
        // node — a destination learned from someone else's advert, a neighbour whose link is down, even
        // self, are all indexed but are NOT links we can receive over. Testing the index would let those
        // adverts through and make this method's refusal contract a lie (the core would reject them
        // anyway, but the caller would see Ok/no-change instead of a distinct refusal).
        if (!_index.TryGetIndex(advert.Via, out var viaIdx) || !_links.ContainsKey(viaIdx))
            return Result<bool>.Refuse(RefusalReason.Unreachable); // advert over a non-neighbour link

        // Index Dest and Origin together: assigning them one at a time lets the first take the last free
        // slot and the second refuse, leaving a refused ingest having mutated the registry (invariant 2)
        // and making capacity order-dependent.
        if (!_index.TryGetOrAssignAll([advert.Dest, advert.Origin], out var indices))
            return Result<bool>.Refuse(RefusalReason.RoutingCapacityExhausted);

        return Result<bool>.Success(
            _core.Ingest(new RouteAdvertisement(indices[1], indices[0], advert.Cost, advert.Seq, viaIdx)));
    }

    /// <summary>
    /// The advertisements to emit to <paramref name="neighbor"/>, NodeId-keyed for the wire. Split-horizon
    /// and poison-reverse are the core's — this only translates. Caller-driven, exactly like olamnit's
    /// router (no thread-per-route).
    /// </summary>
    public Result<IReadOnlyList<InternetRouteAdvertisement>> AdvertsFor(NodeId neighbor)
    {
        if (!_index.TryGetIndex(neighbor, out var idx))
            return Result<IReadOnlyList<InternetRouteAdvertisement>>.Refuse(RefusalReason.Unreachable);

        var core = _core.AdvertsFor(idx);
        var wire = new List<InternetRouteAdvertisement>(core.Count);
        foreach (var a in core)
            wire.Add(new InternetRouteAdvertisement(NodeFor(a.Origin), NodeFor(a.Dest), a.Cost, a.Seq, NodeFor(a.Via)));

        return Result<IReadOnlyList<InternetRouteAdvertisement>>.Success(wire);
    }

    /// <summary>Age out routes not refreshed within the core's expiry window. Caller drives the clock.</summary>
    public bool Tick(long nowTick) => _core.Tick(nowTick);

    /// <summary>This node's routing-table row for <paramref name="dest"/>, for introspection (FR-023).</summary>
    public Result<RouteEntry> TryGetEntry(NodeId dest)
        => _index.TryGetIndex(dest, out var idx) && _core.TryGetEntry(idx, out var entry)
            ? Result<RouteEntry>.Success(entry)
            : Result<RouteEntry>.Refuse(RefusalReason.Unreachable);

    /// <summary>Reachable destinations, as NodeIds (introspection, FR-023).</summary>
    public IReadOnlyList<NodeId> ReachableDestinations()
    {
        var live = new List<NodeId>();
        foreach (var (idx, entry) in _core.Snapshot.Entries)
            if (entry.IsReachable && idx != 0 && _index.TryGetNode(idx, out var node) && node != _self)
                live.Add(node);
        return live;
    }

    // Every index the core holds came from this registry, so a miss is a broken invariant, not a runtime
    // condition. Fail loudly rather than drop a route on the floor (no silent drops, FR-018).
    private NodeId NodeFor(ushort index)
        => _index.TryGetNode(index, out var node)
            ? node
            : throw new InvalidOperationException(
                $"routing table holds index {index} with no NodeId — the node index and the DSDV core have diverged.");
}
