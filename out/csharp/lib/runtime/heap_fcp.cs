/// <summary>
/// FCP Two-Cell Heap with Pointer Architecture
/// <para>
/// Per heap-pointer-architecture-spec.md v3.0:
/// <list type="bullet">
/// <item>Reader cells point TO writer cells</item>
/// <item>Writer cells contain: null (unbound), SuspensionListNode (waiting), or Pointer (bound to var)</item>
/// <item>Suspensions live on writer cells, not reader cells</item>
/// <item>ValueTag indicates bound to ground value</item>
/// </list>
/// </para>
/// </summary>
namespace GlpRuntime.Runtime;

using System;
using System.Collections.Generic;
using GlpRuntime.Multiagent;

/// <summary>Cell tags matching FCP design.</summary>
/// <remarks>
/// SHOUTcase-acronymed member spellings preserved verbatim per spec-string-fidelity precedent
/// (heap-pointer-architecture-spec.md v3.0 uses these exact strings).
/// Default int underlying type; NOT [Flags] — these are mutually exclusive discriminators.
/// </remarks>
public enum HeapCellTag { WrtTag, RoTag, ValueTag }

/// <summary>
/// Heap cell — contains either Pointer, SuspensionListNode, Term, VariableEntry, or WriterContent.
/// </summary>
/// <remarks>
/// Reference type (NOT a record class, struct, or record struct). Every cell IS its address;
/// mutation in-place (BindWriterWithCallbackControl, BindWriterToReader, SuspendOnWriter,
/// BindImportedReader, ForwardSuspensions, AddSuspension) must propagate to every observer.
/// Value-type semantics would silently copy on assignment, losing bindings.
/// </remarks>
public class HeapCell
{
    /// <summary>
    /// Mutable nullable sum-type slot. At runtime holds exactly one of:
    /// null | Pointer | SuspensionListNode | Term | VariableEntry | WriterContent.
    /// Dart dynamic → object? (NOT C# dynamic/DLR — pattern-match on concrete types).
    /// </summary>
    public object? Content { get; set; }

    /// <summary>Mutable discriminator tag.</summary>
    public HeapCellTag Tag { get; set; }

    public HeapCell(object? content, HeapCellTag tag)
    {
        Content = content;
        Tag = tag;
    }

    /// <summary>True when this cell holds a bound-to-ground value (ValueTag).</summary>
    public bool HasValue => Tag == HeapCellTag.ValueTag;

    /// <summary>True when this cell holds a WriterContent with a non-null suspension chain.</summary>
    public bool HasSuspensions => Content is WriterContent wc && wc.Suspensions != null;
}

/// <summary>Pointer to another cell (heap address).</summary>
/// <remarks>
/// Reference type (sealed class, NOT a record class or readonly record struct).
/// Stored as HeapCell.Content (typed object?) — a record struct would box on every
/// assignment and unbox on every type-test, defeating value-type benefits AND losing
/// reference identity across box/unbox cycles. No == override (reference identity
/// preserved — matches Dart's no-==override contract).
/// </remarks>
public sealed class Pointer
{
    /// <summary>Target heap address.</summary>
    public int TargetAddr { get; }

    public Pointer(int targetAddr)
    {
        TargetAddr = targetAddr;
    }

    public override string ToString() => $"Ptr({TargetAddr})";
}

/// <summary>
/// Compound content for an unbound writer cell that has accumulated suspensions.
/// </summary>
/// <remarks>
/// Per spec v3.2 Section 2.3: when suspensions are added to an unbound writer, the
/// reader pointer is preserved in this compound structure so that ReaderForWriter()
/// keeps working even when suspensions are present.
/// <para>
/// Reference type (sealed class, NOT a record/struct). Mutation of Suspensions in place
/// must propagate to every observer; a value type would fork the suspension head on copy.
/// </para>
/// </remarks>
public sealed class WriterContent
{
    /// <summary>Pointer to paired reader (preserved; init-only).</summary>
    public int ReaderAddr { get; }

    /// <summary>
    /// Mutable nullable head of per-writer suspension chain.
    /// Public setter required — SuspendOnWriter and ForwardSuspensions mutate this in place.
    /// </summary>
    public SuspensionListNode? Suspensions { get; set; }

    /// <summary>
    /// Construct a WriterContent. The suspensions parameter is optional (default null),
    /// matching Dart's optional-positional [this.suspensions] bracket syntax.
    /// </summary>
    public WriterContent(int readerAddr, SuspensionListNode? suspensions = null)
    {
        ReaderAddr = readerAddr;
        Suspensions = suspensions;
    }

    public override string ToString() =>
        $"WriterContent(reader={ReaderAddr}, sus={Suspensions?.ToString() ?? "null"})";
}

