using Ynet.Transport.Capability;
using Ynet.Transport.Relay;
using Ynet.Transport.Relay.Dsdv;

namespace Ynet.Transport.Tests.Unit;

// ---- T031a / FR-021 / D3: the DSDV internet-routing EXTENSION ----
//
// Scope discipline (FR-021 "extend, not duplicate"): these tests exercise the EXTENSION — the NodeId
// bridge, the link-kind→metric mapping through olamnit's ILinkCostModel seam, NodeId-keyed adverts, and
// the distinct refusals. They deliberately do NOT re-test the DSDV algebra (Bellman-Ford relaxation,
// sequence-number accept rules, split-horizon/poison-reverse, the metric ceiling): that is olamnit's
// DistanceVectorRouter and is tested in Olamnit.Kernel.Tests. ynet_transport ships no implementation of
// IDistanceVectorRouter on purpose — implementing one here (or in this file) would be the duplication
// FR-021 forbids.
//
// The core is therefore stood in by DsdvCoreDouble: a faithful double of the CONTRACT (it records calls
// and answers next-hops from a table the test programs), never an implementation of the algebra.
public class DsdvInternetRouteTests
{
    private static NodeId N(string name) => new($"node-{name}");

    /// <summary>A double of the DSDV CORE CONTRACT (Olamnit.Kernel.Mesh.IDistanceVectorRouter), so the
    /// extension above it can be exercised before the shared DSDV package lands (DEC-DSDV-1). It models
    /// the contract's observable behaviour — SetLinkState resolves the link cost through the injected
    /// cost model, exactly as the real router documents — and contains NO routing algebra.</summary>
    private sealed class DsdvCoreDouble(ushort self, ILinkCostModel? costModel = null) : IDistanceVectorRouter
    {
        private readonly Dictionary<ushort, RouteEntry> _table = new();

        public ushort Self => self;

        /// <summary>Costs resolved through the model when a link came up — what the extension's
        /// link-kind mapping is asserted on.</summary>
        public Dictionary<ushort, uint> ResolvedCosts { get; } = new();

        public List<RouteAdvertisement> Ingested { get; } = new();

        /// <summary>Program a next hop for a destination (stands in for a converged table).</summary>
        public void SetRoute(ushort dest, ushort nextHop, uint cost = 1, RouteState state = RouteState.Reachable)
            => _table[dest] = new RouteEntry(dest, nextHop, cost, Seq: 1, state);

        public bool Ingest(RouteAdvertisement advert) { Ingested.Add(advert); return true; }

        public bool SetLinkState(ushort neighbor, bool up)
        {
            if (up)
            {
                // Mirrors the real router: a link coming up resolves its cost via the injected model
                // with default inputs, so an InternetLinkCostModel gets its chance to apply link kind.
                ResolvedCosts[neighbor] = costModel?.CostFor(neighbor, new LinkCostInputs()) ?? 1;
                _table[neighbor] = new RouteEntry(neighbor, neighbor, ResolvedCosts[neighbor], 1, RouteState.Reachable);
            }
            else
            {
                ResolvedCosts.Remove(neighbor);
                _table.Remove(neighbor);
            }
            return true;
        }

        public bool TryNextHop(ushort dest, out ushort nextHop)
        {
            if (_table.TryGetValue(dest, out var e) && e.IsReachable) { nextHop = e.NextHop; return true; }
            nextHop = 0; // the reserved slot — the extension must never turn this into a real node
            return false;
        }

        public IReadOnlyList<RouteAdvertisement> AdvertsFor(ushort neighbor)
            => _table.Values.Select(e => new RouteAdvertisement(e.Dest, e.Dest, e.Cost, e.Seq, self)).ToList();

        public bool Tick(long nowTick) => false;

        public bool TryGetEntry(ushort dest, out RouteEntry entry) => _table.TryGetValue(dest, out entry);

        public RoutingTable Snapshot => new(new Dictionary<ushort, RouteEntry>(_table));
    }

    /// <summary>A minimal stand-in for the layered metric. The real layering is
    /// Olamnit.Kernel.Mesh.LayeredLinkCostModel and arrives with the shared package — the extension
    /// deliberately does not re-implement it, so tests inject the simplest additive model that lets the
    /// link-kind base floor be observed.</summary>
    private sealed class AdditiveCostModel : ILinkCostModel
    {
        public uint CostFor(ushort neighbor, in LinkCostInputs inputs) => inputs.Base + inputs.Quality;
    }

