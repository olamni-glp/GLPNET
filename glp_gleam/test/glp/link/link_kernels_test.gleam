//// T050.C2 — the `'_link_setup'/5` (K1) kernel arm + its runner dispatch seam.
////
//// Two levels, mirroring the madGLP A2 split (`mad_kernels_test` + `mad_send_seam_test`):
////   * **kernel isolation** — call `link_kernels.link_dispatch` directly and pin what K1
////     guarantees: establish-or-reuse converging on the ONE registry (R-5 / FR-002),
////     idempotency at ground identity (FR-007), the In/Out/Faults cursors recorded on the
////     handle for C5–C7, and a bad user-supplied LinkId failing the goal NON-fatally
////     rather than crashing the engine.
////   * **runner seam** — drive a real reduction of a wrapper that forwards to
////     `'_link_setup'`, proving the BODY Spawn label-miss actually reaches the link
////     kernel and threads the updated `LinkState` back out on `Reduced.link`.
////
//// The transport is the same fake counting leaf `link_establish_test` uses: a real
//// loopback `connect` would block awaiting a `listen` rendezvous, and "did we dial?" is
//// exactly what these tests need to observe.

import gleam/erlang/process
import gleam/option.{None, Some}
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/runner
import glp/link/primitives/link_kernels
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime
import glp/link/seam/endpoint.{type Endpoint, Endpoint}
import glp/link/seam/link_address
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt}
import glp/link/seam/link_scheme
import glp/link/seam/transport.{type Transport, Transport}
import glp/runtime/heap
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstString, ConstTerm, StructTerm, VarRef,
}

// ── fixtures (shared with link_establish_test's shape) ───────────────────────

fn an_id(nonce: Int) -> LinkId {
  LinkId(
    scheme: link_scheme.tcp(),
    endpoint: link_address.endpoint("127.0.0.1", 9000),
    nonce: NonceInt(nonce),
  )
}

fn a_dead_endpoint(id: LinkId) -> Endpoint {
  Endpoint(
    id: id,
    send: fn(_frame) { Ok(Nil) },
    recv: fn() { Ok(None) },
    close: fn() { Nil },
    faults: process.new_subject(),
  )
}

/// A tcp leaf that always succeeds, recording each open so a test can assert the dial
/// count (idempotency is "did we dial twice?", which a handle compare cannot see).
fn counting_transport(counter: process.Subject(String)) -> Transport {
  Transport(
    supported_schemes: [link_scheme.tcp()],
    listen: fn(_scheme, _addr, _opts) {
      process.send(counter, "listen")
      Ok(a_dead_endpoint(an_id(1)))
    },
    connect: fn(_scheme, _addr, _opts) {
      process.send(counter, "connect")
      Ok(a_dead_endpoint(an_id(1)))
    },
  )
}

fn drain(counter: process.Subject(String), acc: List(String)) -> List(String) {
  case process.receive(counter, 0) {
    Ok(msg) -> drain(counter, [msg, ..acc])
    Error(_) -> acc
  }
}

fn state_with_counter(counter: process.Subject(String)) -> link_runtime.LinkState {
  link_runtime.new() |> link_runtime.with_transport(counting_transport(counter))
}

/// The ground `link_id(tcp, ep("127.0.0.1", 9000), nonce)` term a program would pass —
/// parses to exactly `an_id(nonce)`.
fn tcp_id_term(nonce: Int) -> Term {
  StructTerm("link_id", [
    ConstTerm(ConstAtom("tcp")),
    StructTerm("ep", [
      ConstTerm(ConstString("127.0.0.1")),
      ConstTerm(ConstInt(9000)),
    ]),
    ConstTerm(ConstInt(nonce)),
  ])
}

// ── kernel isolation ─────────────────────────────────────────────────────────

// K1 establishes the link through the ONE funnel and records the In/Out/Faults heap
// cursors on the stored handle (what C5–C7 consume).
pub fn link_setup_establishes_and_wires_cursors_test() {
  let counter = process.new_subject()
  let state = state_with_counter(counter)

  // In (writer), Out (reader), Faults (writer) — three fresh stream variables.
  let #(h, in_w, _) = heap.allocate_variable(heap.new())
  let #(h, _, out_r) = heap.allocate_variable(h)
  let #(h, faults_w, _) = heap.allocate_variable(h)

  let args = [
    tcp_id_term(1),
    ConstTerm(ConstAtom("connector")),
    VarRef(in_w),
    VarRef(out_r),
    VarRef(faults_w),
  ]

  let assert Ok(link_kernels.LinkEffect(_h2, out_state, [])) =
    link_kernels.link_dispatch(h, state, "_link_setup", 5, args)

  link_registry.count(out_state.links) |> should.equal(1)
  drain(counter, []) |> should.equal(["connect"])

  let assert Ok(handle) = link_registry.try_get(out_state.links, an_id(1))
  handle.in_writer |> should.equal(Some(in_w))
  handle.out_reader |> should.equal(Some(out_r))
  handle.faults_writer |> should.equal(Some(faults_w))
}

