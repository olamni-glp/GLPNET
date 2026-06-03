// Global Writers Table for madGLP
//
// Tracks local writers that await incoming assignments from remote agents.
// Each entry maps a global name to the local writer that will be assigned
// when a message arrives.
//
// See: docs/ma/madGLP-spec.md Section 3

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GlpRuntime.Multiagent;

/// <summary>
/// Entry created by Globalize (when exporting a writer).
///
/// Stored at index i in the table, corresponds to global name <c>_w(p, i)</c>.
/// When agent q sends a message <c>_w(p, i) := T</c>, this entry
/// identifies the local writer X that should be assigned.
///
/// NOTE: This is a reference-type class (not a record/struct). The Dart source
/// uses default identity equality; <c>record</c> would silently inject value-based
/// Equals/GetHashCode that breaks the "the entry at index i" identity assumption
/// in the surrounding W_p machinery.
/// </summary>
public class GlobalizeEntry
{
    /// <summary>Local writer address that will be assigned when callback arrives.</summary>
    public int WriterAddr { get; }

    /// <summary>Agent who will send the callback.</summary>
    public string RemoteAgent { get; }

    public GlobalizeEntry(int writerAddr, string remoteAgent)
    {
        WriterAddr = writerAddr;
        RemoteAgent = remoteAgent;
    }

    public override string ToString() =>
        $"GlobalizeEntry(writer={WriterAddr}, remote={RemoteAgent})";
}

/// <summary>
/// Entry created by Localize (when importing a reader global name).
///
/// Must search by (RemoteAgent, RemoteIndex) to find the entry.
/// When message <c>_r(p, i) := T</c> arrives, search for entry matching (p, i).
///
/// NOTE: Reference-type class for the same identity-equality reasons as
/// <see cref="GlobalizeEntry"/>. Instances are stored in a List and searched
/// by predicate; identity is preserved so removal-by-predicate matches the
/// exact entry.
/// </summary>
public class LocalizeEntry
{
    /// <summary>Local writer address that will be assigned.</summary>
    public int WriterAddr { get; }

    /// <summary>Agent who created the global name (p in <c>_r(p, i)</c>).</summary>
    public string RemoteAgent { get; }

    /// <summary>Index from the remote agent's global name (i in <c>_r(p, i)</c>).</summary>
    public int RemoteIndex { get; }

    public LocalizeEntry(int writerAddr, string remoteAgent, int remoteIndex)
    {
        WriterAddr = writerAddr;
        RemoteAgent = remoteAgent;
        RemoteIndex = remoteIndex;
    }

    public override string ToString() =>
        $"LocalizeEntry(writer={WriterAddr}, remote={RemoteAgent}, index={RemoteIndex})";
}

/// <summary>
/// Global Writers Table (W_p).
///
/// Tracks writers awaiting incoming assignments from remote agents.
/// Two entry types are stored:
/// <list type="bullet">
///   <item><see cref="GlobalizeEntry"/>: created by Globalize, direct index lookup.</item>
///   <item><see cref="LocalizeEntry"/>: created by Localize, search by (agent, index).</item>
/// </list>
///
/// Index 0 is reserved for the network input serializer (spec Section 4.1).
/// This entry is permanent and supports many-to-one cold-call reception.
///
/// CONCURRENCY MODEL (load-bearing): This class is NOT internally thread-safe.
/// The Dart source relies on the Dart isolate single-threaded ownership invariant —
/// each agent's W_p lives entirely inside one isolate, which has its own heap and
/// event loop. The .NET port must preserve this invariant: W_p MUST be accessed
/// only by code running on a single owning execution context (a pinned Thread, a
/// single-threaded TaskScheduler, or — per the committed isolate_manager.dart port —
/// a Channel&lt;T&gt;-fed single-consumer Task). Using ConcurrentDictionary,
/// ConcurrentBag, Interlocked, or lock here would be incorrect: it does not make
/// compound operations (e.g. AddLocalizeEntry's FindByRemote-then-Add) atomic,
/// and it misadvertises a thread-safety property the surrounding logic does not have.
///
/// See: docs/ma/madGLP-spec.md Section 3
/// </summary>
public class GlobalWritersTable
{
    /// <summary>Agent ID that owns this table.</summary>
    public string AgentId { get; }

    /// <summary>
    /// Single counter for index allocation (spec Section 3.2).
    /// Shared across both Globalize and Localize operations.
    /// Index 0 is reserved for the serializer, so counter starts at 1.
    /// Indices are never reused.
    /// NOT Interlocked — isolate-local single-threaded ownership preserves correctness.
    /// </summary>
    private int _nextIndex = 1;

