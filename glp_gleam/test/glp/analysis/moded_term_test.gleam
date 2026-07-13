//// Moded-term tests (feature 050, T018) — Dart moded_term.dart is the oracle:
//// mode accessor, constant/variable classification, is_consumed/is_produced,
//// the I/O transition rule (↓→↑ ok, ↑→↓ rejected), the dual involution, path
//// extraction, and byte-identical `toString` rendering.

import gleam/set
import gleeunit/should
import glp/analysis/type_checker/mode.{Input, Output}
import glp/analysis/type_checker/moded_term.{
  ModedCompound, ModedConstant, ModedPath, ModedVariable, PathStep,
}
import glp/runtime/terms

// ---------------------------------------------------------------------------
// mode_of / constructors
// ---------------------------------------------------------------------------

pub fn mode_of_test() {
  moded_term.mode_of(ModedConstant(Input, terms.ConstInt(1)))
  |> should.equal(Input)
  moded_term.mode_of(ModedCompound(Output, "f", 0, []))
  |> should.equal(Output)
  // A variable's mode is its structural mode.
  moded_term.mode_of(ModedVariable("X", True, Input))
  |> should.equal(Input)
}

pub fn list_cons_and_nil_test() {
  let cons =
    moded_term.list_cons(Input, ModedVariable("H", True, Input), moded_term.nil(Input))
  cons |> should.equal(ModedCompound(Input, "[|]", 2, [
    ModedVariable("H", True, Input),
    ModedConstant(Input, terms.ConstAtom("[]")),
  ]))
  moded_term.is_list_cons(cons) |> should.be_true
  moded_term.is_list_cons(ModedCompound(Input, "f", 2, [])) |> should.be_false
  moded_term.nil(Output) |> should.equal(ModedConstant(Output, terms.ConstAtom("[]")))
}

// ---------------------------------------------------------------------------
// Constant classification
// ---------------------------------------------------------------------------

pub fn constant_classification_test() {
  moded_term.constant_is_integer(terms.ConstInt(1)) |> should.be_true
  moded_term.constant_is_real(terms.ConstInt(1)) |> should.be_false
  moded_term.constant_is_numeric(terms.ConstInt(1)) |> should.be_true

  moded_term.constant_is_real(terms.ConstReal(1.5)) |> should.be_true
  moded_term.constant_is_numeric(terms.ConstReal(1.5)) |> should.be_true
  moded_term.constant_is_integer(terms.ConstReal(1.5)) |> should.be_false

  moded_term.constant_is_string(terms.ConstString("hi")) |> should.be_true
  moded_term.constant_is_atom(terms.ConstString("hi")) |> should.be_false

  moded_term.constant_is_atom(terms.ConstAtom("foo")) |> should.be_true
  moded_term.constant_is_string(terms.ConstAtom("foo")) |> should.be_false
  moded_term.constant_is_nil(terms.ConstAtom("foo")) |> should.be_false

  moded_term.constant_is_nil(terms.ConstAtom("[]")) |> should.be_true
  moded_term.constant_is_atom(terms.ConstAtom("[]")) |> should.be_true
}

// ---------------------------------------------------------------------------
// Variable classification
// ---------------------------------------------------------------------------

pub fn implicit_mode_test() {
  // reader → consume (Input, ↓); writer → produce (Output, ↑).
  moded_term.implicit_mode(True) |> should.equal(Input)
  moded_term.implicit_mode(False) |> should.equal(Output)
}

pub fn mode_consistent_test() {
  // Reader at a consume position is consistent; at a produce position it is not.
  moded_term.variable_is_mode_consistent(True, Input) |> should.be_true
  moded_term.variable_is_mode_consistent(True, Output) |> should.be_false
  moded_term.variable_is_mode_consistent(False, Output) |> should.be_true
  moded_term.variable_is_mode_consistent(False, Input) |> should.be_false
}

// ---------------------------------------------------------------------------
// isConsumed / isProduced
// ---------------------------------------------------------------------------

pub fn is_consumed_test() {
  // ↓f(↓1, X?) — every mode is consume.
  let all_in =
    ModedCompound(Input, "f", 2, [
      ModedConstant(Input, terms.ConstInt(1)),
      ModedVariable("X", True, Input),
    ])
  moded_term.is_consumed(all_in) |> should.be_true
  moded_term.is_produced(all_in) |> should.be_false

  // One produce annotation breaks all-consumed.
  let mixed =
    ModedCompound(Input, "f", 1, [ModedConstant(Output, terms.ConstInt(1))])
  moded_term.is_consumed(mixed) |> should.be_false
}

pub fn is_produced_test() {
  // ↑f(↑1, X) — every mode is produce.
  let all_out =
    ModedCompound(Output, "f", 2, [
      ModedConstant(Output, terms.ConstInt(1)),
      ModedVariable("X", False, Output),
    ])
  moded_term.is_produced(all_out) |> should.be_true
  moded_term.is_consumed(all_out) |> should.be_false
}

