using Ynet.Transport.Capability;

namespace Ynet.Transport.Seal;

/// <summary>
/// Routing-mode choice (FR-011): `normal` (latency-optimized clear mesh routes) vs `sealed`
/// (privacy-optimized encoded routes). Unspecified resolves to the declared safe default; a
/// requested seal is NEVER silently downgraded to a clear route — if no sealed path is available
/// the transport fails closed with SealUnavailable. REAL + TESTED (T038).
/// </summary>
public static class RoutingModeResolver
{
    /// <summary>
    /// Resolve the effective selection. A default (unspecified) selection maps to SafeDefault.
    /// </summary>
    public static RoutingSelection Resolve(RoutingSelection? requested)
        => requested ?? RoutingSelection.SafeDefault;

    /// <summary>
    /// Decide whether a selection can be honored given path availability. Fail-closed: a sealed
    /// request with no sealed path available is refused, never downgraded to a clear route (FR-011).
    /// </summary>
    public static RefusalReason? Honor(RoutingSelection selection, bool sealedPathAvailable)
    {
        if (selection.Mode == RoutingMode.Sealed && !sealedPathAvailable)
            return RefusalReason.SealUnavailable; // fail closed — no silent downgrade
        return null;
    }
}

public enum MixTrustSource { Pocw057, LoopixFallback }

public readonly record struct RelayCandidate(NodeId Node, double PocwStake, bool LoopixSemiTrusted);

/// <summary>
/// Sealed-route node selection mix-trust model (FR-010a, clarify §5.5). Standard = stake-weighted
/// selection via the new 057-yngenios-pocw-coin signal; fallback = Loopix-style semi-trusted
/// providers when the pocw signal is unavailable. Fabricates NOTHING when neither is available.
///
/// This pass builds the Loopix FALLBACK for real; the 057 standard path is a clean seam (057 does
/// not exist yet — engineer decision: build against Loopix fallback now, wire 057 when it lands).
/// REAL + TESTED for the fallback; 057 seam documented (T037).
/// </summary>
public sealed class MixTrustSelector
{
    private readonly bool _pocwAvailable;

    public MixTrustSelector(bool pocwSignalAvailable) => _pocwAvailable = pocwSignalAvailable;

    /// <summary>
    /// Select ordered relay hops for a sealed route. Returns null (fabricate nothing) when neither
    /// the pocw signal nor any Loopix semi-trusted provider is available.
    /// </summary>
    public IReadOnlyList<NodeId>? Select(IReadOnlyList<RelayCandidate> candidates, int hopCount)
    {
        if (candidates.Count == 0 || hopCount <= 0) return null;

        IEnumerable<RelayCandidate> ranked;
        if (_pocwAvailable && candidates.Any(c => c.PocwStake > 0))
        {
            // Standard: 057 stake-weighted (higher stake preferred).
            ranked = candidates.Where(c => c.PocwStake > 0).OrderByDescending(c => c.PocwStake);
        }
        else
        {
            // Fallback: Loopix semi-trusted providers only.
            ranked = candidates.Where(c => c.LoopixSemiTrusted);
        }

        var hops = ranked.Take(hopCount).Select(c => c.Node).ToList();
        // Fail closed: an undersized hop set silently weakens the requested anonymity level, so
        // refuse rather than return fewer hops than requested (codexreview finding, FR-011/SC-005).
        return hops.Count < hopCount ? null : hops;
    }

    public MixTrustSource ActiveSource => _pocwAvailable ? MixTrustSource.Pocw057 : MixTrustSource.LoopixFallback;
}
