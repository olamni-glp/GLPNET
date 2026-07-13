//// Generic `Guard` opcode + native body-kernel tests (feature 050, T024).
////
//// Exercises the two T024 deliverables through the real load pipeline + one
//// runner reduction: (1) the generic `Guard` opcode — arithmetic comparisons,
//// type tests, `=?=`, standard-order `@<`, and suspend-on-unbound-reader — and
//// (2) the native body kernels `'_add'`/`'_sub'`/`'_mul'` via the BODY `Spawn`
//// kernel-dispatch path, binding the output writer.

import gleam/set
import glp/bytecode/program
import glp/compiler/loader
import glp/engine/kernels
import glp/engine/runner
import glp/runtime/heap
import glp/runtime/terms.{ConstAtom, ConstInt, ConstReal, ConstTerm, VarRef}

// Minimal prelude declaring the system guards + kernels these programs use
// (the real declarations live in programs/self.glp).
const prelude = "procedure <(_?, _?).
procedure >(_?, _?).
procedure number(_?).
procedure integer(_?).
procedure =?=(_?, _?).
procedure @<(_?, _?)."

fn reduce_goal(
  src: String,
  entry: String,
  regs: program.XRegs,
  h: heap.Heap,
) -> runner.ReduceOutcome {
  let assert Ok(outcome) = loader.load(src, prelude)
  let prog = outcome.program
  let assert Ok(kappa) = program.label_pc(prog, entry)
  runner.reduce(prog, runner.new_context(h, regs), kappa, 2000)
}

// ── generic Guard: arithmetic comparison ─────────────────────────────────────

const cmp_source = "Answer ::= less ; notless.
procedure chk(Integer?, Answer).
chk(X, less) :- X? < 10 | true.
chk(_, notless) :- otherwise | true."

// chk(5, R): 5 < 10 SUCCEEDS → R = less.
pub fn lt_guard_succeeds_test() {
  let #(h, r, _) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstInt(5)))
    |> program.set_reg(1, VarRef(r))
  let assert runner.Reduced(heap: h2, ..) =
    reduce_goal(cmp_source, "chk/2", regs, h)
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("less"))))) =
    heap.deref(h2, r)
}

// chk(20, R): 20 < 10 FAILS → otherwise → R = notless.
pub fn lt_guard_fails_then_otherwise_test() {
  let #(h, r, _) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstInt(20)))
    |> program.set_reg(1, VarRef(r))
  let assert runner.Reduced(heap: h2, ..) =
    reduce_goal(cmp_source, "chk/2", regs, h)
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("notless"))))) =
    heap.deref(h2, r)
}

// chk(X, R) with X an UNBOUND reader: `X? < 10` SUSPENDS (an unbound reader, not
// a failure) → `otherwise` does NOT fire → the goal suspends.
pub fn lt_guard_suspends_on_unbound_reader_test() {
  let #(h, w, rd) = heap.allocate_variable(heap.new())
  let #(h, r, _) = heap.allocate_variable(h)
  let regs =
    program.new_regs()
    |> program.set_reg(0, VarRef(rd))
    |> program.set_reg(1, VarRef(r))
  let assert runner.Suspended(on: on, ..) =
    reduce_goal(cmp_source, "chk/2", regs, h)
  // Suspended on the writer backing the unbound reader.
  let assert True = set.contains(on, w)
}

// ── generic Guard: integer type test ─────────────────────────────────────────

const int_source = "Answer ::= yes ; no.
procedure is_int(Integer?, Answer).
is_int(X, yes) :- integer(X?) | true.
is_int(_, no) :- otherwise | true."

pub fn integer_type_test_succeeds_test() {
  let #(h, r, _) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstInt(7)))
    |> program.set_reg(1, VarRef(r))
  let assert runner.Reduced(heap: h2, ..) =
    reduce_goal(int_source, "is_int/2", regs, h)
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("yes"))))) =
    heap.deref(h2, r)
}

// ── generic Guard: =?= (non-specialized, one const operand) ───────────────────

