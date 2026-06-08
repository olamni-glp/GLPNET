/// The transport establishment role. Mirrors the ground GLP `Role` term that
/// `link_setup/4` dispatches on — `listener` (via
/// `server_listener/3`) and `connector` (via `client_connector/3`)
/// (contracts/link-primitives.md §2.2-2.3, lines 121-145).
///
/// Establishment role is INDEPENDENT of data direction (FR-004): the listener end
/// may be the writer end and vice versa. Which side listens is a deployment/NAT
/// concern only; both [LinkRole.listener] and [LinkRole.connector] paths
/// converge on the same established link (FR-002).
enum LinkRole {
  /// Bind this end as the transport server (`server_listener/3`).
  listener,

  /// Initiate the transport connection (`client_connector/3`).
  connector,
}
