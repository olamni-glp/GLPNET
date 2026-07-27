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
import gleam/option.{None, Some}
import gleam/set.{type Set}
import gleam/string
import glp/bytecode/program.{type BytecodeProgram, type XRegs}
import glp/engine/goal_format
import glp/engine/runner.{type RunnerFault, type SpawnReq, SpawnReq}
import glp/engine/types.{type Activation, type RunQueue, Activation}
import glp/link/primitives/link_pump
import glp/link/primitives/link_runtime.{type DrainRequest, type LinkState}
import glp/mad/global_name
import glp/mad/globalize.{type Spawn}
import glp/mad/mad_kernels.{type MadState, MadState}
import glp/runtime/heap.{type Heap}
import glp/runtime/suspension.{type GoalRef, Suspension}
import glp/runtime/terms

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
    /// Captured `_output/1` program-output lines accumulated across reductions, in
    /// emission order (T034). Read after `run` via `captured_output`.
    output: List(String),
    /// `'_send_to_ui'/1` UI lines accumulated across reductions, in emission order.
    /// A SEPARATE sink from `output` (madGLP-spec §12.6; Gabi §1.14 ruling 2026-07-20)
    /// — read after `run` via `captured_ui`.
    ui: List(String),
    /// Reduction-trace mode (`:trace`). When on, each step appends a reference-shape
    /// trace line (`head :- body` / `goal → suspended` / `goal → failed`).
    trace: Bool,
    /// Accumulated trace lines in emission order (empty unless `trace`).
    trace_lines: List(String),
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
    output: [],
    ui: [],
    trace: False,
    trace_lines: [],
  )
}

/// Enable/disable reduction tracing (`:trace`). Set before `run`/`step`.
pub fn with_trace(engine: Engine, on: Bool) -> Engine {
  Engine(..engine, trace: on)
}

/// The accumulated reduction-trace lines, in emission order (T-US2 `:trace`).
pub fn trace_lines(engine: Engine) -> List(String) {
  engine.trace_lines
}

/// Trace a committed reduction as `head :- body` (Dart `onReduction`): the reduced
/// goal's head over the post-commit heap, and its body goals (the spawned goals)
/// or `true` for a unit clause. No-op unless tracing.
fn trace_reduction(
  engine: Engine,
  act: Activation,
  spawned: List(SpawnReq),
  heap: Heap,
) -> Engine {
  case engine.trace {
    False -> engine
    True -> {
      let head = goal_format.format_goal(act.procedure, act.regs, heap)
      let body = case spawned {
        [] -> "true"
        _ ->
          spawned
          |> list.map(fn(req) {
            goal_format.format_goal(req.procedure, req.regs, heap)
          })
          |> string.join(", ")
      }
      append_trace(engine, head <> " :- " <> body)
    }
  }
}

/// Trace a terminal step (`goal → suspended` / `goal → failed`). No-op unless
/// tracing.
fn trace_terminal(
  engine: Engine,
  act: Activation,
  heap: Heap,
  suffix: String,
) -> Engine {
  case engine.trace {
    False -> engine
    True ->
      append_trace(
        engine,
        goal_format.format_goal(act.procedure, act.regs, heap) <> suffix,
      )
  }
}

fn append_trace(engine: Engine, line: String) -> Engine {
  Engine(..engine, trace_lines: list.append(engine.trace_lines, [line]))
}

/// The engine's current heap (query outputs are read from here after `run`).
pub fn heap(engine: Engine) -> Heap {
  engine.heap
}

/// The captured `_output/1` program-output lines, in emission order (T034).
pub fn captured_output(engine: Engine) -> List(String) {
  engine.output
}

/// The `'_send_to_ui'/1` UI lines, in emission order — the separate UI sink
/// (madGLP-spec §12.4/§12.5). In Dart these go to a Flutter callback; there is no
/// Flutter here, so the embedder drains them from the engine. Kept distinct from
/// `captured_output` on purpose: §12.6 defines UI and network output as different
/// channels, and merged they could not be told apart again (Gabi §1.14 2026-07-20).
pub fn captured_ui(engine: Engine) -> List(String) {
  engine.ui
}