    private static (DsdvInternetRoute Route, DsdvCoreDouble Core) Build(NodeId self)
    {
        var index = new NodeIndex();
        DsdvInternetRoute? route = null;
        var costModel = new InternetLinkCostModel(idx => route?.LinkFor(idx), new AdditiveCostModel());
        var core = new DsdvCoreDouble(self: 1, costModel);
        route = new DsdvInternetRoute(self, core, index);
        return (route, core);
    }

    // ---- the id bridge (the blocker that made T031a a decision, co #220/#221) ----

    [Fact]
    public void The_node_index_is_bijective_and_never_collides_two_nodes()
    {
        // The reason this is a registry and not a hash: folding a 256-bit NodeId into 16 bits collides
        // two distinct nodes at ~256 by the birthday bound, silently merging their routing-table rows.
        var index = new NodeIndex();
        var seen = new HashSet<ushort>();

        for (int i = 0; i < 5_000; i++)
        {
            Assert.True(index.TryGetOrAssign(N($"peer-{i}"), out var idx));
            Assert.True(seen.Add(idx), $"index {idx} was handed to two distinct nodes");
            Assert.True(index.TryGetNode(idx, out var back));
            Assert.Equal(N($"peer-{i}"), back);          // round-trips
        }

        Assert.Equal(5_000, index.Count);
        Assert.True(index.TryGetOrAssign(N("peer-0"), out var again));
        Assert.True(index.TryGetIndex(N("peer-0"), out var stable));
        Assert.Equal(again, stable);                      // assignment is stable, never reshuffled
    }

    [Fact]
    public void Index_zero_is_reserved_so_a_failed_core_lookup_never_aliases_a_real_node()
    {
        var index = new NodeIndex();
        Assert.True(index.TryGetOrAssign(N("first"), out var first));
        Assert.NotEqual(0, first);                        // 0 is never handed out...
        Assert.False(index.TryGetNode(0, out _));         // ...and never resolves to a node
    }

    [Fact]
    public void Node_index_exhaustion_refuses_distinctly_and_never_wraps_onto_another_node()
    {
        var index = new NodeIndex();
        for (int i = 0; i < NodeIndex.MaxNodes; i++)
            Assert.True(index.TryGetOrAssign(N($"n{i}"), out _));

        Assert.Equal(NodeIndex.MaxNodes, index.Count);

        // One too many: refuse, and hand back the reserved 0 rather than wrapping onto a live row.
        Assert.False(index.TryGetOrAssign(N("one-too-many"), out var overflow));
        Assert.Equal(0, overflow);

        // Everything already indexed still resolves — exhaustion degrades, it does not corrupt.
        Assert.True(index.TryGetIndex(N("n0"), out var firstIdx));
        Assert.True(index.TryGetNode(firstIdx, out var firstNode));
        Assert.Equal(N("n0"), firstNode);
    }

    [Fact]
    public void Adding_a_neighbour_beyond_the_index_ceiling_refuses_with_routing_capacity_exhausted()
    {
        var index = new NodeIndex();
        for (int i = 0; i < NodeIndex.MaxNodes; i++)
            Assert.True(index.TryGetOrAssign(N($"filler{i}"), out _));

        // The registry is full, so even self cannot be indexed — the ctor refuses loudly rather than
        // constructing a router whose own row is missing.
        var core = new DsdvCoreDouble(self: 1);
        Assert.Throws<InvalidOperationException>(() => new DsdvInternetRoute(N("self"), core, index));
    }

    [Fact]
    public void A_new_neighbour_is_refused_distinctly_once_the_table_is_full()
    {
        var index = new NodeIndex();
        var core = new DsdvCoreDouble(self: 1);
        var route = new DsdvInternetRoute(N("self"), core, index); // takes one slot

        for (int i = index.Count; i < NodeIndex.MaxNodes; i++)
            Assert.True(index.TryGetOrAssign(N($"filler{i}"), out _));

        var refused = route.AddNeighbor(N("late-arrival"), new InternetLink(InternetLinkKind.Direct));

        Assert.False(refused.Ok);
        Assert.Equal(RefusalReason.RoutingCapacityExhausted, refused.Reason); // co #221, exactly one reason
    }

