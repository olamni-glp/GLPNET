//// glp/link/seam/link_address — host/path (+ optional port) a transport leaf opens
//// against (feature 050, T045).
////
//// Port of `glp_runtime/lib/link/seam/link_address.dart` (mirror
//// `csharp/glp_link/seam/LinkAddress.cs`). The `Endpoint ::= String ; ep(String,
//// Integer)` component of `link_id(Scheme, Endpoint, Nonce)`
//// (025 link-primitives.md §1). Structural value-equality is automatic.

import gleam/option.{type Option, None, Some}

/// - `host`: host or path — a filesystem path for `file`, a host/URL for
///   `tcp`/`quic`, a logical channel name for `loopback`.
/// - `port`: the `ep(Host, Port)` port, or `None` for the bare-string
///   `Endpoint ::= String` form.
pub type LinkAddress {
  LinkAddress(host: String, port: Option(Int))
}

/// The bare-string `Endpoint ::= String` form (path/host, no port).
pub fn path(host_or_path: String) -> LinkAddress {
  case host_or_path {
    "" -> panic as "LinkAddress requires a non-blank host/path."
    _ -> LinkAddress(host_or_path, None)
  }
}

/// The `ep(Host, Port)` form (host + port in [0, 65535]).
pub fn endpoint(host: String, port: Int) -> LinkAddress {
  case host {
    "" -> panic as "LinkAddress requires a non-blank host."
    _ ->
      case port >= 0 && port <= 65_535 {
        False -> panic as "Port must be in [0, 65535]."
        True -> LinkAddress(host, Some(port))
      }
  }
}
