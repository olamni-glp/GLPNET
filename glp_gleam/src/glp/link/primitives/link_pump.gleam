//// glp/link/primitives/link_pump — the inbound pump + egress drainer that drives a
//// link goal to a real round-trip (feature 059, T074 `close-link-inbound-pump`; port
//// of the async data-flow of glp_runtime/lib/link/primitives/link_pump.dart +
//// link_egress.dart's `Out`-drainer).
////
//// The pure scheduler runs a goal to quiescence and stops; a link goal then SUSPENDS
//// on its inbound `In` stream (the consumer) with its outbound `Out` stream produced
//// (the producer) — neither can advance without host I/O. This driver is that host
//// I/O, run as one synchronous loop over the (blocking) transport seam:
////
////   1. run to quiescence with `_link_*` kernels active (`scheduler.run_link`);
////   2. EGRESS — for every open link, ship each bound `Out` cons head over the
////      endpoint (`link_egress.ship_ground` → `endpoint.send`) and close the send
////      half on `Out = []` (graceful eos);
////   3. INGRESS — if a link has a consumer blocked on its `In` stream, BLOCK on
////      `endpoint.recv`, extend `In := [term | In']` (or `In := []` + a terminal
////      `closed(LinkId, eos)` fault on peer eos), reactivate the consumer, and
////      re-drive from step 1.
////
//// Draining `Out` this way — the pump walking the bound prefix between re-drives — is
//// the faithful stand-in for the Dart `heap.onBind(outWriter, …)` egress drainer the
//// Gleam heap has no equivalent of (the handoff note in link_kernels.gleam). All heap
//// mutation flows through `scheduler.bind_and_wake` (the same reactivation path as
//// madGLP Receive), so a delivered frame / fault is an ORDINARY stream bind — never a
//// fourth unification verdict (FR-043). Fragment reassembly + dedup are T077; this
//// slice handles the `Whole`-frame MVP the acceptance programs produce.

import gleam/list
import gleam/option.{None, Some}
import glp/engine/scheduler.{type Engine, type RunStatus}
import glp/link/primitives/link_egress
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime.{type LinkRuntime}
import glp/link/primitives/link_terms
import glp/link/primitives/link_wire
import glp/link/seam/endpoint.{type Endpoint}
import glp/link/seam/link_fault
import glp/runtime/heap
import glp/runtime/terms.{type Term, ConstTerm, StructTerm, VarRef, cons, nil}

/// Bound on pump cycles (a run-away guard; each cycle either delivers one inbound
/// frame or terminates). Generous vs the acceptance programs' handful of values.
const max_cycles = 100_000

/// Drive a booted link goal to completion. Returns the advanced scheduler engine, the
/// final link runtime, and the terminal `RunStatus` (Success when every consumer's
/// inbound stream reached eos and its goal reduced; Suspended if a non-link reader is
/// still blocked). `budget`/`fuel` bound each inner `run_link`.
pub fn drive(
  engine: Engine,
  runtime: LinkRuntime,
  budget: Int,
  fuel: Int,
) -> #(Engine, LinkRuntime, RunStatus) {
  loop(engine, runtime, budget, fuel, max_cycles)
}

fn loop(
  engine: Engine,
  runtime: LinkRuntime,
  budget: Int,
  fuel: Int,
  cycles: Int,
) -> #(Engine, LinkRuntime, RunStatus) {
  case cycles <= 0 {
    True -> #(engine, runtime, scheduler.status(engine))
    False -> {
      // 1. Reduce to quiescence with the link kernels active.
      let #(engine, status, runtime) =
        scheduler.run_link(engine, budget, fuel, runtime)
      // 2. Egress: ship every open link's bound Out prefix, close on [].
      let #(engine, runtime) = drain_all_egress(engine, runtime)
      // 3. Ingress: a consumer blocked on In → block on recv, bind, re-drive.
      case find_waiter(engine, runtime) {
        Some(handle) -> {
          let #(engine, runtime) = recv_and_bind(engine, runtime, handle)
          loop(engine, runtime, budget, fuel, cycles - 1)
        }
        None -> #(engine, runtime, status)
      }
    }
  }
}

