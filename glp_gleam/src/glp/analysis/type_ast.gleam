//// glp/analysis/type_ast — AST for GLP type declarations (Yardeni-Shapiro),
//// feature 050, T008.
////
//// Types are first-class syntactic elements parsed alongside clauses; the parser
//// stores them in the `SourceModule` (glp/parser/ast) and the type checker (T018)
//// consumes them.
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/type_ast.dart
//// (including `TypeEnvironment`, ported here with T018 — Dart's mutating
//// addType/addProcedure become insert functions returning a new environment).

import gleam/dict.{type Dict}
import gleam/float
import gleam/int
import gleam/list
import gleam/option.{type Option, Some}
import gleam/string

/// Source position carried by every node (Dart AstNode line/column).
pub type Pos {
  Pos(line: Int, column: Int)
}

/// Classification of types by mode structure (type-environment.md): `Output` =
/// no complementation in the definition; `Input` = complement of an output type
/// (not directly defined); `Interactive` = contains internal complementation
/// (`_?` or `T?` in alternatives).
pub type TypeClassification {
  Output
  Input
  Interactive
}

/// A type expression — a named reference or one alternative shape of a
/// definition.
pub type TypeExpr {
  /// Reference to a named type, optionally input-moded (`Type?`), optionally
  /// parameterized (`Stream(Integer)`).
  TypeRef(name: String, is_input: Bool, type_args: List(TypeExpr), pos: Pos)
  /// A constant alternative: `0`, `[]`, `foo`.
  ConstantAlt(value: ConstValue, pos: Pos)
  /// A structure alternative: `s(Nat)`, `tree(Nat, Tree, Tree)`.
  StructAlt(functor: String, args: List(TypeExpr), pos: Pos)
  /// Empty-list alternative: `[]`.
  ListNilAlt(pos: Pos)
  /// List-cons alternative: `[Head | Tail]`.
  ListConsAlt(head: TypeExpr, tail: TypeExpr, pos: Pos)
  /// Primitive mode alternative: `_` (output) or `_?` (input) — e.g.
  /// `Any ::= _ ; _?.`
  PrimitiveModeAlt(is_input: Bool, pos: Pos)
  /// Difference-list alternative: `List \ List?`.
  DiffListAlt(content: TypeExpr, hole: TypeExpr, pos: Pos)
}

/// A constant in a type alternative (Dart `Object value` — atom, int, or real;
/// the Gleam port keeps the lexer's constant kind explicit).
pub type ConstValue {
  CvAtom(String)
  CvInt(Int)
  CvReal(Float)
  CvString(String)
}

/// Primitive types (not defined via `::=`, handled specially by the compiler —
/// Dart `TypeRef.builtins`).
pub fn is_builtin_type(name: String) -> Bool {
  case name {
    "Integer" | "Real" | "Number" | "String" -> True
    _ -> False
  }
}

/// System types (defined via `::=` but not redefinable by the user — Dart
/// `TypeRef.systemTypes`).
pub fn is_system_type(name: String) -> Bool {
  case name {
    "Any" | "List" -> True
    _ -> False
  }
}

/// Mode dual per Definition 5.1 — the same reference with inverted mode
/// (meaningful for `TypeRef`/`PrimitiveModeAlt`; identity otherwise).
pub fn dual(expr: TypeExpr) -> TypeExpr {
  case expr {
    TypeRef(name, is_input, type_args, pos) ->
      TypeRef(name, !is_input, type_args, pos)
    PrimitiveModeAlt(is_input, pos) -> PrimitiveModeAlt(!is_input, pos)
    _ -> expr
  }
}

/// Whether a procedure-argument type expression is input (consume) mode
/// (Dart `ProcArgTypeExpr.isInputMode`).
pub fn is_input_mode(expr: TypeExpr) -> Bool {
  case expr {
    TypeRef(_, is_input, _, _) -> is_input
    PrimitiveModeAlt(is_input, _) -> is_input
    _ -> False
  }
}

/// The type name of a procedure-argument expression, `None` for primitives
/// (Dart `ProcArgTypeExpr.typeName`).
pub fn type_name(expr: TypeExpr) -> Option(String) {
  case expr {
    TypeRef(name, _, _, _) -> Some(name)
    _ -> option.None
  }
}

