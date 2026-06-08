/// The host/path (and optional port) a transport leaf opens against. Mirrors the
/// GLP `Endpoint ::= String ; ep(String, Integer)` component of
/// `link_id(Scheme, Endpoint, Nonce)`
/// (contracts/link-primitives.md §1, line 55).
///
/// [host]: The host or path. For `file` a filesystem path; for `ws`/`mqtt`
/// a host (or full URL path); for `loopback` a logical channel name.
///
/// [port]: The port for the `ep(Host, Port)` form, or `null` for the bare-string
/// `Endpoint ::= String` form (path/host with no port).
class LinkAddress {
  final String host;
  final int? port;

  const LinkAddress(this.host, [this.port]);

  /// The bare-string `Endpoint ::= String` form (path or host, no port).
  static LinkAddress path(String hostOrPath) {
    if (hostOrPath.trim().isEmpty) {
      throw ArgumentError.value(
          hostOrPath, 'hostOrPath', 'LinkAddress requires a non-blank host/path.');
    }
    return LinkAddress(hostOrPath, null);
  }

  /// The `ep(Host, Port)` form (host + port).
  static LinkAddress endpoint(String host, int port) {
    if (host.trim().isEmpty) {
      throw ArgumentError.value(
          host, 'host', 'LinkAddress requires a non-blank host.');
    }
    if (port < 0 || port > 65535) {
      throw RangeError.range(port, 0, 65535, 'port', 'Port must be in [0, 65535].');
    }
    return LinkAddress(host, port);
  }

  @override
  bool operator ==(Object other) =>
      other is LinkAddress && host == other.host && port == other.port;

  @override
  int get hashCode => Object.hash(host, port);

  @override
  String toString() => port != null ? '$host:$port' : host;
}
