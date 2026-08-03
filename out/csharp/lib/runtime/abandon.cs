using System.Collections.Generic;

namespace GlpRuntime.Runtime;

[Obsolete("Dead conversion stub: abandon is delivered as the anonymous-writer discard semantic (062 US5 — FCP has no dedicated abandon op). This FCP-exact placeholder was never wired and must not be called.", error: true)]
public static class AbandonOps
{
    /// <summary>FCP-exact design: abandon has no dedicated operation; see the anonymous-writer discard semantic (062 US5).</summary>
    public static IList<GoalRef> AbandonWriter(long writerId)
    {
        throw new NotImplementedException("Abandon operation not implemented in FCP design");
    }
}
