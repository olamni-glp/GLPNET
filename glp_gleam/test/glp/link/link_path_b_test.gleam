//// T050.C4 — establishment path B (request / listen / accept) end-to-end.
////
//// The full handshake through the three path-B kernels over a real loopback transport:
////   * connector: K3 `'_link_request'` connects, ships the in-band `request(...)` token,
////     adopts that connection into an established link;
////   * listener: K4 `'_link_listen'` binds the rendezvous, raw-reads the token, PARKS the
////     accepted endpoint keyed by the token's LinkId, surfaces `request(...)`; then K5
////     `'_link_accept'` adopts the parked endpoint and establishes.
//// Both ends converge on the SAME registry funnel (R-5) and hold the SAME established
//// LinkId (FR-002) — indistinguishable from a path-A listen/connect link.
////
//// As with path A, the two ends block until they rendezvous, so the connector runs in a
//// spawned BEAM process and the listener in the test process, sharing ONE loopback hub
//// but each with its OWN LinkState + registry.

import gleam/erlang/process
import gleeunit/should
import glp/link/primitives/link_kernels
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime
import glp/link/seam/link_address
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt}
import glp/link/seam/link_scheme
import glp/link/transports/loopback
import glp/runtime/heap.{type Heap}
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstString, ConstTerm, StructTerm, VarRef,
}

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

/// Fresh In (writer), Out (reader), Faults (writer) stream args, in kernel arg order.
fn streams(h: Heap) -> #(Heap, List(Term)) {
  let #(h, in_w, _) = heap.allocate_variable(h)
  let #(h, _, out_r) = heap.allocate_variable(h)
  let #(h, faults_w, _) = heap.allocate_variable(h)
  #(h, [VarRef(in_w), VarRef(out_r), VarRef(faults_w)])
}

pub fn path_b_handshake_converges_test() {
  let t = loopback.new()
  let id = loopback_id("chan-b", 1)
  let id_term = loopback_id_term("chan-b", 1)
  let back = process.new_subject()

  // Connector (child): K3 request — connect, ship token, adopt into an established link.
  process.spawn(fn() {
    let state = link_runtime.new() |> link_runtime.with_transport(t)
    let #(h, streams) = streams(heap.new())
    let args = [id_term, ConstTerm(ConstAtom("peer")), ..streams]
    let established = case
      link_kernels.link_dispatch(h, state, "_link_request", 5, args)
    {
      Ok(link_kernels.LinkEffect(_, s, _)) -> link_registry.contains(s.links, id)
      _ -> False
    }
    process.send(back, established)
  })

  // Listener (test process): K4 listen (park + surface), then K5 accept (adopt).
  let state = link_runtime.new() |> link_runtime.with_transport(t)
  let #(h, req_w, _) = heap.allocate_variable(heap.new())
  let listen_args = [
    ConstTerm(ConstAtom("loopback")),
    ConstTerm(ConstString("chan-b")),
    VarRef(req_w),
  ]
  let assert Ok(link_kernels.LinkEffect(h, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_listen", 3, listen_args)

  let #(h, streams) = streams(h)
  let accept_args = [id_term, ConstTerm(ConstAtom("requester")), ..streams]
  let assert Ok(link_kernels.LinkEffect(_, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_accept", 5, accept_args)

  // Listener established the link; the connector converged on the same identity.
  link_registry.count(state.links) |> should.equal(1)
  link_registry.contains(state.links, id) |> should.be_true
  process.receive(back, 5000) |> should.equal(Ok(True))
}
