//// glp/analysis/type_checker/well_typed_clause — well-typed clause checking for
//// the GLP type system (feature 050, T018).
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/well_typed_clause.dart
//// (spec: docs/type system/well-typed-clause.md v0.9; paper Definition 5.7). A
//// clause `H :- G | B` is well-typed when the moded head is well-typed by the
//// procedure's type, each body atom is well-typed by its procedure's type, and
//// every variable pair (X, X?) is dual (both in head / both in body) or the same
//// type (one head, one body — body-body uses subtyping per Definition 4.8).
////
//// Port notes:
////   * The clause-wide anonymous-variable counter (Dart's module-global
////     `_anonVarCounter`, reset once per clause by `modedHead` and left running
////     across `producedTerm` calls) is threaded FUNCTIONALLY: `moded_head`
////     returns the ending counter, which is fed into the first body atom's
////     `produced_term`, and so on. A head `_` writer is flipped to a reader, so a
////     body `_` reusing its name would spuriously pair with it in duality
////     checking — hence the counter must not restart per atom.
////   * Dart `LinkedHashMap`/`Set` iterate in insertion order, and
////     `type_checker.dart` emits every clause error in order, so all maps here are
////     insertion-ordered assoc-lists and paths use `moded_term.paths_ordered`.
////   * Dart's `Goal`/`RemoteGoal`/`SpawnGoal` subclasses are one Gleam union;
////     `RemoteGoal.isDynamic`/`.staticModuleName` are derived from the module
////     term (VarTerm → dynamic; ConstTerm atom → static).
////   * Dart throws `UndeclaredProcedureError` (caught by type_checker); the port
////     returns it as a `Result` error. Dart's `ArityMismatchError` from
////     `modedHead`/`producedTerm` is `moded_head.ModedHeadError`.

import gleam/dict
import gleam/float
import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/set.{type Set}
import gleam/string
import glp/analysis/prelude
import glp/analysis/type_ast.{
  type ProcDecl, type TypeEnvironment, type TypeExpr, PrimitiveModeAlt, TypeRef,
}
import glp/analysis/type_checker/mode
import glp/analysis/type_checker/moded_head
import glp/analysis/type_checker/moded_term.{type ModedTerm}
import glp/analysis/type_checker/program_dfa.{type ProgramDfa}
import glp/analysis/type_checker/subtyping
import glp/analysis/type_checker/well_typed_term.{
  type VariableTypeInfo, type WellTypedError, type WellTypedResult,
  InconsistentPathError, InconsistentVariableError, NonDualError,
}
import glp/parser/ast
import glp/runtime/terms

// =============================================================================
// Result and error types
// =============================================================================

