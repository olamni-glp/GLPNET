using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Transports;

/// <summary>
/// Multi-accept helper (feature 062 US3, T019): accept N concurrent bilateral
/// clients on ONE local address, each an independent link (distinct
/// <see cref="LinkId"/>), none dropped. Honors the D-9 point-to-point base — this is
/// N separate links, never a fan-out hub. Composes over any
/// <see cref="ILinkTransport"/> whose <see cref="ILinkTransport.ListenAsync"/>
/// tolerates concurrent parks on one address (e.g. <see cref="LoopbackTransport"/>'s
/// role-aware queues). Each accept fails or succeeds independently.
/// </summary>
public static class MultiAcceptListener
{
    /// <summary>Issue <paramref name="count"/> concurrent listens on one address and
    /// resolve when all <paramref name="count"/> peers have connected.</summary>
    public static Task<ILinkEndpoint[]> AcceptManyAsync(
        ILinkTransport transport, LinkScheme scheme, LinkAddress local, LinkOptions opts,
        int count, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(transport);
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "multi-accept requires count >= 1");

        var accepts = new Task<ILinkEndpoint>[count];
        for (int i = 0; i < count; i++)
            accepts[i] = transport.ListenAsync(scheme, local, opts, ct);
        return Task.WhenAll(accepts);
    }
}
