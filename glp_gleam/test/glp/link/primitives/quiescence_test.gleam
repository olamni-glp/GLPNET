//// Tests for glp/link/primitives/quiescence (T089) — the network-quiescence oracle
//// (also backs the T083 PI:17 adversarial suite + T066). Drives the PURE predicate
//// with synthesized counts, then the live-state adapters over a LinkRuntime.

import gleeunit/should
import glp/link/primitives/link_handle
import glp/link/primitives/link_registry
import glp/link/primitives/link_runtime
import glp/link/primitives/quiescence.{Active, Quiescent}
import glp/link/seam/endpoint.{Endpoint}
import glp/link/seam/link_address
import glp/link/seam/link_id.{LinkId, NonceInt}
import glp/link/seam/link_options
import glp/link/seam/link_scheme
import gleam/erlang/process
import gleam/option.{None}

pub fn all_zero_is_quiescent_test() {
  quiescence.decide(0, 0, 0) |> should.equal(Quiescent)
  quiescence.is_quiescent(0, 0, 0) |> should.be_true
}

pub fn runnable_goals_block_quiescence_test() {
  quiescence.decide(1, 0, 0) |> should.equal(Active(1, 0, 0))
  quiescence.is_quiescent(1, 0, 0) |> should.be_false
}

pub fn frames_in_flight_block_quiescence_test() {
  quiescence.is_quiescent(0, 2, 0) |> should.be_false
}

pub fn open_pending_blocks_quiescence_test() {
  // A parked path-B connection awaiting `_link_accept` is a handshake in progress.
  quiescence.is_quiescent(0, 0, 1) |> should.be_false
}

fn id() {
  LinkId(link_scheme.tcp(), link_address.endpoint("127.0.0.1", 9100), NonceInt(1))
}

fn dummy_endpoint() {
  Endpoint(
    id: id(),
    send: fn(_frame) { Ok(Nil) },
    recv: fn() { Ok(None) },
    close: fn() { Nil },
    faults: process.new_subject(),
  )
}

pub fn empty_runtime_has_no_activity_test() {
  quiescence.link_activity(link_runtime.new())
  |> should.equal(#(0, 0))
}

pub fn an_open_link_counts_as_in_flight_test() {
  // A registered link whose endpoint is present and not both-directions-closed is
  // still exchanging → in flight.
  let handle =
    link_handle.attach_endpoint(
      link_handle.new(id(), link_options.default()),
      dummy_endpoint(),
    )
  let assert Ok(links) = link_registry.put(link_registry.new(), id(), handle)
  let runtime = link_runtime.with_links(link_runtime.new(), links)
  quiescence.link_activity(runtime)
  |> should.equal(#(1, 0))
  // With one runnable goal + this open link → NOT quiescent.
  quiescence.is_run_quiescent(0, runtime) |> should.be_false
}

pub fn a_fully_closed_link_is_not_in_flight_test() {
  let handle =
    link_handle.LinkHandle(
      ..link_handle.attach_endpoint(
        link_handle.new(id(), link_options.default()),
        dummy_endpoint(),
      ),
      in_closed: True,
      out_closed: True,
    )
  let assert Ok(links) = link_registry.put(link_registry.new(), id(), handle)
  let runtime = link_runtime.with_links(link_runtime.new(), links)
  // Both directions closed → no longer in flight; with no runnable goals → quiescent.
  quiescence.link_activity(runtime) |> should.equal(#(0, 0))
  quiescence.is_run_quiescent(0, runtime) |> should.be_true
}
