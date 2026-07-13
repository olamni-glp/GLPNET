//// glp/compiler/partial_eval — partial evaluator: compile-time unfolding of
//// defined guards (unit clauses in guard position) (feature 050, T017).
////
//// Dart source of truth — BOTH live copies are ported, because both run on
//// the reference load path and their observable surfaces differ:
////
//// * `transform_defined_guards` ports glp_runtime/lib/compiler/
////   partial_evaluator.dart `PartialEvaluator.transformDefinedGuards` — the
////   copy `GlpEngine.loadSource` runs on every module with procedure
////   declarations BEFORE the type checker. It enforces guard admission
////   (builtin guards, single-unit-clause procedures, and 049 form (a1)
////   test-only runtime-defined guards — any other call in guard position is
////   a CompileError), names fresh variables `PE<n>`, treats only the bare
////   `_` as anonymous during unfolding, and preserves RemoteGoal/SpawnGoal
////   under substitution.
//// * `transform_defined_guards_analyzer` ports the second copy embedded in
////   glp_runtime/lib/compiler/analyzer.dart (`Analyzer.analyze` STEP 2) —
////   the copy that feeds codegen. It keeps every non-unit guard untouched
////   (no admission checks), names fresh variables `PE_<n>`, treats every
////   '_'-prefixed variable as anonymous, flattens RemoteGoal/SpawnGoal to
////   their uniform functor/args view under substitution, and short-circuits
////   when no unit clauses exist at all.
////
//// partial_evaluator.dart's Stage-2 `unfoldReduceCalls` (and its private
//// guard simplification) has NO live caller in the reference pipeline (only
//// the archived bin/archive/glp_pe.dart tool reaches it) and is deliberately
//// not ported.
////
//// Prelude unit clauses: Dart caches them process-globally
//// (setPreludeUnitClauseSource/getPreludeUnitClauses over programs/self.glp).
//// The Gleam port has no global state — the loader parses the prelude once
//// and passes `collect_unit_clauses(prelude.procedures)` in explicitly. User
//// definitions override prelude entries (Dart spread order: prelude first,
//// user second).
////
//// SRSW preservation (PI:13): unfolding substitutes the unit clause's head
//// arguments after an injective renaming to fresh `PE`-numbered variables,
//// and injective renaming preserves SRSW-validity — proved as
//// `theorem rename_preserves_SRSW` (dossier row PI:13: "SRSW preserved under
//// faithful clause transforms", Lean, proved — docs/research/
//// glp-gleam-baseline/pipelines/P4-faithfulness/PROOFS/INDEX.md; backs
//// PARITY-BAR FB-M1-27). The SRSW checker itself runs BEFORE this transform
//// (glp/analysis/srsw on the original parse), so guard readers consumed by
//// unfolding have already been counted for pairing.

import gleam/bool
import gleam/dict.{type Dict}
import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/result
import gleam/set.{type Set}
import gleam/string
import glp/analysis/prelude
import glp/analysis/type_ast.{type Pos}
import glp/parser/ast
import glp/runtime/terms

/// A partial-evaluation-stage rejection (Dart CompileError). The two variants
/// preserve the Dart `phase` tag, which is rendering-relevant downstream:
/// phase 'analyzer' maps to ErrorCategory.semantic (`[semantic] ` prefix in
/// CompileError.toString); phase 'partial_evaluator' has no category mapping
/// (no prefix).
pub type PartialEvalError {
  /// Defined-guard reduction errors — negated defined guard, unfolding can
  /// never succeed, or not reducible at compile time (Dart phase 'analyzer').
  DefinedGuardError(message: String, pos: Pos)
  /// 049 guard-admission rejections — non-admissible call in guard position,
  /// or a negated runtime-defined guard (Dart phase 'partial_evaluator').
  GuardAdmissionError(message: String, pos: Pos)
}

/// Result of compile-time GLP unification for partial evaluation
/// (Dart UnifyResult).
type UnifyOutcome {
  UnifySuccess(substitution: Dict(String, ast.Term))
  UnifyFail(reason: String)
  UnifySuspend(unbound_readers: List(String))
}

/// The knobs on which the two Dart copies differ.
type Variant {
  Variant(
    /// Fresh-variable prefix: "PE" (engine copy) / "PE_" (analyzer copy).
    fresh_prefix: String,
    /// Anonymous-variable predicate used by renaming AND unification:
    /// engine copy exempts only the bare "_"; analyzer copy exempts every
    /// '_'-prefixed name.
    is_anonymous_name: fn(String) -> Bool,
    /// Guard admission: the engine copy enforces the 049 rule; the analyzer
    /// copy keeps every non-unit guard untouched.
    admission: Admission,
    /// The engine copy preserves RemoteGoal/SpawnGoal under substitution;
    /// the analyzer copy flattens them to the uniform functor/args view.
    preserve_goal_kinds: Bool,
  )
}

