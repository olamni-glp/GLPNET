//// glp/runtime/suspension — shared suspension records + the activation hand-off.
////
//// F4 owns suspension STORAGE + activation-list PRODUCTION only — it never
//// consumes/schedules the goals (that is the F5 runner). `armed` mirrors the Dart
//// `armed`/`disarm()` guard against double-activation (data-model §4 / R-007).
////
//// Dart source of truth: glp_runtime/lib/runtime/suspension.dart

/// A suspended goal recorded on an unbound writer. `armed` collapses Dart's nullable
/// `goalId` (null == disarmed): an armed record can activate exactly once.
pub type Suspension {
  Suspension(goal_id: Int, resume_pc: Int, armed: Bool)
}

/// The activation hand-off to the future F5 scheduler (← machine_state.dart GoalRef).
pub type GoalRef {
  GoalRef(goal_id: Int, resume_pc: Int)
}
