using GlpRuntime.Multiagent;

namespace GlpRuntime.Runtime;

/// <summary>Suspension operations using FCP-exact shared suspension records.</summary>
/// <remarks>Per heap-pointer-architecture-spec.md v3.0:
/// Suspensions are stored on WRITER cells (not reader cells).
/// For imported readers, suspensions are stored in VariableEntry.</remarks>
public static class SuspendOps
{
    /// <summary>FCP-exact suspension: create ONE shared record, add to each variable's writer.</summary>
    /// <param name="heap">The heap</param>
    /// <param name="goalId">Goal to suspend</param>
    /// <param name="kappa">Resume PC (restart at clause 1)</param>
    /// <param name="readerVarIds">Set of addresses to suspend on (can be writer or reader addresses)</param>
    public static void SuspendGoalFCP(HeapFCP heap, int goalId, int kappa, ISet<int> readerVarIds)
    {
        // Create ONE shared suspension record
        var sharedRecord = new SuspensionRecord(goalId, kappa);

        // Add suspension to each variable
        foreach (var addr in readerVarIds)
        {
            SuspendOnVariable(heap, addr, sharedRecord);
        }
    }

    /// <summary>Add suspension to a variable (follows chain to find final unbound writer).</summary>
    private static void SuspendOnVariable(HeapFCP heap, int addr, SuspensionRecord record)
    {
        // Dereference to find the final target
        var result = heap.DerefAddr(addr);

        if (result is VariableEntry entry)
        {
            // Imported variable - store suspension in entry
            var node = new SuspensionListNode(record);
            node.Next = entry.Suspensions;
            entry.Suspensions = node;
            return;
        }

        if (result is VarRef varRef)
        {
            // Unbound local variable - varRef.Addr is the writer address
            var writerAddr = varRef.Addr;
            heap.SuspendOnWriter(writerAddr, record);
            return;
        }

        // Already bound to ground - no suspension needed
        // (This shouldn't normally happen if we're suspending on unbound vars)
    }

    /// <summary>Legacy version (deprecated).</summary>
    [System.Obsolete("Legacy SuspendGoal deprecated - use SuspendGoalFCP")]
    public static void SuspendGoal(int goalId, int kappa, ISet<int> readerVarIds)
    {
        throw new NotImplementedException("Legacy suspendGoal deprecated - use suspendGoalFCP");
    }
}
