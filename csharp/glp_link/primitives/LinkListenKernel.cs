using GlpRuntime.Link.Reliability;
using GlpRuntime.Link.Seam;
using GlpRuntime.Multiagent;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The <c>'_link_listen'/3</c> body kernel (feature 025, T033) — the host body of the
/// EXPLICIT request producer
/// <c>request_listener(rendezvous(Scheme, Endpoint), Requests?) :- ground(Scheme?), ground(Endpoint?) | '_link_listen'(Scheme?, Endpoint?, Requests).</c>
/// (Option A, ratified 2026-06-07). It is the LISTENER concern, cleanly separated from
/// the ACCEPT concern (<c>'_link_accept'</c>): it binds a transport rendezvous, reads
/// the inbound connection's in-band first frame as a <c>request(LinkId, FromPeer)</c>
/// token (OQ-A3), parks that accepted connection in <see cref="LinkRuntime.Pending"/>
/// keyed by the ground LinkId, and surfaces the token on the <c>Requests</c> stream —
/// where <c>accept_link/4</c> reads it and matches the LinkId with <c>=?=</c>.
/// </summary>
/// <remarks>
/// Base-MVP: ONE request per rendezvous (the <see cref="ILinkTransport.ListenAsync"/>
/// seam returns a single endpoint; a multi-request accept-loop is a transport-leaf
/// concern, Phase 6). The kernel binds <c>Requests</c> with the single request element
/// synchronously on the runner thread — like <c>'_link_setup'</c> wires its cursors —
/// leaving the stream tail open. Args: <c>args[0]</c> Scheme, <c>args[1]</c> Endpoint,
/// <c>args[2]</c> Requests writer.
/// </remarks>
public static class LinkListenKernel
{
    private const string Who = "'_link_listen'/3";
    private const string ListCons = ".";

    public static BodyKernelResult LinkListen(GlpRuntimeEngine rt, IReadOnlyList<object?> args, LinkRuntime link)
    {
        if (args.Count != 3)
            return LinkEstablish.Abort(Who, $"expected 3 arguments, got {args.Count}");

        var heap = rt.Heap;
        LinkScheme scheme;
        LinkAddress endpointAddr;
        try
        {
            scheme = LinkTerms.ParseScheme(LinkTerms.GroundResolve(heap, (Term)args[0]!));
            endpointAddr = LinkTerms.ParseEndpoint(LinkTerms.GroundResolve(heap, (Term)args[1]!));
        }
        catch (ArgumentException ex)
        {
            return LinkEstablish.Abort(Who, ex.Message);
        }
        if (args[2] is not VarRef reqVr || !heap.IsWriter(reqVr.Addr))
            return LinkEstablish.Abort(Who, "arg 3 (Requests) must be an unbound writer cell");

        // Bind the rendezvous and read the in-band request token. Both block the runner
        // thread until a requester connects + sends (bounded by ConnectTimeout); the
        // requester runs in another engine/process, so this is not a self-deadlock.
        LinkId id;
        Term fromPeer;
        ILinkEndpoint endpoint;
        try
        {
            var transport = link.Transports.Select(scheme);
            var opts = LinkOptions.Default;
            using var cts = new CancellationTokenSource(opts.ConnectTimeout);
            endpoint = transport.ListenAsync(scheme, endpointAddr, opts, cts.Token).GetAwaiter().GetResult();
            Term token = ReadOneGroundMessage(endpoint, cts.Token);
            (id, fromPeer) = LinkTerms.ParseRequestToken(token);
        }
        // ANY rendezvous/listen/handshake failure fails CLOSED GRACEFULLY (Abort), never an uncaught
        // crash — e.g. a deferred "quic" trust-material load (LazyQuicTransport → LoadFromRepo throws
        // FileNotFoundException on missing glpquick-cert/) or a host without MsQuic
        // (PlatformNotSupportedException); neither was in the old narrow filter (codexreview
        // 20260713T111017Z P2). Same root fix as the setup/request establishment catches.
        catch (Exception ex)
        {
            return LinkEstablish.Abort(Who, $"rendezvous/handshake on {scheme}:{endpointAddr} failed: {ex.Message}");
        }

        // Park the accepted connection for '_link_accept' to adopt; surface the request.
        link.Pending[id] = endpoint;
        var requestTerm = LinkTerms.RequestToken(id, fromPeer);
        var (_, tailReader) = heap.AllocateVariable();
        var cons = new StructTerm(ListCons, new Term[] { requestTerm, new VarRef(tailReader) });
        foreach (var act in heap.BindVariable(reqVr.Addr, cons))
            rt.EnqueueReactivatedGoal(act);

        return BodyKernelResult.Success;
    }

    /// <summary>
    /// Blocking raw read of one whole ground message off <paramref name="endpoint"/>
    /// (reassembling fragments) — the out-of-band request token, consumed BEFORE the
    /// data pump engages so the data sequence still starts at 0. Throws on a non-ground
    /// payload (the base wire is ground-relay, FR-010) or peer close before a frame.
    /// </summary>
    private static Term ReadOneGroundMessage(ILinkEndpoint endpoint, CancellationToken ct)
    {
        var reassembler = new FrameReassembler();
        var deserializer = new PayloadSerializer(string.Empty);
        while (true)
        {
            byte[]? frame = endpoint.RecvBytesAsync(ct).GetAwaiter().GetResult();
            if (frame is null)
                throw new InvalidOperationException("peer closed before sending a request token");
            byte[]? payload = reassembler.Accept(FrameCodec.ParseFrame(frame));
            if (payload is null)
                continue; // awaiting more fragments
            return deserializer.DeserializeAgentMessagePayload(
                payload,
                allocateImportedVar: _ => throw new InvalidOperationException(
                    "request token carried a non-ground term (embedded variable)"));
        }
    }
}
