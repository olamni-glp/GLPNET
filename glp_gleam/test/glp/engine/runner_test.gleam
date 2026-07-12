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
import glp/runtime/terms.{
  type Term, ConstAtom, ConstTerm, StructTerm, VarRef, cons, nil,
}

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

// ── Slice 21c: HEAD structures (writer-MGU crux) ─────────────────────────────

/// Fully resolve a term for assertions: follow VarRefs through the heap to their
/// ground value (or leave an unbound ref), recursing into structure args.
fn resolve(h: heap.Heap, term: Term) -> Term {
  case term {
    VarRef(addr) ->
      case heap.deref(h, addr) {
        Ok(#(_, heap.Bound(v))) -> resolve(h, v)
        _ -> term
      }
    StructTerm(f, args) ->
      StructTerm(f, list_map(args, fn(a) { resolve(h, a) }))
    _ -> term
  }
}

fn list_map(xs: List(a), f: fn(a) -> b) -> List(b) {
  case xs {
    [] -> []
    [x, ..rest] -> [f(x), ..list_map(rest, f)]
  }
}

const swap_source = "Bit ::= zero ; one.
Pair ::= p(Bit, Bit).
procedure swap(Pair?, Pair).
swap(p(X, Y), p(Y?, X?))."

// swap(p(zero, one), R) → R = p(one, zero). Exercises HEAD-structure READ
// (matching p(zero,one)), WRITE (building the output p in σ̂w as a tentative
// struct), UnifyVariable in both modes, and the writer-MGU commit conversion.
pub fn swap_builds_output_structure_test() {
  let assert Ok(outcome) = loader.load(swap_source, "")
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, "swap/2")
  let #(h, r_writer, _r_reader) = heap.allocate_variable(heap.new())
  let input =
    StructTerm("p", [
      ConstTerm(ConstAtom("zero")),
      ConstTerm(ConstAtom("one")),
    ])
  let regs =
    program.new_regs()
    |> program.set_reg(0, input)
    |> program.set_reg(1, VarRef(r_writer))
  let out = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  let assert runner.Reduced(heap: h2, spawned: _) = out
  let assert Ok(#(_, heap.Bound(result))) = heap.deref(h2, r_writer)
  resolve(h2, result)
  |> should.equal(
    StructTerm("p", [
      ConstTerm(ConstAtom("one")),
      ConstTerm(ConstAtom("zero")),
    ]),
  )
}

const first_bit_source = "Bit ::= zero ; one.
Bits ::= [] ; [Bit | Bits].
Maybe ::= some(Bit) ; none.
procedure first_bit(Bits?, Maybe).
first_bit([H|_], some(H?)).
first_bit([], none)."

// first_bit([one, zero], R) → R = some(one). Exercises HEAD list matching
// (HeadStructure './2' READ), UnifyVariable READ first-occurrence, UnifyVoid
// (the anonymous tail), a subsequent GetValue reader occurrence, and building
// the output `some(H?)` structure in WRITE mode.
pub fn first_bit_reads_list_head_test() {
  let assert Ok(outcome) = loader.load(first_bit_source, "")
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, "first_bit/2")
  let #(h, r_writer, _r_reader) = heap.allocate_variable(heap.new())
  let input =
    cons(ConstTerm(ConstAtom("one")), cons(ConstTerm(ConstAtom("zero")), nil()))
  let regs =
    program.new_regs()
    |> program.set_reg(0, input)
    |> program.set_reg(1, VarRef(r_writer))
  let out = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  let assert runner.Reduced(heap: h2, spawned: _) = out
  let assert Ok(#(_, heap.Bound(result))) = heap.deref(h2, r_writer)
  resolve(h2, result)
  |> should.equal(StructTerm("some", [ConstTerm(ConstAtom("one"))]))
}

// first_bit([], R) → R = none. Exercises HEAD nil matching + the second clause
// (soft-fail off clause 1's list match, then a constant-output HEAD bind).
pub fn first_bit_empty_list_test() {
  let assert Ok(outcome) = loader.load(first_bit_source, "")
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, "first_bit/2")
  let #(h, r_writer, _r_reader) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, nil())
    |> program.set_reg(1, VarRef(r_writer))
  let out = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  let assert runner.Reduced(heap: h2, spawned: _) = out
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("none"))))) =
    heap.deref(h2, r_writer)
}

// first_bit(Xs?, B): arg 0 is an unbound READER — the HEAD list match cannot be
// determined, so the goal suspends (structure match defers via Si → U).
pub fn first_bit_suspends_on_unbound_list_test() {
  let assert Ok(outcome) = loader.load(first_bit_source, "")
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, "first_bit/2")
  let #(h, xs_writer, xs_reader) = heap.allocate_variable(heap.new())
  let #(h, b_writer, _b_reader) = heap.allocate_variable(h)
  let regs =
    program.new_regs()
    |> program.set_reg(0, VarRef(xs_reader))
    |> program.set_reg(1, VarRef(b_writer))
  let out = runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)

  let assert runner.Suspended(heap: _, on: on) = out
  should.be_true(set.contains(on, xs_writer))
}