    /// <summary>
    /// Serializer entry at index 0 (spec Section 4.1).
    /// Maps to the local writer for the network input stream.
    /// This entry is permanent — never removed.
    /// </summary>
    private int? _serializerWriterAddr;

    /// <summary>
    /// GlobalizeEntries: direct index lookup.
    /// Key is the index i, value is the entry at that index.
    /// Corresponds to global name <c>_w(agentId, i)</c>.
    /// NOT ConcurrentDictionary — see class-level concurrency model note.
    /// </summary>
    private readonly Dictionary<int, GlobalizeEntry> _globalizeEntries = new();

    /// <summary>
    /// LocalizeEntries: search by (agent, index).
    /// These entries are created when localizing <c>_r(p, i)</c> names.
    /// NOT ConcurrentBag — see class-level concurrency model note.
    /// </summary>
    private readonly List<LocalizeEntry> _localizeEntries = new();

    public GlobalWritersTable(string agentId)
    {
        AgentId = agentId;
    }

    // =========================================================================
    // Serializer Entry (Index 0) — Network Input Stream
    // =========================================================================

    /// <summary>
    /// Initialize the permanent serializer entry at index 0.
    ///
    /// Called at boot time to set up the network input stream writer.
    /// This entry is never removed — it receives cold-call messages from
    /// any agent via <c>_w(agentId, 0)</c>.
    ///
    /// Spec Section 4.1: "At boot time, each agent p creates a permanent entry
    /// at index 0 mapping <c>_r(p, 0)</c> to the local writer N_p for p's network
    /// input stream."
    /// </summary>
    /// <exception cref="InvalidOperationException">If the serializer entry is already initialized.</exception>
    public void InitializeSerializerEntry(int netInWriterAddr)
    {
        if (_serializerWriterAddr is not null)
        {
            throw new InvalidOperationException("Serializer entry already initialized");
        }
        _serializerWriterAddr = netInWriterAddr;
    }

    /// <summary>Get the current serializer writer address. Returns null if not yet initialized.</summary>
    public int? SerializerWriterAddr => _serializerWriterAddr;

    /// <summary>
    /// Update the serializer writer to a fresh address.
    ///
    /// Called by Receive transaction after binding the current writer
    /// to extend the network input stream.
    ///
    /// Spec Section 8.3: "Assign N_q := [T_q↓ | N'_q] where N'_q is a fresh
    /// writer. Update the entry to (N'_q, *) at index 0."
    /// </summary>
    /// <exception cref="InvalidOperationException">If the serializer entry has not been initialized.</exception>
    public void UpdateSerializerWriter(int newWriterAddr)
    {
        if (_serializerWriterAddr is null)
        {
            throw new InvalidOperationException("Cannot update serializer: not initialized");
        }
        _serializerWriterAddr = newWriterAddr;
    }

    /// <summary>Check if the serializer entry is initialized.</summary>
    public bool HasSerializerEntry => _serializerWriterAddr is not null;

    // =========================================================================
    // GlobalizeEntry Operations
    // =========================================================================

    /// <summary>
    /// Allocate next index and add a GlobalizeEntry.
    ///
    /// Called by Globalize when exporting a writer Y:
    /// <list type="bullet">
    ///   <item>Allocate index i</item>
    ///   <item>Create entry (Y, q) at index i</item>
    ///   <item>Replace Y with <c>_w(p, i)</c> in globalized term</item>
    /// </list>
    /// </summary>
    /// <returns>The allocated index.</returns>
    public int AddGlobalizeEntry(int writerAddr, string remoteAgent)
    {
        var index = _nextIndex++;
        _globalizeEntries[index] = new GlobalizeEntry(writerAddr, remoteAgent);
        return index;
    }

    /// <summary>
    /// Add a LocalizeEntry.
    ///
    /// Called by Localize when importing <c>_r(p, i)</c>:
    /// <list type="bullet">
    ///   <item>Create fresh pair (Z, Z?)</item>
    ///   <item>Add entry (Z, p, i)</item>
    ///   <item>Replace <c>_r(p, i)</c> with Z? in localized term</item>
    /// </list>
    ///
    /// Read-then-write atomicity is guaranteed by the isolate-ownership invariant
    /// (single owning execution context), NOT by an internal lock.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// If a duplicate (remoteAgent, remoteIndex) pair already exists
    /// (spec Section 12: Entry Lifecycle invariant).
    /// </exception>
    public void AddLocalizeEntry(int writerAddr, string remoteAgent, int remoteIndex)
    {
        // Check for duplicates (spec Section 12: Entry Lifecycle invariant)
        var existing = FindByRemote(remoteAgent, remoteIndex);
        if (existing is not null)
        {
            throw new ArgumentException(
                $"Duplicate LocalizeEntry for ({remoteAgent}, {remoteIndex}): " +
                $"existing writer={existing.WriterAddr}, new writer={writerAddr}");
        }

        _localizeEntries.Add(new LocalizeEntry(writerAddr, remoteAgent, remoteIndex));
    }

