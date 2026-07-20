//// glp/link/primitives/transport_registry — scheme → transport-leaf selection
//// (feature 050, T050.C1).
////
//// Port of `csharp/glp_link/primitives/TransportRegistry.cs` (mirror
//// `glp_runtime/lib/link/primitives/transport_registry.dart`). Pure lookup (FR-006/013):
//// no I/O, no state beyond the registered leaves.
////
//// Selection is by MEMBERSHIP (`transport.serves`), not by map key, because one leaf
//// may serve several schemes — the T045 seam models this directly
//// (`Transport.supported_schemes`), e.g. a single WebSocket leaf serving `ws` and `wss`.
//// First registered leaf that serves the scheme wins, so registration order is the
//// override order.
////
//// Available leaves today: `loopback` (T048) and `tcp` (T049). **`quic` is a reserved
//// scheme token with NO leaf** — `link_scheme.quic()` exists for wire-token parity with
//// the C# peer, but the transport is T055 and unbuilt. Asking for it yields
//// `Error(NoTransportForScheme)` rather than a silent fallback to another transport,
//// which would connect the wrong way and look like success.

import gleam/list
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/link/seam/transport.{type Transport}

/// The registered transport leaves, in registration (= precedence) order.
pub opaque type TransportRegistry {
  TransportRegistry(leaves: List(Transport))
}

/// An empty registry — no transports available.
pub fn new() -> TransportRegistry {
  TransportRegistry(leaves: [])
}

/// Register a leaf. Later registrations have LOWER precedence than earlier ones for
/// any scheme both serve.
pub fn register(
  registry: TransportRegistry,
  leaf: Transport,
) -> TransportRegistry {
  TransportRegistry(leaves: list.append(registry.leaves, [leaf]))
}

/// Why no transport could be selected.
pub type TransportError {
  /// No registered leaf serves this scheme. For `quic` this is expected today: the
  /// token is reserved, the leaf is T055 and does not exist yet.
  NoTransportForScheme(scheme: LinkScheme)
}

/// The leaf serving `scheme`, or `NoTransportForScheme`. Never falls back to another
/// transport — a wrong-transport connection would masquerade as success.
pub fn select(
  registry: TransportRegistry,
  scheme: LinkScheme,
) -> Result(Transport, TransportError) {
  case list.find(registry.leaves, fn(leaf) { transport.serves(leaf, scheme) }) {
    Ok(leaf) -> Ok(leaf)
    Error(_) -> Error(NoTransportForScheme(scheme))
  }
}
