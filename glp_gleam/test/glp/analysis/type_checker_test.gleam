//// Type-checker tests (feature 050, T018; closes T018) — Dart type_checker.dart
//// is the oracle: end-to-end `checkModule` wiring (build env → DFA → check),
//// covariance (well-typed clauses), contravariance (input coverage),
//// anonymous-reader validation, declared/undeclared warnings, and byte-identical
//// error/warning renderings.

import gleam/list
import gleam/option.{None, Some}
import gleam/string
import gleeunit/should
import glp/analysis/type_ast.{
  ConstantAlt, CvAtom, ListConsAlt, ListNilAlt, Pos, ProcDecl, StructAlt, TypeDef,
  TypeRef,
}
import glp/analysis/type_checker/type_checker.{
  CoverageError, TypeError, TypeWarning,
}
import glp/parser/ast
import glp/runtime/terms

fn pos() -> type_ast.Pos {
  Pos(0, 0)
}

fn tref(name: String, is_input: Bool) -> type_ast.TypeExpr {
  TypeRef(name, is_input, [], pos())
}

fn nat_def() -> type_ast.TypeDef {
  TypeDef(
    "Nat",
    [],
    [ConstantAlt(CvAtom("zero"), pos()), StructAlt("s", [tref("Nat", False)], pos())],
    pos(),
  )
}

fn natlist_def() -> type_ast.TypeDef {
  TypeDef(
    "NatList",
    [],
    [ListNilAlt(pos()), ListConsAlt(tref("Nat", False), tref("NatList", False), pos())],
    pos(),
  )
}

fn pdecl(name: String, args: List(type_ast.TypeExpr)) -> type_ast.ProcDecl {
  ProcDecl(name, args, [], pos(), False, False, False, None)
}

fn atom_const(name: String) -> ast.Term {
  ast.ConstTerm(terms.ConstAtom(name), pos())
}

fn unit_clause(functor: String, args: List(ast.Term)) -> ast.Clause {
  ast.Clause(ast.Atom(functor, args, pos()), None, None, pos())
}

fn proc(functor: String, arity: Int, clauses: List(ast.Clause)) -> ast.Procedure {
  ast.Procedure(functor, arity, clauses, pos())
}

fn module_of(
  type_defs: List(type_ast.TypeDef),
  proc_decls: List(type_ast.ProcDecl),
  procedures: List(ast.Procedure),
) -> ast.SourceModule {
  ast.SourceModule(None, type_defs, proc_decls, [], procedures, ast.User, pos())
}

fn run(module: ast.SourceModule) -> type_checker.TypeCheckResult {
  case type_checker.check_module(module, None, None) {
    Ok(res) -> res
    Error(_) -> panic as "unexpected TypeEnvError"
  }
}

// ---------------------------------------------------------------------------
// Happy path
// ---------------------------------------------------------------------------

pub fn well_typed_program_test() {
  // procedure gen(Nat).  gen(zero).  — output arg (no coverage), constant clause.
  let module =
    module_of(
      [nat_def()],
      [pdecl("gen", [tref("Nat", False)])],
      [proc("gen", 1, [unit_clause("gen", [atom_const("zero")])])],
    )
  let res = run(module)
  type_checker.is_well_typed(res) |> should.be_true
  res.errors |> should.equal([])
  res.warnings |> should.equal([])
}

// ---------------------------------------------------------------------------
// Covariance — ill-typed clause
// ---------------------------------------------------------------------------

pub fn covariance_error_test() {
  // procedure count(NatList?, Integer).  count(0, 0).  — 0 is not a NatList.
  let module =
    module_of(
      [nat_def(), natlist_def()],
      [pdecl("count", [tref("NatList", True), tref("Integer", False)])],
      [
        proc("count", 2, [
          unit_clause("count", [
            ast.ConstTerm(terms.ConstInt(0), pos()),
            ast.ConstTerm(terms.ConstInt(0), pos()),
          ]),
        ]),
      ],
    )
  let res = run(module)
  type_checker.is_well_typed(res) |> should.be_false
  { res.errors != [] } |> should.be_true
}

// ---------------------------------------------------------------------------
// Contravariance — uncovered input alternative
// ---------------------------------------------------------------------------

pub fn contravariance_coverage_error_test() {
  // procedure sink(NatList?).  sink([]).  — the [|] alternative is uncovered.
  let module =
    module_of(
      [nat_def(), natlist_def()],
      [pdecl("sink", [tref("NatList", True)])],
      [proc("sink", 1, [unit_clause("sink", [ast.ListTerm(None, None, pos())])])],
    )
  let res = run(module)
  type_checker.is_well_typed(res) |> should.be_false
  list.any(res.errors, fn(e) { string.contains(e.message, "uncovered alternative") })
  |> should.be_true
}

// ---------------------------------------------------------------------------
// Phase 0 validation — anonymous reader
// ---------------------------------------------------------------------------

pub fn validation_error_short_circuits_test() {
  // p(_?).  — anonymous reader in a head; validation fails and returns early.
  let module =
    module_of(
      [],
      [],
      [proc("p", 1, [unit_clause("p", [ast.UnderscoreTerm(True, pos())])])],
    )
  let res = run(module)
  res.errors
  |> should.equal([
    TypeError(
      "_? (anonymous reader) is not permitted in program clauses",
      0,
      0,
      Some("p(1 args)."),
    ),
  ])
}

// ---------------------------------------------------------------------------
// Warnings
// ---------------------------------------------------------------------------

pub fn declared_but_not_defined_warning_test() {
  let module = module_of([nat_def()], [pdecl("gen", [tref("Nat", False)])], [])
  let res = run(module)
  res.errors |> should.equal([])
  res.warnings
  |> should.equal([
    TypeWarning("Procedure gen/1 declared but not defined", 0, 0),
  ])
}

pub fn no_type_declaration_warning_test() {
  // A clause with no matching procedure declaration.
  let module =
    module_of([], [], [proc("bar", 1, [unit_clause("bar", [atom_const("zero")])])])
  let res = run(module)
  res.errors |> should.equal([])
  res.warnings
  |> should.equal([TypeWarning("Procedure bar/1 has no type declaration", 0, 0)])
}

// ---------------------------------------------------------------------------
// Renderings
// ---------------------------------------------------------------------------

pub fn type_error_to_string_test() {
  type_checker.type_error_to_string(TypeError("msg", 2, 3, Some("clause")))
  |> should.equal("msg at line 2, column 3\n    in: clause")
  type_checker.type_error_to_string(TypeError("msg", 2, 3, None))
  |> should.equal("msg at line 2, column 3")
}

pub fn type_warning_to_string_test() {
  type_checker.type_warning_to_string(TypeWarning("w", 1, 1))
  |> should.equal("w at line 1, column 1")
}

pub fn coverage_error_to_string_test() {
  type_checker.coverage_error_to_string(CoverageError("p", 1, "s/1", "Nat → s/1"))
  |> should.equal("p argument 1: uncovered alternative \"s/1\" at path: Nat → s/1")
}
