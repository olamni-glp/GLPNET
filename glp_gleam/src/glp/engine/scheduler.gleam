//// glp/engine/scheduler — the scheduler-actor run loop (feature 050, T022/T029).
////
//// R2 (research.md): a pure state-stepping loop over a run queue + the immutable
//// heap + heap-level suspensions. One `reduce` per dequeued goal (T021 runner);
//// the outcome drives the loop:
////   - Reduced      → the goal reduced: mint ids for its body's spawn requests,
////                    re-enqueue any goals its commit/body binding woke, drop the
////                    goal.
////   - Suspended    → register the goal in the heap against each writer it waits
////                    on (a later binding wakes it); keep it in the goal store, and
////                    record its blocking readers.
////   - Failed       → the boot/run definitively failed (no clause committed, nothing
////                    to wait on); STOP the run (Dart `drainWithStatus` breaks on the
////                    first failure).
////   - Budget/Error → surface (never silently swallow).
//// Runs to quiescence (empty queue) or the reduction-fuel cap (loop backstop).
////
//// T029 scheduler refinement (Slice 0 — Gabi-directed 2026-07-12), a faithful port
//// of the Dart `Scheduler.drainWithStatus` terminal-status + blocking-reader
//// derivation (glp_runtime/lib/runtime/scheduler.dart), so the engine facade sits
//// on a real run/step contract rather than a stubbed envelope:
////   1. Faithful terminal `RunStatus` — Success / Suspended(blocking_readers) /
////      Failed, no longer conflated as one `Quiescent`. Failure STOPS the run
////      (Dart `break`). At quiescence: goals remain ⇒ Suspended, else ⇒ Success.
////   2. Blocking-reader exposure — the engine carries a reader-addr → suspended
////      goal-id table (a faithful mirror of Dart `rt.suspended`, cleaned on
////      reactivation exactly as Dart `_removeFromSuspended`); the Suspended status
////      carries its sorted keys, so `build_result_envelope`'s `blocking_readers` is
////      exact. Empty on Success/Failed, matching Dart (blockingReaders only when
////      suspended).
////   3. Real single-step — `step` performs ONE reduction and returns a `StepOutcome`
////      (idle / reduced+woken+spawned / suspended+on / failed / errored), the seam
////      the REPL `:trace` engine-as-value surface consumes. `run` is `step` looped.
////
//// Reactivation is heap-driven: `heap.bind_writer` fires the suspensions the
//// scheduler armed via `heap.suspend_on_writer`, and the runner surfaces the
//// woken `GoalRef`s. Re-enqueue is deduped per (goal id, suspension generation)
//// by `RunQueue.enqueue_wake` (FR-005).
////
//// Dart reference: glp_runtime/lib/runtime/scheduler.dart (drainWithStatus) +
//// glp_runtime/lib/runtime/runtime.dart (rt.suspended / _removeFromSuspended) —
//// re-expressed as an immutable engine value threaded through each step.

import gleam/dict.{type Dict}
import gleam/int
import gleam/list
import gleam/set.{type Set}
import glp/bytecode/program.{type BytecodeProgram, type XRegs}
import glp/engine/runner.{type RunnerFault, type SpawnReq}
import glp/engine/types.{type Activation, type RunQueue, Activation}
import glp/runtime/heap.{type Heap}
import glp/runtime/suspension.{type GoalRef, Suspension}

/// The scheduler state: the loaded program, the current heap, the run queue, the
/// goal store (id → its current activation, for reactivation), the blocking-reader
/// table (reader addr → the goal ids currently suspended on it — a mirror of Dart
/// `rt.suspended`), and the next free goal id. Immutable — every step returns a new
/// `Engine`.
pub opaque type Engine {
  Engine(
    program: BytecodeProgram,
    heap: Heap,
    queue: RunQueue,
    goals: Dict(Int, Activation),
    blocking: Dict(Int, Set(Int)),
    next_id: Int,
  )
}

/// How the run loop ended (T029 cap 1 — a faithful port of Dart
/// `Scheduler.ExecutionStatus`, which the previous `Quiescent` conflated).
pub type RunStatus {
  /// The queue drained with no goal left suspended and none failed — the run
  /// reduced to completion.
  Success
  /// The queue drained but goals remain suspended (waiting on unbound readers).
  /// `blocking_readers` is the sorted, deduped set of reader addresses those
  /// goals are blocked on (Dart `rt.suspended.keys`), for the result envelope.
  Suspended(blocking_readers: List(Int))
  /// A goal failed definitively (no clause committed, nothing to wait on); the
  /// run stopped there (Dart `drainWithStatus` breaks on the first failure).
  Failed
  /// The reduction-fuel cap was hit (non-termination backstop).
  OutOfFuel
  /// A runner fault surfaced (structural violation / unported opcode / malformed).
  Errored(fault: RunnerFault)
}

