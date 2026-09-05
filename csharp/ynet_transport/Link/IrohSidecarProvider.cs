using System.Net;
using System.Net.Sockets;
using System.Text;

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
/// 🔴 <b>Probe measures CAPABILITY, not presence.</b> An earlier draft of this class returned
/// "available" as soon as a TCP connection to the control port succeeded. A codex review caught it,
/// and it was the same defect this feature exists to prevent, one layer up: <i>an open socket is
/// presence; it is not the ability to carry a link.</i> <see cref="IQuicProvider.Probe"/>'s contract
/// is explicitly "can this provider carry a link on this host, right now", so the probe now
/// completes a capability handshake AND requires this adapter build to actually implement link
/// carriage. Reporting availability from a bare accept was a false green in the code whose job is
/// to stop false greens.
/// </para>
/// <para>
/// <b>Known gap, disclosed rather than stubbed:</b> the sidecar wire protocol for carrying a link is
/// not implemented in this build, and no Rust toolchain exists on OLAMNIT (`cargo`/`rustc` absent,
/// measured 2026-09-05). <see cref="Probe"/> therefore reports unavailable — naming precisely which
/// of the two conditions failed — the chain falls to msquic, and the fallback is REPORTED (FR-008).
/// That is the designed behaviour, not a failure.
/// </para>
/// </remarks>
public sealed class IrohSidecarProvider : IQuicProvider
{
    /// <summary>Env var naming the sidecar control endpoint, e.g. <c>127.0.0.1:47899</c>.</summary>
    public const string EndpointEnvVar = "YNET_IROH_SIDECAR";

    /// <summary>Default control endpoint when the env var is unset.</summary>
    public static readonly IPEndPoint DefaultControlEndpoint = new(IPAddress.Loopback, 47899);

    /// <summary>The capability handshake this adapter speaks.</summary>
    public const string Protocol = "YNET-SIDECAR/1";

    /// <summary>The capability a sidecar must advertise before it can be selected to carry a link.</summary>
    public const string LinkCapability = "quic-link";

    /// <summary>The fleet instance, registered at tier 0 in <see cref="QuicProviderChain.Default"/>.</summary>
    public static readonly IrohSidecarProvider Instance = new();

    private readonly IPEndPoint? _explicitEndpoint;
    private readonly TimeSpan _probeTimeout;
    private readonly bool _carriesLinks;

    public IrohSidecarProvider() : this(null, TimeSpan.FromMilliseconds(250)) { }

    /// <param name="controlEndpoint">Override the control endpoint (tests supply a stub sidecar).</param>
    /// <param name="probeTimeout">How long a probe waits for the sidecar to answer.</param>
    /// <param name="carriesLinks">
    /// Whether THIS BUILD implements link carriage over the sidecar protocol. Currently false in
    /// production: the wire protocol is unimplemented. It is a constructor parameter rather than a
    /// hardcoded <c>false</c> so that tier-0 selection is a MEASURABLE property today (FR-004)
    /// instead of an unfalsifiable claim about a list — flip it when carriage lands.
    /// </param>
    public IrohSidecarProvider(IPEndPoint? controlEndpoint, TimeSpan probeTimeout, bool carriesLinks = false)
    {
        _explicitEndpoint = controlEndpoint;
        _probeTimeout = probeTimeout;
        _carriesLinks = carriesLinks;
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
    /// Measure. Two independent conditions must BOTH hold, and the refusal says which one failed:
    /// the sidecar must answer the capability handshake advertising <see cref="LinkCapability"/>,
    /// and this adapter build must implement link carriage.
    /// </summary>
    public QuicAvailability Probe()
    {
        var ep = ControlEndpoint;
        var handshake = TryHandshake(ep, out var caps, out var why);

        if (!handshake)
            return QuicAvailability.No(
                $"iroh sidecar not usable at {ep}: {why}. "
              + $"Start the sidecar, or set {EndpointEnvVar}=host:port. Building it needs a Rust "
              + "toolchain (cargo/rustc) plus the iroh crate — measured ABSENT on OLAMNIT "
              + "2026-09-05, which is why this tier is expected to be unavailable here and why the "
              + "chain falls to msquic AND SAYS SO.");

        if (!caps.Contains(LinkCapability, StringComparer.Ordinal))
            return QuicAvailability.No(
                $"iroh sidecar at {ep} answered {Protocol} but does not advertise "
              + $"'{LinkCapability}' (advertised: {string.Join(",", caps)})");

        if (!_carriesLinks)
            return QuicAvailability.No(
                $"iroh sidecar at {ep} advertises '{LinkCapability}', but THIS ADAPTER BUILD does "
              + "not implement link carriage over the sidecar protocol yet. Reporting available "
              + "here would be presence mistaken for capability — the provider would be selected "
              + "and then fail to bind.");

        return QuicAvailability.Yes($"iroh sidecar at {ep} speaks {Protocol} and advertises {LinkCapability}");
    }

    /// <summary>
    /// Connect, send <c>HELLO</c>, read the <c>CAPS</c> line. Returns false with a named reason
    /// rather than throwing — an absent sidecar is an expected measurement outcome, not an error.
    /// </summary>
    private bool TryHandshake(IPEndPoint ep, out string[] caps, out string why)
    {
        caps = [];
        try
        {
            using var sock = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            if (!sock.ConnectAsync(ep).Wait(_probeTimeout))
            {
                why = $"control port did not accept within {_probeTimeout.TotalMilliseconds:F0} ms";
                return false;
            }
            if (!sock.Connected) { why = "control port did not complete a connection"; return false; }

            sock.ReceiveTimeout = (int)Math.Max(1, _probeTimeout.TotalMilliseconds);
            sock.SendTimeout = sock.ReceiveTimeout;
            sock.Send(Encoding.ASCII.GetBytes($"{Protocol} HELLO\n"));

            var buf = new byte[512];
            var n = sock.Receive(buf);
            if (n <= 0) { why = $"control port accepted but answered no {Protocol} handshake"; return false; }

            var line = Encoding.ASCII.GetString(buf, 0, n).Trim();
            // Expected: "YNET-SIDECAR/1 CAPS quic-link,dht"
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3 || parts[0] != Protocol || parts[1] != "CAPS")
            {
                why = $"control port answered '{line}', which is not a {Protocol} CAPS line";
                return false;
            }

            caps = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries);
            why = "";
            return true;
        }
        catch (Exception ex)
        {
            why = ex.GetBaseException().Message;
            return false;
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
