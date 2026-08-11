//// Well-typed-term tests (feature 050, T018) — Dart well_typed_term.dart is the
//// oracle: path consistency via automaton traversal, variable-type recording,
//// duality of variable pairs, automaton switching at user-defined type
//// boundaries, wildcard/Any subterm acceptance, and the byte-identical
//// `.message` strings of every WellTypedError subclass.

import gleam/option.{None, Some}
import gleeunit/should
import glp/analysis/type_ast.{
  type TypeEnvironment, ConstantAlt, CvAtom, PrimitiveModeAlt, Pos, StructAlt,
  TypeDef, TypeRef,
}
import glp/analysis/type_checker/mode.{Input, Output}
import glp/analysis/type_checker/moded_term.{
  ModedCompound, ModedConstant, ModedPath, ModedVariable, PathStep,
}
import glp/analysis/type_checker/program_dfa
import glp/analysis/type_checker/well_typed_term.{
  InconsistentPathError, InconsistentVariableError, NonDualError,
  VariableTypeInfo,
}
import glp/runtime/terms

fn pos() -> type_ast.Pos {
  Pos(0, 0)
}

fn tref(name: String, is_input: Bool) -> type_ast.TypeExpr {
  TypeRef(name, is_input, [], pos())
}

/// Oracle environment:
///   Nat ::= zero ; s(Nat).
///   Dup ::= d(Nat, Nat?).
///   Box ::= box(_).
fn sample_env() -> TypeEnvironment {
  let nat =
    TypeDef(
      "Nat",
      [],
      [
        ConstantAlt(CvAtom("zero"), pos()),
        StructAlt("s", [tref("Nat", False)], pos()),
      ],
      pos(),
    )
  let dup =
    TypeDef(
      "Dup",
      [],
      [StructAlt("d", [tref("Nat", False), tref("Nat", True)], pos())],
      pos(),
    )
  let box =
    TypeDef("Box", [], [StructAlt("box", [PrimitiveModeAlt(False, pos())], pos())], pos())

  type_ast.empty_environment()
  |> type_ast.add_type(nat)
  |> type_ast.add_type(dup)
  |> type_ast.add_type(box)
}

fn dfa() -> program_dfa.ProgramDfa {
  let assert Ok(dfa) = program_dfa.build_program_dfa(sample_env())
  dfa
}

fn state(name: String) -> program_dfa.DfaState {
  program_dfa.get_dfa_state(dfa(), name)
}

// ---------------------------------------------------------------------------
// Rendering — .message / toString byte-identity
// ---------------------------------------------------------------------------

pub fn variable_type_info_to_string_test() {
  well_typed_term.variable_type_info_to_string(VariableTypeInfo(
    state("Nat"),
    Output,
    False,
  ))
  |> should.equal("(Nat, ↑)")

  well_typed_term.variable_type_info_to_string(VariableTypeInfo(
    state("Nat?"),
    Input,
    True,
  ))
  |> should.equal("(Nat?, ↓)")
}

pub fn inconsistent_path_message_test() {
  let path = ModedPath([PathStep("X?", 0, Output, True, True)])
  well_typed_term.error_message(InconsistentPathError(path, "some reason"))
  |> should.equal("Inconsistent path: some reason\n  Path: (X?, 0, output)")
}

pub fn inconsistent_variable_message_test() {
  let first = VariableTypeInfo(state("Nat"), Output, False)
  let second = VariableTypeInfo(state("Dup"), Output, False)
  well_typed_term.error_message(InconsistentVariableError("X", first, second))
  |> should.equal("Variable X has inconsistent types: (Nat, ↑) vs (Dup, ↑)")
}

pub fn non_dual_message_with_reason_test() {
  let w = VariableTypeInfo(state("Nat"), Output, False)
  let r = VariableTypeInfo(state("Nat"), Input, True)
  well_typed_term.error_message(NonDualError(
    "X",
    Some(w),
    Some(r),
    Some("One must be dual, other not: Nat vs Nat"),
  ))
  |> should.equal(
    "Variable pair (X, X?) not dual: One must be dual, other not: Nat vs Nat: writer=(Nat, ↑), reader=(Nat, ↓)",
  )
}