type Admission {
  EnforceAdmission(all_procedures: Set(String), test_only: Set(String))
  KeepAll
}

// ── Unit-clause and test-only collection ────────────────────────────────────

/// Collect unit clauses: "name/arity" → the head arguments of the procedure's
/// single guardless clause whose body is empty or just `true` (Dart
/// `_collectUnitClauses` == `getPreludeUnitClauses` filtering).
pub fn collect_unit_clauses(
  procedures: List(ast.Procedure),
) -> Dict(String, List(ast.Term)) {
  list.fold(procedures, dict.new(), fn(acc, procedure) {
    case procedure.clauses {
      [clause] ->
        case no_guards(clause.guards) && body_empty_or_true(clause.body) {
          True -> dict.insert(acc, ast.signature(procedure), clause.head.args)
          False -> acc
        }
      _ -> acc
    }
  })
}

/// Builtin guards a runtime-defined guard clause may use (Dart
/// `definedGuardBuiltins`) — exactly the subset the runner's interpretive
/// evaluator implements.
fn is_defined_guard_builtin(key: String) -> Bool {
  case key {
    "ground/1" | "known/1" | "=?=/2" -> True
    _ -> False
  }
}

/// Collect runtime-defined guard procedures (Dart `collectTestOnlyProcedures`,
/// 049 form (a1) admission rule): a procedure whose EVERY clause is test-only
/// (body empty or `true`, every guard a builtin from the admitted subset or a
/// non-negated call to another surviving candidate — greatest fixpoint).
/// Single-clause no-guard procedures are excluded (they keep the §8
/// compile-time unfolding).
pub fn collect_test_only_procedures(
  procedures: List(ast.Procedure),
) -> Set(String) {
  let by_key =
    list.fold(procedures, dict.new(), fn(acc, procedure) {
      dict.insert(acc, ast.signature(procedure), procedure)
    })
  let candidates =
    list.fold(procedures, set.new(), fn(acc, procedure) {
      case is_unit_clause_procedure(procedure) {
        True -> acc
        False ->
          case
            list.all(procedure.clauses, fn(clause) {
              body_empty_or_true(clause.body)
            })
          {
            True -> set.insert(acc, ast.signature(procedure))
            False -> acc
          }
      }
    })
  evict_until_stable(candidates, by_key)
}

fn evict_until_stable(
  candidates: Set(String),
  by_key: Dict(String, ast.Procedure),
) -> Set(String) {
  let next =
    set.to_list(candidates)
    |> list.fold(candidates, fn(current, key) {
      let admissible = case dict.get(by_key, key) {
        Ok(procedure) ->
          list.all(procedure.clauses, fn(clause) {
            option.unwrap(clause.guards, [])
            |> list.all(fn(guard) {
              let guard_key = guard_signature(guard)
              case is_defined_guard_builtin(guard_key) {
                True -> True
                False -> !guard.negated && set.contains(current, guard_key)
              }
            })
          })
        Error(_) -> False
      }
      case admissible {
        True -> current
        False -> set.delete(current, key)
      }
    })
  case set.size(next) == set.size(candidates) {
    True -> next
    False -> evict_until_stable(next, by_key)
  }
}

fn guard_signature(guard: ast.Guard) -> String {
  guard.predicate <> "/" <> int.to_string(list.length(guard.args))
}

fn no_guards(guards: Option(List(ast.Guard))) -> Bool {
  case guards {
    None | Some([]) -> True
    Some(_) -> False
  }
}

fn body_empty_or_true(body: Option(List(ast.Goal))) -> Bool {
  case body {
    None | Some([]) -> True
    Some([ast.Goal("true", [], _)]) -> True
    Some(_) -> False
  }
}

fn is_unit_clause_procedure(procedure: ast.Procedure) -> Bool {
  case procedure.clauses {
    [clause] -> no_guards(clause.guards) && body_empty_or_true(clause.body)
    _ -> False
  }
}

// ── Entry points ────────────────────────────────────────────────────────────

