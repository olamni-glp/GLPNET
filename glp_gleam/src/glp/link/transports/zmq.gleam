//// glp/link/transports/zmq — ZeroMQ (ZMTP) transport (feature 059, owner ruling
//// 2026-07-23).
////
//// ZMQ was added to the Gleam transport contract by the owner ruling of
//// 2026-07-23 (docs/research/fullscope-gleam/phase2-verify/rulings.md — the G5
//// `zmq-comm-base` out-of-scope disposition was OVERRULED; ZMQ is mandatory and
//// the contract is now {loopback, tcp, quic, zmq}). It joins loopback/tcp/quic
//// behind the T045 seam on the same all-gating transport-parity footing.
////
//// Mapping (owner-directed): erlzmq (a NIF over libzmq) via `glp_link_zmq_ffi`.
//// libzmq is a native dependency, so — exactly like Profile-C QUIC (gleam_quic
//// profile_c) — the RUNTIME is provisioned separately (WSL: see
//// glp_gleam/profile_zmq/README.md) and is not present on this Windows host. The
//// default Windows-native `gleam build` compiles this leaf regardless: the
//// `@external` refs to `glp_link_zmq_ffi` resolve to a checked-in Erlang wrapper,
//// whose calls to `erlzmq:*` are runtime-resolved (unresolved-but-compilable) so
//// the green baseline is unaffected; the leaf only *runs* where erlzmq is loaded.
////
//// Bilateral link (FR-005): one ZMQ `PAIR` socket per end — an exclusive 1-to-1
//// duplex pipe. `listen` binds, `connect` connects (ZMTP transport `tcp://H:P`);
//// establishment role is independent of data direction (FR-004). One frame per
//// ZMTP message (ZMQ frames messages itself, so no 4-byte length prefix). A
//// 1-byte tag distinguishes data (0x00) from a graceful end-of-stream (0x01) —
//// ZMTP has no TCP-style FIN, so close sends the EOS tag then closes the socket.

import gleam/dynamic.{type Dynamic}
import gleam/option.{None, Some}
import gleam/string
import glp/link/seam/endpoint.{type Endpoint, Endpoint}
import glp/link/seam/link_address.{type LinkAddress}
import glp/link/seam/link_fault.{
  type LinkFaultSignal, LinkFaultSignal, Permanent, Transient,
}
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt, NonceStr}
import glp/link/seam/link_options.{type LinkOptions}
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/link/seam/transport.{type Transport, Transport}
import gleam/erlang/process

// ---------------------------------------------------------------------------
// erlzmq FFI (glp_link_zmq_ffi.erl → erlzmq NIF; runtime-provisioned in WSL)
// ---------------------------------------------------------------------------

type ZmqSocket

@external(erlang, "glp_link_zmq_ffi", "zmq_bind")
fn ffi_bind(endpoint: String) -> Result(ZmqSocket, Dynamic)

@external(erlang, "glp_link_zmq_ffi", "zmq_connect")
fn ffi_connect(endpoint: String) -> Result(ZmqSocket, Dynamic)

@external(erlang, "glp_link_zmq_ffi", "zmq_send")
fn ffi_send(sock: ZmqSocket, data: BitArray) -> Result(Nil, Dynamic)

@external(erlang, "glp_link_zmq_ffi", "zmq_recv")
fn ffi_recv(sock: ZmqSocket, timeout: Int) -> Result(BitArray, Dynamic)

@external(erlang, "glp_link_zmq_ffi", "zmq_close")
fn ffi_close(sock: ZmqSocket) -> Nil

// ---------------------------------------------------------------------------
// Public constructor
// ---------------------------------------------------------------------------

/// A ZeroMQ (ZMTP/PAIR) transport. Stateless — each listen/connect opens its own
/// context+socket; the ZMTP endpoint string is `tcp://Host:Port`.
pub fn new() -> Transport {
  Transport(
    supported_schemes: [link_scheme.zmq()],
    listen: do_listen,
    connect: do_connect,
  )
}

fn do_listen(
  scheme: LinkScheme,
  addr: LinkAddress,
  opts: LinkOptions,
) -> Result(Endpoint, LinkFaultSignal) {
  use <- require_zmq(scheme, addr)
  use port <- require_port(scheme, addr)
  case ffi_bind(zmtp_endpoint(addr, port)) {
    Error(r) ->
      Error(fault_addr(scheme, addr, Transient, "zmq bind failed: " <> ins(r)))
    Ok(sock) ->
      // Bounded establishment (FR-004): await the connector's hello within the
      // connect budget and ack it BEFORE handing back the endpoint, so a peer that
      // never appears is a fault, not an endpoint whose recv blocks forever.
      case establish_listen(sock, opts.connect_timeout_ms) {
        Ok(Nil) ->
          Ok(make_endpoint(LinkId(link_scheme.zmq(), addr, NonceInt(port)), sock))
        Error(reason) -> {
          ffi_close(sock)
          Error(fault_addr(scheme, addr, Transient, reason))
        }
      }
  }
}