// FR-007: a repeat setup at the same ground identity REUSES — one entry, dialled once.
pub fn link_setup_is_idempotent_at_identity_test() {
  let counter = process.new_subject()
  let state = state_with_counter(counter)

  let #(h, w1, r1) = heap.allocate_variable(heap.new())
  let #(h, w2, _) = heap.allocate_variable(h)
  let args = [
    tcp_id_term(7),
    ConstTerm(ConstAtom("connector")),
    VarRef(w1),
    VarRef(r1),
    VarRef(w2),
  ]

  let assert Ok(link_kernels.LinkEffect(_, s1, [])) =
    link_kernels.link_dispatch(h, state, "_link_setup", 5, args)
  let assert Ok(link_kernels.LinkEffect(_, s2, [])) =
    link_kernels.link_dispatch(h, s1, "_link_setup", 5, args)

  link_registry.count(s2.links) |> should.equal(1)
  drain(counter, []) |> should.equal(["connect"])
}

// A user-supplied blank-scheme LinkId is malformed: the kernel FAILS the goal
// (`LinkAbort`, non-fatal) and never touches the transport — kernel args come from user
// programs, so a bad one must not abort the engine (link_terms' blank-scheme guard).
pub fn link_setup_bad_link_id_aborts_non_fatally_test() {
  let counter = process.new_subject()
  let state = state_with_counter(counter)

  let #(h, w1, r1) = heap.allocate_variable(heap.new())
  let #(h, w2, _) = heap.allocate_variable(h)
  let bad_id =
    StructTerm("link_id", [
      ConstTerm(ConstAtom("")),
      StructTerm("ep", [
        ConstTerm(ConstString("127.0.0.1")),
        ConstTerm(ConstInt(9000)),
      ]),
      ConstTerm(ConstInt(1)),
    ])
  let args = [
    bad_id,
    ConstTerm(ConstAtom("connector")),
    VarRef(w1),
    VarRef(r1),
    VarRef(w2),
  ]

  let assert Ok(link_kernels.LinkAbort(_)) =
    link_kernels.link_dispatch(h, state, "_link_setup", 5, args)

  drain(counter, []) |> should.equal([])
}

// ── runner dispatch seam ─────────────────────────────────────────────────────

// A system-mode wrapper forwarding its five args to the clause-less `'_link_setup'`
// host kernel (self.glp declares it clause-less, so a body call compiles to a Spawn
// whose label misses every program label — the `link_spawn` hook).
const seam_source = "-mode(system).
procedure _link_setup(_?, _?, _, _?, _).
procedure lnk(_?, _?, _, _?, _).
lnk(LinkId, Role, In?, Out, Faults?) :- '_link_setup'(LinkId?, Role?, In, Out?, Faults)."

// Proves the BODY Spawn label-miss reaches the link kernel and threads the updated
// `LinkState` back out on `Reduced.link` — the end-to-end runner seam, not the kernel
// in isolation.
pub fn link_setup_dispatched_through_reduce_test() {
  let counter = process.new_subject()
  let state = state_with_counter(counter)

  let assert Ok(outcome) = loader.load(seam_source, "")
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, "lnk/5")

  let #(h, in_w, _) = heap.allocate_variable(heap.new())
  let #(h, _, out_r) = heap.allocate_variable(h)
  let #(h, faults_w, _) = heap.allocate_variable(h)
  let regs =
    program.new_regs()
    |> program.set_reg(0, tcp_id_term(1))
    |> program.set_reg(1, ConstTerm(ConstAtom("connector")))
    |> program.set_reg(2, VarRef(in_w))
    |> program.set_reg(3, VarRef(out_r))
    |> program.set_reg(4, VarRef(faults_w))
  let ctx = runner.with_link(runner.new_context(h, regs), state)

  let out = runner.reduce(prog, ctx, kappa, 1000)

  let assert runner.Reduced(link: Some(out_state), ..) = out
  link_registry.count(out_state.links) |> should.equal(1)
  drain(counter, []) |> should.equal(["connect"])
}
