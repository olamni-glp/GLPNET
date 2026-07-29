//// glp/link/primitives/link_kernels — the effectful link host kernels + the
//// parallel effect outcome (T050.C2/C7/C8; contracts/link-primitives-port.md §4-§5).
////
//// Port of `glp_runtime/lib/link/primitives/link_kernels.dart` (+ the per-kernel
//// modules it registers) onto the Gleam engine's label-miss effect seam: the
//// runner dispatches here AFTER the pure `kernels.dispatch` misses, when the
//// reduction carries a `LinkState` (the exact `mad_kernels` shape — E5: a
//// PARALLEL outcome type, never a widened `KernelOutcome`).
////
//// Slice: `_link_setup/5` (K1, path A), `_link_monitor/2` (K6), `_link_close/2`
//// (K7). The path-B trio (`_link_request`/`_link_listen`/`_link_accept`) and the
//// LinkId-keyed sender `_link_send/3` (K2) are later steps (C4/C5); their names
//// are recognized so an unwired call fails the goal loudly rather than crashing
//// the engine as an unresolved procedure.
////
//// A kernel abort prints `[ABORT] <who>: <why>` (the Dart `LinkEstablish.abort`
//// surface — FR-029's "reported explicitly, never silent") and fails the goal
//// NON-FATALLY.

import gleam/dict
import gleam/io
import gleam/list
import gleam/result
import glp/link/primitives/capability_gate
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime.{
  type LinkState, Connector, Cursors, Listener, LinkState,
}
import glp/link/primitives/link_terms.{RoleConnector, RoleListener}
import glp/link/primitives/transport_registry
import glp/link/seam/link_id.{type LinkId}
import glp/runtime/heap.{type Heap}
import glp/runtime/suspension.{type GoalRef}
import glp/runtime/terms.{type Term, VarRef}

/// The two-valued outcome of a link kernel — parallel to `kernels.KernelOutcome`
/// and `mad_kernels.MadOutcome`, over `LinkState`.
pub type LinkOutcome {
  /// Success: the (possibly heap-updated) `LinkState` + any reactivated goals.
  LinkEffect(heap: Heap, state: LinkState, woken: List(GoalRef))
  /// A non-fatal abort — the enclosing goal fails, the engine continues.
  LinkAbort(detail: String)
}

/// Is `name/arity` a link host kernel? (The full ratified seven — recognized
/// even where the arm is a later slice, so a miss fails the GOAL, not the run.)
pub fn is_kernel(name: String, arity: Int) -> Bool {
  case name, arity {
    "_link_setup", 5 -> True
    "_link_send", 3 -> True
    "_link_request", 5 -> True
    "_link_listen", 3 -> True
    "_link_accept", 5 -> True
    "_link_monitor", 2 -> True
    "_link_close", 2 -> True
    _, _ -> False
  }
}

/// Dispatch a link kernel over `args`, given the current `LinkState`.
/// `Error(Nil)` means `name/arity` is not a link kernel (the runner then falls
/// through to the madGLP seam / the unresolved-Spawn report).
pub fn dispatch(
  heap: Heap,
  state: LinkState,
  name: String,
  arity: Int,
  args: List(Term),
) -> Result(LinkOutcome, Nil) {
  case name, arity, args {
    "_link_setup", 5, [id_t, role_t, in_t, out_t, faults_t] ->
      Ok(link_setup(heap, state, id_t, role_t, in_t, out_t, faults_t))
    "_link_monitor", 2, [id_t, faults_t] ->
      Ok(link_monitor(heap, state, id_t, faults_t))
    "_link_close", 2, [id_t, reason_t] ->
      Ok(link_close(heap, state, id_t, reason_t))
    "_link_send", 3, _ ->
      Ok(abort("'_link_send'/3", "not yet wired in the Gleam engine (C5)"))
    "_link_request", 5, _ ->
      Ok(abort("'_link_request'/5", "not yet wired in the Gleam engine (C4)"))
    "_link_listen", 3, _ ->
      Ok(abort("'_link_listen'/3", "not yet wired in the Gleam engine (C4)"))
    "_link_accept", 5, _ ->
      Ok(abort("'_link_accept'/5", "not yet wired in the Gleam engine (C4)"))
    _, _, _ -> Error(Nil)
  }
}

