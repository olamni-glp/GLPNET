//// glp/link/primitives/link_loop — the link-aware run-to-quiescence loop
//// (T050.C5/C6; contracts/link-primitives-port.md D-2/D-3).
////
//// The Gleam analogue of the Dart runtime's "run-to-quiescence stays live while
//// link I/O is outstanding" (`rt.inboundPump` + the egress onBind chain): drive
//// the scheduler to quiescence, DRAIN the egress (ship every ground head the
//// program bound onto a link's Out chain — the D-2 no-`onBind` choice: a drain
//// pass at the quiescence seam, inside the loop, rather than a bespoke bind
//// hook), then — while a link is still establishing, or goals are suspended
//// with a live link — BLOCK on the link subject and apply what the per-link
//// processes report (established endpoints, decoded inbound payloads, peer
//// closes, faults) before re-running. A receive timeout is the bounded give-up
//// (SC-007: no scenario blocks indefinitely).

import gleam/erlang/process
import gleam/io
import gleam/list
import gleam/result
import glp/engine/scheduler.{type RunStatus, Suspended}
import glp/link/primitives/link_egress
import glp/link/primitives/link_kernels
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime.{
  type Cursors, type LinkState, Cursors, LinkEstablishFailed, LinkEstablished,
  LinkInbound, LinkPeerClosed, LinkPumpFault, LinkState,
}
import glp/link/primitives/link_terms
import glp/link/primitives/payload_codec
import glp/link/seam/link_id.{type LinkId}
import glp/link/seam/link_options
import glp/runtime/heap
import glp/runtime/terms.{type Term, ConstAtom, ConstTerm, StructTerm, VarRef}

import glp/link/primitives/link_handle

import gleam/dict

/// The bounded wait for link traffic once the scheduler is quiescent (ms).
/// Longer than the transport connect budget (15 s), so an establishment
/// failure always arrives as a message before this fires; hitting it means a
/// live link went silent — give up rather than block forever (SC-007).
const wait_timeout_ms = 30_000

/// Drive `sched` to link-aware quiescence (see module header). Returns the
/// final scheduler + status, shaped exactly like `scheduler.run` so the engine
/// facade substitutes it transparently when transports are injected.
pub fn drive(
  sched: scheduler.Engine,
  reduction_budget: Int,
  fuel: Int,
  state: LinkState,
) -> #(scheduler.Engine, RunStatus) {
  let #(sched, status, state) =
    scheduler.run_link(sched, reduction_budget, fuel, state)
  let #(sched, state) = drain_egress(sched, state)
  case should_wait(status, state) {
    False -> #(sched, status)
    True ->
      case process.receive(state.subject, wait_timeout_ms) {
        // Bounded give-up: a live link went silent past the perm bound.
        Error(Nil) -> #(sched, status)
        Ok(msg) -> {
          let #(sched, state) = apply_msg(sched, state, msg)
          drive(sched, reduction_budget, fuel, state)
        }
      }
  }
}

/// Wait while a link is still establishing (its egress cannot drain and its
/// pump cannot start until the rendezvous resolves — even when every goal
/// already reduced), while a graceful close handshake is still in flight (the
/// D-9 run-termination barrier — returning would halt the VM with the pump
/// still in `recv`, closing the socket abortively and truncating the shipped
/// tail), or while suspended goals could be unblocked by inbound link traffic.
fn should_wait(status: RunStatus, state: LinkState) -> Bool {
  case
    link_runtime.has_pending_establish(state)
    || link_runtime.has_unfinished_close(state)
  {
    True -> True
    False ->
      case status {
        Suspended(_) -> link_runtime.has_live(state)
        _ -> False
      }
  }
}

// ── subject-message application ──────────────────────────────────────────────

fn apply_msg(
  sched: scheduler.Engine,
  state: LinkState,
  msg: link_runtime.LinkMsg,
) -> #(scheduler.Engine, LinkState) {
  case msg {
    LinkEstablished(id, endpoint) ->
      case link_handle.new(id, link_options.default(), endpoint) {
        Error(reason) ->
          fault_out(
            sched,
            state,
            id,
            link_terms.perm_fail_term(id, "handle construction failed: " <> reason),
            close: True,
          )
        Ok(handle) -> {
          let state =
            LinkState(
              ..state,
              registry: link_registry.put(state.registry, handle),
            )
          #(sched, with_cursors(state, id, fn(c) { Cursors(..c, established: True) }))
        }
      }
    LinkEstablishFailed(id, signal) ->
      fault_out(sched, state, id, link_terms.from_signal(signal), close: True)
    LinkPumpFault(id, signal) ->
      fault_out(sched, state, id, link_terms.from_signal(signal), close: False)
    LinkPeerClosed(id) -> {
      // The peer cleanly ended THEIR sender (half-close, D-9): end the
      // program's In with `[]` and emit closed(LinkId, eos) on the monitors —
      // but the link goes terminal ONLY once our own sender has also closed.
      // Marking it closed here would suppress our un-drained egress (the
      // drain gate skips closed links) and let the run return with frames
      // still un-shipped — the graceful-close truncation this fixes.
      let #(sched, state) =
        fault_out(
          sched,
          state,
          id,
          link_terms.closed_term(id, link_terms.graceful_reason),
          close: False,
        )
      let state =
        with_cursors(state, id, fn(c) {
          Cursors(..c, in_ended: True, closed: c.closed || c.out_closed)
        })
      case dict.get(state.cursors, id) {
        Error(_) -> #(sched, state)
        Ok(c) ->
          case scheduler.bind_and_wake(sched, c.in_writer, terms.nil()) {
            Ok(sched) -> #(sched, state)
            // Already bound (a prior close ended the stream) — nothing to do.
            Error(_) -> #(sched, state)
          }
      }
    }
    LinkInbound(id, payloads) ->
      list.fold(payloads, #(sched, state), fn(acc, payload) {
        let #(sched, state) = acc
        apply_inbound(sched, state, id, payload)
      })
  }
}