/// The engine copy (partial_evaluator.dart `transformDefinedGuards`): unfold
/// defined guards with 049 guard-admission enforcement. Runs before the type
/// checker on the reference load path.
pub fn transform_defined_guards(
  procedures: List(ast.Procedure),
  prelude_unit_clauses: Dict(String, List(ast.Term)),
) -> Result(List(ast.Procedure), PartialEvalError) {
  let unit_clauses =
    dict.merge(prelude_unit_clauses, collect_unit_clauses(procedures))
  let all_procedures =
    list.fold(procedures, set.new(), fn(acc, procedure) {
      set.insert(acc, ast.signature(procedure))
    })
  let variant =
    Variant(
      fresh_prefix: "PE",
      is_anonymous_name: fn(name) { name == "_" },
      admission: EnforceAdmission(
        all_procedures,
        collect_test_only_procedures(procedures),
      ),
      preserve_goal_kinds: True,
    )
  run_transform(procedures, unit_clauses, variant)
}

/// The analyzer copy (analyzer.dart `PartialEvaluator.transformDefinedGuards`,
/// `Analyzer.analyze` STEP 2): unfold defined guards keeping every non-unit
/// guard untouched. Feeds codegen on the reference compile path.
pub fn transform_defined_guards_analyzer(
  procedures: List(ast.Procedure),
  prelude_unit_clauses: Dict(String, List(ast.Term)),
) -> Result(List(ast.Procedure), PartialEvalError) {
  let unit_clauses =
    dict.merge(prelude_unit_clauses, collect_unit_clauses(procedures))
  use <- bool.guard(dict.size(unit_clauses) == 0, Ok(procedures))
  let variant =
    Variant(
      fresh_prefix: "PE_",
      is_anonymous_name: fn(name) { string.starts_with(name, "_") },
      admission: KeepAll,
      preserve_goal_kinds: False,
    )
  run_transform(procedures, unit_clauses, variant)
}

/// Transform every clause of every procedure; the fresh-variable counter is
/// shared across the whole run (one Dart PartialEvaluator instance per call).
fn run_transform(
  procedures: List(ast.Procedure),
  unit_clauses: Dict(String, List(ast.Term)),
  variant: Variant,
) -> Result(List(ast.Procedure), PartialEvalError) {
  list.try_fold(procedures, #([], 0), fn(acc, procedure) {
    let #(done, counter) = acc
    use #(clauses, counter) <- result.try(
      list.try_fold(procedure.clauses, #([], counter), fn(clause_acc, clause) {
        let #(transformed, counter) = clause_acc
        use #(new_clause, counter) <- result.try(transform_clause(
          clause,
          unit_clauses,
          variant,
          counter,
        ))
        Ok(#([new_clause, ..transformed], counter))
      }),
    )
    let new_procedure =
      ast.Procedure(
        procedure.name,
        procedure.arity,
        list.reverse(clauses),
        procedure.pos,
      )
    Ok(#([new_procedure, ..done], counter))
  })
  |> result.map(fn(acc) { list.reverse(acc.0) })
}

// ── Clause transformation (Dart _transformClause) ──────────────────────────

fn transform_clause(
  clause: ast.Clause,
  unit_clauses: Dict(String, List(ast.Term)),
  variant: Variant,
  counter: Int,
) -> Result(#(ast.Clause, Int), PartialEvalError) {
  case clause.guards {
    None | Some([]) -> Ok(#(clause, counter))
    Some(guards) -> {
      use #(head, final_guards, body, counter) <- result.try(reduce_guards(
        clause.head,
        guards,
        [],
        clause.body,
        unit_clauses,
        variant,
        counter,
      ))
      let guards_option = case final_guards {
        [] -> None
        _ -> Some(final_guards)
      }
      Ok(#(ast.Clause(head, guards_option, body, clause.pos), counter))
    }
  }
}

