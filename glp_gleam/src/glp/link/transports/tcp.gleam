//// glp/link/transports/tcp — raw-TCP loopback transport (feature 050, T049).
////
//// Port of `glp_runtime/lib/link/transports/tcp_transport.dart` (mirror
//// `csharp/glp_link/transports/TcpTransport.cs`) for `LinkScheme.tcp` — the first
//// real cross-process transport. The `listen` end binds an IPv4-loopback port and
//// accepts one client; the `connect` end dials, retrying while the listener is not
//// yet up (role-order independence, FR-004). The one persistent duplex socket
//// carries the bilateral link; both ends ship frames whenever they want (FR-003/005).
////
//// Wire framing (byte-identical to Dart/C#): each opaque frame is prefixed by a
//// 4-byte **big-endian** length, so one `send` ⇒ one peer `recv`. The FrameCodec
//// frame (T046) rides inside opaquely.
////
//// BEAM mapping: `gen_tcp` in PASSIVE mode ({active,false}) via `glp_link_tcp_ffi`,
//// so `recv` is a blocking exact-length read — the Dart `_SocketReader` buffer +
//// waiter is unnecessary. The seam is synchronous, so listen blocks in accept and
//// connect blocks (with retry) until the peer appears. The socket's controlling
//// process is whoever calls listen/connect; the T050 primitives layer transfers
//// control to its pump when it runs recv off-process.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/erlang/process
import gleam/int
import gleam/option.{None, Some}
import gleam/string
import glp/link/reliability/frame_codec
import glp/link/seam/endpoint.{type Endpoint, Endpoint}
import glp/link/seam/link_address.{type LinkAddress}
import glp/link/seam/link_fault.{
  type LinkFaultSignal, LinkFaultSignal, Permanent, Transient,
}
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt, NonceStr}
import glp/link/seam/link_options.{type LinkOptions}
import glp/link/seam/link_scheme.{type LinkScheme}
import glp/link/seam/transport.{type Transport, Transport}

// ---------------------------------------------------------------------------
// gen_tcp FFI (glp_link_tcp_ffi.erl)
// ---------------------------------------------------------------------------

/// The gen_tcp listen socket (opaque FFI handle). Public so multi_accept (T010)
/// can drive its own accept loop against the same FFI without re-declaring an
/// incompatible phantom type.
pub type ListenSocket

/// The gen_tcp connected socket (opaque FFI handle). Public for the same reason.
pub type Socket

@external(erlang, "glp_link_tcp_ffi", "tcp_listen")
fn ffi_listen(port: Int) -> Result(ListenSocket, Dynamic)

@external(erlang, "glp_link_tcp_ffi", "tcp_accept")
fn ffi_accept(sock: ListenSocket, timeout: Int) -> Result(Socket, Dynamic)

@external(erlang, "glp_link_tcp_ffi", "tcp_close_listener")
fn ffi_close_listener(sock: ListenSocket) -> Nil

@external(erlang, "glp_link_tcp_ffi", "tcp_connect")
fn ffi_connect(host: String, port: Int, timeout: Int) -> Result(Socket, Dynamic)

@external(erlang, "glp_link_tcp_ffi", "tcp_send")
fn ffi_send(sock: Socket, data: BitArray) -> Result(Nil, Dynamic)

@external(erlang, "glp_link_tcp_ffi", "tcp_recv")
fn ffi_recv(sock: Socket, length: Int) -> Result(BitArray, Dynamic)

@external(erlang, "glp_link_tcp_ffi", "tcp_shutdown_write")
fn ffi_shutdown_write(sock: Socket) -> Nil

// ---------------------------------------------------------------------------
// Public constructor
// ---------------------------------------------------------------------------

/// A raw-TCP loopback transport. Stateless — no shared hub (each listen/connect
/// opens its own socket, unlike loopback's in-process rendezvous).
pub fn new() -> Transport {
  Transport(
    supported_schemes: [link_scheme.tcp()],
    listen: do_listen,
    connect: do_connect,
  )
}

fn do_listen(
  scheme: LinkScheme,
  addr: LinkAddress,
  opts: LinkOptions,
) -> Result(Endpoint, LinkFaultSignal) {
  use <- require_tcp(scheme, addr)
  use port <- require_port(scheme, addr)
  case ffi_listen(port) {
    Error(r) ->
      Error(fault_addr(scheme, addr, Transient, "tcp listen failed: " <> ins(r)))
    Ok(lsock) -> {
      let accepted = ffi_accept(lsock, opts.connect_timeout_ms)
      // One link per listen (MVP): release the port for re-use.
      ffi_close_listener(lsock)
      case accepted {
        Error(r) ->
          Error(fault_addr(
            scheme,
            addr,
            Transient,
            "tcp accept failed: " <> ins(r),
          ))
        Ok(sock) ->
          Ok(make_endpoint(LinkId(link_scheme.tcp(), addr, NonceInt(port)), sock))
      }
    }
  }
}

