//// glp/analysis/type_checker/type_checker — the main GLP well-typed-program
//// checker (feature 050, T018; closes the T018 type-checker port).
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/type_checker.dart
//// (spec: docs/modules/well-typed-program.md v0.7; paper Definition 4.10). A typed
//// program is well-typed iff (1) covariance: every clause is well-typed by the
//// declared types, and (2) contravariance: every input path in every procedure
//// type is covered by some clause head.
////
//// Port notes:
////   * The Dart `TypeChecker` class (fields `typeEnv`, `dfa`) becomes module
////     functions taking `type_env` + `dfa` explicitly; `check_module` builds them.
////   * `check_module`/`check_source` return a `Result` because
////     `build_type_environment` surfaces its errors as a `Result`
////     (`type_environment_builder.TypeEnvError`).
////   * `checkClauseFromAst`'s Dart `UndeclaredProcedureError` throw is a `Result`
////     error here; Dart's generic `catch (e)` for other failures has no Gleam
////     analogue (a well-typed-clause invariant violation panics, as in Dart it
////     would surface uncaught).
////   * ORDERING CAVEAT: Dart iterates `typeEnv.procedures` (a LinkedHashMap) and
////     automaton transitions (a LinkedHashMap) in insertion order, and emits
////     errors/warnings in that order. The Gleam `TypeEnvironment.procedures` and
////     `Automaton.transitions` are `Dict`s (unordered), so the RELATIVE ORDER of
////     errors ACROSS procedures / coverage alternatives may differ from Dart for
////     multi-error programs. Individual messages, `is_well_typed`, and per-clause
////     order are faithful.

import gleam/dict
import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/result
import gleam/set
import gleam/string
import glp/analysis/type_ast.{
  type ProcDecl, type TypeEnvironment, PrimitiveModeAlt, TypeRef,
}
import glp/analysis/type_checker/clause_validation
import glp/analysis/type_checker/param_expansion
import glp/analysis/type_checker/program_dfa.{type Automaton, type DfaState, type ProgramDfa}
import glp/analysis/type_checker/type_environment_builder as teb
import glp/analysis/type_checker/well_typed_clause as wtc
import glp/parser/ast
import glp/parser/lexer
import glp/parser/parser

// =============================================================================
// Result types
// =============================================================================

/// A type error (Dart `TypeError`).
pub type TypeError {
  TypeError(message: String, line: Int, column: Int, clause_text: Option(String))
}

/// A type warning (Dart `TypeWarning`).
pub type TypeWarning {
  TypeWarning(message: String, line: Int, column: Int)
}

/// An uncovered input alternative (Dart `CoverageError`).
pub type CoverageError {
  CoverageError(
    procedure: String,
    arg_index: Int,
    uncovered_label: String,
    path: String,
  )
}

/// The result of type-checking a program (Dart `TypeCheckResult`).
pub type TypeCheckResult {
  TypeCheckResult(errors: List(TypeError), warnings: List(TypeWarning))
}

pub fn is_well_typed(res: TypeCheckResult) -> Bool {
  list.is_empty(res.errors)
}

/// Dart `TypeError.toString()`.
pub fn type_error_to_string(error: TypeError) -> String {
  let loc = "line " <> int.to_string(error.line) <> ", column " <> int.to_string(error.column)
  let in_clause = case error.clause_text {
    Some(t) -> "\n    in: " <> t
    None -> ""
  }
  error.message <> " at " <> loc <> in_clause
}

/// Dart `TypeWarning.toString()`.
pub fn type_warning_to_string(warning: TypeWarning) -> String {
  warning.message
  <> " at line "
  <> int.to_string(warning.line)
  <> ", column "
  <> int.to_string(warning.column)
}

/// Dart `CoverageError.toString()`.
pub fn coverage_error_to_string(error: CoverageError) -> String {
  error.procedure
  <> " argument "
  <> int.to_string(error.arg_index)
  <> ": uncovered alternative \""
  <> error.uncovered_label
  <> "\" at path: "
  <> error.path
}

