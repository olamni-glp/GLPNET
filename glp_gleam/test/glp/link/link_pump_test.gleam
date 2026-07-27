//// T050.C6 — the ingress pump: `link_recv` fed by a background receive loop.
////
//// Two levels, the same split C2/C5 used:
////   * **applier isolation** — call `link_pump.apply_item` directly and pin the
////     runner-side contract: `Data` extends `In` by one cons and ADVANCES the handle's
////     ingress cursor (so the next term extends the tail, not the consumed cell);
////     `Closed` binds `[]` and CLEARS the cursor, making a late frame or a second close
////     a no-op rather than a double-bind.
////   * **end-to-end** — a real loopback rendezvous: a peer process ships a ground term
////     through the SAME `link_wire` encode path egress uses, the pump process receives
////     and decodes it, `step_link` ingests it on the runner side, and a `link_recv/3`
////     goal that was suspended on the `In` reader wakes and binds its `Msg`.
////
//// The inbox is a BEAM `Subject` owned by whoever called `link_runtime.new()`, so the
//// LinkState is always created in the TEST process (which is the one that drives
//// `step_link`); the peer runs in a spawned process, as in the C3/C4 harnesses.

import gleam/erlang/process
import gleam/option.{type Option, None, Some}
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/scheduler
import glp/link/primitives/link_handle
import glp/link/primitives/link_kernels
import glp/link/primitives/link_pump
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime
import glp/link/primitives/link_terms
import glp/link/primitives/link_wire
import glp/link/seam/endpoint.{type Endpoint, Endpoint}
import glp/link/seam/link_address
import glp/link/seam/link_id.{type LinkId, LinkId, NonceInt}
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import glp/link/transports/loopback
import glp/runtime/heap.{type Heap}
import glp/runtime/terms.{
  type Term, ConstAtom, ConstInt, ConstString, ConstTerm, StructTerm, VarRef,
}
import gleam/bit_array
import gleam/dynamic.{type Dynamic}

// ── fixtures ─────────────────────────────────────────────────────────────────

fn loopback_id(channel: String) -> LinkId {
  LinkId(
    scheme: link_scheme.loopback(),
    endpoint: link_address.path(channel),
    nonce: NonceInt(1),
  )
}

