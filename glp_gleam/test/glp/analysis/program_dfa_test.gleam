//// Program-DFA + leaf-consistency tests (feature 050, T018) — Dart
//// program_dfa.dart is the oracle: complement-pair automata, constant/struct/
//// list-cons transitions, procedure automata, and Definition 4.3 leaf
//// consistency (variable-mode match, primitive literals, wildcard modes,
//// user-defined constant transitions, accepted primitives). Error strings must
//// be byte-identical to the Dart checker.

import gleam/dict
import gleam/option.{None, Some}
import gleeunit/should
import glp/analysis/type_ast.{
  type TypeEnvironment, ConstantAlt, CvAtom, ListConsAlt,
  ListNilAlt, Pos, ProcDecl, StructAlt, TypeDef, TypeRef,
}
import glp/analysis/type_checker/mode.{Input, Output}
import glp/analysis/type_checker/program_dfa

fn pos() -> type_ast.Pos {
  Pos(0, 0)
}

fn tref(name: String, is_input: Bool) -> type_ast.TypeExpr {
  TypeRef(name, is_input, [], pos())
}

/// A small oracle environment:
///   Nat  ::= zero ; s(Nat).
///   NatList ::= [] ; [Nat | NatList].
///   Konst ::= Integer ; String.
///   procedure count(NatList?, Integer).
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
  let konst =
    TypeDef("Konst", [], [tref("Integer", False), tref("String", False)], pos())
  let count =
    ProcDecl(
      name: "count",
      arg_types: [tref("NatList", True), tref("Integer", False)],
      type_params: [],
      pos: pos(),
      is_builtin: False,
      exported: False,
      imported: False,
      module_path: None,
    )

  type_ast.empty_environment()
  |> type_ast.add_type(nat)
  |> type_ast.add_type(nat_list)
  |> type_ast.add_type(konst)
  |> type_ast.add_procedure(count)
}

fn dfa() -> program_dfa.ProgramDfa {
  program_dfa.build_program_dfa(sample_env())
}

// ---------------------------------------------------------------------------
// State / label accessors
// ---------------------------------------------------------------------------

pub fn state_name_and_dual_test() {
  let s = program_dfa.get_dfa_state(dfa(), "Nat")
  program_dfa.state_name(s) |> should.equal("Nat")
  program_dfa.state_name(program_dfa.state_dual(s)) |> should.equal("Nat?")
}

pub fn system_states_present_test() {
  let d = dfa()
  program_dfa.get_dfa_state(d, "_FINAL_").is_final |> should.be_true
  program_dfa.get_dfa_state(d, "Integer?").is_dual |> should.be_true
  program_dfa.get_dfa_state(d, "count/2").is_procedure |> should.be_true
}

pub fn label_to_string_test() {
  program_dfa.label_to_string(program_dfa.transition_label_functor(
    "[|]",
    2,
    1,
    Some(Output),
  ))
  |> should.equal("[|](2,1):↑")
  program_dfa.label_to_string(program_dfa.transition_label_constant_symbol("[]"))
  |> should.equal("[]")
}

// ---------------------------------------------------------------------------
// Automaton construction
// ---------------------------------------------------------------------------

pub fn constant_transition_built_test() {
  let d = dfa()
  let nat = program_dfa.get_dfa_state(d, "Nat")
  let automaton = program_dfa.get_automaton(d, "Nat")
  // `zero` constant → _FINAL_.
  program_dfa.automaton_transition(
    automaton,
    nat,
    program_dfa.transition_label_constant_value(CvAtom("zero")),
  )
  |> should.equal(Some(program_dfa.get_dfa_state(d, "_FINAL_")))
}

pub fn list_cons_transition_built_test() {
  let d = dfa()
  let natlist = program_dfa.get_dfa_state(d, "NatList")
  let automaton = program_dfa.get_automaton(d, "NatList")
  // head [|](2,1):↑ → Nat.
  program_dfa.automaton_transition(
    automaton,
    natlist,
    program_dfa.transition_label_functor("[|]", 2, 1, Some(Output)),
  )
  |> should.equal(Some(program_dfa.get_dfa_state(d, "Nat")))
  // tail [|](2,2):↑ → NatList.
  program_dfa.automaton_transition(
    automaton,
    natlist,
    program_dfa.transition_label_functor("[|]", 2, 2, Some(Output)),
  )
  |> should.equal(Some(natlist))
}

pub fn procedure_automaton_targets_test() {
  let d = dfa()
  let proc = program_dfa.get_dfa_state(d, "count/2")
  let automaton = program_dfa.get_automaton(d, "count/2")
  // arg 1 (NatList?) — no mode on procedure-arg transitions.
  program_dfa.automaton_transition(
    automaton,
    proc,
    program_dfa.transition_label_functor("count", 2, 1, None),
  )
  |> should.equal(Some(program_dfa.get_dfa_state(d, "NatList?")))
  // arg 2 (Integer).
  program_dfa.automaton_transition(
    automaton,
    proc,
    program_dfa.transition_label_functor("count", 2, 2, None),
  )
  |> should.equal(Some(program_dfa.get_dfa_state(d, "Integer")))
}

