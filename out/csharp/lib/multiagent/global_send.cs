// Global Send mechanism for madGLP
//
// Implements the global_send goal that watches a reader and sends
// its value to a remote agent when it becomes known.
//
// See: madGLP-spec.md Section 4 (The global_send Predicate)

using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlpRuntime.Multiagent;

/// <summary>
/// A pending global_send goal waiting for a reader to become known.
///
/// Spawned symmetrically when:
/// - Globalizing a reader Y?: watches Y?, sends to destination
/// - Localizing _w(p,i): watches Y_q?, sends back to p
///
/// See: madGLP-spec.md Section 4
/// </summary>
/// <remarks>
/// NOTE: Reference-type class (not a record/struct). The Dart source uses
/// default identity equality; goals are stored in GlobalSendRegistry keyed
/// by reader-address, so two goals with the same field tuple are
/// observationally distinct. A record would inject value-equality and break
/// the registry-key behaviour.
/// </remarks>
public class GlobalSendGoal
{
    /// <summary>Address of the reader to watch.</summary>
    public int ReaderAddr { get; }

    /// <summary>Global name identifying the link (_w(p,i) or _r(p,i)).</summary>
    public GlobalName GlobalName { get; }

    /// <summary>Agent to send the message to.</summary>
    public string Destination { get; }

    public GlobalSendGoal(int readerAddr, GlobalName globalName, string destination)
    {
        ReaderAddr = readerAddr;
        GlobalName = globalName;
        Destination = destination;
    }

    /// <summary>Create from a GlobalSendSpawn (conversion from spawn info to goal).</summary>
    public static GlobalSendGoal FromSpawn(GlobalSendSpawn spawn) =>
        new GlobalSendGoal(
            readerAddr: spawn.ReaderAddr,
            globalName: spawn.GlobalName,
            destination: spawn.DestAgent);

    public override string ToString() =>
        $"GlobalSendGoal(reader={ReaderAddr}, name={GlobalName}, dest={Destination})";
}

/// <summary>
/// Result of firing a global_send goal.
///
/// Contains the message to be sent and any new goals spawned
/// for nested variables in the value.
/// </summary>
/// <remarks>
/// NOTE: Reference-type class (not a record/struct). Dart source uses
/// default identity equality; GlobalSendFiredResult is returned by reference
/// and consumed by the caller without copying.
/// </remarks>
public class GlobalSendFiredResult
{
    /// <summary>The global name for the message (G in "G := T").</summary>
    public GlobalName GlobalName { get; }

    /// <summary>Destination agent.</summary>
    public string Destination { get; }

    /// <summary>
    /// The original value (before globalization).
    /// </summary>
    /// <remarks>
    /// Dart <c>Object?</c> maps to <c>object?</c> under .NET nullable reference types.
    /// NOT <c>dynamic</c> — <c>object?</c> preserves static type checking and requires
    /// explicit casts at consumer sites, matching Dart <c>Object?</c> semantics.
    /// </remarks>
    public object? Value { get; }

    /// <summary>New goals spawned for nested variables in the value.</summary>
    public IReadOnlyList<GlobalSendGoal> NewGoals { get; }

    /// <summary>The extracted variables from the value (for globalize-term-with-result).</summary>
    public IReadOnlyList<TermVar> ExtractedVariables { get; }

    /// <summary>The globalize result (for globalize-term-with-result).</summary>
    public GlobalizeResult GlobalizeResult { get; }

    public GlobalSendFiredResult(
        GlobalName globalName,
        string destination,
        object? value,
        IReadOnlyList<GlobalSendGoal> newGoals,
        IReadOnlyList<TermVar> extractedVariables,
        GlobalizeResult globalizeResult)
    {
        GlobalName = globalName;
        Destination = destination;
        Value = value;
        NewGoals = newGoals;
        ExtractedVariables = extractedVariables;
        GlobalizeResult = globalizeResult;
    }
}

/// <summary>
/// Registry for pending global_send goals.
///
/// Maps reader addresses to goals waiting for those readers to become known.
/// When a writer is bound, call OnWriterBound() to fire any matching goals.
///
/// See: madGLP-spec.md Section 4, Implementation Plan Section 3.2
/// </summary>
/// <remarks>
/// CONCURRENCY MODEL (load-bearing): GlobalSendRegistry is per-agent
/// isolate-local state. The Dart <c>Map&lt;int, GlobalSendGoal&gt;</c> is unguarded
/// by lock or atomic because no other thread can ever touch it under Dart's
/// isolate-isolation invariant. The .NET port preserves this single-owning-thread
/// invariant — GlobalSendRegistry MUST be accessed only by code running on the
/// agent's owning execution context (a pinned Thread, a single-threaded
/// TaskScheduler, or a Channel&lt;T&gt;-fed single-consumer Task).
/// NOT ConcurrentDictionary: (i) overgeneralises for never-contended state;
/// (ii) does NOT make OnWriterBound's compound Remove → Globalize → Select.ToList
/// sequence atomic; (iii) silently advertises a thread-safety property the
/// surrounding logic does not enforce. Plain <c>Dictionary&lt;int, GlobalSendGoal&gt;</c>
/// is the faithful counterpart, identical to GlobalWritersTable's choice.
/// </remarks>
public class GlobalSendRegistry
{
    /// <summary>Agent ID for this registry (used when globalizing values).</summary>
    public string AgentId { get; }

