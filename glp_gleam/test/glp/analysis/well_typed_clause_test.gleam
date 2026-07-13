//// Well-typed-clause tests (feature 050, T018) — Dart well_typed_clause.dart is
//// the oracle: head + body-atom well-typing, undefined/undeclared procedures,
//// variable-pair duality across the clause, accepted-label extraction, and the
//// byte-identical `.message` strings of every ClauseError subclass. Outcomes are
//// derived from the leaf-consistency + moded-head variable-flip rules already
//// validated by program_dfa_test / moded_head_test.

import gleam/option.{None, Some}
import gleam/set
import gleeunit/should
import glp/analysis/type_ast.{
  type ProcDecl, type TypeEnvironment, ConstantAlt, CvAtom, ListConsAlt,
  ListNilAlt, PrimitiveModeAlt, Pos, ProcDecl, StructAlt, TypeDef, TypeRef,
}
import glp/analysis/type_checker/mode.{Input, Output}
import glp/analysis/type_checker/moded_term.{ModedPath}
import glp/analysis/type_checker/program_dfa
import glp/analysis/type_checker/well_typed_clause.{
  ArityMismatchClauseError, BodyAtomError, ClauseDualityError, HeadError,
  UndefinedProcedureError,
}
import glp/analysis/type_checker/well_typed_term.{
  InconsistentPathError, VariableTypeInfo,
}
import glp/parser/ast
import glp/runtime/terms

fn pos() -> type_ast.Pos {
  Pos(0, 0)
}

fn tref(name: String, is_input: Bool) -> type_ast.TypeExpr {
  TypeRef(name, is_input, [], pos())
}

fn decl(name: String, arg_types: List(type_ast.TypeExpr)) -> ProcDecl {
  ProcDecl(
    name:,
    arg_types:,
    type_params: [],
    pos: pos(),
    is_builtin: False,
    exported: False,
    imported: False,
    module_path: None,
  )
}

/// Oracle environment:
///   Nat ::= zero ; s(Nat).
///   NatList ::= [] ; [Nat | NatList].
///   procedure count(NatList?, Integer).
///   procedure dup(Nat?, Nat).
///   procedure is_zero(Nat).
fn sample_env() -> TypeEnvironment {
  let nat =
    TypeDef(
      "Nat",
      [],
      [ConstantAlt(CvAtom("zero"), pos()), StructAlt("s", [tref("Nat", False)], pos())],
      pos(),
    )
  let nat_list =
    TypeDef(
      "NatList",
      [],
      [ListNilAlt(pos()), ListConsAlt(tref("Nat", False), tref("NatList", False), pos())],
      pos(),
    )
  type_ast.empty_environment()
  |> type_ast.add_type(nat)
  |> type_ast.add_type(nat_list)
  |> type_ast.add_procedure(decl("count", [tref("NatList", True), tref("Integer", False)]))
  |> type_ast.add_procedure(decl("dup", [tref("Nat", True), tref("Nat", False)]))
  |> type_ast.add_procedure(decl("is_zero", [tref("Nat", False)]))
}

fn dfa() -> program_dfa.ProgramDfa {
  program_dfa.build_program_dfa(sample_env())
}

fn const_int(i: Int) -> ast.Term {
  ast.ConstTerm(terms.ConstInt(i), pos())
}

fn var(name: String, is_reader: Bool) -> ast.Term {
  ast.VarTerm(name, is_reader, pos())
}

fn clause(functor: String, args: List(ast.Term)) -> ast.Clause {
  ast.Clause(ast.Atom(functor, args, pos()), None, None, pos())
}

fn check(functor: String, args: List(ast.Term)) -> Bool {
  case well_typed_clause.check_clause_from_ast(clause(functor, args), dfa(), sample_env()) {
    Ok(result) -> result.is_well_typed
    Error(_) -> panic as "unexpected UndeclaredProcedureError"
  }
}

// ---------------------------------------------------------------------------
// Well-typed / ill-typed clauses
// ---------------------------------------------------------------------------

pub fn unit_clause_constants_well_typed_test() {
  // count([], 0): [] at NatList?, 0 at Integer — both consistent.
  check("count", [ast.ListTerm(None, None, pos()), const_int(0)])
  |> should.be_true
}

pub fn head_writer_at_input_arg_well_typed_test() {
  // count(X, 0): X (writer) flips to reader at the NatList? (↓) position.
  check("count", [var("X", False), const_int(0)]) |> should.be_true
}

pub fn dual_pair_across_head_well_typed_test() {
  // dup(X, X?) against dup(Nat?, Nat): X writer→reader at Nat?, X? reader→writer
  // at Nat — a dual pair.
  check("dup", [var("X", False), var("X", True)]) |> should.be_true
}

pub fn head_reader_at_input_arg_ill_typed_test() {
  // count(X?, 0): X? (reader) flips to writer at a consume (↓) position — a
  // writer requires produce (↑), so it is inconsistent.
  check("count", [var("X", True), const_int(0)]) |> should.be_false
}

pub fn constant_wrong_type_ill_typed_test() {
  // count(0, 0): 0 is not a NatList alternative.
  let result =
    well_typed_clause.check_clause_from_ast(
      clause("count", [const_int(0), const_int(0)]),
      dfa(),
      sample_env(),
    )
  case result {
    Ok(r) -> {
      r.is_well_typed |> should.be_false
      case r.errors {
        [HeadError("count", _)] -> Nil
        _ -> should.fail()
      }
    }
    Error(_) -> should.fail()
  }
}

