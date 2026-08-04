//// bridge_client tests (feature 064, T012; research D3, FR-004).
////
//// The dial helper must produce a working transport against a plain TCP
//// listener — at this layer the C# bridge acceptor is indistinguishable from a
//// TCP peer (same 4-byte big-endian length-prefixed framing). The relay
//// through the mesh router itself is exercised on the C# side
//// (glp_link.tests BridgeRelayTests) and end-to-end by the cross-runtime
//// parity scenarios (T013). Dialers run as spawned children (the seam is
//// synchronous; the tcp connect retries until the listener binds). Distinct
//// deterministic ports per test avoid intra-run collisions (tcp_test holds
//// 34_721–34_723, multi_accept_test 34_731–34_733, primitives_test 34_741;
//// this suite uses 34_751–34_753).

import gleam/erlang/process
import gleam/option.{None, Some}
import gleeunit/should
import glp/link/seam/link_address
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import glp/link/transports/bridge_client.{Bridge}
import glp/link/transports/tcp

pub fn dial_round_trips_against_plain_tcp_listener_test() {
  let bridge = Bridge("127.0.0.1", 34_751)
  let opts = link_options.default()
  let back = process.new_subject()

  process.spawn(fn() {
    case bridge_client.dial(bridge, opts) {
      Ok(link) -> {
        let _ = link.send(<<7, 7>>)
        // The dialer owns its socket; its recv runs here in the child.
        process.send(back, link.recv())
        link.close()
      }
      Error(_) -> process.send(back, Ok(None))
    }
  })

  // The "bridge": a plain tcp listener — indistinguishable at this layer.
  let t = tcp.new()
  let assert Ok(server) =
    t.listen(link_scheme.tcp(), bridge_client.address(bridge), opts)
  server.recv() |> should.equal(Ok(Some(<<7, 7>>)))
  server.send(<<8>>) |> should.equal(Ok(Nil))
  let assert Ok(got) = process.receive(back, 5000)
  got |> should.equal(Ok(Some(<<8>>)))
}

pub fn transport_connect_targets_the_bridge_not_the_given_address_test() {
  let bridge = Bridge("127.0.0.1", 34_752)
  let opts = link_options.default()
  let t = bridge_client.new(bridge)
  let back = process.new_subject()

  process.spawn(fn() {
    // The caller addresses the MESH; the physical dial goes to the bridge.
    let mesh_facing = link_address.endpoint("mesh.invalid", 1)
    case t.connect(link_scheme.tcp(), mesh_facing, opts) {
      Ok(link) -> {
        let _ = link.send(<<0xBB>>)
        process.send(back, link.recv())
        link.close()
      }
      Error(_) -> process.send(back, Ok(None))
    }
  })

  let plain = tcp.new()
  let assert Ok(server) =
    plain.listen(link_scheme.tcp(), bridge_client.address(bridge), opts)
  server.recv() |> should.equal(Ok(Some(<<0xBB>>)))
  server.send(<<0xCC>>) |> should.equal(Ok(Nil))
  let assert Ok(got) = process.receive(back, 5000)
  got |> should.equal(Ok(Some(<<0xCC>>)))
}

pub fn listen_refused_and_non_tcp_scheme_refused_test() {
  let t = bridge_client.new(Bridge("127.0.0.1", 34_753))
  let addr = link_address.endpoint("127.0.0.1", 34_753)
  // A bridge client never listens.
  let assert Error(_) = t.listen(link_scheme.tcp(), addr, link_options.default())
  // A non-tcp scheme is refused by the delegated tcp leaf's guard — fast,
  // before any dial retry.
  let assert Error(_) =
    t.connect(link_scheme.loopback(), addr, link_options.default())
}
