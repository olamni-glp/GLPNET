//// Tests for glp/link/reliability/fencing_registry (T077) — split-brain fencing
//// (FR-047, SC-011).

import gleeunit/should
import glp/link/reliability/fencing_registry.{Admit, Fenced}

pub fn first_writer_is_admitted_test() {
  let #(reg, verdict) =
    fencing_registry.admit(fencing_registry.new(), "_w(p,1)", 5)
  verdict |> should.equal(Admit)
  fencing_registry.highest_epoch_for(reg, "_w(p,1)") |> should.equal(Ok(5))
}

pub fn lower_epoch_is_fenced_higher_admitted_test() {
  let #(reg, _) = fencing_registry.admit(fencing_registry.new(), "n", 5)
  // A stale (lower-epoch) writer is fenced; state unchanged.
  let #(reg, v_stale) = fencing_registry.admit(reg, "n", 3)
  v_stale |> should.equal(Fenced)
  fencing_registry.highest_epoch_for(reg, "n") |> should.equal(Ok(5))
  // A legitimate takeover (higher epoch) is admitted and raises the high-water.
  let #(reg, v_new) = fencing_registry.admit(reg, "n", 9)
  v_new |> should.equal(Admit)
  fencing_registry.highest_epoch_for(reg, "n") |> should.equal(Ok(9))
}

pub fn equal_epoch_is_admitted_idempotent_test() {
  let #(reg, _) = fencing_registry.admit(fencing_registry.new(), "n", 5)
  let #(_reg, v) = fencing_registry.admit(reg, "n", 5)
  v |> should.equal(Admit)
}

pub fn forget_resets_the_name_test() {
  let #(reg, _) = fencing_registry.admit(fencing_registry.new(), "n", 5)
  let reg = fencing_registry.forget(reg, "n")
  fencing_registry.tracked_count(reg) |> should.equal(0)
  fencing_registry.highest_epoch_for(reg, "n") |> should.equal(Error(Nil))
}

pub fn epoch_allocator_is_monotone_test() {
  let a0 = fencing_registry.new_allocator()
  let #(a1, e0) = fencing_registry.next_epoch(a0)
  let #(_a2, e1) = fencing_registry.next_epoch(a1)
  #(e0, e1) |> should.equal(#(1, 2))
}