/// The Dart fixpoint loop: scan `pending` accumulating kept guards; on a
/// successful unfold apply the substitution to head, kept, rest, and body,
/// then restart the scan over kept ++ rest.
fn reduce_guards(
  head: ast.Atom,
  pending: List(ast.Guard),
  kept: List(ast.Guard),
  body: Option(List(ast.Goal)),
  unit_clauses: Dict(String, List(ast.Term)),
  variant: Variant,
  counter: Int,
) -> Result(
  #(ast.Atom, List(ast.Guard), Option(List(ast.Goal)), Int),
  PartialEvalError,
) {
  case pending {
    [] -> Ok(#(head, list.reverse(kept), body, counter))
    [guard, ..rest] -> {
      let key = guard_signature(guard)
      case dict.get(unit_clauses, key) {
        Ok(unit_args) ->
          case guard.negated {
            True ->
              Error(DefinedGuardError(
                "Defined guard \"" <> guard.predicate <> "\" cannot be negated",
                guard.pos,
              ))
            False -> {
              let #(renamed_args, counter) =
                rename_unit_clause_vars(unit_args, variant, counter)
              case glp_unify_for_pe(guard.args, renamed_args, variant) {
                UnifyFail(reason) ->
                  Error(DefinedGuardError(
                    "Defined guard \""
                      <> guard_call_to_string(guard)
                      <> "\" can never succeed.\n  Unit clause: "
                      <> unit_clause_to_string(guard.predicate, unit_args)
                      <> "\n  Reason: "
                      <> reason
                      <> "\n  This clause is unreachable.",
                    guard.pos,
                  ))
                UnifySuspend(unbound_readers) ->
                  Error(DefinedGuardError(
                    "Cannot reduce defined guard \""
                      <> guard_call_to_string(guard)
                      <> "\" at compile time.\n  Unit clause: "
                      <> unit_clause_to_string(guard.predicate, unit_args)
                      <> "\n  Unbound readers: "
                      <> {
                      unbound_readers
                      |> list.map(fn(reader) { reader <> "?" })
                      |> string.join(", ")
                    }
                      <> "\n  Defined guards must be fully reducible at compile time.",
                    guard.pos,
                  ))
                UnifySuccess(substitution) -> {
                  let new_head = apply_substitution_to_atom(head, substitution)
                  let new_pending =
                    list.append(list.reverse(kept), rest)
                    |> list.map(fn(g) {
                      apply_substitution_to_guard(g, substitution)
                    })
                  let new_body =
                    option.map(body, fn(goals) {
                      list.map(goals, fn(goal) {
                        apply_substitution_to_goal(goal, substitution, variant)
                      })
                    })
                  reduce_guards(
                    new_head,
                    new_pending,
                    [],
                    new_body,
                    unit_clauses,
                    variant,
                    counter,
                  )
                }
              }
            }
          }
        Error(_) ->
          // Not a defined guard.
          case variant.admission {
            KeepAll ->
              reduce_guards(
                head,
                rest,
                [guard, ..kept],
                body,
                unit_clauses,
                variant,
                counter,
              )
            EnforceAdmission(all_procedures, test_only) ->
              case prelude.is_builtin_procedure(key) {
                True ->
                  reduce_guards(
                    head,
                    rest,
                    [guard, ..kept],
                    body,
                    unit_clauses,
                    variant,
                    counter,
                  )
                False ->
                  case set.contains(all_procedures, key) {
                    True ->
                      case set.contains(test_only, key) {
                        True ->
                          case guard.negated {
                            True ->
                              Error(GuardAdmissionError(
                                "Runtime-defined guard \""
                                  <> guard.predicate
                                  <> "\" cannot be negated",
                                guard.pos,
                              ))
                            False ->
                              reduce_guards(
                                head,
                                rest,
                                [guard, ..kept],
                                body,
                                unit_clauses,
                                variant,
                                counter,
                              )
                          }
                        False ->
                          Error(GuardAdmissionError(
                            "Cannot call \""
                              <> key
                              <> "\" in guard position.\n  Only builtin guards, single-unit-clause procedures, and test-only\n  (runtime-defined guard) procedures can appear in guards.\n  The procedure \""
                              <> guard.predicate
                              <> "\" has clauses with real bodies or\n  guards outside the admitted subset.",
                            guard.pos,
                          ))
                      }
                    False ->
                      // Unknown guard — the type checker catches undefined
                      // procedures later.
                      reduce_guards(
                        head,
                        rest,
                        [guard, ..kept],
                        body,
                        unit_clauses,
                        variant,
                        counter,
                      )
                  }
              }
          }
      }
    }
  }
}

/// `predicate(arg, arg)` — Dart `'${guard.predicate}(${guard.args.join(", ")})'`.
fn guard_call_to_string(guard: ast.Guard) -> String {
  guard.predicate <> "(" <> ast.terms_to_string(guard.args) <> ")"
}

/// `predicate(unit args)` over the ORIGINAL (unrenamed) unit clause arguments.
fn unit_clause_to_string(
  predicate: String,
  unit_args: List(ast.Term),
) -> String {
  predicate <> "(" <> ast.terms_to_string(unit_args) <> ")"
}

// ── Fresh renaming (Dart _renameUnitClauseVars) ─────────────────────────────

fn rename_unit_clause_vars(
  unit_args: List(ast.Term),
  variant: Variant,
  counter: Int,
) -> #(List(ast.Term), Int) {
  let names = collect_var_names(unit_args)
  let #(renaming, counter) =
    list.fold(names, #(dict.new(), counter), fn(acc, name) {
      let #(renaming, counter) = acc
      case variant.is_anonymous_name(name) {
        True -> acc
        False -> #(
          dict.insert(
            renaming,
            name,
            variant.fresh_prefix <> int.to_string(counter),
          ),
          counter + 1,
        )
      }
    })
  #(list.map(unit_args, fn(arg) { apply_renaming(arg, renaming) }), counter)
}

