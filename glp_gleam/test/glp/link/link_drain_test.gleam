//// T050.C5 (second half) — the HOST LOWERING of the egress drainer.
////
//// C5's first half shipped `link_egress.ship_ground` + the K2 `'_link_send'/3` kernel
//// (the LinkId-keyed sender face) and, in `programs/self.glp`, the `link_drain/3`
//// procedure the channel face needs — `link_send/3` only conses onto `Out`, and
//// `link_drain/3` is what carries those terms to the wire. This half wires that
//// procedure to establishment: nothing lowers a drainer, so an `Out` stream established
//// by `link_setup/4` is consed onto by the program and read by nobody.
////
//// Deviation D-2 (Gabi §1.14 approval 2026-07-27, option (a)): the C#/Dart oracle arms
//// `heap.OnBind(outWriterAddr, …)`; Gleam has no `onBind`, so the drainer is lowered as
//// an ORDINARY runnable goal reading the `Out` READER. Its first head argument is
//// `Stream(X)?`, so it SUSPENDS until the program conses — the existing
//// suspension/reactivation machinery drives egress, exactly as A3 drove `global_send/3`.
////
//// Three things are under test here, and only these — the drainer's own semantics (ship
//// each head, `[]` → `'_link_close'(LinkId?, eos)`) are the GLP procedure's, verified in
//// the REPL when it landed:
////   1. establishment ACCUMULATES one drain request, on `Established` and never on
////      `Reused` (FR-007: two drainers on one `Out` would ship every cons twice);
////   2. `scheduler.step_link` — the missing driver — LOWERS it into a real runnable
////      `link_drain/3` goal, over the SHIPPED `programs/self.glp`, and that goal
////      suspends on the very `Out` reader establishment recorded;
////   3. a missing `link_drain/3` SURFACES as `StepErrored`, never a silent drop (an
////      unlowered drainer is a link that is established but can never reach the wire).

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/erlang/process
import gleam/list
import gleam/option.{None, Some}
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/scheduler
import glp/link/primitives/link_kernels
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime
import glp/link/seam/endpoint.{type Endpoint, Endpoint}
import glp/link/seam/link_address
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt}
import glp/link/seam/link_scheme
import glp/link/seam/transport.{type Transport, Transport}
import glp/link/transports/loopback
import glp/runtime/heap.{type Heap}
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstString, ConstTerm, StructTerm, VarRef,
}

// ── fixtures (the C2 counting leaf — "did we dial?" is what idempotency means) ──

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

fn always_ok_transport() -> Transport {
  Transport(
    supported_schemes: [link_scheme.tcp()],
    listen: fn(_scheme, _addr, _opts) { Ok(a_dead_endpoint(an_id(1))) },
    connect: fn(_scheme, _addr, _opts) { Ok(a_dead_endpoint(an_id(1))) },
  )
}

fn a_state() -> link_runtime.LinkState {
  link_runtime.new() |> link_runtime.with_transport(always_ok_transport())
}

/// The ground `link_id(tcp, ep("127.0.0.1", 9000), nonce)` term a program would pass.
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

/// The same identity as `link_terms.link_id_to_term` rebuilds it — text components go
/// back as ATOMS (the form a GLP source literal lexes to), so the drainer's `LinkId`
/// argument is `=?=`-comparable with what the program wrote.
fn rebuilt_tcp_id_term(nonce: Int) -> Term {
  StructTerm("link_id", [
    ConstTerm(ConstAtom("tcp")),
    StructTerm("ep", [
      ConstTerm(ConstAtom("127.0.0.1")),
      ConstTerm(ConstInt(9000)),
    ]),
    ConstTerm(ConstInt(nonce)),
  ])
}

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_source(path: String) -> String {
  let assert Ok(bytes) = read_file(path)
  let assert Ok(text) = bit_array.to_string(bytes)
  text
}

// ── 1. accumulation: on Established, and NOT on Reused ───────────────────────

/// Establishment records ONE drain request carrying the `Out` READER address (the
/// kernel's arg 4) plus the two ground terms `link_drain/3`'s guards demand: the
/// LinkId term, and the bilateral peer derived from it (`ep(H,P)` → `peer(H,P)`,
/// FR-005 — `'_link_setup'/5` carries no peer argument of its own).
pub fn establish_arms_exactly_one_drainer_test() {
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

  let assert Ok(link_kernels.LinkEffect(_h, state, [])) =
    link_kernels.link_dispatch(h, a_state(), "_link_setup", 5, args)

  let assert [request] = state.drains
  request.out_reader |> should.equal(out_r)
  request.link_id |> should.equal(rebuilt_tcp_id_term(1))
  request.to_peer
  |> should.equal(
    StructTerm("peer", [
      ConstTerm(ConstAtom("127.0.0.1")),
      ConstTerm(ConstInt(9000)),
    ]),
  )
}

