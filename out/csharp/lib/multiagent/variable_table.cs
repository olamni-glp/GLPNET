// Minimal Variable Entry for multiagent runtime support
//
// Provides VariableEntry for tracking suspensions on imported readers.
// The full VariableTable (V_p) has been replaced by GlobalWritersTable (W_p)
// for madGLP. This file provides only the entry type needed by the core
// runtime for suspension management.

using GlpRuntime.Runtime;

namespace GlpRuntime.Multiagent;

/// <summary>
/// Entry for tracking variable state (suspensions and values).
///
/// Used by the core runtime to attach suspension lists to imported reader
/// cells that don't have a local writer cell.
///
/// NOTE: This is a reference-type class (not a record/struct/record class).
/// The surrounding suspension-management and unification logic relies on every
/// reference to a given entry observing the same BoundValue and the same
/// Suspensions head. A value type or record would silently fork the bound-value
/// graph and the suspension head on copy-on-assignment, breaking correctness.
///
/// CONCURRENCY MODEL (inherited from GlobalWritersTable): NOT internally
/// thread-safe. The Dart-isolate single-owning-thread invariant is preserved —
/// VariableEntry instances are touched only from their owning agent's execution
/// context. No lock, Interlocked, volatile, or concurrent collections.
/// </summary>
public class VariableEntry
{
    /// <summary>Variable ID (local heap ID).</summary>
    public int VarId { get; }

    /// <summary>Whether this entry is for the reader view (true) or writer view (false).</summary>
    public bool IsReader { get; }

    /// <summary>Agent who created this variable.</summary>
    public string Creator { get; }

    /// <summary>
    /// Creator's original local ID for this variable.
    /// Defaults to VarId when the caller does not supply creatorLocalId —
    /// i.e. the entry is its own creator-local-id.
    /// </summary>
    public int CreatorLocalId { get; }

    /// <summary>
    /// Cached bound value. Overwritten in place by the unification engine when
    /// the variable becomes bound. Public setter required — external callers
    /// (the core runtime) write this field directly.
    /// </summary>
    public Term? BoundValue { get; set; }

    /// <summary>
    /// For imported writers: the creator's local ID of the paired reader.
    /// Set after construction when the import pairing is finalised.
    /// Public setter required — set externally.
    /// </summary>
    public int? PairedReaderCreatorLocalId { get; set; }

    /// <summary>
    /// Head of the linked suspension chain for goals waiting on this variable.
    /// Re-linked after construction as suspensions are added/removed.
    /// Public setter, NOT init-only — suspension-list maintenance re-links the
    /// head externally.
    /// </summary>
    public SuspensionListNode? Suspensions { get; set; }

    /// <summary>
    /// Construct a VariableEntry.
    ///
    /// <paramref name="varId"/>, <paramref name="isReader"/>, and
    /// <paramref name="creator"/> are required (no default). The remaining
    /// parameters are optional and default to null; <paramref name="creatorLocalId"/>
    /// defaults to <paramref name="varId"/> when null (the entry is its own
    /// creator-local-id — the Dart initialiser-list <c>creatorLocalId ?? varId</c>
    /// fallback, faithfully reproduced here because .NET optional parameters
    /// require compile-time constants, not other parameters).
    /// </summary>
    public VariableEntry(
        int varId,
        bool isReader,
        string creator,
        int? creatorLocalId = null,
        Term? boundValue = null,
        int? pairedReaderCreatorLocalId = null)
    {
        VarId = varId;
        IsReader = isReader;
        Creator = creator;
        CreatorLocalId = creatorLocalId ?? varId;
        BoundValue = boundValue;
        PairedReaderCreatorLocalId = pairedReaderCreatorLocalId;
    }

    /// <summary>
    /// Diagnostic trace string consumed by REPL :trace/:debug output and
    /// integration-test fixtures. Shape preserved verbatim from the Dart source:
    /// R-key has a trailing literal '?' (maGLP reader-view convention, NOT a
    /// nullable marker); the creatorLocalId suffix is emitted only when it
    /// differs from VarId.
    /// </summary>
    public override string ToString()
    {
        string keyStr = IsReader ? $"R{VarId}?" : $"W{VarId}";
        string creatorIdStr = CreatorLocalId != VarId
            ? $", creatorLocalId={CreatorLocalId}"
            : string.Empty;
        return $"VarEntry({keyStr}, creator={Creator}{creatorIdStr})";
    }
}
