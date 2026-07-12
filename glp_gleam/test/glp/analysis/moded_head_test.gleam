//// Moded-head tests (feature 050, T018) — Dart moded_head.dart is the oracle:
//// the root ↓ + unconditional variable flip of `modedHead`, the root ↑ /
//// no-flip of `producedTerm`, arity-mismatch diagnostics, the clause-wide
//// anonymous-variable counter threaded across head + body atoms, embedded-mode
//// involution inside a typed structure, and constant pass-through.

import gleam/option.{None, Some}
import gleeunit/should
import glp/analysis/type_ast
import glp/analysis/type_checker/mode.{Input, Output}
import glp/analysis/type_checker/moded_head
import glp/analysis/type_checker/moded_term.{
  ModedCompound, ModedConstant, ModedVariable,
}
import glp/parser/ast
import glp/runtime/terms

fn pos() -> type_ast.Pos {
  type_ast.Pos(0, 0)
}

fn ty(name: String, is_input: Bool) -> type_ast.TypeExpr {
  type_ast.TypeRef(name, is_input, [], pos())
}

fn decl(name: String, arg_types: List(type_ast.TypeExpr)) -> type_ast.ProcDecl {
  type_ast.ProcDecl(name, arg_types, [], pos(), False, False, False, None)
}

fn goal(functor: String, args: List(ast.Term)) -> ast.Goal {
  ast.Goal(functor, args, pos())
}

fn var(name: String, is_reader: Bool) -> ast.Term {
  ast.VarTerm(name, is_reader, pos())
}

// ---------------------------------------------------------------------------
// modedHead — root ↓ and every variable flipped
// ---------------------------------------------------------------------------

pub fn moded_head_flips_variables_test() {
  // procedure f(Integer?, Integer). head f(X, Y?).
  let d = decl("f", [ty("Integer", True), ty("Integer", False)])
  let head = goal("f", [var("X", False), var("Y", True)])
  moded_head.moded_head(head, d, None)
  |> should.equal(
    Ok(#(
      ModedCompound(Input, "f", 2, [
        // X (writer) → reader at consume position.
        ModedVariable("X", True, Input),
        // Y? (reader) → writer at produce position.
        ModedVariable("Y", False, Output),
      ]),
      0,
    )),
  )
}

pub fn moded_head_arity_mismatch_test() {
  let d = decl("f", [ty("Integer", True), ty("Integer", False)])
  let head = goal("f", [var("X", False)])
  moded_head.moded_head(head, d, None)
  |> should.equal(
    Error(moded_head.ArityMismatch(
      "Head arity 1 does not match declaration arity 2",
    )),
  )
}

// ---------------------------------------------------------------------------
// producedTerm — root ↑, variables NOT flipped
// ---------------------------------------------------------------------------

pub fn produced_term_no_flip_test() {
  let d = decl("f", [ty("Integer", True), ty("Integer", False)])
  let atom = goal("f", [var("X", False), var("Y", True)])
  moded_head.produced_term(atom, d, None, 0)
  |> should.equal(
    Ok(#(
      ModedCompound(Output, "f", 2, [
        ModedVariable("X", False, Input),
        ModedVariable("Y", True, Output),
      ]),
      0,
    )),
  )
}

pub fn produced_term_arity_mismatch_test() {
  let d = decl("f", [ty("Integer", True), ty("Integer", False)])
  let atom = goal("f", [var("X", False)])
  moded_head.produced_term(atom, d, None, 0)
  |> should.equal(
    Error(moded_head.ArityMismatch(
      "Atom arity 1 does not match declaration arity 2",
    )),
  )
}

// ---------------------------------------------------------------------------
// Anonymous-variable counter threaded across head + body atoms
// ---------------------------------------------------------------------------

pub fn anon_counter_threading_test() {
  // procedure g(Integer?, Integer). head g(_, _), then body g(_, _).
  let d = decl("g", [ty("Integer", True), ty("Integer", False)])
  let anon = ast.UnderscoreTerm(False, pos())

  // Head: two fresh anon writers, flipped to readers; counter ends at 2.
  let head_result = moded_head.moded_head(goal("g", [anon, anon]), d, None)
  head_result
  |> should.equal(
    Ok(#(
      ModedCompound(Input, "g", 2, [
        ModedVariable("_#1", True, Input),
        ModedVariable("_#2", True, Output),
      ]),
      2,
    )),
  )

  // Body atom continues from counter 2 → distinct names _#3/_#4, no flip.
  moded_head.produced_term(goal("g", [anon, anon]), d, None, 2)
  |> should.equal(
    Ok(#(
      ModedCompound(Output, "g", 2, [
        ModedVariable("_#3", False, Input),
        ModedVariable("_#4", False, Output),
      ]),
      4,
    )),
  )
}

// ---------------------------------------------------------------------------
// Embedded-mode involution inside a typed structure
// ---------------------------------------------------------------------------

pub fn embedded_mode_struct_test() {
  // Msg ::= m(Integer?).   procedure p(Msg?).   head p(m(V)).
  // p's arg is consume (↓). Inside m, the field Integer? flips ↓→↑, so V sits
  // at a produce position; after the head flip V becomes a reader.
  let msg =
    type_ast.TypeDef(
      "Msg",
      [],
      [type_ast.StructAlt("m", [ty("Integer", True)], pos())],
      pos(),
    )
  let env =
    type_ast.empty_environment()
    |> type_ast.add_type(msg)

  let d = decl("p", [ty("Msg", True)])
  let head = goal("p", [ast.StructTerm("m", [var("V", False)], pos())])

  moded_head.moded_head(head, d, Some(env))
  |> should.equal(
    Ok(#(
      ModedCompound(Input, "p", 1, [
        ModedCompound(Input, "m", 1, [ModedVariable("V", True, Output)]),
      ]),
      0,
    )),
  )
}

// ---------------------------------------------------------------------------
// Constants pass through with the argument mode, unflipped
// ---------------------------------------------------------------------------

pub fn constant_passthrough_test() {
  let d = decl("f", [ty("Integer", True), ty("Integer", False)])
  let head = goal("f", [ast.ConstTerm(terms.ConstInt(1), pos()), var("Y", True)])
  moded_head.moded_head(head, d, None)
  |> should.equal(
    Ok(#(
      ModedCompound(Input, "f", 2, [
        ModedConstant(Input, terms.ConstInt(1)),
        ModedVariable("Y", False, Output),
      ]),
      0,
    )),
  )
}
