//// glp/link/primitives/link_faults — the per-link fault-monitor delivery core
//// (feature 059, T076 — port of glp_runtime/lib/link/primitives/link_faults.dart,
//// mirror csharp/glp_link/primitives/LinkFaults.cs).
////
//// Faults surface as ORDINARY BOUND GROUND TERMS on a per-link monitor stream over
//// the lattice `ok` / `closed(LinkId,Reason)` / `tempFail(LinkId,Reason)` /
//// `permFail(LinkId,Reason)` — read with existing guards, NEVER a fourth
//// unification verdict (FR-043) and NEVER a logical Fail (FR-044). A goal that does
//// not read the stream simply stays suspended on its unbound head.
////
//// Delivery is a plain stream-tail bind (the same idiom as the inbound `In` stream):
//// mint a fresh (writer, reader) pair, cons `[term | freshReader]`, bind the current
//// cursor writer, reactivate any suspended reader, and advance the cursor to the
//// fresh writer. This is WHY a fault cannot become a fourth verdict — it is a normal
//// bind.
////
//// GLEAM MAPPING NOTE: the Dart `LinkFaults` performs the heap bind + goal-queue
//// enqueue in place (runner thread). On BEAM the heap is immutable and reactivation
//// is the scheduler's job, so T076 provides the PURE delivery PLAN — the cons cell to
//// bind per cursor and the fresh writer the cursor advances to. The (T074) link
//// driver applies each plan via `heap.bind_writer` + scheduler reactivation, exactly
//// as it does for the inbound `In` stream. The lattice term construction is the
//// faithful fault-as-data core (`link_terms`).

import gleam/list
import glp/link/primitives/link_handle.{type LinkHandle}
import glp/link/primitives/link_terms
import glp/link/seam/link_fault.{type LinkFaultSignal}
import glp/runtime/terms.{type Term, VarRef, cons, nil}

/// One monitor-cursor delivery plan: bind `cursor` (a writer address) to `value`
/// (either a `[term | freshReader]` cons for a fault, or `[]` to end the stream),
/// then advance the handle's cursor entry to `next_writer` (the fresh writer for the
/// stream tail). The driver applies it with `heap.bind_writer` + reactivation.
pub type MonitorBind {
  MonitorBind(cursor: Int, value: Term, next_writer: Int)
}

/// Map a seam-level `LinkFaultSignal` to its GLP monitor lattice term (`closed` /
/// `tempFail` / `permFail`) — the fault-as-data refinement (FR-043/045).
pub fn signal_to_term(signal: LinkFaultSignal) -> Term {
  link_terms.from_signal(signal)
}

/// Plan the fan-out of one fault `term` to EVERY monitor cursor of `handle` — the
/// establishment `Faults` stream AND all `link_monitor` streams — so each
/// independent observer sees it on its own stream (FR-008). `fresh` supplies the
/// (writer, reader) pair for each cursor's new tail (the driver's
/// `heap.allocate_variable`). Returns the per-cursor binds and the handle with its
/// cursors advanced to the fresh writers.
pub fn fanout_fault(
  handle: LinkHandle,
  term: Term,
  fresh: fn() -> #(Int, Int),
) -> #(LinkHandle, List(MonitorBind)) {
  let #(binds, new_cursors) =
    list.fold(handle.monitor_cursors, #([], []), fn(acc, cursor) {
      let #(binds, cursors) = acc
      let #(fresh_writer, fresh_reader) = fresh()
      let bind =
        MonitorBind(
          cursor: cursor,
          value: cons(term, VarRef(fresh_reader)),
          next_writer: fresh_writer,
        )
      #([bind, ..binds], [fresh_writer, ..cursors])
    })
  #(
    link_handle.LinkHandle(..handle, monitor_cursors: list.reverse(new_cursors)),
    list.reverse(binds),
  )
}

/// Plan the END of every monitor stream of `handle` with `[]` (nil): bind each live
/// cursor to the empty list so a watcher sees end-of-stream and reduces its `[]`
/// clause. Used after a terminal `closed(LinkId,Reason)` on close (T035). The cursor
/// does not advance (the stream is closed), so `next_writer` repeats the cursor.
pub fn end_all(handle: LinkHandle) -> List(MonitorBind) {
  list.map(handle.monitor_cursors, fn(cursor) {
    MonitorBind(cursor: cursor, value: nil(), next_writer: cursor)
  })
}