/// Is there anything left to reduce RIGHT NOW? (T054 quiescence observation —
/// read-only; `False` means the queue is drained, saying nothing about suspended
/// goals.)
pub fn has_runnable(engine: Engine) -> Bool {
  !types.is_empty(engine.queue)
}

/// How many goals remain in the store once the queue is drained — the suspended
/// population (T054 quiescence observation; mirrors `terminal_status`'s emptiness
/// check as a count).
pub fn goal_count(engine: Engine) -> Int {
  dict.size(engine.goals)
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
        // `mad` (T050.A2 madGLP state) is threaded by the A3 MadEngine, not this
        // pure scheduler — always `None` on this path, ignored here.
        runner.Reduced(
          heap: h,
          woken: woken,
          spawned: spawned,
          output: out,
          ui: ui_out,
          mad: _,
          link: _,
        ) -> {
          let engine =
            Engine(
              ..engine,
              heap: h,
              goals: dict.delete(engine.goals, act.goal_id),
              output: list.append(engine.output, out),
              ui: list.append(engine.ui, ui_out),
            )
          // Trace the committed reduction as `head :- body` (Dart onReduction).
          let engine = trace_reduction(engine, act, spawned, h)
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
          let engine = trace_terminal(engine, act, h, " \u{2192} suspended")
          let on_list = on |> set.to_list |> list.sort(int.compare)
          #(engine, StepSuspended(act.goal_id, act.procedure, on_list))
        }
        runner.Failed(heap: h) -> {
          let engine =
            Engine(
              ..engine,
              heap: h,
              goals: dict.delete(engine.goals, act.goal_id),
            )
          let engine = trace_terminal(engine, act, h, " \u{2192} failed")
          #(engine, StepFailed(act.goal_id, act.procedure))
        }
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

// ── madGLP-aware stepping (T050.A3) ────────────────────────────────────────────
//
// The pure `step` above runs a reduction with NO madGLP state (`ctx.mad == None`).
// `step_mad` is the driver the A3 `MadEngine` calls: it injects the caller's
// `MadState` (W_p + a per-step-empty M_p / spawn scratch) into the reduction, reads
// the updated `MadState` back off `Reduced.mad`, LOWERS the reader-branch
// `global_send` spawns globalization emitted into REAL runnable `global_send/3`
// goals (so the ordinary suspension machinery watches the reader and fires `_send`
// on binding — spec §4/§8.1 "Note on Outgoing Messages"), and hands the outgoing
// message set (drained M_p) back to the caller as the returned `MadState`'s `m_p`
// (the Send transaction, §8.2 — enabled every step, so M_p never accumulates).
//
// W_p persists across steps in the returned `MadState`; the index counter lives IN
// W_p (T050.A0). The pure `StepReduced` outcome is UNCHANGED — the outbound messages
// ride on the separately-returned `MadState`, exactly the ratified contract shape
// `step -> #(_, StepOutcome, List(Message))` (contracts/madglp-port.md).

/// One madGLP reduction: like `step`, but threading `mad_in` through the reduction
/// and returning the updated `MadState` (whose `m_p` is this step's drained outgoing
/// messages and whose `w_p` persists; `mad_spawns` is always `[]` on return — lowered
/// into the run queue here). A lowering failure (`global_send/3` not loaded) surfaces
/// as `StepErrored`, never a silent drop.
pub fn step_mad(
  engine: Engine,
  reduction_budget: Int,
  mad_in: MadState,
) -> #(Engine, StepOutcome, MadState) {
  case types.dequeue(engine.queue) {
    Error(_) -> #(engine, StepIdle, mad_in)
    Ok(#(act, queue)) -> {
      let engine = Engine(..engine, queue: queue)
      let ctx = runner.with_mad(runner.new_context(engine.heap, act.regs), mad_in)
      case runner.reduce(engine.program, ctx, act.resume_pc, reduction_budget) {
        runner.Reduced(
          heap: h,
          woken: woken,
          spawned: spawned,
          output: out,
          ui: ui_out,
          mad: mad_o,
          link: _,
        ) -> {
          // In madGLP mode the runner always returns `Some` (we injected `Some`);
          // `None` would be an engine invariant break — treat it as no effect.
          let mad_out = case mad_o {
            Some(m) -> m
            None -> mad_in
          }
          let engine =
            Engine(
              ..engine,
              heap: h,
              goals: dict.delete(engine.goals, act.goal_id),
              output: list.append(engine.output, out),
              ui: list.append(engine.ui, ui_out),
            )
          let engine = trace_reduction(engine, act, spawned, h)
          let #(engine, spawned_ids) =
            list.map_fold(spawned, engine, spawn_goal)
          let engine = list.fold(woken, engine, reactivate)
          let woken_ids = list.map(woken, fn(ref) { ref.goal_id })
          // Lower the accumulated reader-branch `global_send` spawns into real goals.
          case lower_mad_spawns(engine, mad_out.mad_spawns) {
            Ok(engine) -> #(
              engine,
              StepReduced(act.goal_id, act.procedure, woken_ids, spawned_ids),
              MadState(..mad_out, mad_spawns: []),
            )
            Error(reason) -> #(engine, StepErrored(runner.Malformed(reason)), mad_in)
          }
        }
        runner.Suspended(heap: h, on: on) -> {
          let engine = suspend_goal(Engine(..engine, heap: h), act, on)
          let engine = trace_terminal(engine, act, h, " \u{2192} suspended")
          let on_list = on |> set.to_list |> list.sort(int.compare)
          #(engine, StepSuspended(act.goal_id, act.procedure, on_list), mad_in)
        }
        runner.Failed(heap: h) -> {
          let engine =
            Engine(
              ..engine,
              heap: h,
              goals: dict.delete(engine.goals, act.goal_id),
            )
          let engine = trace_terminal(engine, act, h, " \u{2192} failed")
          #(engine, StepFailed(act.goal_id, act.procedure), mad_in)
        }
        runner.BudgetExhausted(heap: h) -> #(
          Engine(..engine, heap: h),
          StepErrored(runner.Malformed(
            "reduction budget exhausted in goal " <> act.procedure,
          )),
          mad_in,
        )
        runner.RunnerError(reason: fault) -> #(engine, StepErrored(fault), mad_in)
      }
    }
  }
}