fn loopback_id_term(channel: String) -> Term {
  StructTerm("link_id", [
    ConstTerm(ConstAtom("loopback")),
    ConstTerm(ConstString(channel)),
    ConstTerm(ConstInt(1)),
  ])
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

/// A handle with its ingress cursor wired to `in_writer` — the shape establishment hands
/// the pump.
fn wired_handle(id: LinkId, in_writer: Int) -> link_handle.LinkHandle {
  link_handle.new(id, a_dead_endpoint(id), link_options.default())
  |> link_handle.with_cursors(in_writer, 0, 0)
}

fn registry_of(handle: link_handle.LinkHandle) -> link_registry.LinkRegistry {
  link_registry.put(link_registry.new(), handle)
}

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn read_source(path: String) -> String {
  let assert Ok(bytes) = read_file(path)
  let assert Ok(text) = bit_array.to_string(bytes)
  text
}

/// What a heap cell currently holds, or `None` if still unbound. ONE level of deref —
/// NOT `ground_resolve`, because an extended `In` stream is `[value | UnboundTail]` by
/// construction and a ground resolve of it always fails.
fn value_at(heap: Heap, addr: Int) -> Option(Term) {
  case heap.deref(heap, addr) {
    Ok(#(_h, heap.Bound(term))) -> Some(term)
    _ -> None
  }
}

/// The ground term a cell holds, for the closed/terminal cases where groundness is the
/// point (`[]`, or a shipped ground payload).
fn ground_at(heap: Heap, addr: Int) -> Option(Term) {
  case link_terms.ground_resolve(heap, VarRef(addr)) {
    Ok(#(_h, term)) -> Some(term)
    Error(_) -> None
  }
}

// ── applier isolation ────────────────────────────────────────────────────────

/// `Data` extends `In` by exactly one cons and advances the cursor: the SECOND term must
/// land on the fresh tail, not on the consumed cell. Getting this wrong is invisible on
/// a single message and corrupts every stream from the second onward.
pub fn data_extends_in_and_advances_the_cursor_test() {
  let id = loopback_id("chan-apply")
  let #(h, in_w, in_r) = heap.allocate_variable(heap.new())
  let links = registry_of(wired_handle(id, in_w))

  let first = ConstTerm(ConstAtom("one"))
  let link_pump.Applied(h, links, _woken) =
    link_pump.apply_item(h, links, link_pump.Data(id, 0, first))

  // The cursor moved off the now-bound cell.
  let assert Ok(handle) = link_registry.try_get(links, id)
  let assert Some(advanced) = handle.in_writer
  { advanced == in_w } |> should.be_false

  let second = ConstTerm(ConstAtom("two"))
  let link_pump.Applied(h, _links, _) =
    link_pump.apply_item(h, links, link_pump.Data(id, 1, second))

  // The reader now sees [one | Tail]; Tail derefs to [two | _] — both terms, in arrival
  // order, on the tail the cursor advanced to.
  let assert Some(StructTerm(".", [head, VarRef(tail)])) = value_at(h, in_r)
  head |> should.equal(first)
  let assert Some(StructTerm(".", [second_head, _])) = value_at(h, tail)
  second_head |> should.equal(second)
}

/// `Closed` ends the stream with `[]` and CLEARS the cursor, so a frame that was already
/// in flight cannot double-bind a consumed cell. A second `Closed` is a no-op.
pub fn close_ends_the_stream_and_clears_the_cursor_test() {
  let id = loopback_id("chan-close")
  let #(h, in_w, in_r) = heap.allocate_variable(heap.new())
  let links = registry_of(wired_handle(id, in_w))

  let link_pump.Applied(h, links, _) =
    link_pump.apply_item(h, links, link_pump.Closed(id))

  ground_at(h, in_r) |> should.equal(Some(ConstTerm(ConstAtom("nil"))))
  let assert Ok(handle) = link_registry.try_get(links, id)
  handle.in_writer |> should.equal(None)

  // Late data and a repeat close are both no-ops — no crash, no second binding.
  let link_pump.Applied(h2, links2, woken) =
    link_pump.apply_item(h, links, link_pump.Data(id, 9, ConstTerm(ConstAtom("late"))))
  woken |> should.equal([])
  ground_at(h2, in_r) |> should.equal(Some(ConstTerm(ConstAtom("nil"))))

  let link_pump.Applied(_, _, woken2) =
    link_pump.apply_item(h2, links2, link_pump.Closed(id))
  woken2 |> should.equal([])
}

/// An item naming a link the registry no longer holds (torn down while a frame was in
/// flight) is dropped, not an error.
pub fn item_for_an_unknown_link_is_a_no_op_test() {
  let #(h, _in_w, _) = heap.allocate_variable(heap.new())
  let link_pump.Applied(_, links, woken) =
    link_pump.apply_item(
      h,
      link_registry.new(),
      link_pump.Data(loopback_id("gone"), 0, ConstTerm(ConstAtom("x"))),
    )
  link_registry.count(links) |> should.equal(0)
  woken |> should.equal([])
}

/// `start` refuses before the ingress cursor is wired — the oracle's
/// `addLink before the In-stream ingress cursor was wired`. A frame arriving with no
/// cursor has nowhere to go, so this must be loud rather than a silent drop.
pub fn start_before_the_cursor_is_wired_is_refused_test() {
  let id = loopback_id("chan-unwired")
  let unwired = link_handle.new(id, a_dead_endpoint(id), link_options.default())
  let assert Error(_) = link_pump.start(link_pump.new_inbox(), unwired)
}

// ── end-to-end: peer ships → pump decodes → step_link ingests → link_recv wakes ──

/// The whole ingress path over a real loopback rendezvous.
///
/// The receiving end establishes through K1 (which wires the cursors and arms the pump),
/// then boots the SHIPPED `link_recv/3` (self.glp:568) on the `In` reader — it suspends,
/// because nothing has arrived. The peer then ships a ground term through the same
/// `link_wire` encode path `link_egress.ship_ground` uses. `step_link` drains the inbox,
/// extends `In`, wakes the goal, and the same step reduces it — which is exactly why
/// ingestion runs BEFORE the dequeue.
pub fn inbound_term_wakes_a_suspended_link_recv_test() {
  let t = loopback.new()
  let channel = "chan-c6-ingress"
  let id = loopback_id(channel)
  let shipped = StructTerm("hello", [ConstTerm(ConstAtom("world"))])
  let ready = process.new_subject()

  // The peer: connect to the same loopback channel, then HOLD until the test says ship.
  // The connect must race the listener's rendezvous, but the frame must not — otherwise
  // the term can land before `link_recv` has even been booted, and the suspend-then-wake
  // assertion below would silently test nothing. The peer owns its own `go` subject
  // (a BEAM subject may only be received on by its owner) and hands it back over `ready`.
  process.spawn(fn() {
    let go = process.new_subject()
    let assert Ok(ep) =
      t.connect(link_scheme.loopback(), link_address.path(channel), link_options.default())
    process.send(ready, go)
    let assert Ok(Nil) = process.receive(go, 5000)
    // Framed exactly as egress frames it: one Whole frame under the default None MTU.
    let assert Ok([frame]) = link_wire.encode_frames(shipped, 0, None)
    let assert Ok(Nil) = ep.send(frame)
  })

  // The receiving engine. The LinkState is built HERE so the test process owns the inbox.
  let state = link_runtime.new() |> link_runtime.with_transport(t)
  let #(h, in_w, in_r) = heap.allocate_variable(heap.new())
  let #(h, out_w, out_r) = heap.allocate_variable(h)
  let #(h, faults_w, _) = heap.allocate_variable(h)
  let assert Ok(link_kernels.LinkEffect(h, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_setup", 5, [
      loopback_id_term(channel),
      ConstTerm(ConstAtom("listener")),
      VarRef(in_w),
      VarRef(out_r),
      VarRef(faults_w),
    ])
  link_registry.contains(state.links, id) |> should.be_true

  // Boot `link_recv(Msg, ch(In?, Out), Ch1)` on the link's In READER (the program's half
  // of the cursor the kernel was handed).
  let assert Ok(prog) = loader.compile_prelude(read_source("../programs/self.glp"))
  let assert Ok(entry) = program.label_pc(prog, "link_recv/3")
  let #(h, msg_w, _) = heap.allocate_variable(h)
  let #(h, chan_w, _) = heap.allocate_variable(h)
  let regs =
    program.new_regs()
    |> program.set_reg(0, VarRef(msg_w))
    |> program.set_reg(1, StructTerm("ch", [VarRef(in_r), VarRef(out_w)]))
    |> program.set_reg(2, VarRef(chan_w))
  let #(engine, _) =
    scheduler.boot(scheduler.new(prog, h), "link_recv/3", entry, regs)

  // Nothing has arrived yet — the peer is holding — so the goal must SUSPEND on the In
  // reader, not fail and not reduce.
  let assert Ok(go) = process.receive(ready, 5000)
  let #(engine, first, state) = scheduler.step_link(engine, 1000, state)
  let assert scheduler.StepSuspended(_, "link_recv/3", _) = first
  ground_at(scheduler.heap(engine), msg_w) |> should.equal(None)

  // Now release the peer. The pump receives and decodes off the runner process; the next
  // `step_link` ingests, wakes the goal, and — because ingestion runs BEFORE the dequeue —
  // reduces it in that same step.
  process.send(go, Nil)
  let #(engine, _state) = run_link_until_reduced(engine, state, 50)

  ground_at(scheduler.heap(engine), msg_w) |> should.equal(Some(shipped))
}

/// Step until a reduction commits (the woken `link_recv`), or the budget runs out. A step
/// that finds neither queue work nor inbound input is idle — retry after parking briefly
/// on the inbox, because the peer's frame may still be crossing the loopback hub.
fn run_link_until_reduced(
  engine: scheduler.Engine,
  state: link_runtime.LinkState,
  budget: Int,
) -> #(scheduler.Engine, link_runtime.LinkState) {
  case budget <= 0 {
    True -> #(engine, state)
    False ->
      case scheduler.step_link(engine, 1000, state) {
        #(engine, scheduler.StepReduced(..), state) -> #(engine, state)
        #(engine, scheduler.StepIdle, state) -> {
          // Quiescent with nothing buffered: yield so the pump process can run, then
          // re-enter. Deliberately a SLEEP and not `drain_wait` — draining here would
          // take the item out of the mailbox and drop it, since only `step_link`'s own
          // ingest applies items to the heap.
          process.sleep(20)
          run_link_until_reduced(engine, state, budget - 1)
        }
        #(engine, _, state) -> run_link_until_reduced(engine, state, budget - 1)
      }
  }
}

/// A graceful peer FIN ends the `In` stream with `[]` all the way through the pump
/// process — the transport's `Ok(None)`, not a synthetic item.
pub fn peer_fin_ends_the_in_stream_test() {
  let t = loopback.new()
  let channel = "chan-c6-fin"
  let id = loopback_id(channel)

  process.spawn(fn() {
    let assert Ok(ep) =
      t.connect(link_scheme.loopback(), link_address.path(channel), link_options.default())
    ep.close()
  })

  let state = link_runtime.new() |> link_runtime.with_transport(t)
  let #(h, in_w, in_r) = heap.allocate_variable(heap.new())
  let #(h, _, out_r) = heap.allocate_variable(h)
  let #(h, faults_w, _) = heap.allocate_variable(h)
  let assert Ok(link_kernels.LinkEffect(h, state, _)) =
    link_kernels.link_dispatch(h, state, "_link_setup", 5, [
      loopback_id_term(channel),
      ConstTerm(ConstAtom("listener")),
      VarRef(in_w),
      VarRef(out_r),
      VarRef(faults_w),
    ])
  link_registry.contains(state.links, id) |> should.be_true

  let assert Ok(h) = drain_until_bound(h, state, in_r, 40)
  ground_at(h, in_r) |> should.equal(Some(ConstTerm(ConstAtom("nil"))))
}

/// Drain + apply the inbox directly (no scheduler needed — this test is about the pump
/// process, not the run loop) until `addr` is bound, or the budget runs out. Uses the
/// BLOCKING `drain_wait` (the oracle's `waitForInbound`), so this waits for the peer's
/// FIN to cross the hub instead of spinning past it.
fn drain_until_bound(
  h: Heap,
  state: link_runtime.LinkState,
  addr: Int,
  budget: Int,
) -> Result(Heap, Nil) {
  case budget <= 0 {
    True -> Error(Nil)
    False ->
      case value_at(h, addr) {
        Some(_) -> Ok(h)
        None -> {
          let h =
            link_pump.drain_wait(state.inbox, 100)
            |> apply_all(h, state.links)
          drain_until_bound(h, state, addr, budget - 1)
        }
      }
  }
}

fn apply_all(
  items: List(link_pump.InboundItem),
  h: Heap,
  links: link_registry.LinkRegistry,
) -> Heap {
  case items {
    [] -> h
    [item, ..rest] -> {
      let link_pump.Applied(h, links, _) = link_pump.apply_item(h, links, item)
      apply_all(rest, h, links)
    }
  }
}
