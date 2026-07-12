//// Runner reduction tests (feature 050, T021 slice 21a/b).
////
//// End-to-end: load a real program through the T020 pipeline, then drive one
//// goal reduction through the three-phase runner and assert the observable
//// outcome (writer bound / goal suspended / definitive failure). This is the
//// engine-value unit seam the proof-obligations contract names.

import gleam/set
import gleeunit/should
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/runner
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstTerm, VarRef}

const flip_source = "Bit ::= zero ; one.
procedure flip(Bit?, Bit).
flip(zero, one).
flip(one, zero)."

fn load_flip() -> program.BytecodeProgram {
  let assert Ok(outcome) = loader.load(flip_source, "")
  outcome.program
}

// flip(zero, X) → clause 1 commits, X is bound to `one`.
pub fn flip_first_clause_binds_writer_test() {
  let prog = load_flip()
  let assert Ok(kappa) = program.label_pc(prog, "flip/2")
  // Fresh query variable X.
  let #(h, x_writer, _x_reader) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("zero")))
    |> program.set_reg(1, VarRef(x_writer))
  let outcome = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  let assert runner.Reduced(heap: h2, spawned: _) = outcome
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("one"))))) =
    heap.deref(h2, x_writer)
}

// flip(one, X) → clause 1 head-mismatches and soft-fails; clause 2 commits,
// X is bound to `zero`. Exercises soft-fail + forward clause scan.
pub fn flip_second_clause_binds_writer_test() {
  let prog = load_flip()
  let assert Ok(kappa) = program.label_pc(prog, "flip/2")
  let #(h, x_writer, _x_reader) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("one")))
    |> program.set_reg(1, VarRef(x_writer))
  let outcome = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  let assert runner.Reduced(heap: h2, spawned: _) = outcome
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("zero"))))) =
    heap.deref(h2, x_writer)
}

// flip(A?, Y): the first argument is an unbound READER — no clause's HEAD can be
// determined, so the goal suspends on the reader's paired writer.
pub fn flip_suspends_on_unbound_reader_test() {
  let prog = load_flip()
  let assert Ok(kappa) = program.label_pc(prog, "flip/2")
  // A: an unbound variable; pass its READER half as arg 0.
  let #(h, a_writer, a_reader) = heap.allocate_variable(heap.new())
  // Y: a fresh writer for the output slot.
  let #(h, y_writer, _y_reader) = heap.allocate_variable(h)
  let regs =
    program.new_regs()
    |> program.set_reg(0, VarRef(a_reader))
    |> program.set_reg(1, VarRef(y_writer))
  let outcome = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  // Suspends on A's writer (deref of A's reader terminates there).
  let assert runner.Suspended(heap: _, on: on) = outcome
  should.be_true(set.contains(on, a_writer))
}