/// Result of checking if a clause is well-typed (Dart `ClauseCheckResult`).
/// `variable_types` is an insertion-ordered assoc-list keyed by "X"/"X?".
pub type ClauseCheckResult {
  ClauseCheckResult(
    is_well_typed: Bool,
    variable_types: List(#(String, VariableTypeInfo)),
    errors: List(ClauseError),
    moded_head: Option(ModedTerm),
    moded_body_atoms: List(ModedTerm),
  )
}

/// Dart `ClauseCheckResult.failure` (variable types / moded terms default empty).
fn clause_check_failure(errors: List(ClauseError)) -> ClauseCheckResult {
  ClauseCheckResult(False, [], errors, None, [])
}

/// A clause checking error (Dart abstract `ClauseError` and its subclasses).
pub type ClauseError {
  /// Error in head checking (Dart `HeadError`).
  HeadError(procedure_name: String, term_errors: List(WellTypedError))
  /// Error in body atom checking (Dart `BodyAtomError`).
  BodyAtomError(
    procedure_name: String,
    atom_index: Int,
    term_errors: List(WellTypedError),
  )
  /// Variable pair not dual across the clause (Dart `ClauseDualityError`).
  ClauseDualityError(
    base_name: String,
    writer_type: Option(VariableTypeInfo),
    reader_type: Option(VariableTypeInfo),
    writer_location: String,
    reader_location: String,
    reason: Option(String),
  )
  /// Undefined procedure (Dart `UndefinedProcedureError`).
  UndefinedProcedureError(procedure_name: String, arity: Int)
  /// Arity mismatch (Dart `ArityMismatchClauseError`).
  ArityMismatchClauseError(
    procedure_name: String,
    expected_arity: Int,
    actual_arity: Int,
  )
}

/// The `.message` getter of a `ClauseError` (Dart, verbatim).
pub fn error_message(error: ClauseError) -> String {
  case error {
    HeadError(procedure_name, term_errors) ->
      "Head of "
      <> procedure_name
      <> " is not well-typed:\n  "
      <> join_term_errors(term_errors)
    BodyAtomError(procedure_name, atom_index, term_errors) ->
      "Body atom "
      <> int.to_string(atom_index)
      <> " ("
      <> procedure_name
      <> ") is not well-typed:\n  "
      <> join_term_errors(term_errors)
    ClauseDualityError(
      base_name,
      writer_type,
      reader_type,
      writer_location,
      reader_location,
      reason,
    ) -> {
      let reason_str = case reason {
        Some(r) -> ": " <> r
        None -> ""
      }
      "Variable pair ("
      <> base_name
      <> ", "
      <> base_name
      <> "?) not dual across clause"
      <> reason_str
      <> ": writer at "
      <> writer_location
      <> "="
      <> optional_info(writer_type)
      <> ", reader at "
      <> reader_location
      <> "="
      <> optional_info(reader_type)
    }
    UndefinedProcedureError(procedure_name, arity) ->
      "Undefined procedure: " <> procedure_name <> "/" <> int.to_string(arity)
    ArityMismatchClauseError(procedure_name, expected_arity, actual_arity) ->
      "Arity mismatch for "
      <> procedure_name
      <> ": expected "
      <> int.to_string(expected_arity)
      <> ", got "
      <> int.to_string(actual_arity)
  }
}

fn join_term_errors(term_errors: List(WellTypedError)) -> String {
  term_errors
  |> list.map(well_typed_term.error_message)
  |> string.join("\n  ")
}

fn optional_info(info: Option(VariableTypeInfo)) -> String {
  case info {
    Some(i) -> well_typed_term.variable_type_info_to_string(i)
    None -> "null"
  }
}

/// Thrown by Dart `checkClauseFromAst` when a procedure is not declared; the port
/// returns it as a `Result` error (Dart `UndeclaredProcedureError`).
pub type UndeclaredProcedureError {
  UndeclaredProcedure(functor: String, arity: Int)
}

// =============================================================================
// Clause representation
// =============================================================================

/// A parsed clause structure for type checking (Dart `TypedClause`). `head` is an
/// `ast.Goal` (built from the clause head atom); `body_atoms` are guards ++ body.
pub type TypedClause {
  TypedClause(
    head: ast.Goal,
    body_atoms: List(ast.Goal),
    guard_atoms: List(ast.Goal),
  )
}

fn clause_head_functor(clause: TypedClause) -> String {
  ast.goal_functor(clause.head)
}

fn clause_head_arity(clause: TypedClause) -> Int {
  list.length(ast.goal_args(clause.head))
}

// =============================================================================
// Public functions
// =============================================================================

/// Check if a clause is well-typed in the given environment (Dart `checkClause`).
pub fn check_clause(
  clause: TypedClause,
  dfa: ProgramDfa,
  env: TypeEnvironment,
) -> ClauseCheckResult {
  let head_functor = clause_head_functor(clause)
  let head_arity = clause_head_arity(clause)

  case type_ast.get_procedure(env, head_functor, head_arity) {
    Error(_) ->
      clause_check_failure([UndefinedProcedureError(head_functor, head_arity)])
    Ok(proc_decl) ->
      case type_ast.arity(proc_decl) != head_arity {
        True ->
          clause_check_failure([
            ArityMismatchClauseError(
              head_functor,
              type_ast.arity(proc_decl),
              head_arity,
            ),
          ])
        False -> check_clause_body(clause, proc_decl, dfa, env, head_functor)
      }
  }
}

fn check_clause_body(
  clause: TypedClause,
  proc_decl: ProcDecl,
  dfa: ProgramDfa,
  env: TypeEnvironment,
  head_functor: String,
) -> ClauseCheckResult {
  // Step 1: head well-typing (returns the moded head and the ending counter).
  let #(head_result, constructed_moded_head, counter0) =
    check_head_with_term(clause, proc_decl, dfa, env)
  let head_errors = case head_result.is_well_typed {
    True -> []
    False -> [HeadError(head_functor, head_result.errors)]
  }
  // Record head variable types with location "head".
  let all_vt0 = head_result.variable_types
  let all_loc0 =
    list.map(head_result.variable_types, fn(e) { #(e.0, "head") })

  // Step 2: check each body atom, threading the anon-var counter.
  let initial = #(head_errors, all_vt0, all_loc0, [], counter0)
  let #(errors_b, all_vt, all_loc, moded_body_atoms, _final_counter) =
    list.index_fold(clause.body_atoms, initial, fn(acc, atom, i) {
      let #(errs, vt, loc, mbats, ctr) = acc
      let #(atom_result, moded_atom, ctr2) =
        check_body_atom_with_term(atom, i, dfa, env, vt, ctr)
      let mbats2 = case moded_atom {
        Some(t) -> list.append(mbats, [t])
        None -> mbats
      }
      let errs2 = case atom_result.is_well_typed {
        True -> errs
        False ->
          list.append(errs, [
            BodyAtomError(ast.goal_functor(atom), i, atom_result.errors),
          ])
      }
      let #(vt2, loc2) =
        merge_body_vars(vt, loc, atom_result.variable_types, i)
      #(errs2, vt2, loc2, mbats2, ctr2)
    })

  // Step 3: check variable pair duality across the clause.
  let duality_errors = check_clause_duality(all_vt, all_loc, dfa)
  let all_errors = list.append(errors_b, duality_errors)

  ClauseCheckResult(
    list.is_empty(all_errors),
    all_vt,
    all_errors,
    constructed_moded_head,
    moded_body_atoms,
  )
}

/// Merge a body atom's variable types into the accumulator (insert-if-absent,
/// preserving order), recording the location for new keys (Dart's step-2 merge).
fn merge_body_vars(
  vt: List(#(String, VariableTypeInfo)),
  loc: List(#(String, String)),
  atom_vt: List(#(String, VariableTypeInfo)),
  atom_index: Int,
) -> #(List(#(String, VariableTypeInfo)), List(#(String, String))) {
  list.fold(atom_vt, #(vt, loc), fn(acc, entry) {
    let #(cur_vt, cur_loc) = acc
    let #(var_key, new_info) = entry
    case list.key_find(cur_vt, var_key) {
      // Existing key: types must match — caught by the clause duality check.
      Ok(_) -> #(cur_vt, cur_loc)
      Error(_) -> #(
        list.append(cur_vt, [#(var_key, new_info)]),
        list.append(cur_loc, [#(var_key, "body atom " <> int.to_string(atom_index))]),
      )
    }
  })
}

/// Check if an `ast.Clause` is well-typed (Dart `checkClauseFromAst`). Returns an
/// `UndeclaredProcedureError` (Dart throws it) when the procedure is not declared.
pub fn check_clause_from_ast(
  clause: ast.Clause,
  dfa: ProgramDfa,
  env: TypeEnvironment,
) -> Result(ClauseCheckResult, UndeclaredProcedureError) {
  // Head Atom → Goal (Dart uses the clause position).
  let head = ast.Goal(clause.head.functor, clause.head.args, clause.pos)

  // Guards → goals (guards are procedure calls for type checking).
  let guard_goals =
    clause.guards
    |> option.unwrap([])
    |> list.map(fn(g) { ast.Goal(g.predicate, g.args, g.pos) })

  let body_goals = option.unwrap(clause.body, [])
  // H :- G | B is treated as H :- G, B.
  let all_body_atoms = list.append(guard_goals, body_goals)

  let typed_clause =
    TypedClause(head:, body_atoms: all_body_atoms, guard_atoms: guard_goals)

  case
    type_ast.has_procedure(
      env,
      clause_head_functor(typed_clause),
      clause_head_arity(typed_clause),
    )
  {
    False ->
      Error(UndeclaredProcedure(
        clause_head_functor(typed_clause),
        clause_head_arity(typed_clause),
      ))
    True -> Ok(check_clause(typed_clause, dfa, env))
  }
}

/// Labels a clause accepts at a 1-indexed argument position (Dart
/// `getAcceptedLabels`). `None` = variable/wildcard (accepts everything).
pub fn get_accepted_labels(
  clause: ast.Clause,
  arg_index: Int,
  _env: TypeEnvironment,
) -> Option(Set(String)) {
  case arg_index < 1 || arg_index > list.length(clause.head.args) {
    True -> Some(set.new())
    False ->
      case list.drop(clause.head.args, arg_index - 1) {
        [arg, ..] -> get_labels_from_term(arg)
        [] -> Some(set.new())
      }
  }
}

/// Labels from a term (Dart `getLabelsFromTerm`). `None` for a variable
/// (wildcard); a singleton for constants/lists/structs.
pub fn get_labels_from_term(term: ast.Term) -> Option(Set(String)) {
  case term {
    ast.VarTerm(_, _, _) -> None
    ast.UnderscoreTerm(_, _) -> None
    ast.ConstTerm(value, _) -> Some(set.from_list([const_label_string(value)]))
    ast.ListTerm(None, None, _) -> Some(set.from_list(["[]"]))
    ast.ListTerm(_, _, _) -> Some(set.from_list(["[|]"]))
    ast.StructTerm(functor, args, _) ->
      Some(set.from_list([functor <> "/" <> int.to_string(list.length(args))]))
  }
}

/// The bare `value.toString()` of a constant (Dart `Object.toString()`: atoms and
/// strings both unquoted).
fn const_label_string(value: terms.Constant) -> String {
  case value {
    terms.ConstAtom(name) -> name
    terms.ConstString(s) -> s
    terms.ConstInt(i) -> int.to_string(i)
    terms.ConstReal(r) -> float.to_string(r)
  }
}

/// The full type name including `?` for input mode (Dart `getFullTypeName`).
pub fn get_full_type_name(type_expr: TypeExpr) -> String {
  case type_expr {
    PrimitiveModeAlt(is_input, _) ->
      case is_input {
        True -> "_?"
        False -> "_"
      }
    TypeRef(name, is_input, _, _) ->
      case is_input {
        True -> name <> "?"
        False -> name
      }
    _ -> panic as "well_typed_clause: unknown type expression"
  }
}

// =============================================================================
// Head / body atom checking
// =============================================================================

/// Check head well-typing and return the constructed moded term and ending
/// anon-var counter (Dart `_checkHeadWithTerm`).
fn check_head_with_term(
  clause: TypedClause,
  proc_decl: ProcDecl,
  dfa: ProgramDfa,
  env: TypeEnvironment,
) -> #(WellTypedResult, Option(ModedTerm), Int) {
  case moded_head.moded_head(clause.head, proc_decl, Some(env)) {
    Ok(#(moded_head_term, counter)) -> {
      let result = check_moded_term_per_arg(moded_head_term, proc_decl, dfa)
      #(result, Some(moded_head_term), counter)
    }
    Error(moded_head.ArityMismatch(msg)) -> #(arity_mismatch_result(msg), None, 0)
  }
}

/// Check body atom well-typing, returning the moded term and the updated anon-var
/// counter (Dart `_checkBodyAtomWithTerm`, with the counter threaded).
fn check_body_atom_with_term(
  atom: ast.Goal,
  atom_index: Int,
  dfa: ProgramDfa,
  env: TypeEnvironment,
  caller_var_types: List(#(String, VariableTypeInfo)),
  counter: Int,
) -> #(WellTypedResult, Option(ModedTerm), Int) {
  case atom {
    // Goal@Agent — type-check the inner goal.
    ast.SpawnGoal(inner, _, _) ->
      check_body_atom_with_term(
        ast.Goal(inner.functor, inner.args, inner.pos),
        atom_index,
        dfa,
        env,
        caller_var_types,
        counter,
      )
    // M # proc(...) — check against the imported declaration.
    ast.RemoteGoal(_, _, _) ->
      check_remote_goal(atom, atom_index, dfa, env, counter)
    ast.Goal(functor, args, _) -> {
      let atom_arity = list.length(args)
      // Skip builtin goals (true, otherwise, :=).
      case prelude.is_builtin_goal(functor) {
        True -> #(well_typed_term.well_typed_success([]), None, counter)
        False ->
          case type_ast.get_procedure(env, functor, atom_arity) {
            Error(_) -> #(
              undefined_procedure_result(functor, atom_arity),
              None,
              counter,
            )
            Ok(proc_decl) ->
              check_declared_body_atom(
                atom,
                proc_decl,
                dfa,
                env,
                caller_var_types,
                counter,
              )
          }
      }
    }
  }
}

/// Handle a body atom whose procedure is declared: Case B parameterized
/// instantiation, then build the produced term and check it.
fn check_declared_body_atom(
  atom: ast.Goal,
  proc_decl: ProcDecl,
  dfa: ProgramDfa,
  env: TypeEnvironment,
  caller_var_types: List(#(String, VariableTypeInfo)),
  counter: Int,
) -> #(WellTypedResult, Option(ModedTerm), Int) {
  // Case B: call-site instantiation for parameterized procedures.
  case dict.get(env.param_proc_decls, type_ast.key(proc_decl)) {
    Ok(param_template) ->
      case caller_var_types != [] {
        True ->
          case infer_concrete_decl(param_template, atom, caller_var_types, dfa, env) {
            Some(inferred) ->
              check_produced_atom(atom, inferred, dfa, env, counter)
            // Inference failed — Case A (wildcard) already covers the proc's
            // own clauses; skip this body atom check.
            None -> #(well_typed_term.well_typed_success([]), None, counter)
          }
        // No caller variable types — can't infer; skip (Case A covers it).
        False -> #(well_typed_term.well_typed_success([]), None, counter)
      }
    Error(_) -> check_produced_atom(atom, proc_decl, dfa, env, counter)
  }
}

/// Build the produced term for a body atom (no variable flip) and check it.
fn check_produced_atom(
  atom: ast.Goal,
  proc_decl: ProcDecl,
  dfa: ProgramDfa,
  env: TypeEnvironment,
  counter: Int,
) -> #(WellTypedResult, Option(ModedTerm), Int) {
  case moded_head.produced_term(atom, proc_decl, Some(env), counter) {
    Ok(#(moded_atom_term, counter2)) -> {
      let result = check_moded_term_per_arg(moded_atom_term, proc_decl, dfa)
      #(result, Some(moded_atom_term), counter2)
    }
    Error(moded_head.ArityMismatch(msg)) -> #(arity_mismatch_result(msg), None, counter)
  }
}

/// Check a remote goal (M # proc(...)) against the imported declaration (Dart
/// `_checkRemoteGoal`). Type checking is local: look up the imported declaration.
fn check_remote_goal(
  remote: ast.Goal,
  _atom_index: Int,
  dfa: ProgramDfa,
  env: TypeEnvironment,
  counter: Int,
) -> #(WellTypedResult, Option(ModedTerm), Int) {
  // Peel nested RemoteGoals to build the module path and reach the actual goal.
  // Any dynamic (variable module) part skips type checking.
  case peel_remote(remote, []) {
    Error(_) -> #(well_typed_term.well_typed_success([]), None, counter)
    Ok(#(module_path, inner_goal)) -> {
      let goal_functor = ast.goal_functor(inner_goal)
      let goal_arity = list.length(ast.goal_args(inner_goal))
      let qualified_key =
        module_path <> "#" <> goal_functor <> "/" <> int.to_string(goal_arity)
      case dict.get(env.procedures, qualified_key) {
        Error(_) -> #(
          well_typed_term.well_typed_failure([
            InconsistentPathError(
              moded_term.ModedPath([
                moded_term.path_step(qualified_key, 0, mode.Output),
              ]),
              "No imported declaration for "
                <> module_path
                <> "#"
                <> goal_functor
                <> "/"
                <> int.to_string(goal_arity)
                <> " — add \"imported procedure "
                <> module_path
                <> "#"
                <> goal_functor
                <> "(...)\" to this module",
            ),
          ]),
          None,
          counter,
        )
        Ok(proc_decl) -> check_produced_atom(inner_goal, proc_decl, dfa, env, counter)
      }
    }
  }
}

/// Peel nested `RemoteGoal`s into `#(modulePath, innerGoal)`; `Error(Nil)` when a
/// dynamic (variable) module is encountered (Dart's while loop + isDynamic skip).
fn peel_remote(
  goal: ast.Goal,
  path_parts: List(String),
) -> Result(#(String, ast.Goal), Nil) {
  case goal {
    ast.RemoteGoal(module, inner, _) ->
      case remote_static_module_name(module) {
        Some(name) -> peel_remote(inner, list.append(path_parts, [name]))
        None -> Error(Nil)
      }
    _ -> Ok(#(string.join(path_parts, "#"), goal))
  }
}

/// The static module name of a remote-goal module term, `None` if dynamic
/// (Dart `RemoteGoal.staticModuleName` / `.isDynamic`).
fn remote_static_module_name(module: ast.Term) -> Option(String) {
  case module {
    ast.ConstTerm(terms.ConstAtom(name), _) -> Some(name)
    _ -> None
  }
}

fn arity_mismatch_result(msg: String) -> WellTypedResult {
  well_typed_term.well_typed_failure([
    InconsistentPathError(
      moded_term.ModedPath([moded_term.path_step(msg, 0, mode.Output)]),
      msg,
    ),
  ])
}

fn undefined_procedure_result(functor: String, arity: Int) -> WellTypedResult {
  let key = functor <> "/" <> int.to_string(arity)
  well_typed_term.well_typed_failure([
    InconsistentPathError(
      moded_term.ModedPath([moded_term.path_step(key, 0, mode.Output)]),
      "Undefined procedure: " <> key,
    ),
  ])
}

// =============================================================================
// Per-argument moded-term checking (Dart `_checkModedTermPerArg`)
// =============================================================================

/// Check each argument of a moded term against its declared type's automaton.
fn check_moded_term_per_arg(
  moded_term_val: ModedTerm,
  decl: ProcDecl,
  dfa: ProgramDfa,
) -> WellTypedResult {
  case moded_term_val {
    moded_term.ModedCompound(_, _, _, margs) -> {
      let initial: #(List(WellTypedError), List(#(String, VariableTypeInfo))) =
        #([], [])
      let #(errors, variable_types) =
        list.index_fold(
          list.zip(decl.arg_types, margs),
          initial,
          fn(acc, pair, i) {
            let #(arg_type, arg_term) = pair
            check_one_arg(acc, arg_type, arg_term, i, dfa)
          },
        )
      let duality_errors = check_term_duality(variable_types)
      let all_errors = list.append(errors, duality_errors)
      well_typed_term.WellTypedResult(
        list.is_empty(all_errors),
        variable_types,
        all_errors,
      )
    }
    _ ->
      well_typed_term.well_typed_failure([
        InconsistentPathError(
          moded_term.ModedPath([
            moded_term.path_step("not-compound", 0, mode.Output),
          ]),
          "Expected compound term for procedure",
        ),
      ])
  }
}

fn check_one_arg(
  acc: #(List(WellTypedError), List(#(String, VariableTypeInfo))),
  arg_type: TypeExpr,
  arg_term: ModedTerm,
  i: Int,
  dfa: ProgramDfa,
) -> #(List(WellTypedError), List(#(String, VariableTypeInfo))) {
  let #(errors, vts) = acc
  let arg_type_name = get_full_type_name(arg_type)
  case dict.get(dfa.automata, arg_type_name) {
    Error(_) -> #(
      list.append(errors, [
        InconsistentPathError(
          moded_term.ModedPath([
            moded_term.path_step(arg_type_name, i + 1, mode.Output),
          ]),
          "Unknown type: " <> arg_type_name,
        ),
      ]),
      vts,
    )
    Ok(arg_automaton) ->
      list.fold(moded_term.paths_ordered(arg_term), #(errors, vts), fn(acc2, path) {
        let #(errs, vts2) = acc2
        case well_typed_term.check_path_against_automaton(path, arg_automaton, dfa) {
          well_typed_term.PathInconsistent(reason) -> #(
            list.append(errs, [InconsistentPathError(path, reason)]),
            vts2,
          )
          well_typed_term.PathConsistent(Some(assignment)) -> {
            let leaf = moded_term.path_leaf(path)
            let var_key = leaf.symbol
            case list.key_find(vts2, var_key) {
              Ok(existing) ->
                case
                  program_dfa.state_name(existing.type_state)
                  != program_dfa.state_name(assignment.type_state)
                {
                  True -> #(
                    list.append(errs, [
                      InconsistentVariableError(var_key, existing, assignment),
                    ]),
                    vts2,
                  )
                  False -> #(errs, vts2)
                }
              Error(_) -> #(errs, list.append(vts2, [#(var_key, assignment)]))
            }
          }
          well_typed_term.PathConsistent(None) -> #(errs, vts2)
        }
      })
  }
}

