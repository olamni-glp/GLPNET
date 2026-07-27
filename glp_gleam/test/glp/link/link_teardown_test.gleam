//// T050.C8 — close/teardown: K7 `'_link_close'/2` + `link_teardown.teardown`.
////
//// What C8 guarantees, and what these tests pin:
////   * ABRUPT close: terminal `closed(LinkId, Reason)` on EVERY monitor stream, those
////     streams then ENDED with `[]` (the delivery must land on the cursors `deliver_fault`
////     advanced — ending the pre-delivery cursors would double-bind), the transport
////     endpoint closed exactly once, the registry entry removed (distributed GC, FR-024).
////   * The `In` DATA path is untouched: a goal suspended on `In` learns of the close
////     through the monitor stream, never through a bind on its data reader (FR-044).
////   * Repeat close → non-fatal abort (close observes; after GC there is nothing).
////   * GRACEFUL close converges on the SAME kernel: `link_drain/3`'s `[]` clause
////     (self.glp:562-564) dispatches `'_link_close'(LinkId?, eos)` — proven end-to-end
////     over the SHIPPED prelude under `step_link`, the D-2/D-5 machinery composed.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleam/erlang/process
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
import glp/runtime/heap.{type Heap}
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstString, ConstTerm, StructTerm, VarRef,
}

// ── fixtures: a transport whose endpoints COUNT their closes ────────────────

fn an_id(nonce: Int) -> LinkId {
  LinkId(
    scheme: link_scheme.tcp(),
    endpoint: link_address.endpoint("127.0.0.1", 9000),
    nonce: NonceInt(nonce),
  )
}

fn closable_endpoint(id: LinkId, closes: process.Subject(Nil)) -> Endpoint {
  Endpoint(
    id: id,
    send: fn(_frame) { Ok(Nil) },
    recv: fn() { Ok(None) },
    close: fn() { process.send(closes, Nil) },
    faults: process.new_subject(),
  )
}

fn counting_transport(closes: process.Subject(Nil)) -> Transport {
  Transport(
    supported_schemes: [link_scheme.tcp()],
    listen: fn(_s, _a, _o) { Ok(closable_endpoint(an_id(1), closes)) },
    connect: fn(_s, _a, _o) { Ok(closable_endpoint(an_id(1), closes)) },
  )
}

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

/// Establish via K1; returns heap, state, and the establishment In/Faults READERS.
fn established(
  closes: process.Subject(Nil),
) -> #(Heap, link_runtime.LinkState, Int, Int) {
  let state =
    link_runtime.new() |> link_runtime.with_transport(counting_transport(closes))
  let #(h, in_w, in_r) = heap.allocate_variable(heap.new())
  let #(h, _, out_r) = heap.allocate_variable(h)
  let #(h, faults_w, faults_r) = heap.allocate_variable(h)
  let assert Ok(link_kernels.LinkEffect(h, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_setup", 5, [
      tcp_id_term(1),
      ConstTerm(ConstAtom("connector")),
      VarRef(in_w),
      VarRef(out_r),
      VarRef(faults_w),
    ])
  #(h, state, in_r, faults_r)
}