/// Dart `TypeCheckResult.toString()`.
pub fn result_to_string(res: TypeCheckResult) -> String {
  let errors_part = case res.errors {
    [] -> ""
    _ ->
      "Type Errors:\n"
      <> {
        res.errors
        |> list.map(fn(e) { "  " <> type_error_to_string(e) <> "\n" })
        |> string.concat
      }
  }
  let warnings_part = case res.warnings {
    [] -> ""
    _ ->
      "Warnings:\n"
      <> {
        res.warnings
        |> list.map(fn(w) { "  " <> type_warning_to_string(w) <> "\n" })
        |> string.concat
      }
  }
  let ok_part = case is_well_typed(res) && list.is_empty(res.warnings) {
    True -> "Program is well-typed.\n"
    False -> ""
  }
  errors_part <> warnings_part <> ok_part
}

// =============================================================================
// Entry points
// =============================================================================

/// Type-check a parsed module (Dart top-level `checkModule`). Builds the base
/// environment (ancestor scope or empty prelude), expands parameterized types,
/// builds the type environment + DFA, and runs the checker.
pub fn check_module(
  module: ast.SourceModule,
  transformed_procedures: Option(List(ast.Procedure)),
  ancestor_scope: Option(TypeEnvironment),
) -> Result(TypeCheckResult, teb.TypeEnvError) {
  use base_env <- result.try(case ancestor_scope {
    Some(e) -> Ok(e)
    None -> teb.build_prelude_environment("")
  })
  let expanded_module =
    param_expansion.expand_parameterized_types(
      module,
      set.from_list(dict.keys(base_env.types)),
      base_env.type_templates,
    )
  use type_env <- result.try(teb.build_type_environment(
    expanded_module,
    Some(base_env),
  ))
  // 064 T035 (wave-2 Finding F1): an unknown type reaching automaton
  // construction is a returned error, not a panic — surfaced as a TypeError so
  // the loader wraps it into a staged diagnostic (Dart parity: the loader
  // catches Dart's `UnknownTypeError` and prints `UnknownTypeError: <name>`).
  case program_dfa.build_program_dfa(type_env) {
    Error(program_dfa.UnknownTypeError(name, pos)) ->
      Ok(TypeCheckResult(
        [TypeError("UnknownTypeError: " <> name, pos.line, pos.column, None)],
        [],
      ))
    Ok(dfa) -> {
      let procedures = option.unwrap(transformed_procedures, module.procedures)
      let clauses = list.flat_map(procedures, fn(p) { p.clauses })
      Ok(check(type_env, dfa, clauses))
    }
  }
}

/// Parse and type-check GLP source (Dart `checkSource`). The source is parsed by
/// the main parser; a lex/parse failure is an invariant here (Dart throws uncaught).
pub fn check_source(source: String) -> Result(TypeCheckResult, teb.TypeEnvError) {
  let tokens = case lexer.tokenize(source) {
    Ok(t) -> t
    Error(e) -> panic as { "check_source: lex error: " <> string.inspect(e) }
  }
  let module = case parser.parse_module(tokens) {
    Ok(m) -> m
    Error(e) -> panic as { "check_source: parse error: " <> string.inspect(e) }
  }
  check_module(module, None, None)
}

// =============================================================================
// Core checker (Dart `TypeChecker.check`)
// =============================================================================

