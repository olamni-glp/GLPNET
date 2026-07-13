using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The <c>'_link_accept'/5</c> body kernel (feature 025, T033) — the host body of
/// <c>accept_link(LinkId, [request(LinkId2, FromPeer)|_], ch(In?, Out), Faults?) :- ground(LinkId?), LinkId? =?= LinkId2? | '_link_accept'(LinkId?, FromPeer?, In, Out?, Faults).</c>
/// It is the ACCEPT concern, cleanly separated from the LISTEN concern
/// (<c>'_link_listen'</c>): the GLP <c>=?=</c> guard has already matched the requested
/// LinkId against one this end serves, so the kernel simply ADOPTS the pending
/// connection that <c>'_link_listen'</c> parked for that ground LinkId and establishes
/// the data link through the shared <see cref="LinkEstablish.WireEstablishedLink"/>
/// core — converging on the same T030 registry as listen/connect (FR-002, R-5).
/// </summary>
/// <remarks>
/// Arguments: <c>args[0]</c> LinkId, <c>args[1]</c> FromPeer (matched origin; validated
/// ground), <c>args[2]</c> In writer, <c>args[3]</c> Out? reader, <c>args[4]</c> Faults
/// writer. "Accept before listen" (no pending connection for the LinkId) is a caller
/// bug, surfaced as Abort.
/// </remarks>
public static class LinkAcceptKernel
{
    private const string Who = "'_link_accept'/5";

    public static BodyKernelResult LinkAccept(GlpRuntimeEngine rt, IReadOnlyList<object?> args, LinkRuntime link)
    {
        if (args.Count != 5)
            return LinkEstablish.Abort(Who, $"expected 5 arguments, got {args.Count}");

        var heap = rt.Heap;
        LinkId id;
        try
        {
            id = LinkTerms.ParseLinkId(LinkTerms.GroundResolve(heap, (Term)args[0]!));
        }
        catch (ArgumentException ex)
        {
            return LinkEstablish.Abort(Who, $"arg 1 (LinkId): {ex.Message}");
        }
        if (heap.Dereference((Term)args[1]!) is VarRef)
            return LinkEstablish.Abort(Who, "arg 2 (FromPeer) must be a ground peer id");

        // Verify-before-act (FR-008, P1 fix 2026-07-13): gate BEFORE adopting the pending connection.
        // The inbound connection was already accepted by the request_listener, but a refused peer must
        // never have its link ESTABLISHED/wired — and its pending connection is disposed so it does not
        // linger half-open. WireEstablishedLink is told preGated:true (record the outcome once, SC-006).
        if (LinkEstablish.CapabilityRefusal(link, id, Who) is BodyKernelResult refusal)
        {
            if (link.Pending.Remove(id, out var refused))
                refused.DisposeAsync().AsTask().GetAwaiter().GetResult();
            return refusal;
        }

        if (!link.Pending.Remove(id, out var endpoint))
            return LinkEstablish.Abort(Who, $"no pending request for {id} — request_listener must surface it first");

        return LinkEstablish.WireEstablishedLink(
            rt, link, id, () => endpoint, args[2], args[3], args[4], Who, preGated: true);
    }
}
