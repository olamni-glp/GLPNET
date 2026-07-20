//// Loopback transport unit smoke (feature 050 T048).
////
//// Verifies the in-process loopback rendezvous + FIFO frame exchange + graceful
//// close behind the T045 seam. Because the seam's listen/connect are synchronous,
//// one side is spawned in a child process; once both endpoints exist they are driven
//// from the test process (the ordered channels are separate processes, so a single
//// process can send on one end and recv on the other). The full cross-transport
//// round-trip matrix (loopback + TCP + QUIC-WS) is T056.

import gleam/erlang/process
import gleam/option.{None, Some}
import gleeunit/should
import glp/link/seam/link_address
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import glp/link/transports/loopback

/// Rendezvous a pair on `channel`: spawn the connector, listen in the test process,
/// and collect both established ends.
fn pair(channel: String) {
  let t = loopback.new()
  let addr = link_address.path(channel)
  let opts = link_options.default()
  let back = process.new_subject()
  process.spawn(fn() {
    let conn = t.connect(link_scheme.loopback(), addr, opts)
    process.send(back, conn)
  })
  let assert Ok(server) = t.listen(link_scheme.loopback(), addr, opts)
  let assert Ok(conn_result) = process.receive(back, 5000)
  let assert Ok(client) = conn_result
  #(server, client)
}

pub fn round_trip_both_directions_test() {
  let #(server, client) = pair("chan-rt")

  // client -> server
  let assert Ok(Nil) = client.send(<<1, 2, 3>>)
  server.recv() |> should.equal(Ok(Some(<<1, 2, 3>>)))

  // server -> client
  let assert Ok(Nil) = server.send(<<9, 8>>)
  client.recv() |> should.equal(Ok(Some(<<9, 8>>)))
}

pub fn fifo_order_preserved_test() {
  let #(server, client) = pair("chan-fifo")
  let assert Ok(Nil) = client.send(<<0xAA>>)
  let assert Ok(Nil) = client.send(<<0xBB>>)
  let assert Ok(Nil) = client.send(<<0xCC>>)
  server.recv() |> should.equal(Ok(Some(<<0xAA>>)))
  server.recv() |> should.equal(Ok(Some(<<0xBB>>)))
  server.recv() |> should.equal(Ok(Some(<<0xCC>>)))
}

pub fn graceful_close_drains_then_eos_test() {
  let #(server, client) = pair("chan-close")
  // Buffer two frames, then close: the peer drains them, then sees end-of-stream.
  let assert Ok(Nil) = client.send(<<1>>)
  let assert Ok(Nil) = client.send(<<2>>)
  client.close()
  server.recv() |> should.equal(Ok(Some(<<1>>)))
  server.recv() |> should.equal(Ok(Some(<<2>>)))
  server.recv() |> should.equal(Ok(None))
}

pub fn send_after_close_is_a_fault_test() {
  let #(_server, client) = pair("chan-sac")
  client.close()
  // Writing to a completed channel is reported as an Error (Closed fault), not a
  // silent drop — mirrors the Dart send-after-close rethrow.
  case client.send(<<1>>) {
    Error(_) -> should.be_true(True)
    Ok(_) -> should.be_true(False)
  }
}
