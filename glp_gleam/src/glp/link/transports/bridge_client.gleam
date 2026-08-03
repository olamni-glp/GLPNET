//// glp/link/transports/bridge_client — QUIC-WS mesh access via the C# bridge
//// (feature 064, T012; research D3, FR-004).
////
//// Gleam peers join a QUIC-WS mesh through the glp_quick_host mesh server's
//// Gleam-facing plain-TCP acceptor (`--role server --bridge-port <port>`): the
//// Gleam↔bridge hop is the existing tcp leaf VERBATIM (same `glp_link_tcp_ffi`
//// path, same 4-byte big-endian length-prefixed framing, same FR-012/D-9
//// socket norms — nothing re-implemented here), and the bridge↔mesh hop is the
//// C# host's QUIC-WS leg. The bridge is a RELAY, not a protocol translator:
//// frame bytes pass through the mesh router unchanged in both directions, so
//// the byte-parity FrameCodec payload rides opaquely end-to-end. At this layer
//// the bridge is indistinguishable from any TCP peer — this module is only a
//// named address wrapper over the tcp leaf's connect path.

import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_address.{type LinkAddress}
import glp/link/seam/link_fault.{
  type LinkFaultSignal, LinkFaultSignal, Permanent,
}
import glp/link/seam/link_id.{LinkId, NonceStr}
import glp/link/seam/link_options.{type LinkOptions}
import glp/link/seam/link_scheme
import glp/link/seam/transport.{type Transport, Transport}
import glp/link/transports/tcp

/// Where the mesh server's Gleam-facing acceptor listens
/// (`glp_quick_host --role server --bridge-port <port> [--bridge-addr <host>]`).
pub type Bridge {
  Bridge(host: String, port: Int)
}

/// The bridge's dial address (the tcp `ep(Host, Port)` form).
pub fn address(bridge: Bridge) -> LinkAddress {
  link_address.endpoint(bridge.host, bridge.port)
}

/// Dial the bridge directly: one established endpoint whose peer is the
/// QUIC-WS mesh. Delegates to the tcp leaf's connect untouched, so the D-9
/// dial-retry norm (retry until the acceptor binds, within
/// `opts.connect_timeout_ms`) is the existing one.
pub fn dial(
  bridge: Bridge,
  opts: LinkOptions,
) -> Result(Endpoint, LinkFaultSignal) {
  let t = tcp.new()
  t.connect(link_scheme.tcp(), address(bridge), opts)
}

/// A seam Transport whose `connect` always dials the bridge — the caller's
/// address is the mesh-facing name and is superseded by the bridge address
/// (the physical hop is Gleam↔bridge; the mesh sits behind it). `listen` is
/// refused: a bridge client joins a mesh, it never accepts inbound links.
pub fn new(bridge: Bridge) -> Transport {
  let dial_leaf = tcp.new()
  let bridge_addr = address(bridge)
  Transport(
    supported_schemes: [link_scheme.tcp()],
    listen: fn(scheme, _addr, _opts) {
      Error(LinkFaultSignal(
        LinkId(scheme, bridge_addr, NonceStr("unestablished")),
        Permanent,
        "bridge_client never listens: a bridge client joins a QUIC-WS mesh via the C# bridge acceptor",
      ))
    },
    connect: fn(scheme, _addr, opts) {
      // Only the ADDRESS is substituted; the scheme guard is the delegated tcp
      // leaf's own (a non-tcp scheme is refused there, before any dial).
      dial_leaf.connect(scheme, bridge_addr, opts)
    },
  )
}
