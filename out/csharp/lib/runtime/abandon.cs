using System.Collections.Generic;

namespace GlpRuntime.Runtime;

public static class AbandonOps
{
    /// <summary>FCP-exact design: Abandon operation not yet implemented.</summary>
    /// <remarks>TODO: Implement FCP-compatible abandon semantics.</remarks>
    public static IList<GoalRef> AbandonWriter(long writerId)
    {
        throw new NotImplementedException("Abandon operation not implemented in FCP design");
    }
}