/// Lower each `global_send` spawn into a runnable `global_send/3` goal and enqueue it
/// (spec §4 / §5.1 reader branch). The spawn's `watch_addr` is the WRITER of the
/// watched pair; the goal reads its paired READER `Y?`, so its `known(Y?)` guard
/// suspends until the writer binds, then fires `_send` (T050.A2) — reusing the
/// suspension machinery, no bespoke onBind. `Error` if `global_send/3` is not loaded.
fn lower_mad_spawns(engine: Engine, spawns: List(Spawn)) -> Result(Engine, String) {
  list.try_fold(spawns, engine, fn(engine, sp) {
    case program.label_pc(engine.program, "global_send/3") {
      Error(_) ->
        Error(
          "global_send/3 not loaded — cannot lower a madGLP reader spawn "
          <> "(prelude wiring is T050.A4)",
        )
      Ok(entry_pc) -> {
        let reader = heap.paired_reader(engine.heap, sp.watch_addr)
        let regs =
          program.new_regs()
          |> program.set_reg(0, terms.VarRef(reader))
          |> program.set_reg(1, global_name.to_term(sp.name))
          |> program.set_reg(2, sp.dest)
        let #(engine, _id) = spawn_goal(engine, SpawnReq("global_send/3", entry_pc, regs))
        Ok(engine)
      }
    }
  })
}

/// Enqueue the `global_send` spawns a Receive-side Localize emitted (spec §5.2 `_w`
/// branch) — the public seam the `MadEngine` Receive transaction uses (same lowering
/// as `step_mad`'s reduce-side spawns). `Error` if `global_send/3` is not loaded.
pub fn enqueue_global_sends(
  engine: Engine,
  spawns: List(Spawn),
) -> Result(Engine, String) {
  lower_mad_spawns(engine, spawns)
}

