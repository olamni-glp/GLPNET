using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The <c>'_link_send'/3</c> body kernel (feature 025, T031) — the host body of the
/// LinkId-keyed sender face
/// <c>out_relay(Msg, LinkId, ToPeer) :- ground(Msg?), ground(LinkId?), ground(ToPeer?) | '_link_send'(Msg?, LinkId?, ToPeer?).</c>
/// (contracts/link-primitives.md §3, OQ-3 ruling). It is the LinkId-keyed twin of the
/// channel face <c>link_send/3</c> (which conses onto the <c>Out</c> stream and is
/// shipped by the <c>'_link_setup'</c> egress drainer): instead of riding the stream,
/// it resolves the link by its ground <see cref="LinkId"/> and ships the ground
/// payload directly (FR-010 ground-relay). Both faces converge on the one
/// <see cref="LinkEgress.ShipGround"/> routine (risk R-5).
/// </summary>
/// <remarks>
/// Arguments — all three already certified ground by the GLP wrapper's
/// <c>ground/1</c> guards, so a malformed/unbound term here is a caller bug
/// (surfaced as <see cref="BodyKernelResult.Abort"/>, never tolerated; FR-010):
/// <list type="number">
///   <item><c>args[0]</c> Msg — the ground term to relay (a COPY crosses the cut, FR-040).</item>
///   <item><c>args[1]</c> LinkId — ground <c>link_id(Scheme, Endpoint, Nonce)</c>; selects the established link.</item>
///   <item><c>args[2]</c> ToPeer — ground peer AgentId; the link is bilateral (FR-005) so it names the single far end (validated ground; routing is by LinkId).</item>
/// </list>
/// The link MUST already be established (a prior <c>'_link_setup'</c> / request/accept
/// for the same ground LinkId): "send before setup" is a caller bug, surfaced as Abort
/// — the base does not invent a suspend-until-established semantics the spec is silent on.
/// </remarks>
public static class LinkSendKernel
{
    public static BodyKernelResult LinkSend(GlpRuntimeEngine rt, IReadOnlyList<object?> args, LinkRuntime link)
    {
        if (args.Count != 3)
            return Abort($"expected 3 arguments, got {args.Count}");

        var heap = rt.Heap;

        // --- parse + validate the two ground addresses (LinkId, ToPeer) ---
        LinkId id;
        try
        {
            id = LinkTerms.ParseLinkId(LinkTerms.GroundResolve(heap, (Term)args[1]!));
        }
        catch (ArgumentException ex)
        {
            return Abort($"arg 2 (LinkId): {ex.Message}");
        }

        // ToPeer is ground-guarded by the wrapper; the bilateral link (FR-005) has a
        // single far end, so it is validated-ground for the relay record, not routed
        // on (routing is by LinkId). An unbound ToPeer here is a caller bug.
        if (heap.Dereference((Term)args[2]!) is VarRef)
            return Abort("arg 3 (ToPeer) must be a ground peer id");

        // --- resolve the established link (idempotent registry, FR-007) ---
        if (!link.Links.TryGet(id, out var handle))
            return Abort($"no established link for {id} — setup before send");

        // --- ground-relay ship (FR-010), shared with the Out-stream egress drainer ---
        try
        {
            LinkEgress.ShipGround(heap, handle, (Term)args[0]!);
        }
        catch (PayloadCodecException ex)
        {
            // The link's payload codec loud-failed the term (e.g. a non-crdtmsg/7 term on a "quic"
            // link) — a controlled Abort, never an uncaught crash of the runner (feature 050 P1 fix).
            return Abort($"arg 1 (Msg) rejected by the payload codec: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            // The ground(Msg?) guard should have excluded this; a non-ground payload
            // reaching the kernel is a caller bug, surfaced rather than partly shipped.
            return Abort($"arg 1 (Msg) is not ground: {ex.Message}");
        }

        return BodyKernelResult.Success;
    }

    private static BodyKernelResult Abort(string why)
    {
        Console.WriteLine($"[ABORT] '_link_send'/3: {why}");
        return BodyKernelResult.Abort;
    }
}