    /// <summary>
    /// Lookup GlobalizeEntry by index.
    ///
    /// Used when receiving <c>_w(p, i) := T</c> where we are agent p.
    /// The entry at index i identifies the local writer to assign.
    /// </summary>
    public GlobalizeEntry? LookupByIndex(int index) =>
        _globalizeEntries.GetValueOrDefault(index);

    /// <summary>
    /// Search for LocalizeEntry matching remote (agent, index).
    ///
    /// Used when receiving <c>_r(p, i) := T</c>:
    /// <list type="bullet">
    ///   <item>Search for entry with RemoteAgent=p and RemoteIndex=i</item>
    ///   <item>That entry's WriterAddr is the local writer to assign</item>
    /// </list>
    ///
    /// Returns null if no match. Returns the same reference stored in the list
    /// (identity preserved — callers that compare by reference keep working).
    /// </summary>
    public LocalizeEntry? FindByRemote(string agent, int index) =>
        _localizeEntries.FirstOrDefault(e => e.RemoteAgent == agent && e.RemoteIndex == index);

    /// <summary>
    /// Remove GlobalizeEntry at index.
    ///
    /// Called after receiving <c>_w(p, i) := T</c> and assigning the writer.
    /// Leaves a gap in the index space; indices are not reused.
    ///
    /// Index 0 (serializer) cannot be removed — use <see cref="UpdateSerializerWriter"/> instead.
    /// This guard is a spec-mandated invariant (Section 4.1: "This entry is never removed")
    /// and MUST be preserved verbatim.
    ///
    /// Safe to call on non-existent index (idempotent).
    /// </summary>
    public void RemoveGlobalizeEntry(int index)
    {
        if (index == 0)
        {
            // Serializer entry is permanent — cannot be removed.
            // Spec Section 4.1: "This entry is never removed."
            return;
        }
        _globalizeEntries.Remove(index);
    }

    /// <summary>
    /// Remove LocalizeEntry by remote agent and index.
    ///
    /// Called after receiving <c>_r(p, i) := T</c> and assigning the writer.
    /// Uses RemoveAll (multi-match) to preserve the defensive shape of the Dart
    /// <c>removeWhere</c> source — do not narrow to single-match Remove.
    ///
    /// Safe to call on non-existent entry (idempotent).
    /// </summary>
    public void RemoveLocalizeEntry(string agent, int index)
    {
        _localizeEntries.RemoveAll(e => e.RemoteAgent == agent && e.RemoteIndex == index);
    }

    // =========================================================================
    // Index Allocation and Counters
    // =========================================================================

    /// <summary>
    /// Allocate next index without creating an entry.
    ///
    /// Called by Globalize when exporting a reader Y?:
    /// <list type="bullet">
    ///   <item>Allocate index i for use in <c>_r(p, i)</c></item>
    ///   <item>No entry is created (outgoing link handled by global_send)</item>
    /// </list>
    ///
    /// Spec Section 5.1: "No entry is created — the global_send goal handles
    /// outgoing communication."
    /// </summary>
    /// <returns>The allocated index.</returns>
    public int AllocateIndex() => _nextIndex++;

    /// <summary>Current index counter (for testing/debugging).</summary>
    public int NextIndex => _nextIndex;

    /// <summary>Number of GlobalizeEntries currently in the table.</summary>
    public int GlobalizeEntryCount => _globalizeEntries.Count;

    /// <summary>Number of LocalizeEntries currently in the table.</summary>
    public int LocalizeEntryCount => _localizeEntries.Count;

    public override string ToString()
    {
        var buf = new StringBuilder();
        buf.Append($"GlobalWritersTable({AgentId}, nextIndex={_nextIndex})\n");
        buf.AppendLine($"  Serializer[0]: {_serializerWriterAddr?.ToString() ?? "(not initialized)"}");
        buf.AppendLine("  GlobalizeEntries:");
        foreach (var entry in _globalizeEntries)
        {
            buf.AppendLine($"    [{entry.Key}] {entry.Value}");
        }
        buf.AppendLine("  LocalizeEntries:");
        foreach (var entry in _localizeEntries)
        {
            buf.AppendLine($"    {entry}");
        }
        return buf.ToString();
    }
}
