/// Shared Suspension Records (FCP Design)
/// One SuspensionRecord shared across multiple lists via wrapper nodes
/// Activated once, then disarmed to prevent double-activation
namespace GlpRuntime.Runtime;

/// <summary>Shared suspension state (one per suspended goal).
/// Multiple SuspensionListNodes can point to the same record.</summary>
public class SuspensionRecord
{
    /// <summary>Process ID — nullable for disarming (disarm sentinel).</summary>
    public int? GoalId { get; private set; }

    /// <summary>Where to resume (kappa — procedure entry point).</summary>
    public int ResumePC { get; }

    public SuspensionRecord(int? goalId, int resumePC)
    {
        GoalId = goalId;
        ResumePC = resumePC;
    }

    /// <summary>Disarm this record (prevent future activation). Idempotent.</summary>
    public void Disarm() { GoalId = null; }

    /// <summary>Check if this record is armed (can activate).</summary>
    public bool Armed => GoalId != null;

    public override string ToString() =>
        $"SuspensionRecord(goal={GoalId?.ToString() ?? "null"}, pc={ResumePC}, armed={Armed})";
}

/// <summary>Wrapper node for linking shared records into suspension lists.
/// Each reader cell stores a SuspensionListNode with independent next pointers.
/// Multiple nodes can point to the same SuspensionRecord (shared state).</summary>
public class SuspensionListNode
{
    /// <summary>Points to shared record (non-nullable; set once at construction).</summary>
    public SuspensionRecord Record { get; }

    /// <summary>List chain — independent per reader; mutable for post-construction re-linking.</summary>
    public SuspensionListNode? Next { get; set; }

    public SuspensionListNode(SuspensionRecord record)
    {
        Record = record;
    }

    /// <summary>Delegate to shared record (not cached — preserves disarm propagation).</summary>
    public bool Armed => Record.Armed;
    public int? GoalId => Record.GoalId;
    public int ResumePC => Record.ResumePC;

    public override string ToString() => $"SuspensionListNode(record={Record})";
}
