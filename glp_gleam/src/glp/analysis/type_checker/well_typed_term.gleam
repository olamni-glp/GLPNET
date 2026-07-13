//// glp/analysis/type_checker/well_typed_term — well-typed moded term checking
//// for the GLP type system (feature 050, T018).
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/well_typed_term.dart
//// (spec: docs/type system/well-typed-term.md v0.7; paper Definition 5.4). A
//// moded term is well-typed by an automaton when (1) every term path has a
//// consistent path in the automaton and (2) every variable pair (X, X?) is dual.
////
//// Port notes:
////   * Dart's `Mode.produce`/`Mode.consume` are mode.Output (↑) / mode.Input (↓).
////   * Dart's `variableTypes` is a `LinkedHashMap` iterated in insertion order,
////     and `type_checker.dart` emits every clause error IN ORDER, so ordering is
////     user-visible. Gleam `dict` is unordered; the port therefore represents the
////     variable-type map as an insertion-ordered assoc-list and uses
////     `moded_term.paths_ordered` (DFS order) so diagnostics come out
////     byte-identical to the Dart oracle.
////   * Dart's abstract `WellTypedError` with three subclasses becomes one sum
////     type; `error_message/1` reproduces each `.message` getter verbatim.
////   * Dart re-parses a leaf's constant kind from its symbol string
////     (`int.tryParse`/`double.tryParse`); the port mirrors that with
////     `int.parse`/`float.parse` (which, like the round-tripped rendering, sees
////     integers without a dot and reals with one).
////   * Dart's `dfa.getAutomaton(...)` inside a try/catch is a genuine recoverable
////     path (not an invariant), so the port looks it up in `dfa.automata`
////     directly rather than via the panicking `program_dfa.get_automaton`.

import gleam/dict
import gleam/float
import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/string
import glp/analysis/type_checker/mode.{type Mode, Input, Output}
import glp/analysis/type_checker/moded_term.{type ModedPath, type ModedTerm}
import glp/analysis/type_checker/program_dfa.{
  type Automaton, type DfaState, type LeafTerm, type ProgramDfa,
  type TransitionLabel,
}

// =============================================================================
// Result types
// =============================================================================

/// Type information for a variable occurrence (Dart `VariableTypeInfo`).
pub type VariableTypeInfo {
  VariableTypeInfo(
    /// The DFA state where this variable appears (includes is_dual).
    type_state: DfaState,
    /// The mode at this position (consume ↓ = Input for readers, produce ↑ =
    /// Output for writers).
    mode: Mode,
    is_reader: Bool,
  )
}

/// A well-typing error (Dart abstract `WellTypedError` and its subclasses).
pub type WellTypedError {
  /// Path has no matching automaton transition (Dart `InconsistentPathError`).
  InconsistentPathError(path: ModedPath, reason: String)
  /// Same variable has different types at different occurrences (Dart
  /// `InconsistentVariableError`).
  InconsistentVariableError(
    variable_name: String,
    first_occurrence: VariableTypeInfo,
    second_occurrence: VariableTypeInfo,
  )
  /// Variable pair (X, X?) not dual (Dart `NonDualError`).
  NonDualError(
    base_name: String,
    writer_type: Option(VariableTypeInfo),
    reader_type: Option(VariableTypeInfo),
    reason: Option(String),
  )
}

