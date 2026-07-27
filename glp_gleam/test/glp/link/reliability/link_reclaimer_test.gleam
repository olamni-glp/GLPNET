//// Tests for glp/link/reliability/link_reclaimer (T077) — distributed-GC coordinator
//// (FR-024, SC-014): idempotent reclamation + straggler-after-teardown.

import gleam/erlang/process
import gleeunit/should
import glp/link/reliability/link_reclaimer
import glp/link/seam/link_address
import glp/link/seam/link_id.{LinkId, NonceInt}
import glp/link/seam/link_scheme

fn id() {
  LinkId(link_scheme.tcp(), link_address.endpoint("127.0.0.1", 9100), NonceInt(1))
}

pub fn reclaim_runs_hooks_once_and_is_idempotent_test() {
  let observed = process.new_subject()
  let r =
    link_reclaimer.register(link_reclaimer.new(), id(), fn() {
      process.send(observed, 1)
    })
  link_reclaimer.pending_link_count(r) |> should.equal(1)
  // First reclaim runs the hook and reports it performed the reclamation.
  let #(r, did) = link_reclaimer.reclaim(r, id())
  did |> should.be_true
  process.receive(observed, 50) |> should.equal(Ok(1))
  link_reclaimer.is_reclaimed(r, id()) |> should.be_true
  // Second reclaim is an idempotent no-op — hook does NOT run again.
  let #(_r, did2) = link_reclaimer.reclaim(r, id())
  did2 |> should.be_false
  process.receive(observed, 20) |> should.be_error
}

pub fn register_after_reclaim_runs_straggler_immediately_test() {
  let observed = process.new_subject()
  let #(r, _) = link_reclaimer.reclaim(link_reclaimer.new(), id())
  // A late allocation after teardown reclaims immediately (must not leak).
  let _r =
    link_reclaimer.register(r, id(), fn() { process.send(observed, 7) })
  process.receive(observed, 50) |> should.equal(Ok(7))
}
