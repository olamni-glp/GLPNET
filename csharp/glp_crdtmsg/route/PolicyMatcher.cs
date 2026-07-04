// Fixed routing-policy matcher (feature 041-crdtmsg-mvp, T051) — MVP-CORE.
//
// Contract C23 / FR-013: the fixed 3-field policy {targets (must-reach), waypoints (ordered), excludes}
// evaluated per hop by a PURE matcher. An unsatisfiable policy FAILS LOUD (consistent with @name
// loud-fail). The DROP taxonomy is logged-never-silent: no-route / malformed / send-failed / over-capacity.
// (This is the shipped evaluator; the experimental GLP guard, T053, is propose-only and does not ship.)

using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Model;

namespace GlpRuntime.CrdtMsg.Route;

/// <summary>The logged-never-silent drop taxonomy (C23).</summary>
public enum DropReason { None, NoRoute, Malformed, SendFailed, OverCapacity }

public sealed record PolicyDecision(bool Deliver, DropReason Drop, string Detail);

public static class PolicyMatcher
{
    /// <summary>
    /// Evaluate the policy at a hop. Deliver iff at least one must-reach target is reachable and no target
    /// is excluded; otherwise a drop with a taxonomy reason (logged, never silent). Pure — no side effects.
    /// </summary>
    public static PolicyDecision Evaluate(RoutingPolicy policy, IReadOnlySet<string> reachable)
    {
        if (policy.Targets.Count > 0)
        {
            var excluded = policy.Targets.FirstOrDefault(t => policy.Excludes.Contains(t));
            if (excluded is not null)
                return new PolicyDecision(false, DropReason.NoRoute, $"target '{excluded}' is excluded — unsatisfiable");

            if (!policy.Targets.Any(reachable.Contains))
                return new PolicyDecision(false, DropReason.NoRoute, "no must-reach target is reachable");
        }
        // waypoints are advisory-ordered for MVP; presence is not enforced at a single hop.
        return new PolicyDecision(true, DropReason.None, "");
    }

    /// <summary>Strict path: fail LOUD on an unsatisfiable policy (throws), else return normally.</summary>
    public static void EvaluateOrThrow(RoutingPolicy policy, IReadOnlySet<string> reachable)
    {
        var d = Evaluate(policy, reachable);
        if (!d.Deliver)
            throw new CrdtMsgException($"routing policy unsatisfiable [{d.Drop}]: {d.Detail}");
    }
}
