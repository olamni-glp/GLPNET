//// glp/analysis/prelude — prelude knowledge shared by the load pipeline
//// (feature 050; started for T014, extended by the type-checker port T018).
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/prelude.dart,
//// ported 1:1 (D4 discipline: no additions): `builtinProcedures` (procedures
//// implemented by the runtime with NO GLP clauses — the parser consults it so
//// a declaration-only builtin does not trigger the "declaration has no
//// clauses" parse error), `predefinedTypeNames`/`predefinedProcedureNames`
//// (not redefinable by user modules; library-level types like DiffList and
//// Channel are deliberately NOT protected), and `builtinGoals` (goals that
//// skip type checking). The Dart `typePrelude` constant is the empty string —
//// all prelude definitions live in programs/self.glp via the scope chain.

/// True builtins: procedures implemented by the runtime with no GLP clauses
/// (Dart `builtinProcedures`, ported 1:1 — D4 discipline: no additions).
pub fn is_builtin_procedure(name_arity: String) -> Bool {
  case name_arity {
    // Type guards
    "integer/1" -> True
    "number/1" -> True
    "string/1" -> True
    // FR-033/SC-005 (OQ-G3): paper-kernel synonym of `string/1`
    "atom/1" -> True
    "constant/1" -> True
    "compound/1" -> True
    "tuple/1" -> True
    "list/1" -> True
    "is_list/1" -> True
    "module/1" -> True
    // Groundness/validation guards
    "ground/1" -> True
    "known/1" -> True
    "unknown/1" -> True
    "no_readers/1" -> True
    // Time guards
    "wait/1" -> True
    "wait_until/1" -> True
    // Arithmetic comparison guards
    "</2" -> True
    ">/2" -> True
    "=</2" -> True
    ">=/2" -> True
    "=:=/2" -> True
    "=\\=/2" -> True
    // Standard-order term comparison (T011/FR-037, OQ-G1)
    "@</2" -> True
    "@>/2" -> True
    "@=</2" -> True
    "@>=/2" -> True
    // Structural equality guard
    "=?=/2" -> True
    // 049 form (b) system guard primitive (§1.14 ruling 2026-07-08)
    "satisfiable/2" -> True
    // Univ operations
    "=../2" -> True
    "..=/2" -> True
    // MWM (Mutual Write Merge) runtime primitives
    "_allocate_mutual_reference/2" -> True
    "is_mutual_ref/1" -> True
    "_stream_append/3" -> True
    "_close_mutual_reference/1" -> True
    // madGLP network primitives
    "_send/3" -> True
    // Output (system predicate)
    "_output/1" -> True
    // UI output (system predicate; madGLP-spec §12.5). Separate sink from _output
    // (§12.6). Kernel approved by Gabi 2026-07-20 under Language Authority §1.14.
    "_send_to_ui/1" -> True
    // Link-layer host kernels (feature 025; rulings OQ-A4/C1/C2)
    "_link_setup/5" -> True
    "_link_send/3" -> True
    "_link_request/5" -> True
    "_link_listen/3" -> True
    "_link_accept/5" -> True
    "_link_monitor/2" -> True
    "_link_close/2" -> True
    _ -> False
  }
}

/// Predefined types that cannot be redefined by user modules (Dart
/// `predefinedTypeNames` / `isPredefinedType`) — only fundamental primitives
/// are protected.
pub fn is_predefined_type(name: String) -> Bool {
  case name {
    // Primitive builtins
    "Number" | "Integer" | "Real" | "String" -> True
    // Constant (Number ; String)
    "Constant" -> True
    // Arithmetic expression type
    "Exp" -> True
    // Fundamental collection types
    "Stream" | "OpenStream" -> True
    // DiffList, Channel are NOT protected — library-level.
    _ -> False
  }
}

/// Predefined procedures (by bare name) that cannot be redefined by user
/// modules (Dart `predefinedProcedureNames` / `isPredefinedProcedure`) —
/// only fundamental runtime guards/operations; library-level operations
/// (dl_append, dl_to_list, new_channel, send, receive) are NOT protected.
pub fn is_predefined_procedure(name: String) -> Bool {
  case name {
    // Type guards
    "integer" | "number" | "string" | "atom" | "constant" | "compound"
    | "tuple" | "list" | "is_list" | "module" -> True
    // Groundness guards
    "ground" | "known" | "unknown" | "no_readers" -> True
    // Time guards
    "wait" | "wait_until" -> True
    // Comparison guards
    "<" | ">" | "=<" | ">=" | "=:=" | "=\\=" -> True
    // Standard-order term comparison (T011/FR-037, OQ-G1)
    "@<" | "@>" | "@=<" | "@>=" -> True
    // Equality
    "=?=" -> True
    // Univ operations
    "=.." | "..=" -> True
    _ -> False
  }
}

/// Built-in goals that don't need type checking (Dart `builtinGoals` /
/// `isBuiltinGoal`): 0-arity control (`true`, `otherwise`) and `:=`
/// (arithmetic assignment, handled specially). `#` (remote module call) is
/// handled as a RemoteGoal before the builtin check.
pub fn is_builtin_goal(name: String) -> Bool {
  case name {
    "true" | "otherwise" | ":=" -> True
    _ -> False
  }
}