    /// <summary>
    /// Pending goals indexed by reader address.
    /// NOT ConcurrentDictionary — see class-level concurrency model note.
    /// readonly freezes the reference; contents are mutable.
    /// </summary>
    private readonly Dictionary<int, GlobalSendGoal> _goals = new Dictionary<int, GlobalSendGoal>();

    public GlobalSendRegistry(string agentId)
    {
        AgentId = agentId;
    }

    /// <summary>
    /// Register a goal to watch a reader.
    ///
    /// If the reader is already known (bound), the goal should fire immediately.
    /// This is handled by the caller checking the reader state before registering.
    /// </summary>
    public void Register(GlobalSendGoal goal)
    {
        _goals[goal.ReaderAddr] = goal;
    }

    /// <summary>Register multiple goals from spawn information.</summary>
    public void RegisterSpawns(IReadOnlyList<GlobalSendSpawn> spawns)
    {
        foreach (var spawn in spawns)
        {
            Register(GlobalSendGoal.FromSpawn(spawn));
        }
    }

    /// <summary>Check if there's a goal watching this reader address.</summary>
    public bool HasGoalFor(int readerAddr) => _goals.ContainsKey(readerAddr);

    /// <summary>Get the goal watching this reader address (if any).</summary>
    public GlobalSendGoal? GetGoalFor(int readerAddr) =>
        _goals.TryGetValue(readerAddr, out var goal) ? goal : null;

    /// <summary>
    /// Called when a writer is bound to a value.
    ///
    /// If there's a goal watching this writer's reader, fires the goal:
    /// 1. Removes the goal (one-shot)
    /// 2. Globalizes the value (may produce new goals for nested variables)
    /// 3. Returns the result with message info and new goals
    ///
    /// The caller is responsible for:
    /// - Actually sending the message to the destination
    /// - Registering any new goals returned
    ///
    /// Returns null if no goal was watching this writer.
    /// </summary>
    /// <remarks>
    /// Spec Section 12 (Goal Atomicity): The globalization and message creation
    /// must happen atomically. New goals for nested variables must be registered
    /// before the current operation completes.
    ///
    /// Atomicity is satisfied STRUCTURALLY by the isolate-ownership invariant —
    /// the entire OnWriterBound body executes on the agent's owning execution context
    /// without interleaving, just as in the Dart side. The .NET port MUST NOT
    /// introduce a per-method lock or SemaphoreSlim: doing so would advertise a
    /// thread-safety property at this boundary that the broader Remove → Globalize →
    /// Select.ToList sequence does not honour at its own boundary.
    /// </remarks>
    public GlobalSendFiredResult? OnWriterBound(
        int writerAddr,
        object? value,
        GlobalWritersTable table,
        Func<object?, IReadOnlyList<TermVar>> extractVariables)
    {
        // The writer and reader share the same address in our model
        if (!_goals.Remove(writerAddr, out var goal)) return null;

        // Globalize the value (may spawn new goals for nested variables)
        var variables = extractVariables(value);
        var globalizeResult = MadHelpers.Globalize(
            variables: variables,
            localAgent: AgentId,
            remoteAgent: goal.Destination,
            table: table);

        // Convert spawns to goals
        var newGoals = globalizeResult.Spawns.Select(s => GlobalSendGoal.FromSpawn(s)).ToList();

        return new GlobalSendFiredResult(
            globalName: goal.GlobalName,
            destination: goal.Destination,
            value: value,
            newGoals: newGoals,
            extractedVariables: variables,
            globalizeResult: globalizeResult);
    }

    /// <summary>Number of pending goals (for testing).</summary>
    public int PendingCount => _goals.Count;

    /// <summary>Clear all pending goals (for testing).</summary>
    public void Clear() => _goals.Clear();

    public override string ToString()
    {
        var buf = new StringBuilder();
        buf.Append($"GlobalSendRegistry({AgentId})\n");
        foreach (var entry in _goals)
        {
            buf.AppendLine($"  [{entry.Key}] {entry.Value}");
        }
        return buf.ToString();
    }
}