// ── egress ─────────────────────────────────────────────────────────────────

fn drain_all_egress(
  engine: Engine,
  runtime: LinkRuntime,
) -> #(Engine, LinkRuntime) {
  let handles = link_registry.handles(runtime.links)
  let links =
    list.fold(handles, runtime.links, fn(links, handle) {
      let drained = drain_one_egress(engine, handle)
      case link_registry.replace(links, handle.id, drained) {
        Ok(links) -> links
        Error(_) -> links
      }
    })
  #(engine, link_runtime.with_links(runtime, links))
}

/// Walk the bound prefix of `handle`'s `Out` stream, shipping each ground cons head
/// and closing the send half on `[]`. Stops at the first unbound tail (nothing more
/// to ship yet). No-op once the send half is closed.
fn drain_one_egress(engine: Engine, handle: LinkHandle) -> LinkHandle {
  case handle.out_closed, handle.endpoint, handle.out_reader {
    False, Some(ep), Some(out_reader) ->
      drain_value(engine, ep, handle, VarRef(out_reader))
    _, _, _ -> handle
  }
}

fn drain_value(
  engine: Engine,
  ep: Endpoint,
  handle: LinkHandle,
  value: Term,
) -> LinkHandle {
  case value {
    // Out = [] → graceful stream-end close of the send half (FR-010/024).
    ConstTerm(_) -> {
      let _ = ep.close()
      link_handle.LinkHandle(..handle, out_closed: True)
    }
    StructTerm(".", [head, tail]) -> {
      // Ground-relay ship (FR-010): serialize + frame; send each fragment.
      case link_egress.ship_ground(scheduler.heap(engine), handle, head) {
        Ok(#(handle, frames)) -> {
          list.each(frames, fn(frame) { ep.send(frame) })
          drain_value(engine, ep, handle, resolve(engine, tail))
        }
        // A non-ground head slipping past the wrapper's ground/1 guard: drop the
        // frame (the Dart channel-face behaviour) and stop draining.
        Error(_) -> handle
      }
    }
    VarRef(addr) ->
      case heap.deref(scheduler.heap(engine), addr) {
        Ok(#(_, heap.Bound(v))) -> drain_value(engine, ep, handle, v)
        // Unbound tail — nothing more produced yet; re-armed next cycle.
        _ -> handle
      }
    _ -> handle
  }
}

fn resolve(engine: Engine, t: Term) -> Term {
  case t {
    VarRef(addr) ->
      case heap.deref(scheduler.heap(engine), addr) {
        Ok(#(_, heap.Bound(v))) -> v
        _ -> t
      }
    _ -> t
  }
}

// ── ingress ──────────────────────────────────────────────────────────────────

/// The first open link with a consumer blocked on its inbound `In` stream (the
/// signal to block on `recv`). `None` when no link is awaiting input — the run is as
/// far as the links can carry it.
fn find_waiter(engine: Engine, runtime: LinkRuntime) -> option.Option(LinkHandle) {
  let blocked = scheduler.blocking_reader_addrs(engine)
  list.find(link_registry.handles(runtime.links), fn(handle) {
    case handle.in_closed, handle.endpoint, handle.in_writer {
      False, Some(_), Some(in_writer) -> {
        let reader = heap.paired_reader(scheduler.heap(engine), in_writer)
        list.contains(blocked, reader)
      }
      _, _, _ -> False
    }
  })
  |> option.from_result
}

/// Block on `handle`'s endpoint for the next inbound frame and extend the `In`
/// stream: `Some(frame)` → `In := [term | In']`; `None` (peer eos) → `In := []` +
/// terminal `closed(LinkId, eos)` on the monitor; `Error` → the fault term on the
/// monitor. Reactivates the consumer via `bind_and_wake`.
fn recv_and_bind(
  engine: Engine,
  runtime: LinkRuntime,
  handle: LinkHandle,
) -> #(Engine, LinkRuntime) {
  case handle.endpoint, handle.in_writer {
    Some(ep), Some(in_writer) ->
      case ep.recv() {
        Ok(Some(bytes)) ->
          case link_wire.decode_term_frame(bytes) {
            Ok(term) -> extend_in(engine, runtime, handle, in_writer, term)
            // Undecodable inbound frame → treat as a permanent fault (never silent).
            Error(why) ->
              close_in(engine, runtime, handle, in_writer, link_terms.perm_fail(
                handle.id,
                why,
              ))
          }
        // Peer cleanly ended the stream → In := [] + closed(LinkId, eos).
        Ok(None) ->
          close_in(engine, runtime, handle, in_writer, link_terms.closed(
            handle.id,
            link_terms.graceful_reason,
          ))
        // Transport fault → the refined lattice term on the monitor (FR-008/043/045).
        Error(signal) ->
          close_in(engine, runtime, handle, in_writer, fault_term(signal))
      }
    _, _ -> #(engine, runtime)
  }
}

/// `In := [term | In']`: allocate a fresh tail, bind the current In writer to the
/// cons, reactivate the consumer, and advance the handle's In cursor to In'.
fn extend_in(
  engine: Engine,
  runtime: LinkRuntime,
  handle: LinkHandle,
  in_writer: Int,
  term: Term,
) -> #(Engine, LinkRuntime) {
  let #(h2, fresh_writer, fresh_reader) =
    heap.allocate_variable(scheduler.heap(engine))
  let engine = scheduler.set_heap(engine, h2)
  let cell = cons(term, VarRef(fresh_reader))
  case scheduler.bind_and_wake(engine, in_writer, cell) {
    Ok(engine) -> {
      let handle = link_handle.LinkHandle(..handle, in_writer: Some(fresh_writer))
      #(engine, replace_handle(runtime, handle))
    }
    Error(_) -> #(engine, runtime)
  }
}

