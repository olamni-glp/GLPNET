namespace GlpRuntime.Link.Seam;

/// <summary>
/// The uniform per-scheme transport seam (FR-058) — the one contract both the C#
/// reference and the Dart mirror share. One implementation per protocol family
/// (loopback, file, ws/wss, http2, mqtt, coap, ble-l2cap); selected by
/// <see cref="LinkScheme"/> (FR-013). It is a HOST interface, BELOW GLP — not a
/// GLP guard/kernel/directive (architecture-context.md §3).
/// </summary>
/// <remarks>
/// The seam is term-agnostic: it carries the already-byte-parity
/// <c>PayloadSerializer</c> blob as an opaque frame and knows nothing about terms,
/// globalize/localize, or SRSW — those live above it in <c>MadContext</c> and the
/// reliability sublayer. A leaf replaces the in-process <c>IsolateManager</c>'s
/// <c>SendPort</c> routing with real bytes (architecture-context.md §1).
///
/// Bilateral invariant (FR-005): the seam exposes exactly two logical ends; a
/// broker (MQTT, server-mediated XMPP) is a transport relay UNDER one endpoint
/// pair, never a logical hub. The relay's FIFO/at-least-once is enforced
/// end-to-end by the reliability sublayer (FR-023), never assumed of the broker.
/// </remarks>
public interface ILinkTransport
{
    /// <summary>
    /// The scheme family this leaf serves (FR-013). The transport registry selects
    /// a leaf by membership in this set (e.g. a single WebSocket leaf serves both
    /// <see cref="LinkScheme.Ws"/> and <see cref="LinkScheme.Wss"/>).
    /// </summary>
    IReadOnlyCollection<LinkScheme> SupportedSchemes { get; }

    /// <summary>
    /// Bind this end as the transport server for a link and wait for the peer to
    /// connect (the <c>server_listener/3</c> / <see cref="LinkRole.Listener"/>
    /// path). Establishment role is independent of data direction (FR-004).
    /// </summary>
    /// <param name="scheme">The selected scheme (must be in <see cref="SupportedSchemes"/>).</param>
    /// <param name="local">The local address to listen on.</param>
    /// <param name="opts">Per-link tunables.</param>
    /// <param name="ct">Cancels the listen rendezvous.</param>
    /// <returns>The established bilateral endpoint.</returns>
    Task<ILinkEndpoint> ListenAsync(LinkScheme scheme, LinkAddress local, LinkOptions opts, CancellationToken ct = default);

    /// <summary>
    /// Initiate the transport connection to a peer (the
    /// <c>client_connector/3</c> / <see cref="LinkRole.Connector"/> path). Pairing
    /// a <see cref="ListenAsync"/> on instance A with a <see cref="ConnectAsync"/>
    /// on instance B yields one established bilateral link (FR-002).
    /// </summary>
    /// <param name="scheme">The selected scheme (must be in <see cref="SupportedSchemes"/>).</param>
    /// <param name="remote">The remote address to connect to.</param>
    /// <param name="opts">Per-link tunables.</param>
    /// <param name="ct">Cancels the connect rendezvous.</param>
    /// <returns>The established bilateral endpoint.</returns>
    Task<ILinkEndpoint> ConnectAsync(LinkScheme scheme, LinkAddress remote, LinkOptions opts, CancellationToken ct = default);
}