/// <summary>
/// FCP Two-Cell Heap with Pointer-Based Variable Identity.
/// </summary>
/// <remarks>
/// Per heap-pointer-architecture-spec.md v3.0:
/// <list type="bullet">
/// <item>AllocateVariable() returns (WriterAddr, ReaderAddr) tuple</item>
/// <item>Reader cell points TO writer cell</item>
/// <item>Writer cell contains null (unbound), SuspensionListNode, or Pointer (chain)</item>
/// <item>Suspensions are stored on writer cells</item>
/// </list>
/// <para>
/// Reference type (NOT a record/struct/record-struct). Identity equality is load-bearing —
/// held by reference from every BytecodeRunner/RunnerContext and mutated in place.
/// NOT static — the runtime supports multiple concurrent heaps (one per agent/MadContext).
/// </para>
/// <para>
/// CONCURRENCY MODEL: Single-owning-context invariant (escalation #4, commit 497428c8).
/// Every HeapFCP instance is accessed by exactly one execution context. Fields are plain
/// (no lock, Interlocked, ConcurrentDictionary, or volatile) — faithfully preserving
/// Dart's single-threaded isolate model. The hot-path DerefAddr runs lock-free.
/// </para>
/// </remarks>
public class HeapFCP
{
    /// <summary>
    /// The heap cells list. Reference is fixed (Dart final); contents mutated via Add and indexer.
    /// MUST be concrete List&lt;HeapCell&gt; (not IList — requires indexer + Add + Count).
    /// </summary>
    public List<HeapCell> Cells { get; } = new();

    /// <summary>
    /// Heap pointer — next free address. Dart HP (WAM/spec name) rendered as Hp
    /// (PascalCase per .NET two-letter-acronym capitalisation rule). Mutable.
    /// </summary>
    public int Hp { get; set; } = 0;

    /// <summary>Callbacks for external observation (Phase 0 I/O). Keyed by writerAddr.</summary>
    private readonly Dictionary<int, Action<Term>> _bindCallbacks = new();

    // ==========================================================================
    // Variable Allocation (Section 3 of spec)
    // ==========================================================================

    /// <summary>
    /// Allocate a fresh local variable.
    /// Returns (WriterAddr, ReaderAddr) tuple.
    /// </summary>
    /// <remarks>
    /// Per spec v3.2 Section 3.1 (FCP pattern):
    /// Writer cell: Pointer to reader (bidirectional).
    /// Reader cell: Pointer to writer.
    /// Both cells point to each other enabling navigation without arithmetic.
    /// The allocation order (writer first, reader second) MUST be preserved — the
    /// writer's Pointer(readerAddr) references HP+1 computed before the second Add.
    /// </remarks>
    public (int WriterAddr, int ReaderAddr) AllocateVariable()
    {
        int writerAddr = Hp;
        int readerAddr = Hp + 1;
        Hp += 2;

        // Writer cell: points TO reader (FCP pattern)
        Cells.Add(new HeapCell(new Pointer(readerAddr), HeapCellTag.WrtTag));

        // Reader cell: points TO writer
        Cells.Add(new HeapCell(new Pointer(writerAddr), HeapCellTag.RoTag));

        return (writerAddr, readerAddr);
    }

    /// <summary>
    /// Allocate a single reader cell for an imported variable (no local writer).
    /// </summary>
    /// <remarks>
    /// Per irmaGLP spec, imported readers have no local paired writer.
    /// The cell content will be set to a VariableEntry by the caller.
    /// </remarks>
    public int AllocateImportedReader()
    {
        int readerAddr = Hp++;
        Cells.Add(new HeapCell(null, HeapCellTag.RoTag));
        return readerAddr;
    }

    /// <summary>
    /// Allocate a single writer cell for an imported variable (no local reader).
    /// </summary>
    /// <remarks>
    /// Per irmaGLP spec, imported writers have no local paired reader.
    /// The cell content will be set to a VariableEntry by the caller.
    /// </remarks>
    public int AllocateImportedWriter()
    {
        int writerAddr = Hp++;
        Cells.Add(new HeapCell(null, HeapCellTag.WrtTag));
        return writerAddr;
    }

    // ==========================================================================
    // Cell Type Checking
    // ==========================================================================

    /// <summary>Check if address is a writer cell.</summary>
    public bool IsWriter(int addr) =>
        addr >= 0 && addr < Cells.Count && Cells[addr].Tag == HeapCellTag.WrtTag;

    /// <summary>Check if address is a reader cell.</summary>
    public bool IsReader(int addr) =>
        addr >= 0 && addr < Cells.Count && Cells[addr].Tag == HeapCellTag.RoTag;

    /// <summary>Check if address is a value cell (bound to ground).</summary>
    public bool IsValue(int addr) =>
        addr >= 0 && addr < Cells.Count && Cells[addr].Tag == HeapCellTag.ValueTag;

    // ==========================================================================
    // Pointer Navigation (Section 7 of spec)
    // ==========================================================================