    // ---- link kind → metric, through olamnit's ILinkCostModel seam (the extension point) ----

    [Fact]
    public void A_relayed_link_costs_more_than_a_punched_one_which_costs_more_than_a_direct_one()
    {
        // FR-018: relays are the fallback, not the default — so the metric must order them that way.
        Assert.True(InternetLinkCostModel.DirectBase < InternetLinkCostModel.HolePunchedBase);
        Assert.True(InternetLinkCostModel.HolePunchedBase < InternetLinkCostModel.RelayedBase);

        var (route, core) = Build(N("self"));
        Assert.True(route.AddNeighbor(N("direct"), new InternetLink(InternetLinkKind.Direct)).Ok);
        Assert.True(route.AddNeighbor(N("punched"), new InternetLink(InternetLinkKind.HolePunched)).Ok);
        Assert.True(route.AddNeighbor(N("relayed"), new InternetLink(InternetLinkKind.Relayed)).Ok);

        uint Cost(string name)
        {
            Assert.True(route.Index.TryGetIndex(N(name), out var idx));
            return core.ResolvedCosts[idx];
        }

        // The kind reached the core's metric through the injected cost model — not by forking the router.
        Assert.Equal(InternetLinkCostModel.DirectBase, Cost("direct"));
        Assert.Equal(InternetLinkCostModel.HolePunchedBase, Cost("punched"));
        Assert.Equal(InternetLinkCostModel.RelayedBase, Cost("relayed"));
    }

    [Fact]
    public void Measured_link_quality_rides_the_existing_cost_inputs_on_top_of_the_kind()
    {
        var (route, core) = Build(N("self"));
        Assert.True(route.AddNeighbor(N("laggy"), new InternetLink(InternetLinkKind.Direct, Quality: 4)).Ok);

        Assert.True(route.Index.TryGetIndex(N("laggy"), out var idx));
        Assert.Equal(InternetLinkCostModel.DirectBase + 4, core.ResolvedCosts[idx]);
    }

    [Fact]
    public void An_unknown_link_kind_costs_the_most_not_the_least()
    {
        // A gap in the kind map must never make a link spuriously cheap and win routes.
        Assert.Equal(InternetLinkCostModel.RelayedBase, InternetLinkCostModel.BaseFor((InternetLinkKind)999));
    }

    [Fact]
    public void A_relayed_path_stays_inside_the_inherited_metric_ceiling_for_a_realistic_diameter()
    {
        // Documented ceiling (co #221): total path metric must stay under MaxCost (16). At RelayedBase=3
        // that is 5 relayed hops; this pins the ceiling so a future cost bump cannot silently shrink the
        // reachable diameter to nothing.
        uint fiveRelayedHops = InternetLinkCostModel.RelayedBase * 5;
        Assert.True(fiveRelayedHops < RouteAdvertisement.MaxCost, "5 relayed hops must remain routable");
        Assert.True(InternetLinkCostModel.RelayedBase * 6 >= RouteAdvertisement.MaxCost);
    }

    // ---- NodeId-keyed wire: the crux of LAN → internet ----

