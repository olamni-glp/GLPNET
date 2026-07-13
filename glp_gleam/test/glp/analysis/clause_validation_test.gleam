//// Clause-validation tests (feature 050, T018) — Dart clause_validation.dart is
//// the oracle: anonymous writers allowed, anonymous readers (`_?`, `_X?`)
//// rejected everywhere, recursively into structures and lists. Error strings are
//// byte-identical to the Dart checker.

import gleam/option.{None, Some}
import gleeunit/should
import glp/analysis/type_ast.{Pos}
import glp/analysis/type_checker/clause_validation.{ValidationError}
import glp/parser/ast
import glp/runtime/terms

fn pos() -> type_ast.Pos {
  Pos(0, 0)
}

pub fn anonymous_reader_rejected_test() {
  clause_validation.validate_clause_head(ast.UnderscoreTerm(True, pos()))
  |> should.equal(Error(ValidationError(
    "_? (anonymous reader) is not permitted in program clauses",
    0,
    0,
  )))
}

pub fn anonymous_writer_allowed_test() {
  clause_validation.validate_clause_head(ast.UnderscoreTerm(False, pos()))
  |> should.equal(Ok(Nil))
}

pub fn named_anonymous_reader_rejected_test() {
  clause_validation.validate_clause_body(ast.VarTerm("_X", True, pos()))
  |> should.equal(Error(ValidationError(
    "_X? (anonymous reader) is not permitted in program clauses",
    0,
    0,
  )))
}

pub fn ordinary_reader_allowed_test() {
  // A reader whose name does not start with `_` is fine.
  clause_validation.validate_clause_head(ast.VarTerm("X", True, pos()))
  |> should.equal(Ok(Nil))
}

pub fn named_underscore_writer_allowed_test() {
  clause_validation.validate_clause_head(ast.VarTerm("_X", False, pos()))
  |> should.equal(Ok(Nil))
}

pub fn constant_allowed_test() {
  clause_validation.validate_guard(ast.ConstTerm(terms.ConstInt(0), pos()))
  |> should.equal(Ok(Nil))
}

pub fn nested_struct_reader_rejected_test() {
  clause_validation.validate_clause_head(ast.StructTerm(
    "f",
    [ast.VarTerm("X", False, pos()), ast.UnderscoreTerm(True, pos())],
    pos(),
  ))
  |> should.equal(Error(ValidationError(
    "_? (anonymous reader) is not permitted in program clauses",
    0,
    0,
  )))
}

pub fn nested_list_reader_rejected_test() {
  clause_validation.validate_clause_head(ast.ListTerm(
    Some(ast.VarTerm("_Y", True, pos())),
    None,
    pos(),
  ))
  |> should.equal(Error(ValidationError(
    "_Y? (anonymous reader) is not permitted in program clauses",
    0,
    0,
  )))
}

pub fn well_formed_list_allowed_test() {
  clause_validation.validate_clause_head(ast.ListTerm(
    Some(ast.VarTerm("H", False, pos())),
    Some(ast.VarTerm("T", True, pos())),
    pos(),
  ))
  |> should.equal(Ok(Nil))
}
