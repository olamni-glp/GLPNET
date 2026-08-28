namespace Ynet.Transport.Relay.Dsdv;

// =====================================================================================================
// TRANSITIONAL MIRROR of `Olamnit.Kernel.Mesh` — engineer decision DEC-DSDV-1 (co #220, solution #67).
//
// FR-021 requires the transport to **extend (not duplicate)** olamnit's DSDV router, and FR-022 forbids
// re-inventing the olamnit DSDV mesh. Olamnit.Kernel is `IsPackable=false` and lives in a separate repo
// (olamni-research/olamnit-assistant), so it cannot be referenced today without a machine-local path
// dependency that would break every other clone and CI. The ruling: **make the olamnit DSDV mesh
// packable/shared**, and meanwhile build the internet EXTENSION against this mirror of its contract.
//
// This file therefore contains **ONLY the contract** — the interfaces and value types the extension
// composes against. It contains **NO DSDV algebra**: no Bellman-Ford relaxation, no sequence-number
// accept rules, no split-horizon / poison-reverse, no metric ceiling logic. That algebra stays
// olamnit's, which is precisely what FR-021 protects.
//
// The shapes are deliberately byte-compatible with `Olamnit.Kernel.Mesh`, so when the shared package
// lands the swap is a `using` statement: DELETE this file and add `using Olamnit.Kernel.Mesh;` to
// DsdvInternetRoute.cs. Nothing else changes. Keep it that way — if you are tempted to add a method
// here that olamnit does not have, you are duplicating, not extending.
// =====================================================================================================

/// <summary>Mirror of <c>Olamnit.Kernel.Mesh.RouteAdvertisement</c>. A neighbour's claim that it can
/// reach <see cref="Dest"/> at <see cref="Cost"/>, carrying destination-originated freshness
/// <see cref="Seq"/> and the advertising neighbour <see cref="Via"/> (split-horizon / poison-reverse).</summary>
public readonly record struct RouteAdvertisement(ushort Origin, ushort Dest, uint Cost, ulong Seq, ushort Via)
{
    /// <summary>Metric ceiling: any cost ≥ this is unreachable. Inherited RIP-style bound ⇒ a route's
    /// path is capped at 15 hops (see the ceiling note on <see cref="DsdvInternetRoute"/>).</summary>
    public const uint MaxCost = 16;

    /// <summary>The unreachable / poisoned metric sentinel.</summary>
    public const uint Infinity = uint.MaxValue;

    public bool IsPoisoned => Cost >= MaxCost;

    public static uint Clamp(uint cost) => cost >= MaxCost ? Infinity : cost;
}

/// <summary>Mirror of <c>Olamnit.Kernel.Mesh.RouteState</c>.</summary>
public enum RouteState : byte
{
    Reachable,
    Poisoned,
}

/// <summary>Mirror of <c>Olamnit.Kernel.Mesh.RouteEntry</c> — one row of the distance-vector table.</summary>
public readonly record struct RouteEntry(
    ushort Dest, ushort NextHop, uint Cost, ulong Seq, RouteState State,
    bool Stable = false,
    long LastRefreshTick = 0)
{
    public bool IsReachable => State == RouteState.Reachable && Cost < RouteAdvertisement.MaxCost;
}

/// <summary>Mirror of <c>Olamnit.Kernel.Mesh.RoutingTable</c> — an immutable table snapshot.</summary>
public sealed class RoutingTable(IReadOnlyDictionary<ushort, RouteEntry> entries)
{
    public IReadOnlyDictionary<ushort, RouteEntry> Entries => entries;

    public int ReachableCount
    {
        get { int n = 0; foreach (var e in entries.Values) if (e.IsReachable) n++; return n; }
    }
}

/// <summary>
/// Mirror of <c>Olamnit.Kernel.Mesh.LinkCostInputs</c> — the injected per-link signal set the cost model
/// reads (olamnit FR-017). This is the seam the internet extension feeds: see
/// <see cref="InternetLinkCostModel"/>.
/// </summary>
public readonly record struct LinkCostInputs(
    uint Base = 1,
    uint Quality = 0,
    uint Load = 0,
    uint Period = 0,
    uint Event = 0)
{
    // A positional record struct does NOT apply primary-ctor defaults to the implicit parameterless
    // ctor — `new LinkCostInputs()` would zero Base to a degenerate 0-cost link. Mirrors olamnit's fix.
    public LinkCostInputs() : this(1, 0, 0, 0, 0) { }
}

/// <summary>Mirror of <c>Olamnit.Kernel.Mesh.ILinkCostModel</c> — the layered additive link-cost seam
/// (olamnit FR-017). Olamnit built this so "on hardware the same seam is fed by real measurements with
/// no routing-logic change"; the internet overlay is exactly that case, which is why the extension
/// plugs in HERE rather than forking the router.</summary>
public interface ILinkCostModel
{
    uint CostFor(ushort neighbor, in LinkCostInputs inputs);
}

/// <summary>Mirror of <c>Olamnit.Kernel.Mesh.IRouteClock</c> — injectable logical time (never wall-clock),
/// so replay is deterministic.</summary>
public interface IRouteClock
{
    long NowTick { get; }
}

/// <summary>
/// Mirror of <c>Olamnit.Kernel.Mesh.IDistanceVectorRouter</c> — THE DSDV CORE. This is the seam the
/// packaged olamnit `DistanceVectorRouter` binds to, unchanged (DEC-DSDV-1). ynet_transport deliberately
/// ships **no implementation** of this interface: implementing it here would be the duplication FR-021
/// forbids. The extension composes over it.
/// </summary>
public interface IDistanceVectorRouter
{
    ushort Self { get; }

    bool Ingest(RouteAdvertisement advert);

    bool SetLinkState(ushort neighbor, bool up);

    bool TryNextHop(ushort dest, out ushort nextHop);

    IReadOnlyList<RouteAdvertisement> AdvertsFor(ushort neighbor);

    bool Tick(long nowTick);

    bool TryGetEntry(ushort dest, out RouteEntry entry);

    RoutingTable Snapshot { get; }
}
