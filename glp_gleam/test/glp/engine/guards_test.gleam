//// Guard evaluation tests (feature 050, T023).
////
//// Three-valued guards over σ̂w + heap: SUCCEED / SUSPEND / FAIL, driven through
//// the real load pipeline + one runner reduction.

import glp/bytecode/program
import glp/compiler/loader
import glp/engine/runner
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstTerm, VarRef}

// Minimal prelude declaring the system guard predicates these programs use
// (the real declarations live in programs/self.glp).
const guard_prelude = "procedure ground(_?).
procedure =?=(_?, _?)."

fn reduce_goal(
  src: String,
  entry: String,
  regs: program.XRegs,
  h: heap.Heap,
) -> runner.ReduceOutcome {
  let assert Ok(outcome) = loader.load(src, guard_prelude)
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, entry)
  runner.reduce(prog, runner.new_context(h, regs), kappa, 1000)
}

const ground_source = "Bit ::= zero ; one.
Answer ::= grounded ; not_grounded.
procedure chk(Bit?, Answer).
chk(X, grounded) :- ground(X?) | true.
chk(_, not_grounded) :- otherwise | true."

// chk(zero, R): X is ground → ground(X?) SUCCEEDS → clause 1 commits R = grounded.
pub fn ground_guard_succeeds_on_ground_test() {
  let #(h, r, _) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("zero")))
    |> program.set_reg(1, VarRef(r))
  let assert runner.Reduced(heap: h2, ..) =
    reduce_goal(ground_source, "chk/2", regs, h)
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("grounded"))))) =
    heap.deref(h2, r)
}

const iszero_source = "Bit ::= zero ; one.
Answer ::= yes ; no.
procedure is_zero(Bit?, Answer).
is_zero(zero, yes).
is_zero(_, no) :- otherwise | true."

// is_zero(one, R): clause 1 head-MISMATCHES (fails, does not suspend); clause 2's
// `otherwise` fires (U empty) → R = no. Exercises otherwise-after-failure.
pub fn otherwise_fires_after_failure_test() {
  let #(h, r, _) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("one")))
    |> program.set_reg(1, VarRef(r))
  let assert runner.Reduced(heap: h2, ..) =
    reduce_goal(iszero_source, "is_zero/2", regs, h)
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("no"))))) =
    heap.deref(h2, r)
}

const eq_source = "Bit ::= zero ; one.
Answer ::= same ; diff.
procedure eq(Bit?, Bit?, Answer).
eq(X, Y, same) :- X? =?= Y? | true.
eq(_, _, diff) :- otherwise | true."

// eq(zero, zero, R): X =?= Y holds → R = same.
pub fn ground_equal_succeeds_on_equal_test() {
  let #(h, r, _) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("zero")))
    |> program.set_reg(1, ConstTerm(ConstAtom("zero")))
    |> program.set_reg(2, VarRef(r))
  let assert runner.Reduced(heap: h2, ..) =
    reduce_goal(eq_source, "eq/3", regs, h)
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("same"))))) =
    heap.deref(h2, r)
}

// eq(zero, one, R): X =?= Y is FALSE → clause 1 fails → otherwise → R = diff.
pub fn ground_equal_fails_then_otherwise_test() {
  let #(h, r, _) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstAtom("zero")))
    |> program.set_reg(1, ConstTerm(ConstAtom("one")))
    |> program.set_reg(2, VarRef(r))
  let assert runner.Reduced(heap: h2, ..) =
    reduce_goal(eq_source, "eq/3", regs, h)
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("diff"))))) =
    heap.deref(h2, r)
}