/// `In := []` (peer eos): bind the In writer to nil, reactivate the consumer's `[]`
/// clause, deliver the terminal `fault` term to every monitor cursor, and mark the
/// inbound stream closed.
fn close_in(
  engine: Engine,
  runtime: LinkRuntime,
  handle: LinkHandle,
  in_writer: Int,
  fault: Term,
) -> #(Engine, LinkRuntime) {
  let engine = case scheduler.bind_and_wake(engine, in_writer, nil()) {
    Ok(engine) -> engine
    Error(_) -> engine
  }
  let #(engine, handle) = deliver_fault(engine, handle, fault)
  let handle = link_handle.LinkHandle(..handle, in_closed: True)
  #(engine, replace_handle(runtime, handle))
}

/// Fan `fault` out to EVERY monitor cursor of `handle` (the establishment `Faults`
/// stream + any `link_monitor` stream — FR-008): allocate a fresh tail per cursor,
/// bind the cursor to `[fault | tail]`, reactivate its reader, advance the cursor.
fn deliver_fault(
  engine: Engine,
  handle: LinkHandle,
  fault: Term,
) -> #(Engine, LinkHandle) {
  let #(engine, new_cursors) =
    list.fold(handle.monitor_cursors, #(engine, []), fn(acc, cursor) {
      let #(engine, cursors) = acc
      let #(h2, fresh_writer, fresh_reader) =
        heap.allocate_variable(scheduler.heap(engine))
      let engine = scheduler.set_heap(engine, h2)
      let cell = cons(fault, VarRef(fresh_reader))
      let engine = case scheduler.bind_and_wake(engine, cursor, cell) {
        Ok(engine) -> engine
        Error(_) -> engine
      }
      #(engine, [fresh_writer, ..cursors])
    })
  #(engine, link_handle.LinkHandle(..handle, monitor_cursors: list.reverse(new_cursors)))
}

fn replace_handle(runtime: LinkRuntime, handle: LinkHandle) -> LinkRuntime {
  case link_registry.replace(runtime.links, handle.id, handle) {
    Ok(links) -> link_runtime.with_links(runtime, links)
    Error(_) -> runtime
  }
}

/// Map a seam `LinkFaultSignal` to its GLP lattice term.
fn fault_term(signal: link_fault.LinkFaultSignal) -> Term {
  link_terms.from_signal(signal)
}