// ---------------------------------------------------------------------------
// isIO — root must be ↓, transitions only ↓→↑
// ---------------------------------------------------------------------------

pub fn is_io_valid_test() {
  // ↓f(↑g(↑1)) — ↓→↑, ↑→↑, both valid.
  let t =
    ModedCompound(Input, "f", 1, [
      ModedCompound(Output, "g", 1, [ModedConstant(Output, terms.ConstInt(1))]),
    ])
  moded_term.is_io(t) |> should.be_true
}

pub fn is_io_bad_root_test() {
  // Root is produce → not an I/O term.
  let t = ModedCompound(Output, "f", 1, [ModedConstant(Output, terms.ConstInt(1))])
  moded_term.is_io(t) |> should.be_false
}

pub fn is_io_flip_back_test() {
  // ↓f(↑g(↓1)) — ↑→↓ transition is invalid.
  let t =
    ModedCompound(Input, "f", 1, [
      ModedCompound(Output, "g", 1, [ModedConstant(Input, terms.ConstInt(1))]),
    ])
  moded_term.is_io(t) |> should.be_false
}

// ---------------------------------------------------------------------------
// dual — flip modes, flip variables, involution
// ---------------------------------------------------------------------------

pub fn dual_flips_test() {
  let t =
    ModedCompound(Input, "f", 2, [
      ModedConstant(Input, terms.ConstInt(1)),
      ModedVariable("X", True, Input),
    ])
  moded_term.dual(t)
  |> should.equal(ModedCompound(Output, "f", 2, [
    ModedConstant(Output, terms.ConstInt(1)),
    ModedVariable("X", False, Output),
  ]))
}

pub fn dual_involution_test() {
  let t =
    ModedCompound(Input, "merge", 2, [
      moded_term.list_cons(Output, ModedVariable("X", True, Output), moded_term.nil(Output)),
      ModedVariable("Ys", True, Input),
    ])
  moded_term.dual(moded_term.dual(t)) |> should.equal(t)
}

// ---------------------------------------------------------------------------
// paths
// ---------------------------------------------------------------------------

pub fn paths_test() {
  // ↓f(↑1, X?) → two root-to-leaf paths.
  let t =
    ModedCompound(Input, "f", 2, [
      ModedConstant(Output, terms.ConstInt(1)),
      ModedVariable("X", True, Input),
    ])
  let ps = moded_term.paths(t)
  set.size(ps) |> should.equal(2)

  let root = PathStep("f/2", 0, Input, False, False)
  let const_path =
    ModedPath([root, PathStep("1", 1, Output, False, False)])
  let var_path =
    ModedPath([root, PathStep("X?", 2, Input, True, True)])
  set.contains(ps, const_path) |> should.be_true
  set.contains(ps, var_path) |> should.be_true
}

pub fn path_helpers_test() {
  let root = PathStep("f/2", 0, Input, False, False)
  let leaf = PathStep("X?", 2, Input, True, True)
  let path = ModedPath([root, leaf])
  moded_term.path_root(path) |> should.equal(root)
  moded_term.path_leaf(path) |> should.equal(leaf)
  moded_term.path_is_input(path) |> should.be_true
  moded_term.path_is_output(path) |> should.be_false
  moded_term.path_length(path) |> should.equal(2)
  moded_term.step_is_writer(leaf) |> should.be_false
  moded_term.step_is_writer(PathStep("X", 1, Output, True, False)) |> should.be_true
}

// ---------------------------------------------------------------------------
// toString rendering
// ---------------------------------------------------------------------------

pub fn to_string_test() {
  // Constant carries a mode prefix.
  moded_term.to_string(ModedConstant(Input, terms.ConstInt(42)))
  |> should.equal("↓42")
  // Variable has NO mode prefix (Dart ModedVariable.toString).
  moded_term.to_string(ModedVariable("X", True, Input)) |> should.equal("X?")
  moded_term.to_string(ModedVariable("X", False, Output)) |> should.equal("X")
  // Arity-0 compound.
  moded_term.to_string(ModedCompound(Output, "foo", 0, [])) |> should.equal("↑foo")
  // Compound with args.
  moded_term.to_string(ModedCompound(Output, "f", 2, [
    ModedConstant(Output, terms.ConstInt(1)),
    ModedVariable("X", False, Output),
  ]))
  |> should.equal("↑f(↑1, X)")
  // List cons.
  moded_term.to_string(moded_term.list_cons(
    Input,
    ModedVariable("H", True, Input),
    moded_term.nil(Input),
  ))
  |> should.equal("↓[H?|↓[]]")
}

pub fn step_to_string_test() {
  moded_term.step_to_string(PathStep("f/2", 0, Input, False, False))
  |> should.equal("(f/2, 0, ↓)")
}
