import 'i_link_endpoint.dart';
import 'link_address.dart';
import 'link_options.dart';
import 'link_scheme.dart';

/// The uniform per-scheme transport seam (FR-058) — the one contract both the C#
/// reference and the Dart mirror share. One implementation per protocol family
/// (loopback, file, ws/wss, http2, mqtt, coap, ble-l2cap); selected by
/// [LinkScheme] (FR-013). It is a HOST interface, BELOW GLP — not a
/// GLP guard/kernel/directive (architecture-context.md §3).
///
/// The seam is term-agnostic: it carries the already-byte-parity
/// `PayloadSerializer` blob as an opaque frame and knows nothing about terms,
/// globalize/localize, or SRSW — those live above it in `MadContext` and the
/// reliability sublayer. A leaf replaces the in-process `IsolateManager`'s
/// `SendPort` routing with real bytes (architecture-context.md §1).
///
/// Bilateral invariant (FR-005): the seam exposes exactly two logical ends; a
/// broker (MQTT, server-mediated XMPP) is a transport relay UNDER one endpoint
/// pair, never a logical hub. The relay's FIFO/at-least-once is enforced
/// end-to-end by the reliability sublayer (FR-023), never assumed of the broker.
abstract class ILinkTransport {
  /// The scheme family this leaf serves (FR-013). The transport registry selects
  /// a leaf by membership in this set (e.g. a single WebSocket leaf serves both
  /// [LinkScheme.ws] and [LinkScheme.wss]).
  List<LinkScheme> get supportedSchemes;

  /// Bind this end as the transport server for a link and wait for the peer to
  /// connect (the `server_listener/3` / [LinkRole.listener]
  /// path). Establishment role is independent of data direction (FR-004).
  ///
  /// [scheme]: The selected scheme (must be in [supportedSchemes]).
  /// [local]: The local address to listen on.
  /// [opts]: Per-link tunables.
  /// [cancel]: Cancels the listen rendezvous.
  ///
  /// Returns the established bilateral endpoint.
  Future<ILinkEndpoint> listen(
      LinkScheme scheme, LinkAddress local, LinkOptions opts,
      {Future<void>? cancel});

  /// Initiate the transport connection to a peer (the
  /// `client_connector/3` / [LinkRole.connector] path). Pairing
  /// a [listen] on instance A with a [connect]
  /// on instance B yields one established bilateral link (FR-002).
  ///
  /// [scheme]: The selected scheme (must be in [supportedSchemes]).
  /// [remote]: The remote address to connect to.
  /// [opts]: Per-link tunables.
  /// [cancel]: Cancels the connect rendezvous.
  ///
  /// Returns the established bilateral endpoint.
  Future<ILinkEndpoint> connect(
      LinkScheme scheme, LinkAddress remote, LinkOptions opts,
      {Future<void>? cancel});
}
