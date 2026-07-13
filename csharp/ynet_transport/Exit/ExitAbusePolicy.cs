using Ynet.Transport.Capability;

namespace Ynet.Transport.Exit;

public enum EgressClass { Http, Dns, Smtp, Other }

public readonly record struct EgressRequest(NodeId Caller, string Destination, EgressClass Class, long Bytes);

/// <summary>
/// Trusted-gate clearnet exit + exit-abuse policy (FR-012/FR-013, research R2). Extends olamnit's
/// default-deny EgressService: egress not explicitly elected/authorized is DENIED by default; never
/// a volunteer/unknown exit. The abuse policy (BUILD-NEW, no corpus reference) enforces, IN ORDER:
///   (1) destination allow/deny lists  (2) per-caller rate/volume caps  (3) egress-class filters.
/// Policy is expressed as operator-signed records the gate loads. REAL + TESTED (T043).
/// </summary>
public sealed class ExitAbusePolicy
{
    private readonly HashSet<string> _allow;
    private readonly HashSet<string> _deny;
    private readonly HashSet<EgressClass> _blockedClasses;
    private readonly long _perCallerVolumeCap;
    private readonly Dictionary<NodeId, long> _consumed = new();

    public ExitAbusePolicy(
        IEnumerable<string> allowList,
        IEnumerable<string> denyList,
        IEnumerable<EgressClass> blockedClasses,
        long perCallerVolumeCap)
    {
        _allow = new(allowList, StringComparer.OrdinalIgnoreCase);
        _deny = new(denyList, StringComparer.OrdinalIgnoreCase);
        _blockedClasses = new(blockedClasses);
        _perCallerVolumeCap = perCallerVolumeCap;
    }

    /// <summary>
    /// Evaluate an egress request. Returns null to permit; EgressDenied to refuse (observable,
    /// FR-013). Order: allow/deny -> rate/volume caps -> class filters -> default-deny.
    /// </summary>
    public RefusalReason? Evaluate(EgressRequest req)
    {
        // (1) destination allow/deny
        if (_deny.Contains(req.Destination)) return RefusalReason.EgressDenied;
        if (_allow.Count > 0 && !_allow.Contains(req.Destination)) return RefusalReason.EgressDenied; // default-deny

        // (2) per-caller rate/volume caps
        long consumed = _consumed.GetValueOrDefault(req.Caller) + req.Bytes;
        if (_perCallerVolumeCap > 0 && consumed > _perCallerVolumeCap) return RefusalReason.EgressDenied;

        // (3) egress-class filters
        if (_blockedClasses.Contains(req.Class)) return RefusalReason.EgressDenied;

        _consumed[req.Caller] = consumed; // commit the volume only on an allowed egress
        return null;
    }
}