    /// <summary>
    /// Try to get the writer address from a reader address.
    /// </summary>
    /// <returns>
    /// The writer address for local readers (cell.Content is Pointer),
    /// or null for imported readers (cell.Content is VariableEntry) or non-readers.
    /// </returns>
    /// <remarks>
    /// Per spec Section 7.1: Follow the reader's pointer to get the writer.
    /// <para>
    /// <strong>Callers MUST handle null appropriately:</strong>
    /// </para>
    /// <para>
    /// 1. <strong>Suspending operations</strong> (most common): If the operation needs the writer
    ///    but gets null, the reader is imported and the goal should suspend:
    ///    <example>
    ///    <code>
    ///    int? wid = heap.TryWriterForReader(rid);
    ///    if (wid == null) {
    ///      // Imported reader - suspend on it
    ///      SuspendOnReader(rid, ...);
    ///      return RunResult.Suspended;
    ///    }
    ///    </code>
    ///    </example>
    /// </para>
    /// <para>
    /// 2. <strong>Read-only operations</strong>: If just reading the value (not modifying),
    ///    use DerefAddr() instead, which handles imported readers transparently.
    /// </para>
    /// <para>
    /// 3. <strong>Binding operations</strong>: Operations that need to bind the writer (e.g.,
    ///    unification) should check IsImportedReader first and use the appropriate
    ///    binding method:
    ///    <example>
    ///    <code>
    ///    if (heap.IsImportedReader(rid)) {
    ///      // Imported - can't bind locally, must receive value from creator
    ///      SuspendOnReader(rid, ...);
    ///    } else {
    ///      // Local - can bind the writer
    ///      heap.BindVariable(wid, value);
    ///    }
    ///    </code>
    ///    </example>
    /// </para>
    /// <para>
    /// <strong>Common mistakes to avoid:</strong>
    /// <list type="bullet">
    /// <item>Using wid! (null-forgiving) without null check — crash on imported readers</item>
    /// <item>Silently ignoring null — logic errors, lost bindings</item>
    /// <item>Throwing errors — breaks multiagent scenarios</item>
    /// <item>Imported readers cannot be targets of writer-to-reader binding</item>
    /// </list>
    /// </para>
    /// </remarks>
    public int? TryWriterForReader(int readerAddr)
    {
        var cell = Cells[readerAddr];
        if (cell.Tag != HeapCellTag.RoTag)
        {
            return null;
        }
        if (cell.Content is Pointer ptr) return ptr.TargetAddr;
        return null; // Imported reader - no local writer
    }

    /// <summary>
    /// Find the paired reader for an unbound writer (FCP pattern).
    /// </summary>
    /// <remarks>
    /// Per spec v3.2 Section 7.2: Follow the writer's pointer to find its paired reader.
    /// Returns null if writer is bound (pointer no longer points to paired reader).
    /// <para>
    /// For code that needs the reader address regardless of binding state,
    /// use PairedReaderAddr() instead.
    /// </para>
    /// </remarks>
    public int? ReaderForWriter(int writerAddr)
    {
        var cell = Cells[writerAddr];
        if (cell.Tag != HeapCellTag.WrtTag)
        {
            return null;
        }

        // Case 1: Unbound without suspensions - direct Pointer to reader
        if (cell.Content is Pointer ptr1)
        {
            int target = ptr1.TargetAddr;
            // Verify it's the paired reader (points back to this writer) — BIDIRECTIONAL CHECK
            if (target < Cells.Count && Cells[target].Tag == HeapCellTag.RoTag &&
                Cells[target].Content is Pointer readerPtr && readerPtr.TargetAddr == writerAddr)
            {
                return target; // Confirmed bidirectional - this is the paired reader
            }
            // Writer is bound to something else, no direct reader access
            return null;
        }

        // Case 2: Unbound with suspensions - compound WriterContent preserves reader pointer
        if (cell.Content is WriterContent wc) return wc.ReaderAddr;

        // Case 3: Bound or invalid - no reader access
        return null;
    }

    /// <summary>
    /// Get the paired reader address for a writer (works for bound and unbound).
    /// </summary>
    /// <remarks>
    /// Per allocation pattern, the reader is always at writerAddr + 1.
    /// This method should be used when you need the reader address regardless
    /// of whether the writer is currently bound.
    /// <para>
    /// Note: For checking if a writer is unbound and getting its reader via
    /// the bidirectional pointer pattern, use ReaderForWriter() instead.
    /// </para>
    /// </remarks>
    public int PairedReaderAddr(int writerAddr)
    {
        // Try the FCP pattern first (works for unbound writers)
        int? reader = ReaderForWriter(writerAddr);
        if (reader != null) return reader.Value;

        // Fallback: by allocation, reader is at writerAddr + 1
        return writerAddr + 1;
    }

    // ==========================================================================
    // Dereferencing (Section 4 of spec)
    // ==========================================================================

    /// <summary>
    /// Dereference an address to its final value.
    /// </summary>
    /// <remarks>
    /// Per spec Section 4.2:
    /// <list type="bullet">
    /// <item>RoTag: follow Pointer to target</item>
    /// <item>WrtTag with null/SuspensionListNode: unbound, return VarRef</item>
    /// <item>WrtTag with Pointer: follow to target (variable chain)</item>
    /// <item>ValueTag: return the Term content</item>
    /// <item>VariableEntry: check state for value or return entry</item>
    /// </list>
    /// Returns: Term (bound) | VarRef (unbound writer) | VariableEntry (imported unbound).
    /// </remarks>
    public object DerefAddr(int startAddr)
    {
        int current = startAddr;
        var visited = new HashSet<int>();
        HeapCellTag? previousTag = null; // Track previous tag for WxW detection