// =============================================================================
// Duality checking
// =============================================================================

/// Check duality within a term (Dart `_checkTermDuality`) — a `NonDualError`
/// without a reason for each non-dual pair.
fn check_term_duality(
  variable_types: List(#(String, VariableTypeInfo)),
) -> List(WellTypedError) {
  variable_types
  |> group_by_base
  |> list.flat_map(fn(entry) {
    let #(base_name, variants) = entry
    case list.key_find(variants, base_name), list.key_find(variants, base_name <> "?") {
      Ok(writer_info), Ok(reader_info) ->
        case are_dual_types(writer_info, reader_info) {
          True -> []
          False -> [
            NonDualError(base_name, Some(writer_info), Some(reader_info), None),
          ]
        }
      _, _ -> []
    }
  })
}

/// Check variable pair type consistency across the entire clause (Dart
/// `_checkClauseDuality`, spec v0.9 Definition 4.10).
fn check_clause_duality(
  variable_types: List(#(String, VariableTypeInfo)),
  variable_locations: List(#(String, String)),
  dfa: ProgramDfa,
) -> List(ClauseError) {
  variable_types
  |> group_by_base
  |> list.flat_map(fn(entry) {
    let #(base_name, variants) = entry
    case list.key_find(variants, base_name), list.key_find(variants, base_name <> "?") {
      Ok(writer_info), Ok(reader_info) ->
        clause_duality_for_pair(
          base_name,
          writer_info,
          reader_info,
          loc_of(variable_locations, base_name),
          loc_of(variable_locations, base_name <> "?"),
          dfa,
        )
      _, _ -> []
    }
  })
}

fn clause_duality_for_pair(
  base_name: String,
  writer_info: VariableTypeInfo,
  reader_info: VariableTypeInfo,
  writer_loc: String,
  reader_loc: String,
  dfa: ProgramDfa,
) -> List(ClauseError) {
  let writer_norm = normalize_location(writer_loc)
  let reader_norm = normalize_location(reader_loc)
  case writer_norm == reader_norm {
    True ->
      case writer_norm == "head" {
        // Both in head: require exact dual types.
        True -> {
          let #(is_compat, reason) =
            are_dual_types_with_reason(writer_info, reader_info)
          case is_compat {
            True -> []
            False -> [
              ClauseDualityError(
                base_name,
                Some(writer_info),
                Some(reader_info),
                writer_loc,
                reader_loc,
                Some(
                  "Variables in same clause part (head) must have dual types: "
                  <> opt_reason(reason),
                ),
              ),
            ]
          }
        }
        // Both in body: require subtyping S <: T (Definition 4.8).
        False -> {
          let writer_output_state = writer_info.type_state
          let reader_output_state =
            program_dfa.get_dfa_state(dfa, reader_info.type_state.base_name)
          case subtyping.is_subtype(writer_output_state, reader_output_state, dfa) {
            True -> []
            False -> [
              ClauseDualityError(
                base_name,
                Some(writer_info),
                Some(reader_info),
                writer_loc,
                reader_loc,
                Some(
                  "Body variable pair: writer type "
                  <> program_dfa.state_name(writer_output_state)
                  <> " is not a subtype of "
                  <> program_dfa.state_name(reader_output_state),
                ),
              ),
            ]
          }
        }
      }
    // One in head, one in body: require same type.
    False -> {
      let #(is_same, reason) = are_same_type_with_reason(writer_info, reader_info)
      case is_same {
        True -> []
        False -> [
          ClauseDualityError(
            base_name,
            Some(writer_info),
            Some(reader_info),
            writer_loc,
            reader_loc,
            Some(
              "Variables across head/body must have same type: "
              <> opt_reason(reason),
            ),
          ),
        ]
      }
    }
  }
}

/// Normalize a location to "head" or "body" (Dart `_normalizeLocation`).
fn normalize_location(location: String) -> String {
  case location {
    "head" -> "head"
    _ ->
      case string.starts_with(location, "body") {
        True -> "body"
        False -> location
      }
  }
}

fn loc_of(locations: List(#(String, String)), key: String) -> String {
  case list.key_find(locations, key) {
    Ok(l) -> l
    Error(_) -> "unknown"
  }
}

fn opt_reason(reason: Option(String)) -> String {
  case reason {
    Some(r) -> r
    None -> "null"
  }
}

/// Whether writer and reader types are dual (Dart `_areDualTypes`).
fn are_dual_types(
  writer_info: VariableTypeInfo,
  reader_info: VariableTypeInfo,
) -> Bool {
  are_dual_types_with_reason(writer_info, reader_info).0
}

/// Dart `_areDualTypesWithReason` — dual iff writer produces, reader consumes,
/// same base name, opposite is_dual.
fn are_dual_types_with_reason(
  writer_info: VariableTypeInfo,
  reader_info: VariableTypeInfo,
) -> #(Bool, Option(String)) {
  case writer_info.mode != mode.Output {
    True -> #(False, Some("Writer must have produce mode"))
    False ->
      case reader_info.mode != mode.Input {
        True -> #(False, Some("Reader must have consume mode"))
        False ->
          case
            writer_info.type_state.base_name != reader_info.type_state.base_name
          {
            True -> #(
              False,
              Some(
                "Types must have same base: "
                <> program_dfa.state_name(writer_info.type_state)
                <> " vs "
                <> program_dfa.state_name(reader_info.type_state),
              ),
            )
            False ->
              case
                writer_info.type_state.is_dual == reader_info.type_state.is_dual
              {
                True -> #(
                  False,
                  Some(
                    "One must be dual, other not: "
                    <> program_dfa.state_name(writer_info.type_state)
                    <> " vs "
                    <> program_dfa.state_name(reader_info.type_state),
                  ),
                )
                False -> #(True, None)
              }
          }
      }
  }
}

/// Dart `_areSameTypeWithReason` — same iff base type names are identical.
fn are_same_type_with_reason(
  writer_info: VariableTypeInfo,
  reader_info: VariableTypeInfo,
) -> #(Bool, Option(String)) {
  case writer_info.type_state.base_name != reader_info.type_state.base_name {
    True -> #(
      False,
      Some(
        program_dfa.state_name(writer_info.type_state)
        <> " (base: "
        <> writer_info.type_state.base_name
        <> ") != "
        <> program_dfa.state_name(reader_info.type_state)
        <> " (base: "
        <> reader_info.type_state.base_name
        <> ")",
      ),
    )
    False -> #(True, None)
  }
}

