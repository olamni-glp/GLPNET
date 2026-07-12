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
  |> should.equal(scheduler.Quiescent)
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
  |> should.equal(scheduler.Quiescent)
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("one"))))) =
    heap.deref(scheduler.heap(engine), out_writer)
}
