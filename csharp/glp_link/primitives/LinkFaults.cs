using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The per-link fault-monitor delivery core (feature 025, T034). Faults surface as
/// ORDINARY BOUND GROUND TERMS on a per-link monitor stream over the lattice
/// <c>ok</c> / <c>closed(LinkId,Reason)</c> / <c>tempFail(LinkId,Reason)</c> /
/// <c>permFail(LinkId,Reason)</c> — read with existing guards, NEVER a fourth
/// unification verdict and NEVER a new guard outcome (FR-043). A disconnect is
/// delivered here as a bound term, so it NEVER maps to a logical Fail (FR-044); a
/// goal that does not read the stream simply stays suspended on its unbound head.
/// </summary>
/// <remarks>
/// Delivery is a plain stream-tail bind (the same idiom as the inbound <c>In</c>
/// stream and <c>mad_context</c>'s serializer-assignment): mint a fresh
/// (writer, reader) pair, cons <c>[term | freshReader]</c>, bind the current writer,
/// enqueue any reactivated goals, and advance the cursor to the fresh writer. This is
/// why a fault cannot become a fourth verdict — it is a normal bind. All mutation runs
/// on the runner thread: the kernel (<see cref="LinkMonitorKernel"/>) registers a
/// cursor and the pump (<see cref="LinkPump"/>) fans an asynchronous fault out to
/// every registered cursor.
/// </remarks>
public static class LinkFaults
{
    private const string ListCons = ".";

    /// <summary>
    /// Register <paramref name="faultWriterAddr"/> as a live monitor cursor for
    /// <paramref name="handle"/>. Used at establishment to track the <c>Faults</c>
    /// stream and by <c>'_link_monitor'</c> to add an independent observer (FR-008).
    /// </summary>
    public static void Register(LinkHandle handle, int faultWriterAddr) =>
        handle.MonitorCursors.Add(faultWriterAddr);

    /// <summary>
    /// Extend ONE monitor cursor (the entry at <paramref name="index"/> in
    /// <paramref name="cursors"/>) by one ground fault term, advancing it to the fresh
    /// writer. Reactivated goals (a reader suspended on this stream's head) are enqueued.
    /// </summary>
    public static void Extend(HeapFCP heap, GlpRuntimeEngine engine, List<int> cursors, int index, Term term)
    {
        var (freshWriter, freshReader) = heap.AllocateVariable();
        var cons = new StructTerm(ListCons, new Term[] { term, new VarRef(freshReader) });
        foreach (var act in heap.BindVariable(cursors[index], cons))
            engine.EnqueueReactivatedGoal(act);
        cursors[index] = freshWriter;
    }

    /// <summary>
    /// Fan a fault term out to EVERY monitor cursor of <paramref name="handle"/> — the
    /// establishment <c>Faults</c> stream and all <c>link_monitor</c> streams — so each
    /// independent observer sees it on its own stream (FR-008). Runner thread only.
    /// </summary>
    public static void DeliverFault(HeapFCP heap, GlpRuntimeEngine engine, LinkHandle handle, Term term)
    {
        for (int i = 0; i < handle.MonitorCursors.Count; i++)
            Extend(heap, engine, handle.MonitorCursors, i, term);
    }
}