fn value_at(heap: Heap, addr: Int) -> option.Option(Term) {
  case heap.deref(heap, addr) {
    Ok(#(_h, heap.Bound(term))) -> Some(term)
    _ -> None
  }
}

fn count(closes: process.Subject(Nil), acc: Int) -> Int {
  case process.receive(closes, 0) {
    Ok(Nil) -> count(closes, acc + 1)
    Error(_) -> acc
  }
}

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_source(path: String) -> String {
  let assert Ok(bytes) = read_file(path)
  let assert Ok(text) = bit_array.to_string(bytes)
  text
}

// ── abrupt close (K7 direct) ─────────────────────────────────────────────────

/// Full abrupt-teardown contract on one dispatch: terminal + ended monitor streams on
/// BOTH observers, endpoint closed once, registry emptied, `In` untouched.
pub fn abrupt_close_tears_down_test() {
  let closes = process.new_subject()
  let #(h, state, in_r, est_r) = established(closes)

  // Add an independent link_monitor observer (its stream starts [ok | _]).
  let #(h, mon_w, mon_r) = heap.allocate_variable(h)
  let assert Ok(link_kernels.LinkEffect(h, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_monitor", 2, [
      tcp_id_term(1),
      VarRef(mon_w),
    ])

  let assert Ok(link_kernels.LinkEffect(h, state, _woken)) =
    link_kernels.link_dispatch(h, state, "_link_close", 2, [
      tcp_id_term(1),
      ConstTerm(ConstAtom("bye")),
    ])

  // Establishment stream: [closed(id, bye) | []] — terminal then ended.
  let assert Some(StructTerm(".", [closed_t, VarRef(est_tail)])) = value_at(h, est_r)
  let assert StructTerm("closed", [_, ConstTerm(ConstAtom("bye"))]) = closed_t
  value_at(h, est_tail) |> should.equal(Some(ConstTerm(ConstAtom("nil"))))

  // Monitor stream: [ok, closed(id, bye) | []].
  let assert Some(StructTerm(".", [ConstTerm(ConstAtom("ok")), VarRef(t1)])) =
    value_at(h, mon_r)
  let assert Some(StructTerm(".", [closed_m, VarRef(t2)])) = value_at(h, t1)
  closed_m |> should.equal(closed_t)
  value_at(h, t2) |> should.equal(Some(ConstTerm(ConstAtom("nil"))))

  // Endpoint closed exactly once; registry back to baseline; In untouched (FR-044).
  count(closes, 0) |> should.equal(1)
  link_registry.count(state.links) |> should.equal(0)
  value_at(h, in_r) |> should.equal(None)
}

/// A second close finds nothing (GC already ran): non-fatal abort, no second endpoint
/// close, monitor streams untouched.
pub fn repeat_close_aborts_test() {
  let closes = process.new_subject()
  let #(h, state, _in_r, _est_r) = established(closes)

  let close_args = [tcp_id_term(1), ConstTerm(ConstAtom("bye"))]
  let assert Ok(link_kernels.LinkEffect(h, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_close", 2, close_args)
  let assert Ok(link_kernels.LinkAbort(_)) =
    link_kernels.link_dispatch(h, state, "_link_close", 2, close_args)
  count(closes, 0) |> should.equal(1)
}

// ── pump shutdown: teardown kills the parked recv loop ───────────────────────

/// C8's dispose-equivalent (Gabi ruling 2026-07-27: `process.kill` extends the no-OTP
/// subset). The endpoint here PARKS its recv — the loop can never exit on its own — so
/// only the teardown kill can end it. Establish arms the pump; close must kill it.
pub fn teardown_kills_the_parked_pump_test() {
  let parked =
    Transport(
      supported_schemes: [link_scheme.tcp()],
      listen: fn(_s, _a, _o) { Ok(parked_endpoint(an_id(1))) },
      connect: fn(_s, _a, _o) { Ok(parked_endpoint(an_id(1))) },
    )
  let state = link_runtime.new() |> link_runtime.with_transport(parked)
  let #(h, in_w, _) = heap.allocate_variable(heap.new())
  let #(h, _, out_r) = heap.allocate_variable(h)
  let #(h, faults_w, _) = heap.allocate_variable(h)
  let assert Ok(link_kernels.LinkEffect(h, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_setup", 5, [
      tcp_id_term(1),
      ConstTerm(ConstAtom("connector")),
      VarRef(in_w),
      VarRef(out_r),
      VarRef(faults_w),
    ])
  let assert Ok(handle) = link_registry.try_get(state.links, an_id(1))
  let assert Some(pid) = handle.pump
  process.is_alive(pid) |> should.be_true

  let assert Ok(link_kernels.LinkEffect(_, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_close", 2, [
      tcp_id_term(1),
      ConstTerm(ConstAtom("bye")),
    ])
  link_registry.count(state.links) |> should.equal(0)
  wait_dead(pid, 50)
}

fn parked_endpoint(id: LinkId) -> Endpoint {
  Endpoint(
    id: id,
    send: fn(_frame) { Ok(Nil) },
    // Parks (effectively) forever: without the teardown kill this loop never ends.
    recv: fn() {
      process.sleep(3_600_000)
      Ok(None)
    },
    close: fn() { Nil },
    faults: process.new_subject(),
  )
}

/// The kill signal is asynchronous; poll briefly rather than asserting instantly.
fn wait_dead(pid: process.Pid, budget: Int) -> Nil {
  case process.is_alive(pid) {
    False -> Nil
    True ->
      case budget <= 0 {
        True -> panic as "pump process survived teardown kill"
        False -> {
          process.sleep(10)
          wait_dead(pid, budget - 1)
        }
      }
  }
}

// ── graceful close: link_drain's [] clause → K7 with eos, over the prelude ──

/// The graceful stream-end, end-to-end over the SHIPPED self.glp: a `link_drain/3` goal
/// whose stream is `[]` reduces its second clause (self.glp:562-564) and dispatches
/// `'_link_close'(LinkId?, eos)` through `step_link` — tearing the link down with the
/// graceful reason. This is the D-2 drainer, the D-5 seam, and K7 composed: the whole
/// C5→C8 egress lifecycle in one reduction chain.
pub fn graceful_eos_via_link_drain_test() {
  let closes = process.new_subject()
  let #(h, state, _in_r, est_r) = established(closes)

  let assert Ok(prog) = loader.compile_prelude(read_source("../programs/self.glp"))
  let assert Ok(entry) = program.label_pc(prog, "link_drain/3")
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("nil")))
    |> program.set_reg(1, tcp_id_term(1))
    |> program.set_reg(2, ConstTerm(ConstAtom("peer")))
  let #(engine, _) =
    scheduler.boot(scheduler.new(prog, h), "link_drain/3", entry, regs)

  let #(engine, state) = run_link(engine, state, 10)

  // The link is gone (GC), the endpoint closed once, and the establishment Faults
  // stream carries the terminal with the GRACEFUL reason: [closed(id, eos) | []].
  link_registry.count(state.links) |> should.equal(0)
  count(closes, 0) |> should.equal(1)
  let h = scheduler.heap(engine)
  let assert Some(StructTerm(".", [closed_t, VarRef(tail)])) = value_at(h, est_r)
  let assert StructTerm("closed", [_, ConstTerm(ConstAtom("eos"))]) = closed_t
  value_at(h, tail) |> should.equal(Some(ConstTerm(ConstAtom("nil"))))
}

fn run_link(
  engine: scheduler.Engine,
  state: link_runtime.LinkState,
  budget: Int,
) -> #(scheduler.Engine, link_runtime.LinkState) {
  case budget <= 0 {
    True -> #(engine, state)
    False ->
      case scheduler.step_link(engine, 1000, state) {
        #(engine, scheduler.StepIdle, state) -> #(engine, state)
        #(engine, _, state) -> run_link(engine, state, budget - 1)
      }
  }
}