/// Check a program (list of clauses) against declared types (Dart `check`).
pub fn check(
  type_env: TypeEnvironment,
  dfa: ProgramDfa,
  clauses: List(ast.Clause),
) -> TypeCheckResult {
  // Phase 0: validate clause terms (anonymous-variable restrictions). At most one
  // validation error per clause (Dart wraps the whole clause in one try/catch).
  let validation_errors =
    list.filter_map(clauses, fn(clause) {
      case validate_clause(clause) {
        Error(ve) ->
          Ok(TypeError(ve.message, ve.line, ve.column, Some(clause_to_string(clause))))
        Ok(_) -> Error(Nil)
      }
    })

  case validation_errors {
    // If validation errors, return early.
    [_, ..] -> TypeCheckResult(validation_errors, [])
    [] -> {
      let grouped = group_clauses(clauses)

      // Check each declared procedure.
      let #(errors, warnings) =
        list.fold(dict.values(type_env.procedures), #([], []), fn(acc, proc_decl) {
          let #(errs, warns) = acc
          let key = type_ast.key(proc_decl)
          case list.key_find(grouped, key) {
            Error(_) ->
              // No clauses — warn unless it is a builtin (no GLP clauses).
              case proc_decl.is_builtin {
                True -> #(errs, warns)
                False -> #(
                  errs,
                  list.append(warns, [
                    TypeWarning(
                      "Procedure "
                        <> proc_decl.name
                        <> "/"
                        <> int.to_string(type_ast.arity(proc_decl))
                        <> " declared but not defined",
                      proc_decl.pos.line,
                      proc_decl.pos.column,
                    ),
                  ]),
                )
              }
            Ok(proc_clauses) -> {
              let res = check_procedure(type_env, dfa, proc_decl, proc_clauses)
              #(list.append(errs, res.errors), list.append(warns, res.warnings))
            }
          }
        })

      // Warn about clauses whose functor/arity has no type declaration.
      let undeclared_warnings =
        list.filter_map(grouped, fn(entry) {
          let #(key, clause_list) = entry
          case dict.has_key(type_env.procedures, key) {
            True -> Error(Nil)
            False ->
              case clause_list {
                [first, ..] ->
                  Ok(TypeWarning(
                    "Procedure " <> key <> " has no type declaration",
                    first.pos.line,
                    first.pos.column,
                  ))
                [] -> Error(Nil)
              }
          }
        })

      TypeCheckResult(errors, list.append(warnings, undeclared_warnings))
    }
  }
}

/// Validate one clause's head/guard/body terms, returning the first anonymous-
/// reader violation (Dart Phase-0 per-clause try/catch).
fn validate_clause(clause: ast.Clause) -> Result(Nil, clause_validation.ValidationError) {
  use _ <- result.try(list.try_each(
    clause.head.args,
    clause_validation.validate_clause_head,
  ))
  use _ <- result.try(case clause.guards {
    Some(guards) ->
      list.try_each(guards, fn(g) {
        list.try_each(g.args, clause_validation.validate_guard)
      })
    None -> Ok(Nil)
  })
  case clause.body {
    Some(body) ->
      list.try_each(body, fn(goal) {
        list.try_each(ast.goal_args(goal), clause_validation.validate_clause_body)
      })
    None -> Ok(Nil)
  }
}

