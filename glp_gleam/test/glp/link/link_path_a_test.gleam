//// T050.C3 — establishment path A (listen / connect) exercised through the K1
//// `'_link_setup'/5` kernel over REAL transports.
////
//// C2 proved the K1 arm and its runner dispatch against a FAKE counting transport and
//// the `connector` role only. C3 adds the coverage a fake leaf cannot give: BOTH roles
//// (`listener` → `leaf.listen`, `connector` → `leaf.connect`) rendezvousing over a
//// genuine transport, and the FR-002 convergence property — a listener end and a
//// connector end that meet each hold the SAME established link identity, one entry in
//// each of their own registries.
////
//// K1 is exercised directly rather than through the GLP wrappers because
//// `server_listener/3` and `client_connector/3` merely delegate to `link_setup/4` →
//// `'_link_setup'` (self.glp:496-504; `contracts/link-primitives-port.md` C3): the
//// kernel IS the thing under test.
////
//// The seam's listen/connect are synchronous and block until they rendezvous, so — as
//// in `loopback_test`/`tcp_test` — the connector end runs in a spawned BEAM process and
//// the listener end runs in the test process. Both ends share ONE transport value (one
//// loopback hub / one tcp leaf) but each has its OWN `LinkState` + registry, exactly as
//// two engines on two hosts would.

import gleam/erlang/process
import gleeunit/should
import glp/link/primitives/link_kernels
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime
import glp/link/seam/link_address
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt}
import glp/link/seam/link_scheme
import glp/link/transports/loopback
import glp/link/transports/tcp
import glp/runtime/heap
import glp/runtime/terms.{type Term, ConstAtom, ConstInt, ConstString, ConstTerm, StructTerm, VarRef}

// ── identities (value + the ground term a program would pass) ────────────────

fn loopback_id(channel: String, nonce: Int) -> LinkId {
  LinkId(
    scheme: link_scheme.loopback(),
    endpoint: link_address.path(channel),
    nonce: NonceInt(nonce),
  )
}

fn loopback_id_term(channel: String, nonce: Int) -> Term {
  StructTerm("link_id", [
    ConstTerm(ConstAtom("loopback")),
    ConstTerm(ConstString(channel)),
    ConstTerm(ConstInt(nonce)),
  ])
}

fn tcp_id(host: String, port: Int, nonce: Int) -> LinkId {
  LinkId(
    scheme: link_scheme.tcp(),
    endpoint: link_address.endpoint(host, port),
    nonce: NonceInt(nonce),
  )
}

fn tcp_id_term(host: String, port: Int, nonce: Int) -> Term {
  StructTerm("link_id", [
    ConstTerm(ConstAtom("tcp")),
    StructTerm("ep", [ConstTerm(ConstString(host)), ConstTerm(ConstInt(port))]),
    ConstTerm(ConstInt(nonce)),
  ])
}

// ── the one thing under test: run K1 for a role, return the resulting LinkState ──

/// Establish `id_term` in `role` through the K1 kernel over `state`'s transport. Fresh
/// In/Out/Faults stream variables (their addresses become the recorded cursors — C5–C7
/// consume them; C3 only cares that establishment converged).
fn setup(
  state: link_runtime.LinkState,
  id_term: Term,
  role: String,
) -> Result(link_runtime.LinkState, Nil) {
  let #(h, w1, r1) = heap.allocate_variable(heap.new())
  let #(h, w2, _) = heap.allocate_variable(h)
  let args = [
    id_term,
    ConstTerm(ConstAtom(role)),
    VarRef(w1),
    VarRef(r1),
    VarRef(w2),
  ]
  case link_kernels.link_dispatch(h, state, "_link_setup", 5, args) {
    Ok(link_kernels.LinkEffect(_, s, _)) -> Ok(s)
    _ -> Error(Nil)
  }
}

// ── path A over the loopback transport ───────────────────────────────────────

pub fn path_a_loopback_round_trip_converges_test() {
  let t = loopback.new()
  let id = loopback_id("chan-c3", 1)
  let id_term = loopback_id_term("chan-c3", 1)
  let back = process.new_subject()

  // Connector in a child — listen/connect block until they rendezvous.
  process.spawn(fn() {
    let state = link_runtime.new() |> link_runtime.with_transport(t)
    let established = case setup(state, id_term, "connector") {
      Ok(s) -> link_registry.contains(s.links, id)
      Error(_) -> False
    }
    process.send(back, established)
  })

  // Listener in the test process.
  let assert Ok(listener_state) =
    setup(link_runtime.new() |> link_runtime.with_transport(t), id_term, "listener")

  // Both ends hold the SAME established identity — one link, each in its own registry.
  link_registry.count(listener_state.links) |> should.equal(1)
  link_registry.contains(listener_state.links, id) |> should.be_true
  process.receive(back, 5000) |> should.equal(Ok(True))
}

// ── path A over the real TCP transport (the transport C3 names) ──────────────

pub fn path_a_tcp_round_trip_converges_test() {
  let t = tcp.new()
  let host = "127.0.0.1"
  let port = 34_731
  let id = tcp_id(host, port, 1)
  let id_term = tcp_id_term(host, port, 1)
  let back = process.new_subject()

  // Connector in a child — tcp connect retries until the listener below binds.
  process.spawn(fn() {
    let state = link_runtime.new() |> link_runtime.with_transport(t)
    let established = case setup(state, id_term, "connector") {
      Ok(s) -> link_registry.contains(s.links, id)
      Error(_) -> False
    }
    process.send(back, established)
  })

  let assert Ok(listener_state) =
    setup(link_runtime.new() |> link_runtime.with_transport(t), id_term, "listener")

  link_registry.count(listener_state.links) |> should.equal(1)
  link_registry.contains(listener_state.links, id) |> should.be_true
  process.receive(back, 5000) |> should.equal(Ok(True))
}
