using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The <c>'_link_monitor'/2</c> system-predicate (feature 025, T034) — the host body of
/// the GLP wrapper
/// <c>link_monitor(LinkId, Faults?) :- ground(LinkId?) | '_link_monitor'(LinkId?, Faults).</c>
/// Given a ground, already-established <see cref="LinkId"/>, it hands back a per-link
/// fault MONITOR STREAM on which faults surface as ordinary bound ground terms
/// (<c>ok</c> / <c>closed</c> / <c>tempFail</c> / <c>permFail</c>) read with existing
/// guards (FR-008/043-046). The stream is independent of the data path and of any other
/// monitor stream for the same link (FR-008): it shares only the ground <see cref="LinkId"/>
/// (a registry lookup), so there is no cell aliasing and no SRSW interference.
/// </summary>
/// <remarks>
/// Arguments (ground-guarded by the wrapper): <c>args[0]</c> LinkId, <c>args[1]</c> the
/// <c>Faults</c> writer the host extends (the program reads the paired reader hole). The
/// link MUST already be established — monitoring an unestablished link is a caller bug
/// surfaced as an abort, not tolerated (CLAUDE.md "robustness is a workaround in
/// disguise"). On registration the kernel pushes one <c>ok</c> term: the lattice's
/// healthy baseline (contracts/link-primitives.md §2.7), so a watcher's <c>[ok|Rest]</c>
/// clause is immediately reducible and the link is observably healthy until a fault.
/// </remarks>
public static class LinkMonitorKernel
{
    private const string Who = "'_link_monitor'/2";

    public static BodyKernelResult LinkMonitor(GlpRuntimeEngine rt, IReadOnlyList<object?> args, LinkRuntime link)
    {
        if (args.Count != 2)
            return LinkEstablish.Abort(Who, $"expected 2 arguments, got {args.Count}");

        var heap = rt.Heap;
        LinkId id;
        try
        {
            id = LinkTerms.ParseLinkId(heap.Dereference((Term)args[0]!));
        }
        catch (ArgumentException ex)
        {
            return LinkEstablish.Abort(Who, ex.Message);
        }

        // The Faults output hole: a body writer the host extends (program reads the
        // paired reader). Use the raw VarRef addr, not Dereference (a reader derefs to
        // its writer) — same discipline as LinkEstablish's three holes.
        if (args[1] is not VarRef faultsVr || !heap.IsWriter(faultsVr.Addr))
            return LinkEstablish.Abort(Who, "Faults must be an unbound writer cell");

        // The link must already be established (FR-007 registry): monitor observes an
        // existing link, it does not create one.
        if (!link.Links.TryGet(id, out var handle))
            return LinkEstablish.Abort(Who, $"monitor of unestablished link {id}");

        // Register the independent observer cursor, then push the healthy-baseline `ok`.
        int index = handle.MonitorCursors.Count;
        LinkFaults.Register(handle, faultsVr.Addr);
        LinkFaults.Extend(heap, rt, handle.MonitorCursors, index, LinkTerms.Ok());

        return BodyKernelResult.Success;
    }
}