/// Result of checking if a moded term is well-typed (Dart `WellTypedResult`).
/// `variable_types` is an insertion-ordered assoc-list keyed by "X"/"X?".
pub type WellTypedResult {
  WellTypedResult(
    is_well_typed: Bool,
    variable_types: List(#(String, VariableTypeInfo)),
    errors: List(WellTypedError),
  )
}

/// Dart `WellTypedResult.success`.
pub fn well_typed_success(
  variable_types: List(#(String, VariableTypeInfo)),
) -> WellTypedResult {
  WellTypedResult(True, variable_types, [])
}

/// Dart `WellTypedResult.failure` (variable types default to empty).
pub fn well_typed_failure(errors: List(WellTypedError)) -> WellTypedResult {
  WellTypedResult(False, [], errors)
}

/// Result of checking a single path against the automaton (Dart
/// `PathCheckResult`).
pub type PathCheckResult {
  PathConsistent(variable_assignment: Option(VariableTypeInfo))
  PathInconsistent(reason: String)
}

// =============================================================================
// Rendering (Dart toString / .message getters)
// =============================================================================

/// Dart `VariableTypeInfo.toString()`.
pub fn variable_type_info_to_string(info: VariableTypeInfo) -> String {
  "(" <> program_dfa.state_name(info.type_state) <> ", " <> glyph(info.mode) <> ")"
}

/// Dart glyph for a mode: consume (↓ = Input) / produce (↑ = Output).
fn glyph(mode: Mode) -> String {
  case mode {
    Input -> "↓"
    Output -> "↑"
  }
}

/// The `.message` getter of a `WellTypedError` (Dart, verbatim).
pub fn error_message(error: WellTypedError) -> String {
  case error {
    InconsistentPathError(path, reason) ->
      "Inconsistent path: " <> reason <> "\n  Path: " <> moded_term.path_to_string(path)
    InconsistentVariableError(variable_name, first, second) ->
      "Variable "
      <> variable_name
      <> " has inconsistent types: "
      <> variable_type_info_to_string(first)
      <> " vs "
      <> variable_type_info_to_string(second)
    NonDualError(base_name, writer_type, reader_type, reason) -> {
      let reason_str = case reason {
        Some(r) -> ": " <> r
        None -> ""
      }
      "Variable pair ("
      <> base_name
      <> ", "
      <> base_name
      <> "?) not dual"
      <> reason_str
      <> ": writer="
      <> optional_info(writer_type)
      <> ", reader="
      <> optional_info(reader_type)
    }
  }
}

/// Dart string interpolation of a nullable `VariableTypeInfo` (null → "null").
fn optional_info(info: Option(VariableTypeInfo)) -> String {
  case info {
    Some(i) -> variable_type_info_to_string(i)
    None -> "null"
  }
}

// =============================================================================
// Public functions
// =============================================================================

/// Check if a moded term is well-typed by an automaton (Dart `checkModedTerm`).
pub fn check_moded_term(
  term: ModedTerm,
  automaton: Automaton,
  dfa: ProgramDfa,
) -> WellTypedResult {
  let term_paths = moded_term.paths_ordered(term)

  // Step 1: extract and check all paths, recording variable types in order.
  let initial: #(List(WellTypedError), List(#(String, VariableTypeInfo))) =
    #([], [])
  let #(path_errors, variable_types) =
    list.fold(term_paths, initial, fn(acc, path) {
      let #(errors, vts) = acc
      case check_path_against_automaton(path, automaton, dfa) {
        PathInconsistent(reason) -> #(
          list.append(errors, [InconsistentPathError(path, reason)]),
          vts,
        )
        PathConsistent(Some(assignment)) -> {
          let leaf = moded_term.path_leaf(path)
          let var_key = leaf.symbol
          case list.key_find(vts, var_key) {
            Ok(existing) ->
              // Same variable appears multiple times — types must match.
              case
                program_dfa.state_name(existing.type_state)
                != program_dfa.state_name(assignment.type_state)
              {
                True -> #(
                  list.append(errors, [
                    InconsistentVariableError(var_key, existing, assignment),
                  ]),
                  vts,
                )
                False -> #(errors, vts)
              }
            Error(_) -> #(errors, list.append(vts, [#(var_key, assignment)]))
          }
        }
        PathConsistent(None) -> #(errors, vts)
      }
    })

  // Step 2: check variable pair duality.
  let duality_errors = check_duality(variable_types)
  let all_errors = list.append(path_errors, duality_errors)

  WellTypedResult(list.is_empty(all_errors), variable_types, all_errors)
}

/// Check if a single moded path is consistent with the automaton (Dart
/// `checkPathAgainstAutomaton`). Switches automata at user-defined type
/// boundaries (Fix 4.1).
pub fn check_path_against_automaton(
  path: ModedPath,
  automaton: Automaton,
  dfa: ProgramDfa,
) -> PathCheckResult {
  // A single-step path is just a leaf at the start state; the recursion handles
  // both this and the traversal case uniformly.
  traverse(path.steps, automaton.start_state, automaton, dfa)
}

// =============================================================================
// Internal functions
// =============================================================================