        while (true)
        {
            if (visited.Contains(current))
            {
                throw new InvalidOperationException($"Cycle detected at address {current} - SRSW violation!");
            }
            visited.Add(current);

            var cell = Cells[current];

            // Per spec Section 4.5: WxW detection during deref
            // If we followed a pointer from a writer and landed on another writer, that's a violation
            if (previousTag == HeapCellTag.WrtTag && cell.Tag == HeapCellTag.WrtTag)
            {
                throw new InvalidOperationException(
                    $"SRSW violation: writer at {visited.ElementAt(visited.Count - 2)} points to writer at {current}");
            }

            switch (cell.Tag)
            {
                case HeapCellTag.RoTag:
                    // Reader cell
                    if (cell.Content is VariableEntry entryRo)
                    {
                        // Imported reader - check for cached bound value in entry
                        if (entryRo.BoundValue != null)
                        {
                            return entryRo.BoundValue;
                        }
                        return entryRo; // Unbound imported
                    }
                    if (cell.Content is Pointer ptrRo)
                    {
                        // Follow pointer to writer
                        previousTag = cell.Tag;
                        current = ptrRo.TargetAddr;
                        continue;
                    }
                    throw new InvalidOperationException($"Reader cell at {current} has invalid content: {cell.Content}");

                case HeapCellTag.WrtTag:
                    // Writer cell
                    if (cell.Content is VariableEntry entryWrt)
                    {
                        // Imported writer - check for cached bound value in entry
                        if (entryWrt.BoundValue != null)
                        {
                            return entryWrt.BoundValue;
                        }
                        return entryWrt; // Unbound imported
                    }
                    // Case 1: WriterContent - unbound with suspensions (FCP pattern)
                    if (cell.Content is WriterContent)
                    {
                        // Unbound writer with suspensions - return VarRef to this address
                        return new VarRef(current);
                    }
                    // Case 2: Pointer - check if bidirectional (unbound) or chain (bound)
                    if (cell.Content is Pointer ptrWrt)
                    {
                        int target = ptrWrt.TargetAddr;
                        // Check if pointer is to paired reader (unbound) or to bound value
                        if (target < Cells.Count && Cells[target].Tag == HeapCellTag.RoTag)
                        {
                            if (Cells[target].Content is Pointer readerContent && readerContent.TargetAddr == current)
                            {
                                // Bidirectional - points to paired reader which points back
                                // This is an unbound variable
                                return new VarRef(current);
                            }
                        }
                        // Bound to another cell - follow the pointer
                        previousTag = cell.Tag;
                        current = target;
                        continue;
                    }
                    throw new InvalidOperationException($"Writer cell at {current} has invalid content: {cell.Content}");

                case HeapCellTag.ValueTag:
                    // Bound to ground value
                    return (Term)cell.Content!;

                default:
                    throw new InvalidOperationException($"Unknown HeapCellTag: {cell.Tag}");
            }
        }
    }

    // ==========================================================================
    // Binding (Section 5 of spec)
    // ==========================================================================

    /// <summary>
    /// Bind a writer to a ground term value.
    /// </summary>
    /// <remarks>
    /// Per spec Section 5.1:
    /// Changes writer tag to ValueTag, stores value as content, activates any suspensions.
    /// Returns list of goals to reactivate.
    /// </remarks>
    public List<GoalRef> BindWriter(int writerAddr, Term value) =>
        BindWriterWithCallbackControl(writerAddr, value, fireCallback: true);

    /// <summary>
    /// Bind a writer without firing callbacks.
    /// </summary>
    /// <remarks>
    /// Used by applySigmaHatFCP to defer callbacks until all bindings complete.
    /// This ensures nested VarRefs in structures can be dereferenced correctly.
    /// </remarks>
    public List<GoalRef> BindWriterNoCallback(int writerAddr, Term value) =>
        BindWriterWithCallbackControl(writerAddr, value, fireCallback: false);

    /// <summary>Internal: bind with callback control.</summary>
    public List<GoalRef> BindWriterWithCallbackControl(int writerAddr, Term value, bool fireCallback)
    {
        var cell = Cells[writerAddr];
        if (cell.Tag != HeapCellTag.WrtTag)
        {
            throw new InvalidOperationException(
                $"bindWriter called on non-writer cell at {writerAddr} (tag: {cell.Tag})");
        }

        var activations = new List<GoalRef>();

        // Save and process suspensions before overwriting (FCP pattern: check WriterContent)
        if (cell.Content is WriterContent wc)
        {
            WalkAndActivate(wc.Suspensions, activations);
        }

        // Bind to value (in-place mutation — BOTH fields on same HeapCell instance)
        cell.Content = value;
        cell.Tag = HeapCellTag.ValueTag;

        // Notify external observer if registered
        if (fireCallback)
        {
            if (_bindCallbacks.Remove(writerAddr, out var callback))
            {
                callback(value);
            }
        }

        return activations;
    }

    /// <summary>
    /// Fire pending callback for a writer (if any).
    /// Used after all bindings complete to fire deferred callbacks.
    /// </summary>
    public void FirePendingCallback(int writerAddr)
    {
        if (_bindCallbacks.Remove(writerAddr, out var callback))
        {
            var value = GetValue(writerAddr);
            if (value != null) callback(value);
        }
    }

    /// <summary>
    /// Bind a writer to another variable (via its reader).
    /// </summary>
    /// <remarks>
    /// Per spec Section 5.3:
    /// Stores Pointer(readerAddr) in writer cell; forwards suspensions to target writer;
    /// Tag REMAINS WrtTag (not bound to ground).
    /// Returns list of goals to reactivate (empty if target unbound).
    /// </remarks>
    public List<GoalRef> BindWriterToReader(int writerAddr, int readerAddr)
    {
        var writerCell = Cells[writerAddr];
        if (writerCell.Tag != HeapCellTag.WrtTag)
        {
            throw new InvalidOperationException(
                $"bindWriterToReader called on non-writer at {writerAddr}");
        }

        var readerCell = Cells[readerAddr];
        if (readerCell.Tag != HeapCellTag.RoTag)
        {
            throw new InvalidOperationException(
                $"bindWriterToReader target is not a reader at {readerAddr}");
        }

        // BindWriterToReader only works with LOCAL readers (must have paired writer).
        // Imported readers cannot be targets of writer-to-reader binding.
        int? targetWriterAddr = TryWriterForReader(readerAddr);
        if (targetWriterAddr == null)
        {
            throw new InvalidOperationException(
                $"bindWriterToReader target at {readerAddr} is an imported reader (no local writer)");
        }

        var activations = new List<GoalRef>();

        // Forward suspensions to target writer (FCP pattern: check WriterContent)
        if (writerCell.Content is WriterContent wc)
        {
            ForwardSuspensions(wc.Suspensions, targetWriterAddr.Value);
        }

        // Store pointer to reader (creates variable chain); Tag REMAINS WrtTag
        writerCell.Content = new Pointer(readerAddr);

        // Relocate external callback from this writer to target writer
        if (_bindCallbacks.Remove(writerAddr, out var callback))
        {
            _bindCallbacks[targetWriterAddr.Value] = callback;
        }

        return activations;
    }

