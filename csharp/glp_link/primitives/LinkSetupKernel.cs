using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The <c>'_link_setup'/5</c> body kernel (feature 025, T030) — the host body of the
/// GLP wrapper
/// <c>link_setup(LinkId, Role, ch(In?, Out), Faults?) :- ground(LinkId?), ground(Role?) | '_link_setup'(LinkId?, Role?, In, Out?, Faults).</c>
/// It establishes-or-reuses a transport leaf for a ground <see cref="LinkId"/> in a
/// given <see cref="LinkRole"/> (listen/connect, FR-002 path A) and converges on the
/// shared <see cref="LinkEstablish.WireEstablishedLink"/> core — the same core the
/// path-B handshake (<c>'_link_request'</c>/<c>'_link_accept'</c>) uses, so both paths
/// yield an indistinguishable established link (FR-001/002/003/004/007).
/// </summary>
/// <remarks>
/// Arguments (already ground-guarded by the GLP wrapper): <c>args[0]</c> LinkId,
/// <c>args[1]</c> Role (<c>listener</c>|<c>connector</c>), <c>args[2]</c> In writer,
/// <c>args[3]</c> Out? reader, <c>args[4]</c> Faults writer.
/// </remarks>
public static class LinkSetupKernel
{
    private const string Who = "'_link_setup'/5";

    public static BodyKernelResult LinkSetup(GlpRuntimeEngine rt, IReadOnlyList<object?> args, LinkRuntime link)
    {
        if (args.Count != 5)
            return LinkEstablish.Abort(Who, $"expected 5 arguments, got {args.Count}");

        var heap = rt.Heap;
        LinkId id;
        LinkRole role;
        try
        {
            id = LinkTerms.ParseLinkId(LinkTerms.GroundResolve(heap, (Term)args[0]!));
            role = LinkTerms.ParseRole(LinkTerms.GroundResolve(heap, (Term)args[1]!));
        }
        catch (ArgumentException ex)
        {
            return LinkEstablish.Abort(Who, ex.Message);
        }

        return LinkEstablish.WireEstablishedLink(
            rt, link, id, () => Establish(link, id, role),
            args[2], args[3], args[4], Who);
    }

    /// <summary>Open the transport leaf for <paramref name="id"/> in <paramref name="role"/> and build the endpoint.</summary>
    private static ILinkEndpoint Establish(LinkRuntime link, LinkId id, LinkRole role)
    {
        var transport = link.Transports.Select(id.Scheme);
        var opts = LinkOptions.Default;
        // The establishment rendezvous blocks the runner thread until the peer appears
        // (you genuinely cannot proceed until connected); the peer runs in another
        // engine/process, so this is not a self-deadlock. Cross-host rendezvous needs
        // more than the 15s LinkOptions default: listener 180s, connector 120s.
        using var cts = new CancellationTokenSource(
            role == LinkRole.Listener ? TimeSpan.FromSeconds(180) : TimeSpan.FromSeconds(120));
        var endpointTask = role == LinkRole.Listener
            ? transport.ListenAsync(id.Scheme, id.Endpoint, opts, cts.Token)
            : transport.ConnectAsync(id.Scheme, id.Endpoint, opts, cts.Token);
        return endpointTask.GetAwaiter().GetResult();
    }
}