    [Fact]
    public void Adverts_cross_the_wire_keyed_by_node_id_so_nodes_with_different_local_indices_interoperate()
    {
        // Two nodes index the same peers in DIFFERENT orders, so their local ushort spaces disagree.
        // If the wire carried indices this would silently misroute; because it carries NodeIds it does not.
        var (a, aCore) = Build(N("A"));
        Assert.True(a.AddNeighbor(N("B"), new InternetLink(InternetLinkKind.Direct)).Ok);
        Assert.True(a.AddNeighbor(N("C"), new InternetLink(InternetLinkKind.Relayed)).Ok);

        var (b, bCore) = Build(N("B"));
        Assert.True(b.AddNeighbor(N("C"), new InternetLink(InternetLinkKind.Direct)).Ok); // C first
        Assert.True(b.AddNeighbor(N("A"), new InternetLink(InternetLinkKind.Direct)).Ok);

        // The same node genuinely has different indices at A and at B.
        Assert.True(a.Index.TryGetIndex(N("C"), out var cAtA));
        Assert.True(b.Index.TryGetIndex(N("C"), out var cAtB));
        Assert.NotEqual(cAtA, cAtB);

        // A emits its adverts for B: NodeId-keyed on the wire.
        var adverts = a.AdvertsFor(N("B"));
        Assert.True(adverts.Ok);
        Assert.All(adverts.Value!, ad => Assert.Equal(N("A"), ad.Via));
        Assert.Contains(adverts.Value!, ad => ad.Dest == N("C"));

        // B ingests them and translates into ITS OWN index space — C lands on B's index for C, not A's.
        // The ingress is the authenticated peer (A), not the wire's claim.
        foreach (var ad in adverts.Value!)
            Assert.True(b.Ingest(N("A"), ad).Ok);

        var forC = Assert.Single(bCore.Ingested, i => i.Dest == cAtB);
        Assert.Equal(cAtB, forC.Dest);
        Assert.NotEqual(cAtA, forC.Dest); // the wire did NOT smuggle A's index into B's table
    }

    [Fact]
    public void An_advert_arriving_over_a_link_we_do_not_have_is_refused_distinctly()
    {
        var (route, core) = Build(N("self"));

        var refused = route.Ingest(N("a-stranger"), new InternetRouteAdvertisement(
            Origin: N("X"), Dest: N("X"), Cost: 1, Seq: 5, Via: N("a-stranger")));

        Assert.False(refused.Ok);
        Assert.Equal(RefusalReason.Unreachable, refused.Reason);
        Assert.Empty(core.Ingested); // refused before touching the core — zero side-effects
    }

    // ---- codex review findings (be8a8cbb): all three were live bugs the first test pass missed ----

    [Fact]
    public void An_advert_via_a_merely_INDEXED_node_that_is_not_a_link_is_refused()
    {
        // [P1] Being indexed only means we have heard of a node. A destination learned from someone
        // else's advert is indexed but is NOT a link we can receive over; testing the index instead of
        // the link set let those adverts into the core.
        var (route, core) = Build(N("self"));
        Assert.True(route.AddNeighbor(N("real-link"), new InternetLink(InternetLinkKind.Direct)).Ok);
        Assert.True(route.Index.TryGetOrAssign(N("merely-known"), out _)); // indexed, never a link

        var refused = route.Ingest(N("merely-known"), new InternetRouteAdvertisement(
            Origin: N("Z"), Dest: N("Z"), Cost: 1, Seq: 9, Via: N("merely-known")));

        Assert.False(refused.Ok);
        Assert.Equal(RefusalReason.Unreachable, refused.Reason);
        Assert.Empty(core.Ingested);
    }

    [Fact]
    public void An_advert_via_a_neighbour_whose_link_is_down_is_refused_not_silently_accepted()
    {
        // [P1] A downed neighbour stays indexed — it must still not be a valid ingress.
        var (route, core) = Build(N("self"));
        Assert.True(route.AddNeighbor(N("flaky"), new InternetLink(InternetLinkKind.Direct)).Ok);
        Assert.True(route.ForgetNeighbor(N("flaky")).Ok);

        var refused = route.Ingest(N("flaky"), new InternetRouteAdvertisement(
            Origin: N("Q"), Dest: N("Q"), Cost: 1, Seq: 3, Via: N("flaky")));

        Assert.False(refused.Ok);
        Assert.Equal(RefusalReason.Unreachable, refused.Reason);
        Assert.Empty(core.Ingested);
    }

    [Fact]
    public void An_advert_via_self_is_refused()
    {
        // [P1] self is always indexed; it is never a link we receive over.
        var (route, core) = Build(N("self"));

        var refused = route.Ingest(N("self"), new InternetRouteAdvertisement(
            Origin: N("W"), Dest: N("W"), Cost: 1, Seq: 2, Via: N("self")));

        Assert.False(refused.Ok);
        Assert.Equal(RefusalReason.Unreachable, refused.Reason);
        Assert.Empty(core.Ingested);
    }

