using System.Net;

namespace Ynet.Transport.Link;

/// <summary>
/// The ordered QUIC fallback chain: iroh → MsQuic → ngtcp2. Selects the first provider that
/// <b>measures</b> available, records which one won, and refuses loudly — naming every tier and its
/// reason — when none can serve.
/// </summary>
/// <remarks>
/// <para>
/// The chain never degrades to a non-QUIC or simulated transport (FR-001). Its whole purpose is that
/// the three tiers fail <i>independently</i>: iroh depends on a Rust runtime, MsQuic on a Microsoft
/// package feed, ngtcp2 on the distribution's own archive. A host that loses one keeps the others.
/// </para>
/// <para>
/// Selection is deliberately not cached. Provisioning changes between a probe and a bind — a library
/// dropped beside the binary, a package installed — and a cached "unsupported" would outlive the fix.
/// Callers that need a stable answer for a quorum decision should probe once at registration and
/// carry the result, which is the seam ruling: membership is asserted by an actual successful bind at
/// registration, and "listener down" reports as a QUORUM CHANGE.
/// </para>
/// </remarks>
public sealed class QuicProviderChain
{
    private readonly IReadOnlyList<IQuicProvider> _ordered;

    /// <summary>
    /// The fleet default. iroh is absent until its stack lands (Q-glpnetshiras-38 keeps it at L1); it
    /// registers itself at tier 0 and the chain order needs no edit when it does.
    /// </summary>
    public static QuicProviderChain Default { get; } =
        new(new IQuicProvider[] { MsQuicProvider.Instance, Ngtcp2Provider.Instance });

    public QuicProviderChain(IEnumerable<IQuicProvider> providers)
        => _ordered = providers.OrderBy(p => (int)p.Tier).ToList();

    /// <summary>The providers in fallback order.</summary>
    public IReadOnlyList<IQuicProvider> Providers => _ordered;

    /// <summary>
    /// Probe every provider, in order, and return each one's measured verdict. Probing does not stop
    /// at the first success: an operator needs to see what the fallbacks would do before the primary
    /// fails, not after.
    /// </summary>
    public IReadOnlyList<(string Provider, QuicProviderTier Tier, QuicAvailability Availability)> ProbeAll()
        => _ordered.Select(p => (p.Name, p.Tier, p.Probe())).ToList();

    /// <summary>
    /// The first available provider, or null with the full diagnosis when none is.
    /// </summary>
    public bool TrySelect(
        out IQuicProvider? provider,
        out IReadOnlyList<(string Provider, QuicProviderTier Tier, QuicAvailability Availability)> diagnoses)
    {
        var results = new List<(string, QuicProviderTier, QuicAvailability)>();
        foreach (var p in _ordered)
        {
            var a = p.Probe();
            results.Add((p.Name, p.Tier, a));
            if (a.Supported)
            {
                provider = p;
                diagnoses = results;
                return true;
            }
        }

        provider = null;
        diagnoses = results;
        return false;
    }

    /// <summary>The first available provider, or <see cref="QuicUnavailableException"/> naming every tier.</summary>
    public IQuicProvider Select(string operation = "link")
        => TrySelect(out var p, out var diagnoses) ? p! : throw new QuicUnavailableException(operation, diagnoses);

    /// <summary>Bind a listener on the first available provider.</summary>
    public Task<IQuicListenerHandle> BindListenerAsync(IPEndPoint local, CancellationToken ct = default)
        => Select("listen").BindListenerAsync(local, ct);

    /// <summary>Dial a peer on the first available provider.</summary>
    public Task<IWireChannel> ConnectAsync(IPEndPoint remote, CancellationToken ct = default)
        => Select("connect").ConnectAsync(remote, ct);

    /// <summary>
    /// One line per tier, for a startup log or a registration record. Print this at service start:
    /// it is the difference between a host that is known deaf and a host that is silently deaf.
    /// </summary>
    public string Describe()
        => string.Join(Environment.NewLine,
            ProbeAll().Select(d => $"quic tier {(int)d.Tier} {d.Provider}: {d.Availability}"));
}