/// Dart `TypeExpr.toString()` — used verbatim in expanded-name canonicalization
/// (param_expansion) and checker diagnostics, so the rendering must be
/// byte-identical: `TypeRef` renders `Name(Args…)?` with args joined `", "`;
/// constants render via the Dart value toString (atoms bare, strings
/// quote-wrapped, numbers plain).
pub fn type_expr_to_string(expr: TypeExpr) -> String {
  case expr {
    TypeRef(name, is_input, type_args, _) -> {
      let args_str = case type_args {
        [] -> ""
        _ ->
          "("
          <> {
            type_args |> list.map(type_expr_to_string) |> string.join(", ")
          }
          <> ")"
      }
      case is_input {
        True -> name <> args_str <> "?"
        False -> name <> args_str
      }
    }
    ConstantAlt(value, _) -> const_value_to_string(value)
    StructAlt(functor, args, _) ->
      functor
      <> "("
      <> { args |> list.map(type_expr_to_string) |> string.join(", ") }
      <> ")"
    ListNilAlt(_) -> "[]"
    ListConsAlt(head, tail, _) ->
      "["
      <> type_expr_to_string(head)
      <> " | "
      <> type_expr_to_string(tail)
      <> "]"
    PrimitiveModeAlt(True, _) -> "_?"
    PrimitiveModeAlt(False, _) -> "_"
    DiffListAlt(content, hole, _) ->
      type_expr_to_string(content) <> " \\ " <> type_expr_to_string(hole)
  }
}

/// Dart `ConstantAlt.value.toString()` — the Dart value space carries string
/// literals quote-wrapped and atoms bare.
pub fn const_value_to_string(value: ConstValue) -> String {
  case value {
    CvAtom(name) -> name
    CvString(s) -> "\"" <> s <> "\""
    CvInt(i) -> int.to_string(i)
    CvReal(r) -> float.to_string(r)
  }
}

/// A type definition `TypeName ::= alt1 ; alt2 ; … .`; parameterized types
/// carry non-empty `type_params` (`Stream(X) ::= [] ; [X | Stream(X)].`).
pub type TypeDef {
  TypeDef(
    name: String,
    type_params: List(String),
    alternatives: List(TypeExpr),
    pos: Pos,
  )
}

pub fn is_parameterized_def(def: TypeDef) -> Bool {
  def.type_params != []
}

/// Classify a definition by mode structure (type-environment.md v0.5): any
/// alternative containing complementation makes the type `Interactive`, else
/// `Output`. (`Input` types are never directly defined.)
pub fn classification(def: TypeDef) -> TypeClassification {
  case list.any(def.alternatives, contains_complement) {
    True -> Interactive
    False -> Output
  }
}

/// Does a type expression contain any complementation (`T?` or `_?`)?
pub fn contains_complement(expr: TypeExpr) -> Bool {
  case expr {
    TypeRef(_, is_input, _, _) -> is_input
    PrimitiveModeAlt(is_input, _) -> is_input
    ListConsAlt(head, tail, _) ->
      contains_complement(head) || contains_complement(tail)
    StructAlt(_, args, _) -> list.any(args, contains_complement)
    DiffListAlt(content, hole, _) ->
      contains_complement(content) || contains_complement(hole)
    ConstantAlt(_, _) | ListNilAlt(_) -> False
  }
}

/// A procedure declaration `procedure name(Type1, Type2, …).` — argument types
/// are `TypeRef` or `PrimitiveModeAlt`. Parameterized declarations
/// (`procedure gethead(Stream(X)?, X).`) carry `type_params` and are templates
/// instantiated per call site by the type checker.
pub type ProcDecl {
  ProcDecl(
    name: String,
    arg_types: List(TypeExpr),
    type_params: List(String),
    pos: Pos,
    /// Implemented by the runtime (no GLP clauses).
    is_builtin: Bool,
    /// Declared `exported procedure`.
    exported: Bool,
    /// Declared `imported procedure`.
    imported: Bool,
    /// For imported procedures: module path (`social`, `ui#actors`); `None`
    /// for ancestor scope.
    module_path: Option(String),
  )
}

pub fn arity(decl: ProcDecl) -> Int {
  list.length(decl.arg_types)
}

pub fn is_parameterized_decl(decl: ProcDecl) -> Bool {
  decl.type_params != []
}