    [Fact]
    public void A_link_recovering_from_down_keeps_its_kind_and_is_never_re_costed_as_cheapest()
    {
        // [P2] The sharp one: dropping the link metadata on down let a later up re-admit a RELAY with no
        // kind, so the cost model fell through to the unit base and made the relay the CHEAPEST link —
        // FR-018 exactly inverted.
        var (route, core) = Build(N("self"));
        Assert.True(route.AddNeighbor(N("relay"), new InternetLink(InternetLinkKind.Relayed, Quality: 1)).Ok);
        Assert.True(route.Index.TryGetIndex(N("relay"), out var idx));
        var costWhenFirstUp = core.ResolvedCosts[idx];

        Assert.True(route.SetLinkState(N("relay"), up: false).Ok);   // transient outage
        Assert.True(route.SetLinkState(N("relay"), up: true).Ok);    // ...and it comes back

        Assert.Equal(costWhenFirstUp, core.ResolvedCosts[idx]);       // same cost as before the outage
        Assert.Equal(InternetLinkCostModel.RelayedBase + 1, core.ResolvedCosts[idx]);
        Assert.NotEqual(InternetLinkCostModel.DirectBase, core.ResolvedCosts[idx]); // NOT re-costed cheap

        // ...and the route is usable again, reporting the kind it always had.
        Assert.True(route.Index.TryGetOrAssign(N("dest"), out var destIdx));
        core.SetRoute(dest: destIdx, nextHop: idx);
        var routed = route.TryRoute(N("dest"));
        Assert.True(routed.Ok);
        Assert.Equal(InternetLinkKind.Relayed, routed.Value.Link.Kind);
    }

    [Fact]
    public void Forgetting_a_neighbour_drops_its_link_for_good_unlike_a_transient_down()
    {
        var (route, _) = Build(N("self"));
        Assert.True(route.AddNeighbor(N("gone"), new InternetLink(InternetLinkKind.HolePunched)).Ok);

        Assert.True(route.ForgetNeighbor(N("gone")).Ok);
        Assert.Null(route.LinkTo(N("gone")));                          // kind is gone too

        var again = route.ForgetNeighbor(N("gone"));
        Assert.False(again.Ok);
        Assert.Equal(RefusalReason.Unreachable, again.Reason);         // idempotent, distinct refusal
    }

    [Fact]
    public void A_refused_ingest_leaves_the_node_index_untouched()
    {
        // [P2] With exactly one free slot and two new distinct nodes, assigning one at a time consumed
        // the slot for Dest and then refused on Origin — a refusal with a lasting side effect, and
        // order-dependent capacity (invariant 2).
        var index = new NodeIndex();
        var core = new DsdvCoreDouble(self: 1);
        var route = new DsdvInternetRoute(N("self"), core, index);
        Assert.True(route.AddNeighbor(N("via"), new InternetLink(InternetLinkKind.Direct)).Ok);

        // Fill until exactly ONE slot remains.
        for (int i = index.Count; i < NodeIndex.MaxNodes - 1; i++)
            Assert.True(index.TryGetOrAssign(N($"filler{i}"), out _));
        Assert.Equal(NodeIndex.MaxNodes - 1, index.Count);

        var refused = route.Ingest(N("via"), new InternetRouteAdvertisement(
            Origin: N("brand-new-origin"), Dest: N("brand-new-dest"), Cost: 1, Seq: 4, Via: N("via")));

        Assert.False(refused.Ok);
        Assert.Equal(RefusalReason.RoutingCapacityExhausted, refused.Reason);

        // The refusal consumed nothing: neither newcomer was indexed, and the slot is still free.
        Assert.Equal(NodeIndex.MaxNodes - 1, index.Count);
        Assert.False(index.TryGetIndex(N("brand-new-dest"), out _));
        Assert.False(index.TryGetIndex(N("brand-new-origin"), out _));
        Assert.Empty(core.Ingested);
    }

    // ---- codex review cycle 3: route spoofing (the security finding) ----

