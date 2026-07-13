//// glp/analysis/type_checker/clause_validation — validates term AST nodes in
//// clause contexts (feature 050, T018).
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/clause_validation.dart
//// (spec: docs/type system/clause-validation.md). Anonymous writers (`_`, `_X`)
//// are allowed; anonymous readers (`_?`, `_X?`) are never permitted in program
//// clauses (typed-glp-manual §9).
////
//// Port notes:
////   * Dart throws `CompileError(message, line, column, phase: 'validation')`,
////     caught by the type checker and turned into a `TypeError`. The `'validation'`
////     phase maps to a null error category in Dart (only message/line/column are
////     used downstream), so the port returns a minimal `ValidationError` as a
////     `Result` error.

import gleam/list
import gleam/option.{None, Some}
import gleam/result
import gleam/string
import glp/parser/ast.{
  type Term, ConstTerm, ListTerm, StructTerm, UnderscoreTerm, VarTerm,
}

/// A clause-validation failure (Dart `CompileError` with phase `'validation'`).
pub type ValidationError {
  ValidationError(message: String, line: Int, column: Int)
}

/// Validate a term in clause head context (Dart `validateClauseHead`): rejects
/// `_?`, allows `_`.
pub fn validate_clause_head(term: Term) -> Result(Nil, ValidationError) {
  check_no_anonymous_reader(term)
}

/// Validate a term in clause body context (Dart `validateClauseBody`).
pub fn validate_clause_body(term: Term) -> Result(Nil, ValidationError) {
  check_no_anonymous_reader(term)
}

/// Validate a term in guard context (Dart `validateGuard`).
pub fn validate_guard(term: Term) -> Result(Nil, ValidationError) {
  check_no_anonymous_reader(term)
}

/// Recursively reject anonymous readers — bare `_?` and named `_X?` (Dart
/// `_checkNoAnonymousReader`).
fn check_no_anonymous_reader(term: Term) -> Result(Nil, ValidationError) {
  case term {
    UnderscoreTerm(True, pos) ->
      Error(ValidationError(
        "_? (anonymous reader) is not permitted in program clauses",
        pos.line,
        pos.column,
      ))
    UnderscoreTerm(False, _) -> Ok(Nil)
    // Named anonymous readers (_X?) are also rejected.
    VarTerm(name, True, pos) ->
      case string.starts_with(name, "_") {
        True ->
          Error(ValidationError(
            name <> "? (anonymous reader) is not permitted in program clauses",
            pos.line,
            pos.column,
          ))
        False -> Ok(Nil)
      }
    VarTerm(_, False, _) -> Ok(Nil)
    ConstTerm(_, _) -> Ok(Nil)
    StructTerm(_, args, _) -> list.try_each(args, check_no_anonymous_reader)
    ListTerm(head, tail, _) -> {
      use _ <- result.try(check_option(head))
      check_option(tail)
    }
  }
}

fn check_option(term: option.Option(Term)) -> Result(Nil, ValidationError) {
  case term {
    Some(t) -> check_no_anonymous_reader(t)
    None -> Ok(Nil)
  }
}
