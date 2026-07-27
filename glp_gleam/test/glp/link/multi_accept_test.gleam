//// Tests for multi-accept (feature 059, T089) — one listener/transport yields N
//// DISTINCT links, each with its OWN per-link reliability state.
////
//// The loopback transport's rendezvous hub multiplexes: repeated listen/connect pairs
//// on one transport instance yield N distinct established links (distinct LinkId
//// nonces). This is the transport-level multi-accept (an accept-loop over the seam);
//// both leaves also multi-accept by repeated establishment (`_link_setup` / path-B
//// `_link_listen`), each producing a fresh `LinkHandle` with an independent sequencer /
//// reassembler / inbound-ordering (T077).

import gleam/erlang/process
import gleeunit/should
import glp/link/primitives/link_handle
import glp/link/reliability/link_sequencer
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_address
import glp/link/seam/link_id.{LinkId, NonceInt}
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import glp/link/seam/transport.{type Transport}
import glp/link/transports/loopback

/// Rendezvous one pair on `channel` over the SHARED transport `t`, returning the
/// server (listener) endpoint.
fn accept_one(t: Transport, channel: String) -> Endpoint {
  let addr = link_address.path(channel)
  let opts = link_options.default()
  let back = process.new_subject()
  process.spawn(fn() {
    let conn = t.connect(link_scheme.loopback(), addr, opts)
    process.send(back, conn)
  })
  let assert Ok(server) = t.listen(link_scheme.loopback(), addr, opts)
  let assert Ok(_conn_result) = process.receive(back, 5000)
  server
}

pub fn one_transport_yields_two_distinct_links_test() {
  // ONE transport (one rendezvous hub) accepts TWO connections → two distinct links.
  let t = loopback.new()
  let a = accept_one(t, "ma-chan-a")
  let b = accept_one(t, "ma-chan-b")
  // Distinct LinkIds (the hub mints a fresh nonce per pairing).
  { a.id != b.id }
  |> should.be_true
}

pub fn per_link_reliability_state_is_independent_test() {
  let id1 =
    LinkId(link_scheme.tcp(), link_address.endpoint("127.0.0.1", 9100), NonceInt(1))
  let id2 =
    LinkId(link_scheme.tcp(), link_address.endpoint("127.0.0.1", 9100), NonceInt(2))
  let h1 = link_handle.new(id1, link_options.default())
  let h2 = link_handle.new(id2, link_options.default())
  // Advance link 1's outbound sequencer twice; link 2's stays fresh.
  let #(h1, _) = link_handle.next_seq(h1)
  let #(h1, _) = link_handle.next_seq(h1)
  link_sequencer.peek(h1.sequencer)
  |> should.equal(2)
  link_sequencer.peek(h2.sequencer)
  |> should.equal(0)
}