/// Group variable types by base name (X and X? share base "X"), preserving
/// first-encounter order for base names and variants.
fn group_by_base(
  variable_types: List(#(String, VariableTypeInfo)),
) -> List(#(String, List(#(String, VariableTypeInfo)))) {
  list.fold(variable_types, [], fn(acc, entry) {
    let #(var_key, info) = entry
    let base_name = case string.ends_with(var_key, "?") {
      True -> string.slice(var_key, 0, string.length(var_key) - 1)
      False -> var_key
    }
    case list.key_find(acc, base_name) {
      Ok(variants) ->
        list.key_set(acc, base_name, list.append(variants, [#(var_key, info)]))
      Error(_) -> list.append(acc, [#(base_name, [#(var_key, info)])])
    }
  })
}

// =============================================================================
// Case B: call-site instantiation for parameterized procedures
// =============================================================================

/// Infer a concrete proc decl by matching a parameterized template against the
/// actual argument types at a call site (Dart `_inferConcreteDecl`). `None` when
/// inference fails.
fn infer_concrete_decl(
  param_template: ProcDecl,
  atom: ast.Goal,
  caller_var_types: List(#(String, VariableTypeInfo)),
  dfa: ProgramDfa,
  env: TypeEnvironment,
) -> Option(ProcDecl) {
  let _ = env
  // For each argument (up to the shorter of arity / args), infer bindings.
  let bindings =
    list.fold(
      list.zip(param_template.arg_types, ast.goal_args(atom)),
      dict.new(),
      fn(binds, pair) {
        let #(declared_type, actual_arg) = pair
        case actual_type_name(actual_arg, caller_var_types) {
          Some(actual_type) ->
            match_type_for_inference(
              declared_type,
              actual_type,
              param_template.type_params,
              binds,
            )
          None -> binds
        }
      },
    )

  case dict.is_empty(bindings) {
    True -> None
    False ->
      // All type params must be bound.
      case list.all(param_template.type_params, dict.has_key(bindings, _)) {
        False -> None
        True -> {
          let concrete_arg_types =
            list.map(param_template.arg_types, substitute_type_params(_, bindings))
          // Every referenced type must exist in the DFA.
          case
            list.all(concrete_arg_types, fn(t) {
              dict.has_key(dfa.automata, get_full_type_name(t))
            })
          {
            False -> None
            True ->
              Some(type_ast.ProcDecl(
                name: param_template.name,
                arg_types: concrete_arg_types,
                type_params: [],
                pos: param_template.pos,
                is_builtin: False,
                exported: param_template.exported,
                imported: param_template.imported,
                module_path: param_template.module_path,
              ))
          }
        }
      }
  }
}

/// The actual type name of a call-site argument from the caller's variable types
/// (Dart's `actualArg is VarTerm` block).
fn actual_type_name(
  actual_arg: ast.Term,
  caller_var_types: List(#(String, VariableTypeInfo)),
) -> Option(String) {
  case actual_arg {
    ast.VarTerm(name, is_reader, _) -> {
      let var_key = case is_reader {
        True -> name <> "?"
        False -> name
      }
      case list.key_find(caller_var_types, var_key) {
        Ok(info) -> Some(info.type_state.base_name)
        Error(_) -> None
      }
    }
    _ -> None
  }
}

/// Match a declared type expression against an actual type name to infer type
/// parameter bindings (Dart `_matchTypeForInference`).
fn match_type_for_inference(
  declared_type: TypeExpr,
  actual_type_name: String,
  type_params: List(String),
  bindings: dict.Dict(String, String),
) -> dict.Dict(String, String) {
  case declared_type {
    TypeRef(name, _, type_args, _) ->
      case type_args == [] && list.contains(type_params, name) {
        // Bare type parameter: X → actualTypeName.
        True -> dict_put_if_absent(bindings, name, actual_type_name)
        False ->
          case type_args != [] {
            True ->
              match_parameterized(
                name,
                type_args,
                actual_type_name,
                type_params,
                bindings,
              )
            False -> bindings
          }
      }
    _ -> bindings
  }
}

fn match_parameterized(
  decl_name: String,
  decl_type_args: List(TypeExpr),
  actual_type_name: String,
  type_params: List(String),
  bindings: dict.Dict(String, String),
) -> dict.Dict(String, String) {
  // Parse the actual type name "Stream<AgentMsg>" into template + args.
  case string.split_once(actual_type_name, "<") {
    Error(_) -> bindings
    Ok(#(actual_template, rest)) ->
      case actual_template != decl_name {
        True -> bindings
        False -> {
          // rest ends with the trailing ">".
          let args_str = string.slice(rest, 0, string.length(rest) - 1)
          let actual_args = split_type_args(args_str)
          case list.length(actual_args) != list.length(decl_type_args) {
            True -> bindings
            False ->
              list.fold(
                list.zip(decl_type_args, actual_args),
                bindings,
                fn(binds, pair) {
                  let #(decl_arg, actual) = pair
                  case decl_arg {
                    TypeRef(pname, _, [], _) ->
                      case list.contains(type_params, pname) {
                        True -> dict_put_if_absent(binds, pname, actual)
                        False -> binds
                      }
                    _ -> binds
                  }
                },
              )
          }
        }
      }
  }
}

/// Split comma-separated type args, respecting nested angle brackets (Dart
/// `_splitTypeArgs`).
fn split_type_args(s: String) -> List(String) {
  let #(parts, last, _depth) =
    list.fold(string.to_graphemes(s), #([], "", 0), fn(acc, ch) {
      let #(parts, cur, depth) = acc
      case ch {
        "<" -> #(parts, cur <> ch, depth + 1)
        ">" -> #(parts, cur <> ch, depth - 1)
        "," ->
          case depth == 0 {
            True -> #(list.append(parts, [string.trim(cur)]), "", depth)
            False -> #(parts, cur <> ch, depth)
          }
        _ -> #(parts, cur <> ch, depth)
      }
    })
  case last {
    "" -> parts
    _ -> list.append(parts, [string.trim(last)])
  }
}

/// Substitute type parameter names in a `TypeExpr` with concrete type names
/// (Dart `_substituteTypeParams`).
fn substitute_type_params(
  expr: TypeExpr,
  bindings: dict.Dict(String, String),
) -> TypeExpr {
  case expr {
    TypeRef(name, is_input, [], pos) ->
      case dict.get(bindings, name) {
        Ok(bound) -> TypeRef(bound, is_input, [], pos)
        Error(_) -> expr
      }
    TypeRef(name, is_input, type_args, pos) -> {
      let new_args = list.map(type_args, substitute_type_params(_, bindings))
      // Are all args now concrete (no remaining type params)?
      let all_concrete =
        list.all(new_args, fn(a) {
          case a {
            TypeRef(n, _, [], _) -> !dict.has_key(bindings, n)
            _ -> False
          }
        })
      case all_concrete {
        True -> {
          let expanded_name =
            name
            <> "<"
            <> {
              new_args
              |> list.map(type_ref_name)
              |> string.join(",")
            }
            <> ">"
          TypeRef(expanded_name, is_input, [], pos)
        }
        False -> TypeRef(name, is_input, new_args, pos)
      }
    }
    _ -> expr
  }
}

fn type_ref_name(expr: TypeExpr) -> String {
  case expr {
    TypeRef(name, _, _, _) -> name
    _ -> panic as "well_typed_clause: expected TypeRef in expanded type name"
  }
}

fn dict_put_if_absent(
  d: dict.Dict(String, String),
  key: String,
  value: String,
) -> dict.Dict(String, String) {
  case dict.has_key(d, key) {
    True -> d
    False -> dict.insert(d, key, value)
  }
}