/// Group clauses by "functor/arity", preserving first-encounter order (Dart's
/// `procedureClauses` LinkedHashMap).
fn group_clauses(clauses: List(ast.Clause)) -> List(#(String, List(ast.Clause))) {
  list.fold(clauses, [], fn(acc, clause) {
    let key =
      clause.head.functor <> "/" <> int.to_string(ast.atom_arity(clause.head))
    case list.key_find(acc, key) {
      Ok(existing) -> list.key_set(acc, key, list.append(existing, [clause]))
      Error(_) -> list.append(acc, [#(key, [clause])])
    }
  })
}

/// Check a single procedure against its declared type (Dart `_checkProcedure`).
fn check_procedure(
  type_env: TypeEnvironment,
  dfa: ProgramDfa,
  decl: ProcDecl,
  clauses: List(ast.Clause),
) -> TypeCheckResult {
  // Condition 1: covariance — every clause is well-typed.
  let covariance_errors =
    list.flat_map(clauses, fn(clause) {
      check_clause_covariance(type_env, dfa, clause)
    })

  // Condition 2: contravariance — every input path is covered.
  let arity = type_ast.arity(decl)
  let coverage_errors = case arity {
    0 -> []
    _ ->
      seq(1, arity)
      |> list.flat_map(fn(arg_index) {
        case type_ast.is_input_arg(decl, arg_index - 1) {
          True -> check_input_coverage(type_env, dfa, clauses, decl, arg_index)
          False -> []
        }
      })
  }

  TypeCheckResult(list.append(covariance_errors, coverage_errors), [])
}

// =============================================================================
// Covariance (Dart `_checkClauseCovariance`)
// =============================================================================

fn check_clause_covariance(
  type_env: TypeEnvironment,
  dfa: ProgramDfa,
  clause: ast.Clause,
) -> List(TypeError) {
  case wtc.check_clause_from_ast(clause, dfa, type_env) {
    Ok(res) ->
      case res.is_well_typed {
        True -> []
        False ->
          list.map(res.errors, fn(e) {
            TypeError(
              wtc.error_message(e),
              clause.pos.line,
              clause.pos.column,
              Some(clause_to_string(clause)),
            )
          })
      }
    Error(wtc.UndeclaredProcedure(functor, arity)) -> [
      TypeError(
        "Undeclared procedure: " <> functor <> "/" <> int.to_string(arity),
        clause.pos.line,
        clause.pos.column,
        Some(clause_to_string(clause)),
      ),
    ]
  }
}

// =============================================================================
// Contravariance — structural coverage (Dart `_checkInputCoverage` et al.)
// =============================================================================

fn check_input_coverage(
  _type_env: TypeEnvironment,
  dfa: ProgramDfa,
  clauses: List(ast.Clause),
  decl: ProcDecl,
  arg_index: Int,
) -> List(TypeError) {
  let arg_type = case list.drop(decl.arg_types, arg_index - 1) {
    [t, ..] -> t
    [] -> panic as "type_checker: argument index out of range"
  }
  case arg_type {
    // Wildcards are final states requiring no coverage checking (spec v0.7).
    PrimitiveModeAlt(_, _) -> []
    TypeRef(name, is_input, _, _) -> {
      let input_type_name = case is_input {
        True -> name <> "?"
        False -> name
      }
      case dict.get(dfa.automata, input_type_name) {
        Error(_) -> [
          TypeError(
            "Cannot get automaton for type " <> input_type_name,
            decl.pos.line,
            decl.pos.column,
            None,
          ),
        ]
        Ok(input_automaton) -> {
          let #(coverage_errors, _visited) =
            check_state_coverage(
              input_automaton.start_state,
              clauses,
              arg_index,
              name,
              set.new(),
              input_automaton,
              decl,
              [],
            )
          list.map(coverage_errors, fn(ce) {
            TypeError(
              coverage_error_to_string(ce),
              decl.pos.line,
              decl.pos.column,
              None,
            )
          })
        }
      }
    }
    _ -> panic as "type_checker: procedure argument type is neither TypeRef nor wildcard"
  }
}

/// Recursively check coverage at a DFA state (Dart `_checkStateCoverage`),
/// threading the visited-state set.
fn check_state_coverage(
  state: DfaState,
  clauses: List(ast.Clause),
  arg_index: Int,
  path_prefix: String,
  visited: set.Set(String),
  automaton: Automaton,
  decl: ProcDecl,
  struct_path: List(Int),
) -> #(List(CoverageError), set.Set(String)) {
  case set.contains(visited, program_dfa.state_name(state)) {
    True -> #([], visited)
    False -> {
      let visited1 = set.insert(visited, program_dfa.state_name(state))
      // Primitive/wildcard and final states need no structural coverage.
      case state.base_name == "_" || state.is_final {
        True -> #([], visited1)
        False ->
          // A variable at this path covers all alternatives.
          case any_clause_has_variable_at_path(clauses, arg_index, struct_path) {
            True -> #([], visited1)
            False ->
              list.fold(
                get_transitions_from_state(state, automaton),
                #([], visited1),
                fn(acc, entry) {
                  let #(errs, vis) = acc
                  let #(label, target_state) = entry
                  case
                    clause_accepts_label_at_path(clauses, arg_index, struct_path, label)
                  {
                    True -> {
                      let new_path = path_prefix <> " → " <> label
                      let new_struct_path = case extract_arg_index(label) {
                        Some(idx) -> list.append(struct_path, [idx])
                        None -> struct_path
                      }
                      let #(nested, vis2) =
                        check_state_coverage(
                          target_state,
                          clauses,
                          arg_index,
                          new_path,
                          vis,
                          automaton,
                          decl,
                          new_struct_path,
                        )
                      #(list.append(errs, nested), vis2)
                    }
                    False -> #(
                      list.append(errs, [
                        CoverageError(
                          decl.name,
                          arg_index,
                          label,
                          path_prefix <> " → " <> label,
                        ),
                      ]),
                      vis,
                    )
                  }
                },
              )
          }
      }
    }
  }
}