/// Traverse the remaining path steps (Dart's `for` loop, plus the final
/// leaf-consistency check). `state`/`current_automaton` are the loop's mutable
/// locals, threaded through the recursion.
fn traverse(
  steps: List(moded_term.PathStep),
  state: DfaState,
  current_automaton: Automaton,
  dfa: ProgramDfa,
) -> PathCheckResult {
  case steps {
    // All transitions consumed: check the leaf against the final state.
    [leaf] -> check_leaf_consistency_for_path(leaf, state, dfa)
    [step, next_step, ..rest] -> {
      let label = build_transition_label(step, next_step)
      case program_dfa.automaton_transition(current_automaton, state, label) {
        None -> no_transition(state, next_step, label)
        Some(next_state) ->
          // Fix 4.1: switch automata when entering a user-defined type.
          case
            program_dfa.is_user_defined_type(next_state)
            && next_state.base_name != state.base_name
          {
            True ->
              case dict.get(dfa.automata, program_dfa.state_name(next_state)) {
                Ok(next_automaton) ->
                  traverse([next_step, ..rest], next_state, next_automaton, dfa)
                Error(_) ->
                  PathInconsistent(
                    "Cannot get automaton for type "
                    <> program_dfa.state_name(next_state),
                  )
              }
            False -> traverse([next_step, ..rest], next_state, current_automaton, dfa)
          }
      }
    }
    [] -> panic as "well_typed_term: empty path"
  }
}

/// No transition for the label (Dart's `nextState == null` branch): a wildcard
/// or Any state accepts the whole subterm if the structural mode matches.
fn no_transition(
  state: DfaState,
  next_step: moded_term.PathStep,
  label: TransitionLabel,
) -> PathCheckResult {
  case program_dfa.is_wildcard(state) || program_dfa.is_any_type(state) {
    True -> {
      let structural_mode_at_wildcard = next_step.mode
      let expected_mode = case state.is_dual {
        True -> Input
        False -> Output
      }
      case structural_mode_at_wildcard == expected_mode {
        True -> PathConsistent(None)
        False -> {
          let kind = case program_dfa.is_any_type(state) {
            True -> "Any"
            False -> "wildcard"
          }
          PathInconsistent(
            "Mode mismatch at "
            <> kind
            <> " "
            <> program_dfa.state_name(state)
            <> ": expected "
            <> glyph(expected_mode)
            <> ", got "
            <> glyph(structural_mode_at_wildcard),
          )
        }
      }
    }
    False ->
      PathInconsistent(
        "No transition for "
        <> program_dfa.label_to_string(label)
        <> " from state "
        <> program_dfa.state_name(state),
      )
  }
}

/// Build a transition label from two consecutive path steps (Dart
/// `_buildTransitionLabel`).
fn build_transition_label(
  current_step: moded_term.PathStep,
  next_step: moded_term.PathStep,
) -> TransitionLabel {
  case string.split(current_step.symbol, "/") {
    [functor, arity_str] -> {
      let arity = case int.parse(arity_str) {
        Ok(a) -> a
        Error(_) -> 0
      }
      program_dfa.transition_label_functor(
        functor,
        arity,
        next_step.arg_index,
        Some(next_step.mode),
      )
    }
    // Leaf node or malformed symbol — use the whole symbol, arity 0.
    _ ->
      program_dfa.transition_label_functor(
        current_step.symbol,
        0,
        next_step.arg_index,
        Some(next_step.mode),
      )
  }
}

/// Check leaf consistency with a DFA state (Dart
/// `_checkLeafConsistencyForPath`).
fn check_leaf_consistency_for_path(
  leaf: moded_term.PathStep,
  state: DfaState,
  dfa: ProgramDfa,
) -> PathCheckResult {
  let leaf_term = path_step_to_leaf_term(leaf)
  case program_dfa.check_leaf_consistency(leaf_term, state, dfa) {
    program_dfa.LeafConsistent(type_) ->
      case leaf.is_variable {
        True -> {
          let is_reader = leaf.is_reader
          let m = case is_reader {
            True -> Input
            False -> Output
          }
          PathConsistent(
            Some(VariableTypeInfo(
              type_state: option.unwrap(type_, state),
              mode: m,
              is_reader: is_reader,
            )),
          )
        }
        False -> PathConsistent(None)
      }
    program_dfa.LeafInconsistent(reason) -> PathInconsistent(reason)
  }
}

