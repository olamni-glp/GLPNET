/// The transport scheme that selects which [ILinkTransport] leaf
/// serves a link (FR-013). Mirrors the GLP `Scheme ::= String` component of
/// `LinkId ::= link_id(Scheme, Endpoint, Nonce)`
/// (contracts/link-primitives.md §1, lines 53-54).
///
/// A normalized, case-insensitive scheme token (e.g. "ws", "wss", "file",
/// "mqtt", "coap", "loopback"). Value equality so it can key the transport
/// registry. Nothing protocol-specific leaks above the seam (FR-006); the scheme
/// is the only protocol identity the language layer ever sees.
class LinkScheme {
  /// Deterministic in-memory transport for hermetic tests (T026).
  static final LinkScheme loopback = LinkScheme._('loopback');

  /// File endpoint (binary + text) — first headline-split transport (T041, FR-012).
  static final LinkScheme file = LinkScheme._('file');

  /// Raw TCP over IPv4 localhost (127.0.0.1) — the FIRST real cross-process transport
  /// (feature 025 runtime-integration; ws/wss is the next iteration). One bidirectional duplex socket.
  static final LinkScheme tcp = LinkScheme._('tcp');

  static final LinkScheme ws = LinkScheme._('ws');
  static final LinkScheme wss = LinkScheme._('wss');
  static final LinkScheme https = LinkScheme._('https');
  static final LinkScheme mqtt = LinkScheme._('mqtt');
  static final LinkScheme coap = LinkScheme._('coap');
  static final LinkScheme bleL2cap = LinkScheme._('ble-l2cap');

  /// The normalized (lower-case) scheme token. Never null/empty for a valid value.
  final String name;

  const LinkScheme._(this.name);

  /// Build a scheme from a raw GLP `Scheme` string, normalizing case so
  /// "WS" and "ws" denote the same transport. Throws on null/blank — an unbound
  /// or empty scheme is a caller bug (the GLP wrapper guards `ground/1`
  /// before the host ever sees it), not a value to tolerate.
  static LinkScheme of(String scheme) {
    if (scheme.trim().isEmpty) {
      throw ArgumentError.value(
          scheme, 'scheme', 'LinkScheme requires a non-blank scheme token.');
    }
    return LinkScheme._(scheme.trim().toLowerCase());
  }

  @override
  bool operator ==(Object other) => other is LinkScheme && name == other.name;

  @override
  int get hashCode => name.hashCode;

  @override
  String toString() => name;
}
