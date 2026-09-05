using System.Net;

namespace Ynet.Transport.Link;

/// <summary>
/// The QUIC stack that backs a <see cref="IWireChannel"/>, in fallback order. Lower is preferred.
/// </summary>
/// <remarks>
/// Ruling <c>Q-glpnetshiras-38</c> places iroh's identity MODEL in L0 and the iroh STACK at L1,
/// <i>beneath</i> the link seam — so iroh is a QUIC provider here, not a peer of one. The tiers below
/// it exist because iroh is greenfield and, when it lands, will itself be a Rust runtime with a
/// per-host provisioning story; a stack whose only QUIC is iroh has no answer when iroh cannot load.
/// </remarks>
public enum QuicProviderTier
{
    /// <summary>iroh / <c>noq</c> (quinn) — the primary once it lands (Q-glpnetshiras-38, L1a/L1b).</summary>
    Iroh = 0,

    /// <summary>
    /// MsQuic via <c>System.Net.Quic</c>. Bundled inside the .NET runtime on Windows; on Linux it is a
    /// separate <c>libmsquic.so.2</c> that is <b>not in Ubuntu apt</b> and not bundled — measured absent
    /// on shiras 2026-09-04.
    /// </summary>
    MsQuic = 1,

    /// <summary>
    /// ngtcp2 + <c>ngtcp2_crypto_ossl</c> — the distro-native Linux QUIC engine, the ULTIMATE fallback.
    /// Present in the Ubuntu archive itself (<c>libngtcp2-16</c>, <c>libngtcp2-crypto-ossl0</c>), so it
    /// introduces no third-party package feed and no Rust toolchain.
    /// </summary>
    Ngtcp2 = 2,
}

/// <summary>The measured answer to "can this provider carry a link on this host, right now".</summary>
/// <param name="Supported">True only when probed, never when assumed.</param>
/// <param name="Detail">Why — named and specific, so a refusal says what to install.</param>
public readonly record struct QuicAvailability(bool Supported, string Detail)
{
    public static QuicAvailability Yes(string detail) => new(true, detail);
    public static QuicAvailability No(string detail) => new(false, detail);
    public override string ToString() => (Supported ? "available: " : "unavailable: ") + Detail;
}

/// <summary>A bound listener, owned by the provider that produced it.</summary>
public interface IQuicListenerHandle : IAsyncDisposable
{
    /// <summary>The address actually bound (port 0 resolves to the kernel's choice).</summary>
    IPEndPoint LocalEndPoint { get; }

    /// <summary>The provider that owns this listener — recorded, never inferred from configuration.</summary>
    string ProviderName { get; }

    /// <summary>Accept one peer and return its established wire channel.</summary>
    Task<IWireChannel> AcceptAsync(CancellationToken ct = default);
}

/// <summary>
/// One QUIC stack behind the YNET link seam. Providers are interchangeable at the
/// <see cref="IWireChannel"/> boundary: <see cref="YnetSession"/>'s signed-ECDH handshake is the
/// identity anchor either way (FR-002), so swapping the stack never moves the trust decision.
/// </summary>
public interface IQuicProvider
{
    /// <summary>Stable short name for logs, refusals and quorum records (e.g. <c>msquic</c>).</summary>
    string Name { get; }

    /// <summary>Fallback rank; the chain tries lower tiers first.</summary>
    QuicProviderTier Tier { get; }

    /// <summary>
    /// Measure — do not assume — whether this provider can bind and dial here. Implementations load
    /// the native library and verify its exports; they never report availability from configuration,
    /// from an environment variable, or from the fact that the managed code compiled.
    /// </summary>
    QuicAvailability Probe();

    /// <summary>Bind a listener, or throw <see cref="QuicUnavailableException"/> if unsupported.</summary>
    Task<IQuicListenerHandle> BindListenerAsync(IPEndPoint local, CancellationToken ct = default);

    /// <summary>Dial a peer, or throw <see cref="QuicUnavailableException"/> if unsupported.</summary>
    Task<IWireChannel> ConnectAsync(IPEndPoint remote, CancellationToken ct = default);
}

/// <summary>
/// Raised when no QUIC provider can serve a link. Carries every tier's measured reason, because the
/// failure this fleet keeps paying for is a capability gap that presents as health: a host whose
/// tests pass and whose service is deaf takes the PBFT margin from f=1 to f=0 with no signal.
/// </summary>
public sealed class QuicUnavailableException : PlatformNotSupportedException
{
    /// <summary>Per-provider diagnosis, in the order the chain tried them.</summary>
    public IReadOnlyList<(string Provider, QuicProviderTier Tier, QuicAvailability Availability)> Diagnoses { get; }

    public QuicUnavailableException(
        string operation,
        IReadOnlyList<(string Provider, QuicProviderTier Tier, QuicAvailability Availability)> diagnoses)
        : base(Format(operation, diagnoses))
        => Diagnoses = diagnoses;

    private static string Format(
        string operation,
        IReadOnlyList<(string Provider, QuicProviderTier Tier, QuicAvailability Availability)> diagnoses)
    {
        var lines = diagnoses.Count == 0
            ? "  (no providers registered)"
            : string.Join(Environment.NewLine,
                diagnoses.Select(d => $"  tier {(int)d.Tier} {d.Provider}: {d.Availability.Detail}"));
        return $"QUIC {operation} unavailable: no registered provider is supported on this host. "
             + $"Real QUIC only — no simulated fallback (FR-001)."
             + Environment.NewLine + lines;
    }
}
