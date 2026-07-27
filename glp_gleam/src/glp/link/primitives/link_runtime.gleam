//// glp/link/primitives/link_runtime — the per-engine link-layer state
//// (feature 059, T076 — port of glp_runtime/lib/link/primitives/link_runtime.dart,
//// mirror csharp/glp_link/primitives/LinkRuntime.cs).
////
//// The state the `_link_*` kernels operate on: the scheme→leaf `TransportRegistry`
//// (FR-013), the idempotent LinkId→handle `LinkRegistry` (FR-007), and the
//// path-B `pending` table (accepted-but-not-yet-adopted inbound connections keyed by
//// the ground `LinkId` carried in the request token — populated by `_link_listen`,
//// drained by `_link_accept`).
////
//// GLEAM MAPPING NOTE: one immutable `LinkRuntime` value threaded through the (T074)
//// link driver — the analogue of `mad_kernels.MadState` for the effectful link seam.
//// The Dart `LinkRuntime` additionally holds the async `LinkPump` + `LinkReclaimer`;
//// on BEAM the pump is a separate driver (T074 `close-link-inbound-pump`) and GC is
//// the registry `remove`, so this record is the pure state trunk both compose over.

import gleam/dict.{type Dict}
import glp/link/primitives/link_registry.{type LinkRegistry}
import glp/link/primitives/transport_registry.{type TransportRegistry}
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_id.{type LinkId}

/// The per-engine link-layer state.
pub type LinkRuntime {
  LinkRuntime(
    /// Scheme→transport-leaf selection (FR-013); register leaves before setup.
    transports: TransportRegistry,
    /// Idempotent-at-identity LinkId→handle registry (FR-007).
    links: LinkRegistry,
    /// Accepted-but-not-yet-adopted inbound connections, keyed by the ground
    /// `LinkId` in the request token (path-B: `_link_listen` populates,
    /// `_link_accept` drains).
    pending: Dict(LinkId, Endpoint),
  )
}

/// A fresh link runtime with no transports registered and no links established.
pub fn new() -> LinkRuntime {
  LinkRuntime(
    transports: transport_registry.new(),
    links: link_registry.new(),
    pending: dict.new(),
  )
}

/// Replace the transport registry (after registering a leaf).
pub fn with_transports(
  runtime: LinkRuntime,
  transports: TransportRegistry,
) -> LinkRuntime {
  LinkRuntime(..runtime, transports: transports)
}

/// Replace the link registry (the driver threads an advanced registry back).
pub fn with_links(runtime: LinkRuntime, links: LinkRegistry) -> LinkRuntime {
  LinkRuntime(..runtime, links: links)
}

/// Park an accepted inbound endpoint under the request-token LinkId (path-B).
pub fn park_pending(
  runtime: LinkRuntime,
  id: LinkId,
  endpoint: Endpoint,
) -> LinkRuntime {
  LinkRuntime(..runtime, pending: dict.insert(runtime.pending, id, endpoint))
}

/// Take (and remove) a parked inbound endpoint by LinkId (path-B accept adoption).
pub fn take_pending(
  runtime: LinkRuntime,
  id: LinkId,
) -> Result(#(LinkRuntime, Endpoint), Nil) {
  case dict.get(runtime.pending, id) {
    Ok(ep) -> Ok(#(LinkRuntime(..runtime, pending: dict.delete(runtime.pending, id)), ep))
    Error(Nil) -> Error(Nil)
  }
}