/// Whether any clause has a variable at the given structural path (Dart
/// `_anyClauseHasVariableAtPath`).
fn any_clause_has_variable_at_path(
  clauses: List(ast.Clause),
  arg_index: Int,
  struct_path: List(Int),
) -> Bool {
  list.any(clauses, fn(clause) {
    case arg_index > list.length(clause.head.args) {
      True -> False
      False ->
        case top_arg(clause, arg_index) {
          None -> False
          Some(arg) ->
            case navigate_to_path(arg, struct_path) {
              Some(ast.VarTerm(_, _, _)) -> True
              Some(ast.UnderscoreTerm(_, _)) -> True
              _ -> False
            }
        }
    }
  })
}

/// Whether any clause accepts the label at the given structural path (Dart
/// `_clauseAcceptsLabelAtPath`).
fn clause_accepts_label_at_path(
  clauses: List(ast.Clause),
  arg_index: Int,
  struct_path: List(Int),
  label_str: String,
) -> Bool {
  list.any(clauses, fn(clause) {
    case arg_index > list.length(clause.head.args) {
      True -> False
      False ->
        case top_arg(clause, arg_index) {
          None -> False
          Some(arg) ->
            case navigate_to_path(arg, struct_path) {
              None -> False
              Some(ast.VarTerm(_, _, _)) -> True
              Some(ast.UnderscoreTerm(_, _)) -> True
              Some(term) ->
                case wtc.get_labels_from_term(term) {
                  None -> True
                  Some(labels) -> labels_match(labels, label_str)
                }
            }
        }
    }
  })
}

fn top_arg(clause: ast.Clause, arg_index: Int) -> Option(ast.Term) {
  case list.drop(clause.head.args, arg_index - 1) {
    [arg, ..] -> Some(arg)
    [] -> None
  }
}

/// Navigate into a term following a structural path of 1-based arg indices (Dart
/// `_navigateToPath`).
fn navigate_to_path(term: ast.Term, struct_path: List(Int)) -> Option(ast.Term) {
  list.fold(struct_path, Some(term), fn(acc, idx) {
    case acc {
      None -> None
      Some(ast.StructTerm(_, args, _)) ->
        case idx >= 1 && idx <= list.length(args) {
          True -> option.from_result(list.first(list.drop(args, idx - 1)))
          False -> None
        }
      Some(ast.ListTerm(head, tail, _)) ->
        case head, tail {
          // Nil list: cannot navigate.
          None, None -> None
          _, _ ->
            case idx {
              1 -> head
              2 -> tail
              _ -> None
            }
        }
      Some(_) -> None
    }
  })
}