pub fn dual_automaton_flips_modes_test() {
  let d = dfa()
  // NatList? automaton is the dual: head transition mode is flipped to ↓.
  let natlist_dual = program_dfa.get_dfa_state(d, "NatList?")
  let automaton = program_dfa.get_automaton(d, "NatList?")
  program_dfa.automaton_transition(
    automaton,
    natlist_dual,
    program_dfa.transition_label_functor("[|]", 2, 1, Some(Input)),
  )
  |> should.equal(Some(program_dfa.get_dfa_state(d, "Nat?")))
}

// ---------------------------------------------------------------------------
// Leaf consistency — variables (Definition 4.3, Case 1)
// ---------------------------------------------------------------------------

pub fn reader_at_consume_is_consistent_test() {
  let d = dfa()
  let s = program_dfa.get_dfa_state(d, "Nat")
  program_dfa.check_leaf_consistency(program_dfa.leaf_reader("X", Input), s, d)
  |> should.equal(program_dfa.LeafConsistent(Some(s)))
}

pub fn writer_at_produce_is_consistent_test() {
  let d = dfa()
  let s = program_dfa.get_dfa_state(d, "Nat")
  program_dfa.check_leaf_consistency(program_dfa.leaf_writer("X", Output), s, d)
  |> should.equal(program_dfa.LeafConsistent(Some(s)))
}

pub fn reader_at_produce_is_inconsistent_test() {
  let d = dfa()
  let s = program_dfa.get_dfa_state(d, "Nat")
  program_dfa.check_leaf_consistency(program_dfa.leaf_reader("X", Output), s, d)
  |> should.equal(program_dfa.LeafInconsistent(
    "Variable mode mismatch: reader requires ↓ (consume), got ↑ (produce)",
  ))
}

pub fn writer_at_consume_is_inconsistent_test() {
  let d = dfa()
  let s = program_dfa.get_dfa_state(d, "Nat")
  program_dfa.check_leaf_consistency(program_dfa.leaf_writer("X", Input), s, d)
  |> should.equal(program_dfa.LeafInconsistent(
    "Variable mode mismatch: writer requires ↑ (produce), got ↓ (consume)",
  ))
}

// ---------------------------------------------------------------------------
// Leaf consistency — constants (Definition 4.3, Case 2)
// ---------------------------------------------------------------------------

pub fn integer_literal_at_integer_type_test() {
  let d = dfa()
  let s = program_dfa.get_dfa_state(d, "Integer")
  program_dfa.check_leaf_consistency(
    program_dfa.leaf_integer_constant(5, Some(Output)),
    s,
    d,
  )
  |> should.equal(program_dfa.LeafConsistent(Some(program_dfa.get_dfa_state(d, "_FINAL_"))))
}

pub fn integer_literal_at_string_type_fails_test() {
  let d = dfa()
  let s = program_dfa.get_dfa_state(d, "String")
  program_dfa.check_leaf_consistency(
    program_dfa.leaf_integer_constant(5, Some(Output)),
    s,
    d,
  )
  |> should.equal(program_dfa.LeafInconsistent("String type requires string literal"))
}

pub fn produced_wildcard_accepts_produced_test() {
  let d = dfa()
  let s = program_dfa.get_dfa_state(d, "_")
  program_dfa.check_leaf_consistency(
    program_dfa.leaf_constant(CvAtom("foo"), Some(Output)),
    s,
    d,
  )
  |> should.equal(program_dfa.LeafConsistent(Some(s)))
}

pub fn consumed_wildcard_rejects_produced_test() {
  let d = dfa()
  let s = program_dfa.get_dfa_state(d, "_?")
  program_dfa.check_leaf_consistency(
    program_dfa.leaf_constant(CvAtom("foo"), Some(Output)),
    s,
    d,
  )
  |> should.equal(program_dfa.LeafInconsistent(
    "_? expects consumed term (mode ↓), got produced term",
  ))
}

pub fn atom_matches_user_defined_constant_test() {
  let d = dfa()
  let nat = program_dfa.get_dfa_state(d, "Nat")
  // `zero` is a constant alternative of Nat → consistent, next = _FINAL_.
  program_dfa.check_leaf_consistency(
    program_dfa.leaf_constant(CvAtom("zero"), Some(Output)),
    nat,
    d,
  )
  |> should.equal(program_dfa.LeafConsistent(Some(program_dfa.get_dfa_state(d, "_FINAL_"))))
}

pub fn atom_not_in_user_type_fails_test() {
  let d = dfa()
  let nat = program_dfa.get_dfa_state(d, "Nat")
  program_dfa.check_leaf_consistency(
    program_dfa.leaf_constant(CvAtom("nope"), Some(Output)),
    nat,
    d,
  )
  |> should.equal(program_dfa.LeafInconsistent(
    "Constant does not match any alternative at type position Nat",
  ))
}

pub fn accepted_primitive_admits_integer_test() {
  let d = dfa()
  // Konst ::= Integer ; String. — an integer literal is admitted via the
  // accepted-primitives path.
  let konst = program_dfa.get_dfa_state(d, "Konst")
  program_dfa.check_leaf_consistency(
    program_dfa.leaf_integer_constant(7, Some(Output)),
    konst,
    d,
  )
  |> should.equal(program_dfa.LeafConsistent(Some(program_dfa.get_dfa_state(d, "_FINAL_"))))
}

pub fn missing_state_lookup_stays_total_test() {
  // A sanity check that dict lookups on the built DFA are populated.
  { dict.size(dfa().states) > 13 } |> should.be_true
}