    /// <summary>
    /// Bind writer to writer (WxW violation). Per spec Section 5.2: this is forbidden and always throws.
    /// </summary>
    public void BindWriterToWriter(int w1, int w2) =>
        throw new InvalidOperationException($"WxW violation: cannot bind writer {w1} to writer {w2}");

    // ==========================================================================
    // Suspension (Section 6 of spec)
    // ==========================================================================

    /// <summary>
    /// Add a suspension to a writer cell.
    /// </summary>
    /// <remarks>
    /// Per spec v3.2 Section 6.1: Suspensions are stored on writer cells using
    /// WriterContent to preserve the reader pointer.
    /// </remarks>
    public void SuspendOnWriter(int writerAddr, SuspensionRecord record)
    {
        var cell = Cells[writerAddr];
        if (cell.Tag != HeapCellTag.WrtTag)
        {
            throw new InvalidOperationException($"suspendOnWriter called on non-writer at {writerAddr}");
        }

        var node = new SuspensionListNode(record);

        // FCP pattern: preserve reader pointer using WriterContent
        if (cell.Content is WriterContent wc)
        {
            // Already has WriterContent - add to suspension list (prepend; ORDER MATTERS)
            node.Next = wc.Suspensions;
            wc.Suspensions = node;
        }
        else if (cell.Content is Pointer ptr)
        {
            // First suspension: convert Pointer to WriterContent (promote)
            cell.Content = new WriterContent(ptr.TargetAddr, node);
        }
        else
        {
            throw new InvalidOperationException(
                $"suspendOnWriter: unexpected content {cell.Content} at {writerAddr}");
        }
    }

    /// <summary>
    /// Add a suspension via a reader (finds writer and adds there).
    /// </summary>
    /// <remarks>
    /// Per spec Section 6.1: Find the reader's writer and add suspension there.
    /// </remarks>
    public void SuspendOnReader(int readerAddr, SuspensionRecord record)
    {
        var cell = Cells[readerAddr];

        if (cell.Content is VariableEntry entry)
        {
            // Imported reader - store suspension in VariableEntry.Suspensions
            // Per spec Section 3.1.2: For imported readers, V_p serves as the
            // "virtual writer" that holds suspensions. When an assignment arrives,
            // goals are resumed from VariableEntry.Suspensions.
            var node = new SuspensionListNode(record);
            node.Next = entry.Suspensions;
            entry.Suspensions = node;
            return;
        }

        if (cell.Tag != HeapCellTag.RoTag || cell.Content is not Pointer ptr)
        {
            throw new InvalidOperationException(
                $"suspendOnReader called on invalid reader at {readerAddr}");
        }

        SuspendOnWriter(ptr.TargetAddr, record);
    }

    /// <summary>
    /// Forward suspensions from one writer to another.
    /// </summary>
    /// <remarks>
    /// Per spec v3.2: Target writer uses WriterContent to preserve reader pointer.
    /// CRITICAL: clones the wrapper node but SHARES the SuspensionRecord by reference —
    /// Disarm() on the shared record propagates to every wrapper preventing double-activation.
    /// MUST NOT clone the record itself (would break disarm propagation).
    /// Silently no-ops on bound or invalid targets (intentional — not an error).
    /// Leading underscore dropped per .NET naming guideline (private modifier is canonical).
    /// </remarks>
    private void ForwardSuspensions(SuspensionListNode? list, int targetWriterAddr)
    {
        var current = list;
        while (current != null)
        {
            if (current.Armed)
            {
                // Clone WRAPPER node but SHARE the record by reference (disarm propagation)
                var newNode = new SuspensionListNode(current.Record);
                var targetCell = Cells[targetWriterAddr];

                if (targetCell.Content is WriterContent wc)
                {
                    // Target already has WriterContent - add to its suspension list
                    newNode.Next = wc.Suspensions;
                    wc.Suspensions = newNode;
                }
                else if (targetCell.Content is Pointer ptr)
                {
                    // Target is unbound with no suspensions - create WriterContent (promote)
                    targetCell.Content = new WriterContent(ptr.TargetAddr, newNode);
                }
                // Ignore other cases (e.g., bound targets) — intentional no-op
            }
            current = current.Next;
        }
    }

    /// <summary>
    /// Walk suspension list and activate armed records.
    /// </summary>
    /// <remarks>
    /// Static method (no instance state). For each armed node: constructs a GoalRef from
    /// goalId/resumePC, adds to activations, then calls Record.Disarm() to propagate
    /// disarming across every wrapper pointing at the same shared SuspensionRecord.
    /// Leading underscore dropped per .NET naming guideline.
    /// </remarks>
    private static void WalkAndActivate(SuspensionListNode? list, List<GoalRef> activations)
    {
        var current = list;
        while (current != null)
        {
            if (current.Armed)
            {
                activations.Add(new GoalRef(current.GoalId!.Value, current.ResumePC));
                current.Record.Disarm();
            }
            current = current.Next;
        }
    }

