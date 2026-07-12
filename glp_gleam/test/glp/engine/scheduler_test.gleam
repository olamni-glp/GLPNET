//// Scheduler run-loop tests (feature 050, T022).
////
//// Boot a goal, run the scheduler to quiescence, and assert the query output —
//// the first end-to-end MULTI-GOAL execution (a clause body spawns a second
//// goal whose reduction binds the query variable).

import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/scheduler
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstTerm, VarRef}

const relay_source = "Bit ::= zero ; one.
procedure run_flip(Bit).
run_flip(R?) :- flip(zero, R).
procedure flip(Bit?, Bit).
flip(zero, one).
flip(one, zero)."

// run_flip(Out): the body spawns flip(zero, Out); flip's reduction binds Out to
// `one`. Two goals run through the queue to quiescence. Exercises boot → reduce
// → Spawn (id minted) → second reduce → commit-binds-query-var → quiescence.
pub fn run_flip_spawns_and_binds_output_test() {
  let assert Ok(outcome) = loader.load(relay_source, "")
  let prog = outcome.program
  let assert Ok(entry) = program.label_pc(prog, "run_flip/1")

  // The query's output variable.
  let #(h, out_writer, _out_reader) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, VarRef(out_writer))

  let engine = scheduler.new(prog, h)
  let #(engine, _goal_id) = scheduler.boot(engine, "run_flip/1", entry, regs)
  let #(engine, status) = scheduler.run(engine, 1000, 1000)

  status
  |> should.equal(scheduler.Success)
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("one"))))) =
    heap.deref(scheduler.heap(engine), out_writer)
}

const rendezvous_source = "Bit ::= zero ; one.
procedure pc(Bit).
pc(R?) :- flip(X?, R), set_zero(X).
procedure flip(Bit?, Bit).
flip(zero, one).
flip(one, zero).
procedure set_zero(Bit).
set_zero(zero)."

// pc(Out): the body spawns flip(X?, Out) then set_zero(X). flip runs first and
// SUSPENDS (X unbound); set_zero then binds X = zero, which WAKES flip via the
// heap suspension; flip re-runs and commits Out = one. This is the definitive
// end-to-end validation of suspend → cross-goal reactivation → resume.
pub fn suspend_then_cross_goal_reactivation_test() {
  let assert Ok(outcome) = loader.load(rendezvous_source, "")
  let prog = outcome.program
  let assert Ok(entry) = program.label_pc(prog, "pc/1")

  let #(h, out_writer, _out_reader) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, VarRef(out_writer))

  let engine = scheduler.new(prog, h)
  let #(engine, _goal_id) = scheduler.boot(engine, "pc/1", entry, regs)
  let #(engine, status) = scheduler.run(engine, 1000, 1000)

  status
  |> should.equal(scheduler.Success)
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("one"))))) =
    heap.deref(scheduler.heap(engine), out_writer)
}

// T029 cap 1 — a definitive failure. flip/2 only matches zero/one; booting it on
// the ground atom `two` (a non-Bit) matches no clause with nothing to wait on, so
// the goal Fails and the run reports Failed (not the old catch-all Quiescent).
pub fn failed_boot_reports_failed_test() {
  let assert Ok(outcome) = loader.load(relay_source, "")
  let prog = outcome.program
  let assert Ok(entry) = program.label_pc(prog, "flip/2")

  let #(h, out_writer, _out_reader) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("two")))
    |> program.set_reg(1, VarRef(out_writer))

  let engine = scheduler.new(prog, h)
  let #(engine, _goal_id) = scheduler.boot(engine, "flip/2", entry, regs)
  let #(_engine, status) = scheduler.run(engine, 1000, 1000)

  status
  |> should.equal(scheduler.Failed)
}

// T029 cap 1+2 — a permanent suspension. flip(In?, Out) reads an unbound In that
// nobody ever binds, so the goal suspends forever: status Suspended, and the
// blocking-reader set is exactly In's paired reader (allocated reader = writer+1).
pub fn suspended_boot_reports_blocking_readers_test() {
  let assert Ok(outcome) = loader.load(relay_source, "")
  let prog = outcome.program
  let assert Ok(entry) = program.label_pc(prog, "flip/2")

  let #(h, _in_writer, in_reader) = heap.allocate_variable(heap.new())
  let #(h, out_writer, _out_reader) = heap.allocate_variable(h)
  let regs =
    program.new_regs()
    |> program.set_reg(0, VarRef(in_reader))
    |> program.set_reg(1, VarRef(out_writer))

  let engine = scheduler.new(prog, h)
  let #(engine, _goal_id) = scheduler.boot(engine, "flip/2", entry, regs)
  let #(_engine, status) = scheduler.run(engine, 1000, 1000)

  // Blocking reader = the paired reader of the writer flip suspended on.
  status
  |> should.equal(scheduler.Suspended([in_reader]))
}

// T029 cap 3 — real single-step. run_flip boots, reduces once (spawning flip),
// reduces flip once (binding Out), then the queue is idle. Each step reports its
// own goal + effects rather than a fuel=1 black box.
pub fn single_step_reports_per_step_outcomes_test() {
  let assert Ok(outcome) = loader.load(relay_source, "")
  let prog = outcome.program
  let assert Ok(entry) = program.label_pc(prog, "run_flip/1")

  let #(h, out_writer, _out_reader) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, VarRef(out_writer))

  let engine = scheduler.new(prog, h)
  let #(engine, _goal_id) = scheduler.boot(engine, "run_flip/1", entry, regs)

  // Step 1: run_flip reduces and spawns exactly one body goal (flip).
  let #(engine, first) = scheduler.step(engine, 1000)
  let assert scheduler.StepReduced(_, "run_flip/1", [], [_flip_id]) = first

  // Step 2: flip reduces (binds Out); no spawn, no wake.
  let #(engine, second) = scheduler.step(engine, 1000)
  let assert scheduler.StepReduced(_, "flip/2", [], []) = second

  // Step 3: queue drained — idle.
  let #(engine, third) = scheduler.step(engine, 1000)
  third |> should.equal(scheduler.StepIdle)

  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("one"))))) =
    heap.deref(scheduler.heap(engine), out_writer)
}
