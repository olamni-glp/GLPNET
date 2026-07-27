//// Tests for glp/link/reliability/cycle_guard (T077) — cycle detection on the send
//// path (FR-022/028).

import gleeunit/should
import glp/link/reliability/cycle_guard

pub fn distinct_nodes_enter_cleanly_test() {
  let g = cycle_guard.new()
  let assert Ok(g) = cycle_guard.enter(g, 1)
  let assert Ok(g) = cycle_guard.enter(g, 2)
  cycle_guard.depth(g) |> should.equal(2)
}

pub fn re_entering_a_node_on_the_path_is_a_cycle_test() {
  let g = cycle_guard.new()
  let assert Ok(g) = cycle_guard.enter(g, 1)
  // Cell 1 is already on the active recursion path → a cycle (clean error, no loop).
  cycle_guard.enter(g, 1)
  |> should.be_error
}

pub fn leaving_a_node_permits_dag_sharing_test() {
  let g = cycle_guard.new()
  let assert Ok(g) = cycle_guard.enter(g, 1)
  let g = cycle_guard.leave(g, 1)
  // After leaving, re-entering the same node via another parent (a DAG) is fine.
  let assert Ok(g) = cycle_guard.enter(g, 1)
  cycle_guard.depth(g) |> should.equal(1)
}