fn do_connect(
  scheme: LinkScheme,
  addr: LinkAddress,
  opts: LinkOptions,
) -> Result(Endpoint, LinkFaultSignal) {
  use <- require_tcp(scheme, addr)
  use port <- require_port(scheme, addr)
  connect_retry(addr, port, opts.connect_timeout_ms)
}

// The connector's goal may activate before the listener has bound; retry any error
// until the connect budget is exhausted, so establishment is timing-independent
// (FR-004). Each attempt uses a short 100ms connect timeout; a refused connect
// returns near-instantly, then we back off 100ms before retrying.
fn connect_retry(
  addr: LinkAddress,
  port: Int,
  remaining_ms: Int,
) -> Result(Endpoint, LinkFaultSignal) {
  case ffi_connect(addr.host, port, 100) {
    Ok(sock) ->
      Ok(make_endpoint(LinkId(link_scheme.tcp(), addr, NonceInt(port)), sock))
    Error(r) ->
      case remaining_ms <= 0 {
        True ->
          Error(fault_addr(
            link_scheme.tcp(),
            addr,
            Transient,
            "tcp connect timed out: " <> ins(r),
          ))
        False -> {
          process.sleep(100)
          connect_retry(addr, port, remaining_ms - 200)
        }
      }
  }
}

// ---------------------------------------------------------------------------
// Endpoint over one duplex socket
// ---------------------------------------------------------------------------

/// Slack over `frame_codec.max_payload_bytes` for one inbound length prefix:
/// the prefix wraps the whole FrameCodec frame (22-byte header included), not
/// the bare payload. Same 1 KiB the normative C# `EngineServer.ReadFrameAsync`
/// allows.
const frame_header_slack = 1024

/// Wrap one connected socket as a seam endpoint carrying 4-byte big-endian
/// length-prefixed frames. Public so multi_accept (T010) reuses the exact same
/// framing endpoint for every accepted socket instead of duplicating it.
pub fn make_endpoint(id: LinkId, sock: Socket) -> Endpoint {
  let faults = process.new_subject()
  Endpoint(
    id: id,
    send: fn(frame: BitArray) {
      let len = bit_array.byte_size(frame)
      let framed = <<len:int-size(32)-big, frame:bits>>
      case ffi_send(sock, framed) {
        Ok(Nil) -> Ok(Nil)
        Error(r) -> {
          let signal =
            LinkFaultSignal(id, Transient, "tcp send failed: " <> ins(r))
          process.send(faults, signal)
          Error(signal)
        }
      }
    },
    recv: fn() {
      case ffi_recv(sock, 4) {
        // Peer FIN before a new frame → graceful close (→ closed/eos upstream).
        Error(_) -> Ok(None)
        Ok(<<len:int-size(32)-big>>) ->
          case len == 0 {
            True -> Ok(Some(<<>>))
            // Bound the allocation BEFORE trusting the header: the length is an
            // UNSIGNED 32-bit int, so 4 attacker bytes must not command a
            // multi-GB `gen_tcp:recv` (codexreview 064 cycle-2
            // unbounded-frame-allocation). Same bound as the normative C#
            // `EngineServer.ReadFrameAsync` — the framing stack's own
            // `max_payload_bytes` plus 1 KiB of slack, because the length prefix
            // wraps the whole FrameCodec frame, not the bare payload.
            False ->
              case len > frame_codec.max_payload_bytes + frame_header_slack {
                True -> {
                  let signal =
                    LinkFaultSignal(
                      id,
                      Permanent,
                      "tcp: frame length "
                        <> int.to_string(len)
                        <> " exceeds max_payload_bytes "
                        <> int.to_string(frame_codec.max_payload_bytes)
                        <> " (+1 KiB frame-header slack)",
                    )
                  process.send(faults, signal)
                  Error(signal)
                }
                False ->
                  case ffi_recv(sock, len) {
                    Ok(body) -> Ok(Some(body))
                    Error(_) -> {
                      process.send(
                        faults,
                        LinkFaultSignal(
                          id,
                          Transient,
                          "tcp: peer closed mid-frame",
                        ),
                      )
                      Ok(None)
                    }
                  }
              }
          }
        // A 4-byte read that is not 4 bytes is impossible with gen_tcp exact-recv;
        // treat any other shape as a clean end.
        Ok(_) -> Ok(None)
      }
    },
    close: fn() {
      ffi_shutdown_write(sock)
      Nil
    },
    faults: faults,
  )
}

// ---------------------------------------------------------------------------
// Guards / helpers
// ---------------------------------------------------------------------------

fn require_tcp(
  scheme: LinkScheme,
  addr: LinkAddress,
  k: fn() -> Result(Endpoint, LinkFaultSignal),
) -> Result(Endpoint, LinkFaultSignal) {
  case link_scheme.name(scheme) == "tcp" {
    True -> k()
    False ->
      Error(fault_addr(
        scheme,
        addr,
        Permanent,
        "TcpTransport does not serve scheme '" <> link_scheme.name(scheme) <> "'",
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
        "tcp requires an ep(Host, Port) endpoint",
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