/// Extract the argument index from a label ending in "(arity,argIndex)" (Dart
/// `_extractArgIndex`, anchored at end — a mode suffix like ":↑" defeats the match).
fn extract_arg_index(symbol: String) -> Option(Int) {
  case string.ends_with(symbol, ")") {
    False -> None
    True ->
      case string.split_once(symbol, "(") {
        Error(_) -> None
        Ok(#(_before, rest)) -> {
          let inner = string.drop_end(rest, 1)
          case string.split_once(inner, ",") {
            Ok(#(a, b)) ->
              case is_digits(a) && is_digits(b) {
                True -> option.from_result(int.parse(b))
                False -> None
              }
            Error(_) -> None
          }
        }
      }
  }
}

/// All transitions from a state, keyed by the label's string form (Dart
/// `_getTransitionsFromState`).
fn get_transitions_from_state(
  state: DfaState,
  automaton: Automaton,
) -> List(#(String, DfaState)) {
  automaton.transitions
  |> dict.to_list
  |> list.filter_map(fn(entry) {
    let #(#(from_state, label), to_state) = entry
    case from_state == state {
      True -> Ok(#(program_dfa.label_to_string(label), to_state))
      False -> Error(Nil)
    }
  })
}

/// Whether accepted labels match a DFA transition label (Dart `_labelsMatch`).
fn labels_match(accepted_labels: set.Set(String), label_str: String) -> Bool {
  case set.contains(accepted_labels, label_str) {
    True -> True
    False ->
      // List cons: [|](2,1) / [|](2,2) match [|].
      case string.starts_with(label_str, "[|](") && set.contains(accepted_labels, "[|]") {
        True -> True
        False ->
          case label_str == "[]" {
            True -> set.contains(accepted_labels, "[]")
            False ->
              case string.starts_with(label_str, "\\(") {
                True -> difflist_labels_match(accepted_labels, label_str)
                False -> functor_labels_match(accepted_labels, label_str)
              }
          }
      }
  }
}

/// Difflist labels: `\(2,1)` / `\(2,2)` match `\/2` (or raw `\` / `\\`).
fn difflist_labels_match(
  accepted_labels: set.Set(String),
  label_str: String,
) -> Bool {
  let by_arity = case extract_functor_arity(label_str) {
    Some(#(_functor, arity)) -> set.contains(accepted_labels, "\\/" <> arity)
    None -> False
  }
  by_arity
  || set.contains(accepted_labels, "\\")
  || set.contains(accepted_labels, "\\\\")
}

/// Functor labels: `f(2,1)` matches `f/2`.
fn functor_labels_match(
  accepted_labels: set.Set(String),
  label_str: String,
) -> Bool {
  case extract_functor_arity(label_str) {
    Some(#(functor, arity)) ->
      case is_word(functor) {
        True -> set.contains(accepted_labels, functor <> "/" <> arity)
        False -> False
      }
    None -> False
  }
}

/// Parse "functor(arity,..." into (functor, arity) — the Dart `(\w+)\((\d+),`
/// match (functor validity checked by the caller via `is_word`).
fn extract_functor_arity(label_str: String) -> Option(#(String, String)) {
  case string.split_once(label_str, "(") {
    Error(_) -> None
    Ok(#(functor, rest)) ->
      case string.split_once(rest, ",") {
        Ok(#(arity, _)) ->
          case is_digits(arity) {
            True -> Some(#(functor, arity))
            False -> None
          }
        Error(_) -> None
      }
  }
}

// =============================================================================
// Helpers
// =============================================================================

/// Convert a clause to a string for error messages (Dart `_clauseToString`).
fn clause_to_string(clause: ast.Clause) -> String {
  let head =
    clause.head.functor
    <> "("
    <> int.to_string(ast.atom_arity(clause.head))
    <> " args)"
  case clause.body {
    None -> head <> "."
    Some([]) -> head <> "."
    Some(body) -> head <> " :- " <> int.to_string(list.length(body)) <> " goals."
  }
}

/// Inclusive integer sequence `from..to` (empty when `from > to`); stdlib
/// `list.range` is unavailable in this toolchain version.
fn seq(from: Int, to: Int) -> List(Int) {
  case from > to {
    True -> []
    False -> [from, ..seq(from + 1, to)]
  }
}

fn is_digits(s: String) -> Bool {
  s != ""
  && list.all(string.to_utf_codepoints(s), fn(cp) {
    let n = string.utf_codepoint_to_int(cp)
    n >= 48 && n <= 57
  })
}

fn is_word(s: String) -> Bool {
  s != ""
  && list.all(string.to_utf_codepoints(s), fn(cp) {
    let n = string.utf_codepoint_to_int(cp)
    { n >= 48 && n <= 57 }
    || { n >= 65 && n <= 90 }
    || { n >= 97 && n <= 122 }
    || n == 95
  })
}
