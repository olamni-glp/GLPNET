using GlpRuntime.Link.Seam;
using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The per-engine link-layer state the <c>'_link_setup'</c> kernel operates on
/// (feature 025, T030): the scheme→leaf <see cref="TransportRegistry"/>, the
/// idempotent LinkId→<see cref="LinkHandle"/> <see cref="LinkRegistry"/>, and the
/// single <see cref="LinkPump"/> that bridges asynchronous inbound frames onto the
/// runner thread (Option B).
/// </summary>
/// <remarks>
/// One <see cref="LinkRuntime"/> per <see cref="GlpRuntimeEngine"/>. It lives in the
/// hand-authored <c>glp_link</c> package, so the dependency flows
/// <c>glp_link → out/csharp</c> only (FR-057): the engine never references the link
/// layer; the link layer is wired in at the composition root (a test, or the REPL's
/// boot path) by <see cref="LinkKernels.Install"/>, which registers the kernels and
/// hands the engine a pump to drain.
/// </remarks>
public sealed class LinkRuntime
{
    /// <summary>Scheme→transport-leaf selection (FR-013); register leaves before setup.</summary>
    public TransportRegistry Transports { get; } = new();

    /// <summary>Idempotent-at-identity LinkId→handle registry (FR-007).</summary>
    public LinkRegistry Links { get; } = new();

    /// <summary>The one inbound pump this engine's run-to-quiescence driver services (Option B).</summary>
    public LinkPump Pump { get; }

    /// <summary>
    /// Accepted-but-not-yet-adopted inbound connections, keyed by the ground
    /// <see cref="LinkId"/> carried in the request token (T033 Option A). Populated by
    /// <c>'_link_listen'</c> when it reads an in-band request frame; drained by
    /// <c>'_link_accept'</c>, which adopts the endpoint into <see cref="Links"/> via the
    /// shared establish-core. The two-step split (listen produces requests; accept
    /// establishes one) is the ratified separation of concerns.
    /// </summary>
    public Dictionary<LinkId, ILinkEndpoint> Pending { get; } = new();

    /// <summary>The engine whose heap the kernel binds and whose goal queue the pump feeds.</summary>
    public GlpRuntimeEngine Engine { get; }

    public LinkRuntime(GlpRuntimeEngine engine)
    {
        Engine = engine ?? throw new ArgumentNullException(nameof(engine));
        Pump = new LinkPump(engine);
    }
}
