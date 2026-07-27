//// Tests for glp/link/primitives/link_faults (T076/T075) — fault-as-data delivery
//// planning (signal→lattice, fan-out, end-all) + the T075 fault decoration: the
//// bounded-silence heuristic, fencing→permFail, and establishment-failure.

import gleam/list
import gleam/option.{None, Some}
import gleeunit/should
import glp/link/primitives/link_faults.{
  MonitorBind, NoSilenceFault, PermFailSilence, TempFailSilence,
}
import glp/link/primitives/link_handle
import glp/link/primitives/link_terms
import glp/link/reliability/fencing_registry.{Admit, Fenced}
import glp/link/seam/link_address
import glp/link/seam/link_fault.{LinkFaultSignal, Transient}
import glp/link/seam/link_id.{LinkId, NonceInt}
import glp/link/seam/link_options.{type LinkOptions}
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

// ── T075: bounded-silence heuristic (FR-045) ──────────────────────────────────

fn opts() -> LinkOptions {
  // temp_fail after 5000ms, perm_fail after 30000ms (the defaults).
  link_options.default()
}

pub fn silence_below_temp_threshold_is_no_fault_test() {
  link_faults.classify_silence(4999, opts()) |> should.equal(NoSilenceFault)
  link_faults.silence_fault(NoSilenceFault, id()) |> should.equal(None)
}

pub fn silence_past_temp_threshold_is_temp_fail_test() {
  link_faults.classify_silence(5000, opts()) |> should.equal(TempFailSilence)
  link_faults.silence_fault(TempFailSilence, id())
  |> should.equal(Some(link_terms.temp_fail(
    id(),
    "bounded silence exceeded temp-fail threshold",
  )))
}

pub fn silence_past_perm_threshold_is_perm_fail_test() {
  link_faults.classify_silence(30_000, opts()) |> should.equal(PermFailSilence)
  case link_faults.silence_fault(PermFailSilence, id()) {
    Some(_perm_term) -> should.be_true(True)
    None -> should.be_true(False)
  }
}

// ── T075: fencing → permFail (FR-047) ─────────────────────────────────────────

pub fn admitted_writer_has_no_fault_test() {
  link_faults.fence_fault(Admit, id(), "_w(p,1)") |> should.equal(None)
}

pub fn fenced_writer_surfaces_perm_fail_test() {
  link_faults.fence_fault(Fenced, id(), "_w(p,1)")
  |> should.equal(Some(link_terms.perm_fail(id(), "fenced by newer epoch: _w(p,1)")))
}

// ── T075: establishment-failure decoration (FR-044) ───────────────────────────

pub fn establishment_failure_is_perm_fail_term_test() {
  link_faults.establishment_failure(id(), "connection refused")
  |> should.equal(link_terms.perm_fail(
    id(),
    "transport establishment failed: connection refused",
  ))
}