const eq_source = "Answer ::= same ; diff.
procedure eqfive(Integer?, Answer).
eqfive(X, same) :- X? =?= 5 | true.
eqfive(_, diff) :- otherwise | true."

pub fn ground_equal_generic_succeeds_test() {
  let #(h, r, _) = heap.allocate_variable(heap.new())
  let regs =
    program.new_regs()
    |> program.set_reg(0, ConstTerm(ConstInt(5)))
    |> program.set_reg(1, VarRef(r))
  let assert runner.Reduced(heap: h2, ..) =
    reduce_goal(eq_source, "eqfive/2", regs, h)
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstAtom("same"))))) =
    heap.deref(h2, r)
}

// ── body kernels (direct dispatch) ───────────────────────────────────────────
//
// Kernels are reachable in a real program ONLY via `:=` (self.glp's clauses call
// them), which needs the prelude compiled into the program — a T029 concern; a
// direct kernel call in a user body correctly fails type-check (Dart parity).
// So T024 unit-tests the kernel module directly (arithmetic + writer binding);
// the Spawn→kernel wiring is exercised end-to-end via `:=` at T030.

// Allocate a heap with one input const and one unbound output writer, dispatch
// a binary kernel, assert the writer binds to `expect`.
fn assert_binary(name: String, x: Int, y: Int, expect: terms.Term) {
  let #(h, z, _) = heap.allocate_variable(heap.new())
  let assert Ok(kernels.KSuccess(heap: h2, ..)) =
    kernels.dispatch(h, name, 3, [
      ConstTerm(ConstInt(x)),
      ConstTerm(ConstInt(y)),
      VarRef(z),
    ])
  let assert Ok(#(_, heap.Bound(bound))) = heap.deref(h2, z)
  let assert True = bound == expect
}

pub fn add_kernel_binds_output_test() {
  assert_binary("_add", 2, 3, ConstTerm(ConstInt(5)))
}

pub fn sub_kernel_binds_output_test() {
  assert_binary("_sub", 10, 4, ConstTerm(ConstInt(6)))
}

pub fn mul_kernel_binds_output_test() {
  assert_binary("_mul", 6, 7, ConstTerm(ConstInt(42)))
}

// `/` always yields a float (Dart `a / b`): 7 / 2 = 3.5.
pub fn div_kernel_yields_float_test() {
  assert_binary("_div", 7, 2, ConstTerm(ConstReal(3.5)))
}

// `//` is truncating integer division: 7 // 2 = 3.
pub fn idiv_kernel_truncates_test() {
  assert_binary("_idiv", 7, 2, ConstTerm(ConstInt(3)))
}

pub fn mod_kernel_test() {
  assert_binary("_mod", 17, 5, ConstTerm(ConstInt(2)))
}

pub fn neg_kernel_test() {
  let #(h, z, _) = heap.allocate_variable(heap.new())
  let assert Ok(kernels.KSuccess(heap: h2, ..)) =
    kernels.dispatch(h, "_neg", 2, [ConstTerm(ConstInt(9)), VarRef(z)])
  let assert Ok(#(_, heap.Bound(ConstTerm(ConstInt(-9))))) = heap.deref(h2, z)
}

// Division by zero aborts (Dart divKernel).
pub fn div_by_zero_aborts_test() {
  let #(h, z, _) = heap.allocate_variable(heap.new())
  let assert Ok(kernels.KAbort(_)) =
    kernels.dispatch(h, "_div", 3, [
      ConstTerm(ConstInt(1)),
      ConstTerm(ConstInt(0)),
      VarRef(z),
    ])
}

// Output not a writer aborts (Dart _bindResult).
pub fn nonwriter_output_aborts_test() {
  let assert Ok(kernels.KAbort(_)) =
    kernels.dispatch(heap.new(), "_add", 3, [
      ConstTerm(ConstInt(1)),
      ConstTerm(ConstInt(2)),
      ConstTerm(ConstInt(3)),
    ])
}

// An unregistered kernel name is not a kernel (→ runner reports unresolved).
pub fn unregistered_kernel_is_error_test() {
  let assert Error(Nil) = kernels.dispatch(heap.new(), "_frobnicate", 3, [])
}
