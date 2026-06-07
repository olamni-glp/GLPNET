namespace GlpRuntime.Link.Seam;

/// <summary>
/// Per-link tunables passed to <see cref="ILinkTransport.ListenAsync"/> /
/// <see cref="ILinkTransport.ConnectAsync"/>. Every value is a tuning parameter,
/// NOT a correctness condition (architecture-context.md §5): defaults preserve the
/// GLP invariants; overrides only adjust resource bounds and timeouts.
/// </summary>
public sealed record LinkOptions
{
    /// <summary>The default for all fields.</summary>
    public static readonly LinkOptions Default = new();

    /// <summary>
    /// Bounded outbound window before the producer suspends (FR-025). Default
    /// <c>8</c>. Scheme-overridable BELOW the seam; the producer suspends (FCP
    /// suspension, not an unbounded buffer) so there is no OOM and no
    /// head-of-line blocking across independent links.
    /// </summary>
    public int BackpressureWindow { get; init; } = 8;

    /// <summary>
    /// Require transport-level encryption for inter-host links (FR-029). Default
    /// <c>true</c>: a plain inter-host link is refused (TLS/DTLS by default). A
    /// leaf MAY ignore this for an intrinsically in-process scheme
    /// (<see cref="LinkScheme.Loopback"/>).
    /// </summary>
    public bool RequireTls { get; init; } = true;

    /// <summary>
    /// Maximum transport frame size in bytes, or <c>null</c> for no leaf-imposed
    /// limit. Constrained leaves (CoAP ~1 KB, BLE GATT 20 B) set this so the
    /// framing layer (T021) fragments/reassembles under it (FR-022).
    /// </summary>
    public int? MaxFrameBytes { get; init; }

    /// <summary>
    /// Bounded silence after which the sublayer surfaces <c>tempFail</c> (FR-045,
    /// SC-010). A liveness tuning knob, not a correctness bound.
    /// </summary>
    public TimeSpan TempFailAfter { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Bounded silence after which the sublayer gives up with <c>permFail</c>
    /// (FR-045). A deliberate, possibly-wrong give-up (FLP/two-generals): bounds
    /// blast radius, does not remove the wall.
    /// </summary>
    public TimeSpan PermFailAfter { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Timeout for the initial listen/connect rendezvous.</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(15);
}
