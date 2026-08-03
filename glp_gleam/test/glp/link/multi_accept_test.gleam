//// Multi-accept TCP listener tests (feature 064, T011).
////
//// Two concurrent dials to ONE listening address both establish (neither
//// dropped, each link independently live both directions), and a peer that
//// half-closes at establishment does not kill the accepted side's write path
//// ({exit_on_close, false} on listen — inherited by accepted sockets — and on
//// connect; FR-012/D-9). Dialers run as spawned children (the seam is
//// synchronous; tcp's connect retries until the listener binds). Distinct
//// deterministic ports per test avoid intra-run collisions (tcp_test holds
//// 34_721–34_723, primitives_test 34_741; this suite uses 34_731–34_733).

import gleam/dynamic.{type Dynamic}
import gleam/erlang/atom
import gleam/erlang/process.{type Subject}
import gleam/option.{type Option, None, Some}
import gleeunit/should
import glp/link/primitives/capability_gate
import glp/link/primitives/link_establish
import glp/link/primitives/link_registry
import glp/link/seam/link_address
import glp/link/seam/link_fault.{type LinkFaultSignal}
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import glp/link/transports/multi_accept
import glp/link/transports/tcp

/// Dial the listener, send `tag`, then await one echo frame and report it back.
fn dial_echo_client(
  addr,
  tag: BitArray,
  back: Subject(Result(Option(BitArray), LinkFaultSignal)),
) -> Nil {
  let t = tcp.new()
  case t.connect(link_scheme.tcp(), addr, link_options.default()) {
    Ok(client) -> {
      let _ = client.send(tag)
      // The client owns its socket; its recv runs here in the child.
      process.send(back, client.recv())
      client.close()
    }
    Error(_) -> process.send(back, Ok(None))
  }
}

pub fn two_concurrent_dials_both_establish_test() {
  let addr = link_address.endpoint("127.0.0.1", 34_731)
  let opts = link_options.default()
  let assert Ok(listener) =
    multi_accept.listen(link_scheme.tcp(), addr, opts)

  let back_a = process.new_subject()
  let back_b = process.new_subject()
  process.spawn(fn() { dial_echo_client(addr, <<1>>, back_a) })
  process.spawn(fn() { dial_echo_client(addr, <<2>>, back_b) })

  // Both concurrent inbound links are accepted on the one address — neither
  // dropped.
  let assert Ok(first) = multi_accept.accept(listener, 5000)
  let assert Ok(second) = multi_accept.accept(listener, 5000)

  // Each accepted link carries exactly its own client's tag (accept order is
  // nondeterministic, so the two tags form the set {1, 2}).
  let assert Ok(Some(tag_first)) = first.recv()
  let assert Ok(Some(tag_second)) = second.recv()
  case tag_first, tag_second {
    <<1>>, <<2>> | <<2>>, <<1>> -> True
    _, _ -> False
  }
  |> should.be_true

  // Echo each tag back on ITS link: per-link isolation in both directions.
  first.send(tag_first) |> should.equal(Ok(Nil))
  second.send(tag_second) |> should.equal(Ok(Nil))
  let assert Ok(got_a) = process.receive(back_a, 5000)
  let assert Ok(got_b) = process.receive(back_b, 5000)
  got_a |> should.equal(Ok(Some(<<1>>)))
  got_b |> should.equal(Ok(Some(<<2>>)))

  first.close()
  second.close()
  multi_accept.stop(listener)
}

pub fn half_close_at_establishment_keeps_write_side_test() {
  let addr = link_address.endpoint("127.0.0.1", 34_732)
  let opts = link_options.default()
  let assert Ok(listener) =
    multi_accept.listen(link_scheme.tcp(), addr, opts)

  let back = process.new_subject()
  process.spawn(fn() {
    let t = tcp.new()
    case t.connect(link_scheme.tcp(), addr, opts) {
      Ok(client) -> {
        // A pure consumer: half-close (FIN) immediately at establishment...
        client.close()
        // ...then keep reading — half-close leaves our inbound side open.
        process.send(back, client.recv())
      }
      Error(_) -> process.send(back, Ok(None))
    }
  })

  let assert Ok(server) = multi_accept.accept(listener, 5000)
  // The peer's FIN arrives as a graceful end-of-stream on the read side...
  server.recv() |> should.equal(Ok(None))
  // ...and must NOT kill the write side ({exit_on_close, false}, FR-012/D-9):
  // the frame sent AFTER observing the FIN still lands at the peer.
  server.send(<<0xC3>>) |> should.equal(Ok(Nil))
  server.close()

  let assert Ok(got) = process.receive(back, 5000)
  got |> should.equal(Ok(Some(<<0xC3>>)))
  multi_accept.stop(listener)
}

