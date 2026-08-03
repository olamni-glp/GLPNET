//// Subtyping tests (feature 050, T018) — Dart subtyping.dart is the oracle:
//// reflexivity, `_`/`_FINAL_` as top, the Integer/Real <: Number lattice,
//// structural (transition-subset) subtyping, and contravariant reversal at a
//// dual (input-mode) target position.

import gleeunit/should
import glp/analysis/type_ast.{
  type TypeEnvironment, ConstantAlt, CvAtom, Pos, StructAlt, TypeDef, TypeRef,
}
import glp/analysis/type_checker/program_dfa
import glp/analysis/type_checker/subtyping

fn pos() -> type_ast.Pos {
  Pos(0, 0)
}

/// Subtyping oracle environment:
///   Small ::= a.
///   Big   ::= a ; b.
///   WrapA ::= w(Small?).
///   WrapB ::= w(Big?).
fn sample_env() -> TypeEnvironment {
  let small = TypeDef("Small", [], [ConstantAlt(CvAtom("a"), pos())], pos())
  let big =
    TypeDef(
      "Big",
      [],
      [ConstantAlt(CvAtom("a"), pos()), ConstantAlt(CvAtom("b"), pos())],
      pos(),
    )
  let wrap_a =
    TypeDef("WrapA", [], [StructAlt("w", [TypeRef("Small", True, [], pos())], pos())], pos())
  let wrap_b =
    TypeDef("WrapB", [], [StructAlt("w", [TypeRef("Big", True, [], pos())], pos())], pos())

  type_ast.empty_environment()
  |> type_ast.add_type(small)
  |> type_ast.add_type(big)
  |> type_ast.add_type(wrap_a)
  |> type_ast.add_type(wrap_b)
}

fn dfa() -> program_dfa.ProgramDfa {
  let assert Ok(dfa) = program_dfa.build_program_dfa(sample_env())
  dfa
}

fn state(name: String) -> program_dfa.DfaState {
  program_dfa.get_dfa_state(dfa(), name)
}

fn sub(a: String, b: String) -> Bool {
  subtyping.is_subtype(state(a), state(b), dfa())
}

// ---------------------------------------------------------------------------

pub fn reflexivity_test() {
  sub("Small", "Small") |> should.be_true
  sub("Big", "Big") |> should.be_true
}

pub fn wildcard_is_top_test() {
  sub("Small", "_") |> should.be_true
  sub("_", "Small") |> should.be_false
}

pub fn anonymous_final_is_top_test() {
  sub("Small", "_FINAL_") |> should.be_true
}

pub fn primitive_lattice_test() {
  sub("Integer", "Number") |> should.be_true
  sub("Real", "Number") |> should.be_true
  sub("Number", "Integer") |> should.be_false
  sub("Integer", "String") |> should.be_false
  sub("Integer", "Integer") |> should.be_true
}

pub fn structural_subset_test() {
  // Small's single alternative is a subset of Big's → Small <: Big.
  sub("Small", "Big") |> should.be_true
  // Big has `b`, which Small lacks → not a subtype.
  sub("Big", "Small") |> should.be_false
}

pub fn contravariance_at_dual_target_test() {
  // w(Small?)/w(Big?): the field is input-moded (dual target), so subtyping
  // reverses. WrapB <: WrapA because Small <: Big; WrapA </: WrapB.
  sub("WrapB", "WrapA") |> should.be_true
  sub("WrapA", "WrapB") |> should.be_false
}
