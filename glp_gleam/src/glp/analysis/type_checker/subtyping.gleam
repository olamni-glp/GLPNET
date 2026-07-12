//// glp/analysis/type_checker/subtyping — subtyping for GLP output types
//// (feature 050, T018).
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/subtyping.dart
//// (spec: docs/type system/subtyping.md; paper §4.6 Definition 4.7). `A <: B`
//// iff every simple prefix of A is accepted by B, with contravariant reversal at
//// mode-inversion (dual) points. Coinductive with a visited set for cycles.
////
//// Port note: Dart threads ONE mutable `visited` set through the whole check —
//// it is shared across sibling transitions and grows monotonically. The port
//// threads that set functionally (each step returns `#(Bool, Set(String))`) so
//// the cross-branch memoization is byte-faithful to the oracle.

import gleam/dict
import gleam/list
import gleam/option.{None, Some}
import gleam/set.{type Set}
import glp/analysis/type_checker/program_dfa.{type DfaState, type ProgramDfa}

/// Is output type A a subtype of output type B? Both states must be output
/// types (`is_dual == False`). Dart `isSubtype`.
pub fn is_subtype(
  state_a: DfaState,
  state_b: DfaState,
  dfa: ProgramDfa,
) -> Bool {
  is_subtype_go(state_a, state_b, dfa, set.new()).0
}

/// Core coinductive algorithm (Dart `_isSubtype`) — returns the answer and the
/// grown visited set.
fn is_subtype_go(
  state_a: DfaState,
  state_b: DfaState,
  dfa: ProgramDfa,
  visited: Set(String),
) -> #(Bool, Set(String)) {
  let pair_key = program_dfa.state_name(state_a) <> ":" <> program_dfa.state_name(state_b)
  case set.contains(visited, pair_key) {
    // Coinductive: already assumed this pair → succeed.
    True -> #(True, visited)
    False -> {
      let visited = set.insert(visited, pair_key)
      case state_a == state_b {
        // Reflexivity.
        True -> #(True, visited)
        False ->
          // Wildcard/final handling: `_`/`_FINAL_` is top for output types.
          case
            program_dfa.is_wildcard(state_b)
            || program_dfa.is_anonymous_final(state_b)
          {
            True -> #(True, visited)
            False ->
              case
                program_dfa.is_wildcard(state_a)
                || program_dfa.is_anonymous_final(state_a)
              {
                True -> #(False, visited)
                False ->
                  case
                    program_dfa.is_primitive_type(state_a)
                    || program_dfa.is_primitive_type(state_b)
                  {
                    // Primitive type lattice.
                    True -> #(check_primitive_subtype(state_a, state_b), visited)
                    // User-defined types: check transitions.
                    False ->
                      check_user_defined_subtype(state_a, state_b, dfa, visited)
                  }
              }
          }
      }
    }
  }
}

/// Every transition from A's start state must have a matching transition in B
/// (Dart's transition loop).
fn check_user_defined_subtype(
  state_a: DfaState,
  state_b: DfaState,
  dfa: ProgramDfa,
  visited: Set(String),
) -> #(Bool, Set(String)) {
  let auto_a = program_dfa.get_automaton(dfa, program_dfa.state_name(state_a))
  let auto_b = program_dfa.get_automaton(dfa, program_dfa.state_name(state_b))

  // Only transitions leaving A's start state.
  let a_transitions =
    list.filter(dict.to_list(auto_a.transitions), fn(entry) {
      let #(#(from, _label), _target) = entry
      from == state_a
    })

  list.fold(a_transitions, #(True, visited), fn(acc, entry) {
    let #(still_ok, v) = acc
    case still_ok {
      // Short-circuit once a required transition has failed.
      False -> #(False, v)
      True -> {
        let #(#(_from, label), target_a) = entry
        case program_dfa.automaton_transition(auto_b, state_b, label) {
          // A has a transition B lacks → not a subtype.
          None -> #(False, v)
          Some(target_b) ->
            case target_a == target_b {
              // Trivially equal targets.
              True -> #(True, v)
              False -> check_target_subtype(target_a, target_b, dfa, v)
            }
        }
      }
    }
  })
}

/// Target compatibility (Dart `_checkTargetSubtype`): covariant for output
/// targets, contravariant (reversed) at mode-inversion (dual) targets, mixed
/// modes incompatible.
fn check_target_subtype(
  target_a: DfaState,
  target_b: DfaState,
  dfa: ProgramDfa,
  visited: Set(String),
) -> #(Bool, Set(String)) {
  case target_a.is_dual, target_b.is_dual {
    // Both output → covariant recursion.
    False, False -> is_subtype_go(target_a, target_b, dfa, visited)
    // Both dual → contravariant recursion (inner types reversed).
    True, True -> {
      let inner_a = program_dfa.get_dfa_state(dfa, target_a.base_name)
      let inner_b = program_dfa.get_dfa_state(dfa, target_b.base_name)
      is_subtype_go(inner_b, inner_a, dfa, visited)
    }
    // Mixed → incompatible mode structure.
    _, _ -> #(False, visited)
  }
}

/// Primitive type lattice (Dart `_checkPrimitiveSubtype`): `_` is top;
/// `Integer <: Number`, `Real <: Number`; otherwise identical base names.
fn check_primitive_subtype(state_a: DfaState, state_b: DfaState) -> Bool {
  case program_dfa.is_wildcard(state_b) {
    True -> True
    False ->
      case program_dfa.is_wildcard(state_a) {
        True -> False
        False ->
          case
            program_dfa.is_integer_type(state_a)
            && program_dfa.is_number_type(state_b)
          {
            True -> True
            False ->
              case
                program_dfa.is_real_type(state_a)
                && program_dfa.is_number_type(state_b)
              {
                True -> True
                False -> state_a.base_name == state_b.base_name
              }
          }
      }
  }
}
