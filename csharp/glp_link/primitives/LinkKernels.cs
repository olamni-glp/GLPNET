using GlpRuntime.Runtime;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// The composition-root entry point that wires the link layer into a
/// <see cref="GlpRuntimeEngine"/> (feature 025). Registers the host body kernels on
/// the engine's <see cref="BodyKernelRegistry"/> via the injection seam — NO edit to
/// <c>out/csharp</c>'s <c>body_kernels.cs</c>, so the dependency stays
/// <c>glp_link → out/csharp</c> (FR-057). Called by a test, or by the REPL/isolate
/// boot path, AFTER transports are registered into the returned
/// <see cref="LinkRuntime.Transports"/>.
/// </summary>
/// <remarks>
/// Body-kernel names are registered WITHOUT the surrounding quotes the GLP source
/// writes (<c>'_link_setup'</c> compiles to the label <c>_link_setup</c>, split on
/// '/' to the bare proc name at the dispatch site <c>runner.cs</c>), matching the
/// standard-kernel convention (<c>_send</c>, <c>_add</c>, …).
/// </remarks>
public static class LinkKernels
{
    /// <summary>The registered (quote-stripped) name of the link-setup kernel.</summary>
    public const string LinkSetupName = "_link_setup";

    /// <summary>The kernel arity of <c>'_link_setup'/5</c>.</summary>
    public const long LinkSetupArity = 5;

    /// <summary>The registered (quote-stripped) name of the LinkId-keyed sender kernel.</summary>
    public const string LinkSendName = "_link_send";

    /// <summary>The kernel arity of <c>'_link_send'/3</c>.</summary>
    public const long LinkSendArity = 3;

    /// <summary>Path-B handshake kernels (T033): connector / rendezvous-listener / accept.</summary>
    public const string LinkRequestName = "_link_request";
    public const long LinkRequestArity = 5;
    public const string LinkListenName = "_link_listen";
    public const long LinkListenArity = 3;
    public const string LinkAcceptName = "_link_accept";
    public const long LinkAcceptArity = 5;

    /// <summary>
    /// Build a <see cref="LinkRuntime"/> for <paramref name="engine"/> and register
    /// the link body kernels on it. Register transports into the returned runtime's
    /// <see cref="LinkRuntime.Transports"/> before the first <c>'_link_setup'</c> goal.
    /// </summary>
    public static LinkRuntime Install(GlpRuntimeEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        var link = new LinkRuntime(engine);
        Register(engine.BodyKernels, link);
        return link;
    }

    /// <summary>Register the link body kernels on an existing registry against a given runtime.</summary>
    public static void Register(BodyKernelRegistry registry, LinkRuntime link)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(link);
        registry.Register(LinkSetupName, LinkSetupArity,
            (rt, args) => LinkSetupKernel.LinkSetup(rt, args, link));
        registry.Register(LinkSendName, LinkSendArity,
            (rt, args) => LinkSendKernel.LinkSend(rt, args, link));
        registry.Register(LinkRequestName, LinkRequestArity,
            (rt, args) => LinkRequestKernel.LinkRequest(rt, args, link));
        registry.Register(LinkListenName, LinkListenArity,
            (rt, args) => LinkListenKernel.LinkListen(rt, args, link));
        registry.Register(LinkAcceptName, LinkAcceptArity,
            (rt, args) => LinkAcceptKernel.LinkAccept(rt, args, link));
    }
}