    // ==========================================================================
    // High-Level API
    // ==========================================================================

    /// <summary>
    /// Check if variable is fully bound to ground term.
    /// Returns false for VarRef (unbound) or VariableEntry (imported unbound).
    /// </summary>
    public bool IsFullyBound(int writerAddr)
    {
        var result = DerefAddr(writerAddr);
        return result is not VarRef && result is not VariableEntry;
    }

    /// <summary>
    /// Get variable value (dereferenced). Returns null if unbound.
    /// </summary>
    public Term? GetValue(int writerAddr)
    {
        var result = DerefAddr(writerAddr);
        if (result is VarRef || result is VariableEntry) return null;
        return (Term)result;
    }

    /// <summary>
    /// Dereference a term. If term is VarRef, dereferences it. Otherwise returns term unchanged.
    /// </summary>
    /// <remarks>
    /// LOAD-BEARING: for imported-unbound (VariableEntry result), the method returns the
    /// ORIGINAL term — preserves caller's VarRef handle identity. Must NOT return a fresh VarRef.
    /// </remarks>
    public Term Dereference(Term term)
    {
        if (term is VarRef varRef)
        {
            var result = DerefAddr(varRef.Addr);
            if (result is VariableEntry) return term; // Imported unbound - return original
            if (result is VarRef resultVar) return resultVar; // Still unbound
            return (Term)result;
        }
        return term;
    }

    /// <summary>Register callback for when variable is bound.</summary>
    /// <remarks>
    /// If already bound, fires immediately. Otherwise registers for later firing on binding.
    /// Indexer-set replaces any previous callback at that address (NOT a multi-cast event).
    /// </remarks>
    public void OnBind(int writerAddr, Action<Term> callback)
    {
        if (IsFullyBound(writerAddr))
        {
            var value = GetValue(writerAddr);
            if (value != null) callback(value);
            return;
        }
        _bindCallbacks[writerAddr] = callback;
    }

    /// <summary>Remove a registered callback.</summary>
    public void RemoveBindCallback(int writerAddr) => _bindCallbacks.Remove(writerAddr);

    // ==========================================================================
    // Imported Reader Binding (Multiagent)
    // ==========================================================================

    /// <summary>
    /// Bind an imported reader to a received value.
    /// </summary>
    /// <remarks>
    /// Per irmaGLP spec Section 5.3 (imported reader case):
    /// Imported readers have no local writer, just a reader cell with VariableEntry.
    /// When assignment arrives, the reader cell is updated to point to the value.
    /// Activations are extracted from VariableEntry.Suspensions.
    /// <para>
    /// Heap structure transformation:
    /// <list type="bullet">
    /// <item>BEFORE (unbound imported reader): cells[readerAddr] = HeapCell(VariableEntry(...), HeapCellTag.RoTag)</item>
    /// <item>AFTER (bound imported reader): cells[readerAddr] = HeapCell(Pointer(valueCellAddr), HeapCellTag.RoTag)</item>
    /// <item>AFTER (value cell): cells[valueCellAddr] = HeapCell(value, HeapCellTag.ValueTag)</item>
    /// </list>
    /// </para>
    /// <para>
    /// Note: Unlike local readers (which point to their paired writer), imported
    /// readers point directly to a ValueTag cell. This distinction is used by
    /// IsImportedReader() to detect bound imported readers.
    /// </para>
    /// <para>
    /// CRITICAL: Hp++ immediately followed by Cells.Add — HP-cells-length sync invariant
    /// (from source comment "Use HP++ to keep HP in sync with cells.length"). These two
    /// operations must remain co-located.
    /// </para>
    /// Returns list of goals to reactivate (from VariableEntry suspensions).
    /// </remarks>
    public List<GoalRef> BindImportedReader(int readerAddr, Term value, VariableEntry entry)
    {
        var cell = Cells[readerAddr];
        if (cell.Tag != HeapCellTag.RoTag)
        {
            throw new InvalidOperationException(
                $"bindImportedReader called on non-reader cell at {readerAddr} (tag: {cell.Tag})");
        }
        if (cell.Content is not VariableEntry)
        {
            throw new InvalidOperationException(
                $"bindImportedReader called on reader without VariableEntry at {readerAddr}");
        }

        var activations = new List<GoalRef>();

        // Extract activations from VariableEntry suspensions (linked list)
        if (entry.Suspensions != null)
        {
            WalkAndActivate(entry.Suspensions, activations);
        }

        // Allocate a value cell for the term and point reader to it
        // IMPORTANT: Hp++ immediately followed by Cells.Add — HP-cells-length sync invariant
        int valueCellAddr = Hp++;
        Cells.Add(new HeapCell(value, HeapCellTag.ValueTag));
        cell.Content = new Pointer(valueCellAddr);

        return activations;
    }

    // ==========================================================================
    // Compatibility Methods (for gradual migration of callers)
    // ==========================================================================

    /// <summary>Bind variable to a term (compatibility wrapper).</summary>
    public List<GoalRef> BindVariable(int writerAddr, Term value)
    {
        if (value is VarRef varRef)
        {
            // Binding to another variable
            if (IsReader(varRef.Addr))
            {
                return BindWriterToReader(writerAddr, varRef.Addr);
            }
            else if (IsWriter(varRef.Addr))
            {
                BindWriterToWriter(writerAddr, varRef.Addr); // Will throw
                return new List<GoalRef>(); // Unreachable: BindWriterToWriter always throws
            }
        }
        return BindWriter(writerAddr, value);
    }