/// FR-007: a repeat `link_setup` at the same ground identity REUSES the handle, and must
/// NOT arm a second drainer. Two `link_drain/3` goals reading one `Out` stream is a
/// double-read of a non-constant stream — every cons would be shipped twice.
pub fn reuse_does_not_arm_a_second_drainer_test() {
  let #(h, in_w, _) = heap.allocate_variable(heap.new())
  let #(h, _, out_r) = heap.allocate_variable(h)
  let #(h, faults_w, _) = heap.allocate_variable(h)
  let args = [
    tcp_id_term(7),
    ConstTerm(ConstAtom("connector")),
    VarRef(in_w),
    VarRef(out_r),
    VarRef(faults_w),
  ]

  let assert Ok(link_kernels.LinkEffect(_, s1, [])) =
    link_kernels.link_dispatch(h, a_state(), "_link_setup", 5, args)
  // Take what the first establishment armed, exactly as `step_link` does, so the second
  // call starts from a drained state — otherwise the assertion could not tell a re-arm
  // from the first request still sitting there.
  let #(s1, first) = link_runtime.take_drains(s1)
  list.length(first) |> should.equal(1)

  let assert Ok(link_kernels.LinkEffect(_, s2, [])) =
    link_kernels.link_dispatch(h, s1, "_link_setup", 5, args)

  link_registry.count(s2.links) |> should.equal(1)
  s2.drains |> should.equal([])
}

// ── 1b. path B arms with the REAL peer, path A with the derived one ──────────

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

