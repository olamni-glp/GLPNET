//// glp/link/seam/transport — the uniform per-scheme transport seam
//// (feature 050, T045).
////
//// Port of `glp_runtime/lib/link/seam/i_link_transport.dart` (mirror
//// `csharp/glp_link/seam/ILinkTransport.cs`). One implementation per protocol family
//// (loopback T048 / tcp T049 / quic-ws T055), selected by `LinkScheme` (FR-013) via
//// the transport registry (T050). It is a HOST interface, BELOW GLP — not a GLP
//// guard/kernel/directive (architecture-context.md §3) — so the Gleam mapping choices
//// (record-of-functions vtable, synchronous `Result`, no `gleam_otp`) are
//// language-mapping, not GLP-semantic, decisions (contracts/link-parity.md).
////
//// The seam is term-agnostic: it carries the byte-parity payload blob (038 TLV +
//// FrameCodec, T046) as an opaque frame and knows nothing about terms, globalize/
//// localize, or SRSW — those live above it. Bilateral invariant (FR-005): the seam
//// exposes exactly two logical ends; a broker is a transport relay UNDER one endpoint
//// pair, never a logical hub.

import gleam/list
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_address.{type LinkAddress}
import glp/link/seam/link_fault.{type LinkFaultSignal}
import glp/link/seam/link_options.{type LinkOptions}
import glp/link/seam/link_scheme.{type LinkScheme}

/// A per-scheme transport leaf.
///
/// - `supported_schemes`: the scheme family this leaf serves (FR-013); the registry
///   selects a leaf by membership here (e.g. a single WebSocket leaf could serve
///   both `ws` and `wss`).
/// - `listen`: bind this end as the transport server and await the peer
///   (`server_listener/3` / listener role); establishment role is independent of
///   data direction (FR-004). `Ok(endpoint)` = one established bilateral end.
/// - `connect`: initiate the connection to a peer (`client_connector/3` / connector
///   role). Pairing a `listen` on instance A with a `connect` on B yields one
///   established bilateral link (FR-002).
///
/// The rendezvous is bounded by `LinkOptions.connect_timeout_ms`; a failure is
/// reported as `Error(LinkFaultSignal)`, never a crash.
pub type Transport {
  Transport(
    supported_schemes: List(LinkScheme),
    listen: fn(LinkScheme, LinkAddress, LinkOptions) ->
      Result(Endpoint, LinkFaultSignal),
    connect: fn(LinkScheme, LinkAddress, LinkOptions) ->
      Result(Endpoint, LinkFaultSignal),
  )
}

/// Whether this leaf serves `scheme` — the registry's selection-by-membership test
/// (FR-013).
pub fn serves(transport: Transport, scheme: LinkScheme) -> Bool {
  list.contains(transport.supported_schemes, scheme)
}
