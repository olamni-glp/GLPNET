//// glp/mad/mad_kernels — the madGLP effectful body kernel `_send` and the parallel
//// effect seam `MadOutcome` (feature 050, T050.A2).
////
//// The pure engine's body kernels are `KernelOutcome{KSuccess|KAbort}` over the HEAP
//// alone (kernels.gleam). `_send` is effectful over madGLP STATE (W_p + M_p), so it
//// gets a PARALLEL outcome type here — NOT a widened `KernelOutcome` (which would
//// touch ~30 dispatch arms + runner + scheduler; escalation E5 RATIFIED 2026-07-14
//// §1.14). The runner dispatches `_send` at the BODY Spawn label-miss (runner.gleam),
//// AFTER the pure `kernels.dispatch` misses, when the reduction carries a `MadState`.
////
//// Frozen semantics — faithful port of `body_kernels.dart` `sendKernel` +
//// `mad_context.dart` `send` (spec §11.5):
////   '_send'(T, G, Q): globalize T for destination Q (mints W_p entries for writers,
////   emits `global_send` spawns for readers — T050.A1 `globalize`), then add ONE
////   outgoing message to M_p:
////     - G = `_w(q,0)` (serializer, index 0): `_w(q,0) := [T↑ | _w(q,0)]` (wrap in a
////       list cell, reuse the serializer writer in the tail — spec §4.1);
////     - G = `_w/_r(_, i)` with i>0 (normal): `G := T↑` (sent directly).
////   Aborts (NON-FATAL — the goal fails, the engine keeps running; spec-v5.3-PURE,
////   send reliability deferred to T052) unless G is a `_w/2`/`_r/2` name and Q is
////   ground.

import gleam/list
import glp/mad/globalize.{type Spawn, collect_vars, globalize, globalize_term}
import glp/mad/global_name.{WriterName, of_term, to_term}
import glp/mad/global_writers_table.{type WritersTable, owner}
import glp/mad/message.{type Message, Message}
import glp/runtime/heap.{type Heap, Bound, Unbound, deref, is_reader}
import glp/runtime/suspension.{type GoalRef}
import glp/runtime/terms.{type Term, StructTerm, VarRef, cons}

/// The madGLP effect state threaded through a reduction. `w_p` also carries the owner
/// agent p and the single shared index counter (T050.A0 embedded the counter IN W_p,
/// so it is NOT a separate field — one source, per §3.2). `mad_spawns` accumulates the
/// reader-branch `global_send` goals globalization emits; A3 lowers them to runnable
/// `SpawnReq`s once the prelude's `global_send` label exists.
pub type MadState {
  MadState(w_p: WritersTable, m_p: List(Message), mad_spawns: List(Spawn))
}

/// The two-valued outcome of a madGLP effectful kernel — parallel to
/// `kernels.KernelOutcome`, but over `MadState` not just the heap.
pub type MadOutcome {
  /// Success: the (possibly heap-updated) `MadState` + any reactivated goals.
  MadEffect(heap: Heap, state: MadState, woken: List(GoalRef))
  /// A non-fatal abort — the enclosing goal fails, the engine continues (NOT the
  /// fatal `KAbort→RunnerError` path).
  MadAbort(detail: String)
}

/// Is `name/arity` a registered madGLP effectful kernel? (`_now` remains for a later
/// slice; only `_send` is A2.)
pub fn mad_is_kernel(name: String, arity: Int) -> Bool {
  case name, arity {
    "_send", 3 -> True
    _, _ -> False
  }
}

/// Dispatch a madGLP effectful kernel over `args`, given the current `MadState`.
/// `Error(Nil)` means `name/arity` is not a mad kernel (the runner then reports the
/// unresolved Spawn, exactly as for a missed pure kernel).
pub fn mad_dispatch(
  heap: Heap,
  state: MadState,
  name: String,
  arity: Int,
  args: List(Term),
) -> Result(MadOutcome, Nil) {
  case name, arity, args {
    "_send", 3, [t, g, q] -> Ok(send_kernel(heap, state, t, g, q))
    _, _, _ -> Error(Nil)
  }
}

/// `'_send'(T, G, Q)` — spec §11.5. See module header.
fn send_kernel(
  heap: Heap,
  state: MadState,
  t_arg: Term,
  g_arg: Term,
  q_arg: Term,
) -> MadOutcome {
  // G must resolve to a `_w/2`/`_r/2` global name (else abort — spec §11.5).
  case of_term(resolve_term(heap, g_arg)) {
    Error(_) -> MadAbort("_send: G must be a _w/2 or _r/2 global name")
    Ok(g_name) -> {
      // Q must resolve to a ground destination agent term.
      case resolve_term(heap, q_arg) {
        VarRef(_) -> MadAbort("_send: destination Q is unbound")
        dest -> {
          // Globalize T for Q (spec §11.5 step 1): mint W_p entries for writers,
          // emit `global_send` spawns for readers, substitute the global names.
          let t = resolve_term(heap, t_arg)
          let vars = collect_vars(t, heap)
          let #(w_p2, result) = globalize(vars, owner(state.w_p), dest, state.w_p)
          let t_up = globalize_term(t, vars, result)
          // Build the assignment term (spec §11.5 step 2): serializer wraps
          // `[T↑ | _w(q,0)]`; normal sends `T↑` directly.
          let msg_term = case g_name {
            WriterName(_, 0) -> cons(t_up, to_term(g_name))
            _ -> t_up
          }
          MadEffect(
            heap: heap,
            state: MadState(
              w_p: w_p2,
              m_p: list.append(state.m_p, [Message(g_name, msg_term, dest)]),
              mad_spawns: list.append(state.mad_spawns, result.spawns),
            ),
            woken: [],
          )
        }
      }
    }
  }
}

/// Resolve a term over the heap for globalization: bound variables become their
/// (deep) values; an UNBOUND writer/reader stays a `VarRef` with its ROLE preserved
/// (a reader keeps its own addr, so `collect_vars` re-classifies it correctly — a
/// plain `deref` would collapse a reader onto its writer and lose polarity). Faithful
/// to the Dart `_deepDeref` used before globalization.
fn resolve_term(heap: Heap, term: Term) -> Term {
  case term {
    VarRef(addr) ->
      case deref(heap, addr) {
        Ok(#(_, Bound(v))) -> resolve_term(heap, v)
        Ok(#(_, Unbound(w))) ->
          case is_reader(heap, addr) {
            True -> VarRef(addr)
            False -> VarRef(w)
          }
        Error(_) -> term
      }
    StructTerm(f, args) ->
      StructTerm(f, list.map(args, fn(a) { resolve_term(heap, a) }))
    _ -> term
  }
}
