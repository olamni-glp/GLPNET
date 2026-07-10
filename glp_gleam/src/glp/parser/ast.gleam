//// glp/parser/ast — AST node types for GLP source (feature 050, T008).
////
//// Dart source of truth: glp_runtime/lib/compiler/ast.dart. The Dart base class
//// line/column pair is carried as a `Pos` field on every node (Gleam has no
//// inheritance). `SourceModule` is this feature's name for the Dart `Module`
//// (data-model.md §SourceModule) — a parsed `.glp` file: module name, type
//// definitions, procedure declarations, clauses, directives.
////
//// Conformance note: Dart `ConstTerm.value` is `Object?` (String/int/double);
//// the Gleam port keeps the constant kind the lexer already distinguishes, via
//// glp/runtime/terms.Constant (atom/int/real/string) — same information, typed.

import gleam/int
import gleam/list
import gleam/option.{type Option}
import glp/analysis/type_ast.{type Pos, type ProcDecl, type TypeDef}
import glp/runtime/terms.{type Constant}

/// Compilation mode: controls compiler restrictions. `User` (default) rejects
/// underscore-prefixed constants; `System` (`-mode(system).`) allows them.
pub type CompileMode {
  User
  System
}

/// A term (expression) in a clause.
pub type Term {
  /// A named variable — `X` (writer, is_reader: False) or `X?` (reader).
  VarTerm(name: String, is_reader: Bool, pos: Pos)
  /// A structure `f(args…)`.
  StructTerm(functor: String, args: List(Term), pos: Pos)
  /// A list — `[H|T]` is `ListTerm(Some(h), Some(t))`; `[]` is
  /// `ListTerm(None, None)` (Dart nullable head/tail).
  ListTerm(head: Option(Term), tail: Option(Term), pos: Pos)
  /// A constant (atom, integer, real, or string).
  ConstTerm(value: Constant, pos: Pos)
  /// An anonymous variable — `_` (is_reader: False) or `_?`.
  UnderscoreTerm(is_reader: Bool, pos: Pos)
}

/// Is this list term `[]`? (Dart `ListTerm.isNil`.)
pub fn is_nil(term: Term) -> Bool {
  case term {
    ListTerm(option.None, option.None, _) -> True
    _ -> False
  }
}

/// The predicate atom in a clause head.
pub type Atom {
  Atom(functor: String, args: List(Term), pos: Pos)
}

pub fn atom_arity(atom: Atom) -> Int {
  list.length(atom.args)
}

/// A predicate call in a clause body. `RemoteGoal` (`Module # Goal`) and
/// `SpawnGoal` (`Goal@AgentId`) are goal forms in the same union (Dart models
/// them as `Goal` subclasses whose functor/args view is '#'/'@' — see
/// `goal_functor`/`goal_args`).
pub type Goal {
  Goal(functor: String, args: List(Term), pos: Pos)
  /// Cross-module call `Module # Goal`; module is a `ConstTerm` atom (static)
  /// or `VarTerm` (dynamic).
  RemoteGoal(module: Term, goal: InnerGoal, pos: Pos)
  /// Isolate spawn `Goal@AgentId` (boot clauses). dGLP mode ignores the
  /// annotation; madGLP spawns the goal in an isolate named `agent_id`.
  SpawnGoal(inner: InnerGoal, agent_id: String, pos: Pos)
}

/// The plain call wrapped by `RemoteGoal`/`SpawnGoal` (Dart stores a `Goal`;
/// the wrapped form is always the plain functor/args shape).
pub type InnerGoal {
  InnerGoal(functor: String, args: List(Term), pos: Pos)
}

/// The Dart uniform functor view: plain goals show their own functor;
/// `RemoteGoal` shows `#`, `SpawnGoal` shows `@`.
pub fn goal_functor(goal: Goal) -> String {
  case goal {
    Goal(functor, _, _) -> functor
    RemoteGoal(_, _, _) -> "#"
    SpawnGoal(_, _, _) -> "@"
  }
}

/// The Dart uniform args view: `RemoteGoal` → `[module, goal-as-struct]`;
/// `SpawnGoal` → `[goal-as-struct, agent-id-atom]`.
pub fn goal_args(goal: Goal) -> List(Term) {
  case goal {
    Goal(_, args, _) -> args
    RemoteGoal(module, inner, _) -> [module, inner_to_term(inner)]
    SpawnGoal(inner, agent_id, pos) -> [
      inner_to_term(inner),
      ConstTerm(terms.ConstAtom(agent_id), pos),
    ]
  }
}

/// A wrapped goal as a `StructTerm` (Dart `_goalToTerm`).
pub fn inner_to_term(inner: InnerGoal) -> Term {
  StructTerm(inner.functor, inner.args, inner.pos)
}

/// A pure test in the guard section; `negated` marks `~G`.
pub type Guard {
  Guard(predicate: String, args: List(Term), negated: Bool, pos: Pos)
}

/// A clause `Head :- Guards | Body.` — `guards`/`body` are `None` when the
/// section is absent (unit clause `foo(X).` has neither; Dart nullable lists).
pub type Clause {
  Clause(head: Atom, guards: Option(List(Guard)), body: Option(List(Goal)), pos: Pos)
}

/// All clauses with the same functor/arity.
pub type Procedure {
  Procedure(name: String, arity: Int, clauses: List(Clause), pos: Pos)
}

/// "name/arity" signature (Dart `Procedure.signature`).
pub fn signature(procedure: Procedure) -> String {
  procedure.name <> "/" <> int.to_string(procedure.arity)
}

/// A module declaration directive `-module(name).`
pub type ModuleDeclaration {
  ModuleDeclaration(name: String, pos: Pos)
}

/// A parsed `.glp` file (data-model.md §SourceModule; Dart `Module`): the
/// input to the load pipeline
/// `parsed → SRSW-checked → partially-evaluated → type-checked → compiled`.
pub type SourceModule {
  SourceModule(
    declaration: Option(ModuleDeclaration),
    /// Type definitions `Name ::= alt ; alt.`
    type_defs: List(TypeDef),
    /// Procedure declarations (each carries its exported/imported flags).
    proc_declarations: List(ProcDecl),
    /// Parameterized proc-decl templates (call-site inference).
    param_proc_decls: List(ProcDecl),
    procedures: List(Procedure),
    compile_mode: CompileMode,
    pos: Pos,
  )
}

/// Module name, `None` if anonymous (Dart `Module.name`).
pub fn module_name(module: SourceModule) -> Option(String) {
  case module.declaration {
    option.Some(ModuleDeclaration(name, _)) -> option.Some(name)
    option.None -> option.None
  }
}

/// All exported procedure signatures — "name/arity" of declarations with
/// `exported: True` (Dart `Module.exportedSignatures`).
pub fn exported_signatures(module: SourceModule) -> List(String) {
  module.proc_declarations
  |> list.filter(fn(decl) { decl.exported })
  |> list.map(type_ast.key)
}
