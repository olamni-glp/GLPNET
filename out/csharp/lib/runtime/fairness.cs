using GlpRuntime.Runtime;

namespace GlpRuntime.Runtime;

/// <summary>Utility methods for GLP scheduler tail-recursion fairness.</summary>
public static class Fairness
{
    /// <summary>Returns the next tail budget after one tail reduction.
    /// If zero, the scheduler should yield and reset to tailRecursionBudgetInit.</summary>
    public static int NextTailBudget(int current) => current <= 0 ? 0 : current - 1;

    /// <summary>Reset the budget after a yield.</summary>
    public static int ResetTailBudget() => MachineStateConstants.TailRecursionBudgetInit;
}
