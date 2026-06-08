using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The ONE link-teardown core both close paths converge on (feature 025, T035): the
/// ABRUPT <c>'_link_close'/2</c> kernel (RST_STREAM-equiv teardown by ground LinkId,
/// regardless of stream state) and the GRACEFUL stream-end <c>[]</c> close (the program
/// binds its <c>Out</c> writer to <c>[]</c>, observed by <see cref="LinkEstablish"/>'s
/// egress drainer). Both emit a terminal <c>closed(LinkId, Reason)</c> on every monitor
/// stream, end those streams, tear the transport endpoint down, and run distributed GC —
/// the abrupt path with the caller's reason (default <c>abrupt</c>), the graceful path with
/// <c>eos</c> (rulings-log "graceful reason eos"; FR-024).
/// </summary>
/// <remarks>
/// <c>closed</c> is an INTENTIONAL terminal — distinct from the <c>tempFail</c>/<c>permFail</c>
/// faults — but it rides the same monitor-stream delivery (an ordinary bound ground term,
/// never a fourth verdict and never a logical Fail; FR-043/044). The data path is left
/// untouched: a goal suspended on <c>In</c> stays suspended after a close (it learns of the
/// teardown through the monitor stream, FR-044), so teardown never binds the <c>In</c> cursor.
/// All steps run on the runner thread — the kernel dispatch and the egress <c>OnBind</c>
/// callback are both runner-thread — so the heap binds here are safe.
/// </remarks>
public static class LinkTeardown
{
    private const string Who = "'_link_close'/2";

    /// <summary>
    /// Abrupt close by ground <see cref="LinkId"/> (the <c>'_link_close'/2</c> kernel body).
    /// Aborts on an unestablished link — close observes an existing link, it never creates one,
    /// and a repeat close after GC finds nothing (CLAUDE.md "robustness is a workaround").
    /// </summary>
    public static BodyKernelResult Close(GlpRuntimeEngine rt, LinkRuntime link, LinkId id, string reason)
    {
        if (!link.Links.TryGet(id, out var handle))
            return LinkEstablish.Abort(Who, $"close of unestablished link {id}");
        Teardown(rt, link, handle, reason);
        return BodyKernelResult.Success;
    }

    /// <summary>
    /// Tear a link down: emit the terminal <c>closed(LinkId, Reason)</c> on every monitor
    /// stream, end those streams with <c>[]</c>, close the transport endpoint, and run the
    /// distributed-GC hooks (registry entry → baseline; FR-024). Shared by the abrupt kernel
    /// and the graceful <c>[]</c> path. Idempotent at the GC layer: a second teardown for the
    /// same link reclaims nothing (the registry entry is already gone).
    /// </summary>
    public static void Teardown(GlpRuntimeEngine rt, LinkRuntime link, LinkHandle handle, string reason)
    {
        var heap = rt.Heap;

        // 1. Terminal close term on every monitor observer (establishment Faults + each
        //    link_monitor stream, FR-008), then 2. end those streams — a close is terminal.
        LinkFaults.DeliverFault(heap, rt, handle, LinkTerms.Closed(handle.Id, reason));
        LinkFaults.EndAll(heap, rt, handle);
        handle.MonitorCursors.Clear(); // streams ended; a late fan-out would extend a dead stream

        // 3. Transport teardown (abrupt = RST-equiv per leaf; graceful drains — both via the
        //    seam's CloseAsync, which is idempotent). The background recv loop ends when the
        //    peer closes or the pump is disposed.
        handle.Endpoint.CloseAsync().GetAwaiter().GetResult();

        // 4. Distributed GC (FR-024): run every registered reclamation hook so the runtime
        //    returns to its pre-link baseline (registry entry removed today). Idempotent.
        link.Reclaimer.Reclaim(handle.Id, reason);
    }
}
