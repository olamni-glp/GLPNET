//// glp/link/primitives/transport_registry — scheme → transport-leaf lookup
//// (T050.C1; contracts/link-primitives-port.md §4).
////
//// Port of C# `TransportRegistry` (Dart mirror `transport_registry.dart`):
//// leaves register at the composition root; `select` picks the first leaf
//// serving the scheme (FR-013 selection-by-membership via `transport.serves`).
//// An unserved scheme is a reasoned error the kernel surfaces as an abort —
//// never a silent fallback.

import gleam/list
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/link/seam/transport.{type Transport}

pub opaque type TransportRegistry {
  TransportRegistry(leaves: List(Transport))
}

pub fn new() -> TransportRegistry {
  TransportRegistry([])
}

/// Register a transport leaf (composition-root seam; registration order is
/// selection precedence for overlapping scheme claims).
pub fn register(
  registry: TransportRegistry,
  leaf: Transport,
) -> TransportRegistry {
  TransportRegistry(list.append(registry.leaves, [leaf]))
}

/// The leaf serving `scheme`, or a reasoned error when none does.
pub fn select(
  registry: TransportRegistry,
  scheme: LinkScheme,
) -> Result(Transport, String) {
  case list.find(registry.leaves, fn(leaf) { transport.serves(leaf, scheme) }) {
    Ok(leaf) -> Ok(leaf)
    Error(_) ->
      Error(
        "no transport registered for scheme '"
        <> link_scheme.name(scheme)
        <> "'",
      )
  }
}
