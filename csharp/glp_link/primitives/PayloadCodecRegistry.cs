using GlpRuntime.Link.Seam;

namespace GlpRuntime.Link.Primitives;

/// <summary>
/// Selects the per-link <see cref="IPayloadCodec"/> for a <see cref="LinkScheme"/> (feature 050,
/// FR-005). Mirrors <see cref="TransportRegistry"/>: a codec is registered for a scheme at the
/// composition root (e.g. the <c>"quic"</c> crdtmsg codec, injected there so <c>glp_link</c> keeps no
/// compile-time dependency on <c>glp_crdtmsg</c> — research D-1). Any scheme with no registered codec
/// resolves to <see cref="DefaultPayloadCodec"/> (the 025 ground-relay blob), so loopback/tcp links
/// are byte-for-byte unchanged.
/// </summary>
public sealed class PayloadCodecRegistry
{
    private readonly Dictionary<LinkScheme, IPayloadCodec> _byScheme = new();

    /// <summary>Register a codec for a scheme; throws on an ambiguous re-registration to a different codec.</summary>
    public void Register(LinkScheme scheme, IPayloadCodec codec)
    {
        ArgumentNullException.ThrowIfNull(codec);
        if (_byScheme.TryGetValue(scheme, out var existing) && !ReferenceEquals(existing, codec))
            throw new InvalidOperationException(
                $"scheme '{scheme}' already has payload codec {existing.GetType().Name}");
        _byScheme[scheme] = codec;
    }

    /// <summary>Select the codec for a scheme; the default ground-relay blob if none is registered.</summary>
    public IPayloadCodec Select(LinkScheme scheme) =>
        _byScheme.TryGetValue(scheme, out var c) ? c : DefaultPayloadCodec.Instance;
}