fn do_connect(
  scheme: LinkScheme,
  addr: LinkAddress,
  opts: LinkOptions,
) -> Result(Endpoint, LinkFaultSignal) {
  use <- require_zmq(scheme, addr)
  use port <- require_port(scheme, addr)
  // ZMQ connect succeeds locally even with no listener (it just queues), so
  // establishment is confirmed by a bounded application handshake, mirroring
  // tcp.gleam's bounded connect: the connector's hello is queued until the peer
  // binds (role-order independence, FR-004), and a peer that never acks within the
  // connect budget becomes a fault instead of an endpoint that blocks forever.
  case ffi_connect(zmtp_endpoint(addr, port)) {
    Error(r) ->
      Error(fault_addr(scheme, addr, Transient, "zmq connect failed: " <> ins(r)))
    Ok(sock) ->
      case establish_connect(sock, opts.connect_timeout_ms) {
        Ok(Nil) ->
          Ok(make_endpoint(LinkId(link_scheme.zmq(), addr, NonceInt(port)), sock))
        Error(reason) -> {
          ffi_close(sock)
          Error(fault_addr(scheme, addr, Transient, reason))
        }
      }
  }
}

// ---------------------------------------------------------------------------
// Establishment handshake (0x02 tag, distinct from data 0x00 / eos 0x01)
// ---------------------------------------------------------------------------
// Confirms a real bilateral PAIR link before either end is returned. Role order
// stays independent (FR-004): ZMQ queues the connector's hello until the listener
// binds. Bounded by connect_timeout_ms (default 15_000), so a never-appearing peer
// is a transport fault, not a silently-buffering endpoint.

fn establish_connect(sock: ZmqSocket, timeout_ms: Int) -> Result(Nil, String) {
  case ffi_send(sock, <<2>>) {
    Error(r) -> Error("zmq handshake hello failed: " <> ins(r))
    Ok(Nil) ->
      case ffi_recv(sock, timeout_ms) {
        Ok(<<2>>) -> Ok(Nil)
        Ok(_) -> Error("zmq handshake: unexpected frame before ack")
        Error(r) -> Error("zmq handshake: peer never acked: " <> ins(r))
      }
  }
}

fn establish_listen(sock: ZmqSocket, timeout_ms: Int) -> Result(Nil, String) {
  case ffi_recv(sock, timeout_ms) {
    Ok(<<2>>) ->
      case ffi_send(sock, <<2>>) {
        Ok(Nil) -> Ok(Nil)
        Error(r) -> Error("zmq handshake ack failed: " <> ins(r))
      }
    Ok(_) -> Error("zmq handshake: unexpected frame before hello")
    Error(r) -> Error("zmq handshake: connector never appeared: " <> ins(r))
  }
}

// ---------------------------------------------------------------------------
// Endpoint over one PAIR socket
// ---------------------------------------------------------------------------

fn make_endpoint(id: LinkId, sock: ZmqSocket) -> Endpoint {
  let faults = process.new_subject()
  Endpoint(
    id: id,
    // One frame per ZMTP message, tagged 0x00 (data). FrameCodec frames ride
    // inside opaquely; ZMQ preserves message boundaries so no length prefix.
    send: fn(frame: BitArray) {
      case ffi_send(sock, <<0, frame:bits>>) {
        Ok(Nil) -> Ok(Nil)
        Error(r) -> {
          let signal =
            LinkFaultSignal(id, Transient, "zmq send failed: " <> ins(r))
          process.send(faults, signal)
          Error(signal)
        }
      }
    },
    recv: fn() {
      case ffi_recv(sock, -1) {
        // 0x01 tag = the peer's graceful end-of-stream → closed/eos upstream.
        Ok(<<1>>) -> Ok(None)
        Ok(<<0, body:bits>>) -> Ok(Some(body))
        // Empty frame or any tag other than 0x00/0x01 is a protocol violation on
        // a link whose framing reserves only data/EOS — surface a transport fault
        // rather than a silent clean close (codex 059 finding #3).
        Ok(_) -> {
          let signal =
            LinkFaultSignal(id, Transient, "zmq: malformed frame (bad tag)")
          process.send(faults, signal)
          Error(signal)
        }
        // A recv error after peer close is a graceful end (mirrors tcp.gleam),
        // not a data fault.
        Error(_) -> Ok(None)
      }
    },
    close: fn() {
      // ZMTP has no FIN: signal EOS explicitly, then tear the socket down.
      let _ = ffi_send(sock, <<1>>)
      ffi_close(sock)
      Nil
    },
    faults: faults,
  )
}

// ---------------------------------------------------------------------------
// Guards / helpers
// ---------------------------------------------------------------------------

fn zmtp_endpoint(addr: LinkAddress, port: Int) -> String {
  "tcp://" <> addr.host <> ":" <> string.inspect(port)
}

fn require_zmq(
  scheme: LinkScheme,
  addr: LinkAddress,
  k: fn() -> Result(Endpoint, LinkFaultSignal),
) -> Result(Endpoint, LinkFaultSignal) {
  case link_scheme.name(scheme) == "zmq" {
    True -> k()
    False ->
      Error(fault_addr(
        scheme,
        addr,
        Permanent,
        "ZmqTransport does not serve scheme '" <> link_scheme.name(scheme) <> "'",
      ))
  }
}

fn require_port(
  scheme: LinkScheme,
  addr: LinkAddress,
  k: fn(Int) -> Result(Endpoint, LinkFaultSignal),
) -> Result(Endpoint, LinkFaultSignal) {
  case addr.port {
    Some(port) -> k(port)
    None ->
      Error(fault_addr(
        scheme,
        addr,
        Permanent,
        "zmq requires an ep(Host, Port) endpoint",
      ))
  }
}

fn fault_addr(
  scheme: LinkScheme,
  addr: LinkAddress,
  kind,
  reason: String,
) -> LinkFaultSignal {
  LinkFaultSignal(LinkId(scheme, addr, NonceStr("unestablished")), kind, reason)
}

fn ins(d: Dynamic) -> String {
  string.inspect(d)
}