/// Convert a path step to a leaf term for `check_leaf_consistency` (Dart
/// `_pathStepToLeafTerm`).
fn path_step_to_leaf_term(step: moded_term.PathStep) -> LeafTerm {
  case step.is_variable {
    True ->
      case step.is_reader {
        True -> program_dfa.leaf_reader(step.symbol, step.mode)
        False -> program_dfa.leaf_writer(step.symbol, step.mode)
      }
    False -> {
      let value = step.symbol
      // Integer first (no decimal point), then real, then string/atom.
      case int.parse(value) {
        Ok(i) -> program_dfa.leaf_integer_constant(i, Some(step.mode))
        Error(_) ->
          case float.parse(value) {
            Ok(f) -> program_dfa.leaf_real_constant(f, Some(step.mode))
            Error(_) ->
              case is_quoted(value) {
                True ->
                  program_dfa.leaf_string_constant(
                    strip_quotes(value),
                    Some(step.mode),
                  )
                // Atoms (unquoted identifiers) are strings per paper §4.1.
                False -> program_dfa.leaf_string_constant(value, Some(step.mode))
              }
          }
      }
    }
  }
}

fn is_quoted(value: String) -> Bool {
  { string.starts_with(value, "'") && string.ends_with(value, "'") }
  || { string.starts_with(value, "\"") && string.ends_with(value, "\"") }
}

fn strip_quotes(value: String) -> String {
  string.slice(value, 1, string.length(value) - 2)
}

// =============================================================================
// Duality checking (Dart `_checkDuality`)
// =============================================================================

/// Check duality of variable pairs (Dart `_checkDuality`). Groups by base name
/// (X and X? share base "X"), preserving insertion order.
fn check_duality(
  variable_types: List(#(String, VariableTypeInfo)),
) -> List(WellTypedError) {
  variable_types
  |> group_by_base
  |> list.flat_map(fn(entry) {
    let #(base_name, variants) = entry
    let writer_key = base_name
    let reader_key = base_name <> "?"
    case list.key_find(variants, writer_key), list.key_find(variants, reader_key) {
      Ok(writer_info), Ok(reader_info) ->
        duality_for_pair(base_name, writer_info, reader_info)
      _, _ -> []
    }
  })
}

/// Group variable types by base name, preserving first-encounter order for both
/// the base names and the variants within each.
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

/// The Dart if/continue chain for one (writer, reader) pair — returns the single
/// error (as a one-element list) or the empty list when dual.
fn duality_for_pair(
  base_name: String,
  writer_info: VariableTypeInfo,
  reader_info: VariableTypeInfo,
) -> List(WellTypedError) {
  case writer_info.mode != Output {
    True -> [
      NonDualError(
        base_name,
        Some(writer_info),
        Some(reader_info),
        Some("Writer must have produce mode"),
      ),
    ]
    False ->
      case reader_info.mode != Input {
        True -> [
          NonDualError(
            base_name,
            Some(writer_info),
            Some(reader_info),
            Some("Reader must have consume mode"),
          ),
        ]
        False -> {
          let writer_is_wildcard = writer_info.type_state.base_name == "_"
          let reader_is_wildcard = reader_info.type_state.base_name == "_"
          case writer_is_wildcard || reader_is_wildcard {
            True ->
              case writer_is_wildcard && writer_info.type_state.is_dual {
                True -> [
                  NonDualError(
                    base_name,
                    Some(writer_info),
                    Some(reader_info),
                    Some("Writer wildcard must be _ (non-dual), not _?"),
                  ),
                ]
                False ->
                  case reader_is_wildcard && !reader_info.type_state.is_dual {
                    True -> [
                      NonDualError(
                        base_name,
                        Some(writer_info),
                        Some(reader_info),
                        Some("Reader wildcard must be _? (dual), not _"),
                      ),
                    ]
                    // Wildcards are dual to anything.
                    False -> []
                  }
              }
            False ->
              case
                writer_info.type_state.base_name != reader_info.type_state.base_name
              {
                True -> [
                  NonDualError(
                    base_name,
                    Some(writer_info),
                    Some(reader_info),
                    Some(
                      "Types must have same base: "
                      <> program_dfa.state_name(writer_info.type_state)
                      <> " vs "
                      <> program_dfa.state_name(reader_info.type_state),
                    ),
                  ),
                ]
                False ->
                  case
                    writer_info.type_state.is_dual == reader_info.type_state.is_dual
                  {
                    True -> [
                      NonDualError(
                        base_name,
                        Some(writer_info),
                        Some(reader_info),
                        Some(
                          "One must be dual, other not: "
                          <> program_dfa.state_name(writer_info.type_state)
                          <> " vs "
                          <> program_dfa.state_name(reader_info.type_state),
                        ),
                      ),
                    ]
                    False -> []
                  }
              }
          }
        }
      }
  }
}
