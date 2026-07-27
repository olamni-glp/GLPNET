//// Tests for glp/link/reliability/resource_snapshot (T077) — the reclamation baseline
//// value (FR-024, SC-014).

import gleeunit/should
import glp/link/reliability/resource_snapshot.{ResourceSnapshot}

pub fn zero_is_its_own_baseline_test() {
  resource_snapshot.is_baseline(resource_snapshot.zero(), resource_snapshot.zero())
  |> should.be_true
}

pub fn a_leaked_counter_is_not_baseline_test() {
  // One W_p entry still held → not reclaimed to baseline.
  resource_snapshot.is_baseline(ResourceSnapshot(1, 0, 0, 0), resource_snapshot.zero())
  |> should.be_false
}

pub fn equal_snapshots_are_baseline_test() {
  let s = ResourceSnapshot(3, 2, 1, 4)
  resource_snapshot.is_baseline(s, ResourceSnapshot(3, 2, 1, 4))
  |> should.be_true
}
