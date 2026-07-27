//// glp/link/primitives/transport_registry — scheme → transport-leaf selection
//// (feature 059, T076 — port of glp_runtime/lib/link/primitives/transport_registry.dart,
//// mirror csharp/glp_link/primitives/TransportRegistry.cs).
////
//// Selects the `Transport` leaf that serves a given `LinkScheme` (FR-013). The
//// scheme is the only protocol identity above the seam (FR-006), so selection is a
//// pure scheme→leaf lookup. Transports are registered at startup (loopback always;
//// tcp / zmq / quic as each leaf lands). One registry per link runtime.
////
//// Keyed by the normalized scheme NAME string (`LinkScheme` is opaque; its name is
//// the stable equality key). Registering a scheme already served by a DIFFERENT leaf
//// is an ambiguous configuration, not a runtime condition to paper over — it is an
//// `Error` the caller surfaces.

import gleam/dict.{type Dict}
import gleam/list
import gleam/result
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/link/seam/transport.{type Transport}

/// A scheme-name → transport-leaf registry.
pub opaque type TransportRegistry {
  TransportRegistry(by_scheme: Dict(String, Transport))
}

/// An empty registry.
pub fn new() -> TransportRegistry {
  TransportRegistry(dict.new())
}

/// Register `transport` for every scheme it declares (`Transport.supported_schemes`).
/// `Error(String)` if a scheme is already served by another leaf (ambiguous config).
pub fn register(
  registry: TransportRegistry,
  transport: Transport,
) -> Result(TransportRegistry, String) {
  list.try_fold(transport.supported_schemes, registry, fn(reg, scheme) {
    let key = link_scheme.name(scheme)
    case dict.get(reg.by_scheme, key) {
      // Re-registering the SAME leaf for a scheme is idempotent; a DIFFERENT leaf
      // is the ambiguous-configuration error.
      Ok(_existing) ->
        Error("scheme '" <> key <> "' already served by another transport")
      Error(Nil) ->
        Ok(TransportRegistry(dict.insert(reg.by_scheme, key, transport)))
    }
  })
}

/// Select the transport for `scheme`; `Error(String)` if none is registered.
pub fn select(
  registry: TransportRegistry,
  scheme: LinkScheme,
) -> Result(Transport, String) {
  dict.get(registry.by_scheme, link_scheme.name(scheme))
  |> result.replace_error(
    "no transport registered for scheme '" <> link_scheme.name(scheme) <> "'",
  )
}

/// The scheme names currently served (test/inspection hook).
pub fn schemes(registry: TransportRegistry) -> List(String) {
  dict.keys(registry.by_scheme)
}