// An accepted link nobody ever took is CLOSED at stop, not leaked: the pump
// reclaims it from the broker and closes the socket, so the peer sees the FIN
// (its blocking recv ends) instead of the BEAM holding the port for the node's
// lifetime (codexreview 064 cycle-2 socket-fd-leak).
pub fn stop_closes_accepted_but_untaken_sockets_test() {
  let addr = link_address.endpoint("127.0.0.1", 34_734)
  let opts = link_options.default()
  let assert Ok(listener) = multi_accept.listen(link_scheme.tcp(), addr, opts)

  let connected = process.new_subject()
  let back = process.new_subject()
  process.spawn(fn() {
    let t = tcp.new()
    case t.connect(link_scheme.tcp(), addr, opts) {
      Ok(client) -> {
        process.send(connected, True)
        // Never taken by any consumer: this recv only ends when the listener's
        // stop releases the accepted-but-untaken socket.
        process.send(back, client.recv())
      }
      Error(_) -> process.send(connected, False)
    }
  })
  let assert Ok(True) = process.receive(connected, 5000)
  // Let the accept pump take its slice and buffer the socket.
  process.sleep(200)

  multi_accept.stop(listener)
  let assert Ok(got) = process.receive(back, 5000)
  got |> should.equal(Ok(None))
}

// The pump latches ONLY on a per-listener fault. A per-connection transient —
// `econnaborted` when a client RSTs between SYN and accept — and any reason the
// classifier does not enumerate keep the listener accepting, exactly as the
// pre-064 pump did; only fd/memory exhaustion (or a dead listen socket) ends
// accepting.
pub fn accept_error_classification_latches_only_permanent_test() {
  accept_error_kind("timeout") |> should.equal("timeout")
  accept_error_kind("closed") |> should.equal("closed")
  accept_error_kind("econnaborted") |> should.equal("transient")
  accept_error_kind("einval") |> should.equal("transient")
  accept_error_kind("enotconn") |> should.equal("transient")
  accept_error_kind("emfile") |> should.equal("fault")
  accept_error_kind("enfile") |> should.equal("fault")
  accept_error_kind("enomem") |> should.equal("fault")
}

@external(erlang, "glp_link_tcp_ffi", "tcp_accept_error_kind")
fn ffi_accept_error_kind(reason: Dynamic) -> String

fn accept_error_kind(reason: String) -> String {
  ffi_accept_error_kind(atom.to_dynamic(atom.create(reason)))
}

pub fn establish_composes_n_listener_links_test() {
  let scheme = link_scheme.tcp()
  let addr = link_address.endpoint("127.0.0.1", 34_733)
  let opts = link_options.default()
  let assert Ok(listener) = multi_accept.listen(scheme, addr, opts)
  let t = multi_accept.transport(listener)

  process.spawn(fn() {
    let d = tcp.new()
    case d.connect(scheme, addr, opts) {
      Ok(c) -> c.close()
      Error(_) -> Nil
    }
  })
  process.spawn(fn() {
    let d = tcp.new()
    case d.connect(scheme, addr, opts) {
      Ok(c) -> c.close()
      Error(_) -> Nil
    }
  })

  // The canonical establish core (T032) accepts N listener-role links over ONE
  // address through the seam adapter: distinct per-accept nonces keep the
  // registry's idempotency-at-identity rule satisfied (FR-007).
  let gates = capability_gate.new()
  let assert Ok(#(reg1, h1)) =
    link_establish.establish(
      link_registry.new(),
      gates,
      t,
      link_establish.Listener,
      scheme,
      addr,
      opts,
    )
  let assert Ok(#(reg2, h2)) =
    link_establish.establish(
      reg1,
      gates,
      t,
      link_establish.Listener,
      scheme,
      addr,
      opts,
    )
  link_registry.size(reg2) |> should.equal(2)
  should.be_false(h1.id == h2.id)
  multi_accept.stop(listener)
}