/// "name/arity" lookup key (Dart `ProcDecl.key`).
pub fn key(decl: ProcDecl) -> String {
  decl.name <> "/" <> int.to_string(arity(decl))
}

/// The full qualified name — module path prefix for imported procedures
/// (Dart `ProcDecl.qualifiedName`).
pub fn qualified_name(decl: ProcDecl) -> String {
  case decl.module_path {
    Some(path) -> path <> "#" <> decl.name
    option.None -> decl.name
  }
}

/// TypeEnvironment lookup key including the module path for imported
/// procedures: `factorial/2`, `math#factorial/2` (Dart `ProcDecl.qualifiedKey`).
pub fn qualified_key(decl: ProcDecl) -> String {
  qualified_name(decl) <> "/" <> int.to_string(arity(decl))
}

/// Mode of argument `i` (`True` = input mode; Dart `ProcDecl.isInputArg`).
pub fn is_input_arg(decl: ProcDecl, i: Int) -> Bool {
  case list.drop(decl.arg_types, i) {
    [arg, ..] -> is_input_mode(arg)
    [] -> False
  }
}

/// The base type name of argument `i`, `None` for primitives (`_`/`_?`;
/// Dart `ProcDecl.getTypeName`).
pub fn get_type_name(decl: ProcDecl, i: Int) -> Option(String) {
  case list.drop(decl.arg_types, i) {
    [arg, ..] -> type_name(arg)
    [] -> option.None
  }
}

/// The type environment: all type definitions and procedure declarations in a
/// module (Dart `TypeEnvironment`).
pub type TypeEnvironment {
  TypeEnvironment(
    types: Dict(String, TypeDef),
    /// Keyed by "name/arity" (qualified key for imported procedures).
    procedures: Dict(String, ProcDecl),
    /// Parameterized procedure declaration templates, keyed by "name/arity" —
    /// used for call-site type parameter inference (Case B).
    param_proc_decls: Dict(String, ProcDecl),
    /// Parameterized type templates from prelude/ancestors, passed to
    /// downstream expansions so references to templates defined in ancestor
    /// scopes can expand.
    type_templates: Dict(String, TypeDef),
  )
}

pub fn empty_environment() -> TypeEnvironment {
  TypeEnvironment(dict.new(), dict.new(), dict.new(), dict.new())
}

/// Merge another environment into this one (the other's entries win).
pub fn merge(env: TypeEnvironment, other: TypeEnvironment) -> TypeEnvironment {
  TypeEnvironment(
    types: dict.merge(env.types, other.types),
    procedures: dict.merge(env.procedures, other.procedures),
    param_proc_decls: dict.merge(env.param_proc_decls, other.param_proc_decls),
    type_templates: dict.merge(env.type_templates, other.type_templates),
  )
}

/// Look up a type definition (Dart `getType`).
pub fn get_type(env: TypeEnvironment, name: String) -> Result(TypeDef, Nil) {
  dict.get(env.types, name)
}

/// Look up a procedure declaration by name/arity (Dart `getProcedure`).
pub fn get_procedure(
  env: TypeEnvironment,
  name: String,
  procedure_arity: Int,
) -> Result(ProcDecl, Nil) {
  dict.get(env.procedures, name <> "/" <> int.to_string(procedure_arity))
}

/// Is a type name defined (including built-ins)? (Dart `hasType`.)
pub fn has_type(env: TypeEnvironment, name: String) -> Bool {
  dict.has_key(env.types, name) || is_builtin_type(name)
}

/// Is a procedure declared? (Dart `hasProcedure`.)
pub fn has_procedure(
  env: TypeEnvironment,
  name: String,
  procedure_arity: Int,
) -> Bool {
  dict.has_key(env.procedures, name <> "/" <> int.to_string(procedure_arity))
}

/// Add a type definition (Dart `addType`).
pub fn add_type(env: TypeEnvironment, type_def: TypeDef) -> TypeEnvironment {
  TypeEnvironment(..env, types: dict.insert(env.types, type_def.name, type_def))
}

/// Add a procedure declaration under its qualified key (Dart `addProcedure`).
pub fn add_procedure(env: TypeEnvironment, decl: ProcDecl) -> TypeEnvironment {
  TypeEnvironment(
    ..env,
    procedures: dict.insert(env.procedures, qualified_key(decl), decl),
  )
}