/// Variable names in first-seen depth-first order (the Dart LinkedHashSet
/// iteration order — fresh numbering depends on it).
fn collect_var_names(args: List(ast.Term)) -> List(String) {
  list.fold(args, [], collect_var_names_term) |> list.reverse
}

fn collect_var_names_term(acc: List(String), term: ast.Term) -> List(String) {
  case term {
    ast.VarTerm(name, _, _) ->
      case list.contains(acc, name) {
        True -> acc
        False -> [name, ..acc]
      }
    ast.StructTerm(_, args, _) -> list.fold(args, acc, collect_var_names_term)
    ast.ListTerm(head, tail, _) -> {
      let acc = case head {
        Some(head_term) -> collect_var_names_term(acc, head_term)
        None -> acc
      }
      case tail {
        Some(tail_term) -> collect_var_names_term(acc, tail_term)
        None -> acc
      }
    }
    ast.ConstTerm(_, _) | ast.UnderscoreTerm(_, _) -> acc
  }
}

fn apply_renaming(term: ast.Term, renaming: Dict(String, String)) -> ast.Term {
  case term {
    // Dart: a VarTerm literally named "_" becomes an UnderscoreTerm (writer).
    ast.VarTerm("_", _, pos) -> ast.UnderscoreTerm(False, pos)
    ast.VarTerm(name, is_reader, pos) ->
      case dict.get(renaming, name) {
        Ok(new_name) -> ast.VarTerm(new_name, is_reader, pos)
        Error(_) -> term
      }
    ast.StructTerm(functor, args, pos) ->
      ast.StructTerm(
        functor,
        list.map(args, fn(arg) { apply_renaming(arg, renaming) }),
        pos,
      )
    ast.ListTerm(head, tail, pos) ->
      ast.ListTerm(
        option.map(head, fn(t) { apply_renaming(t, renaming) }),
        option.map(tail, fn(t) { apply_renaming(t, renaming) }),
        pos,
      )
    ast.ConstTerm(_, _) | ast.UnderscoreTerm(_, _) -> term
  }
}

// ── Compile-time GLP unification (Dart _glpUnifyForPE) ─────────────────────

/// Unification state: substitution plus the suspension set. The suspension
/// set keeps INSERTION order (Dart LinkedHashSet) — the suspend-error message
/// lists unresolved readers in first-added order. Stored reversed; read via
/// `list.reverse`.
type UState {
  UState(subst: Dict(String, ast.Term), susp_reversed: List(String))
}

fn susp_add(state: UState, name: String) -> UState {
  case list.contains(state.susp_reversed, name) {
    True -> state
    False -> UState(..state, susp_reversed: [name, ..state.susp_reversed])
  }
}

fn glp_unify_for_pe(
  call_args: List(ast.Term),
  unit_args: List(ast.Term),
  variant: Variant,
) -> UnifyOutcome {
  use <- bool.guard(
    list.length(call_args) != list.length(unit_args),
    UnifyFail(
      "Arity mismatch: "
      <> int.to_string(list.length(call_args))
      <> " vs "
      <> int.to_string(list.length(unit_args)),
    ),
  )
  // Phase 1: collection over each argument pair.
  let collected =
    list.zip(call_args, unit_args)
    |> list.try_fold(UState(dict.new(), []), fn(state, pair) {
      let #(call_arg, unit_arg) = pair
      unify_terms(call_arg, unit_arg, state, variant)
    })
  case collected {
    Error(reason) -> UnifyFail(reason)
    Ok(state) -> {
      // Phase 2: resolution — a reader X? suspends if X never entered the
      // substitution domain.
      let unresolved =
        state.susp_reversed
        |> list.reverse
        |> list.filter(fn(name) { !dict.has_key(state.subst, name) })
      case unresolved {
        [] -> UnifySuccess(resolve_substitution(state.subst))
        _ -> UnifySuspend(unresolved)
      }
    }
  }
}

