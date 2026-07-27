//// Tests for glp/link/primitives/link_faults (T076) — fault-as-data delivery
//// planning: signal→lattice term, fan-out to every monitor cursor, and end-all.

import gleam/list
import gleeunit/should
import glp/link/primitives/link_faults.{MonitorBind}
import glp/link/primitives/link_handle
import glp/link/primitives/link_terms
import glp/link/seam/link_address
import glp/link/seam/link_fault.{LinkFaultSignal, Transient}
import glp/link/seam/link_id.{LinkId, NonceInt}
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import glp/runtime/terms.{cons, nil, VarRef}

fn id() {
  LinkId(link_scheme.tcp(), link_address.endpoint("127.0.0.1", 9130), NonceInt(1))
}

fn handle_with_cursors(cursors) {
  link_handle.LinkHandle(
    ..link_handle.new(id(), link_options.default()),
    monitor_cursors: cursors,
  )
}

pub fn signal_to_term_is_lattice_test() {
  link_faults.signal_to_term(LinkFaultSignal(id(), Transient, "reset"))
  |> should.equal(link_terms.temp_fail(id(), "reset"))
}

pub fn fanout_binds_every_cursor_and_advances_test() {
  // Two monitor cursors (the establishment Faults stream + one link_monitor stream).
  let handle = handle_with_cursors([100, 200])
  let term = link_terms.temp_fail(id(), "reset")
  // A deterministic fresh-pair allocator for the test (writer, reader).
  let fresh = fn() { #(300, 301) }
  let #(handle2, binds) = link_faults.fanout_fault(handle, term, fresh)
  // One bind per cursor, in cursor order, each a `[term | freshReader]` cons.
  binds
  |> should.equal([
    MonitorBind(100, cons(term, VarRef(301)), 300),
    MonitorBind(200, cons(term, VarRef(301)), 300),
  ])
  // Each cursor advanced to the fresh writer.
  handle2.monitor_cursors
  |> should.equal([300, 300])
}

pub fn end_all_binds_nil_without_advancing_test() {
  let handle = handle_with_cursors([100, 200])
  link_faults.end_all(handle)
  |> should.equal([MonitorBind(100, nil(), 100), MonitorBind(200, nil(), 200)])
}

pub fn fanout_on_no_cursors_is_empty_test() {
  let #(_h, binds) =
    link_faults.fanout_fault(handle_with_cursors([]), nil(), fn() { #(1, 2) })
  list.length(binds)
  |> should.equal(0)
}
