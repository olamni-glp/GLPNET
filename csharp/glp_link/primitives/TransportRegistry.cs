using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// Selects the <see cref="ILinkTransport"/> leaf that serves a given
/// <see cref="LinkScheme"/> (FR-013). One registry per instance; transports are
/// registered at startup (loopback always; ws/wss, http2, mqtt, coap, ble-l2cap as
/// each Phase-6 leaf lands). The scheme is the only protocol identity above the
/// seam (FR-006), so selection is a pure scheme→leaf lookup.
/// </summary>
public sealed class TransportRegistry
{
    private readonly Dictionary<LinkScheme, ILinkTransport> _byScheme = new();

    /// <summary>
    /// Register a transport for every scheme it declares
    /// (<see cref="ILinkTransport.SupportedSchemes"/>). Throws if a scheme is
    /// already served by another transport (an ambiguous configuration, not a
    /// runtime condition to paper over).
    /// </summary>
    public void Register(ILinkTransport transport)
    {
        ArgumentNullException.ThrowIfNull(transport);
        foreach (var scheme in transport.SupportedSchemes)
        {
            if (_byScheme.TryGetValue(scheme, out var existing) && !ReferenceEquals(existing, transport))
                throw new InvalidOperationException($"scheme '{scheme}' already served by {existing.GetType().Name}");
            _byScheme[scheme] = transport;
        }
    }

    /// <summary>Select the transport for a scheme; throws if none is registered.</summary>
    public ILinkTransport Select(LinkScheme scheme) =>
        _byScheme.TryGetValue(scheme, out var t)
            ? t
            : throw new KeyNotFoundException($"no transport registered for scheme '{scheme}'");

    /// <summary>Try to select the transport for a scheme.</summary>
    public bool TrySelect(LinkScheme scheme, out ILinkTransport transport) =>
        _byScheme.TryGetValue(scheme, out transport!);

    /// <summary>Schemes currently served.</summary>
    public IReadOnlyCollection<LinkScheme> Schemes => _byScheme.Keys;
}