/// Deliver one decoded payload: build its ground term and extend the program's
/// In stream — bind the In cursor writer to `[term | NewTail]`, advance the
/// cursor, and let the binding wake the suspended `link_recv` (the ordinary
/// suspension machinery — no bespoke wake path).
fn apply_inbound(
  sched: scheduler.Engine,
  state: LinkState,
  id: LinkId,
  payload: BitArray,
) -> #(scheduler.Engine, LinkState) {
  case dict.get(state.cursors, id) {
    Error(_) -> #(sched, state)
    Ok(c) ->
      case payload_codec.decode_ground(payload) {
        Error(why) ->
          fault_out(
            sched,
            state,
            id,
            link_terms.temp_fail_term(id, why),
            close: False,
          )
        Ok(term) -> {
          let h = scheduler.heap(sched)
          let #(h, new_writer, new_reader) = heap.allocate_variable(h)
          let sched = scheduler.set_heap(sched, h)
          case
            scheduler.bind_and_wake(
              sched,
              c.in_writer,
              terms.cons(term, VarRef(new_reader)),
            )
          {
            Error(why) -> {
              io.println("[link ingress] " <> why)
              #(sched, state)
            }
            Ok(sched) -> #(
              sched,
              with_cursors(state, id, fn(c) {
                Cursors(..c, in_writer: new_writer)
              }),
            )
          }
        }
      }
  }
}

/// Emit one fault term on every live monitor cursor of `id` (via the shared
/// kernel fan-out) and optionally mark the link closed.
fn fault_out(
  sched: scheduler.Engine,
  state: LinkState,
  id: LinkId,
  fault: Term,
  close close_link: Bool,
) -> #(scheduler.Engine, LinkState) {
  case dict.get(state.cursors, id) {
    Error(_) -> #(sched, state)
    Ok(c) -> {
      let h = scheduler.heap(sched)
      case link_kernels.emit_fault(h, c.faults_cursors, fault) {
        Error(why) -> {
          io.println("[link faults] " <> why)
          #(sched, state)
        }
        Ok(#(h, new_cursors, woken)) -> {
          let sched =
            scheduler.set_heap(sched, h) |> scheduler.wake_all(woken)
          let c =
            Cursors(
              ..c,
              faults_cursors: new_cursors,
              closed: c.closed || close_link,
            )
          #(
            sched,
            LinkState(..state, cursors: dict.insert(state.cursors, id, c)),
          )
        }
      }
    }
  }
}

fn with_cursors(
  state: LinkState,
  id: LinkId,
  update: fn(Cursors) -> Cursors,
) -> LinkState {
  case dict.get(state.cursors, id) {
    Error(_) -> state
    Ok(c) ->
      LinkState(..state, cursors: dict.insert(state.cursors, id, update(c)))
  }
}

// ── egress drain (D-2: the no-onBind choice) ─────────────────────────────────

/// Ship every ground head the program has bound onto each established link's
/// Out chain, in chain (= submission) order; `Out = []` is the graceful
/// stream-end teardown. Unestablished links keep their bound chain — it drains
/// on a later pass, right after `LinkEstablished` lands.
fn drain_egress(
  sched: scheduler.Engine,
  state: LinkState,
) -> #(scheduler.Engine, LinkState) {
  dict.fold(state.cursors, #(sched, state), fn(acc, id, _c) {
    let #(sched, state) = acc
    // Re-read the cursor — an earlier fold step never touches another link's
    // cursors, but the state value advances.
    case dict.get(state.cursors, id) {
      Error(_) -> #(sched, state)
      Ok(c) ->
        case c.established && !c.closed && !c.out_closed {
          False -> #(sched, state)
          True -> drain_chain(sched, state, id, c.out_writer)
        }
    }
  })
}

