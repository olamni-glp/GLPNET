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
  _opts: LinkOptions,
) -> Result(Endpoint, LinkFaultSignal) {
  use <- require_zmq(scheme, addr)
  use port <- require_port(scheme, addr)
  case ffi_bind(zmtp_endpoint(addr, port)) {
    Error(r) ->
      Error(fault_addr(scheme, addr, Transient, "zmq bind failed: " <> ins(r)))
    Ok(sock) ->
      Ok(make_endpoint(LinkId(link_scheme.zmq(), addr, NonceInt(port)), sock))
  }
}

fn do_connect(
  scheme: LinkScheme,
  addr: LinkAddress,
  _opts: LinkOptions,
) -> Result(Endpoint, LinkFaultSignal) {
  use <- require_zmq(scheme, addr)
  use port <- require_port(scheme, addr)
  // ZMQ connect is asynchronous — it succeeds immediately and queues until the
  // bound peer appears (role-order independence, FR-004; no connect-retry needed).
  case ffi_connect(zmtp_endpoint(addr, port)) {
    Error(r) ->
      Error(fault_addr(scheme, addr, Transient, "zmq connect failed: " <> ins(r)))
    Ok(sock) ->
      Ok(make_endpoint(LinkId(link_scheme.zmq(), addr, NonceInt(port)), sock))
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
        // Any other shape (empty / bad tag) → treat as a clean end rather than
        // crash (fault-as-data boundary, T052, sits above this).
        Ok(_) -> Ok(None)
        // A recv error after peer close is a graceful end, not a data fault.
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
