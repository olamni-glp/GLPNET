//// glp/engine/scheduler — the scheduler-actor run loop (feature 050, T022).
////
//// R2 (research.md): a pure state-stepping loop over a run queue + the immutable
//// heap + heap-level suspensions. One `reduce` per dequeued goal (T021 runner);
//// the outcome drives the loop:
////   - Reduced      → the goal reduced: mint ids for its body's spawn requests,
////                    re-enqueue any goals its commit/body binding woke, drop the
////                    goal.
////   - Suspended    → register the goal in the heap against each writer it waits
////                    on (a later binding wakes it); keep it in the goal store.
////   - Failed       → drop the goal (definitive failure).
////   - Budget/Error → surface (never silently swallow).
//// Runs to quiescence (empty queue) or the reduction-fuel cap (loop backstop).
////
//// Reactivation is heap-driven: `heap.bind_writer` fires the suspensions the
//// scheduler armed via `heap.suspend_on_writer`, and the runner surfaces the
//// woken `GoalRef`s. Re-enqueue is deduped per (goal id, suspension generation)
//// by `RunQueue.enqueue_wake` (FR-005).
////
//// Dart reference: glp_runtime/lib/runtime/glp_runtime.dart goal-queue loop —
//// re-expressed as an immutable engine value threaded through each step.

import gleam/dict.{type Dict}
import gleam/list
import gleam/set
import glp/bytecode/program.{type BytecodeProgram, type XRegs}
import glp/engine/runner.{type RunnerFault, type SpawnReq}
import glp/engine/types.{type Activation, type RunQueue, Activation}
import glp/runtime/heap.{type Heap}
import glp/runtime/suspension.{type GoalRef, Suspension}

/// The scheduler state: the loaded program, the current heap, the run queue, the
/// goal store (id → its current activation, for reactivation), and the next free
/// goal id. Immutable — every step returns a new `Engine`.
pub opaque type Engine {
  Engine(
    program: BytecodeProgram,
    heap: Heap,
    queue: RunQueue,
    goals: Dict(Int, Activation),
    next_id: Int,
  )
}

/// How the run loop ended.
pub type RunStatus {
  /// The queue drained — no runnable goals remain.
  Quiescent
  /// The reduction-fuel cap was hit (non-termination backstop).
  OutOfFuel
  /// A runner fault surfaced (structural violation / unported opcode / malformed).
  Errored(fault: RunnerFault)
}

/// A fresh engine over `program` and an initial `heap`.
pub fn new(program: BytecodeProgram, heap: Heap) -> Engine {
  Engine(
    program: program,
    heap: heap,
    queue: types.new_queue(),
    goals: dict.new(),
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
/// per goal reduction; `fuel` bounds total goal reductions (loop backstop).
pub fn run(
  engine: Engine,
  reduction_budget: Int,
  fuel: Int,
) -> #(Engine, RunStatus) {
  case fuel <= 0 {
    True -> #(engine, OutOfFuel)
    False ->
      case types.dequeue(engine.queue) {
        Error(_) -> #(engine, Quiescent)
        Ok(#(act, queue)) -> {
          let engine = Engine(..engine, queue: queue)
          let ctx = runner.new_context(engine.heap, act.regs)
          case
            runner.reduce(engine.program, ctx, act.resume_pc, reduction_budget)
          {
            runner.Reduced(heap: h, woken: woken, spawned: spawned) -> {
              let engine =
                Engine(
                  ..engine,
                  heap: h,
                  goals: dict.delete(engine.goals, act.goal_id),
                )
              let engine = list.fold(spawned, engine, spawn_goal)
              let engine = list.fold(woken, engine, reactivate)
              run(engine, reduction_budget, fuel - 1)
            }
            runner.Suspended(heap: h, on: on) -> {
              let engine = suspend_goal(Engine(..engine, heap: h), act, on)
              run(engine, reduction_budget, fuel - 1)
            }
            runner.Failed(heap: h) ->
              run(
                Engine(
                  ..engine,
                  heap: h,
                  goals: dict.delete(engine.goals, act.goal_id),
                ),
                reduction_budget,
                fuel - 1,
              )
            runner.BudgetExhausted(heap: h) -> #(
              Engine(..engine, heap: h),
              Errored(runner.Malformed(
                "reduction budget exhausted in goal " <> act.procedure,
              )),
            )
            runner.RunnerError(reason: fault) -> #(engine, Errored(fault))
          }
        }
      }
  }
}

/// Mint an id for a body spawn request and enqueue it as a fresh goal.
fn spawn_goal(engine: Engine, req: SpawnReq) -> Engine {
  let id = engine.next_id
  let act =
    Activation(id, req.procedure, req.entry_pc, req.regs, types.Runnable, 0)
  Engine(
    ..engine,
    next_id: id + 1,
    goals: dict.insert(engine.goals, id, act),
    queue: types.enqueue(engine.queue, act),
  )
}

/// Re-enqueue a woken goal (dedup per (goal id, generation) — FR-005). A wake
/// for a goal no longer in the store (already reduced) is ignored.
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
      )
    }
  }
}

/// Register a suspended goal against every writer it waits on (a later binding
/// wakes it), advancing its suspension generation, and keep it in the store.
fn suspend_goal(engine: Engine, act: Activation, on: set.Set(Int)) -> Engine {
  let generation = act.generation + 1
  let act =
    Activation(
      ..act,
      state: types.Suspended(generation),
      generation: generation,
    )
  let heap =
    set.fold(on, engine.heap, fn(h, writer) {
      case
        heap.suspend_on_writer(
          h,
          writer,
          Suspension(act.goal_id, act.resume_pc, True),
        )
      {
        Ok(h2) -> h2
        // Writer already bound (a race with another goal's binding): the goal
        // will be re-driven on the next quiescence check; skip registration.
        Error(_) -> h
      }
    })
  Engine(
    ..engine,
    heap: heap,
    goals: dict.insert(engine.goals, act.goal_id, act),
  )
}
