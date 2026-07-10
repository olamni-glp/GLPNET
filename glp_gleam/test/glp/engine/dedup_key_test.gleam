//// Reactivation-dedup key semantics (feature 050, T012 / FR-005): a wake is
//// keyed by (goal_id, suspension_generation); a duplicate enqueue for the same
//// key is dropped, a stale generation's wake is dropped, and the suspension
//// table consumes atomically so no waiter can be produced twice.

import gleam/set
import gleeunit/should
import glp/bytecode/program
import glp/engine/types.{Activation, Runnable, WakeKey}
import glp/runtime/suspension.{Waiter}

fn activation(goal_id: Int, generation: Int) -> types.Activation {
  Activation(
    goal_id: goal_id,
    procedure: "p/0",
    resume_pc: 0,
    regs: program.new_regs(),
    state: Runnable,
    generation: generation,
  )
}

pub fn duplicate_wake_same_generation_is_dropped_test() {
  let queue = types.new_queue()
  let #(queue, first) = types.enqueue_wake(queue, activation(7, 1))
  let #(queue, second) = types.enqueue_wake(queue, activation(7, 1))
  first |> should.be_true
  second |> should.be_false
  // Exactly one activation comes off the queue.
  let assert Ok(#(got, queue)) = types.dequeue(queue)
  got.goal_id |> should.equal(7)
  types.dequeue(queue) |> should.equal(Error(Nil))
}

pub fn later_generation_wake_is_enqueued_test() {
  let queue = types.new_queue()
  let #(queue, first) = types.enqueue_wake(queue, activation(7, 1))
  let #(queue, second) = types.enqueue_wake(queue, activation(7, 2))
  first |> should.be_true
  second |> should.be_true
  let assert Ok(#(a, queue)) = types.dequeue(queue)
  let assert Ok(#(b, _)) = types.dequeue(queue)
  a.generation |> should.equal(1)
  b.generation |> should.equal(2)
}

pub fn stale_generation_wake_is_not_current_test() {
  // A goal reactivated from generation 1 runs at generation 2; a wake recorded
  // against generation 1 is stale and must be dropped by the scheduler.
  types.wake_is_current(WakeKey(goal_id: 7, generation: 1), 2)
  |> should.be_false
  types.wake_is_current(WakeKey(goal_id: 7, generation: 2), 2)
  |> should.be_true
}

pub fn distinct_goals_do_not_collide_test() {
  let queue = types.new_queue()
  let #(queue, first) = types.enqueue_wake(queue, activation(7, 1))
  let #(_, second) = types.enqueue_wake(queue, activation(8, 1))
  first |> should.be_true
  second |> should.be_true
}

// --- suspension-table atomic consume (T010) ---------------------------------

pub fn consume_is_atomic_no_double_wake_test() {
  let table =
    suspension.new_table()
    |> suspension.suspend(100, Waiter(goal_id: 7, generation: 1))
    |> suspension.suspend(100, Waiter(goal_id: 9, generation: 4))
  let #(table, woken) = suspension.consume(table, 100)
  set.size(woken) |> should.equal(2)
  // Second consume of the same writer: entry already gone — empty set.
  let #(_, again) = suspension.consume(table, 100)
  set.size(again) |> should.equal(0)
}

pub fn suspend_is_set_scoped_per_writer_test() {
  // Registering the same (goal, generation) twice on one writer stores ONE
  // waiter; a multi-variable suspension registers on several writers instead.
  let table =
    suspension.new_table()
    |> suspension.suspend(100, Waiter(goal_id: 7, generation: 1))
    |> suspension.suspend(100, Waiter(goal_id: 7, generation: 1))
    |> suspension.suspend(200, Waiter(goal_id: 7, generation: 1))
  set.size(suspension.waiters_on(table, 100)) |> should.equal(1)
  set.size(suspension.waiters_on(table, 200)) |> should.equal(1)
}