// ---------------------------------------------------------------------------
// Undefined / undeclared procedures
// ---------------------------------------------------------------------------

pub fn undeclared_procedure_from_ast_test() {
  well_typed_clause.check_clause_from_ast(
    clause("nope", [var("X", False)]),
    dfa(),
    sample_env(),
  )
  |> should.equal(Error(well_typed_clause.UndeclaredProcedure("nope", 1)))
}

pub fn undefined_procedure_check_clause_test() {
  let tc =
    well_typed_clause.TypedClause(
      head: ast.Goal("nope", [var("X", False)], pos()),
      body_atoms: [],
      guard_atoms: [],
    )
  let result = well_typed_clause.check_clause(tc, dfa(), sample_env())
  result.is_well_typed |> should.be_false
  result.errors |> should.equal([UndefinedProcedureError("nope", 1)])
}

// ---------------------------------------------------------------------------
// Accepted labels / full type name
// ---------------------------------------------------------------------------

pub fn labels_from_term_test() {
  well_typed_clause.get_labels_from_term(var("X", False)) |> should.equal(None)
  well_typed_clause.get_labels_from_term(ast.UnderscoreTerm(False, pos()))
  |> should.equal(None)
  well_typed_clause.get_labels_from_term(ast.ConstTerm(terms.ConstAtom("foo"), pos()))
  |> should.equal(Some(set.from_list(["foo"])))
  // Strings render bare (Dart Object.toString), like atoms.
  well_typed_clause.get_labels_from_term(ast.ConstTerm(terms.ConstString("bar"), pos()))
  |> should.equal(Some(set.from_list(["bar"])))
  well_typed_clause.get_labels_from_term(ast.ListTerm(None, None, pos()))
  |> should.equal(Some(set.from_list(["[]"])))
  well_typed_clause.get_labels_from_term(ast.ListTerm(
    Some(var("H", False)),
    Some(var("T", False)),
    pos(),
  ))
  |> should.equal(Some(set.from_list(["[|]"])))
  well_typed_clause.get_labels_from_term(ast.StructTerm("s", [var("X", False)], pos()))
  |> should.equal(Some(set.from_list(["s/1"])))
}

pub fn accepted_labels_test() {
  let c = clause("count", [ast.ListTerm(None, None, pos()), const_int(0)])
  well_typed_clause.get_accepted_labels(c, 1, sample_env())
  |> should.equal(Some(set.from_list(["[]"])))
  well_typed_clause.get_accepted_labels(c, 2, sample_env())
  |> should.equal(Some(set.from_list(["0"])))
  // Out of bounds → accepts nothing.
  well_typed_clause.get_accepted_labels(c, 3, sample_env())
  |> should.equal(Some(set.new()))
}

pub fn full_type_name_test() {
  well_typed_clause.get_full_type_name(tref("Nat", False)) |> should.equal("Nat")
  well_typed_clause.get_full_type_name(tref("Nat", True)) |> should.equal("Nat?")
  well_typed_clause.get_full_type_name(PrimitiveModeAlt(False, pos()))
  |> should.equal("_")
  well_typed_clause.get_full_type_name(PrimitiveModeAlt(True, pos()))
  |> should.equal("_?")
}

// ---------------------------------------------------------------------------
// Error message byte-identity
// ---------------------------------------------------------------------------

pub fn head_error_message_test() {
  let term_err =
    InconsistentPathError(
      ModedPath([moded_term.path_step("x", 0, Output)]),
      "boom",
    )
  well_typed_clause.error_message(HeadError("count", [term_err]))
  |> should.equal("Head of count is not well-typed:\n  Inconsistent path: boom\n  Path: (x, 0, output)")
}

pub fn body_atom_error_message_test() {
  let term_err =
    InconsistentPathError(ModedPath([moded_term.path_step("x", 0, Output)]), "boom")
  well_typed_clause.error_message(BodyAtomError("q", 1, [term_err]))
  |> should.equal("Body atom 1 (q) is not well-typed:\n  Inconsistent path: boom\n  Path: (x, 0, output)")
}

pub fn clause_duality_error_message_test() {
  let d = dfa()
  let w = VariableTypeInfo(program_dfa.get_dfa_state(d, "Nat"), Output, False)
  let r = VariableTypeInfo(program_dfa.get_dfa_state(d, "Nat"), Input, True)
  well_typed_clause.error_message(ClauseDualityError(
    "X",
    Some(w),
    Some(r),
    "head",
    "body atom 0",
    Some("Variables across head/body must have same type: reasons"),
  ))
  |> should.equal(
    "Variable pair (X, X?) not dual across clause: Variables across head/body must have same type: reasons: writer at head=(Nat, ↑), reader at body atom 0=(Nat, ↓)",
  )
}

pub fn undefined_procedure_message_test() {
  well_typed_clause.error_message(UndefinedProcedureError("foo", 2))
  |> should.equal("Undefined procedure: foo/2")
}

pub fn arity_mismatch_message_test() {
  well_typed_clause.error_message(ArityMismatchClauseError("foo", 2, 1))
  |> should.equal("Arity mismatch for foo: expected 2, got 1")
}