    /// <summary>
    /// FR-035/SC-009: dispatch a value-arrival bind to the correct heap path,
    /// keeping BOTH variable representations (Preserve-Working-Code). A genuinely
    /// writerless imported reader (<see cref="AllocateImportedReader"/>: a RoTag cell
    /// whose content is a <see cref="VariableEntry"/>) keeps its suspended goals in
    /// <c>VariableEntry.Suspensions</c>, drained ONLY by <see cref="BindImportedReader"/>;
    /// a local writer binds via <see cref="BindVariable"/>. The madGLP assignment ingress
    /// (<c>HandleMadAssignment</c>) MUST route value arrivals through this single seam so a
    /// guard suspended on an imported reader reactivates exactly once when its value arrives
    /// (FR-051), instead of staying permanently un-woken because BindVariable never touches
    /// VariableEntry.Suspensions. Only an *unbound* imported reader (content is a
    /// VariableEntry) is routed here; every other address takes the existing path.
    /// </summary>
    public List<GoalRef> BindAny(int addr, Term value)
    {
        if (addr >= 0 && addr < Cells.Count)
        {
            var cell = Cells[addr];
            if (cell.Tag == HeapCellTag.RoTag && cell.Content is VariableEntry entry)
            {
                // Unbound imported reader → drain its VariableEntry.Suspensions.
                return BindImportedReader(addr, value, entry);
            }
        }
        // Local writer (or any non-imported-reader address) → existing path.
        return BindVariable(addr, value);
    }

    /// <summary>Bind variable to constant (compatibility wrapper).</summary>
    public List<GoalRef> BindVariableConst(int writerAddr, object? v) =>
        BindWriter(writerAddr, new ConstTerm(v));

    /// <summary>Bind variable to structure (compatibility wrapper).</summary>
    public List<GoalRef> BindVariableStruct(int writerAddr, string functor, List<Term> args) =>
        BindWriter(writerAddr, new StructTerm(functor, args));

    /// <summary>Compatibility: IsWriterBound.</summary>
    public bool IsWriterBound(int writerAddr) => IsFullyBound(writerAddr);

    /// <summary>Compatibility: ValueOfWriter.</summary>
    public Term? ValueOfWriter(int writerAddr) => GetValue(writerAddr);

    /// <summary>Compatibility: BindWriterConst.</summary>
    public List<GoalRef> BindWriterConst(int writerAddr, object? v) => BindVariableConst(writerAddr, v);

    /// <summary>Compatibility: BindWriterStruct.</summary>
    public List<GoalRef> BindWriterStruct(int writerAddr, string f, List<Term> args) =>
        BindVariableStruct(writerAddr, f, args);

    /// <summary>Compatibility: IsBound.</summary>
    public bool IsBound(int varId) => IsFullyBound(varId);

    // ==========================================================================
    // Reader abstraction methods (work for local AND imported readers)
    // ==========================================================================

    /// <summary>
    /// Check if a reader is bound (local or imported).
    /// </summary>
    /// <remarks>
    /// For local readers: checks if paired writer is fully bound.
    /// For imported readers: checks if cell content is Pointer (bound by BindImportedReader).
    /// </remarks>
    public bool IsReaderBound(int readerAddr)
    {
        var cell = Cells[readerAddr];
        if (cell.Tag != HeapCellTag.RoTag) return false;

        if (cell.Content is Pointer ptr)
        {
            var targetCell = Cells[ptr.TargetAddr];
            if (targetCell.Tag == HeapCellTag.WrtTag)
            {
                // Local reader - check if writer is fully bound
                return IsFullyBound(ptr.TargetAddr);
            }
            else if (targetCell.Tag == HeapCellTag.ValueTag)
            {
                // Imported reader, bound via BindImportedReader
                return true;
            }
        }
        // VariableEntry = unbound imported reader
        return false;
    }

    /// <summary>
    /// Get value for a bound reader (local or imported). Returns null if reader is unbound.
    /// </summary>
    public Term? GetReaderValue(int readerAddr)
    {
        var cell = Cells[readerAddr];
        if (cell.Tag != HeapCellTag.RoTag) return null;

        if (cell.Content is Pointer ptr)
        {
            var targetCell = Cells[ptr.TargetAddr];
            if (targetCell.Tag == HeapCellTag.WrtTag)
            {
                // Local reader - get writer value
                return GetValue(ptr.TargetAddr);
            }
            else if (targetCell.Tag == HeapCellTag.ValueTag)
            {
                // Imported reader, bound via BindImportedReader - value is in the cell
                return (Term)targetCell.Content!;
            }
        }
        return null;
    }

