//// Tests for glp/link/primitives/link_terms (T076) — the GLP ground-term ↔ host
//// link value mapping: parse round-trips, the fault lattice, and the ground-relay
//// deep-resolve gate over the heap.

import gleeunit/should
import glp/link/primitives/link_terms
import glp/link/seam/link_address
import glp/link/seam/link_fault.{LinkFaultSignal, Closed, Permanent, Transient}
import glp/link/seam/link_id.{LinkId, NonceInt, NonceStr}
import glp/link/seam/link_scheme
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstInt, ConstString, ConstTerm, StructTerm, VarRef}

fn tcp_link_id_term() {
  StructTerm("link_id", [
    ConstTerm(ConstString("tcp")),
    StructTerm("ep", [
      ConstTerm(ConstString("127.0.0.1")),
      ConstTerm(ConstInt(9100)),
    ]),
    ConstTerm(ConstInt(1)),
  ])
}

pub fn parse_link_id_ep_int_nonce_test() {
  link_terms.parse_link_id(tcp_link_id_term())
  |> should.equal(
    Ok(LinkId(link_scheme.tcp(), link_address.endpoint("127.0.0.1", 9100), NonceInt(1))),
  )
}

pub fn link_id_round_trip_is_structural_test() {
  let assert Ok(id) = link_terms.parse_link_id(tcp_link_id_term())
  // to_term rebuilds a term structurally identical to the source literal (Gleam
  // keeps atom/string distinct — no Dart-style re-quoting).
  link_terms.to_term(id)
  |> should.equal(tcp_link_id_term())
}

pub fn parse_endpoint_bare_string_test() {
  link_terms.parse_endpoint(ConstTerm(ConstString("/tmp/sock")))
  |> should.equal(Ok(link_address.path("/tmp/sock")))
}

pub fn parse_nonce_string_form_test() {
  let term =
    StructTerm("link_id", [
      ConstTerm(ConstString("loopback")),
      ConstTerm(ConstString("chan")),
      ConstTerm(ConstString("n1")),
    ])
  link_terms.parse_link_id(term)
  |> should.equal(
    Ok(LinkId(link_scheme.loopback(), link_address.path("chan"), NonceStr("n1"))),
  )
}

pub fn parse_role_listener_test() {
  link_terms.parse_role(ConstTerm(ConstAtom("listener")))
  |> should.equal(Ok("listener"))
}

pub fn parse_role_connector_test() {
  link_terms.parse_role(ConstTerm(ConstAtom("connector")))
  |> should.equal(Ok("connector"))
}

pub fn parse_role_rejects_junk_test() {
  link_terms.parse_role(ConstTerm(ConstAtom("boss")))
  |> should.be_error
}

pub fn fault_lattice_closed_eos_test() {
  let assert Ok(id) = link_terms.parse_link_id(tcp_link_id_term())
  link_terms.closed(id, link_terms.graceful_reason)
  |> should.equal(
    StructTerm("closed", [tcp_link_id_term(), ConstTerm(ConstString("eos"))]),
  )
}

pub fn from_signal_maps_each_kind_test() {
  let assert Ok(id) = link_terms.parse_link_id(tcp_link_id_term())
  link_terms.from_signal(LinkFaultSignal(id, Closed, "eos"))
  |> should.equal(link_terms.closed(id, "eos"))
  link_terms.from_signal(LinkFaultSignal(id, Transient, "reset"))
  |> should.equal(link_terms.temp_fail(id, "reset"))
  link_terms.from_signal(LinkFaultSignal(id, Permanent, "refused"))
  |> should.equal(link_terms.perm_fail(id, "refused"))
}

pub fn request_token_round_trip_test() {
  let assert Ok(id) = link_terms.parse_link_id(tcp_link_id_term())
  let peer = ConstTerm(ConstAtom("acceptor"))
  let token = link_terms.request_token(id, peer)
  link_terms.parse_request_token(token)
  |> should.equal(Ok(#(id, peer)))
}

pub fn parse_rendezvous_test() {
  let term =
    StructTerm("rendezvous", [
      ConstTerm(ConstString("tcp")),
      StructTerm("ep", [ConstTerm(ConstString("127.0.0.1")), ConstTerm(ConstInt(9120))]),
    ])
  link_terms.parse_rendezvous(term)
  |> should.equal(Ok(#(link_scheme.tcp(), link_address.endpoint("127.0.0.1", 9120))))
}

pub fn ground_resolve_deep_derefs_bound_cells_test() {
  // Build a heap with a bound writer, then a struct whose arg is a VarRef into it.
  let #(h, writer, reader) = heap.allocate_variable(heap.new())
  let assert Ok(#(h, _)) = heap.bind_writer(h, writer, ConstTerm(ConstInt(7)))
  let struct_with_var = StructTerm("wrap", [VarRef(reader)])
  link_terms.ground_resolve(h, struct_with_var)
  |> should.equal(Ok(StructTerm("wrap", [ConstTerm(ConstInt(7))])))
}

pub fn ground_resolve_rejects_unbound_test() {
  let #(h, _writer, reader) = heap.allocate_variable(heap.new())
  link_terms.ground_resolve(h, VarRef(reader))
  |> should.be_error
}