/// Gabi's ruling 2026-07-27: a path-B kernel HAS a real counterparty and its drainer says
/// so — K3 lowers with the `ToPeer` the program passed, K5 with the `FromPeer` off the
/// request token. Only K1 (path A), which has no peer argument at all, falls back to the
/// LinkId-derived `bilateral_peer`. Both peers here differ from that derivation
/// (`chan-c5-drain`), so this cannot pass by accident.
///
/// The two ends block until they rendezvous, so the connector runs in a spawned process
/// and the listener in the test process, sharing one loopback hub (the C4 harness shape).
pub fn path_b_drainers_carry_the_real_peer_test() {
  let t = loopback.new()
  let id_term = loopback_id_term("chan-c5-drain", 1)
  let back = process.new_subject()

  // Connector (child): K3 `'_link_request'` — drainer must carry ToPeer = `bob`.
  process.spawn(fn() {
    let state = link_runtime.new() |> link_runtime.with_transport(t)
    let #(h, streams) = streams(heap.new())
    let args = [id_term, ConstTerm(ConstAtom("bob")), ..streams]
    let peer = case
      link_kernels.link_dispatch(h, state, "_link_request", 5, args)
    {
      Ok(link_kernels.LinkEffect(_, s, _)) ->
        case s.drains {
          [request] -> Ok(request.to_peer)
          _ -> Error(Nil)
        }
      _ -> Error(Nil)
    }
    process.send(back, peer)
  })

  // Listener (test process): K4 listen (park + surface), then K5 accept (adopt).
  let state = link_runtime.new() |> link_runtime.with_transport(t)
  let #(h, req_w, _) = heap.allocate_variable(heap.new())
  let listen_args = [
    ConstTerm(ConstAtom("loopback")),
    ConstTerm(ConstString("chan-c5-drain")),
    VarRef(req_w),
  ]
  let assert Ok(link_kernels.LinkEffect(h, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_listen", 3, listen_args)
  // K4 does not establish, so it arms no drainer.
  state.drains |> should.equal([])

  let #(h, streams) = streams(h)
  let accept_args = [id_term, ConstTerm(ConstAtom("requester")), ..streams]
  let assert Ok(link_kernels.LinkEffect(_, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_accept", 5, accept_args)

  let assert [accept_request] = state.drains
  accept_request.to_peer |> should.equal(ConstTerm(ConstAtom("requester")))
  process.receive(back, 5000)
  |> should.equal(Ok(Ok(ConstTerm(ConstAtom("bob")))))
}

// ── 2. the driver: step_link lowers it over the SHIPPED prelude ───────────────

/// End-to-end over the SHIPPED prelude: boot `link_setup/4` (self.glp:487-491) itself —
/// so the `Out` reader the drainer receives is the one the wrapper's own `ch(In?, Out)`
/// head minted, not one the test fabricated — and reduce it under `step_link`. The
/// drainer establishment accumulated becomes a REAL runnable `link_drain/3` goal, which
/// then suspends, because the program has not consed onto `Out` yet. That suspension IS
/// the egress mechanism (D-2): no `heap.onBind`, just an ordinary reader read.
///
/// The prelude is compiled with `loader.compile_prelude` (parse → SRSW → PE → codegen,
/// no type-check stage) because that is how the engine facade loads self.glp — its
/// clauses call host kernels deliberately absent from `builtinProcedures`.
pub fn step_link_lowers_the_drainer_over_the_shipped_prelude_test() {
  let assert Ok(prog) = loader.compile_prelude(read_source("../programs/self.glp"))
  // The lowering target must be a callable label in the very program under test.
  let assert Ok(_) = program.label_pc(prog, "link_drain/3")
  let assert Ok(entry) = program.label_pc(prog, "link_setup/4")

  let #(h, link_w, _) = heap.allocate_variable(heap.new())
  let #(h, faults_w, _) = heap.allocate_variable(h)
  let regs =
    program.new_regs()
    |> program.set_reg(0, tcp_id_term(1))
    |> program.set_reg(1, ConstTerm(ConstAtom("connector")))
    |> program.set_reg(2, VarRef(link_w))
    |> program.set_reg(3, VarRef(faults_w))

  let #(engine, _goal) =
    scheduler.boot(scheduler.new(prog, h), "link_setup/4", entry, regs)

  // Reduce the wrapper into the kernel, then keep stepping until the drainer is lowered.
  // `step_link` is the ONLY driver that injects the LinkState, so this also proves the
  // seam `step`/`step_mad` previously discarded (`link: _`).
  let #(engine, state, outcomes) = run_link(engine, a_state(), 8, [])

  // The link established, and its drainer was taken out of the state and lowered.
  link_registry.count(state.links) |> should.equal(1)
  state.drains |> should.equal([])

  // The lowered goal is real: it dequeued as `link_drain/3` and suspended on the very
  // `Out` reader establishment recorded on the handle — i.e. it is waiting for
  // `link_send/3` to cons. That wait IS the egress arming.
  let assert Ok(handle) = link_registry.try_get(state.links, an_id(1))
  let assert Some(out_reader) = handle.out_reader
  let assert [on] =
    list.filter_map(outcomes, fn(o) {
      case o {
        scheduler.StepSuspended(_id, "link_drain/3", on) -> Ok(on)
        _ -> Error(Nil)
      }
    })
  // `StepSuspended.on` carries WRITER addresses; the drainer read the paired READER.
  let final_heap = scheduler.heap(engine)
  on
  |> list.map(heap.paired_reader(final_heap, _))
  |> list.contains(out_reader)
  |> should.equal(True)
}

/// `step_link` to quiescence (or `budget` steps), threading the LinkState — the shape a
/// link-aware engine facade will use. Returns the final engine + state plus every step
/// outcome, in order, so a test can assert on what the run actually did.
fn run_link(
  engine: scheduler.Engine,
  state: link_runtime.LinkState,
  budget: Int,
  acc: List(scheduler.StepOutcome),
) -> #(scheduler.Engine, link_runtime.LinkState, List(scheduler.StepOutcome)) {
  case budget <= 0 {
    True -> #(engine, state, list.reverse(acc))
    False ->
      case scheduler.step_link(engine, 1000, state) {
        #(engine, scheduler.StepIdle, state) -> #(
          engine,
          state,
          list.reverse(acc),
        )
        #(engine, outcome, state) ->
          run_link(engine, state, budget - 1, [outcome, ..acc])
      }
  }
}

// ── 3. a missing link_drain/3 is SURFACED, never dropped ─────────────────────

/// The C2 seam wrapper: forwards to the clause-less `'_link_setup'` kernel, with NO
/// prelude — so `link_drain/3` is genuinely absent from the program.
const bare_seam_source = "-mode(system).
procedure _link_setup(_?, _?, _, _?, _).
procedure lnk(_?, _?, _, _?, _).
lnk(LinkId, Role, In?, Out, Faults?) :- '_link_setup'(LinkId?, Role?, In, Out?, Faults)."

/// An established link whose drainer cannot be lowered is a link that can never reach the
/// wire. That is an engine fault, not a warning: it surfaces as `StepErrored` and the
/// caller keeps its ORIGINAL LinkState (the half-applied one is not handed back).
pub fn missing_link_drain_surfaces_as_step_errored_test() {
  let assert Ok(outcome) = loader.load(bare_seam_source, "")
  let prog = outcome.program
  program.label_pc(prog, "link_drain/3") |> should.equal(Error(Nil))
  let assert Ok(entry) = program.label_pc(prog, "lnk/5")

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

  let #(engine, _goal) =
    scheduler.boot(scheduler.new(prog, h), "lnk/5", entry, regs)
  let state = a_state()

  let #(_engine, outcome, returned) = scheduler.step_link(engine, 1000, state)
  let assert scheduler.StepErrored(_) = outcome
  // The original state came back, not the half-applied one.
  link_registry.count(returned.links) |> should.equal(0)
}