fn drain_chain(
  sched: scheduler.Engine,
  state: LinkState,
  id: LinkId,
  cursor: Int,
) -> #(scheduler.Engine, LinkState) {
  let h = scheduler.heap(sched)
  case heap.deref(h, cursor) {
    // Not yet bound — the drain resumes here next pass.
    Ok(#(_, heap.Unbound(_))) -> #(
      sched,
      with_cursors(state, id, fn(c) { Cursors(..c, out_writer: cursor) }),
    )
    Error(_) -> #(sched, state)
    Ok(#(_, heap.Bound(v))) -> drain_value(sched, state, id, v)
  }
}

fn drain_value(
  sched: scheduler.Engine,
  state: LinkState,
  id: LinkId,
  value: Term,
) -> #(scheduler.Engine, LinkState) {
  let h = scheduler.heap(sched)
  case value {
    // `Out = []` → graceful stream-end close (FR-010): same teardown as
    // '_link_close' with reason `eos`.
    ConstTerm(ConstAtom("nil")) -> graceful_close(sched, state, id)
    StructTerm(".", [head, tail]) ->
      case ship_one(sched, state, id, head) {
        Error(#(sched, state)) -> #(sched, state)
        Ok(#(sched, state)) ->
          // Advance to the tail: through a reader to its paired writer (the
          // wrapper conses `[Msg? | Out?]`), or a writer chain directly.
          case tail {
            VarRef(taddr) ->
              case heap.is_reader(h, taddr) {
                True ->
                  case heap.paired_writer(h, taddr) {
                    Ok(w) -> drain_chain(sched, state, id, w)
                    Error(_) -> #(sched, state)
                  }
                False -> drain_chain(sched, state, id, taddr)
              }
            ConstTerm(ConstAtom("nil")) -> graceful_close(sched, state, id)
            StructTerm(".", _) -> drain_value(sched, state, id, tail)
            _ -> #(sched, state)
          }
      }
    // A writer bound onto another variable — follow the chain.
    VarRef(addr) ->
      case heap.is_reader(h, addr) {
        True ->
          case heap.paired_writer(h, addr) {
            Ok(w) -> drain_chain(sched, state, id, w)
            Error(_) -> #(sched, state)
          }
        False -> drain_chain(sched, state, id, addr)
      }
    // Not a stream cons — nothing to ship (defensive; the wrapper only conses).
    _ -> #(sched, state)
  }
}

/// Ship one ground head on the link. `Ok` advances; `Error` carries the state
/// after surfacing the problem — the chain drain stops for this pass.
fn ship_one(
  sched: scheduler.Engine,
  state: LinkState,
  id: LinkId,
  head: Term,
) -> Result(
  #(scheduler.Engine, LinkState),
  #(scheduler.Engine, LinkState),
) {
  let h = scheduler.heap(sched)
  let ground =
    link_terms.ground_resolve(h, head)
    |> result.try(payload_codec.encode_ground)
  case ground {
    Error(why) -> {
      // Ground-relay gate violated / unencodable — dropped LOUDLY, never a
      // partial term on the wire (the Dart egress prints the same surface).
      io.println("[link egress] " <> why <> " — frame dropped")
      Error(#(sched, state))
    }
    Ok(payload) ->
      case link_registry.get(state.registry, id) {
        Error(_) -> Error(#(sched, state))
        Ok(handle) ->
          case link_egress.ship(handle, payload) {
            Ok(handle) ->
              Ok(#(
                sched,
                LinkState(
                  ..state,
                  registry: link_registry.put(state.registry, handle),
                ),
              ))
            Error(link_egress.EgressWouldBlock) ->
              // Window full: the producer parks; the chain resumes next pass.
              Error(#(sched, state))
            Error(link_egress.EgressEncodeError(detail)) -> {
              let #(sched, state) =
                fault_out(
                  sched,
                  state,
                  id,
                  link_terms.temp_fail_term(id, detail),
                  close: False,
                )
              Error(#(sched, state))
            }
            Error(link_egress.EgressFault(signal)) -> {
              let #(sched, state) =
                fault_out(
                  sched,
                  state,
                  id,
                  link_terms.from_signal(signal),
                  close: False,
                )
              Error(#(sched, state))
            }
          }
      }
  }
}

/// The graceful `Out = []` SENDER close (FR-010): half-close the endpoint (the
/// transport's `close` is a shutdown-write — the peer's recv ends with EOS) and
/// stop draining. RECEIVING CONTINUES — closing our own sender never stops the
/// inbound side of a bilateral link; the link goes terminal only on the peer's
/// end-of-stream (`LinkPeerClosed`), a `_link_close`, or a permanent fault.
fn graceful_close(
  sched: scheduler.Engine,
  state: LinkState,
  id: LinkId,
) -> #(scheduler.Engine, LinkState) {
  case link_registry.get(state.registry, id) {
    Ok(handle) -> handle.endpoint.close()
    Error(_) -> Nil
  }
  // Both directions ended → the link is terminal (D-9); with only our side
  // closed, `has_unfinished_close` keeps the loop waiting for the peer's FIN.
  #(
    sched,
    with_cursors(state, id, fn(c) {
      Cursors(..c, out_closed: True, closed: c.closed || c.in_ended)
    }),
  )
}
