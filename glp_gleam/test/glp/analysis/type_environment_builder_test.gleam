//// Type-environment-builder tests (feature 050, T018) — Dart
//// type_environment_builder.dart is the oracle: predefined-redefinition
//// rejection, determinism checking, simple/union alias resolution, circular-alias
//// detection, clause extraction, and byte-identical error strings. The prelude
//// source is threaded explicitly (Dart's mutable global is dropped).

import gleam/dict
import gleam/option.{None}
import gleeunit/should
import glp/analysis/type_ast.{
  ConstantAlt, CvAtom, Pos, ProcDecl, StructAlt, TypeDef, TypeRef,
}
import glp/analysis/type_checker/type_environment_builder.{
  CircularAliasError, NonDeterministicTypeError, RedefinitionError,
}
import glp/parser/ast

fn pos() -> type_ast.Pos {
  Pos(0, 0)
}

fn tref(name: String, is_input: Bool) -> type_ast.TypeExpr {
  TypeRef(name, is_input, [], pos())
}

fn tdef(name: String, alts: List(type_ast.TypeExpr)) -> type_ast.TypeDef {
  TypeDef(name, [], alts, pos())
}

fn pdecl(name: String, args: List(type_ast.TypeExpr)) -> type_ast.ProcDecl {
  ProcDecl(name, args, [], pos(), False, False, False, None)
}

fn smodule(
  type_defs: List(type_ast.TypeDef),
  proc_decls: List(type_ast.ProcDecl),
) -> ast.SourceModule {
  ast.SourceModule(None, type_defs, proc_decls, [], [], ast.User, pos())
}

// ---------------------------------------------------------------------------
// Basic construction
// ---------------------------------------------------------------------------

pub fn builds_types_and_procedures_test() {
  let module =
    smodule(
      [
        tdef("Nat", [
          ConstantAlt(CvAtom("zero"), pos()),
          StructAlt("s", [tref("Nat", False)], pos()),
        ]),
      ],
      [pdecl("count", [tref("Nat", True), tref("Integer", False)])],
    )
  case type_environment_builder.build_type_environment(module, None) {
    Ok(env) -> {
      type_ast.has_type(env, "Nat") |> should.be_true
      type_ast.has_procedure(env, "count", 2) |> should.be_true
    }
    Error(_) -> should.fail()
  }
}

pub fn empty_prelude_environment_test() {
  type_environment_builder.build_prelude_environment("")
  |> should.equal(Ok(type_ast.empty_environment()))
}

// ---------------------------------------------------------------------------
// Redefinition / determinism errors
// ---------------------------------------------------------------------------

pub fn redefine_predefined_type_rejected_test() {
  let module = smodule([tdef("Integer", [ConstantAlt(CvAtom("x"), pos())])], [])
  type_environment_builder.build_type_environment(module, None)
  |> should.equal(Error(RedefinitionError(
    "Cannot redefine predefined type: Integer",
    0,
    0,
  )))
}

pub fn duplicate_constant_alternative_rejected_test() {
  let module =
    smodule(
      [tdef("Bad", [ConstantAlt(CvAtom("zero"), pos()), ConstantAlt(CvAtom("zero"), pos())])],
      [],
    )
  type_environment_builder.build_type_environment(module, None)
  |> should.equal(Error(NonDeterministicTypeError(
    "Duplicate constant alternative: zero in Bad",
    0,
    0,
  )))
}

pub fn duplicate_functor_alternative_rejected_test() {
  let module =
    smodule(
      [
        tdef("Bad", [
          StructAlt("s", [tref("Nat", False)], pos()),
          StructAlt("s", [tref("Nat", False)], pos()),
        ]),
      ],
      [],
    )
  type_environment_builder.build_type_environment(module, None)
  |> should.equal(Error(NonDeterministicTypeError(
    "Duplicate functor alternative: s/1 in Bad",
    0,
    0,
  )))
}

// ---------------------------------------------------------------------------
// Alias resolution
// ---------------------------------------------------------------------------

pub fn simple_alias_resolved_and_removed_test() {
  // Nat ::= zero.  MyNat ::= Nat.  procedure p(MyNat).
  let module =
    smodule(
      [
        tdef("Nat", [ConstantAlt(CvAtom("zero"), pos())]),
        tdef("MyNat", [tref("Nat", False)]),
      ],
      [pdecl("p", [tref("MyNat", False)])],
    )
  case type_environment_builder.build_type_environment(module, None) {
    Ok(env) -> {
      // The simple alias is removed from the types map.
      dict.has_key(env.types, "MyNat") |> should.be_false
      // The procedure's argument type is resolved to the real type.
      case type_ast.get_procedure(env, "p", 1) {
        Ok(decl) -> decl.arg_types |> should.equal([tref("Nat", False)])
        Error(_) -> should.fail()
      }
    }
    Error(_) -> should.fail()
  }
}

pub fn union_alias_expanded_test() {
  // A ::= a.  B ::= b.  Msg ::= A ; B.
  let module =
    smodule(
      [
        tdef("A", [ConstantAlt(CvAtom("a"), pos())]),
        tdef("B", [ConstantAlt(CvAtom("b"), pos())]),
        tdef("Msg", [tref("A", False), tref("B", False)]),
      ],
      [],
    )
  case type_environment_builder.build_type_environment(module, None) {
    Ok(env) ->
      case type_ast.get_type(env, "Msg") {
        Ok(msg) ->
          msg.alternatives
          |> should.equal([
            ConstantAlt(CvAtom("a"), pos()),
            ConstantAlt(CvAtom("b"), pos()),
          ])
        Error(_) -> should.fail()
      }
    Error(_) -> should.fail()
  }
}

pub fn circular_alias_rejected_test() {
  // X ::= Y.  Y ::= X.
  let module =
    smodule([tdef("X", [tref("Y", False)]), tdef("Y", [tref("X", False)])], [])
  case type_environment_builder.build_type_environment(module, None) {
    Error(CircularAliasError(_, _, _)) -> Nil
    _ -> should.fail()
  }
}

// ---------------------------------------------------------------------------
// Clause extraction / error rendering
// ---------------------------------------------------------------------------

pub fn extract_clauses_test() {
  let clause = ast.Clause(ast.Atom("f", [], pos()), None, None, pos())
  let module =
    ast.SourceModule(
      None,
      [],
      [],
      [],
      [ast.Procedure("f", 0, [clause], pos())],
      ast.User,
      pos(),
    )
  type_environment_builder.extract_clauses(module) |> should.equal([clause])
}

pub fn error_to_string_test() {
  type_environment_builder.error_to_string(RedefinitionError("msg", 3, 7))
  |> should.equal("msg at line 3, column 7")
}
