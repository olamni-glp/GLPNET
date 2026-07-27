//// glp/link/primitives/link_faults — the per-link fault-monitor delivery core
//// (feature 050, T050.C7).
////
//// Port of `csharp/glp_link/primitives/LinkFaults.cs` (mirror
//// `glp_runtime/lib/link/primitives/link_faults.dart`).
////
//// Faults surface as ORDINARY BOUND GROUND TERMS on a per-link monitor stream over the
//// lattice `ok` / `closed(LinkId, Reason)` / `tempFail(LinkId, Reason)` /
//// `permFail(LinkId, Reason)` — read with existing guards, NEVER a fourth unification
//// verdict (FR-043), and a disconnect NEVER maps to a logical Fail (FR-044): a goal
//// that does not read the stream simply stays suspended on its unbound head. Delivery
//// is a plain stream-tail bind — the same idiom as the C6 ingress `In` extension —
//// which is exactly why a fault cannot become a fourth verdict: it IS a normal bind.
////
//// **D-1 (RESOLVED):** bare `ok` (arity 0) + `closed/2` + `tempFail/2` + `permFail/2`,
//// per `self.glp:451` and the C# oracle (`LinkTerms.Ok()` → `ok`).
//// `architecture-context.md §5`'s `ok(LinkId)` is a superseded proposal — never emitted.
////
//// The cursor list lives on the `LinkHandle` (`monitor_cursors`) and every operation
//// here returns the ADVANCED handle for the caller to thread back into the registry —
//// the same immutable discipline as `take_seq` (outbound) and `advance_in_cursor`
//// (ingress). The oracle mutates `handle.monitorCursors[i]` in place; the outcome is
//// identical, the threading explicit.

import gleam/list
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/primitives/link_terms
import glp/link/seam/link_fault.{
  type LinkFaultSignal, Closed, Permanent, Transient,
}
import glp/link/seam/link_id.{type LinkId}
import glp/runtime/heap.{type Heap}
import glp/runtime/suspension.{type GoalRef}
import glp/runtime/terms.{type Term, ConstAtom, ConstTerm, StructTerm, VarRef}

// ── the fault lattice (D-1 shapes, self.glp:451) ─────────────────────────────

/// The healthy baseline: bare `ok`, arity 0 (D-1).
pub fn ok() -> Term {
  ConstTerm(ConstAtom("ok"))
}

/// `closed(LinkId, Reason)` — an INTENTIONAL terminal (Reason = `eos` for the graceful
/// `Out = []`, or the user reason from `link_close`), distinct from the failure arms.
pub fn closed(id: LinkId, reason: String) -> Term {
  fault2("closed", id, reason)
}

/// `tempFail(LinkId, Reason)` — a recoverable transport fault (FR-045).
pub fn temp_fail(id: LinkId, reason: String) -> Term {
  fault2("tempFail", id, reason)
}

/// `permFail(LinkId, Reason)` — an unrecoverable fault; C8 runs distributed GC on it.
pub fn perm_fail(id: LinkId, reason: String) -> Term {
  fault2("permFail", id, reason)
}

fn fault2(functor: String, id: LinkId, reason: String) -> Term {
  StructTerm(functor, [
    link_terms.link_id_to_term(id),
    ConstTerm(ConstAtom(reason)),
  ])
}

/// Refine a coarse seam-level signal to its GLP lattice term (the oracle's
/// `LinkTerms.fromSignal`). `Closed` → `closed/2`, `Transient` → `tempFail/2`,
/// `Permanent` → `permFail/2`. A `Term` is pure data, so the pump may call this OFF
/// the runner process — refinement touches no heap.
pub fn from_signal(signal: LinkFaultSignal) -> Term {
  case signal.kind {
    Closed -> closed(signal.link, signal.reason)
    Transient -> temp_fail(signal.link, signal.reason)
    Permanent -> perm_fail(signal.link, signal.reason)
  }
}

// ── delivery (runner side only — every function here binds the heap) ─────────

/// Extend ONE monitor cursor by one ground fault term: mint a fresh (writer, reader)
/// pair, cons `[term | fresh_reader]`, bind the cursor, hand back the fresh writer as
/// the advanced cursor plus the goals the bind woke (a reader suspended on the stream's
/// head). `Error(Nil)` if the cursor was already bound — a stream that was ended.
pub fn extend(
  heap: Heap,
  cursor: Int,
  term: Term,
) -> Result(#(Heap, Int, List(GoalRef)), Nil) {
  let #(heap, fresh_writer, fresh_reader) = heap.allocate_variable(heap)
  case heap.bind_writer(heap, cursor, terms.cons(term, VarRef(fresh_reader))) {
    Error(_) -> Error(Nil)
    Ok(#(heap, woken)) -> Ok(#(heap, fresh_writer, woken))
  }
}

/// Fan a fault term out to EVERY monitor cursor of `handle` — the establishment
/// `Faults` stream and each `link_monitor` stream — so each independent observer sees
/// it on its OWN stream (FR-008). Returns the handle with every cursor advanced; the
/// caller threads it back into the registry. A cursor whose stream was already ended
/// (bind refused) is dropped from the list rather than double-bound.
pub fn deliver_fault(
  heap: Heap,
  handle: LinkHandle,
  term: Term,
) -> #(Heap, LinkHandle, List(GoalRef)) {
  let #(heap, advanced, woken) =
    list.fold(handle.monitor_cursors, #(heap, [], []), fn(acc, cursor) {
      let #(heap, cursors, woken) = acc
      case extend(heap, cursor, term) {
        Error(Nil) -> #(heap, cursors, woken)
        Ok(#(heap, fresh, newly)) -> #(
          heap,
          [fresh, ..cursors],
          list.append(woken, newly),
        )
      }
    })
  #(
    heap,
    link_handle.set_monitor_cursors(handle, list.reverse(advanced)),
    woken,
  )
}

/// End EVERY monitor stream of `handle` with `[]` (nil), so each watcher sees
/// end-of-stream and reduces its `[]` clause. Used by C8 after the terminal
/// `closed(LinkId, Reason)`: a close is terminal, so the monitor streams close too.
/// The cursor list comes back EMPTY — an ended stream must never be extended again.
pub fn end_all(
  heap: Heap,
  handle: LinkHandle,
) -> #(Heap, LinkHandle, List(GoalRef)) {
  let #(heap, woken) =
    list.fold(handle.monitor_cursors, #(heap, []), fn(acc, cursor) {
      let #(heap, woken) = acc
      case heap.bind_writer(heap, cursor, ConstTerm(ConstAtom("nil"))) {
        Error(_) -> #(heap, woken)
        Ok(#(heap, newly)) -> #(heap, list.append(woken, newly))
      }
    })
  #(heap, link_handle.set_monitor_cursors(handle, []), woken)
}