pub fn non_dual_message_without_reason_test() {
  let w = VariableTypeInfo(state("Nat"), Output, False)
  let r = VariableTypeInfo(state("Nat?"), Input, True)
  well_typed_term.error_message(NonDualError("X", Some(w), Some(r), None))
  |> should.equal(
    "Variable pair (X, X?) not dual: writer=(Nat, ↑), reader=(Nat?, ↓)",
  )
}

pub fn non_dual_message_null_infos_test() {
  well_typed_term.error_message(NonDualError("X", None, None, None))
  |> should.equal("Variable pair (X, X?) not dual: writer=null, reader=null")
}

// ---------------------------------------------------------------------------
// check_moded_term — positive cases
// ---------------------------------------------------------------------------

pub fn integer_literal_well_typed_test() {
  let d = dfa()
  let term = ModedConstant(Output, terms.ConstInt(5))
  let result =
    well_typed_term.check_moded_term(term, program_dfa.get_automaton(d, "Integer"), d)
  result.is_well_typed |> should.be_true
  result.errors |> should.equal([])
}

pub fn variable_writer_records_type_test() {
  let d = dfa()
  let term = ModedVariable("X", False, Output)
  let result =
    well_typed_term.check_moded_term(term, program_dfa.get_automaton(d, "Nat"), d)
  result.is_well_typed |> should.be_true
  result.variable_types
  |> should.equal([#("X", VariableTypeInfo(program_dfa.get_dfa_state(d, "Nat"), Output, False))])
}

pub fn dual_pair_well_typed_test() {
  let d = dfa()
  // d(X, X?) checked against Dup ::= d(Nat, Nat?): X writer at Nat (↑),
  // X? reader at Nat? (↓) — a dual pair, well-typed.
  let term =
    ModedCompound(Output, "d", 2, [
      ModedVariable("X", False, Output),
      ModedVariable("X", True, Input),
    ])
  let result =
    well_typed_term.check_moded_term(term, program_dfa.get_automaton(d, "Dup"), d)
  result.is_well_typed |> should.be_true
  result.errors |> should.equal([])
}

pub fn structure_over_user_type_well_typed_test() {
  let d = dfa()
  // s(zero) against Nat — switches into Nat's automaton, leaf zero matches.
  let term =
    ModedCompound(Output, "s", 1, [ModedConstant(Output, terms.ConstAtom("zero"))])
  let result =
    well_typed_term.check_moded_term(term, program_dfa.get_automaton(d, "Nat"), d)
  result.is_well_typed |> should.be_true
  result.errors |> should.equal([])
}

pub fn wildcard_accepts_deeper_subterm_test() {
  let d = dfa()
  // box(s(zero)) against Box ::= box(_): the wildcard accepts the whole s(zero)
  // subterm even though the type path is shorter (Case 3, Definition 4.5 v0.7).
  let term =
    ModedCompound(Output, "box", 1, [
      ModedCompound(Output, "s", 1, [ModedConstant(Output, terms.ConstAtom("zero"))]),
    ])
  let result =
    well_typed_term.check_moded_term(term, program_dfa.get_automaton(d, "Box"), d)
  result.is_well_typed |> should.be_true
  result.errors |> should.equal([])
}

// ---------------------------------------------------------------------------
// check_moded_term — negative cases
// ---------------------------------------------------------------------------

pub fn reader_at_produce_position_fails_test() {
  let d = dfa()
  // A reader X? with structural produce mode is leaf-inconsistent at Nat.
  let term = ModedVariable("X", True, Output)
  let result =
    well_typed_term.check_moded_term(term, program_dfa.get_automaton(d, "Nat"), d)
  result.is_well_typed |> should.be_false
  result.errors
  |> should.equal([
    InconsistentPathError(
      ModedPath([PathStep("X?", 0, Output, True, True)]),
      "Variable mode mismatch: reader requires ↓ (consume), got ↑ (produce)",
    ),
  ])
}

pub fn no_transition_fails_test() {
  let d = dfa()
  // foo(zero) against Nat — no `foo` transition from the Nat state.
  let term =
    ModedCompound(Output, "foo", 1, [ModedConstant(Output, terms.ConstAtom("zero"))])
  let result =
    well_typed_term.check_moded_term(term, program_dfa.get_automaton(d, "Nat"), d)
  result.is_well_typed |> should.be_false
  case result.errors {
    [InconsistentPathError(_, reason)] ->
      reason |> should.equal("No transition for foo(1,1):↑ from state Nat")
    _ -> should.fail()
  }
}
