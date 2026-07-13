//// Type-checker foundation tests (feature 050, T018 chunk A): mode algebra
//// (mode.dart), TypeEnvironment (type_ast.dart), prelude sets (prelude.dart).

import gleam/dict
import gleam/option.{None, Some}
import gleeunit/should
import glp/analysis/prelude
import glp/analysis/type_ast.{
  type ProcDecl, Pos, ProcDecl, TypeDef, TypeRef,
}
import glp/analysis/type_checker/mode.{Input, Output}

// ── Mode algebra ────────────────────────────────────────────────────────────

pub fn mode_dual_test() {
  mode.dual(Output) |> should.equal(Input)
  mode.dual(Input) |> should.equal(Output)
  mode.flip(Input) |> should.equal(Output)
}

pub fn combine_mode_is_xor_test() {
  // Same modes cancel to Output; different modes combine to Input.
  mode.combine_mode(Output, Output) |> should.equal(Output)
  mode.combine_mode(Input, Input) |> should.equal(Output)
  mode.combine_mode(Output, Input) |> should.equal(Input)
  mode.combine_mode(Input, Output) |> should.equal(Input)
}

pub fn mode_to_string_test() {
  mode.to_string(Output) |> should.equal("output")
  mode.to_string(Input) |> should.equal("input")
}

// ── TypeEnvironment ─────────────────────────────────────────────────────────

fn decl(name: String, module_path: option.Option(String)) -> ProcDecl {
  ProcDecl(
    name: name,
    arg_types: [TypeRef("Integer", True, [], Pos(1, 1))],
    type_params: [],
    pos: Pos(1, 1),
    is_builtin: False,
    exported: False,
    imported: module_path != None,
    module_path: module_path,
  )
}

pub fn environment_add_and_lookup_test() {
  let env =
    type_ast.empty_environment()
    |> type_ast.add_type(TypeDef("Nat", [], [], Pos(1, 1)))
    |> type_ast.add_procedure(decl("double", None))
  type_ast.has_type(env, "Nat") |> should.be_true
  type_ast.has_type(env, "Missing") |> should.be_false
  // Built-in types count as defined even without a ::= definition.
  type_ast.has_type(env, "Integer") |> should.be_true
  type_ast.has_procedure(env, "double", 1) |> should.be_true
  type_ast.get_procedure(env, "double", 1) |> should.equal(Ok(decl("double", None)))
  type_ast.get_procedure(env, "double", 2) |> should.equal(Error(Nil))
}

pub fn environment_add_procedure_uses_qualified_key_test() {
  // Imported procedures with a module path are keyed 'path#name/arity'.
  let env =
    type_ast.empty_environment()
    |> type_ast.add_procedure(decl("double", Some("math")))
  dict.has_key(env.procedures, "math#double/1") |> should.be_true
  type_ast.has_procedure(env, "double", 1) |> should.be_false
}

pub fn environment_merge_other_wins_test() {
  let base =
    type_ast.empty_environment()
    |> type_ast.add_type(TypeDef("T", [], [], Pos(1, 1)))
  let override =
    type_ast.empty_environment()
    |> type_ast.add_type(TypeDef("T", ["X"], [], Pos(2, 2)))
  let merged = type_ast.merge(base, override)
  type_ast.get_type(merged, "T")
  |> should.equal(Ok(TypeDef("T", ["X"], [], Pos(2, 2))))
}

// ── Prelude sets (spot checks against prelude.dart) ────────────────────────

pub fn predefined_type_names_test() {
  prelude.is_predefined_type("Stream") |> should.be_true
  prelude.is_predefined_type("Exp") |> should.be_true
  // Library-level types are deliberately NOT protected.
  prelude.is_predefined_type("DiffList") |> should.be_false
  prelude.is_predefined_type("Channel") |> should.be_false
}

pub fn predefined_procedure_names_test() {
  prelude.is_predefined_procedure("ground") |> should.be_true
  prelude.is_predefined_procedure("@>=") |> should.be_true
  prelude.is_predefined_procedure("=..") |> should.be_true
  // Library-level operations are deliberately NOT protected.
  prelude.is_predefined_procedure("new_channel") |> should.be_false
  prelude.is_predefined_procedure("send") |> should.be_false
}

pub fn builtin_goals_test() {
  prelude.is_builtin_goal("true") |> should.be_true
  prelude.is_builtin_goal("otherwise") |> should.be_true
  prelude.is_builtin_goal(":=") |> should.be_true
  prelude.is_builtin_goal("#") |> should.be_false
}