/// Dart `_substSet`: assign subst[key] = value; if key was previously aliased
/// to a writer variable and the new value is concrete, propagate the binding
/// to the alias target too.
fn subst_set(state: UState, key: String, value: ast.Term) -> UState {
  let subst = case dict.get(state.subst, key) {
    Ok(ast.VarTerm(old_name, False, _)) ->
      case value {
        ast.VarTerm(_, _, _) -> state.subst
        _ ->
          case dict.has_key(state.subst, old_name) {
            True -> state.subst
            False -> dict.insert(state.subst, old_name, value)
          }
      }
    _ -> state.subst
  }
  UState(..state, subst: dict.insert(subst, key, value))
}

fn is_anonymous_term(term: ast.Term, variant: Variant) -> Bool {
  case term {
    ast.UnderscoreTerm(_, _) -> True
    ast.VarTerm(name, _, _) -> variant.is_anonymous_name(name)
    _ -> False
  }
}

/// Unify one call/unit argument pair (Dart `_unifyTerms`); Error carries the
/// UnifyFail reason.
fn unify_terms(
  call_arg: ast.Term,
  unit_arg: ast.Term,
  state: UState,
  variant: Variant,
) -> Result(UState, String) {
  // Anonymous on either side: always succeeds, no binding.
  use <- bool.guard(
    is_anonymous_term(call_arg, variant) || is_anonymous_term(unit_arg, variant),
    Ok(state),
  )
  case call_arg {
    // Call arg is a writer.
    ast.VarTerm(call_name, False, _) ->
      case unit_arg {
        // Writer/reader unit variable: alias the unit variable to the call
        // writer (plain assignment — Dart uses subst[...] =, not _substSet).
        ast.VarTerm(unit_name, _, _) ->
          Ok(
            UState(
              ..state,
              subst: dict.insert(state.subst, unit_name, call_arg),
            ),
          )
        // Constant/structure: bind the call writer to the unit arg.
        _ ->
          Ok(
            UState(
              ..state,
              subst: dict.insert(state.subst, call_name, unit_arg),
            ),
          )
      }
    // Call arg is a reader — X? refers to writer X.
    ast.VarTerm(writer_name, True, pos) ->
      case unit_arg {
        ast.VarTerm(unit_name, False, _) ->
          // Reader vs writer: alias unit writer to the call writer.
          Ok(
            UState(
              ..state,
              subst: dict.insert(
                state.subst,
                unit_name,
                ast.VarTerm(writer_name, False, pos),
              ),
            ),
          )
        ast.VarTerm(unit_name, True, _) -> {
          // Reader vs reader: both suspend on the same writer; alias.
          let state =
            UState(
              ..state,
              subst: dict.insert(
                state.subst,
                unit_name,
                ast.VarTerm(writer_name, False, pos),
              ),
            )
          Ok(susp_add(state, writer_name))
        }
        _ -> {
          // Reader vs constant/structure: suspend on the writer; record what
          // it should match (structural compatibility if already bound).
          let state = susp_add(state, writer_name)
          case dict.get(state.subst, writer_name) {
            Ok(existing) -> {
              use _ <- result.try(check_compatible(existing, unit_arg))
              Ok(state)
            }
            Error(_) ->
              Ok(
                UState(
                  ..state,
                  subst: dict.insert(state.subst, writer_name, unit_arg),
                ),
              )
          }
        }
      }
    // Call arg is a constant.
    ast.ConstTerm(call_value, _) ->
      case unit_arg {
        ast.ConstTerm(unit_value, _) ->
          case constants_equal(call_value, unit_value) {
            True -> Ok(state)
            False ->
              Error(
                "Constant mismatch: "
                <> ast.const_value_to_string(call_value)
                <> " vs "
                <> ast.const_value_to_string(unit_value),
              )
          }
        ast.VarTerm(unit_name, _, _) ->
          Ok(subst_set(state, unit_name, call_arg))
        _ ->
          Error(
            "Constant "
            <> ast.const_value_to_string(call_value)
            <> " cannot match structure "
            <> ast.term_to_string(unit_arg),
          )
      }
    // Call arg is a structure.
    ast.StructTerm(call_functor, call_struct_args, _) ->
      case unit_arg {
        ast.StructTerm(unit_functor, unit_struct_args, _) ->
          case
            call_functor == unit_functor
            && list.length(call_struct_args) == list.length(unit_struct_args)
          {
            True ->
              list.zip(call_struct_args, unit_struct_args)
              |> list.try_fold(state, fn(state, pair) {
                unify_terms(pair.0, pair.1, state, variant)
              })
            False ->
              Error(
                "Functor mismatch: "
                <> call_functor
                <> "/"
                <> int.to_string(list.length(call_struct_args))
                <> " vs "
                <> unit_functor
                <> "/"
                <> int.to_string(list.length(unit_struct_args)),
              )
          }
        ast.VarTerm(unit_name, _, _) ->
          Ok(subst_set(state, unit_name, call_arg))
        _ ->
          Error(
            "Structure "
            <> call_functor
            <> " cannot match "
            <> ast.term_to_string(unit_arg),
          )
      }
    // Call arg is a list.
    ast.ListTerm(call_head, call_tail, _) ->
      case unit_arg {
        ast.ListTerm(unit_head, unit_tail, _) -> {
          let call_nil = ast.is_nil(call_arg)
          let unit_nil = ast.is_nil(unit_arg)
          case call_nil, unit_nil {
            True, True -> Ok(state)
            True, False | False, True ->
              Error("List structure mismatch: nil vs non-nil")
            False, False -> {
              // Recurse on head and tail where BOTH sides have them (Dart
              // skips a side whose head/tail is null).
              use state <- result.try(case call_head, unit_head {
                Some(call_h), Some(unit_h) ->
                  unify_terms(call_h, unit_h, state, variant)
                _, _ -> Ok(state)
              })
              case call_tail, unit_tail {
                Some(call_t), Some(unit_t) ->
                  unify_terms(call_t, unit_t, state, variant)
                _, _ -> Ok(state)
              }
            }
          }
        }
        ast.VarTerm(unit_name, _, _) ->
          Ok(subst_set(state, unit_name, call_arg))
        _ -> Error("List cannot match " <> ast.term_to_string(unit_arg))
      }
    // Anonymous call args were handled above; unreachable.
    ast.UnderscoreTerm(_, _) -> Ok(state)
  }
}