    [Fact]
    public void A_neighbour_cannot_inject_routes_as_another_neighbour()
    {
        // [P1] Trusting advert.Via — a value the SENDER controls — let any linked peer poison or inject
        // routes as another live neighbour. The ingress must come from the authenticated session peer
        // (FR-002: the node key IS the identity, verified pre-frame).
        var (route, core) = Build(N("self"));
        Assert.True(route.AddNeighbor(N("attacker"), new InternetLink(InternetLinkKind.Direct)).Ok);
        Assert.True(route.AddNeighbor(N("victim"), new InternetLink(InternetLinkKind.Direct)).Ok);

        // The attacker is a legitimate, authenticated, up neighbour — it just lies about Via.
        var spoofed = route.Ingest(N("attacker"), new InternetRouteAdvertisement(
            Origin: N("target"), Dest: N("target"), Cost: 1, Seq: 99, Via: N("victim")));

        Assert.False(spoofed.Ok);
        Assert.Equal(RefusalReason.IdentityMismatch, spoofed.Reason);
        Assert.Empty(core.Ingested); // nothing reached the core attributed to the victim

        // The same advert from the peer it actually claims to be from is fine.
        Assert.True(route.Ingest(N("victim"), new InternetRouteAdvertisement(
            Origin: N("target"), Dest: N("target"), Cost: 1, Seq: 99, Via: N("victim"))).Ok);
    }

    [Fact]
    public void A_spoofed_advert_cannot_poison_a_route_as_another_neighbour()
    {
        // The poisoning direction of the same attack: claim to be the victim and withdraw its routes.
        var (route, core) = Build(N("self"));
        Assert.True(route.AddNeighbor(N("attacker"), new InternetLink(InternetLinkKind.Direct)).Ok);
        Assert.True(route.AddNeighbor(N("victim"), new InternetLink(InternetLinkKind.Direct)).Ok);

        var poison = route.Ingest(N("attacker"), new InternetRouteAdvertisement(
            Origin: N("target"), Dest: N("target"),
            Cost: RouteAdvertisement.Infinity, Seq: 1000, Via: N("victim")));

        Assert.False(poison.Ok);
        Assert.Equal(RefusalReason.IdentityMismatch, poison.Reason);
        Assert.Empty(core.Ingested);
    }

    // ---- codex review cycle 2 findings ----

    [Fact]
    public void An_advert_via_a_neighbour_whose_link_is_DOWN_is_refused()
    {
        // Cycle 2 exposed a gap the cycle-1 fix created: keeping the link's kind across an outage (right)
        // also kept it in the set Ingest tested (wrong), so a down link became a valid ingress. Kind and
        // up-state are now separate facts.
        var (route, core) = Build(N("self"));
        Assert.True(route.AddNeighbor(N("flaky"), new InternetLink(InternetLinkKind.Direct)).Ok);
        Assert.True(route.SetLinkState(N("flaky"), up: false).Ok);

        var refused = route.Ingest(N("flaky"), new InternetRouteAdvertisement(
            Origin: N("R"), Dest: N("R"), Cost: 1, Seq: 7, Via: N("flaky")));

        Assert.False(refused.Ok);
        Assert.Equal(RefusalReason.Unreachable, refused.Reason);
        Assert.Empty(core.Ingested);

        // ...but the kind survived the outage, so recovery still costs it correctly.
        Assert.NotNull(route.LinkTo(N("flaky")));
        Assert.False(route.IsLinkUp(N("flaky")));
        Assert.True(route.SetLinkState(N("flaky"), up: true).Ok);
        Assert.True(route.IsLinkUp(N("flaky")));
        Assert.True(route.Ingest(N("flaky"), new InternetRouteAdvertisement(N("R"), N("R"), 1, 8, N("flaky"))).Ok);
    }

    [Fact]
    public void Adverts_are_only_emitted_over_a_link_that_is_actually_up()
    {
        // The egress side of the same rule — symmetric with Ingest.
        var (route, _) = Build(N("self"));
        Assert.True(route.AddNeighbor(N("peer"), new InternetLink(InternetLinkKind.Direct)).Ok);
        Assert.True(route.AdvertsFor(N("peer")).Ok);

        Assert.True(route.Index.TryGetOrAssign(N("merely-known"), out _));
        var merelyKnown = route.AdvertsFor(N("merely-known"));
        Assert.False(merelyKnown.Ok);
        Assert.Equal(RefusalReason.Unreachable, merelyKnown.Reason); // never a link — nothing to send over

        Assert.True(route.SetLinkState(N("peer"), up: false).Ok);
        var down = route.AdvertsFor(N("peer"));
        Assert.False(down.Ok);
        Assert.Equal(RefusalReason.Unreachable, down.Reason);        // down — we could not deliver them

        Assert.True(route.ForgetNeighbor(N("peer")).Ok);
        Assert.False(route.AdvertsFor(N("peer")).Ok);
    }

