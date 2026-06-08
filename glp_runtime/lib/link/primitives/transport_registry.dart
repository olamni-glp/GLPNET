import '../seam/i_link_transport.dart';
import '../seam/link_scheme.dart';

/// Selects the [ILinkTransport] leaf that serves a given
/// [LinkScheme] (FR-013). One registry per instance; transports are
/// registered at startup (loopback always; ws/wss, http2, mqtt, coap, ble-l2cap as
/// each Phase-6 leaf lands). The scheme is the only protocol identity above the
/// seam (FR-006), so selection is a pure scheme→leaf lookup.
class TransportRegistry {
  final Map<LinkScheme, ILinkTransport> _byScheme =
      <LinkScheme, ILinkTransport>{};

  /// Register a transport for every scheme it declares
  /// ([ILinkTransport.supportedSchemes]). Throws if a scheme is
  /// already served by another transport (an ambiguous configuration, not a
  /// runtime condition to paper over).
  void register(ILinkTransport transport) {
    // ignore: unnecessary_null_comparison, dead_code
    if (transport == null) throw ArgumentError.notNull('transport');
    for (final scheme in transport.supportedSchemes) {
      final existing = _byScheme[scheme];
      if (existing != null && !identical(existing, transport)) {
        throw StateError(
            "scheme '$scheme' already served by ${existing.runtimeType}");
      }
      _byScheme[scheme] = transport;
    }
  }

  /// Select the transport for a scheme; throws if none is registered.
  ILinkTransport select(LinkScheme scheme) {
    final t = _byScheme[scheme];
    if (t == null) {
      throw StateError("no transport registered for scheme '$scheme'");
    }
    return t;
  }

  /// Try to select the transport for a scheme.
  ILinkTransport? trySelect(LinkScheme scheme) => _byScheme[scheme];

  /// Schemes currently served.
  Iterable<LinkScheme> get schemes => _byScheme.keys;
}