/// Dart num equality on ConstTerm values: cross-kind int/real compare
/// numerically; the Dart value space quote-wraps strings, so an atom equals a
/// string iff the atom text IS the quote-wrapped string.
fn constants_equal(a: terms.Constant, b: terms.Constant) -> Bool {
  case a, b {
    terms.ConstInt(x), terms.ConstInt(y) -> x == y
    terms.ConstReal(x), terms.ConstReal(y) -> x == y
    terms.ConstInt(x), terms.ConstReal(y) -> int.to_float(x) == y
    terms.ConstReal(x), terms.ConstInt(y) -> x == int.to_float(y)
    terms.ConstAtom(x), terms.ConstAtom(y) -> x == y
    terms.ConstString(x), terms.ConstString(y) -> x == y
    terms.ConstAtom(x), terms.ConstString(y) -> x == "\"" <> y <> "\""
    terms.ConstString(x), terms.ConstAtom(y) -> "\"" <> x <> "\"" == y
    // Remaining cross-kind pairs (number vs text) are never equal in the
    // Dart Object value space.
    _, _ -> False
  }
}

/// Dart `_checkCompatible`: shallow structural compatibility between an
/// existing binding and a new requirement.
fn check_compatible(
  existing: ast.Term,
  new_term: ast.Term,
) -> Result(Nil, String) {
  case existing, new_term {
    ast.ConstTerm(existing_value, _), ast.ConstTerm(new_value, _) ->
      case constants_equal(existing_value, new_value) {
        True -> Ok(Nil)
        False ->
          Error(
            "Incompatible bindings: "
            <> ast.const_value_to_string(existing_value)
            <> " vs "
            <> ast.const_value_to_string(new_value),
          )
      }
    ast.StructTerm(existing_functor, existing_args, _),
      ast.StructTerm(new_functor, new_args, _)
    ->
      case
        existing_functor == new_functor
        && list.length(existing_args) == list.length(new_args)
      {
        True -> Ok(Nil)
        False ->
          Error(
            "Incompatible structures: "
            <> existing_functor
            <> " vs "
            <> new_functor,
          )
      }
    // Other combinations accepted (variables resolve later).
    _, _ -> Ok(Nil)
  }
}

// ── Substitution resolution and application ────────────────────────────────

/// Dart `_resolveSubstitution`: resolve variable chains, e.g.
/// σ = {X → Y, Y → f(Z)} becomes {X → f(Z), Y → f(Z)}.
fn resolve_substitution(
  subst: Dict(String, ast.Term),
) -> Dict(String, ast.Term) {
  dict.map_values(subst, fn(_, term) { resolve_term(term, subst, set.new()) })
}

