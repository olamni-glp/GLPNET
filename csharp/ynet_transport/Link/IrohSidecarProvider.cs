using System.Net;
using System.Net.Sockets;

namespace Ynet.Transport.Link;

/// <summary>
/// Tier 0: iroh, reached as a <b>sidecar process</b> rather than as a linked native library
/// (engineer ruling <c>Q-olg15-03</c>).
/// </summary>
/// <remarks>
/// <para>
/// The directive requires iroh "from L0 upward"; the layer gate refuses distro-dependent components
/// below L3; iroh is Rust with no .NET binding. A <b>process boundary</b> is what dissolves that:
/// this adapter is pure managed code with no native dependency, so it compiles and ships everywhere
/// L0 does, while the Rust binary is a peer process. The boundary moves from link-time to
/// process-time, and both constraints hold.
/// </para>
/// <para>
/// 🔴 <b>This adapter never fakes availability.</b> <see cref="Probe"/> measures whether the sidecar
/// control endpoint actually accepts a connection. It does not report available because an
/// environment variable is set, because the adapter compiled, or because a path exists — the
/// <see cref="IQuicProvider"/> contract forbids all three, and reporting availability from
/// configuration is the exact defect that lets a deaf host count toward a quorum.
/// </para>
/// <para>
/// <b>Known gap, disclosed rather than stubbed:</b> no Rust toolchain exists on OLAMNIT
/// (`cargo`/`rustc` absent, `~/.cargo` absent, measured 2026-09-05), so the sidecar binary is not
/// produced by this repo in this era. On such a host <see cref="Probe"/> correctly reports
/// unavailable, the chain falls to msquic, and the fallback is REPORTED (FR-008). That is the
/// designed behaviour, not a failure.
/// </para>
/// </remarks>
public sealed class IrohSidecarProvider : IQuicProvider
{
    /// <summary>Env var naming the sidecar control endpoint, e.g. <c>127.0.0.1:47899</c>.</summary>
    public const string EndpointEnvVar = "YNET_IROH_SIDECAR";

    /// <summary>Default control endpoint when the env var is unset.</summary>
    public static readonly IPEndPoint DefaultControlEndpoint = new(IPAddress.Loopback, 47899);

    /// <summary>The fleet instance, registered at tier 0 in <see cref="QuicProviderChain.Default"/>.</summary>
    public static readonly IrohSidecarProvider Instance = new();

    private readonly IPEndPoint? _explicitEndpoint;
    private readonly TimeSpan _probeTimeout;

    public IrohSidecarProvider() : this(null, TimeSpan.FromMilliseconds(250)) { }

    /// <param name="controlEndpoint">Override the control endpoint (tests supply a stub sidecar).</param>
    /// <param name="probeTimeout">How long a probe waits for the sidecar to accept.</param>
    public IrohSidecarProvider(IPEndPoint? controlEndpoint, TimeSpan probeTimeout)
    {
        _explicitEndpoint = controlEndpoint;
        _probeTimeout = probeTimeout;
    }

    public string Name => "iroh-sidecar";
    public QuicProviderTier Tier => QuicProviderTier.Iroh;

    /// <summary>The control endpoint this instance will probe — explicit, then env var, then default.</summary>
    public IPEndPoint ControlEndpoint
    {
        get
        {
            if (_explicitEndpoint is not null) return _explicitEndpoint;
            var raw = Environment.GetEnvironmentVariable(EndpointEnvVar);
            return IPEndPoint.TryParse(raw, out var parsed) ? parsed : DefaultControlEndpoint;
        }
    }

    /// <summary>
    /// Measure — by opening a TCP connection to the sidecar's control endpoint. A refusal here is
    /// the honest answer on a host with no iroh binary, and it names what is missing.
    /// </summary>
    public QuicAvailability Probe()
    {
        var ep = ControlEndpoint;
        try
        {
            using var sock = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            var connect = sock.ConnectAsync(ep);
            if (!connect.Wait(_probeTimeout))
                return QuicAvailability.No(
                    $"iroh sidecar at {ep} did not accept within {_probeTimeout.TotalMilliseconds:F0} ms");

            return sock.Connected
                ? QuicAvailability.Yes($"iroh sidecar reachable at {ep}")
                : QuicAvailability.No($"iroh sidecar at {ep} did not complete a connection");
        }
        catch (Exception ex)
        {
            return QuicAvailability.No(
                $"iroh sidecar not reachable at {ep} ({ex.GetBaseException().Message}). "
              + $"Start the sidecar, or set {EndpointEnvVar}=host:port. "
              + "Building it needs a Rust toolchain (cargo/rustc) plus the iroh crate — "
              + "measured ABSENT on OLAMNIT 2026-09-05, which is why this tier is expected to be "
              + "unavailable here and why the chain falls to msquic AND SAYS SO.");
        }
    }

    /// <summary>
    /// Not yet carried by the sidecar protocol. Throws with the measured diagnosis rather than
    /// silently handing the call to another tier — a provider that quietly delegates is how a
    /// fallback becomes invisible.
    /// </summary>
    public Task<IQuicListenerHandle> BindListenerAsync(IPEndPoint local, CancellationToken ct = default)
        => throw new QuicUnavailableException("listen", new[] { (Name, Tier, Probe()) });

    /// <inheritdoc cref="BindListenerAsync"/>
    public Task<IWireChannel> ConnectAsync(IPEndPoint remote, CancellationToken ct = default)
        => throw new QuicUnavailableException("connect", new[] { (Name, Tier, Probe()) });
}
