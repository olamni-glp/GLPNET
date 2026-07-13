using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// Selects the per-scheme <see cref="ICapabilityGate"/> (feature 050 US3, FR-008). Mirrors
/// <see cref="PayloadCodecRegistry"/>: the concrete macaroon gate is registered for <c>"quic"</c> at
/// the composition root (so <c>glp_link</c> keeps no compile-time dependency on <c>glp_crdtmsg</c>);
/// any scheme with no registered gate resolves to the allow-all <see cref="DefaultCapabilityGate"/>,
/// leaving loopback/tcp establishment unchanged.
/// </summary>
public sealed class CapabilityGateRegistry
{
    private readonly Dictionary<LinkScheme, ICapabilityGate> _byScheme = new();

    /// <summary>Register a gate for a scheme; throws on an ambiguous re-registration to a different gate.</summary>
    public void Register(LinkScheme scheme, ICapabilityGate gate)
    {
        ArgumentNullException.ThrowIfNull(gate);
        if (_byScheme.TryGetValue(scheme, out var existing) && !ReferenceEquals(existing, gate))
            throw new InvalidOperationException(
                $"scheme '{scheme}' already has capability gate {existing.GetType().Name}");
        _byScheme[scheme] = gate;
    }

    /// <summary>Select the gate for a scheme; the allow-all default if none is registered.</summary>
    public ICapabilityGate Select(LinkScheme scheme) =>
        _byScheme.TryGetValue(scheme, out var g) ? g : DefaultCapabilityGate.Instance;
}
