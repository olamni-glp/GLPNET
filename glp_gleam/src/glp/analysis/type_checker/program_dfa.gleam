//// glp/analysis/type_checker/program_dfa — the program DFA (Yardeni-Shapiro
//// type automata) and leaf-consistency check (feature 050, T018).
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/program_dfa.dart
//// (spec: docs/modules/type-dfa.md v0.9; paper §4.1, Definition 4.3). Builds a
//// complement pair of automata (T and T?) for every defined type, a primitive
//// automaton for each system type, and a no-complement automaton for each
//// procedure; `check_leaf_consistency` decides whether a term leaf is admissible
//// at a DFA state (Definition 4.3, Mode Correspondence Property).
////
//// Port notes:
//// - Dart's `Mode.produce`/`Mode.consume` are mode.Output (↑) / mode.Input (↓).
//// - Dart's `DFAState.==`/`hashCode` compare ONLY baseName+isDual; because the
////   builder keeps exactly one canonical state per (baseName, isDual), Gleam's
////   full-record structural equality (used for the transition `Dict` keys) is
////   equivalent on every reachable state — `dual` preserves isFinal/isProcedure.
//// - Dart throws `UnknownTypeError`/`StateError`; those are invariant violations
////   (the checker validates type references upstream), so the port panics the
////   same way rather than threading a Result (matching type_conversion.gleam).

import gleam/dict.{type Dict}
import gleam/float
import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/set.{type Set}
import glp/analysis/type_ast.{
  type ConstValue, type ProcDecl, type TypeDef, type TypeEnvironment,
  type TypeExpr,
}
import glp/analysis/type_checker/mode.{type Mode, Input, Output}

// ============================================================================
// Core types
// ============================================================================

/// A state in the program DFA. `base_name` + `is_dual` identify the state;
/// `is_final` marks `_`/`_?`/`_FINAL_`; `is_procedure` distinguishes procedure
/// states (which have no complement).
pub type DfaState {
  DfaState(
    base_name: String,
    is_dual: Bool,
    is_final: Bool,
    is_procedure: Bool,
  )
}

/// A transition label: `symbol` (functor or constant value), `arity`
/// (0 for constants), 1-based `arg_index` (0 for constants), and the mode at
/// this position (`None` for constants and procedure-argument transitions).
pub type TransitionLabel {
  TransitionLabel(symbol: String, arity: Int, arg_index: Int, mode: Option(Mode))
}