fn resolve_term(
  term: ast.Term,
  subst: Dict(String, ast.Term),
  visited: Set(String),
) -> ast.Term {
  case term {
    ast.VarTerm(name, is_reader, _) ->
      case set.contains(visited, name) {
        // Cycle — return as is.
        True -> term
        False ->
          case dict.get(subst, name) {
            Ok(bound) -> {
              let resolved =
                resolve_term(bound, subst, set.insert(visited, name))
              // Preserve reader status when resolving to another variable.
              case is_reader, resolved {
                True, ast.VarTerm(resolved_name, False, resolved_pos) ->
                  ast.VarTerm(resolved_name, True, resolved_pos)
                _, _ -> resolved
              }
            }
            Error(_) -> term
          }
      }
    ast.StructTerm(functor, args, pos) ->
      ast.StructTerm(
        functor,
        list.map(args, fn(arg) { resolve_term(arg, subst, visited) }),
        pos,
      )
    ast.ListTerm(head, tail, pos) ->
      case ast.is_nil(term) {
        True -> term
        False ->
          ast.ListTerm(
            option.map(head, fn(t) { resolve_term(t, subst, visited) }),
            option.map(tail, fn(t) { resolve_term(t, subst, visited) }),
            pos,
          )
      }
    ast.ConstTerm(_, _) | ast.UnderscoreTerm(_, _) -> term
  }
}

/// Dart `_applySubstitution`.
fn apply_substitution(
  term: ast.Term,
  subst: Dict(String, ast.Term),
) -> ast.Term {
  case term {
    ast.VarTerm("_", _, _) -> term
    ast.VarTerm(name, is_reader, _) ->
      case dict.get(subst, name) {
        Ok(replacement) ->
          // A reader whose replacement is a writer variable stays a reader of
          // the new name; otherwise substitute through.
          case is_reader, replacement {
            True, ast.VarTerm(replacement_name, False, replacement_pos) ->
              ast.VarTerm(replacement_name, True, replacement_pos)
            _, _ -> apply_substitution(replacement, subst)
          }
        Error(_) -> term
      }
    ast.StructTerm(functor, args, pos) ->
      ast.StructTerm(
        functor,
        list.map(args, fn(arg) { apply_substitution(arg, subst) }),
        pos,
      )
    ast.ListTerm(head, tail, pos) ->
      case ast.is_nil(term) {
        True -> term
        False ->
          ast.ListTerm(
            option.map(head, fn(t) { apply_substitution(t, subst) }),
            option.map(tail, fn(t) { apply_substitution(t, subst) }),
            pos,
          )
      }
    ast.ConstTerm(_, _) | ast.UnderscoreTerm(_, _) -> term
  }
}

fn apply_substitution_to_atom(
  atom: ast.Atom,
  subst: Dict(String, ast.Term),
) -> ast.Atom {
  ast.Atom(
    atom.functor,
    list.map(atom.args, fn(arg) { apply_substitution(arg, subst) }),
    atom.pos,
  )
}

fn apply_substitution_to_guard(
  guard: ast.Guard,
  subst: Dict(String, ast.Term),
) -> ast.Guard {
  ast.Guard(
    guard.predicate,
    list.map(guard.args, fn(arg) { apply_substitution(arg, subst) }),
    guard.negated,
    guard.pos,
  )
}

/// Goal substitution — the variant divergence: the engine copy preserves
/// RemoteGoal/SpawnGoal; the analyzer copy rebuilds a plain Goal over the
/// uniform functor/args view ('#'/'@' with term-encoded payload).
fn apply_substitution_to_goal(
  goal: ast.Goal,
  subst: Dict(String, ast.Term),
  variant: Variant,
) -> ast.Goal {
  case variant.preserve_goal_kinds {
    True ->
      case goal {
        ast.RemoteGoal(module, inner, pos) ->
          ast.RemoteGoal(
            apply_substitution(module, subst),
            apply_substitution_to_goal(inner, subst, variant),
            pos,
          )
        ast.SpawnGoal(inner, agent_id, pos) ->
          ast.SpawnGoal(
            ast.InnerGoal(
              inner.functor,
              list.map(inner.args, fn(arg) { apply_substitution(arg, subst) }),
              inner.pos,
            ),
            agent_id,
            pos,
          )
        ast.Goal(functor, args, pos) ->
          ast.Goal(
            functor,
            list.map(args, fn(arg) { apply_substitution(arg, subst) }),
            pos,
          )
      }
    False ->
      ast.Goal(
        ast.goal_functor(goal),
        list.map(ast.goal_args(goal), fn(arg) { apply_substitution(arg, subst) }),
        ast.goal_pos(goal),
      )
  }
}