/// The outcome of one `step` (T029 cap 3 — the single-reduction seam the REPL
/// `:trace` surface consumes; NEW capability, no Dart public equivalent — Dart
/// only exposes the `maxCycles`-bounded drain).
pub type StepOutcome {
  /// The queue was empty — nothing to reduce (the engine is unchanged).
  StepIdle
  /// A clause committed: the goal reduced. `woken` are the goal ids re-enqueued by
  /// the commit's bindings; `spawned` are the freshly-minted body goal ids.
  StepReduced(
    goal_id: Int,
    procedure: String,
    woken: List(Int),
    spawned: List(Int),
  )
  /// The goal suspended on the writer addresses `on` (sorted) — reactivated when
  /// any binds.
  StepSuspended(goal_id: Int, procedure: String, on: List(Int))
  /// The goal failed definitively (no clause committed, nothing to wait on).
  StepFailed(goal_id: Int, procedure: String)
  /// A runner fault surfaced (budget exhausted / structural violation / unported
  /// opcode) — surfaced, never hidden.
  StepErrored(fault: RunnerFault)
}

/// A fresh engine over `program` and an initial `heap`.
pub fn new(program: BytecodeProgram, heap: Heap) -> Engine {
  Engine(
    program: program,
    heap: heap,
    queue: types.new_queue(),
    goals: dict.new(),
    blocking: dict.new(),
    next_id: 1,
  )
}

/// The engine's current heap (query outputs are read from here after `run`).
pub fn heap(engine: Engine) -> Heap {
  engine.heap
}

/// The next goal id the engine would mint (test/inspection hook).
pub fn next_id(engine: Engine) -> Int {
  engine.next_id
}

/// Enqueue a fresh boot goal for `procedure` at `entry_pc` with argument
/// registers `regs`. Returns the engine and the minted goal id.
pub fn boot(
  engine: Engine,
  procedure: String,
  entry_pc: Int,
  regs: XRegs,
) -> #(Engine, Int) {
  let id = engine.next_id
  let act = Activation(id, procedure, entry_pc, regs, types.Runnable, 0)
  #(
    Engine(
      ..engine,
      next_id: id + 1,
      goals: dict.insert(engine.goals, id, act),
      queue: types.enqueue(engine.queue, act),
    ),
    id,
  )
}

/// Run to quiescence (or the fuel cap). `reduction_budget` bounds instructions
/// per goal reduction; `fuel` bounds total goal reductions (loop backstop). The
/// run is `step` looped: each Reduced/Suspended step consumes one fuel; a Failed
/// step STOPS the run (Dart `break`); an empty queue yields the terminal status.
pub fn run(
  engine: Engine,
  reduction_budget: Int,
  fuel: Int,
) -> #(Engine, RunStatus) {
  case fuel <= 0 {
    True -> #(engine, OutOfFuel)
    False -> {
      let #(engine, outcome) = step(engine, reduction_budget)
      case outcome {
        StepIdle -> #(engine, terminal_status(engine))
        StepReduced(..) -> run(engine, reduction_budget, fuel - 1)
        StepSuspended(..) -> run(engine, reduction_budget, fuel - 1)
        StepFailed(..) -> #(engine, Failed)
        StepErrored(fault) -> #(engine, Errored(fault))
      }
    }
  }
}

/// Perform ONE reduction: dequeue the next runnable goal, reduce it once, apply
/// its effects (spawn / wake / suspend / drop), and report what happened. The
/// shared core of `run` and the REPL single-step seam (T029 cap 3).
pub fn step(engine: Engine, reduction_budget: Int) -> #(Engine, StepOutcome) {
  case types.dequeue(engine.queue) {
    Error(_) -> #(engine, StepIdle)
    Ok(#(act, queue)) -> {
      let engine = Engine(..engine, queue: queue)
      let ctx = runner.new_context(engine.heap, act.regs)
      case runner.reduce(engine.program, ctx, act.resume_pc, reduction_budget) {
        runner.Reduced(heap: h, woken: woken, spawned: spawned) -> {
          let engine =
            Engine(
              ..engine,
              heap: h,
              goals: dict.delete(engine.goals, act.goal_id),
            )
          let #(engine, spawned_ids) =
            list.map_fold(spawned, engine, spawn_goal)
          let engine = list.fold(woken, engine, reactivate)
          let woken_ids = list.map(woken, fn(ref) { ref.goal_id })
          #(
            engine,
            StepReduced(act.goal_id, act.procedure, woken_ids, spawned_ids),
          )
        }
        runner.Suspended(heap: h, on: on) -> {
          let engine = suspend_goal(Engine(..engine, heap: h), act, on)
          let on_list = on |> set.to_list |> list.sort(int.compare)
          #(engine, StepSuspended(act.goal_id, act.procedure, on_list))
        }
        runner.Failed(heap: h) -> #(
          Engine(
            ..engine,
            heap: h,
            goals: dict.delete(engine.goals, act.goal_id),
          ),
          StepFailed(act.goal_id, act.procedure),
        )
        runner.BudgetExhausted(heap: h) -> #(
          Engine(..engine, heap: h),
          StepErrored(runner.Malformed(
            "reduction budget exhausted in goal " <> act.procedure,
          )),
        )
        runner.RunnerError(reason: fault) -> #(engine, StepErrored(fault))
      }
    }
  }
}