/// The automaton for a single type (T or T?), or a procedure.
pub type Automaton {
  Automaton(
    start_state: DfaState,
    transitions: Dict(#(DfaState, TransitionLabel), DfaState),
    /// Primitive type names accepted as alternatives (e.g. {Integer, String}
    /// for `Constant ::= Integer ; String.`).
    accepted_primitives: Set(String),
  )
}

/// The complete DFA for a typed GLP program.
pub type ProgramDfa {
  ProgramDfa(states: Dict(String, DfaState), automata: Dict(String, Automaton))
}

// ============================================================================
// DfaState accessors (Dart getters)
// ============================================================================

/// Dart `DFAState.name`: `baseName?` if dual, else `baseName`.
pub fn state_name(state: DfaState) -> String {
  case state.is_dual {
    True -> state.base_name <> "?"
    False -> state.base_name
  }
}

/// Dart `DFAState.dual`: same state with `is_dual` flipped (isFinal/isProcedure
/// preserved).
pub fn state_dual(state: DfaState) -> DfaState {
  DfaState(state.base_name, !state.is_dual, state.is_final, state.is_procedure)
}

/// Dart `DFAState.toString()`.
pub fn state_to_string(state: DfaState) -> String {
  state_name(state)
}

/// True for `_` or `_?`.
pub fn is_wildcard(state: DfaState) -> Bool {
  state.base_name == "_"
}

/// True for `_` (produced wildcard, non-complement).
pub fn is_produced_wildcard(state: DfaState) -> Bool {
  state.base_name == "_" && !state.is_dual
}

/// True for `_?` (consumed wildcard, complement).
pub fn is_consumed_wildcard(state: DfaState) -> Bool {
  state.base_name == "_" && state.is_dual
}

pub fn is_integer_type(state: DfaState) -> Bool {
  state.base_name == "Integer"
}

pub fn is_real_type(state: DfaState) -> Bool {
  state.base_name == "Real"
}

pub fn is_number_type(state: DfaState) -> Bool {
  state.base_name == "Number"
}

pub fn is_string_type(state: DfaState) -> Bool {
  state.base_name == "String"
}

pub fn is_any_type(state: DfaState) -> Bool {
  state.base_name == "Any"
}

/// True for `_FINAL_` (anonymous final for constant/literal matches).
pub fn is_anonymous_final(state: DfaState) -> Bool {
  state.base_name == "_FINAL_"
}

/// True for numeric types: Integer, Real, Number.
pub fn is_numeric_type(state: DfaState) -> Bool {
  is_integer_type(state) || is_real_type(state) || is_number_type(state)
}

/// True for primitive types: _, Integer, Real, Number, String, Any.
pub fn is_primitive_type(state: DfaState) -> Bool {
  is_wildcard(state)
  || is_integer_type(state)
  || is_real_type(state)
  || is_number_type(state)
  || is_string_type(state)
  || is_any_type(state)
}

/// True for user-defined types (not primitive, not procedure, not anon final).
pub fn is_user_defined_type(state: DfaState) -> Bool {
  !is_primitive_type(state) && !state.is_procedure && !is_anonymous_final(state)
}

// ============================================================================
// TransitionLabel constructors/accessors (Dart factories + getters)
// ============================================================================

/// Dart `TransitionLabel.functor`.
pub fn transition_label_functor(
  name: String,
  arity: Int,
  arg_index: Int,
  mode: Option(Mode),
) -> TransitionLabel {
  TransitionLabel(name, arity, arg_index, mode)
}

/// Dart `TransitionLabel.constant(value)` — symbol is the Dart `value.toString()`,
/// which is BARE for strings (a Dart `String`'s `toString` is itself, unquoted).
/// Dart therefore conflates atom `x` and quoted string `'x'` (both become the
/// bare label `x`); a `CvString` leaf built from an atom (well_typed_term's
/// Bug-1 rule) must match a `CvAtom` alternative. This differs from
/// `type_ast.const_value_to_string`, which quote-wraps `CvString` for display,
/// so a dedicated bare renderer is used for label symbols.
pub fn transition_label_constant_value(value: ConstValue) -> TransitionLabel {
  TransitionLabel(const_label_symbol(value), 0, 0, None)
}

/// Bare constant-label symbol (Dart `Object.toString()`): unquoted for atoms and
/// strings alike.
fn const_label_symbol(value: ConstValue) -> String {
  case value {
    type_ast.CvAtom(name) -> name
    type_ast.CvString(s) -> s
    type_ast.CvInt(i) -> int.to_string(i)
    type_ast.CvReal(r) -> float.to_string(r)
  }
}

/// Dart `TransitionLabel.constant('[]')` — a constant label from a raw symbol.
pub fn transition_label_constant_symbol(symbol: String) -> TransitionLabel {
  TransitionLabel(symbol, 0, 0, None)
}

/// Dart `TransitionLabel.dual`: same label with the mode flipped.
pub fn transition_label_dual(label: TransitionLabel) -> TransitionLabel {
  TransitionLabel(
    label.symbol,
    label.arity,
    label.arg_index,
    option.map(label.mode, mode.flip),
  )
}

/// Dart `TransitionLabel.toString()`.
pub fn label_to_string(label: TransitionLabel) -> String {
  case label.arity {
    0 -> label.symbol
    _ -> {
      let mode_str = case label.mode {
        Some(m) ->
          ":"
          <> case m {
            Output -> "↑"
            Input -> "↓"
          }
        None -> ""
      }
      label.symbol
      <> "("
      <> int.to_string(label.arity)
      <> ","
      <> int.to_string(label.arg_index)
      <> ")"
      <> mode_str
    }
  }
}

// ============================================================================
// Automaton / ProgramDfa accessors (Dart getters)
// ============================================================================

/// Dart `Automaton.transition` — target state, or `None` if no such transition.
pub fn automaton_transition(
  automaton: Automaton,
  from: DfaState,
  label: TransitionLabel,
) -> Option(DfaState) {
  case dict.get(automaton.transitions, #(from, label)) {
    Ok(s) -> Some(s)
    Error(_) -> None
  }
}

/// Dart `Automaton.dual` — flip all states and modes.
pub fn automaton_dual(automaton: Automaton) -> Automaton {
  let new_transitions =
    dict.fold(automaton.transitions, dict.new(), fn(acc, key, to_state) {
      let #(from_state, label) = key
      dict.insert(
        acc,
        #(state_dual(from_state), transition_label_dual(label)),
        state_dual(to_state),
      )
    })
  Automaton(
    state_dual(automaton.start_state),
    new_transitions,
    automaton.accepted_primitives,
  )
}

/// Dart `ProgramDFA.getState` — panics (Dart StateError) if absent.
pub fn get_dfa_state(dfa: ProgramDfa, name: String) -> DfaState {
  case dict.get(dfa.states, name) {
    Ok(s) -> s
    Error(_) -> panic as { "State not found: " <> name }
  }
}

/// Dart `ProgramDFA.getAutomaton` — panics (Dart StateError) if absent.
pub fn get_automaton(dfa: ProgramDfa, type_name: String) -> Automaton {
  case dict.get(dfa.automata, type_name) {
    Ok(a) -> a
    Error(_) -> panic as { "Automaton not found: " <> type_name }
  }
}

// ============================================================================
// DFA construction (Dart buildProgramDFA and helpers)
// ============================================================================

/// Build the complete program DFA from the type environment
/// (Dart `buildProgramDFA`).
pub fn build_program_dfa(env: TypeEnvironment) -> ProgramDfa {
  // States for defined types (both T and T?), created BEFORE any automata so
  // cross-type references resolve.
  let states_with_types =
    list.fold(dict.to_list(env.types), system_states(), fn(acc, entry) {
      let #(type_name, _) = entry
      acc
      |> dict.insert(
        type_name,
        DfaState(type_name, is_dual: False, is_final: False, is_procedure: False),
      )
      |> dict.insert(
        type_name <> "?",
        DfaState(type_name, is_dual: True, is_final: False, is_procedure: False),
      )
    })
  // Procedure states (no complement).
  let states =
    list.fold(dict.to_list(env.procedures), states_with_types, fn(acc, entry) {
      let #(proc_key, _) = entry
      dict.insert(
        acc,
        proc_key,
        DfaState(proc_key, is_dual: False, is_final: False, is_procedure: True),
      )
    })

  // Automata for defined types (T with declared modes, T? with all flipped).
  let automata_with_types =
    list.fold(dict.to_list(env.types), system_automata(states), fn(acc, entry) {
      let #(type_name, type_def) = entry
      acc
      |> dict.insert(type_name, build_type_automaton(type_def, states, False))
      |> dict.insert(
        type_name <> "?",
        build_type_automaton(type_def, states, True),
      )
    })
  // Procedure automata.
  let automata =
    list.fold(
      dict.to_list(env.procedures),
      automata_with_types,
      fn(acc, entry) {
        let #(proc_key, proc_decl) = entry
        dict.insert(acc, proc_key, build_procedure_automaton(proc_decl, states))
      },
    )

  ProgramDfa(states, automata)
}

/// The 13 system states (complement pairs plus the anonymous final).
fn system_states() -> Dict(String, DfaState) {
  dict.new()
  |> dict.insert("_", DfaState("_", is_dual: False, is_final: True, is_procedure: False))
  |> dict.insert("_?", DfaState("_", is_dual: True, is_final: True, is_procedure: False))
  |> dict.insert("Integer", DfaState("Integer", is_dual: False, is_final: False, is_procedure: False))
  |> dict.insert("Integer?", DfaState("Integer", is_dual: True, is_final: False, is_procedure: False))
  |> dict.insert("Real", DfaState("Real", is_dual: False, is_final: False, is_procedure: False))
  |> dict.insert("Real?", DfaState("Real", is_dual: True, is_final: False, is_procedure: False))
  |> dict.insert("Number", DfaState("Number", is_dual: False, is_final: False, is_procedure: False))
  |> dict.insert("Number?", DfaState("Number", is_dual: True, is_final: False, is_procedure: False))
  |> dict.insert("String", DfaState("String", is_dual: False, is_final: False, is_procedure: False))
  |> dict.insert("String?", DfaState("String", is_dual: True, is_final: False, is_procedure: False))
  |> dict.insert("Any", DfaState("Any", is_dual: False, is_final: False, is_procedure: False))
  |> dict.insert("Any?", DfaState("Any", is_dual: True, is_final: False, is_procedure: False))
  |> dict.insert("_FINAL_", DfaState("_FINAL_", is_dual: False, is_final: True, is_procedure: False))
}

/// System automata: `_`/`_?` are final (no transitions); the primitive types
/// carry no explicit transitions (handled in leaf consistency). `_FINAL_` has a
/// state but no automaton (matches Dart).
fn system_automata(states: Dict(String, DfaState)) -> Dict(String, Automaton) {
  dict.new()
  |> dict.insert("_", final_automaton(get_state(states, "_")))
  |> dict.insert("_?", final_automaton(get_state(states, "_?")))
  |> dict.insert("Integer", primitive_type_automaton(get_state(states, "Integer")))
  |> dict.insert("Integer?", primitive_type_automaton(get_state(states, "Integer?")))
  |> dict.insert("Real", primitive_type_automaton(get_state(states, "Real")))
  |> dict.insert("Real?", primitive_type_automaton(get_state(states, "Real?")))
  |> dict.insert("Number", primitive_type_automaton(get_state(states, "Number")))
  |> dict.insert("Number?", primitive_type_automaton(get_state(states, "Number?")))
  |> dict.insert("String", primitive_type_automaton(get_state(states, "String")))
  |> dict.insert("String?", primitive_type_automaton(get_state(states, "String?")))
  |> dict.insert("Any", primitive_type_automaton(get_state(states, "Any")))
  |> dict.insert("Any?", primitive_type_automaton(get_state(states, "Any?")))
}

/// A final automaton (no transitions).
fn final_automaton(state: DfaState) -> Automaton {
  Automaton(state, dict.new(), set.new())
}

/// A primitive type automaton (no explicit transitions — leaf consistency
/// handles constants specially).
fn primitive_type_automaton(state: DfaState) -> Automaton {
  Automaton(state, dict.new(), set.new())
}

/// Build an automaton for a type definition (Dart `_buildTypeAutomaton`).
fn build_type_automaton(
  type_def: TypeDef,
  states: Dict(String, DfaState),
  is_dual: Bool,
) -> Automaton {
  let start_state_name = case is_dual {
    True -> type_def.name <> "?"
    False -> type_def.name
  }
  let start_state = get_state(states, start_state_name)

  let #(transitions, accepted_primitives) =
    list.fold(
      type_def.alternatives,
      #(dict.new(), set.new()),
      fn(acc, alt) {
        let #(transitions, primitives) = acc
        // Collect primitive-type alternatives.
        let primitives = case alt {
          type_ast.TypeRef(name, _, _, _) ->
            case name {
              "Integer" | "Real" | "Number" | "String" | "Any" ->
                set.insert(primitives, name)
              _ -> primitives
            }
          _ -> primitives
        }
        let transitions =
          add_type_transitions(
            start_state,
            alt,
            Output,
            states,
            transitions,
            is_dual,
          )
        #(transitions, primitives)
      },
    )

  Automaton(start_state, transitions, accepted_primitives)
}

/// Add transitions from a type alternative (Dart `_addTypeTransitions`).
/// `TypeRef`/`PrimitiveModeAlt` alternatives contribute no transitions (they are
/// leaves resolved via `resolve_type_expr`).
fn add_type_transitions(
  from_state: DfaState,
  alt: TypeExpr,
  context_mode: Mode,
  states: Dict(String, DfaState),
  transitions: Dict(#(DfaState, TransitionLabel), DfaState),
  is_dual: Bool,
) -> Dict(#(DfaState, TransitionLabel), DfaState) {
  case alt {
    type_ast.ConstantAlt(value, _) -> {
      let label = transition_label_constant_value(value)
      dict.insert(transitions, #(from_state, label), get_state(states, "_FINAL_"))
    }
    type_ast.ListNilAlt(_) -> {
      let label = transition_label_constant_symbol("[]")
      dict.insert(transitions, #(from_state, label), get_state(states, "_FINAL_"))
    }
    type_ast.ListConsAlt(head, tail, _) -> {
      let head_mode = maybe_flip(mode_of(head, context_mode), is_dual)
      let tail_mode = maybe_flip(mode_of(tail, context_mode), is_dual)
      let head_label = transition_label_functor("[|]", 2, 1, Some(head_mode))
      let tail_label = transition_label_functor("[|]", 2, 2, Some(tail_mode))
      transitions
      |> dict.insert(
        #(from_state, head_label),
        resolve_type_expr(head, states, is_dual),
      )
      |> dict.insert(
        #(from_state, tail_label),
        resolve_type_expr(tail, states, is_dual),
      )
    }
    type_ast.StructAlt(functor, args, _) -> {
      let arity = list.length(args)
      list.index_fold(args, transitions, fn(acc, arg_type, i) {
        let arg_mode = maybe_flip(mode_of(arg_type, context_mode), is_dual)
        let label = transition_label_functor(functor, arity, i + 1, Some(arg_mode))
        dict.insert(acc, #(from_state, label), resolve_type_expr(
          arg_type,
          states,
          is_dual,
        ))
      })
    }
    type_ast.DiffListAlt(content, hole, _) -> {
      let content_mode = maybe_flip(mode_of(content, context_mode), is_dual)
      let hole_mode = maybe_flip(mode_of(hole, context_mode), is_dual)
      let content_label = transition_label_functor("\\", 2, 1, Some(content_mode))
      let hole_label = transition_label_functor("\\", 2, 2, Some(hole_mode))
      transitions
      |> dict.insert(
        #(from_state, content_label),
        resolve_type_expr(content, states, is_dual),
      )
      |> dict.insert(
        #(from_state, hole_label),
        resolve_type_expr(hole, states, is_dual),
      )
    }
    // TypeRef / PrimitiveModeAlt as a bare alternative: no transitions.
    _ -> transitions
  }
}

/// Apply the complement flip to a mode when building the dual automaton.
fn maybe_flip(m: Mode, is_dual: Bool) -> Mode {
  case is_dual {
    True -> mode.flip(m)
    False -> m
  }
}

/// Compute the mode for a type expression at a context mode (Dart `_modeOf`):
/// `T?` flips the mode, `T` keeps it (before complement is applied).
fn mode_of(expr: TypeExpr, context_mode: Mode) -> Mode {
  case expr {
    type_ast.TypeRef(_, True, _, _) -> mode.flip(context_mode)
    type_ast.PrimitiveModeAlt(True, _) -> mode.flip(context_mode)
    _ -> context_mode
  }
}

/// Resolve a type expression to a DFA state (Dart `_resolveTypeExpr`) — XOR of
/// the reference's own complement with the automaton complement flag.
fn resolve_type_expr(
  expr: TypeExpr,
  states: Dict(String, DfaState),
  is_dual: Bool,
) -> DfaState {
  case expr {
    type_ast.PrimitiveModeAlt(is_input, _) -> {
      let final_is_complement = is_input != is_dual
      case final_is_complement {
        True -> get_state(states, "_?")
        False -> get_state(states, "_")
      }
    }
    type_ast.TypeRef(name, is_input, _, _) -> {
      let final_is_complement = is_input != is_dual
      case name {
        "Integer" -> primitive_state(states, "Integer", final_is_complement)
        "Real" -> primitive_state(states, "Real", final_is_complement)
        "Number" -> primitive_state(states, "Number", final_is_complement)
        "String" -> primitive_state(states, "String", final_is_complement)
        "Any" -> primitive_state(states, "Any", final_is_complement)
        _ -> {
          let target_name = case final_is_complement {
            True -> name <> "?"
            False -> name
          }
          case dict.get(states, target_name) {
            Ok(s) -> s
            Error(_) -> panic as { "UnknownTypeError: " <> name }
          }
        }
      }
    }
    _ -> panic as "program_dfa: cannot resolve type expression"
  }
}

fn primitive_state(
  states: Dict(String, DfaState),
  name: String,
  is_complement: Bool,
) -> DfaState {
  case is_complement {
    True -> get_state(states, name <> "?")
    False -> get_state(states, name)
  }
}

/// Build an automaton for a procedure declaration (Dart
/// `_buildProcedureAutomaton`). Argument transitions have no mode.
fn build_procedure_automaton(
  proc_decl: ProcDecl,
  states: Dict(String, DfaState),
) -> Automaton {
  let proc_state = get_state(states, type_ast.qualified_key(proc_decl))
  let arity = type_ast.arity(proc_decl)
  let transitions =
    list.index_fold(proc_decl.arg_types, dict.new(), fn(acc, arg_type, i) {
      let label = transition_label_functor(proc_decl.name, arity, i + 1, None)
      let target_name = get_full_type_name(arg_type)
      let target = case dict.get(states, target_name) {
        Ok(s) -> s
        Error(_) -> panic as { "UnknownTypeError: " <> target_name }
      }
      dict.insert(acc, #(proc_state, label), target)
    })
  Automaton(proc_state, transitions, set.new())
}

/// The full type name including `?` for input mode (Dart `_getFullTypeName`).
fn get_full_type_name(expr: TypeExpr) -> String {
  case expr {
    type_ast.PrimitiveModeAlt(True, _) -> "_?"
    type_ast.PrimitiveModeAlt(False, _) -> "_"
    type_ast.TypeRef(name, True, _, _) -> name <> "?"
    type_ast.TypeRef(name, False, _, _) -> name
    _ -> panic as "program_dfa: cannot get type name"
  }
}

/// Look up a state that must exist (Dart `states[...]!`).
fn get_state(states: Dict(String, DfaState), name: String) -> DfaState {
  case dict.get(states, name) {
    Ok(s) -> s
    Error(_) -> panic as { "program_dfa: state not found: " <> name }
  }
}

// ============================================================================
// Leaf consistency (Definition 4.3)
// ============================================================================

/// A leaf term in a term path (Dart `LeafTerm`).
pub type LeafTerm {
  LeafTerm(
    name: Option(String),
    is_variable: Bool,
    is_reader: Bool,
    mode: Option(Mode),
    value: Option(ConstValue),
    is_integer: Bool,
    is_real: Bool,
    is_string: Bool,
  )
}

/// Dart `LeafTerm.writer`.
pub fn leaf_writer(name: String, mode: Mode) -> LeafTerm {
  LeafTerm(
    name: Some(name),
    is_variable: True,
    is_reader: False,
    mode: Some(mode),
    value: None,
    is_integer: False,
    is_real: False,
    is_string: False,
  )
}

/// Dart `LeafTerm.reader`.
pub fn leaf_reader(name: String, mode: Mode) -> LeafTerm {
  LeafTerm(
    name: Some(name),
    is_variable: True,
    is_reader: True,
    mode: Some(mode),
    value: None,
    is_integer: False,
    is_real: False,
    is_string: False,
  )
}

/// Dart `LeafTerm.integerConstant`.
pub fn leaf_integer_constant(value: Int, mode: Option(Mode)) -> LeafTerm {
  LeafTerm(
    name: None,
    is_variable: False,
    is_reader: False,
    mode: mode,
    value: Some(type_ast.CvInt(value)),
    is_integer: True,
    is_real: False,
    is_string: False,
  )
}

/// Dart `LeafTerm.realConstant`.
pub fn leaf_real_constant(value: Float, mode: Option(Mode)) -> LeafTerm {
  LeafTerm(
    name: None,
    is_variable: False,
    is_reader: False,
    mode: mode,
    value: Some(type_ast.CvReal(value)),
    is_integer: False,
    is_real: True,
    is_string: False,
  )
}

/// Dart `LeafTerm.stringConstant`.
pub fn leaf_string_constant(value: String, mode: Option(Mode)) -> LeafTerm {
  LeafTerm(
    name: None,
    is_variable: False,
    is_reader: False,
    mode: mode,
    value: Some(type_ast.CvString(value)),
    is_integer: False,
    is_real: False,
    is_string: True,
  )
}

/// Dart `LeafTerm.constant` (atom or other).
pub fn leaf_constant(value: ConstValue, mode: Option(Mode)) -> LeafTerm {
  LeafTerm(
    name: None,
    is_variable: False,
    is_reader: False,
    mode: mode,
    value: Some(value),
    is_integer: False,
    is_real: False,
    is_string: False,
  )
}

/// Result of checking leaf consistency (Dart `LeafConsistencyResult`).
pub type LeafConsistencyResult {
  LeafConsistent(type_: Option(DfaState))
  LeafInconsistent(reason: String)
}

/// Check leaf consistency per Definition 4.3 (Dart `checkLeafConsistency`).
pub fn check_leaf_consistency(
  leaf: LeafTerm,
  state: DfaState,
  dfa: ProgramDfa,
) -> LeafConsistencyResult {
  case leaf.is_variable {
    // Case 1: variable leaf — path mode must match the variable's implicit mode.
    True ->
      case leaf.is_reader, leaf.mode {
        True, Some(Input) -> LeafConsistent(Some(state))
        False, Some(Output) -> LeafConsistent(Some(state))
        _, _ -> {
          let expected = case leaf.is_reader {
            True -> "↓ (consume)"
            False -> "↑ (produce)"
          }
          let actual = case leaf.mode {
            Some(Input) -> "↓ (consume)"
            _ -> "↑ (produce)"
          }
          let who = case leaf.is_reader {
            True -> "reader"
            False -> "writer"
          }
          LeafInconsistent(
            "Variable mode mismatch: "
            <> who
            <> " requires "
            <> expected
            <> ", got "
            <> actual,
          )
        }
      }
    // Case 2: constant leaf — check the type accepts this constant.
    False -> check_constant_leaf(leaf, state, dfa)
  }
}

fn check_constant_leaf(
  leaf: LeafTerm,
  state: DfaState,
  dfa: ProgramDfa,
) -> LeafConsistencyResult {
  case state.base_name, state.is_dual {
    // Case 2a: anonymous final (already matched a constant transition).
    "_FINAL_", _ -> LeafConsistent(Some(state))
    // Case 2b: primitive type states.
    "Integer", _ ->
      case leaf.is_integer {
        True -> LeafConsistent(final_state(dfa))
        False -> LeafInconsistent("Integer type requires integer literal")
      }
    "Real", _ ->
      case leaf.is_real {
        True -> LeafConsistent(final_state(dfa))
        False -> LeafInconsistent("Real type requires real literal")
      }
    "Number", _ ->
      case leaf.is_integer || leaf.is_real {
        True -> LeafConsistent(final_state(dfa))
        False -> LeafInconsistent("Number type requires numeric literal")
      }
    "String", _ ->
      case leaf.is_string {
        True -> LeafConsistent(final_state(dfa))
        False -> LeafInconsistent("String type requires string literal")
      }
    "Any", _ -> LeafConsistent(final_state(dfa))
    // Case 2c: wildcard states.
    "_", False ->
      case leaf.mode == Some(Output) {
        True -> LeafConsistent(Some(state))
        False ->
          LeafInconsistent("_ expects produced term (mode ↑), got consumed term")
      }
    "_", True ->
      case leaf.mode == Some(Input) {
        True -> LeafConsistent(Some(state))
        False ->
          LeafInconsistent("_? expects consumed term (mode ↓), got produced term")
      }
    // Case 2d: user-defined type state.
    _, _ -> check_user_defined_constant(leaf, state, dfa)
  }
}

fn check_user_defined_constant(
  leaf: LeafTerm,
  state: DfaState,
  dfa: ProgramDfa,
) -> LeafConsistencyResult {
  case leaf.value {
    Some(v) ->
      case dict.get(dfa.automata, state_name(state)) {
        Ok(automaton) -> {
          // First, try an explicit constant transition.
          let const_label = transition_label_constant_value(v)
          case automaton_transition(automaton, state, const_label) {
            Some(next_state) -> LeafConsistent(Some(next_state))
            None ->
              // Second, check accepted primitive alternatives.
              case accepted_primitive_match(leaf, automaton, dfa) {
                Some(final) -> LeafConsistent(Some(final))
                None -> user_defined_fail(state)
              }
          }
        }
        Error(_) -> user_defined_fail(state)
      }
    None -> user_defined_fail(state)
  }
}

/// If the type accepts a primitive matching this constant leaf, return the
/// `_FINAL_` state (Dart's `acceptedPrimitives` block).
fn accepted_primitive_match(
  leaf: LeafTerm,
  automaton: Automaton,
  dfa: ProgramDfa,
) -> Option(DfaState) {
  let primitives = automaton.accepted_primitives
  let matches =
    { leaf.is_integer && { set.contains(primitives, "Integer") || set.contains(primitives, "Number") } }
    || { leaf.is_real && { set.contains(primitives, "Real") || set.contains(primitives, "Number") } }
    || { leaf.is_string && set.contains(primitives, "String") }
    || set.contains(primitives, "Any")
  case matches {
    True -> final_state(dfa)
    False -> None
  }
}

fn user_defined_fail(state: DfaState) -> LeafConsistencyResult {
  LeafInconsistent(
    "Constant does not match any alternative at type position "
    <> state_name(state),
  )
}

/// The `_FINAL_` state as an `Option` (Dart passes `dfa.states['_FINAL_']`,
/// which always exists after construction).
fn final_state(dfa: ProgramDfa) -> Option(DfaState) {
  case dict.get(dfa.states, "_FINAL_") {
    Ok(s) -> Some(s)
    Error(_) -> None
  }
}
