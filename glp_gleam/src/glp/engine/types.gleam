//// glp/engine/types — engine core types (feature 050, T009).
////
//// Data model: specs/050-full-gleam-combined/data-model.md §Goal/Activation.
//// Concurrency model: research.md R2 — scheduler-actor inside one BEAM process;
//// the engine is a pure state-stepping function over goal queue + immutable heap
//// + suspension table. Determinism is load-bearing for corpus parity, and
//// reactivation is message-like, not pointer-like — hence the generation-scoped
//// dedup (FR-005).
////
//// Dart reference: glp_runtime/lib/bytecode/runner.dart (RunnerContext /
//// GoalRef machinery) — re-expressed as immutable values, same observable
//// scheduling semantics.

import gleam/list
import gleam/set.{type Set}
import glp/bytecode/opcodes.{type LabelName}
import glp/bytecode/program.{type XRegs}

/// Goal state (data-model.md):
/// `runnable → running → (succeeded | failed | suspended(generation))`;
/// `suspended(g) → runnable` on reactivation, which advances the goal to
/// generation g+1.
pub type GoalState {
  Runnable
  Running
  Succeeded
  Failed
  Suspended(generation: Int)
}

/// A schedulable unit: goal id (unique per instance), procedure ref, the
/// positional X-register file holding its argument terms (FR-006), the resume
/// PC (procedure entry for fresh goals; κ for reactivated goals), state, and
/// the current suspension generation.
pub type Activation {
  Activation(
    goal_id: Int,
    procedure: LabelName,
    resume_pc: Int,
    regs: XRegs,
    state: GoalState,
    generation: Int,
  )
}

/// The FR-005 reactivation dedup key: a wake is keyed by
/// (goal identity, suspension generation).
pub type WakeKey {
  WakeKey(goal_id: Int, generation: Int)
}

/// The wake key of an activation's current incarnation.
pub fn wake_key(activation: Activation) -> WakeKey {
  WakeKey(goal_id: activation.goal_id, generation: activation.generation)
}

/// FR-005 staleness test: a wake fires only for the goal's CURRENT generation;
/// a wake recorded against an older generation is dropped (multi-variable
/// suspensions reactivate exactly once per generation).
pub fn wake_is_current(wake: WakeKey, current_generation: Int) -> Bool {
  wake.generation == current_generation
}

/// The engine run queue: FIFO of activations (two-list functional queue) plus
/// the pending wake-key set that enforces at-most-one reactivation enqueue per
/// (goal_id, generation).
pub opaque type RunQueue {
  RunQueue(front: List(Activation), back: List(Activation), pending: Set(WakeKey))
}

/// An empty queue.
pub fn new_queue() -> RunQueue {
  RunQueue(front: [], back: [], pending: set.new())
}

/// Plain FIFO enqueue — fresh goals (Spawn/boot), no dedup involved.
pub fn enqueue(queue: RunQueue, activation: Activation) -> RunQueue {
  RunQueue(..queue, back: [activation, ..queue.back])
}

/// Reactivation enqueue, deduplicated by the activation's (goal_id, generation)
/// wake key (FR-005). Returns `#(queue, True)` when enqueued, `#(queue, False)`
/// when dropped as a duplicate of an already-pending wake.
pub fn enqueue_wake(queue: RunQueue, activation: Activation) -> #(RunQueue, Bool) {
  let key = wake_key(activation)
  case set.contains(queue.pending, key) {
    True -> #(queue, False)
    False -> #(
      RunQueue(
        ..queue,
        back: [activation, ..queue.back],
        pending: set.insert(queue.pending, key),
      ),
      True,
    )
  }
}

/// Dequeue the next runnable activation (FIFO). Its wake key leaves the
/// pending set — a LATER generation of the same goal may enqueue again, a
/// duplicate of the same generation may not (the goal is now running).
pub fn dequeue(queue: RunQueue) -> Result(#(Activation, RunQueue), Nil) {
  case queue.front, queue.back {
    [], [] -> Error(Nil)
    [next, ..rest], _ ->
      Ok(#(
        next,
        RunQueue(
          ..queue,
          front: rest,
          pending: set.delete(queue.pending, wake_key(next)),
        ),
      ))
    [], back -> {
      let assert [next, ..rest] = list.reverse(back)
      Ok(#(
        next,
        RunQueue(
          front: rest,
          back: [],
          pending: set.delete(queue.pending, wake_key(next)),
        ),
      ))
    }
  }
}

/// Is the queue empty? (Quiescence input for the engine loop.)
pub fn is_empty(queue: RunQueue) -> Bool {
  case queue.front, queue.back {
    [], [] -> True
    _, _ -> False
  }
}