/// The terminal status once the queue has drained (T029 cap 1): goals remaining
/// in the store are suspended goals ⇒ Suspended (with their blocking readers);
/// an empty store ⇒ the run reduced to completion ⇒ Success. Mirrors Dart's
/// end-of-drain `userSuspendedGoals.isEmpty ? succeeded : suspended` (the
/// MVP engine has no infrastructure/serve goals to exclude).
fn terminal_status(engine: Engine) -> RunStatus {
  case dict.is_empty(engine.goals) {
    True -> Success
    False -> Suspended(blocking_readers(engine))
  }
}

/// The sorted, deduped reader addresses that the still-suspended goals are blocked
/// on (Dart `rt.suspended.keys`). Entries are dropped as they empty, so every key
/// is a live blocking reader.
fn blocking_readers(engine: Engine) -> List(Int) {
  engine.blocking
  |> dict.keys
  |> list.sort(int.compare)
}

/// Mint an id for a body spawn request and enqueue it as a fresh goal; returns the
/// engine and the minted id (for the step outcome's `spawned` report).
fn spawn_goal(engine: Engine, req: SpawnReq) -> #(Engine, Int) {
  let id = engine.next_id
  let act =
    Activation(id, req.procedure, req.entry_pc, req.regs, types.Runnable, 0)
  #(
    Engine(
      ..engine,
      next_id: id + 1,
      goals: dict.insert(engine.goals, id, act),
      queue: types.enqueue(engine.queue, act),
    ),
    id,
  )
}

/// Re-enqueue a woken goal (dedup per (goal id, generation) — FR-005) and clear it
/// from the blocking-reader table (Dart `enqueueReactivatedGoal` →
/// `_removeFromSuspended`). A wake for a goal no longer in the store (already
/// reduced) is ignored.
fn reactivate(engine: Engine, ref: GoalRef) -> Engine {
  case dict.get(engine.goals, ref.goal_id) {
    Error(_) -> engine
    Ok(act) -> {
      let act =
        Activation(..act, resume_pc: ref.resume_pc, state: types.Runnable)
      let #(queue, _enqueued) = types.enqueue_wake(engine.queue, act)
      Engine(
        ..engine,
        queue: queue,
        goals: dict.insert(engine.goals, ref.goal_id, act),
        blocking: remove_from_blocking(engine.blocking, ref.goal_id),
      )
    }
  }
}

/// Register a suspended goal against every writer it waits on (a later binding
/// wakes it), advancing its suspension generation, recording each writer's paired
/// reader in the blocking table (Dart `suspendGoalFCP` on `readerVarIds`), and
/// keeping it in the store.
fn suspend_goal(engine: Engine, act: Activation, on: Set(Int)) -> Engine {
  let generation = act.generation + 1
  let act =
    Activation(
      ..act,
      state: types.Suspended(generation),
      generation: generation,
    )
  let #(heap, blocking) =
    set.fold(on, #(engine.heap, engine.blocking), fn(acc, writer) {
      let #(h, bl) = acc
      case
        heap.suspend_on_writer(
          h,
          writer,
          Suspension(act.goal_id, act.resume_pc, True),
        )
      {
        // Armed: record the writer's paired reader as blocking this goal.
        Ok(h2) -> #(
          h2,
          add_blocking(bl, heap.paired_reader(h2, writer), act.goal_id),
        )
        // Writer already bound (a race with another goal's binding): the goal
        // will be re-driven on the next quiescence check; skip registration.
        Error(_) -> acc
      }
    })
  Engine(
    ..engine,
    heap: heap,
    blocking: blocking,
    goals: dict.insert(engine.goals, act.goal_id, act),
  )
}

/// Record `goal_id` as blocked on `reader` in the blocking table.
fn add_blocking(
  blocking: Dict(Int, Set(Int)),
  reader: Int,
  goal_id: Int,
) -> Dict(Int, Set(Int)) {
  let goals = case dict.get(blocking, reader) {
    Ok(existing) -> set.insert(existing, goal_id)
    Error(Nil) -> set.insert(set.new(), goal_id)
  }
  dict.insert(blocking, reader, goals)
}

/// Remove `goal_id` from every blocking entry, dropping any reader whose goal set
/// becomes empty (Dart `_removeFromSuspended`).
fn remove_from_blocking(
  blocking: Dict(Int, Set(Int)),
  goal_id: Int,
) -> Dict(Int, Set(Int)) {
  blocking
  |> dict.to_list
  |> list.filter_map(fn(pair) {
    let #(reader, goals) = pair
    let goals = set.delete(goals, goal_id)
    case set.is_empty(goals) {
      True -> Error(Nil)
      False -> Ok(#(reader, goals))
    }
  })
  |> dict.from_list
}