    [Fact]
    public void The_cost_model_preserves_the_signals_the_core_supplies_instead_of_dropping_them()
    {
        // Rebuilding LinkCostInputs from scratch dropped the core's Period/Event penalties, so an
        // event-penalised path could be selected as if it were cheap.
        LinkCostInputs? seen = null;
        var capturing = new CapturingCostModel(i => seen = i);
        var links = new Dictionary<ushort, InternetLink> { [7] = new(InternetLinkKind.Relayed, Quality: 3, Load: 2) };
        var model = new InternetLinkCostModel(idx => links.TryGetValue(idx, out var l) ? l : null, capturing);

        model.CostFor(7, new LinkCostInputs(Base: 1, Quality: 10, Load: 20, Period: 5, Event: 9));

        Assert.NotNull(seen);
        Assert.Equal(InternetLinkCostModel.RelayedBase, seen!.Value.Base); // kind overrides the base floor
        Assert.Equal(13u, seen.Value.Quality);                             // ours + the core's, not ours alone
        Assert.Equal(22u, seen.Value.Load);
        Assert.Equal(5u, seen.Value.Period);                               // passed through, not dropped
        Assert.Equal(9u, seen.Value.Event);
    }

    [Fact]
    public void Combining_cost_signals_saturates_rather_than_wrapping_into_a_cheap_link()
    {
        LinkCostInputs? seen = null;
        var capturing = new CapturingCostModel(i => seen = i);
        var links = new Dictionary<ushort, InternetLink> { [1] = new(InternetLinkKind.Direct, Quality: uint.MaxValue) };
        var model = new InternetLinkCostModel(idx => links.TryGetValue(idx, out var l) ? l : null, capturing);

        model.CostFor(1, new LinkCostInputs(Quality: 100));

        // A wrapped sum would become a spuriously CHEAP link and win routes it should never win.
        Assert.Equal(uint.MaxValue, seen!.Value.Quality);
    }

    private sealed class CapturingCostModel(Action<LinkCostInputs> capture) : ILinkCostModel
    {
        public uint CostFor(ushort neighbor, in LinkCostInputs inputs)
        {
            capture(inputs);
            return inputs.Base + inputs.Quality;
        }
    }

    [Fact]
    public void Assigning_a_group_needs_room_for_every_newcomer_or_none_is_assigned()
    {
        var index = new NodeIndex();
        for (int i = 0; i < NodeIndex.MaxNodes - 1; i++)
            Assert.True(index.TryGetOrAssign(N($"n{i}"), out _));

        // Two newcomers, one slot: all-or-nothing.
        Assert.False(index.TryGetOrAssignAll([N("newA"), N("newB")], out _));
        Assert.Equal(NodeIndex.MaxNodes - 1, index.Count);
        Assert.False(index.TryGetIndex(N("newA"), out _));

        // The same node twice needs ONE slot, not two — so this fits.
        Assert.True(index.TryGetOrAssignAll([N("newA"), N("newA")], out var same));
        Assert.Equal(same[0], same[1]);
        Assert.Equal(NodeIndex.MaxNodes, index.Count);
    }

    // ---- routing decisions the overlay above consumes ----

    [Fact]
    public void An_unreachable_destination_refuses_rather_than_fabricating_a_hop()
    {
        var (route, _) = Build(N("self"));

        var never = route.TryNextHop(N("never-heard-of"));
        Assert.False(never.Ok);
        Assert.Equal(RefusalReason.Unreachable, never.Reason); // FR-018, no silent drop

        // Known to us, but with no live route: still a refusal, never index 0 turned into a "node".
        Assert.True(route.AddNeighbor(N("known"), new InternetLink(InternetLinkKind.Direct)).Ok);
        Assert.True(route.SetLinkState(N("known"), up: false).Ok);
        var down = route.TryNextHop(N("known"));
        Assert.False(down.Ok);
        Assert.Equal(RefusalReason.Unreachable, down.Reason);
    }