// ── K1: '_link_setup'/5 — establish-or-refuse in a given role (path A) ───────
//
// Arguments (ground-guarded by the GLP wrapper): id, role (listener|connector),
// In writer, Out? reader, Faults writer. Wires the cursors + registers the
// pending link SYNCHRONOUSLY (so a same-id re-setup aborts idempotently while
// this one is still connecting), then spawns the per-link rendezvous/pump
// process and returns — the scheduler never blocks (a symmetric bidi
// establishment would deadlock otherwise; the Dart mirror kicks its async
// connect off unawaited for the same reason).

fn link_setup(
  heap: Heap,
  state: LinkState,
  id_t: Term,
  role_t: Term,
  in_t: Term,
  out_t: Term,
  faults_t: Term,
) -> LinkOutcome {
  let who = "'_link_setup'/5"
  let parsed = {
    use id_g <- result.try(link_terms.ground_resolve(heap, id_t))
    use id <- result.try(link_terms.parse_link_id(id_g))
    use role_g <- result.try(link_terms.ground_resolve(heap, role_t))
    use role <- result.try(link_terms.parse_role(role_g))
    Ok(#(id, role))
  }
  case parsed {
    Error(why) -> abort(who, why)
    Ok(#(id, role)) ->
      case cursor_holes(heap, in_t, out_t, faults_t) {
        Error(why) -> abort(who, why)
        Ok(#(in_writer, out_writer, faults_writer)) ->
          // Idempotency at link-identity (FR-007): re-establishment of the
          // same ground LinkId is refused — cell-aliasing unspecified,
          // surfaced not guessed. Checked BEFORE any wiring or spawn.
          case dict.has_key(state.cursors, id) {
            True ->
              abort(
                who,
                "re-establishment of an already-established link: "
                  <> "cell-aliasing unspecified (FR-007) — first only",
              )
            False ->
              case transport_registry.select(state.transports, id.scheme) {
                Error(why) -> abort(who, why)
                Ok(transport) -> {
                  // Capability gate: verify-before-act, fail-closed (D-7 —
                  // allow-all by default in the base scope).
                  let gate = capability_gate.gate_for(state.gates, id.scheme)
                  case gate.gate_establish(id) {
                    False ->
                      abort(
                        who,
                        "capability refused: establishment gate denied",
                      )
                    True -> {
                      let cursors =
                        Cursors(
                          in_writer: in_writer,
                          out_writer: out_writer,
                          faults_cursors: [faults_writer],
                          established: False,
                          closed: False,
                        )
                      let state =
                        LinkState(
                          ..state,
                          cursors: dict.insert(state.cursors, id, cursors),
                        )
                      let role = case role {
                        RoleListener -> Listener
                        RoleConnector -> Connector
                      }
                      link_runtime.spawn_establish(state, transport, id, role)
                      LinkEffect(heap, state, [])
                    }
                  }
                }
              }
          }
      }
  }
}

// ── K6: '_link_monitor'/2 — an independent per-link fault-monitor stream ─────

fn link_monitor(
  heap: Heap,
  state: LinkState,
  id_t: Term,
  faults_t: Term,
) -> LinkOutcome {
  let who = "'_link_monitor'/2"
  let parsed = {
    use id_g <- result.try(link_terms.ground_resolve(heap, id_t))
    link_terms.parse_link_id(id_g)
  }
  case parsed {
    Error(why) -> abort(who, why)
    Ok(id) ->
      case writer_hole(heap, faults_t, "Faults") {
        Error(why) -> abort(who, why)
        Ok(faults_writer) ->
          case dict.get(state.cursors, id) {
            Error(_) -> abort(who, "monitor of an unknown link")
            Ok(c) -> {
              let c =
                Cursors(..c, faults_cursors: [
                  faults_writer,
                  ..c.faults_cursors
                ])
              LinkEffect(
                heap,
                LinkState(..state, cursors: dict.insert(state.cursors, id, c)),
                [],
              )
            }
          }
      }
  }
}

// ── K7: '_link_close'/2 — abrupt teardown + terminal closed(LinkId, Reason) ──

fn link_close(
  heap: Heap,
  state: LinkState,
  id_t: Term,
  reason_t: Term,
) -> LinkOutcome {
  let who = "'_link_close'/2"
  let parsed = {
    use id_g <- result.try(link_terms.ground_resolve(heap, id_t))
    use id <- result.try(link_terms.parse_link_id(id_g))
    use reason_g <- result.try(link_terms.ground_resolve(heap, reason_t))
    use reason <- result.try(link_terms.parse_reason(reason_g))
    Ok(#(id, reason))
  }
  case parsed {
    Error(why) -> abort(who, why)
    Ok(#(id, reason)) ->
      case dict.get(state.cursors, id) {
        Error(_) -> abort(who, "close of an unknown link")
        Ok(c) ->
          case c.closed {
            // Close is idempotent at teardown: a second close is a no-op
            // success (the terminal closed/2 already went out).
            True -> LinkEffect(heap, state, [])
            False -> teardown(heap, state, id, c, reason)
          }
      }
  }
}

/// The shared teardown core (K7 + the egress drain's graceful `Out = []` path):
/// close the transport endpoint (if established), emit the terminal
/// `closed(LinkId, Reason)` on EVERY live monitor cursor, mark the link closed,
/// and reclaim its registry entry (the base distributed-GC surface, FR-024).
pub fn teardown(
  heap: Heap,
  state: LinkState,
  id: LinkId,
  c: link_runtime.Cursors,
  reason: String,
) -> LinkOutcome {
  case link_registry.get(state.registry, id) {
    Ok(handle) -> handle.endpoint.close()
    Error(_) -> Nil
  }
  let terminal = link_terms.closed_term(id, reason)
  case emit_fault(heap, c.faults_cursors, terminal) {
    Error(why) -> abort("'_link_close'/2", why)
    Ok(#(heap, new_cursors, woken)) -> {
      let c =
        Cursors(..c, faults_cursors: new_cursors, closed: True)
      LinkEffect(
        heap,
        LinkState(
          ..state,
          registry: link_registry.remove(state.registry, id),
          cursors: dict.insert(state.cursors, id, c),
        ),
        woken,
      )
    }
  }
}

/// Fan one fault term out to every live monitor cursor: bind each cursor writer
/// to `[fault | NewTail]` and advance the cursor to the fresh tail writer, so
/// the stream stays open for the next fault. Returns the advanced heap, the new
/// cursor writers, and every goal the binds woke.
pub fn emit_fault(
  heap: Heap,
  cursors: List(Int),
  fault: Term,
) -> Result(#(Heap, List(Int), List(GoalRef)), String) {
  list.try_fold(cursors, #(heap, [], []), fn(acc, cursor) {
    let #(heap, new_cursors, woken) = acc
    let #(heap, new_writer, new_reader) = heap.allocate_variable(heap)
    case heap.bind_writer(heap, cursor, terms.cons(fault, VarRef(new_reader))) {
      Error(_) ->
        Error("fault-monitor cursor already bound — cursor discipline broken")
      Ok(#(heap, w)) ->
        Ok(#(heap, [new_writer, ..new_cursors], list.append(woken, w)))
    }
  })
}

// ── argument-hole validation (the Dart wireEstablishedLink checks) ───────────

fn cursor_holes(
  heap: Heap,
  in_t: Term,
  out_t: Term,
  faults_t: Term,
) -> Result(#(Int, Int, Int), String) {
  use in_writer <- result.try(writer_hole(heap, in_t, "In"))
  use out_writer <- result.try(reader_paired_writer(heap, out_t))
  use faults_writer <- result.try(writer_hole(heap, faults_t, "Faults"))
  Ok(#(in_writer, out_writer, faults_writer))
}

fn writer_hole(heap: Heap, t: Term, what: String) -> Result(Int, String) {
  case t {
    VarRef(addr) ->
      case heap.is_writer(heap, addr) {
        True -> Ok(addr)
        False -> Error(what <> " must be an unbound writer cell")
      }
    _ -> Error(what <> " must be an unbound writer cell")
  }
}

fn reader_paired_writer(heap: Heap, t: Term) -> Result(Int, String) {
  case t {
    VarRef(addr) ->
      case heap.is_reader(heap, addr) {
        False -> Error("Out? must be an unbound reader cell")
        True ->
          heap.paired_writer(heap, addr)
          |> result.replace_error("Out reader has no paired writer to drain")
      }
    _ -> Error("Out? must be an unbound reader cell")
  }
}

fn abort(who: String, why: String) -> LinkOutcome {
  io.println("[ABORT] " <> who <> ": " <> why)
  LinkAbort(who <> ": " <> why)
}