    /// <summary>
    /// Check if reader is an imported reader (no local writer).
    /// </summary>
    /// <remarks>
    /// Returns true for both bound and unbound imported readers, identified by
    /// cell structure rather than V_p presence. This is intentional.
    /// <para>
    /// <strong>Semantics of "imported reader":</strong>
    /// An imported reader is one that was received from another agent (creator != self).
    /// The heap structure permanently marks this:
    /// <list type="bullet">
    /// <item>Unbound imported reader: cell.Content is VariableEntry (suspensions stored here)</item>
    /// <item>Bound imported reader: cell.Content is Pointer pointing to ValueTag cell</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Contrast with local readers:</strong>
    /// Local readers have cell.Content as Pointer pointing to WrtTag cell (the paired writer).
    /// </para>
    /// <para>
    /// <strong>Why this matters:</strong>
    /// After BindImportedReader(), the VariableEntry is removed from V_p, but the
    /// heap structure (Pointer to ValueTag) still identifies it as imported. This
    /// allows DerefAddr() to correctly retrieve the bound value without needing V_p lookup.
    /// </para>
    /// <para>
    /// <strong>Cell structure summary:</strong>
    /// <list type="table">
    /// <listheader><term>State</term><term>cell.Content</term><term>Target cell</term></listheader>
    /// <item><term>Unbound imported</term><term>VariableEntry</term><term>N/A</term></item>
    /// <item><term>Bound imported</term><term>Pointer</term><term>ValueTag</term></item>
    /// <item><term>Local (any)</term><term>Pointer</term><term>WrtTag (writer)</term></item>
    /// </list>
    /// </para>
    /// </remarks>
    public bool IsImportedReader(int readerAddr)
    {
        var cell = Cells[readerAddr];
        if (cell.Tag != HeapCellTag.RoTag) return false;

        if (cell.Content is VariableEntry)
        {
            // Unbound imported reader
            return true;
        }
        if (cell.Content is Pointer ptr)
        {
            // Could be local reader (points to writer) or bound imported reader (points to ValueTag)
            var targetCell = Cells[ptr.TargetAddr];
            // If target is ValueTag, it was bound via BindImportedReader
            return targetCell.Tag == HeapCellTag.ValueTag;
        }
        return false;
    }

    /// <summary>
    /// Get writer address for local reader, null for imported reader.
    /// This is the safe version — use this instead of writerForReader when
    /// the reader might be imported.
    /// </summary>
    public int? GetWriterForReader(int readerAddr) => TryWriterForReader(readerAddr);

    /// <summary>Legacy: Get suspension list (now on writer via WriterContent).</summary>
    /* Legacy: preserved for caller compatibility */
    public SuspensionListNode? GetSuspensions(int writerAddr)
    {
        var cell = Cells[writerAddr];
        if (cell.Content is WriterContent wc) return wc.Suspensions;
        return null;
    }

    /// <summary>Legacy: Add suspension (now on writer via WriterContent).</summary>
    /* Legacy: preserved for caller compatibility */
    public void AddSuspension(int writerAddr, SuspensionListNode node)
    {
        var cell = Cells[writerAddr];
        if (cell.Content is WriterContent wc)
        {
            node.Next = wc.Suspensions;
            wc.Suspensions = node;
        }
        else if (cell.Content is Pointer ptr)
        {
            cell.Content = new WriterContent(ptr.TargetAddr, node);
        }
    }

    // ==========================================================================
    // Term Storage Helper (for Heap-Only Argument Registers per spec v2.16.3)
    // ==========================================================================

    /// <summary>
    /// Store a Term on the heap and return the cell address.
    /// </summary>
    /// <remarks>
    /// Per spec Section 1.1 (Heap-Only Requirement):
    /// All data passed through argument registers MUST be heap-allocated.
    /// Direct ConstTerm and StructTerm objects are NOT permitted in CallEnv.
    /// <para>
    /// This helper converts any Term to a heap-stored VarRef:
    /// <list type="bullet">
    /// <item>VarRef: already on heap, return the address</item>
    /// <item>ConstTerm: allocate a ValueTag cell containing the constant</item>
    /// <item>StructTerm: recursively store args, allocate ValueTag cell with VarRef args</item>
    /// </list>
    /// </para>
    /// <para>
    /// Returns the heap address suitable for use in CallEnv via VarRef(addr).
    /// The Hp++/Cells.Add pair MUST be co-located in each allocating arm (HP-cells-length sync).
    /// </para>
    /// </remarks>
    public int StoreTermOnHeap(Term term)
    {
        if (term is VarRef varRef)
        {
            // Already on heap
            return varRef.Addr;
        }

        if (term is ConstTerm constTerm)
        {
            // Allocate a ValueTag cell containing the constant
            int addr = Hp++;
            Cells.Add(new HeapCell(constTerm, HeapCellTag.ValueTag));
            return addr;
        }

        if (term is StructTerm structTerm)
        {
            // Recursively store all args on heap, creating VarRef args
            var heapArgs = new List<Term>();
            foreach (var arg in structTerm.Args)
            {
                int argAddr = StoreTermOnHeap(arg);
                heapArgs.Add(new VarRef(argAddr));
            }
            // Allocate a ValueTag cell containing the StructTerm with VarRef args
            int addr = Hp++;
            Cells.Add(new HeapCell(new StructTerm(structTerm.Functor, heapArgs), HeapCellTag.ValueTag));
            return addr;
        }

        if (term is MutualRefTerm mutualRefTerm)
        {
            // MutualRefTerm contains a writer address for circular structures
            int addr = Hp++;
            Cells.Add(new HeapCell(mutualRefTerm, HeapCellTag.ValueTag));
            return addr;
        }

        if (term is ModuleTerm moduleTerm)
        {
            // ModuleTerm wraps a compiled module binary — stored as opaque value
            int addr = Hp++;
            Cells.Add(new HeapCell(moduleTerm, HeapCellTag.ValueTag));
            return addr;
        }

        throw new ArgumentException($"Unknown term type: {term.GetType()}");
    }
}