    [Fact]
    public void The_next_hop_translates_back_to_a_node_id()
    {
        var (route, core) = Build(N("self"));
        Assert.True(route.AddNeighbor(N("gateway"), new InternetLink(InternetLinkKind.HolePunched)).Ok);
        Assert.True(route.Index.TryGetIndex(N("gateway"), out var gwIdx));
        Assert.True(route.Index.TryGetOrAssign(N("far-away"), out var farIdx));
        core.SetRoute(dest: farIdx, nextHop: gwIdx);

        var hop = route.TryNextHop(N("far-away"));

        Assert.True(hop.Ok);
        Assert.Equal(N("gateway"), hop.Value); // index space never leaks above this layer
    }

    [Fact]
    public void TryRoute_reports_how_to_reach_the_next_hop_so_the_overlay_knows_to_dial_punch_or_relay()
    {
        var (route, core) = Build(N("self"));
        Assert.True(route.AddNeighbor(N("relay"), new InternetLink(InternetLinkKind.Relayed, Quality: 2)).Ok);
        Assert.True(route.Index.TryGetIndex(N("relay"), out var relayIdx));
        Assert.True(route.Index.TryGetOrAssign(N("behind-nat"), out var destIdx));
        core.SetRoute(dest: destIdx, nextHop: relayIdx);

        var routed = route.TryRoute(N("behind-nat"));

        Assert.True(routed.Ok);
        Assert.Equal(N("relay"), routed.Value.NextHop);
        Assert.Equal(InternetLinkKind.Relayed, routed.Value.Link.Kind); // ride an admitted relay (US4)
        Assert.Equal(2u, routed.Value.Link.Quality);
    }

    [Fact]
    public void TryRoute_refuses_when_the_table_and_the_link_set_have_diverged()
    {
        var (route, core) = Build(N("self"));
        Assert.True(route.Index.TryGetOrAssign(N("ghost-hop"), out var ghostIdx));
        Assert.True(route.Index.TryGetOrAssign(N("dest"), out var destIdx));
        core.SetRoute(dest: destIdx, nextHop: ghostIdx); // a next hop that is not one of our links

        var routed = route.TryRoute(N("dest"));

        Assert.False(routed.Ok);
        Assert.Equal(RefusalReason.Unreachable, routed.Reason); // refuse, don't guess reachability
    }

    [Fact]
    public void A_node_routes_to_itself_without_consulting_the_core()
    {
        var (route, _) = Build(N("self"));

        var toSelf = route.TryNextHop(N("self"));
        Assert.True(toSelf.Ok);
        Assert.Equal(N("self"), toSelf.Value);

        var routedToSelf = route.TryRoute(N("self"));
        Assert.True(routedToSelf.Ok);
        Assert.Equal(InternetLinkKind.Direct, routedToSelf.Value.Link.Kind);
    }

    [Fact]
    public void A_core_whose_self_index_disagrees_with_the_registry_is_refused_at_construction()
    {
        // A silent off-by-one-node: every translation across the boundary would resolve to the wrong
        // peer with no error anywhere. Surfaced by dogfooding against the real olamnit router.
        var index = new NodeIndex();
        var mismatched = new DsdvCoreDouble(self: 42); // registry will assign self index 1, not 42

        var ex = Assert.Throws<ArgumentException>(() => new DsdvInternetRoute(N("self"), mismatched, index));
        Assert.Contains("does not match", ex.Message);
    }

    [Fact]
    public void A_node_cannot_be_its_own_neighbour()
    {
        var (route, _) = Build(N("self"));
        Assert.Throws<ArgumentException>(() => route.AddNeighbor(N("self"), new InternetLink(InternetLinkKind.Direct)));
    }

    [Fact]
    public void Reachable_destinations_are_reported_as_node_ids_excluding_self()
    {
        var (route, _) = Build(N("self"));
        Assert.True(route.AddNeighbor(N("one"), new InternetLink(InternetLinkKind.Direct)).Ok);
        Assert.True(route.AddNeighbor(N("two"), new InternetLink(InternetLinkKind.Relayed)).Ok);

        var live = route.ReachableDestinations();

        Assert.Contains(N("one"), live);
        Assert.Contains(N("two"), live);
        Assert.DoesNotContain(N("self"), live);
    }
}
