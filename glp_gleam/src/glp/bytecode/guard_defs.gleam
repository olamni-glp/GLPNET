//// glp/bytecode/guard_defs — runtime-defined guard clause specs (feature 050, T007).
////
//// Port of the 049 Deliverable A form (a)/(a1) side-table types: a declared guard
//// procedure whose clauses are all test-only (body empty or `true`; guards drawn
//// from the builtin subset or recursively test-only) compiles to a side table of
//// clause specs, evaluated three-valued at runtime by the `Guard` opcode handler
//// (suspension on unbound readers). §1.14 ruling recorded in
//// specs/049-wave1-guard-link-acceptance/spec.md Clarifications (2026-07-08);
//// design: specs/049-wave1-guard-link-acceptance/evidence/guard/a1-design.md.
////
//// Dart source of truth: glp_runtime/lib/bytecode/guard_defs.dart

import gleam/int
import gleam/list
import glp/runtime/terms.{type Constant}

/// Neutral term shape for guard clause specs. Lists are encoded as '.'/2
/// structures with a 'nil' terminator, matching the runtime representation.
pub type GTerm {
  GConst(value: Constant)
  /// Anonymous variables ('_'-prefixed) match anything and bind nothing;
  /// each occurrence is independent (see `is_anonymous`).
  GVar(name: String, is_reader: Bool)
  GStruct(functor: String, args: List(GTerm))
}

/// Anonymous-variable test for a `GVar` name (Dart `GVar.isAnonymous`).
pub fn is_anonymous(name: String) -> Bool {
  case name {
    "_" <> _ -> True
    _ -> False
  }
}

/// One guard conjunct of a test-only clause: a builtin-subset guard
/// (ground/1, known/1, =?=/2 — negation per the builtin's own verdict matrix)
/// or a recursive call to another runtime-defined guard (never negated).
pub type GGuardSpec {
  GGuardSpec(predicate: String, args: List(GTerm), negated: Bool)
}

/// "predicate/arity" key of a guard conjunct (Dart `GGuardSpec.key`).
pub fn conjunct_key(spec: GGuardSpec) -> String {
  guard_key(spec.predicate, list.length(spec.args))
}

/// One clause of a runtime-defined guard: head argument patterns + guard
/// conjuncts. Test-only by construction (body empty or `true`), so no body
/// is represented.
pub type GuardClauseSpec {
  GuardClauseSpec(head_args: List(GTerm), guards: List(GGuardSpec))
}

/// A runtime-defined guard procedure (keyed as "name/arity" in the
/// `BytecodeProgram` defined-guards side table).
pub type GuardProcSpec {
  GuardProcSpec(name: String, arity: Int, clauses: List(GuardClauseSpec))
}

/// The side-table key "name/arity" (Dart `GuardProcSpec.key`).
pub fn spec_key(spec: GuardProcSpec) -> String {
  guard_key(spec.name, spec.arity)
}

/// Build a "name/arity" side-table key.
pub fn guard_key(name: String, arity: Int) -> String {
  name <> "/" <> int.to_string(arity)
}
