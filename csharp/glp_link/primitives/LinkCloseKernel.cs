using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The <c>'_link_close'/2</c> system-predicate (feature 025, T035) — the host body of the
/// GLP wrappers
/// <c>link_close(LinkId) :- ground(LinkId?) | '_link_close'(LinkId?, abrupt).</c> and
/// <c>link_close(LinkId, Reason) :- ground(LinkId?), ground(Reason?) | '_link_close'(LinkId?, Reason?).</c>
/// Given a ground, already-established <see cref="LinkId"/> and a ground close reason, it
/// performs an ABRUPT teardown (RST_STREAM-equiv) regardless of stream state: it emits a
/// terminal <c>closed(LinkId, Reason)</c> on every monitor stream, ends those streams, tears
/// the transport endpoint down, and runs distributed GC (FR-024). Graceful close is the
/// separate stream-end <c>[]</c> path on <c>Out</c>; both converge on
/// <see cref="LinkClose.Teardown"/> (rulings-log "link_close — 9th base primitive").
/// </summary>
/// <remarks>
/// Arguments (ground-guarded by the wrapper): <c>args[0]</c> LinkId, <c>args[1]</c> the
/// close Reason (the atom <c>abrupt</c> from <c>/1</c>, or a user reason from <c>/2</c>;
/// graceful uses <c>eos</c>). Closing an unestablished link is a caller bug surfaced as an
/// abort, not tolerated.
/// </remarks>
public static class LinkCloseKernel
{
    private const string Who = "'_link_close'/2";

    public static BodyKernelResult LinkClose(GlpRuntimeEngine rt, IReadOnlyList<object?> args, LinkRuntime link)
    {
        if (args.Count != 2)
            return LinkEstablish.Abort(Who, $"expected 2 arguments, got {args.Count}");

        var heap = rt.Heap;
        LinkId id;
        string reason;
        try
        {
            id = LinkTerms.ParseLinkId(heap.Dereference((Term)args[0]!));
            reason = LinkTerms.ParseReason(heap.Dereference((Term)args[1]!));
        }
        catch (ArgumentException ex)
        {
            return LinkEstablish.Abort(Who, ex.Message);
        }

        return LinkTeardown.Close(rt, link, id, reason);
    }
}
