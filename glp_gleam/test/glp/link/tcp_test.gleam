//// TCP transport unit smoke (feature 050 T049).
////
//// Establishes a genuine IPv4-loopback TCP link between two processes and checks
//// length-prefixed framing, both directions, and graceful close over real sockets.
//// One side is spawned (the seam's listen/connect are synchronous; connect retries
//// until listen binds — role-order independence). Distinct ports per test avoid
//// intra-run collisions. The full cross-transport / cross-runtime matrix is T056/T061.

import gleam/dynamic.{type Dynamic}
import gleam/erlang/process
import gleam/option.{None, Some}
import gleeunit/should
import glp/link/seam/link_address
import glp/link/seam/link_fault.{Permanent}
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import glp/link/transports/tcp

// A RAW peer (not the framing endpoint): it writes bytes of its own choosing,
// so a forged length prefix can be put on the wire.
@external(erlang, "glp_link_tcp_ffi", "tcp_connect")
fn ffi_connect(
  host: String,
  port: Int,
  timeout: Int,
) -> Result(tcp.Socket, Dynamic)

@external(erlang, "glp_link_tcp_ffi", "tcp_send")
fn ffi_send(sock: tcp.Socket, data: BitArray) -> Result(Nil, Dynamic)

pub fn round_trip_and_graceful_close_test() {
  let t = tcp.new()
  let addr = link_address.endpoint("127.0.0.1", 34_721)
  let opts = link_options.default()

  // Connector runs in a child: it retries until the listener below binds.
  process.spawn(fn() {
    case t.connect(link_scheme.tcp(), addr, opts) {
      Ok(client) -> {
        let _ = client.send(<<1, 2, 3>>)
        let _ = client.send(<<4, 5>>)
        client.close()
      }
      Error(_) -> Nil
    }
  })

  let assert Ok(server) = t.listen(link_scheme.tcp(), addr, opts)
  server.recv() |> should.equal(Ok(Some(<<1, 2, 3>>)))
  server.recv() |> should.equal(Ok(Some(<<4, 5>>)))
  // Peer FIN after draining → graceful end-of-stream.
  server.recv() |> should.equal(Ok(None))
}

pub fn bidirectional_test() {
  let t = tcp.new()
  let addr = link_address.endpoint("127.0.0.1", 34_722)
  let opts = link_options.default()
  let back = process.new_subject()

  process.spawn(fn() {
    case t.connect(link_scheme.tcp(), addr, opts) {
      Ok(client) -> {
        let _ = client.send(<<0xA1>>)
        // client owns its socket, so its recv runs here in the child.
        process.send(back, client.recv())
        client.close()
      }
      Error(_) -> process.send(back, Ok(None))
    }
  })

  let assert Ok(server) = t.listen(link_scheme.tcp(), addr, opts)
  server.recv() |> should.equal(Ok(Some(<<0xA1>>)))
  let assert Ok(Nil) = server.send(<<0xB2>>)
  let assert Ok(child_got) = process.receive(back, 5000)
  child_got |> should.equal(Ok(Some(<<0xB2>>)))
}

// A forged 4-byte length header must be REFUSED before the body read: an
// unsigned 32-bit prefix would otherwise command a ~4 GiB gen_tcp:recv from
// four attacker bytes. Same bound as the normative C#
// EngineServer.ReadFrameAsync (FrameCodec.MaxPayloadBytes + 1 KiB).
pub fn oversized_length_prefix_refused_before_read_test() {
  let t = tcp.new()
  let addr = link_address.endpoint("127.0.0.1", 34_724)
  let opts = link_options.default()

  process.spawn(fn() { dial_raw(34_724, <<0xFF, 0xFF, 0xFF, 0xFF>>, 3000) })

  let assert Ok(server) = t.listen(link_scheme.tcp(), addr, opts)
  let assert Error(signal) = server.recv()
  signal.kind |> should.equal(Permanent)
}

// Dial 127.0.0.1:port raw (retrying while the listener binds), write `bytes`,
// then hold the socket open so the peer's read is not ended by our FIN.
fn dial_raw(port: Int, bytes: BitArray, remaining_ms: Int) -> Nil {
  case ffi_connect("127.0.0.1", port, 100) {
    Ok(sock) -> {
      let _ = ffi_send(sock, bytes)
      process.sleep(1000)
    }
    Error(_) ->
      case remaining_ms <= 0 {
        True -> Nil
        False -> {
          process.sleep(100)
          dial_raw(port, bytes, remaining_ms - 200)
        }
      }
  }
}

pub fn empty_frame_round_trips_test() {
  let t = tcp.new()
  let addr = link_address.endpoint("127.0.0.1", 34_723)
  let opts = link_options.default()

  process.spawn(fn() {
    case t.connect(link_scheme.tcp(), addr, opts) {
      Ok(client) -> {
        let _ = client.send(<<>>)
        client.close()
      }
      Error(_) -> Nil
    }
  })

  let assert Ok(server) = t.listen(link_scheme.tcp(), addr, opts)
  // A zero-length frame is a real frame (len prefix 0), distinct from eos.
  server.recv() |> should.equal(Ok(Some(<<>>)))
  server.recv() |> should.equal(Ok(None))
}