// ── link-aware stepping (T050.C5) ──────────────────────────────────────────────
//
// The pure `step` and the madGLP `step_mad` both run with NO link state
// (`ctx.link == None`), so a `_link_*` kernel reached from either fails non-fatally
// (`runner.link_spawn`'s None branch). `step_link` is the missing driver: it injects the
// caller's `LinkState` into the reduction, reads the updated one back off `Reduced.link`,
// and LOWERS the egress drainers establishment accumulated into real runnable
// `link_drain/3` goals — the exact shape `step_mad` uses for `global_send/3`, and for the
// same reason: the kernels run deep inside `runner.reduce`, which owns neither goal
// identity nor the run queue.
//
// This is ONE seam rather than a per-step-type special case, and C6 confirmed the shape:
// the ingress pump needed exactly this injection point, and landed as an inbox drain
// (`ingest_inbound`) next to `take_drains` rather than a second driver.

/// One link-aware reduction: like `step`, but threading `link_in` through the reduction
/// and returning the updated `LinkState` (whose `drains` is always `[]` on return — the
/// requests are lowered into the run queue here). A lowering failure (`link_drain/3` not
/// loaded) surfaces as `StepErrored`, never a silent drop: an unlowered drainer is a link
/// whose `Out` stream is established but never reaches the wire.
///
/// INGRESS RUNS FIRST (C6). Every buffered inbound frame is applied to the heap before
/// the queue is even consulted, so a frame that arrived while the engine sat at
/// quiescence wakes its suspended `link_recv` and that goal is dequeued by this very
/// step. Draining after the reduction instead would report `StepIdle` on the step that
/// ingested, and a driver that stops on `StepIdle` would never run the woken goal.
pub fn step_link(
  engine: Engine,
  reduction_budget: Int,
  link_in: LinkState,
) -> #(Engine, StepOutcome, LinkState) {
  let #(engine, link_in) = ingest_inbound(engine, link_in)
  case types.dequeue(engine.queue) {
    Error(_) -> #(engine, StepIdle, link_in)
    Ok(#(act, queue)) -> {
      let engine = Engine(..engine, queue: queue)
      let ctx =
        runner.with_link(runner.new_context(engine.heap, act.regs), link_in)
      case runner.reduce(engine.program, ctx, act.resume_pc, reduction_budget) {
        runner.Reduced(
          heap: h,
          woken: woken,
          spawned: spawned,
          output: out,
          ui: ui_out,
          mad: _,
          link: link_o,
        ) -> {
          // We injected `Some`, so the runner always returns `Some`; `None` would be an
          // engine invariant break — treat it as no effect rather than losing the state.
          let link_out = case link_o {
            Some(l) -> l
            None -> link_in
          }
          let engine =
            Engine(
              ..engine,
              heap: h,
              goals: dict.delete(engine.goals, act.goal_id),
              output: list.append(engine.output, out),
              ui: list.append(engine.ui, ui_out),
            )
          let engine = trace_reduction(engine, act, spawned, h)
          let #(engine, spawned_ids) = list.map_fold(spawned, engine, spawn_goal)
          let engine = list.fold(woken, engine, reactivate)
          let woken_ids = list.map(woken, fn(ref) { ref.goal_id })
          let #(link_out, drains) = link_runtime.take_drains(link_out)
          case lower_link_drains(engine, drains) {
            Ok(engine) -> #(
              engine,
              StepReduced(act.goal_id, act.procedure, woken_ids, spawned_ids),
              link_out,
            )
            Error(reason) -> #(
              engine,
              StepErrored(runner.Malformed(reason)),
              link_in,
            )
          }
        }
        runner.Suspended(heap: h, on: on) -> {
          let engine = suspend_goal(Engine(..engine, heap: h), act, on)
          let engine = trace_terminal(engine, act, h, " \u{2192} suspended")
          let on_list = on |> set.to_list |> list.sort(int.compare)
          #(engine, StepSuspended(act.goal_id, act.procedure, on_list), link_in)
        }
        runner.Failed(heap: h) -> {
          let engine =
            Engine(
              ..engine,
              heap: h,
              goals: dict.delete(engine.goals, act.goal_id),
            )
          let engine = trace_terminal(engine, act, h, " \u{2192} failed")
          #(engine, StepFailed(act.goal_id, act.procedure), link_in)
        }
        runner.BudgetExhausted(heap: h) -> #(
          Engine(..engine, heap: h),
          StepErrored(runner.Malformed(
            "reduction budget exhausted in goal " <> act.procedure,
          )),
          link_in,
        )
        runner.RunnerError(reason: fault) -> #(
          engine,
          StepErrored(fault),
          link_in,
        )
      }
    }
  }
}

