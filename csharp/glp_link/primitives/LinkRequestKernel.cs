using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The <c>'_link_request'/5</c> body kernel (feature 025, T033) — the host body of
/// <c>request_link(LinkId, ToPeer, ch(In?, Out), Faults?) :- ground(LinkId?), ground(ToPeer?) | '_link_request'(LinkId?, ToPeer?, In, Out?, Faults).</c>
/// (path-B establishment, FR-002). It is the CONNECTOR half of the handshake: it
/// connects to the peer's rendezvous over the transport (OQ-A3), emits the in-band
/// <c>request(LinkId, FromPeer)</c> token as a raw out-of-band frame, then establishes
/// the data link through the shared <see cref="LinkEstablish.WireEstablishedLink"/>
/// core — so the resulting link is indistinguishable from a listen/connect one (R-5).
/// </summary>
/// <remarks>
/// Arguments (ground-guarded by the wrapper): <c>args[0]</c> LinkId, <c>args[1]</c>
/// ToPeer (the peer AgentId; validated ground), <c>args[2]</c> In writer,
/// <c>args[3]</c> Out? reader, <c>args[4]</c> Faults writer. The token's
/// <c>FromPeer</c> is a base placeholder (<c>requester</c>) — authenticated origin
/// binding is FR-026/T075.
/// </remarks>
public static class LinkRequestKernel
{
    private const string Who = "'_link_request'/5";

    public static BodyKernelResult LinkRequest(GlpRuntimeEngine rt, IReadOnlyList<object?> args, LinkRuntime link)
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
            return LinkEstablish.Abort(Who, "arg 2 (ToPeer) must be a ground peer id");

        // Verify-before-act (FR-008, P1 fix 2026-07-13): gate BEFORE opening the QUIC connection or
        // shipping the request token. The gate keys only on the (already-parsed) ground LinkId, so a
        // refused requester never establishes a transport connection or exchanges the pre-data
        // handshake. WireEstablishedLink below is told preGated:true so the outcome is recorded once.
        if (LinkEstablish.CapabilityRefusal(link, id, Who) is BodyKernelResult refusal)
            return refusal;

        // Connect to the peer's rendezvous (blocks until the request_listener is there;
        // bounded by ConnectTimeout), then ship the in-band request token. The endpoint
        // is captured so the establish-core adopts the SAME connection it handshook on.
        ILinkEndpoint endpoint;
        try
        {
            var transport = link.Transports.Select(id.Scheme);
            var opts = LinkOptions.Default;
            using var cts = new CancellationTokenSource(opts.ConnectTimeout);
            endpoint = transport.ConnectAsync(id.Scheme, id.Endpoint, opts, cts.Token).GetAwaiter().GetResult();

            var token = LinkTerms.RequestToken(id, new ConstTerm("requester"));
            byte[] payload = new PayloadSerializer(string.Empty).SerializeAgentMessage(token);
            // Raw out-of-band frame (messageId 0, NOT via the link Sequencer) so the data
            // sequence still starts at 0; the listener consumes it before the data pump.
            foreach (var frame in FrameCodec.Encode(payload, messageId: 0u))
                endpoint.SendBytesAsync(frame).GetAwaiter().GetResult();
        }
        // ANY connect/handshake failure fails CLOSED GRACEFULLY (Abort), never an uncaught crash —
        // e.g. a "quic" link on a host without MsQuic throws PlatformNotSupportedException, and a
        // connect-timeout throws OperationCanceledException; neither was in the old narrow filter
        // (codexreview 20260713T110357Z P2). Same root fix as the LinkEstablish establishment catch.
        catch (Exception ex)
        {
            return LinkEstablish.Abort(Who, $"connect/handshake failed for {id}: {ex.Message}");
        }

        var result = LinkEstablish.WireEstablishedLink(
            rt, link, id, () => endpoint, args[2], args[3], args[4], Who, preGated: true);
        // 061 US4: the requester is the CONNECTOR half of the handshake — recorded for
        // snapshot section 0x09 re-establishment (contracts/snapshot-store.md).
        if (result == BodyKernelResult.Success && link.Links.TryGet(id, out var handle))
            handle.Role = LinkRole.Connector;
        return result;
    }
}
