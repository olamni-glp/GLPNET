//// T054 — the quiescence oracle (GAP-G6): quiescent vs deadlocked vs running.
////
//// Two layers, matching the module's judge/observe split:
////   * the PURE judge truth table (every arm, including the in-flight override that
////     makes "waiting on the network" report Running, not Deadlocked);
////   * `observe` over REAL engines in each terminal state — completed (Success),
////     suspended-forever (the deadlock half), and mid-run — driven through the
////     shipped prelude so the observation seam is the one T056's driver will use.

import gleam/bit_array
import gleam/dynamic.{type Dynamic}
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/scheduler
import glp/link/quiescence.{
  Deadlocked, NodeObservation, Quiescent, Running,
}
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstTerm, VarRef}

// ── the pure judge ───────────────────────────────────────────────────────────

pub fn all_idle_no_suspension_is_quiescent_test() {
  quiescence.judge([NodeObservation(0, 0, 0), NodeObservation(0, 0, 0)], 0)
  |> should.equal(Quiescent)
}

pub fn suspension_with_nothing_in_flight_is_deadlock_test() {
  quiescence.judge([NodeObservation(0, 3, 0), NodeObservation(0, 0, 0)], 0)
  |> should.equal(Deadlocked)
}

pub fn a_runnable_goal_anywhere_is_running_test() {
  quiescence.judge([NodeObservation(0, 3, 0), NodeObservation(1, 0, 0)], 0)
  |> should.equal(Running)
}

pub fn an_in_flight_frame_overrides_deadlock_test() {
  // The load-bearing arm: the same suspension picture as the deadlock case, but ONE
  // frame in flight — the wake may still arrive, so this is waiting, not deadlock.
  quiescence.judge([NodeObservation(0, 3, 0), NodeObservation(0, 0, 0)], 1)
  |> should.equal(Running)
}

pub fn a_buffered_inbound_item_is_running_test() {
  quiescence.judge([NodeObservation(0, 1, 1)], 0)
  |> should.equal(Running)
}

pub fn empty_run_is_quiescent_test() {
  quiescence.judge([], 0) |> should.equal(Quiescent)
}

// ── double-collect stability ─────────────────────────────────────────────────

pub fn terminal_verdict_needs_two_agreeing_snapshots_test() {
  let dead = #([NodeObservation(0, 1, 0)], 0)
  let live = #([NodeObservation(0, 1, 0)], 1)
  quiescence.judge_stable(dead, dead) |> should.equal(Deadlocked)
  quiescence.judge_stable(dead, live) |> should.equal(Running)
  quiescence.judge_stable(live, dead) |> should.equal(Running)
  let done = #([NodeObservation(0, 0, 0)], 0)
  quiescence.judge_stable(done, done) |> should.equal(Quiescent)
  // Disagreeing terminals (a wake slipped between snapshots) → still Running.
  quiescence.judge_stable(dead, done) |> should.equal(Running)
}

// ── observe over real engines ────────────────────────────────────────────────

const probe_source = "-mode(system).
procedure done_goal(_?).
done_goal(x).
procedure waiting(_?).
waiting(V) :- ground(V?) | true."

@external(erlang, "file", "read_file")
fn read_file(path: String) -> Result(BitArray, Dynamic)

fn probe_program() -> program.BytecodeProgram {
  // Loaded over the shipped prelude — `ground/1` is a prelude guard.
  let assert Ok(bytes) = read_file("../programs/self.glp")
  let assert Ok(self_source) = bit_array.to_string(bytes)
  let assert Ok(outcome) = loader.load(probe_source, self_source)
  outcome.program
}

/// A goal that reduces away: after the run the engine observes (0,0) → Quiescent.
pub fn completed_engine_observes_quiescent_test() {
  let prog = probe_program()
  let assert Ok(entry) = program.label_pc(prog, "done_goal/1")
  let regs = program.new_regs() |> program.set_reg(0, ConstTerm(ConstAtom("x")))
  let #(engine, _) =
    scheduler.boot(scheduler.new(prog, heap.new()), "done_goal/1", entry, regs)
  let #(engine, status) = scheduler.run(engine, 1000, 100)
  status |> should.equal(scheduler.Success)
  quiescence.judge([quiescence.observe(engine, 0)], 0)
  |> should.equal(Quiescent)
}

/// A goal suspended on a reader nobody will ever bind: (0 runnable, 1 suspended),
/// nothing in flight → Deadlocked; the SAME observation with one frame claimed in
/// flight → Running. One engine state, both sides of the distinction.
pub fn suspended_engine_observes_deadlock_unless_in_flight_test() {
  let prog = probe_program()
  let assert Ok(entry) = program.label_pc(prog, "waiting/1")
  let #(h, _w, r) = heap.allocate_variable(heap.new())
  let regs = program.new_regs() |> program.set_reg(0, VarRef(r))
  let #(engine, _) =
    scheduler.boot(scheduler.new(prog, h), "waiting/1", entry, regs)
  let #(engine, status) = scheduler.run(engine, 1000, 100)
  let assert scheduler.Suspended(_) = status

  let observation = quiescence.observe(engine, 0)
  observation.runnable |> should.equal(0)
  observation.suspended |> should.equal(1)
  quiescence.judge([observation], 0) |> should.equal(Deadlocked)
  quiescence.judge([observation], 1) |> should.equal(Running)
}

/// An engine with a booted-but-unreduced goal observes runnable → Running, without
/// consulting suspension at all.
pub fn unrun_engine_observes_running_test() {
  let prog = probe_program()
  let assert Ok(entry) = program.label_pc(prog, "done_goal/1")
  let regs = program.new_regs() |> program.set_reg(0, ConstTerm(ConstAtom("x")))
  let #(engine, _) =
    scheduler.boot(scheduler.new(prog, heap.new()), "done_goal/1", entry, regs)
  quiescence.judge([quiescence.observe(engine, 0)], 0)
  |> should.equal(Running)
}