/// Apply every buffered inbound frame to the heap (C6). The pump processes only ever
/// ENQUEUE — this is the runner-side half that touches the heap, keeping the single-owner
/// invariant the whole pump design exists to protect.
///
/// Each applied item extends its link's `In` stream and may wake goals suspended on it;
/// those are re-enqueued exactly as a commit-time binding's are. The advanced ingress
/// cursor comes back on the handle inside the returned registry, so ingress state is
/// threaded, never held on the side. An empty inbox is the overwhelmingly common case and
/// costs one non-blocking receive.
fn ingest_inbound(engine: Engine, state: LinkState) -> #(Engine, LinkState) {
  list.fold(
    link_pump.drain(state.inbox),
    #(engine, state),
    fn(acc, item) {
      let #(engine, state) = acc
      let link_pump.Applied(heap, links, woken) =
        link_pump.apply_item(engine.heap, state.links, item)
      let engine = list.fold(woken, Engine(..engine, heap: heap), reactivate)
      #(engine, link_runtime.with_links(state, links))
    },
  )
}

/// Lower each accumulated `DrainRequest` into a runnable `link_drain/3` goal and enqueue
/// it (self.glp:557-564). Register 0 is the channel `Out` READER, so the drainer's first
/// head argument `[Msg|Rest]` (declared `Stream(X)?`) SUSPENDS until the program conses
/// via `link_send/3` — the ordinary suspension/reactivation machinery drives egress and
/// no `heap.onBind` is needed (deviation D-2). `Out = []` takes the drainer's second
/// clause into `'_link_close'(LinkId?, eos)`, the graceful stream-end close (FR-024).
/// `Error` if `link_drain/3` is not loaded.
fn lower_link_drains(
  engine: Engine,
  drains: List(DrainRequest),
) -> Result(Engine, String) {
  list.try_fold(drains, engine, fn(engine, d) {
    case program.label_pc(engine.program, "link_drain/3") {
      Error(_) ->
        Error(
          "link_drain/3 not loaded — cannot lower the egress drainer for an "
          <> "established link (it ships in programs/self.glp)",
        )
      Ok(entry_pc) -> {
        let regs =
          program.new_regs()
          |> program.set_reg(0, terms.VarRef(d.out_reader))
          |> program.set_reg(1, d.link_id)
          |> program.set_reg(2, d.to_peer)
        let #(engine, _id) =
          spawn_goal(engine, SpawnReq("link_drain/3", entry_pc, regs))
        Ok(engine)
      }
    }
  })
}

/// Replace the engine's heap (the `MadEngine` Receive transaction threads Localize's
/// freshly-allocated heap back in before binding the target writer).
pub fn set_heap(engine: Engine, heap: Heap) -> Engine {
  Engine(..engine, heap: heap)
}

/// Bind a local writer to `value` and re-enqueue every goal the binding wakes (the
/// Receive transaction's "assign X := T↓, reactivate suspended goals" — spec §8.3).
/// `Error` if the writer is already bound (a Receive on a consumed entry is an
/// upstream violation, surfaced not swallowed — reliability dedup is T052).
pub fn bind_and_wake(
  engine: Engine,
  writer_addr: Int,
  value: terms.Term,
) -> Result(Engine, String) {
  case heap.bind_writer(engine.heap, writer_addr, value) {
    Ok(#(h, woken)) -> {
      let engine = Engine(..engine, heap: h)
      Ok(list.fold(woken, engine, reactivate))
    }
    Error(_) ->
      Error(
        "madGLP Receive: writer "
        <> int.to_string(writer_addr)
        <> " already bound",
      )
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

/// The terminal status for the engine's CURRENT drained state (Success vs
/// Suspended) — the facade `step` seam builds the result envelope from this once
/// the queue is idle (T029 cap 3 completion).
pub fn status(engine: Engine) -> RunStatus {
  terminal_status(engine)
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
